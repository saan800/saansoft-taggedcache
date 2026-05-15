using SaanSoft.TaggedCache;
using SaanSoft.TaggedCache.StackExchangeRedis;
using SaanSoft.Tests.TaggedCache.Base;
using StackExchange.Redis;
using Testcontainers.Redis;

namespace SaanSoft.Tests.TaggedCache.StackExchangeRedis;

public class RedisTaggedCacheTests : BaseTaggedCacheTests
{
    private RedisContainer? _container;

    protected override async Task<ITaggedCache> CreateCache()
    {
        _container = new RedisBuilder("redis:latest").Build();
        await _container.StartAsync().ConfigureAwait(false);

        var connection = await ConnectionMultiplexer.ConnectAsync(_container.GetConnectionString());
        return new RedisTaggedCache(connection, new RedisTaggedCacheOptions());
    }

    public override async Task DisposeAsync()
    {
        await base.DisposeAsync();

        if (_container != null)
            await _container.DisposeAsync();
    }
}
