using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace SaanSoft.TaggedCache.AwsDynamoDb;

public static class DynamoDbSetup
{
    /// <summary>
    /// Ensures the required DynamoDB tables exist, creating them if they don't.
    /// Call this during application startup (e.g. Program.cs or Startup.cs) before the application begins handling requests.
    /// </summary>
    public static async Task ConfigureDynamoDbAsync(this IAmazonDynamoDB dynamoDb, DynamoDbTaggedCacheOptions options, CancellationToken ct = default)
    {
        await EnsureCacheTableExistsAsync(dynamoDb, options.CacheTableName, ct);
        await EnsureTagTableExistsAsync(dynamoDb, options.TagTableName, ct);
    }

    private static async Task EnsureCacheTableExistsAsync(IAmazonDynamoDB dynamoDb, string tableName, CancellationToken ct)
    {
        if (await TableExistsAsync(dynamoDb, tableName, ct))
            return;

        await dynamoDb.CreateTableAsync(new CreateTableRequest
        {
            TableName = tableName,
            KeySchema =
            [
                new KeySchemaElement { AttributeName = "CacheKey", KeyType = KeyType.HASH }
            ],
            AttributeDefinitions =
            [
                new AttributeDefinition { AttributeName = "CacheKey", AttributeType = ScalarAttributeType.S }
            ],
            BillingMode = BillingMode.PAY_PER_REQUEST
        }, ct);
    }

    private static async Task EnsureTagTableExistsAsync(IAmazonDynamoDB dynamoDb, string tableName, CancellationToken ct)
    {
        if (await TableExistsAsync(dynamoDb, tableName, ct))
            return;

        await dynamoDb.CreateTableAsync(new CreateTableRequest
        {
            TableName = tableName,
            KeySchema =
            [
                new KeySchemaElement { AttributeName = "Tag", KeyType = KeyType.HASH },
                new KeySchemaElement { AttributeName = "CacheKey", KeyType = KeyType.RANGE }
            ],
            AttributeDefinitions =
            [
                new AttributeDefinition { AttributeName = "Tag", AttributeType = ScalarAttributeType.S },
                new AttributeDefinition { AttributeName = "CacheKey", AttributeType = ScalarAttributeType.S }
            ],
            BillingMode = BillingMode.PAY_PER_REQUEST
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
