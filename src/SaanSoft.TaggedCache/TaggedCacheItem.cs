namespace SaanSoft.TaggedCache;

public sealed class TaggedCacheItem<T>
{
    public required string Key { get; init; }
    public required T Value { get; init; }

    /// <summary>
    /// Tags specific to this item
    /// </summary>
    public IEnumerable<string>? Tags { get; init; }
}
