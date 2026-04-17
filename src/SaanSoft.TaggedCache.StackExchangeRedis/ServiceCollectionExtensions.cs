using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace SaanSoft.TaggedCache.StackExchangeRedis;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Configure <see cref="ITaggedCache"/> backed by Redis and register it as a service.
    /// Also registers <see cref="IDistributedCache"/>, so other dependencies can use that interface if they prefer and don't need tagging specific cache.
    /// IMPORTANT: you must register <see cref="IConnectionMultiplexer"/> in the service collection yourself.
    /// </summary>
    /// <remarks>
    /// <see cref="ITaggedCache"/> is registered as <c>Scoped</c>; all cache state lives in Redis.
    /// <see cref="RedisTaggedCacheOptions"/> is registered as <c>Singleton</c>.
    /// </remarks>
    public static IServiceCollection AddRedisTaggedCache(this IServiceCollection services, RedisTaggedCacheOptions? cacheOptions = null)
    {
        var options = cacheOptions ?? new RedisTaggedCacheOptions();
        services.AddSingleton(_ => options);
        services.AddScoped<ITaggedCache, RedisTaggedCache>();
        services.AddDistributedCache();

        return services;
    }
}
