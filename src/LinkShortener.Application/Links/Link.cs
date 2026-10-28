namespace LinkShortener.Application.Links;

public sealed class Link
{
    private Link()
    {
    }

    public Link(
        string code,
        string targetUrl,
        DateTime createdAtUtc,
        DateTime? expiresAtUtc,
        string? managementKeyHash = null)
    {
        Code = code;
        TargetUrl = targetUrl;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        ManagementKeyHash = managementKeyHash;
    }

    public int Id { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string TargetUrl { get; private set; } = string.Empty;

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? ExpiresAtUtc { get; private set; }

    public long AccessCount { get; private set; }

    public string? ManagementKeyHash { get; private set; }

    public bool IsExpired(DateTime utcNow)
    {
        return ExpiresAtUtc.HasValue && ExpiresAtUtc.Value <= utcNow;
    }
}
