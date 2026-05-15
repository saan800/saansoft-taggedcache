# SaanSoft.TaggedCache

A .NET library providing tag-based cache on top of `IDistributedCache`.

Cache entries can be tagged with one or more string labels, then invalidated in bulk by tag — without needing to track individual cache keys.

## Overview

This package is the **core library**: it defines `ITaggedCache`, `ITaggedCacheOptions`, and the base classes used by all backend implementations. You will typically install one of the backend packages instead:

| Backend | Package |
|---------|---------|
| In-memory (dev/test) | `SaanSoft.TaggedCache.Memory` |
| Redis | `SaanSoft.TaggedCache.StackExchangeRedis` |
| AWS DynamoDB | `SaanSoft.TaggedCache.AwsDynamoDb` |

## Usage

Once a backend is registered, inject `ITaggedCache` wherever you need it.

NOTE: As `ITaggedCache` extends `IDistributedCache`, any usages of `IDistributedCache` will work with the configured backend implementation, and references to `IDistributedCache` will still work as expected.

### Get and set with tags

```csharp
// Set a single item with tags
await cache.SetAsync("product:42", product, tags: ["products", "category:electronics"]);

// Get a typed item
var product = await cache.GetAsync<Product>("product:42");

// Get an item, and if it doesn't exist in cache - create it, set in the cache and return the item
// NOTE: return type is `T?`, to handle the possibility of creating the item being null
//       null values are not cached
var products = await cache.GetOrCreateAsync<List<Product>>(
    cacheKey: "products",
    factory: ct => await db.GetProductsAsync(ct),
    tags: ["products"]
);

// Set multiple items at once
await cache.SetManyAsync([
    new TaggedCacheItem<Product>("product:1", p1) { Tags = ["products"] },
    new TaggedCacheItem<Product>("product:2", p2) { Tags = ["products"] },
]);
```

### Invalidate by tag

```csharp
// Remove all cache entries tagged "products"
await cache.RemoveByTagAsync("products");

// Remove all cache entries matching any of the given tags
await cache.RemoveByAnyTagAsync(["products", "category:electronics"]);
```

### Custom expiry

```csharp
var options = new DistributedCacheEntryOptions
{
    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1),
    SlidingExpiration = TimeSpan.FromMinutes(15)
};

await cache.SetAsync("user:99", user, options, tags: ["users"]);
```

## Key normalisation

All cache keys and tags are trimmed and lowercased before use. Operations are case-insensitive.
