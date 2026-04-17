namespace SaanSoft.TaggedCache.Base;

public sealed record ResolvedExpiry(
    DateTimeOffset ExpiresAtUtc,
    DateTimeOffset? AbsoluteExpiresAtUtc,
    TimeSpan? SlidingExpiration);
