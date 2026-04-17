using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace SaanSoft.TaggedCache;

public interface ITaggedCacheOptions
{
    /// <summary>
    /// The fraction of the sliding expiration duration at which to refresh the cache entry.
    /// For example, if the sliding expiration is 1 hour and the threshold fraction is 0.25, then the cache entry will be refreshed when it has 15 minutes left before it expires.
    /// </summary>
    /// <remarks>
    /// Used to balance between refreshing cache entries too early (which can cause unnecessary load on the cache and underlying data source) and refreshing them too late (which can lead to cache misses and stale data).
    /// Default: 0.25 (refresh when 25% of the sliding expiration duration is left)
    /// Must be greater than 0 and less than or equal to 1.
    /// </remarks>
    decimal SlidingRefreshThresholdFraction { get; }

    /// <summary>
    /// Prefix for all tag cache items, so can easily differentiate between normal object cache and tag cache.
    /// Set to null or "" for no prefix.
    ///
    /// Prefix will be normalised in NormalizeTag along with rest of the tag.
    /// </summary>
    /// <remarks>
    /// Default: "tag:"
    /// </remarks>
    string TagKeyPrefix { get; }

    /// <summary>
    /// Default cache options to use when adding cache entries if no options are provided.
    /// This is used to ensure that cache entries have a reasonable default expiry time if the caller does not specify one.
    /// </summary>
    /// <remarks>
    /// Default: AbsoluteExpirationRelativeToNow = 5 minutes
    /// </remarks>
    DistributedCacheEntryOptions DefaultCacheOptions { get; }

    /// <summary>
    /// Json serialisation options for when reading and writing cache entries
    /// </summary>
    /// <remarks>
    /// Default: JsonSerializerOptions(JsonSerializerDefaults.Web)
    /// </remarks>
    JsonSerializerOptions JsonSerializerOptions { get; }
}
