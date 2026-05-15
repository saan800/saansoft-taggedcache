# SaanSoft.TaggedCache.Memory

In-memory backend for [SaanSoft.TaggedCache](https://www.nuget.org/packages/SaanSoft.TaggedCache) — tag-based cache on top of `IDistributedCache`.

> **Not recommended for production.** This backend stores all cache data in-process. It does not support distributed caching and will produce inconsistencies in multi-instance deployments. Use it for local development and testing only.

## Installation

```
dotnet add package SaanSoft.TaggedCache.Memory
```

## Setup

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMemoryTaggedCache();

var app = builder.Build();
app.Run();
```

## Configuration

Pass a `MemoryTaggedCacheOptions` instance to customise behaviour:

```csharp
builder.Services.AddMemoryTaggedCache(new MemoryTaggedCacheOptions
{
    // Default expiry when none is specified in SetAsync calls
    DefaultCacheOptions = new DistributedCacheEntryOptions
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
    },

    // Refresh a sliding-expiry entry when this fraction of its window remains
    SlidingRefreshThresholdFraction = 0.25,

    // Prefix added to tag storage keys (set to "" to disable)
    TagKeyPrefix = "tag:",

    // Customise the underlying IMemoryCache
    ConfigureMemoryCache = memOptions =>
    {
        memOptions.SizeLimit = 1024;
    }
});
```

### Available options

| Property | Default | Description |
|----------|---------|-------------|
| `DefaultCacheOptions` | 5 min absolute | Expiry used when none is supplied to `SetAsync` |
| `SlidingRefreshThresholdFraction` | `0.25` | Fraction of sliding window remaining at which to proactively refresh; must be `> 0` and `<= 1` |
| `TagKeyPrefix` | `"tag:"` | Prefix applied to tag storage keys |
| `JsonSerializerOptions` | `JsonSerializerDefaults.Web` | Controls serialisation of cached values |
| `ConfigureMemoryCache` | `null` | Optional `Action<MemoryCacheOptions>` forwarded to `AddMemoryCache` |

## DI registration

`ITaggedCache` is registered as **Singleton** because the in-process tag index must be shared across all requests.
`IDistributedCache` is also registered, resolving to the same `ITaggedCache` instance.

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
