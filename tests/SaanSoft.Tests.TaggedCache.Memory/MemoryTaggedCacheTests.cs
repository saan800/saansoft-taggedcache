using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using SaanSoft.TaggedCache;
using SaanSoft.TaggedCache.Memory;
using SaanSoft.Tests.TaggedCache.Base;

namespace SaanSoft.Tests.TaggedCache.Memory;

public class MemoryTaggedCacheTests : BaseTaggedCacheTests
{
    private IMemoryCache? _memoryCache;

    protected override Task<ITaggedCache> CreateCache()
    {
        _memoryCache = new MemoryCache(Options.Create(new MemoryCacheOptions()));
        return Task.FromResult((ITaggedCache)new MemoryTaggedCache(_memoryCache, new MemoryTaggedCacheOptions()));
    }
}
