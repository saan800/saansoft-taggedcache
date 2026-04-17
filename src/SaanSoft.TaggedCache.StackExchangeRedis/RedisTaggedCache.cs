using Microsoft.Extensions.Caching.Distributed;
using SaanSoft.TaggedCache.Base;
using StackExchange.Redis;
using System.Text.Json;

namespace SaanSoft.TaggedCache.StackExchangeRedis;

public class RedisTaggedCache(IConnectionMultiplexer redis, RedisTaggedCacheOptions cacheOptions) : BaseTaggedCache<RedisCacheRecord, string>(cacheOptions), ITaggedCache
{
    private readonly IDatabase _db = redis.GetDatabase();

    public override async Task SetAsync<T>(string cacheKey, T value, DistributedCacheEntryOptions? options = null, IReadOnlyCollection<string>? tags = null, CancellationToken ct = default)
    {
        var normalizedCacheKey = NormalizeCacheKey(cacheKey);
        options ??= cacheOptions.DefaultCacheOptions;

        var existing = await GetRecordInternalAsync(normalizedCacheKey, ct);

        var nowUtc = DateTimeOffset.UtcNow;
        var resolved = options.Resolve(nowUtc);

        var newTags = NormalizeTags(tags);
        var obsoleteTags = (existing?.Tags ?? Array.Empty<string>()).Except(newTags, StringComparer.OrdinalIgnoreCase).ToList();

        var payload = JsonSerializer.Serialize(value, cacheOptions.JsonSerializerOptions);

        var record = new RedisCacheRecord
        {
            CacheKey = normalizedCacheKey,
            Payload = payload,
            ExpiresAtUtc = resolved.ExpiresAtUtc,
            AbsoluteExpiresAtUtc = resolved.AbsoluteExpiresAtUtc,
            SlidingExpiration = resolved.SlidingExpiration,
            Tags = newTags
        };

        await UpsertRecordInternalAsync(record, obsoleteTags, ct);
    }

    public override async Task RemoveByTagAsync(string tag, CancellationToken ct = default)
    {
        await base.RemoveByTagAsync(tag, ct);
        await CleanupTagIndexAsync(tag);
    }

    public override async Task RemoveByTagsAsync(IReadOnlyCollection<string> tags, CancellationToken ct = default)
    {
        await base.RemoveByTagsAsync(tags, ct);
        foreach (var tag in tags)
        {
            await CleanupTagIndexAsync(tag);
        }
    }

    protected override async Task<RedisCacheRecord?> GetRecordInternalAsync(string normalizedCacheKey, CancellationToken ct)
    {
        var record = RedisValueToCacheRecord(await _db.StringGetAsync(normalizedCacheKey));
        if (record is null) return null;

        var nowUtc = DateTimeOffset.UtcNow;
        if (record.ExpiresAtUtc <= nowUtc)
        {
            await RemoveRecordInternalAsync(normalizedCacheKey, record, ct);
            return null;
        }

        if (await TryRefreshSlidingAsync(record, nowUtc, ct))
        {
            var refreshed = RedisValueToCacheRecord(await _db.StringGetAsync(normalizedCacheKey));
            if (refreshed is not null) return refreshed;
        }

        return record;
    }

    protected override async Task<Dictionary<string, RedisCacheRecord?>> GetManyRecordsInternalAsync(IReadOnlyCollection<string> normalizedCacheKeys, CancellationToken ct)
    {
        var keys = normalizedCacheKeys.Select(key => new RedisKey(key)).ToArray();
        var values = await _db.StringGetAsync(keys);

        var result = new Dictionary<string, RedisCacheRecord?>();
        var cacheKeysArray = normalizedCacheKeys.ToArray();
        for (var i = 0; i < cacheKeysArray.Length; i++)
        {
            var cacheKey = cacheKeysArray[i];
            var val = values.Length > i ? values[i] : RedisValue.Null;
            result.Add(cacheKey, RedisValueToCacheRecord(val));
        }
        return result;
    }

    protected override Task RemoveRecordInternalAsync(string normalizedCacheKey, RedisCacheRecord? existing, CancellationToken ct)
        => RemoveManyRecordsInternalAsync(new Dictionary<string, RedisCacheRecord?> { { normalizedCacheKey, existing } }, ct);

