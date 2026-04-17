using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using SaanSoft.TaggedCache.Base;

namespace SaanSoft.TaggedCache.Memory;

public class MemoryTaggedCacheOptions : BaseTaggedCacheOptions
{
    /// <summary>
    /// Configuration used with AddMemoryCache.
    /// See <see cref="MemoryCacheServiceCollectionExtensions.AddMemoryCache(IServiceCollection, Action{MemoryCacheOptions})"/> for more details.
    /// </summary>
    public Action<MemoryCacheOptions>? ConfigureMemoryCache { get; set; }
}
