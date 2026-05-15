using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using SaanSoft.TaggedCache.Base;
using System.Collections.Concurrent;

namespace SaanSoft.TaggedCache.Memory;

public class MemoryTaggedCache(IMemoryCache memoryCache, MemoryTaggedCacheOptions cacheOptions) : BaseTaggedCache<MemoryCacheRecord, byte[]>(cacheOptions), ITaggedCache
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _tagIndex =
        new(StringComparer.Ordinal);

    public override async Task<byte[]?> GetAsync(string cacheKey, CancellationToken ct = default)
    {
        var normalizedCacheKey = NormalizeCacheKey(cacheKey);
        var record = await GetRecordInternalAsync(normalizedCacheKey, ct);
        return record?.Payload;
    }

    public override async Task SetAsync<T>(string cacheKey, T obj, DistributedCacheEntryOptions? options = null, IReadOnlyCollection<string>? tags = null, CancellationToken ct = default)
    {
        var normalizedCacheKey = NormalizeCacheKey(cacheKey);
        options ??= cacheOptions.DefaultCacheOptions;

        var existing = await GetRecordInternalAsync(normalizedCacheKey, ct);

        var nowUtc = DateTimeOffset.UtcNow;
        var resolved = options.Resolve(nowUtc);
        var payloadUtf8 = obj.AsByteArray(cacheOptions.JsonSerializerOptions);

        var newTags = NormalizeTags(tags);
        var obsoleteTags = (existing?.Tags ?? Array.Empty<string>()).Except(newTags, StringComparer.OrdinalIgnoreCase).ToList();

        var record = new MemoryCacheRecord
        {
            CacheKey = normalizedCacheKey,
            Payload = payloadUtf8,
            ExpiresAtUtc = resolved.ExpiresAtUtc,
            AbsoluteExpiresAtUtc = resolved.AbsoluteExpiresAtUtc,
            SlidingExpiration = resolved.SlidingExpiration,
            Tags = newTags
        };

        await UpsertRecordInternalAsync(record, obsoleteTags, ct);
    }

    public override async Task RemoveAsync(string cacheKey, CancellationToken ct = default)
    {
        var normalizedCacheKey = NormalizeCacheKey(cacheKey);

        if (memoryCache.TryGetValue<MemoryCacheRecord>(normalizedCacheKey, out var existing) && existing != null)
            await RemoveRecordInternalAsync(normalizedCacheKey, existing, ct);
    }

    protected override async Task<MemoryCacheRecord?> GetRecordInternalAsync(string normalizedCacheKey, CancellationToken ct)
    {
        if (!memoryCache.TryGetValue<MemoryCacheRecord>(normalizedCacheKey, out var record) || record is null)
            return null;

        var nowUtc = DateTimeOffset.UtcNow;
        if (record.ExpiresAtUtc <= nowUtc)
        {
            await RemoveRecordInternalAsync(normalizedCacheKey, record, ct);
            return null;
        }

        if (await TryRefreshSlidingAsync(record, nowUtc, ct))
        {
            if (memoryCache.TryGetValue<MemoryCacheRecord>(normalizedCacheKey, out var refreshed) && refreshed is not null)
                return refreshed;
        }

        return record;
    }

    protected override Task RemoveRecordInternalAsync(string normalizedCacheKey, MemoryCacheRecord? existing, CancellationToken ct)
    {
        if (existing != null)
        {
            lock (existing.Sync)
            {
                memoryCache.Remove(normalizedCacheKey);

                foreach (var tag in existing.Tags)
                {
                    if (_tagIndex.TryGetValue(tag, out var keys))
                    {
                        keys.TryRemove(normalizedCacheKey, out _);

                        if (keys.IsEmpty)
                            _tagIndex.TryRemove(tag, out _);
                    }
                }
            }
        }
        else
        {
            memoryCache.Remove(normalizedCacheKey);
        }
        return Task.CompletedTask;
    }

    protected override async Task UpdateExpiryInternalAsync(string normalizedCacheKey, DateTimeOffset expiresAtUtc, DateTimeOffset? absoluteExpiresAtUtc, TimeSpan? slidingExpiration, CancellationToken ct)
    {
        var record = await GetRecordInternalAsync(normalizedCacheKey, ct);
        if (record == null) return;

        if (expiresAtUtc <= DateTimeOffset.UtcNow)
        {
            await RemoveRecordInternalAsync(normalizedCacheKey, record, ct);
            return;
        }

        var refreshedRecord = new MemoryCacheRecord
        {
            CacheKey = record.CacheKey,
            Payload = record.Payload,
            ExpiresAtUtc = expiresAtUtc,
            AbsoluteExpiresAtUtc = absoluteExpiresAtUtc,
            SlidingExpiration = slidingExpiration,
            Tags = record.Tags
        };

        await UpsertRecordInternalAsync(refreshedRecord, new List<string>(), ct);
    }

    protected override async Task UpsertRecordInternalAsync(MemoryCacheRecord record, IReadOnlyCollection<string> obsoleteTags, CancellationToken ct)
    {
        var cacheKey = record.CacheKey;
        if (memoryCache.TryGetValue<MemoryCacheRecord>(cacheKey, out var oldRecord) && oldRecord is not null)
        {
            await RemoveRecordInternalAsync(cacheKey, oldRecord, ct);
        }

        var memCacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpiration = record.ExpiresAtUtc
        };
        if (record.SlidingExpiration.HasValue)
            memCacheOptions.SlidingExpiration = record.SlidingExpiration.Value;

        memCacheOptions.RegisterPostEvictionCallback((key, value, _, _) =>
        {
            if (value is MemoryCacheRecord evicted && key is string keyStr)
            {
                foreach (var tag in evicted.Tags)
                {
                    if (_tagIndex.TryGetValue(tag, out var keys))
                    {
                        keys.TryRemove(keyStr, out _);
                        if (keys.IsEmpty)
                            _tagIndex.TryRemove(tag, out _);
                    }
                }
            }
        });

        memoryCache.Set(cacheKey, record, memCacheOptions);

        foreach (var tag in record.Tags)
        {
            var keys = _tagIndex.GetOrAdd(
                tag,
                static _ => new ConcurrentDictionary<string, byte>(StringComparer.Ordinal));

            keys[cacheKey] = 0;
        }

        foreach (var tag in obsoleteTags)
        {
            if (_tagIndex.TryGetValue(tag, out var staleKeys))
            {
                staleKeys.TryRemove(cacheKey, out _);
                if (staleKeys.IsEmpty)
                    _tagIndex.TryRemove(tag, out _);
            }
        }
    }

    protected override async Task<HashSet<string>> GetCacheKeysForTagAsync(string normalizedTag, CancellationToken ct)
    {
        if (!_tagIndex.TryGetValue(normalizedTag, out var dict))
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        return dict.Keys
            .ToHashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    public override Task DisposeAsync()
    {
        memoryCache.Dispose();
        return Task.CompletedTask;
    }
}
