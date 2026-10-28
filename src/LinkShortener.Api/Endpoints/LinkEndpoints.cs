using LinkShortener.Application.Links;
using Microsoft.AspNetCore.Mvc;

namespace LinkShortener.Api.Endpoints;

public static class LinkEndpoints
{
    public static IEndpointRouteBuilder MapLinkEndpoints(
        this IEndpointRouteBuilder endpoints,
        string shortLinkBaseUrl)
    {
        endpoints.MapPost(
                "/api/links",
                async (
                    CreateLinkRequest request,
                    LinkService linkService,
                    CancellationToken cancellationToken) =>
                {
                    var result = await linkService.CreateAsync(
                        request.TargetUrl,
                        request.Code,
                        cancellationToken);

                    if (!result.IsSuccess)
                    {
                        return Results.Problem(
                            statusCode: result.ErrorStatusCode,
                            title: result.ErrorStatusCode switch
                            {
                                StatusCodes.Status400BadRequest => "Erro de validação",
                                StatusCodes.Status409Conflict => "Código em uso",
                                _ => "Serviço temporariamente indisponível"
                            },
                            detail: result.ErrorMessage);
                    }

                    var link = result.Link!;
                    var shortUrl = $"{shortLinkBaseUrl.TrimEnd('/')}/{link.Code}";

                    return Results.Created(
                        shortUrl,
                        new CreateLinkResponse(shortUrl, link.Code, result.ManagementKey!));
                })
            .WithName("CreateLink")
            .WithSummary("Cria um link curto")
            .WithDescription("Valida a URL de destino e cria um link com código automático ou personalizado.")
            .Produces<CreateLinkResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        endpoints.MapGet(
                "/api/links/{code}/stats",
                async (
                    string code,
                    [FromHeader(Name = "X-Management-Key")] string? managementKey,
                    LinkService linkService,
                    CancellationToken cancellationToken) =>
                {
                    var result = await linkService.GetStatisticsAsync(
                        code,
                        managementKey,
                        cancellationToken);

                    if (result.IsNotFound)
                    {
                        return Results.NotFound();
                    }

                    if (result.IsUnauthorized)
                    {
                        return Results.Problem(
                            statusCode: StatusCodes.Status401Unauthorized,
                            title: "Não autorizado",
                            detail: "A chave de gerenciamento está ausente ou é inválida.");
                    }

                    return Results.Ok(new LinkStatisticsResponse(code, result.AccessCount!.Value));
                })
            .WithName("GetLinkStatistics")
            .WithSummary("Consulta as estatísticas de acesso de um link")
            .WithDescription("Retorna o total de acessos usando a chave de gerenciamento recebida na criação do link.")
            .Produces<LinkStatisticsResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        endpoints.MapGet(
                "/{code}",
                async (string code, LinkService linkService, CancellationToken cancellationToken) =>
                {
                    var result = await linkService.RedirectAsync(code, cancellationToken);

                    if (result.IsNotFound)
                    {
                        return Results.NotFound();
                    }

                    if (result.IsExpired)
                    {
                        return Results.StatusCode(StatusCodes.Status410Gone);
                    }

                    return Results.Redirect(result.TargetUrl!);
                })
            .WithName("RedirectLink")
            .WithSummary("Redireciona para a URL de destino")
            .WithDescription("Resolve o código curto e redireciona para o destino ou retorna erro quando não existe ou expirou.")
            .Produces(StatusCodes.Status302Found)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status410Gone);

        return endpoints;
    }

    public sealed record CreateLinkRequest(string? TargetUrl, string? Code = null);

    /// <summary>Response returned after creating a short link.</summary>
    public sealed record CreateLinkResponse(string ShortUrl, string Code, string ManagementKey);

    /// <summary>Access statistics for a short link.</summary>
    public sealed record LinkStatisticsResponse(string Code, long AccessCount);
}
