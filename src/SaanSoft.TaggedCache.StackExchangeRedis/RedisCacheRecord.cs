using SaanSoft.TaggedCache.Base;

namespace SaanSoft.TaggedCache.StackExchangeRedis;

public sealed class RedisCacheRecord : BaseCacheRecord<string>
{
    public override string PayloadAsString() => Payload;
}
