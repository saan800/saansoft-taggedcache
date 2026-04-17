using DotNet.Testcontainers.Builders;
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

    public override async Task InitializeAsync()
    {
        try
        {
            await base.InitializeAsync();
        }
        catch (Exception ex) when (IsDockerUnavailable(ex))
        {
            SetSkipCache($"Docker is not available: {ex.GetBaseException().Message}");
        }
    }

    public override async Task DisposeAsync()
    {
        await base.DisposeAsync();

        if (_container != null)
            await _container.DisposeAsync();
    }

    private static bool IsDockerUnavailable(Exception ex)
    {
        var current = (Exception?)ex;
        while (current is not null)
        {
            if (current is DockerUnavailableException) return true;
            if (current is TypeInitializationException tie &&
                tie.TypeName?.StartsWith("DotNet.Testcontainers") == true) return true;
            current = current.InnerException;
        }
        return false;
    }
}
