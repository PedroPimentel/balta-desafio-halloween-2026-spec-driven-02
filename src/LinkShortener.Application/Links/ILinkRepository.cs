namespace LinkShortener.Application.Links;

public interface ILinkRepository
{
    Task<bool> TryAddAsync(Link link, CancellationToken cancellationToken);
    Task<Link?> GetByCodeAsync(string code, CancellationToken cancellationToken);
    Task<bool> IncrementAccessCountAsync(string code, CancellationToken cancellationToken);
}
