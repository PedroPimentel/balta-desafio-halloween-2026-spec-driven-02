using System.Net;

namespace LinkShortener.Application.Links;

public sealed class DnsLinkHostResolver : ILinkHostResolver
{
    public Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken)
    {
        return Dns.GetHostAddressesAsync(host, cancellationToken);
    }
}
