using Microsoft.Extensions.DependencyInjection;

namespace SaanSoft.TaggedCache.OutputCache;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Configure <see cref="Microsoft.AspNetCore.OutputCaching.IOutputCacheStore"/> to be backed by the registered <see cref="ITaggedCache"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="Microsoft.AspNetCore.OutputCaching.IOutputCacheStore"/> is registered as <c>Scoped</c>; all cache state lives in registered <see cref="ITaggedCache"/>.
    /// <see cref="TaggedOutputCacheStoreOptions"/> is registered as <c>Singleton</c>.
    /// </remarks>
    public static IServiceCollection AddTaggedOutputCacheStore(this IServiceCollection services, TaggedOutputCacheStoreOptions? cacheOptions = null)
    {
        var options = cacheOptions ?? new TaggedOutputCacheStoreOptions();
        services.AddSingleton(_ => options);
        services.AddScoped<Microsoft.AspNetCore.OutputCaching.IOutputCacheStore, TaggedOutputCacheStore>();
        return services;
    }
}
