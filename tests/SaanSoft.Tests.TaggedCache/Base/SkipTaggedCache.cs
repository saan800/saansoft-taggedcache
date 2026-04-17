using Microsoft.Extensions.Caching.Distributed;
using SaanSoft.TaggedCache;

namespace SaanSoft.Tests.TaggedCache.Base;

/// <summary>
/// A no-op ITaggedCache that throws a SkippableException on every call.
/// Used by BaseTaggedCacheTests when infrastructure (e.g. Docker) is unavailable:
/// the exception is thrown from within the test method body where [SkippableFact] can catch it.
/// </summary>
public sealed class SkipTaggedCache(string reason) : ITaggedCache
{
    private T Throw<T>()
    {
        Skip.If(true, reason);
        return default!;
    }

    public byte[]? Get(string key) => Throw<byte[]?>();
    public Task<byte[]?> GetAsync(string key, CancellationToken token) => Task.FromResult(Throw<byte[]?>());
    public Task<T?> GetAsync<T>(string cacheKey, CancellationToken ct = default) => Task.FromResult(Throw<T?>());
    public Task<Dictionary<string, T?>> GetManyAsync<T>(IReadOnlyCollection<string> cacheKeys, CancellationToken ct = default) => Task.FromResult(Throw<Dictionary<string, T?>>());
    public Task<string?> GetPayloadAsync(string cacheKey, CancellationToken ct = default) => Task.FromResult(Throw<string?>());

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => Throw<byte[]?>();
    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token) => Task.FromResult(Throw<byte[]?>());
    public Task SetAsync<T>(string cacheKey, T obj, DistributedCacheEntryOptions? options = null, IReadOnlyCollection<string>? tags = null, CancellationToken ct = default) => Task.FromResult(Throw<byte[]?>());
    public Task SetAsync<T>(TaggedCacheItem<T> item, DistributedCacheEntryOptions? options = null, CancellationToken ct = default) => Task.FromResult(Throw<byte[]?>());
    public Task SetManyAsync<T>(IReadOnlyCollection<TaggedCacheItem<T>> items, DistributedCacheEntryOptions? options = null, CancellationToken ct = default) => Task.FromResult(Throw<byte[]?>());

    public void Refresh(string key) => Throw<byte[]?>();
    public Task RefreshAsync(string key, CancellationToken token) => Task.FromResult(Throw<byte[]?>());
    public Task RefreshAsync(string cacheKey, DistributedCacheEntryOptions? options = null, CancellationToken ct = default) => Task.FromResult(Throw<byte[]?>());

    public void Remove(string key) => Throw<byte[]?>();
    public Task RemoveAsync(string key, CancellationToken token) => Task.FromResult(Throw<byte[]?>());
    public Task RemoveManyAsync(IReadOnlyCollection<string> cacheKeys, CancellationToken ct = default) => Task.FromResult(Throw<byte[]?>());
    public Task RemoveByTagAsync(string tag, CancellationToken ct = default) => Task.FromResult(Throw<byte[]?>());
    public Task RemoveByTagsAsync(IReadOnlyCollection<string> tags, CancellationToken ct = default) => Task.FromResult(Throw<byte[]?>());

    public Task DisposeAsync() => Task.CompletedTask;
}
