using Amazon.DynamoDBv2;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace SaanSoft.TaggedCache.AwsDynamoDb;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Configure <see cref="ITaggedCache"/> backed by AWS DynamoDB and register it as a service.
    /// Also registers <see cref="IDistributedCache"/>, so other dependencies can use that interface if they prefer and don't need tagging specific cache.
    /// IMPORTANT: you must register <see cref="IAmazonDynamoDB"/> in the service collection yourself.
    /// </summary>
    public static IServiceCollection AddDynamoDbTaggedCache(this IServiceCollection services, DynamoDbTaggedCacheOptions? cacheOptions = null)
    {
        services.AddScoped(_ => cacheOptions ??= new DynamoDbTaggedCacheOptions());
        services.AddScoped<ITaggedCache, DynamoDbTaggedCache>();
        services.AddDistributedCache();

        return services;
    }
}
