using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Caching.Distributed;
using SaanSoft.TaggedCache.Base;

namespace SaanSoft.TaggedCache.OutputCache;

public class TaggedOutputCacheStore(ITaggedCache taggedCache, TaggedOutputCacheStoreOptions options) : IOutputCacheStore
{
    public async ValueTask EvictByTagAsync(string tag, CancellationToken ct = default)
        => await taggedCache.RemoveByTagAsync(tag, ct);

    public async ValueTask<byte[]?> GetAsync(string key, CancellationToken ct = default)
        => await taggedCache.GetAsync($"{options.CacheKeyPrefix}{key}", ct);

    public async ValueTask SetAsync(string key, byte[] value, string[]? tags, TimeSpan validFor, CancellationToken ct = default)
        => await taggedCache.SetAsync($"{options.CacheKeyPrefix}{key}", value.AsString(), new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = validFor }, tags, ct);
}
