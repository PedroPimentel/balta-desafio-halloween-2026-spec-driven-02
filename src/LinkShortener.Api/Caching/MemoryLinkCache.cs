using LinkShortener.Application.Links;
using Microsoft.Extensions.Caching.Memory;

namespace LinkShortener.Api.Caching;

public sealed class MemoryLinkCache(IMemoryCache memoryCache) : ILinkCache
{
    private static readonly TimeSpan SlidingExpiration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MaximumLifetime = TimeSpan.FromMinutes(30);

    public Link? Get(string code)
    {
        return memoryCache.Get<Link>(GetCacheKey(code));
    }

    public void Set(Link link)
    {
        var now = DateTimeOffset.UtcNow;
        var absoluteExpiration = now.Add(MaximumLifetime);

        if (link.ExpiresAtUtc is { } expiresAtUtc)
        {
            var linkExpiration = new DateTimeOffset(
                DateTime.SpecifyKind(expiresAtUtc, DateTimeKind.Utc));
            if (linkExpiration <= now)
            {
                return;
            }

            absoluteExpiration = linkExpiration < absoluteExpiration
                ? linkExpiration
                : absoluteExpiration;
        }

        memoryCache.Set(
            GetCacheKey(link.Code),
            link,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpiration = absoluteExpiration,
                SlidingExpiration = SlidingExpiration,
                Size = 1
            });
    }

    private static string GetCacheKey(string code)
    {
        return $"link:{code}";
    }
}
