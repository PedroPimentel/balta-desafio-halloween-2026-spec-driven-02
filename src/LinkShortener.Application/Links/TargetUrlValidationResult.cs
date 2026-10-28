namespace LinkShortener.Application.Links;

public sealed record TargetUrlValidationResult(bool IsValid, string? ErrorMessage)
{
    public static TargetUrlValidationResult Valid { get; } = new(true, null);

    public static TargetUrlValidationResult Invalid(string errorMessage)
    {
        return new TargetUrlValidationResult(false, errorMessage);
    }
}
