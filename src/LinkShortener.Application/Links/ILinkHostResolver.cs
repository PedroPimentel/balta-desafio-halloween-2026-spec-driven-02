using System.Net;

namespace LinkShortener.Application.Links;

public interface ILinkHostResolver
{
    Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken);
}
