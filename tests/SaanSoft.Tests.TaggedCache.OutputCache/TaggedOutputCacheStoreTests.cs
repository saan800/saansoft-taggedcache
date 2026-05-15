using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using SaanSoft.TaggedCache.Memory;
using SaanSoft.TaggedCache.OutputCache;

namespace SaanSoft.Tests.TaggedCache.OutputCache;

public class TaggedOutputCacheStoreTests : IAsyncLifetime
{
    private IMemoryCache? _memoryCache;
    private TaggedOutputCacheStore? _store;

    ValueTask IAsyncLifetime.InitializeAsync()
    {
        _memoryCache = new MemoryCache(Options.Create(new MemoryCacheOptions()));
        var taggedCache = new MemoryTaggedCache(_memoryCache, new MemoryTaggedCacheOptions());
        _store = new TaggedOutputCacheStore(taggedCache, new TaggedOutputCacheStoreOptions());
        return ValueTask.CompletedTask;
    }

    ValueTask IAsyncDisposable.DisposeAsync()
    {
        _memoryCache?.Dispose();
        return ValueTask.CompletedTask;
    }

    private TaggedOutputCacheStore Store => _store!;

    // ---- GetAsync ----

    [Fact]
    public async Task GetAsync_KeyNotFound_ReturnsNull()
    {
        var result = await Store.GetAsync("missing-key", TestContext.Current.CancellationToken);
        result.Should().BeNull();
    }

    // ---- SetAsync / GetAsync round-trips ----

    [Fact]
    public async Task SetAsync_GetAsync_RoundTrips_TextContent()
    {
        var expected = "Hello, output cache!"u8.ToArray();

        await Store.SetAsync("text-key", expected, null, TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken);
        var result = await Store.GetAsync("text-key", TestContext.Current.CancellationToken);

        result.Should().Equal(expected);
    }

    [Fact]
    public async Task SetAsync_GetAsync_RoundTrips_BinaryContent()
    {
        var expected = new byte[] { 0x00, 0x01, 0xFF, 0xFE, 0x80, 0x81 };

        await Store.SetAsync("binary-key", expected, null, TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken);
        var result = await Store.GetAsync("binary-key", TestContext.Current.CancellationToken);

        result.Should().Equal(expected);
    }

    [Fact]
    public async Task SetAsync_GetAsync_ReturnsNull_AfterExpiry()
    {
        var value = "expiring"u8.ToArray();

        await Store.SetAsync("expiring-key", value, null, TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken);
        await Task.Delay(200, TestContext.Current.CancellationToken);

        var result = await Store.GetAsync("expiring-key", TestContext.Current.CancellationToken);
        result.Should().BeNull();
    }

    [Fact]
    public async Task SetAsync_OverwritesExistingEntry()
    {
        var original = "original"u8.ToArray();
        var updated = "updated"u8.ToArray();

        await Store.SetAsync("overwrite-key", original, null, TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken);
        await Store.SetAsync("overwrite-key", updated, null, TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken);

        var result = await Store.GetAsync("overwrite-key", TestContext.Current.CancellationToken);
        result.Should().Equal(updated);
    }

    // ---- EvictByTagAsync ----

    [Fact]
    public async Task EvictByTagAsync_RemovesTaggedEntry()
    {
        var value = "tagged response"u8.ToArray();

        await Store.SetAsync("tagged-key", value, ["page-tag"], TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken);
        await Store.EvictByTagAsync("page-tag", TestContext.Current.CancellationToken);

        var result = await Store.GetAsync("tagged-key", TestContext.Current.CancellationToken);
        result.Should().BeNull();
    }

    [Fact]
    public async Task EvictByTagAsync_RemovesAllEntriesWithTag()
    {
        var value = "response"u8.ToArray();

        await Store.SetAsync("tag-multi-a", value, ["shared-tag"], TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken);
        await Store.SetAsync("tag-multi-b", value, ["shared-tag"], TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken);
        await Store.EvictByTagAsync("shared-tag", TestContext.Current.CancellationToken);

        (await Store.GetAsync("tag-multi-a", TestContext.Current.CancellationToken)).Should().BeNull();
        (await Store.GetAsync("tag-multi-b", TestContext.Current.CancellationToken)).Should().BeNull();
    }

    [Fact]
    public async Task EvictByTagAsync_DoesNotRemoveUntaggedEntry()
    {
        var value = "response"u8.ToArray();

        await Store.SetAsync("untagged-key", value, null, TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken);
        await Store.SetAsync("tagged-key", value, ["evict-tag"], TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken);
        await Store.EvictByTagAsync("evict-tag", TestContext.Current.CancellationToken);

        (await Store.GetAsync("tagged-key", TestContext.Current.CancellationToken)).Should().BeNull();
        (await Store.GetAsync("untagged-key", TestContext.Current.CancellationToken)).Should().Equal(value);
    }

    [Fact]
    public async Task EvictByTagAsync_NonExistentTag_DoesNotThrow()
    {
        await Store.EvictByTagAsync("non-existent-tag", TestContext.Current.CancellationToken);
    }
}
