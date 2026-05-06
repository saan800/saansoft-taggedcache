using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.DependencyInjection;

namespace SaanSoft.TaggedCache.AwsDynamoDb;

/// <summary>
/// Provide DynamoDb table creation options
/// </summary>
/// <remarks>
/// The following fields in CacheTableOptions/CreateTableRequest will be overridden with SaanSoft.TaggedCache.AwsDynamoDb specific configuration, so don't provide these:
/// <code>
/// - TableName
/// - KeySchema
/// - AttributeDefinitions
/// </code>
/// </remarks>
public class CacheTableOptions : Amazon.DynamoDBv2.Model.CreateTableRequest
{
    public CacheTableOptions()
    {
    }
}

public static class DynamoDbSetup
{

    /// <summary>
    /// Ensures the required DynamoDB tables exist, creating them if they don't.
    /// Call this during application startup (e.g. Program.cs or Startup.cs) before the application begins handling requests.
    /// </summary>
    public static async Task ConfigureDynamoDbTaggedCacheTables(this IServiceProvider serviceProvider, CacheTableOptions cacheTableOptions, bool updateExisting = false, CancellationToken ct = default)
    {
        var dynamoDb = serviceProvider.GetRequiredService<IAmazonDynamoDB>();
        var options = serviceProvider.GetRequiredService<DynamoDbTaggedCacheOptions>();
        await dynamoDb.ConfigureDynamoDbTaggedCacheTables(options, cacheTableOptions, updateExisting, ct);
    }

    /// <summary>
    /// Ensures the required DynamoDB tables exist, creating them if they don't.
    /// Call this during application startup (e.g. Program.cs or Startup.cs) before the application begins handling requests.
    /// </summary>
    public static async Task ConfigureDynamoDbTaggedCacheTables(this IAmazonDynamoDB dynamoDb, DynamoDbTaggedCacheOptions options, CacheTableOptions cacheTableOptions, bool updateExisting = false, CancellationToken ct = default)
    {
        await EnsureCacheTableExistsAsync(dynamoDb, options.CacheTableName, cacheTableOptions, updateExisting, ct);
        await EnsureTagTableExistsAsync(dynamoDb, options.TagTableName, cacheTableOptions, updateExisting, ct);
    }

    private static async Task EnsureCacheTableExistsAsync(IAmazonDynamoDB dynamoDb, string tableName, CacheTableOptions cacheTableOptions, bool updateExisting, CancellationToken ct)
    {
        cacheTableOptions.TableName = tableName;
        cacheTableOptions.KeySchema =
            [
                new KeySchemaElement { AttributeName = "CacheKey", KeyType = KeyType.HASH }
            ];
        cacheTableOptions.AttributeDefinitions =
            [
                new AttributeDefinition { AttributeName = "CacheKey", AttributeType = ScalarAttributeType.S }
            ];

        await EnsureTableExistsAsync(dynamoDb, cacheTableOptions, updateExisting, ct);
    }

    private static async Task EnsureTagTableExistsAsync(IAmazonDynamoDB dynamoDb, string tableName, CacheTableOptions cacheTableOptions, bool updateExisting, CancellationToken ct)
    {
        cacheTableOptions.TableName = tableName;
        cacheTableOptions.KeySchema =
            [
                new KeySchemaElement { AttributeName = "Tag", KeyType = KeyType.HASH },
                new KeySchemaElement { AttributeName = "CacheKey", KeyType = KeyType.RANGE }
            ];
        cacheTableOptions.AttributeDefinitions =
            [
                new AttributeDefinition { AttributeName = "Tag", AttributeType = ScalarAttributeType.S },
                new AttributeDefinition { AttributeName = "CacheKey", AttributeType = ScalarAttributeType.S }
            ];

        await EnsureTableExistsAsync(dynamoDb, cacheTableOptions, updateExisting, ct);
    }

    private static async Task EnsureTableExistsAsync(IAmazonDynamoDB dynamoDb, CacheTableOptions cacheTableOptions, bool updateExisting, CancellationToken ct)
    {

        if (await TableExistsAsync(dynamoDb, cacheTableOptions.TableName, ct))
        {
            if (updateExisting)
            {
                await UpdateTableAsync(dynamoDb, cacheTableOptions, ct);
            }

            return;
        }

        await dynamoDb.CreateTableAsync(cacheTableOptions, ct);
    }

    private static async Task UpdateTableAsync(IAmazonDynamoDB dynamoDb, CacheTableOptions cacheTableOptions, CancellationToken ct)
    {
        await dynamoDb.UpdateTableAsync(new UpdateTableRequest
        {
            TableName = cacheTableOptions.TableName,
            TableClass = cacheTableOptions.TableClass,
            AttributeDefinitions = cacheTableOptions.AttributeDefinitions,
            BillingMode = cacheTableOptions.BillingMode,
            DeletionProtectionEnabled = cacheTableOptions.DeletionProtectionEnabled,
            OnDemandThroughput = cacheTableOptions.OnDemandThroughput,
            ProvisionedThroughput = cacheTableOptions.ProvisionedThroughput,
            WarmThroughput = cacheTableOptions.WarmThroughput,
            SSESpecification = cacheTableOptions.SSESpecification,
            StreamSpecification = cacheTableOptions.StreamSpecification,
        });
    }

    private static async Task<bool> TableExistsAsync(IAmazonDynamoDB dynamoDb, string tableName, CancellationToken ct)
    {
        try
        {
            await dynamoDb.DescribeTableAsync(tableName, ct);
            return true;
        }
        catch (ResourceNotFoundException)
        {
            return false;
        }
    }
}
