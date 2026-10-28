using System.Security.Cryptography;
using System.Text;

namespace LinkShortener.Application.Links;

public sealed class LinkService(
    ILinkRepository linkRepository,
    ILinkCache linkCache,
    ILinkCodeGenerator codeGenerator,
    TargetUrlValidator targetUrlValidator,
    IManagementKeyGenerator managementKeyGenerator)
{
    private const int MaximumCodeGenerationAttempts = 5;

    public Task<CreateLinkResult> CreateAsync(
        string? targetUrl,
        CancellationToken cancellationToken = default)
    {
        return CreateAsync(targetUrl, null, cancellationToken);
    }

    public async Task<CreateLinkResult> CreateAsync(
        string? targetUrl,
        string? customCode,
        CancellationToken cancellationToken = default)
    {
        if (customCode is not null && !IsValidCustomCode(customCode))
        {
            return CreateLinkResult.ValidationFailure(
                "O código personalizado deve ter de 4 a 20 caracteres e conter apenas letras maiúsculas de A a Z e dígitos de 1 a 9.");
        }

        var validationResult = await targetUrlValidator.ValidateAsync(
            targetUrl,
            cancellationToken);

        if (!validationResult.IsValid)
        {
            return CreateLinkResult.ValidationFailure(validationResult.ErrorMessage!);
        }

        if (customCode is not null)
        {
            var managementKey = managementKeyGenerator.Generate();
            var customLink = new Link(
                customCode,
                targetUrl!,
                DateTime.UtcNow,
                null,
                HashManagementKey(managementKey));

            return await linkRepository.TryAddAsync(customLink, cancellationToken)
                ? CreateLinkResult.Success(customLink, managementKey)
                : CreateLinkResult.CodeConflict();
        }

        for (var attempt = 0; attempt < MaximumCodeGenerationAttempts; attempt++)
        {
            var managementKey = managementKeyGenerator.Generate();
            var link = new Link(
                codeGenerator.Generate(),
                targetUrl!,
                DateTime.UtcNow,
                null,
                HashManagementKey(managementKey));

            if (await linkRepository.TryAddAsync(link, cancellationToken))
            {
                return CreateLinkResult.Success(link, managementKey);
            }
        }

        return CreateLinkResult.CodeUnavailable();
    }

    public async Task<LinkStatisticsResult> GetStatisticsAsync(
        string code,
        string? managementKey,
        CancellationToken cancellationToken)
    {
        var link = await linkRepository.GetByCodeAsync(code, cancellationToken);
        if (link is null)
        {
            return LinkStatisticsResult.NotFound();
        }

        if (!IsValidManagementKey(link.ManagementKeyHash, managementKey))
        {
            return LinkStatisticsResult.Unauthorized();
        }

        return LinkStatisticsResult.Success(link.AccessCount);
    }

    private static bool IsValidManagementKey(string? expectedHash, string? managementKey)
    {
        if (string.IsNullOrEmpty(expectedHash) || string.IsNullOrEmpty(managementKey))
        {
            return false;
        }

        var actualHash = HashManagementKey(managementKey);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(expectedHash),
            Encoding.ASCII.GetBytes(actualHash));
    }

    private static string HashManagementKey(string managementKey)
    {
        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(managementKey)));
    }

    public async Task<RedirectLinkResult> RedirectAsync(string code, CancellationToken cancellationToken)
    {
        var link = linkCache.Get(code);
        var isCacheMiss = link is null;
        if (isCacheMiss)
        {
            link = await linkRepository.GetByCodeAsync(code, cancellationToken);
        }

        if (link is null)
        {
            return RedirectLinkResult.NotFound();
        }

        if (link.IsExpired(DateTime.UtcNow))
        {
            return RedirectLinkResult.Expired();
        }

        if (isCacheMiss)
        {
            linkCache.Set(link);
        }

        await linkRepository.IncrementAccessCountAsync(code, cancellationToken);
        return RedirectLinkResult.Redirect(link.TargetUrl);
    }

    private static bool IsValidCustomCode(string code)
    {
        return code.Length is >= 4 and <= 20
            && code.All(character =>
                character is >= 'A' and <= 'Z'
                or >= '1' and <= '9');
    }
}

public sealed record LinkStatisticsResult(long? AccessCount, bool IsNotFound, bool IsUnauthorized)
{
    public static LinkStatisticsResult Success(long accessCount)
    {
        return new LinkStatisticsResult(accessCount, false, false);
    }

    public static LinkStatisticsResult NotFound()
    {
        return new LinkStatisticsResult(null, true, false);
    }

    public static LinkStatisticsResult Unauthorized()
    {
        return new LinkStatisticsResult(null, false, true);
    }
}

public sealed record RedirectLinkResult(string? TargetUrl, bool IsNotFound, bool IsExpired)
{
    public static RedirectLinkResult Redirect(string targetUrl)
    {
        return new RedirectLinkResult(targetUrl, false, false);
    }

    public static RedirectLinkResult NotFound()
    {
        return new RedirectLinkResult(null, true, false);
    }

    public static RedirectLinkResult Expired()
    {
        return new RedirectLinkResult(null, false, true);
    }
}
