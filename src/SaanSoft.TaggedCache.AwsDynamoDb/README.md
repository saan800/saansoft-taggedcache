# SaanSoft.TaggedCache.AwsDynamoDb

AWS DynamoDB backend for [SaanSoft.TaggedCache](https://www.nuget.org/packages/SaanSoft.TaggedCache) — tag-based cache on top of `IDistributedCache`, backed by two DynamoDB tables.

## Installation

```
dotnet add package SaanSoft.TaggedCache.AwsDynamoDb
```

## DynamoDB tables

This backend requires **two DynamoDB tables** (names are configurable via `DynamoDbTaggedCacheOptions`):

| Table | Default name | Purpose |
|-------|-------------|---------|
| Cache | `Cache` | Stores the serialised cache entries |
| Tags | `CacheTags` | Stores the tag-to-key index used for `xxByTagAsync` operations |

### Creating tables

`SaanSoft.TaggedCache.AwsDynamoDb` provides a `ConfigureDynamoDbTaggedCacheTables` extension method on `IServiceProvider` that creates both tables if they do not already exist. 
Call it once during application startup before the app starts handling requests.

The `ConfigureDynamoDbTaggedCacheTables` requires a `CacheTableOptions` object, which has DynamoDb setup for things like:
* BillingMode
* OnDemandThroughput
* ... (and other standard DynamoDb table creation options)

NOTE: The following fields in CacheTableOptions will be overridden with SaanSoft.TaggedCache.AwsDynamoDb specific configuration, so don't provide these:
* TableName (populated from `DynamoDbTaggedCacheOptions`)
* KeySchema
* AttributeDefinitions

`KeySchema` and `AttributeDefinitions` are created as per below for each table.

Check the [Setup](#Setup) section below for an example of running during app startup.

### Table schemas

**Cache table** (`CacheTableName`):

| Attribute | Type | Key |
|-----------|------|-----|
| `CacheKey` | String | Hash (partition) |

**Tags table** (`TagTableName`):

| Attribute | Type | Key |
|-----------|------|-----|
| `Tag` | String | Hash (partition) |
| `CacheKey` | String | Range (sort) |

If you manage tables externally (e.g. via Terraform or CloudFormation), ensure the schemas match the above.

## Setup

You must register `IAmazonDynamoDB` yourself before calling `AddDynamoDbTaggedCache`.

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Register the DynamoDB client (required)
// Uses credentials and region from the standard AWS SDK configuration chain (environment variables, `appsettings.json`, EC2/ECS instance metadata, etc)
builder.Services.AddAWSService<IAmazonDynamoDB>();

// Register the tagged cache
builder.Services.AddDynamoDbTaggedCache();

var app = builder.Build();

// Create DynamoDb tables (and their indexes) if they don't exist during startup 
// The method is safe to call on every startup
// To update existing tables with the options (i.e. "Upsert Table"), provide the parameter `updateExisting=true` to the function
await app.Services.ConfigureDynamoDbTaggedCacheTables(new CacheTableOptions
{
    BillingMode = BillingMode.PAY_PER_REQUEST
});

app.Run();
```


## Configuration

Pass a `DynamoDbTaggedCacheOptions` instance to customise behaviour (if not provided, it defaults to using "new DynamoDbTaggedCacheOptions()" and its defaults):

```csharp
builder.Services.AddDynamoDbTaggedCache(new DynamoDbTaggedCacheOptions
{
    // DynamoDB table names (if different from the defaults)
    CacheTableName = "MyApp_Cache",
    TagTableName = "MyApp_CacheTags",

    // Max retries on DynamoDB read/write operations
    MaxRetries = 3,

    // Default expiry when none is specified in SetAsync calls
    DefaultCacheOptions = new DistributedCacheEntryOptions
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
    },

    // Refresh a sliding-expiry entry when this fraction of its window remains
    SlidingRefreshThresholdFraction = 0.25,

    // Prefix added to tag storage keys (set to "" to disable)
    TagKeyPrefix = "tag:"
});
```

## DI registration

* `DynamoDbTaggedCacheOptions` is registered as **Singleton**
* `ITaggedCache` is registered as **Scoped**; all cache state lives in DynamoDB
* `IDistributedCache` is also registered **Scoped**, resolving to the `ITaggedCache` instance

## Usage

```csharp
app.MapGet("/products", async (ITaggedCache cache) =>
{
    var products = await cache.GetOrCreateAsync<List<Product>>(
        cacheKey: "products",
        factory: ct => await db.GetProductsAsync(ct),
        tags: ["products"]
    );
    return product;
});

app.MapGet("/products/{id}", async (int id, ITaggedCache cache) =>
{
    var product = await cache.GetAsync<Product>($"product:{id}");
    if (product is null)
    {
        product = await db.GetProductAsync(id);
        await cache.SetAsync($"product:{id}", product, tags: ["products"]);
    }
    return product;
});

app.MapPost("/products/invalidate", async (ITaggedCache cache) =>
{
    await cache.RemoveByTagAsync("products");
});
```
