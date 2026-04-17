using Microsoft.Extensions.Caching.Distributed;
using SaanSoft.TaggedCache;

namespace SaanSoft.Tests.TaggedCache.Base;

public abstract class BaseTaggedCacheTests : IAsyncLifetime
{
    private ITaggedCache Cache { get; set; } = null!;

    protected abstract Task<ITaggedCache> CreateCache();

    /// <summary>
    /// Call from an <see cref="InitializeAsync"/> override when infrastructure is unavailable.
    /// Sets Cache to a stub that throws SkipException from within each test method body,
    /// which [SkippableFact] catches and marks as Skipped.
    /// </summary>
    protected void SetSkipCache(string reason) => Cache = new SkipTaggedCache(reason);

    public virtual async Task InitializeAsync()
    {
        Cache = await CreateCache();
    }

    public virtual Task DisposeAsync()
        => Cache.DisposeAsync();

    private record TestObject(string Name, int Value);

    // ---- GetAsync ----

    [SkippableFact]
    public async Task GetAsync_KeyNotFound_ReturnsNull()
    {
        var result = await Cache.GetAsync<TestObject>("nonexistent-key");
        result.Should().BeNull();
    }

    [SkippableFact]
    public async Task GetAsync_AfterSet_ReturnsValue()
    {
        var expected = new TestObject("Alice", 42);
        await Cache.SetAsync("get-key", expected);

        var result = await Cache.GetAsync<TestObject>("get-key");
        result.Should().Be(expected);
    }

    [SkippableFact]
    public async Task GetAsync_KeyCaseInsensitive_ReturnsSameValue()
    {
        var expected = new TestObject("Bob", 7);
        await Cache.SetAsync("MyKey", expected);

        var result = await Cache.GetAsync<TestObject>("mykey");
        result.Should().Be(expected);
    }

    [SkippableFact]
    public async Task GetAsync_KeyWithWhitespace_NormalizedAndRetrieved()
    {
        var expected = new TestObject("Carol", 3);
        await Cache.SetAsync("  spaced-key  ", expected);

        var result = await Cache.GetAsync<TestObject>("spaced-key");
        result.Should().Be(expected);
    }

    // ---- GetPayloadAsync ----

    [SkippableFact]
    public async Task GetPayloadAsync_AfterSet_ReturnsJson()
    {
        var value = new TestObject("Dave", 99);
        await Cache.SetAsync("payload-key", value);

        var payload = await Cache.GetPayloadAsync("payload-key");
        payload.Should().NotBeNull();
        payload.Should().Contain("Dave");
    }

    [SkippableFact]
    public async Task GetPayloadAsync_KeyNotFound_ReturnsNull()
    {
        var result = await Cache.GetPayloadAsync("missing-payload");
        result.Should().BeNull();
    }

    // ---- SetAsync ----

    [SkippableFact]
    public async Task SetAsync_OverwriteExistingKey_ReturnsNewValue()
    {
        await Cache.SetAsync("overwrite-key", new TestObject("Original", 1));
        await Cache.SetAsync("overwrite-key", new TestObject("Updated", 2));

        var result = await Cache.GetAsync<TestObject>("overwrite-key");
        result.Should().Be(new TestObject("Updated", 2));
    }

    [SkippableFact]
    public async Task SetAsync_WithTaggedCacheItem_RetrievableAndTagged()
    {
        var item = new TaggedCacheItem<TestObject>
        {
            Key = "item-key",
            Value = new TestObject("Tagged", 5),
            Tags = ["item-tag"]
        };
        await Cache.SetAsync(item);

        (await Cache.GetAsync<TestObject>("item-key")).Should().Be(new TestObject("Tagged", 5));

        await Cache.RemoveByTagAsync("item-tag");
        (await Cache.GetAsync<TestObject>("item-key")).Should().BeNull();
    }

    // ---- GetManyAsync ----

    [SkippableFact]
    public async Task GetManyAsync_MixedExistingAndMissing_ReturnsCorrectValues()
    {
        await Cache.SetAsync("multi-a", new TestObject("A", 1));
        await Cache.SetAsync("multi-b", new TestObject("B", 2));

        var result = await Cache.GetManyAsync<TestObject>(["multi-a", "multi-b", "multi-missing"]);

        result["multi-a"].Should().Be(new TestObject("A", 1));
        result["multi-b"].Should().Be(new TestObject("B", 2));
        result["multi-missing"].Should().BeNull();
    }

