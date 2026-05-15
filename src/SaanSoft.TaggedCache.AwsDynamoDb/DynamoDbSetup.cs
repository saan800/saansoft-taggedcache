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
public class CacheTableOptions : CreateTableRequest;

public static class DynamoDbSetup
{

    /// <summary>
    /// Ensures the required DynamoDB tables exist, creating them if they don't.
    /// Also configures TTL on the <c>ExpiresAtUnix</c> attribute for both tables.
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
    /// Also configures TTL on the <c>ExpiresAtUnix</c> attribute for both tables.
    /// Call this during application startup (e.g. Program.cs or Startup.cs) before the application begins handling requests.
    /// </summary>
    public static async Task ConfigureDynamoDbTaggedCacheTables(this IAmazonDynamoDB dynamoDb, DynamoDbTaggedCacheOptions options, CacheTableOptions cacheTableOptions, bool updateExisting = false, CancellationToken ct = default)
    {
        await EnsureCacheTableExistsAsync(dynamoDb, options.CacheTableName, cacheTableOptions, updateExisting, ct);
        await EnsureTagTableExistsAsync(dynamoDb, options.TagTableName, cacheTableOptions, updateExisting, ct);
    }

    private static async Task EnsureCacheTableExistsAsync(IAmazonDynamoDB dynamoDb, string tableName, CacheTableOptions cacheTableOptions, bool updateExisting, CancellationToken ct)
    {
        var request = BuildCreateRequest(
            cacheTableOptions,
            tableName,
            keySchema: [new KeySchemaElement { AttributeName = "CacheKey", KeyType = KeyType.HASH }],
            attributeDefinitions: [new AttributeDefinition { AttributeName = "CacheKey", AttributeType = ScalarAttributeType.S }]);

        await EnsureTableExistsAsync(dynamoDb, request, updateExisting, ct);
    }

    private static async Task EnsureTagTableExistsAsync(IAmazonDynamoDB dynamoDb, string tableName, CacheTableOptions cacheTableOptions, bool updateExisting, CancellationToken ct)
    {
        var request = BuildCreateRequest(
            cacheTableOptions,
            tableName,
            keySchema: [
                new KeySchemaElement { AttributeName = "Tag", KeyType = KeyType.HASH },
                new KeySchemaElement { AttributeName = "CacheKey", KeyType = KeyType.RANGE }
            ],
            attributeDefinitions: [
                new AttributeDefinition { AttributeName = "Tag", AttributeType = ScalarAttributeType.S },
                new AttributeDefinition { AttributeName = "CacheKey", AttributeType = ScalarAttributeType.S }
            ]);

        await EnsureTableExistsAsync(dynamoDb, request, updateExisting, ct);
    }

    private static CreateTableRequest BuildCreateRequest(
        CacheTableOptions options,
        string tableName,
        List<KeySchemaElement> keySchema,
        List<AttributeDefinition> attributeDefinitions) =>
        new()
        {
            TableName = tableName,
            KeySchema = keySchema,
            AttributeDefinitions = attributeDefinitions,
            BillingMode = options.BillingMode,
            ProvisionedThroughput = options.ProvisionedThroughput,
            TableClass = options.TableClass,
            SSESpecification = options.SSESpecification,
            StreamSpecification = options.StreamSpecification,
            DeletionProtectionEnabled = options.DeletionProtectionEnabled,
            OnDemandThroughput = options.OnDemandThroughput,
            WarmThroughput = options.WarmThroughput,
        };

    private static async Task EnsureTableExistsAsync(IAmazonDynamoDB dynamoDb, CreateTableRequest request, bool updateExisting, CancellationToken ct)
    {
        if (await TableExistsAsync(dynamoDb, request.TableName, ct))
        {
            if (updateExisting)
                await UpdateTableAsync(dynamoDb, request, ct);

            await EnsureTimeToLiveAsync(dynamoDb, request.TableName, ct);
            return;
        }

        await dynamoDb.CreateTableAsync(request, ct);
        await EnsureTimeToLiveAsync(dynamoDb, request.TableName, ct);
    }

    private static async Task UpdateTableAsync(IAmazonDynamoDB dynamoDb, CreateTableRequest request, CancellationToken ct)
    {
        await dynamoDb.UpdateTableAsync(new UpdateTableRequest
        {
            TableName = request.TableName,
            TableClass = request.TableClass,
            AttributeDefinitions = request.AttributeDefinitions,
            BillingMode = request.BillingMode,
            DeletionProtectionEnabled = request.DeletionProtectionEnabled,
            OnDemandThroughput = request.OnDemandThroughput,
            ProvisionedThroughput = request.ProvisionedThroughput,
            WarmThroughput = request.WarmThroughput,
            SSESpecification = request.SSESpecification,
            StreamSpecification = request.StreamSpecification,
        }, ct);
    }

    private static async Task EnsureTimeToLiveAsync(IAmazonDynamoDB dynamoDb, string tableName, CancellationToken ct)
    {
        var describe = await dynamoDb.DescribeTimeToLiveAsync(new DescribeTimeToLiveRequest { TableName = tableName }, ct);
        var status = describe.TimeToLiveDescription.TimeToLiveStatus;
        if (status == TimeToLiveStatus.ENABLED || status == TimeToLiveStatus.ENABLING)
            return;

        await dynamoDb.UpdateTimeToLiveAsync(new UpdateTimeToLiveRequest
        {
            TableName = tableName,
            TimeToLiveSpecification = new TimeToLiveSpecification
            {
                AttributeName = "ExpiresAtUnix",
                Enabled = true
            }
        }, ct);
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
