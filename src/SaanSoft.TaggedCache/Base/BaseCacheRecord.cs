namespace SaanSoft.TaggedCache.Base;

public abstract class BaseCacheRecord<TPayload>
{
    public required string CacheKey { get; init; }
    public required TPayload Payload { get; init; }
    public required DateTimeOffset ExpiresAtUtc { get; init; }
    public DateTimeOffset? AbsoluteExpiresAtUtc { get; init; }
    public TimeSpan? SlidingExpiration { get; init; }
    public required string[] Tags { get; init; }

    public abstract string PayloadAsString();
}