    // ---- SetManyAsync ----

    [SkippableFact]
    public async Task SetManyAsync_MultipleItems_AllRetrievable()
    {
        var items = new TaggedCacheItem<TestObject>[]
        {
            new() { Key = "many-1", Value = new("One", 1) },
            new() { Key = "many-2", Value = new("Two", 2) },
            new() { Key = "many-3", Value = new("Three", 3) },
        };
        await Cache.SetManyAsync(items);

        (await Cache.GetAsync<TestObject>("many-1")).Should().Be(new TestObject("One", 1));
        (await Cache.GetAsync<TestObject>("many-2")).Should().Be(new TestObject("Two", 2));
        (await Cache.GetAsync<TestObject>("many-3")).Should().Be(new TestObject("Three", 3));
    }

    // ---- RemoveAsync ----

    [SkippableFact]
    public async Task RemoveAsync_ExistingKey_ReturnsNullAfterRemoval()
    {
        await Cache.SetAsync("remove-me", new TestObject("ToRemove", 0));
        await Cache.RemoveAsync("remove-me");

        (await Cache.GetAsync<TestObject>("remove-me")).Should().BeNull();
    }

    [SkippableFact]
    public async Task RemoveAsync_NonexistentKey_DoesNotThrow()
    {
        await Cache.RemoveAsync("does-not-exist");
    }

    // ---- RemoveManyAsync ----

    [SkippableFact]
    public async Task RemoveManyAsync_MultipleKeys_AllRemoved()
    {
        await Cache.SetAsync("rm-a", new TestObject("A", 1));
        await Cache.SetAsync("rm-b", new TestObject("B", 2));
        await Cache.SetAsync("rm-keep", new TestObject("Keep", 3));

        await Cache.RemoveManyAsync(["rm-a", "rm-b"]);

        (await Cache.GetAsync<TestObject>("rm-a")).Should().BeNull();
        (await Cache.GetAsync<TestObject>("rm-b")).Should().BeNull();
        (await Cache.GetAsync<TestObject>("rm-keep")).Should().NotBeNull();
    }

    // ---- RemoveByTagAsync ----

    [SkippableFact]
    public async Task RemoveByTagAsync_TaggedEntries_AllRemoved()
    {
        await Cache.SetAsync("tag-a", new TestObject("A", 1), tags: ["product:1"]);
        await Cache.SetAsync("tag-b", new TestObject("B", 2), tags: ["product:1"]);

        await Cache.RemoveByTagAsync("product:1");

        (await Cache.GetAsync<TestObject>("tag-a")).Should().BeNull();
        (await Cache.GetAsync<TestObject>("tag-b")).Should().BeNull();
    }

    [SkippableFact]
    public async Task RemoveByTagAsync_UntaggedEntries_NotAffected()
    {
        await Cache.SetAsync("tagged-item", new TestObject("Tagged", 1), tags: ["some-tag"]);
        await Cache.SetAsync("untagged-item", new TestObject("Untagged", 2));

        await Cache.RemoveByTagAsync("some-tag");

        (await Cache.GetAsync<TestObject>("tagged-item")).Should().BeNull();
        (await Cache.GetAsync<TestObject>("untagged-item")).Should().NotBeNull();
    }

    [SkippableFact]
    public async Task RemoveByTagAsync_TagCaseInsensitive_RemovesEntry()
    {
        await Cache.SetAsync("ci-tagged", new TestObject("X", 1), tags: ["MyTag"]);

        await Cache.RemoveByTagAsync("mytag");

        (await Cache.GetAsync<TestObject>("ci-tagged")).Should().BeNull();
    }

    [SkippableFact]
    public async Task RemoveByTagAsync_NonexistentTag_DoesNotThrow()
    {
        await Cache.RemoveByTagAsync("nonexistent-tag");
    }

    // ---- RemoveByTagsAsync ----