    protected override async Task RemoveManyRecordsInternalAsync(Dictionary<string, RedisCacheRecord?> cachedRecords, CancellationToken ct)
    {
        var batch = _db.CreateBatch();
        var tasks = new List<Task>();

        foreach (var kvp in cachedRecords)
        {
            tasks.Add(batch.KeyDeleteAsync(kvp.Key));

            if (kvp.Value is not null)
            {
                foreach (var tag in kvp.Value.Tags)
                {
                    tasks.Add(batch.SortedSetRemoveAsync(tag, kvp.Key));
                }
            }
        }

        batch.Execute();
        await Task.WhenAll(tasks);
    }

    protected override async Task UpsertRecordInternalAsync(RedisCacheRecord record, IReadOnlyCollection<string> obsoleteTags, CancellationToken ct)
    {
        var ttl = record.ExpiresAtUtc - DateTimeOffset.UtcNow;
        if (ttl <= TimeSpan.Zero)
        {
            await RemoveRecordInternalAsync(record.CacheKey, record, ct);
            return;
        }

        var recordJson = JsonSerializer.Serialize(record, cacheOptions.JsonSerializerOptions);
        var expiresAtUnix = record.ExpiresAtUtc.ToUnixTimeSeconds();

        var batch = _db.CreateBatch();
        var tasks = new List<Task>
        {
            batch.StringSetAsync(record.CacheKey, recordJson, ttl)
        };

        foreach (var tag in record.Tags)
        {
            tasks.Add(batch.SortedSetAddAsync(tag, record.CacheKey, expiresAtUnix));
        }

        foreach (var removedTag in obsoleteTags)
        {
            tasks.Add(batch.SortedSetRemoveAsync(removedTag, record.CacheKey));
        }

        batch.Execute();
        await Task.WhenAll(tasks);

        foreach (var removedTag in obsoleteTags)
        {
            await CleanupTagIndexAsync(removedTag);
        }
    }

    protected override async Task UpdateExpiryInternalAsync(string normalizedCacheKey, DateTimeOffset expiresAtUtc, DateTimeOffset? absoluteExpiresAtUtc, TimeSpan? slidingExpiration, CancellationToken ct)
    {
        var record = RedisValueToCacheRecord(await _db.StringGetAsync(normalizedCacheKey));
        if (record == null) return;

        var ttl = expiresAtUtc - DateTimeOffset.UtcNow;
        if (ttl <= TimeSpan.Zero)
        {
            await RemoveRecordInternalAsync(normalizedCacheKey, record, ct);
            return;
        }

        var refreshedRecord = new RedisCacheRecord
        {
            CacheKey = record.CacheKey,
            Payload = record.Payload,
            ExpiresAtUtc = expiresAtUtc,
            AbsoluteExpiresAtUtc = absoluteExpiresAtUtc,
            SlidingExpiration = slidingExpiration,
            Tags = record.Tags
        };

        var recordJson = JsonSerializer.Serialize(refreshedRecord, cacheOptions.JsonSerializerOptions);

        var batch = _db.CreateBatch();
        var tasks = new List<Task> { batch.StringSetAsync(normalizedCacheKey, recordJson, ttl) };

        batch.Execute();
        await Task.WhenAll(tasks);
    }

    protected override async Task<HashSet<string>> GetCacheKeysForTagAsync(string normalizedTag, CancellationToken ct)
    {
        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await _db.SortedSetRemoveRangeByScoreAsync(
            normalizedTag,
            double.NegativeInfinity,
            nowUnix);

        var members = await _db.SortedSetRangeByRankAsync(normalizedTag, 0, -1);
        if (members.Length == 0)
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        return members
            .Where(static m => m.HasValue)
            .Select(static m => m.ToString())
            .Where(static s => !string.IsNullOrWhiteSpace(s))
            .ToHashSet<string>(StringComparer.OrdinalIgnoreCase);

    }

    private async Task CleanupTagIndexAsync(string tag)
    {
        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var normalizedTag = NormalizeTag(tag);

        await _db.SortedSetRemoveRangeByScoreAsync(
            normalizedTag,
            double.NegativeInfinity,
            nowUnix);

        var remaining = await _db.SortedSetLengthAsync(normalizedTag);
        if (remaining == 0)
            await _db.KeyDeleteAsync(normalizedTag);
    }

    private RedisCacheRecord? RedisValueToCacheRecord(RedisValue value)
    {
        if (!value.HasValue || value == RedisValue.Null)
            return null;

        string json = value.ToString();
        if (string.IsNullOrWhiteSpace(json))
            return null;

        return JsonSerializer.Deserialize<RedisCacheRecord>(json, cacheOptions.JsonSerializerOptions);
    }

    public override async Task DisposeAsync()
    {
        await redis.DisposeAsync();
    }
}
