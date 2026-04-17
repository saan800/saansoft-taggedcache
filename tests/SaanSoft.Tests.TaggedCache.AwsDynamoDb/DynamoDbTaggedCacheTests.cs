using Amazon.DynamoDBv2;
using Amazon.Runtime;
using DotNet.Testcontainers.Builders;
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

        return new DynamoDbTaggedCache(db, new DynamoDbTaggedCacheOptions());
    }

    public override async Task InitializeAsync()
    {
        try
        {
            await base.InitializeAsync();
        }
        catch (Exception ex) when (IsDockerUnavailable(ex))
        {
            SetSkipCache($"Docker is not available: {ex.GetBaseException().Message}");
        }
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

    private static bool IsDockerUnavailable(Exception ex)
    {
        var current = (Exception?)ex;
        while (current is not null)
        {
            if (current is DockerUnavailableException) return true;
            if (current is TypeInitializationException tie &&
                tie.TypeName?.StartsWith("DotNet.Testcontainers") == true) return true;
            current = current.InnerException;
        }
        return false;
    }
}
