using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace SaanSoft.TaggedCache;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IDistributedCache"/> with the <see cref="ITaggedCache"/> implementation, so other dependencies can use that interface if they prefer and don't need tagging specific cache.
    /// Does not register the <see cref="ITaggedCache"/> itself.
    /// </summary>
    /// <remarks>
    /// Intended for to be called by <see cref="ITaggedCache"/> implementations, so users only need to call e.g. <c>services.AddMemoryTaggedCache()</c>
    /// </remarks>
    public static IServiceCollection AddDistributedCache(this IServiceCollection services)
    {
        services.AddScoped<IDistributedCache>(sp => sp.GetRequiredService<ITaggedCache>());
        return services;
    }
}
public class Product { };
