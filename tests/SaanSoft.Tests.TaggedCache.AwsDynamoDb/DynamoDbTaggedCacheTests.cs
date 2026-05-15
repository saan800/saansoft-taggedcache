using Amazon.DynamoDBv2;
using Amazon.Runtime;
using SaanSoft.TaggedCache;
using SaanSoft.TaggedCache.AwsDynamoDb;
using SaanSoft.Tests.TaggedCache.Base;
using Testcontainers.DynamoDb;

namespace SaanSoft.Tests.TaggedCache.AwsDynamoDb;

public class DynamoDbTaggedCacheTests : BaseTaggedCacheTests
{
    private DynamoDbContainer? _container;

    protected override async Task<ITaggedCache> CreateCache()
    {
        _container = new DynamoDbBuilder("amazon/dynamodb-local:latest")
            .WithCleanUp(true)
            .Build();

        await _container.StartAsync();

        IAmazonDynamoDB db = new AmazonDynamoDBClient(
            new BasicAWSCredentials("test", "test"),  // dummy creds, any value works
            new AmazonDynamoDBConfig
            {
                ServiceURL = _container.GetConnectionString()
            }
        );

        var options = new DynamoDbTaggedCacheOptions();
        await db.ConfigureDynamoDbTaggedCacheTables(options, new CacheTableOptions { BillingMode = BillingMode.PAY_PER_REQUEST });

        return new DynamoDbTaggedCache(db, options);
    }

    public override async Task DisposeAsync()
    {
        await base.DisposeAsync();

        if (_container != null)
        {
            await _container.StopAsync();
            await _container.DisposeAsync();
        }
    }
}
