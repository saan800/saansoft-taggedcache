using Microsoft.Extensions.Caching.Distributed;

namespace SaanSoft.TaggedCache;

public interface ITaggedCache : IDistributedCache
{
    /// <summary>
    /// Get the item from cache if it exists and is not expired.
    /// Returns null if the item doesn't exist or is expired.
    /// </summary>
    /// <remarks>
    /// Does not throw on expiry, it will just return null and clean up the expired item and any associated tags.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown when the cache key is null or empty.</exception>
    Task<T?> GetAsync<T>(string cacheKey, CancellationToken ct = default);

    /// <summary>
    /// Bulk / batch get items from cache if they exists and are not expired.
    /// Returns a dictionary values for the given keys.
    /// If the value for a key is not found in the cache (or expired), the value will be null in the resulting dictionary
    /// </summary>
    /// <remarks>
    /// Does not throw on expiry, it will just return null and clean up the expired item(s) and any associated tags.
    /// </remarks>
    Task<Dictionary<string, T?>> GetManyAsync<T>(IReadOnlyCollection<string> cacheKeys, CancellationToken ct = default);

    /// <summary>
    /// Get the raw payload of the cached item as a string if it exists and is not expired.
    /// Returns null if the item doesn't exist or is expired.
    /// </summary>
    Task<string?> GetPayloadAsync(string cacheKey, CancellationToken ct = default);

    /// <summary>
    /// Refresh the cached item expiry date(s) if it exists and is not expired.
    /// Resets the expiry information for the item based on the provided options (or default options if not provided).
    /// </summary>
    Task RefreshAsync(string cacheKey, DistributedCacheEntryOptions? options = null, CancellationToken ct = default);

    /// <summary>
    /// Add/replace an item in the cache with the specified cache key and options.
    /// If an item with the same cache key already exists, it will be replaced with a new expiry time and tags (if provided).
    /// </summary>
    /// <remarks>
    /// If options are not provided, it will use the default options configured for the cache implementation, which would include a default expiry time and (probably) no tags.
    /// </remarks>
    Task SetAsync<T>(string cacheKey, T obj, DistributedCacheEntryOptions? options = null, IReadOnlyCollection<string>? tags = null, CancellationToken ct = default);

    /// <summary>
    /// Add/replace an item in the cache with the specified cache key and options.
    /// If an item with the same cache key already exists, it will be replaced with a new expiry time and tags (if provided).
    /// </summary>
    /// <remarks>
    /// If options are not provided, it will use the default options configured for the cache implementation, which would include a default expiry time and (probably) no tags.
    /// </remarks>
    Task SetAsync<T>(TaggedCacheItem<T> item, DistributedCacheEntryOptions? options = null, CancellationToken ct = default);

    /// <summary>
    /// Bulk / batch function to add/replace many items in the cache.
    /// If an item with the same cache key already exists, it will be replaced with a new expiry time and tags (if provided).
    /// </summary>
    /// <remarks>
    /// If options are not provided, it will use the default options configured for the cache implementation, which would include a default expiry time and (probably) no tags.
    /// </remarks>
    Task SetManyAsync<T>(IReadOnlyCollection<TaggedCacheItem<T>> items, DistributedCacheEntryOptions? options = null, CancellationToken ct = default);

    /// <summary>
    /// Remove cached item (and associated tags) by cache key. If the item doesn't exist, this method does nothing.
    /// </summary>
    new Task RemoveAsync(string cacheKey, CancellationToken ct = default);

    /// <summary>
    /// Bulk / batch removal of cached items (and associated tags) by cache keys
    /// </summary>
    Task RemoveManyAsync(IReadOnlyCollection<string> cacheKeys, CancellationToken ct = default);

    /// <summary>
    /// Remove cached items by tag. All items with the specified tag will be removed from the cache.
    /// </summary>
    Task RemoveByTagAsync(string tag, CancellationToken ct = default);

    /// <summary>
    /// Remove cached items by tags. All items with at least one tag will be removed from the cache.
    /// </summary>
    Task RemoveByTagsAsync(IReadOnlyCollection<string> tags, CancellationToken ct = default);
}
