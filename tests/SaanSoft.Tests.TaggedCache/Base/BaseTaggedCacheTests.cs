using Microsoft.Extensions.Caching.Distributed;
using SaanSoft.TaggedCache;

namespace SaanSoft.Tests.TaggedCache.Base;

public abstract class BaseTaggedCacheTests : IAsyncLifetime
{
    private ITaggedCache Cache { get; set; } = null!;

    protected abstract Task<ITaggedCache> CreateCache();

    async ValueTask IAsyncLifetime.InitializeAsync()
        => await InitializeAsync();

    async ValueTask IAsyncDisposable.DisposeAsync()
        => await DisposeAsync();

    public virtual async Task InitializeAsync()
        => Cache = await CreateCache();

    public virtual Task DisposeAsync()
        => Cache.DisposeAsync();

    private record TestObject(string Name, int Value);

    // ---- GetAsync ----

    [Fact]
    public async Task GetAsync_KeyNotFound_ReturnsNull()
    {
        var result = await Cache.GetAsync<TestObject>("non-existent-key", TestContext.Current.CancellationToken);
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_AfterSet_ReturnsValue()
    {
        var expected = new TestObject("Alice", 42);
        await Cache.SetAsync("get-key", expected, ct: TestContext.Current.CancellationToken);

        var result = await Cache.GetAsync<TestObject>("get-key", TestContext.Current.CancellationToken);
        result.Should().Be(expected);
    }

    [Fact]
    public async Task GetAsync_KeyCaseInsensitive_ReturnsSameValue()
    {
        var expected = new TestObject("Bob", 7);
        await Cache.SetAsync("Key", expected, ct: TestContext.Current.CancellationToken);

        var result = await Cache.GetAsync<TestObject>("key", TestContext.Current.CancellationToken);
        result.Should().Be(expected);
    }

    [Fact]
    public async Task GetAsync_KeyWithWhitespace_NormalizedAndRetrieved()
    {
        var expected = new TestObject("Carol", 3);
        await Cache.SetAsync("  spaced-key  ", expected, ct: TestContext.Current.CancellationToken);

        var result = await Cache.GetAsync<TestObject>("spaced-key", TestContext.Current.CancellationToken);
        result.Should().Be(expected);
    }

    // ---- GetPayloadAsync ----

    [Fact]
    public async Task GetPayloadAsync_AfterSet_ReturnsJson()
    {
        var value = new TestObject("Bugs", 99);
        await Cache.SetAsync("payload-key", value, ct: TestContext.Current.CancellationToken);

        var payload = await Cache.GetPayloadAsync("payload-key", TestContext.Current.CancellationToken);
        payload.Should().NotBeNull();
        payload.Should().Contain("Bugs");
    }

    [Fact]
    public async Task GetPayloadAsync_KeyNotFound_ReturnsNull()
    {
        var result = await Cache.GetPayloadAsync("missing-payload", TestContext.Current.CancellationToken);
        result.Should().BeNull();
    }

    // ---- SetAsync ----

    [Fact]
    public async Task SetAsync_OverwriteExistingKey_ReturnsNewValue()
    {
        await Cache.SetAsync("overwrite-key", new TestObject("Original", 1), ct: TestContext.Current.CancellationToken);
        await Cache.SetAsync("overwrite-key", new TestObject("Updated", 2), ct: TestContext.Current.CancellationToken);

        var result = await Cache.GetAsync<TestObject>("overwrite-key", TestContext.Current.CancellationToken);
        result.Should().Be(new TestObject("Updated", 2));
    }

    [Fact]
    public async Task SetAsync_WithTaggedCacheItem_RetrievableAndTagged()
    {
        var item = new TaggedCacheItem<TestObject>
        {
            Key = "item-key",
            Value = new TestObject("Tagged", 5),
            Tags = ["item-tag"]
        };
        await Cache.SetAsync(item, ct: TestContext.Current.CancellationToken);

        (await Cache.GetAsync<TestObject>("item-key", TestContext.Current.CancellationToken)).Should().Be(new TestObject("Tagged", 5));

        await Cache.RemoveByTagAsync("item-tag", TestContext.Current.CancellationToken);
        (await Cache.GetAsync<TestObject>("item-key", TestContext.Current.CancellationToken)).Should().BeNull();
    }

    // ---- GetManyAsync ----

    [Fact]
    public async Task GetManyAsync_MixedExistingAndMissing_ReturnsCorrectValues()
    {
        await Cache.SetAsync("multi-a", new TestObject("A", 1), ct: TestContext.Current.CancellationToken);
        await Cache.SetAsync("multi-b", new TestObject("B", 2), ct: TestContext.Current.CancellationToken);

        var result = await Cache.GetManyAsync<TestObject>(["multi-a", "multi-b", "multi-missing"], TestContext.Current.CancellationToken);

        result["multi-a"].Should().Be(new TestObject("A", 1));
        result["multi-b"].Should().Be(new TestObject("B", 2));
        result["multi-missing"].Should().BeNull();
    }

    // ---- GetManyByTagAsync ----

    [Fact]
    public async Task GetManyByTagAsync_TagNotFound_ReturnsEmpty()
    {
        var result = await Cache.GetManyByTagAsync<TestObject>("tag-fetch-missing", TestContext.Current.CancellationToken);
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetManyByTagAsync_SingleTaggedItem_ReturnsItem()
    {
        var expected = new TestObject("Alpha", 1);
        await Cache.SetAsync("tag-fetch-single", expected, tags: ["single-item-tag"], ct: TestContext.Current.CancellationToken);

        var result = await Cache.GetManyByTagAsync<TestObject>("single-item-tag", TestContext.Current.CancellationToken);

        result.Should().HaveCount(1);
        result["tag-fetch-single"].Should().Be(expected);
    }

    [Fact]
    public async Task GetManyByTagAsync_MultipleItemsSameTag_ReturnsAll()
    {
        await Cache.SetAsync("shared-tag-item-a", new TestObject("A", 1), tags: ["shared-items-tag"], ct: TestContext.Current.CancellationToken);
        await Cache.SetAsync("shared-tag-item-b", new TestObject("B", 2), tags: ["shared-items-tag"], ct: TestContext.Current.CancellationToken);
        await Cache.SetAsync("shared-tag-item-c", new TestObject("C", 3), tags: ["shared-items-tag"], ct: TestContext.Current.CancellationToken);

        var result = await Cache.GetManyByTagAsync<TestObject>("shared-items-tag", TestContext.Current.CancellationToken);

        result.Should().HaveCount(3);
        result["shared-tag-item-a"].Should().Be(new TestObject("A", 1));
        result["shared-tag-item-b"].Should().Be(new TestObject("B", 2));
        result["shared-tag-item-c"].Should().Be(new TestObject("C", 3));
    }

    [Fact]
    public async Task GetManyByTagAsync_OnlyReturnsItemsMatchingTag()
    {
        await Cache.SetAsync("tag-fetch-matched", new TestObject("Match", 1), tags: ["target-fetch-tag"], ct: TestContext.Current.CancellationToken);
        await Cache.SetAsync("tag-fetch-other", new TestObject("Other", 2), tags: ["other-fetch-tag"], ct: TestContext.Current.CancellationToken);

        var result = await Cache.GetManyByTagAsync<TestObject>("target-fetch-tag", TestContext.Current.CancellationToken);

        result.Should().HaveCount(1);
        result.Should().ContainKey("tag-fetch-matched");
        result.Should().NotContainKey("tag-fetch-other");
    }

    [Fact]
    public async Task GetManyByTagAsync_TagCaseInsensitive_ReturnsItem()
    {
        await Cache.SetAsync("tag-fetch-case", new TestObject("CI", 1), tags: ["CASE-TAG-FETCH"], ct: TestContext.Current.CancellationToken);

        var result = await Cache.GetManyByTagAsync<TestObject>("case-tag-fetch", TestContext.Current.CancellationToken);

        result.Should().HaveCount(1);
        result["tag-fetch-case"].Should().Be(new TestObject("CI", 1));
    }

    [Fact]
    public async Task GetManyByTagAsync_UntaggedItems_NotIncluded()
    {
        await Cache.SetAsync("tag-fetch-tagged", new TestObject("Tagged", 1), tags: ["tagged-only-fetch"], ct: TestContext.Current.CancellationToken);
        await Cache.SetAsync("tag-fetch-untagged", new TestObject("Untagged", 2), ct: TestContext.Current.CancellationToken);

        var result = await Cache.GetManyByTagAsync<TestObject>("tagged-only-fetch", TestContext.Current.CancellationToken);

        result.Should().HaveCount(1);
        result.Should().ContainKey("tag-fetch-tagged");
        result.Should().NotContainKey("tag-fetch-untagged");
    }

    [Fact]
    public async Task GetManyByTagAsync_ExpiredItem_NotReturned()
    {
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMilliseconds(50)
        };
        await Cache.SetAsync("tag-fetch-expiring", new TestObject("Expiring", 1), options, tags: ["expiry-fetch-tag"], ct: TestContext.Current.CancellationToken);

        await Task.Delay(200, TestContext.Current.CancellationToken);

        var result = await Cache.GetManyByTagAsync<TestObject>("expiry-fetch-tag", TestContext.Current.CancellationToken);
        result.Should().BeEmpty();
    }

    // ---- SetManyAsync ----

    [Fact]
    public async Task SetManyAsync_MultipleItems_AllRetrievable()
    {
        var items = new TaggedCacheItem<TestObject>[]
        {
            new() { Key = "many-1", Value = new("One", 1) },
            new() { Key = "many-2", Value = new("Two", 2) },
            new() { Key = "many-3", Value = new("Three", 3) },
        };
        await Cache.SetManyAsync(items, ct: TestContext.Current.CancellationToken);

        (await Cache.GetAsync<TestObject>("many-1", TestContext.Current.CancellationToken)).Should().Be(new TestObject("One", 1));
        (await Cache.GetAsync<TestObject>("many-2", TestContext.Current.CancellationToken)).Should().Be(new TestObject("Two", 2));
        (await Cache.GetAsync<TestObject>("many-3", TestContext.Current.CancellationToken)).Should().Be(new TestObject("Three", 3));
    }

    // ---- RemoveAsync ----

    [Fact]
    public async Task RemoveAsync_ExistingKey_ReturnsNullAfterRemoval()
    {
        await Cache.SetAsync("remove-me", new TestObject("ToRemove", 0), ct: TestContext.Current.CancellationToken);
        await Cache.RemoveAsync("remove-me", TestContext.Current.CancellationToken);

        (await Cache.GetAsync<TestObject>("remove-me", TestContext.Current.CancellationToken)).Should().BeNull();
    }

    [Fact]
    public async Task RemoveAsync_NonExistentKey_DoesNotThrow()
    {
        await Cache.RemoveAsync("does-not-exist", TestContext.Current.CancellationToken);
    }

    // ---- RemoveManyAsync ----

    [Fact]
    public async Task RemoveManyAsync_MultipleKeys_AllRemoved()
    {
        await Cache.SetAsync("rm-a", new TestObject("A", 1), ct: TestContext.Current.CancellationToken);
        await Cache.SetAsync("rm-b", new TestObject("B", 2), ct: TestContext.Current.CancellationToken);
        await Cache.SetAsync("rm-keep", new TestObject("Keep", 3), ct: TestContext.Current.CancellationToken);

        await Cache.RemoveManyAsync(["rm-a", "rm-b"], TestContext.Current.CancellationToken);

        (await Cache.GetAsync<TestObject>("rm-a", TestContext.Current.CancellationToken)).Should().BeNull();
        (await Cache.GetAsync<TestObject>("rm-b", TestContext.Current.CancellationToken)).Should().BeNull();
        (await Cache.GetAsync<TestObject>("rm-keep", TestContext.Current.CancellationToken)).Should().NotBeNull();
    }

    // ---- RemoveByTagAsync ----

    [Fact]
    public async Task RemoveByTagAsync_TaggedEntries_AllRemoved()
    {
        await Cache.SetAsync("tag-a", new TestObject("A", 1), tags: ["product:1"], ct: TestContext.Current.CancellationToken);
        await Cache.SetAsync("tag-b", new TestObject("B", 2), tags: ["product:1"], ct: TestContext.Current.CancellationToken);

        await Cache.RemoveByTagAsync("product:1", TestContext.Current.CancellationToken);

        (await Cache.GetAsync<TestObject>("tag-a", TestContext.Current.CancellationToken)).Should().BeNull();
        (await Cache.GetAsync<TestObject>("tag-b", TestContext.Current.CancellationToken)).Should().BeNull();
    }

    [Fact]
    public async Task RemoveByTagAsync_UntaggedEntries_NotAffected()
    {
        await Cache.SetAsync("tagged-item", new TestObject("Tagged", 1), tags: ["some-tag"], ct: TestContext.Current.CancellationToken);
        await Cache.SetAsync("untagged-item", new TestObject("Untagged", 2), ct: TestContext.Current.CancellationToken);

        await Cache.RemoveByTagAsync("some-tag", TestContext.Current.CancellationToken);

        (await Cache.GetAsync<TestObject>("tagged-item", TestContext.Current.CancellationToken)).Should().BeNull();
        (await Cache.GetAsync<TestObject>("untagged-item", TestContext.Current.CancellationToken)).Should().NotBeNull();
    }

    [Fact]
    public async Task RemoveByTagAsync_TagCaseInsensitive_RemovesEntry()
    {
        await Cache.SetAsync("ci-tagged", new TestObject("X", 1), tags: ["tag-name"], ct: TestContext.Current.CancellationToken);

        await Cache.RemoveByTagAsync("tag-name", TestContext.Current.CancellationToken);

        (await Cache.GetAsync<TestObject>("ci-tagged", TestContext.Current.CancellationToken)).Should().BeNull();
    }

    [Fact]
    public async Task RemoveByTagAsync_NonExistentTag_DoesNotThrow()
    {
        await Cache.RemoveByTagAsync("non-existent-tag", TestContext.Current.CancellationToken);
    }

    // ---- RemoveByAnyTagAsync ----

    [Fact]
    public async Task RemoveByAnyTagAsync_MultipleTaggedEntries_AllMatchingRemoved()
    {
        await Cache.SetAsync("tags-a", new TestObject("A", 1), tags: ["region:us"], ct: TestContext.Current.CancellationToken);
        await Cache.SetAsync("tags-b", new TestObject("B", 2), tags: ["region:eu"], ct: TestContext.Current.CancellationToken);
        await Cache.SetAsync("tags-c", new TestObject("C", 3), tags: ["region:ap"], ct: TestContext.Current.CancellationToken);
        await Cache.SetAsync("tags-keep", new TestObject("Keep", 4), tags: ["region:au"], ct: TestContext.Current.CancellationToken);

        await Cache.RemoveByAnyTagAsync(["region:us", "region:eu", "region:ap"], TestContext.Current.CancellationToken);

        (await Cache.GetAsync<TestObject>("tags-a", TestContext.Current.CancellationToken)).Should().BeNull();
        (await Cache.GetAsync<TestObject>("tags-b", TestContext.Current.CancellationToken)).Should().BeNull();
        (await Cache.GetAsync<TestObject>("tags-c", TestContext.Current.CancellationToken)).Should().BeNull();
        (await Cache.GetAsync<TestObject>("tags-keep", TestContext.Current.CancellationToken)).Should().NotBeNull();
    }

    // ---- Tag update on overwrite ----

    [Fact]
    public async Task SetAsync_OverwriteWithDifferentTags_OldTagNoLongerInvalidates()
    {
        await Cache.SetAsync("retag-key", new TestObject("V1", 1), tags: ["old-tag"], ct: TestContext.Current.CancellationToken);
        await Cache.SetAsync("retag-key", new TestObject("V2", 2), tags: ["new-tag"], ct: TestContext.Current.CancellationToken);

        await Cache.RemoveByTagAsync("old-tag", TestContext.Current.CancellationToken);

        (await Cache.GetAsync<TestObject>("retag-key", TestContext.Current.CancellationToken)).Should().Be(new TestObject("V2", 2));
    }

    [Fact]
    public async Task SetAsync_OverwriteWithDifferentTags_NewTagInvalidates()
    {
        await Cache.SetAsync("retag-key2", new TestObject("V1", 1), tags: ["old-tag2"], ct: TestContext.Current.CancellationToken);
        await Cache.SetAsync("retag-key2", new TestObject("V2", 2), tags: ["new-tag2"], ct: TestContext.Current.CancellationToken);

        await Cache.RemoveByTagAsync("new-tag2", TestContext.Current.CancellationToken);

        (await Cache.GetAsync<TestObject>("retag-key2", TestContext.Current.CancellationToken)).Should().BeNull();
    }

    // ---- Expiry ----

    [Fact]
    public async Task SetAsync_AbsoluteExpiry_ReturnsNullAfterExpiry()
    {
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMilliseconds(50)
        };
        await Cache.SetAsync("expiring-key", new TestObject("Expiring", 1), options, ct: TestContext.Current.CancellationToken);

        await Task.Delay(200, TestContext.Current.CancellationToken);

        (await Cache.GetAsync<TestObject>("expiring-key", TestContext.Current.CancellationToken)).Should().BeNull();
    }