    [SkippableFact]
    public async Task RemoveByTagsAsync_MultipleTaggedEntries_AllMatchingRemoved()
    {
        await Cache.SetAsync("tags-a", new TestObject("A", 1), tags: ["region:us"]);
        await Cache.SetAsync("tags-b", new TestObject("B", 2), tags: ["region:eu"]);
        await Cache.SetAsync("tags-c", new TestObject("C", 3), tags: ["region:ap"]);
        await Cache.SetAsync("tags-keep", new TestObject("Keep", 4), tags: ["region:au"]);

        await Cache.RemoveByTagsAsync(["region:us", "region:eu", "region:ap"]);

        (await Cache.GetAsync<TestObject>("tags-a")).Should().BeNull();
        (await Cache.GetAsync<TestObject>("tags-b")).Should().BeNull();
        (await Cache.GetAsync<TestObject>("tags-c")).Should().BeNull();
        (await Cache.GetAsync<TestObject>("tags-keep")).Should().NotBeNull();
    }

    // ---- Tag update on overwrite ----

    [SkippableFact]
    public async Task SetAsync_OverwriteWithDifferentTags_OldTagNoLongerInvalidates()
    {
        await Cache.SetAsync("retag-key", new TestObject("V1", 1), tags: ["old-tag"]);
        await Cache.SetAsync("retag-key", new TestObject("V2", 2), tags: ["new-tag"]);

        await Cache.RemoveByTagAsync("old-tag");

        (await Cache.GetAsync<TestObject>("retag-key")).Should().Be(new TestObject("V2", 2));
    }

    [SkippableFact]
    public async Task SetAsync_OverwriteWithDifferentTags_NewTagInvalidates()
    {
        await Cache.SetAsync("retag-key2", new TestObject("V1", 1), tags: ["old-tag2"]);
        await Cache.SetAsync("retag-key2", new TestObject("V2", 2), tags: ["new-tag2"]);

        await Cache.RemoveByTagAsync("new-tag2");

        (await Cache.GetAsync<TestObject>("retag-key2")).Should().BeNull();
    }

    // ---- Expiry ----

    [SkippableFact]
    public async Task SetAsync_AbsoluteExpiry_ReturnsNullAfterExpiry()
    {
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMilliseconds(50)
        };
        await Cache.SetAsync("expiring-key", new TestObject("Expiring", 1), options);

        await Task.Delay(200);

        (await Cache.GetAsync<TestObject>("expiring-key")).Should().BeNull();
    }

    [SkippableFact]
    public async Task SetAsync_AbsoluteExpiry_ReturnableBeforeExpiry()
    {
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30)
        };
        await Cache.SetAsync("not-yet-expired", new TestObject("Active", 1), options);

        (await Cache.GetAsync<TestObject>("not-yet-expired")).Should().NotBeNull();
    }

    // ---- GetOrCreateAsync ----

    [SkippableFact]
    public async Task GetOrCreateAsync_KeyNotFound_CallsFactory()
    {
        var factoryCalled = 0;

        var result = await Cache.GetOrCreateAsync<TestObject>(
            "create-key",
            ct =>
            {
                factoryCalled++;
                return Task.FromResult<TestObject?>(new TestObject("Created", 1));
            });

        factoryCalled.Should().Be(1);
        result.Should().Be(new TestObject("Created", 1));
    }

    [SkippableFact]
    public async Task GetOrCreateAsync_KeyExists_DoesNotCallFactory()
    {
        await Cache.SetAsync("existing-create-key", new TestObject("Existing", 1));
        var factoryCalled = 0;

        var result = await Cache.GetOrCreateAsync<TestObject>(
            "existing-create-key",
            ct =>
            {
                factoryCalled++;
                return Task.FromResult<TestObject?>(new TestObject("ShouldNotBeCalled", 99));
            });

        factoryCalled.Should().Be(0);
        result.Should().Be(new TestObject("Existing", 1));
    }

    [SkippableFact]
    public async Task GetOrCreateAsync_AfterCreate_SubsequentGetReturnsCachedValue()
    {
        await Cache.GetOrCreateAsync<TestObject>(
            "populate-key",
            ct => Task.FromResult<TestObject?>(new TestObject("Populated", 1)));

        (await Cache.GetAsync<TestObject>("populate-key")).Should().Be(new TestObject("Populated", 1));
    }
}
