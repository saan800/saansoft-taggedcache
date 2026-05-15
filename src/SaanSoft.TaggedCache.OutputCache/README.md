# SaanSoft.TaggedCache.OutputCache

Provides an implementation of `IOutputCacheStore` that uses the configured `ITaggedCache` for web/api applications.


## Installation

```
dotnet add package SaanSoft.TaggedCache.OutputCache
```

## Setup


You must register a provider for `ITaggedCache` as well as `AddTaggedOutputCacheStore`.

e.g.


```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRedisTaggedCache();
builder.Services.AddTaggedOutputCacheStore();

// add any other output cache config

var app = builder.Build();
app.Run();
```

All other usage and configuration options for output cache are done as normal.

Read these for more information on usage and configuration options:

* [Output caching middleware in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/output)
* [Improving Performance with Output Caching (C#)](https://learn.microsoft.com/en-us/aspnet/mvc/overview/older-versions-1/controllers-and-routing/improving-performance-with-output-caching-cs)

## Configuration

### Available options

| Property | Default | Description |
|----------|---------|-------------|
| `CacheKeyPrefix` | `"outputcache:"` | Prefix applied to output cache storage keys |
