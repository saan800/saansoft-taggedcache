namespace SaanSoft.TaggedCache.OutputCache;

public class TaggedOutputCacheStoreOptions
{
    private string _cacheKeyPrefix = "outputcache:";

    /// <summary>
    /// Prefix for all output cache items, so can easily differentiate between normal object cache and output cache.
    /// Set to null or "" for no prefix.
    ///
    /// Prefix will be normalised in NormalizeTag along with rest of the tag.
    /// </summary>
    /// <remarks>
    /// Default: "outputcache:"
    /// </remarks>
    public string CacheKeyPrefix
    {
        get => _cacheKeyPrefix;
        set
        {
            var val = value.Trim();
            if (string.IsNullOrWhiteSpace(val))
            {
                _cacheKeyPrefix = "";
                return;
            }

            _cacheKeyPrefix = val;
        }
    }
}
