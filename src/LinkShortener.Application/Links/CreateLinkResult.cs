namespace LinkShortener.Application.Links;

public sealed record CreateLinkResult(
    Link? Link,
    string? ManagementKey,
    string? ErrorMessage,
    int? ErrorStatusCode)
{
    public bool IsSuccess => Link is not null;

    public static CreateLinkResult Success(Link link, string managementKey)
    {
        return new CreateLinkResult(link, managementKey, null, null);
    }

    public static CreateLinkResult ValidationFailure(string errorMessage)
    {
        return new CreateLinkResult(null, null, errorMessage, 400);
    }

    public static CreateLinkResult CodeConflict()
    {
        return new CreateLinkResult(
            null,
            null,
            "O código informado já está em uso.",
            409);
    }

    public static CreateLinkResult CodeUnavailable()
    {
        return new CreateLinkResult(
            null,
            null,
            "Não foi possível gerar um código disponível. Tente novamente.",
            503);
    }
}
