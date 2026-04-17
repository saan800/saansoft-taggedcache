using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace SaanSoft.TaggedCache.Memory;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Configure <see cref="ITaggedCache"/> backed by in-process memory and register it as a service.
    /// Also registers <see cref="IDistributedCache"/>, so other dependencies can use that interface if they prefer and don't need tagging specific cache.
    /// </summary>
    /// <remarks>
    /// IMPORTANT: Not recommended for production environments — does not support distributed caching and will produce cache inconsistencies in multi-instance deployments.
    /// <see cref="ITaggedCache"/> is registered as <c>Singleton</c> because the in-process tag index must be shared across all requests.
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