    [Fact]
    public async Task SetAsync_AbsoluteExpiry_ReturnableBeforeExpiry()
    {
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30)
        };
        await Cache.SetAsync("not-yet-expired", new TestObject("Active", 1), options, ct: TestContext.Current.CancellationToken);

        (await Cache.GetAsync<TestObject>("not-yet-expired", TestContext.Current.CancellationToken)).Should().NotBeNull();
    }

    // ---- GetOrCreateAsync ----

    [Fact]
    public async Task GetOrCreateAsync_KeyNotFound_CallsFactory()
    {
        var factoryCalled = 0;

        var result = await Cache.GetOrCreateAsync<TestObject>("create-key", ct =>
            {
                factoryCalled++;
                return Task.FromResult<TestObject?>(new TestObject("Created", 1));
            }, ct: TestContext.Current.CancellationToken);

        factoryCalled.Should().Be(1);
        result.Should().Be(new TestObject("Created", 1));
    }

    [Fact]
    public async Task GetOrCreateAsync_KeyExists_DoesNotCallFactory()
    {
        await Cache.SetAsync("existing-create-key", new TestObject("Existing", 1), ct: TestContext.Current.CancellationToken);
        var factoryCalled = 0;

        var result = await Cache.GetOrCreateAsync<TestObject>("existing-create-key", ct =>
            {
                factoryCalled++;
                return Task.FromResult<TestObject?>(new TestObject("ShouldNotBeCalled", 99));
            }, ct: TestContext.Current.CancellationToken);

        factoryCalled.Should().Be(0);
        result.Should().Be(new TestObject("Existing", 1));
    }

    [Fact]
    public async Task GetOrCreateAsync_AfterCreate_SubsequentGetReturnsCachedValue()
    {
        await Cache.GetOrCreateAsync<TestObject>("populate-key", ct => Task.FromResult<TestObject?>(new TestObject("Populated", 1)), ct: TestContext.Current.CancellationToken);

        (await Cache.GetAsync<TestObject>("populate-key", TestContext.Current.CancellationToken)).Should().Be(new TestObject("Populated", 1));
    }
}
