using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace SaanSoft.TaggedCache.Memory;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Configure ITagg<see cref="ITaggedCache"/> backed by MemoryDistributedCache and register it as a service.
    /// Also registers <see cref="IDistributedCache"/>, so other dependencies can use that interface if they prefer and don't need tagging specific cache.
    /// </summary>
    /// <remarks>
    /// IMPORTANT: It is not recommended to use this in production environments, as it does not support distributed caching and may lead to cache inconsistencies in multi-instance scenarios.
    /// </remarks>
    public static IServiceCollection AddMemoryTaggedCache(this IServiceCollection services, MemoryTaggedCacheOptions? cacheOptions = null)
    {
        cacheOptions ??= new MemoryTaggedCacheOptions();

        services.AddMemoryCache(o => {
            cacheOptions.ConfigureMemoryCache?.Invoke(o);
        });
        services.AddSingleton(_ => cacheOptions);
        services.AddSingleton<ITaggedCache, MemoryTaggedCache>();
        services.AddDistributedCache();

        return services;
    }
}

