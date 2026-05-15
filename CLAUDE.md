# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Build
dotnet build

# Run all tests
dotnet test

# Run a single test project
dotnet test tests/SaanSoft.TaggedCache.Tests/

# Run a specific test
dotnet test --filter "FullyQualifiedName~TestClassName.TestMethodName"

# Pack NuGet packages
dotnet pack
```

## Architecture

**SaanSoft.TaggedCache** is a .NET library providing tag-based cache on top of `IDistributedCache`.

### Project Layout

- `src/SaanSoft.TaggedCache` — Core library: interfaces, base classes, models
- `src/SaanSoft.TaggedCache.Memory` — In-memory backend (`IMemoryCache`)
- `src/SaanSoft.TaggedCache.StackExchangeRedis` — Redis backend (`IConnectionMultiplexer`)
- `src/SaanSoft.TaggedCache.AwsDynamoDb` — DynamoDB backend (`IAmazonDynamoDB`, uses two tables: Cache + CacheTags)
- `tests/` — Test projects (xUnit, AutoFixture, FakeItEasy, AwesomeAssertions)

### Key Abstractions

- **`ITaggedCache`** — Main interface extending `IDistributedCache`. Adds typed get/set, bulk operations, and `RemoveByTagAsync` / `RemoveByAnyTagAsync`.
- **`ITaggedCacheOptions`** — Options interface: `SlidingRefreshThresholdFraction` (default 0.25), `TagKeyPrefix` (default `"tag:"`), `DefaultCacheOptions` (default 5 min absolute), `JsonSerializerOptions`.
- **`BaseTaggedCache<TCacheRecord, TPayload>`** — Template method base class. Handles key/tag normalization (trim + lowercase, case-insensitive), expiry checking, sliding refresh, and tag fanout. Subclasses implement five protected abstract methods: `GetRecordInternalAsync`, `UpsertRecordInternalAsync`, `UpdateExpiryInternalAsync`, `RemoveRecordInternalAsync`, `GetCacheKeysForTagAsync`.
- **`BaseCacheRecord<TPayload>`** — Stores key, payload, expiry times, sliding duration, and tags.
- **`TaggedCacheItem<T>`** — Input model for set operations: key + value + optional tags.

### Adding a New Backend

1. Create a record type extending `BaseCacheRecord<TPayload>`.
2. Create an options class implementing `ITaggedCacheOptions`.
3. Implement `BaseTaggedCache<TRecord, TPayload>` with the five abstract methods.
4. Add a `ServiceCollectionExtensions` class with an `Add*TaggedCache()` method.

### Sync vs Async

The synchronous `IDistributedCache` methods (`Get`, `Set`, `Refresh`, `Remove`) are implemented via `.GetAwaiter().GetResult()` and can deadlock in environments with an active `SynchronizationContext` (ASP.NET classic, Blazor Server, WPF). Always prefer the async variants.

## Build Configuration

- Target: `net10.0`; nullable enabled; warnings as errors
- Package versions managed centrally in `Directory.Packages.props`
- Test projects are auto-detected by project name ending in `.Tests` or containing `.Tests.`
- Coverage collected via Coverlet, output to `reports/coverage/` in OpenCover + JSON formats
