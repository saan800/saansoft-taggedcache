using SaanSoft.TaggedCache.Base;

namespace SaanSoft.TaggedCache.Memory;

public sealed class MemoryCacheRecord : BaseCacheRecord<byte[]>
{
    public object Sync { get; } = new();

    public override string PayloadAsString() => Payload.AsString();
}
