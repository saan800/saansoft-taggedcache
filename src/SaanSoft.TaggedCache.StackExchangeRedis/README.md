# SaanSoft.TaggedCache.StackExchangeRedis

Redis backend for [SaanSoft.TaggedCache](https://www.nuget.org/packages/SaanSoft.TaggedCache) — tag-based cache on top of `IDistributedCache`, backed by Redis via StackExchange.Redis.

## Installation

```
dotnet add package SaanSoft.TaggedCache.StackExchangeRedis
```

## Setup

You must register `IConnectionMultiplexer` yourself before calling `AddRedisTaggedCache`.

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Register the Redis connection (required)
builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")!)
);

// Register the tagged cache
builder.Services.AddRedisTaggedCache();

var app = builder.Build();
app.Run();
```

## Configuration

Pass a `RedisTaggedCacheOptions` instance to customise behaviour:

```csharp
builder.Services.AddRedisTaggedCache(new RedisTaggedCacheOptions
{
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

### Available options

| Property | Default | Description |
|----------|---------|-------------|
| `DefaultCacheOptions` | 5 min absolute | Expiry used when none is supplied to `SetAsync` |
| `SlidingRefreshThresholdFraction` | `0.25` | Fraction of sliding window remaining at which to proactively refresh; must be `> 0` and `<= 1` |
| `TagKeyPrefix` | `"tag:"` | Prefix applied to tag storage keys in Redis |
| `JsonSerializerOptions` | `JsonSerializerDefaults.Web` | Controls serialisation of cached values |

## DI registration

`ITaggedCache` is registered as **Scoped**; all cache state lives in Redis.
`RedisTaggedCacheOptions` is registered as **Singleton**.
`IDistributedCache` is also registered, resolving to the `ITaggedCache` instance.

## Usage

```csharp
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
