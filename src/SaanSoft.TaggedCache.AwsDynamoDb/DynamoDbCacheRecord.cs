using SaanSoft.TaggedCache.Base;

namespace SaanSoft.TaggedCache.AwsDynamoDb;

public sealed class DynamoDbCacheRecord : BaseCacheRecord<string>
{
    public override string PayloadAsString() => Payload;
}
