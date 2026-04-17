using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace SaanSoft.TaggedCache.Base;

public abstract class BaseTaggedCache<TCacheRecord, TPayload>(ITaggedCacheOptions cacheOptions) : ITaggedCache
    where TCacheRecord : BaseCacheRecord<TPayload>
{
    /// <remarks>
    /// WARNING: Uses <c>.GetAwaiter().GetResult()</c> to satisfy the synchronous <see cref="IDistributedCache"/> contract.
    /// This can cause deadlocks in environments with an active <c>SynchronizationContext</c> (e.g. classic ASP.NET, WPF, Blazor Server).
    /// Prefer <see cref="GetAsync"/> wherever possible.
    /// </remarks>
    public virtual byte[]? Get(string cacheKey)
        => GetAsync(cacheKey).GetAwaiter().GetResult();

    public virtual async Task<byte[]?> GetAsync(string cacheKey, CancellationToken ct = default)
    {
        var payload = await GetPayloadAsync(cacheKey, ct);
        return payload?.AsByteArray();
    }

    public virtual async Task<T?> GetAsync<T>(string cacheKey, CancellationToken ct = default)
    {
        var payload = await GetPayloadAsync(cacheKey, ct);
        if (string.IsNullOrWhiteSpace(payload))
            return default;

        return JsonSerializer.Deserialize<T>(payload, cacheOptions.JsonSerializerOptions);
    }

    public virtual async Task<Dictionary<string, T?>> GetManyAsync<T>(IReadOnlyCollection<string> cacheKeys, CancellationToken ct = default)
    {
        var normalizedKeys = cacheKeys.Select(key => NormalizeCacheKey(key)).ToArray();
        var cachedRecords = await GetManyRecordsInternalAsync(normalizedKeys, ct);

        var results = new Dictionary<string, T?>();
        foreach( var kvp in cachedRecords)
        {
            if (kvp.Value == null)
                results.Add(kvp.Key, default);
            else
                results.Add(kvp.Key, JsonSerializer.Deserialize<T>(kvp.Value.PayloadAsString(), cacheOptions.JsonSerializerOptions));
        }
        return results;
    }

    public virtual async Task<string?> GetPayloadAsync(string cacheKey, CancellationToken ct = default)
    {
        var normalizedCacheKey = NormalizeCacheKey(cacheKey);

        var record = await GetRecordInternalAsync(normalizedCacheKey, ct);
        if (record is null)
            return null;

        var nowUtc = DateTimeOffset.UtcNow;
        if (record.ExpiresAtUtc <= nowUtc)
        {
            await RemoveAsync(cacheKey, ct);
            return null;
        }

        await TryRefreshSlidingAsync(record, nowUtc, ct);

        return record.PayloadAsString();
    }

    /// <remarks>
    /// WARNING: Uses <c>.GetAwaiter().GetResult()</c> to satisfy the synchronous <see cref="IDistributedCache"/> contract.
    /// This can cause deadlocks in environments with an active <c>SynchronizationContext</c> (e.g. classic ASP.NET, WPF, Blazor Server).
    /// Prefer <see cref="SetAsync"/> wherever possible.
    /// Use <see cref="ITaggedCache.SetAsync{T}(string, T, DistributedCacheEntryOptions?, IReadOnlyCollection{string}?, CancellationToken)"/> directly for tagged entries.
    /// </remarks>
    public virtual void Set(string cacheKey, byte[] value, DistributedCacheEntryOptions options)
        => SetAsync(cacheKey, value, options, null).GetAwaiter().GetResult();

    public virtual Task SetAsync(string cacheKey, byte[] value, DistributedCacheEntryOptions options, CancellationToken ct = default)
        => SetAsync(cacheKey, value.AsString(), options, null, ct);

    public virtual Task SetAsync<T>(TaggedCacheItem<T> item, DistributedCacheEntryOptions? options = null, CancellationToken ct = default)
        => SetAsync(item.Key, item.Value, options, item.Tags?.ToList(), ct);

    public abstract Task SetAsync<T>(string cacheKey, T obj, DistributedCacheEntryOptions? options = null, IReadOnlyCollection<string>? tags = null, CancellationToken ct = default);

    public virtual async Task SetManyAsync<T>(IReadOnlyCollection<TaggedCacheItem<T>> items, DistributedCacheEntryOptions? options = null, CancellationToken ct = default)
    {
        var tasks = items.Select(async item =>
        {
            await SetAsync<T>(item.Key, item.Value, options, item.Tags?.ToList(), ct);
        });
        await Task.WhenAll(tasks);
    }

    /// <remarks>
    /// WARNING: Uses <c>.GetAwaiter().GetResult()</c> to satisfy the synchronous <see cref="IDistributedCache"/> contract.
    /// This can cause deadlocks in environments with an active <c>SynchronizationContext</c> (e.g. classic ASP.NET, WPF, Blazor Server).
    /// Prefer <see cref="RefreshAsync(string, CancellationToken)"/> wherever possible.
    /// </remarks>
    public virtual void Refresh(string cacheKey)
        => RefreshAsync(cacheKey, null).GetAwaiter().GetResult();

    public virtual Task RefreshAsync(string cacheKey, CancellationToken ct = default)
        => RefreshAsync(cacheKey, null, ct);

    public virtual async Task RefreshAsync(string cacheKey, DistributedCacheEntryOptions? options = null, CancellationToken ct = default)
    {
        var normalizedCacheKey = NormalizeCacheKey(cacheKey);

        var record = await GetRecordInternalAsync(normalizedCacheKey, ct);
        if (record is null)
            return;

        var nowUtc = DateTimeOffset.UtcNow;
        if (record.ExpiresAtUtc <= nowUtc)
        {
            await RemoveRecordInternalAsync(normalizedCacheKey, record, ct);
            return;
        }

        var sliding = options?.SlidingExpiration ?? record.SlidingExpiration;
        var absolute = options?.AbsoluteExpiration ?? record.AbsoluteExpiresAtUtc;

        DateTimeOffset newExpiresAtUtc;
        if (sliding.HasValue)
        {
            newExpiresAtUtc = nowUtc.Add(sliding.Value);

            if (absolute.HasValue && newExpiresAtUtc > absolute.Value)
                newExpiresAtUtc = absolute.Value;
        }
        else if (absolute.HasValue)
        {
            newExpiresAtUtc = absolute.Value;
        }
        else
        {
            newExpiresAtUtc = record.ExpiresAtUtc;
        }

        if (newExpiresAtUtc <= nowUtc)
        {
            await RemoveRecordInternalAsync(normalizedCacheKey, record, ct);
            return;
        }

        await UpdateExpiryInternalAsync(normalizedCacheKey, newExpiresAtUtc, absolute, sliding, ct);
    }

    /// <remarks>
    /// WARNING: Uses <c>.GetAwaiter().GetResult()</c> to satisfy the synchronous <see cref="IDistributedCache"/> contract.
    /// This can cause deadlocks in environments with an active <c>SynchronizationContext</c> (e.g. classic ASP.NET, WPF, Blazor Server).
    /// Prefer <see cref="RemoveAsync"/> wherever possible.
    /// </remarks>
    public virtual void Remove(string cacheKey)
        => RemoveAsync(cacheKey).GetAwaiter().GetResult();

    public virtual async Task RemoveAsync(string cacheKey, CancellationToken ct = default)
    {
        var normalizedCacheKey = NormalizeCacheKey(cacheKey);

        var existing = await GetRecordInternalAsync(normalizedCacheKey, ct);
        await RemoveRecordInternalAsync(normalizedCacheKey, existing, ct);
    }

    public virtual async Task RemoveManyAsync(IReadOnlyCollection<string> cacheKeys, CancellationToken ct = default)
    {
        var normalizedKeys = cacheKeys.Select(key => NormalizeCacheKey(key)).ToList();
        var cachedRecords = (await GetManyRecordsInternalAsync(normalizedKeys, ct))
            .Where(kvp => kvp.Value != null)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        await RemoveManyRecordsInternalAsync(cachedRecords, ct);
    }

    public virtual async Task RemoveByTagAsync(string tag, CancellationToken ct = default)
    {
        var normalizedTags = NormalizeTag(tag);

        var cacheKeys = await GetCacheKeysForTagAsync(normalizedTags, ct);
        await RemoveManyAsync(cacheKeys, ct);
    }

    public virtual async Task RemoveByTagsAsync(IReadOnlyCollection<string> tags, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(tags);
        var normalizedTags = NormalizeTags(tags);

        var cacheKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var tag in normalizedTags)
        {
            var tempCacheKeys = await GetCacheKeysForTagAsync(tag, ct);
            foreach (var cacheKey in tempCacheKeys)
                cacheKeys.Add(cacheKey);
        }

        await RemoveManyAsync(cacheKeys, ct);
    }

    /// <remarks>
    /// Accepts an already-normalized key — avoids double-normalization on internal call paths
    /// </remarks>
    protected abstract Task<TCacheRecord?> GetRecordInternalAsync(string normalizedCacheKey, CancellationToken ct);

    /// <remarks>
    /// Accepts an already-normalized key — avoids double-normalization on internal call paths
    /// </remarks>
    protected virtual async Task<Dictionary<string, TCacheRecord?>> GetManyRecordsInternalAsync(IReadOnlyCollection<string> normalizedCacheKeys, CancellationToken ct)
    {
        if (normalizedCacheKeys == null || normalizedCacheKeys.Count == 0) return [];

        var getResults = await Task.WhenAll(
            normalizedCacheKeys.Select(async key => (key, value: await GetRecordInternalAsync(key, ct)))
        );
        return getResults.ToDictionary(x => x.key, x => x.value);
    }

    /// <summary>
    /// Add / update cache record. Adds new tags on the records and removes any old obsolete tags for the a pre-existing record.
    /// </summary>
    protected abstract Task UpsertRecordInternalAsync(TCacheRecord record, IReadOnlyCollection<string> obsoleteTags, CancellationToken ct);

    /// <summary>
    /// Updates the expiry time(s) on a cached record
    /// </summary>
    protected abstract Task UpdateExpiryInternalAsync(string normalizedCacheKey, DateTimeOffset expiresAtUtc, DateTimeOffset? absoluteExpiresAtUtc, TimeSpan? slidingExpiration, CancellationToken ct);

    /// <remarks>
    /// Accepts an already-normalized key — avoids double-normalization on internal call paths
    /// </remarks>
    protected abstract Task RemoveRecordInternalAsync(string normalizedCacheKey, TCacheRecord? existing, CancellationToken ct);

    protected virtual async Task RemoveManyRecordsInternalAsync(Dictionary<string, TCacheRecord?> cachedRecords, CancellationToken ct)
    {
        var tasks = cachedRecords.Select(async kvp => await RemoveRecordInternalAsync(kvp.Key, kvp.Value, ct));
        await Task.WhenAll(tasks);
    }

    protected virtual async Task<bool> TryRefreshSlidingAsync(TCacheRecord record, DateTimeOffset nowUtc, CancellationToken ct)
    {
        // check if we should refresh the cached item
        if (record.SlidingExpiration is null)
            return false;

        var remaining = record.ExpiresAtUtc - nowUtc;
        if (remaining <= TimeSpan.Zero)
            return false;

        var thresholdTicks =
            (long)(record.SlidingExpiration.Value.Ticks * cacheOptions.SlidingRefreshThresholdFraction);
        if (remaining.Ticks > thresholdTicks) return false;

        // work out the new expiry date, and make sure its not later than AbsoluteExpiresAtUtc (if its set)
        var newExpiresAtUtc = nowUtc.Add(record.SlidingExpiration.Value);

        if (record.AbsoluteExpiresAtUtc is { } absolute && newExpiresAtUtc > absolute)
            newExpiresAtUtc = absolute;

        if (newExpiresAtUtc <= record.ExpiresAtUtc)
            return false;

        await UpdateExpiryInternalAsync(record.CacheKey, newExpiresAtUtc, record.AbsoluteExpiresAtUtc, record.SlidingExpiration, ct);
        return true;
    }

    /// <summary>
    /// Get all the cacheKeys associated with a tag
    /// </summary>
    protected abstract Task<HashSet<string>> GetCacheKeysForTagAsync(string normalizedTag, CancellationToken ct);

    /// <summary>
    /// CacheKeys are trimmed and normalized before being stored for consistency.
    /// For example, "  USER:123  " and "user:123" are normalised to the same value and will be treated as the same cacheKey.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the cache key is null or empty.</exception>"
    protected virtual string NormalizeCacheKey(string? cacheKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheKey);
        return cacheKey.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Tags are trimmed and normalized before being stored for consistency.
    /// For example, "  USER:123  " and "user:123" are normalised to the same value and will be treated as the same tag.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the tag is null or empty.</exception>"
    protected virtual string NormalizeTag(string tag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);

        var prefix = (cacheOptions.TagKeyPrefix ?? "").Trim().ToLowerInvariant();
        var tagKey = $"{prefix}{tag.Trim().ToLowerInvariant()}";

        if (!string.IsNullOrWhiteSpace(prefix) && tagKey.StartsWith($"{prefix}{prefix}", StringComparison.OrdinalIgnoreCase))
        {
            tagKey = tagKey.Replace($"{prefix}{prefix}", prefix, StringComparison.OrdinalIgnoreCase);
        }
        return tagKey;
    }

    /// <summary>
    /// Tags are trimmed and normalized before being stored for consistency.
    /// For example, "  USER:123  " and "user:123" are normalised to the same value and will be treated as the same tag.
    /// </summary>
    protected string[] NormalizeTags(IEnumerable<string>? tags)
    {
        if (tags is null)
            return [];
        return tags
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => NormalizeTag(t))
            .Distinct()
            .ToArray();
    }
}
