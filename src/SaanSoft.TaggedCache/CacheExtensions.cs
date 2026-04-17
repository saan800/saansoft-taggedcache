using Microsoft.Extensions.Caching.Distributed;

namespace SaanSoft.TaggedCache;

public static class CacheExtensions
{
    ///// <summary>
    ///// Get the item from cache if it exists and is not expired.
    ///// If the item does not exist (or is expired) then the factory function will be called to create the item, which will then be cached and returned.
    ///// </summary>
    public static async Task<T?> GetOrCreateAsync<T>(
        this ITaggedCache cache,
        string cacheKey,
        Func<CancellationToken, Task<T?>> factory,
        DistributedCacheEntryOptions? options = null,
        IReadOnlyCollection<string>? tags = null,
        CancellationToken ct = default)
    {
        var result = await cache.GetAsync<T>(cacheKey, ct);
        if (result != null)
            return result;

        result = await factory(ct);
        if (result == null)
            return default;

        await cache.SetAsync(cacheKey, result, options, tags, ct);
        return result;
    }
}
