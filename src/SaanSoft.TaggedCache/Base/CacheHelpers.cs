using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace SaanSoft.TaggedCache.Base;

public static class CacheHelpers
{
    public static IEnumerable<List<T>> Batch<T>(this IEnumerable<T> source, int size)
    {
        var batch = new List<T>(size);

        foreach (var item in source)
        {
            batch.Add(item);
            if (batch.Count == size)
            {
                yield return batch;
                batch = new List<T>(size);
            }
        }

        if (batch.Count > 0)
            yield return batch;
    }

    public static TimeSpan Backoff(this int attempt) => TimeSpan.FromMilliseconds(100 * Math.Pow(2, attempt));

    public static byte[] AsByteArray<T>(this T obj, JsonSerializerOptions? jsonOptions = null)
        => JsonSerializer.SerializeToUtf8Bytes(obj, jsonOptions ?? new JsonSerializerOptions(JsonSerializerDefaults.Web));

    public static byte[] AsByteArray(this string str)
        => System.Text.Encoding.UTF8.GetBytes(str);

    public static string AsString(this byte[] bytes)
        => System.Text.Encoding.UTF8.GetString(bytes);

    /// <summary>
    /// Work out the expiry of this cache entry based on the options provided.
    /// </summary>
    /// <remarks>
    ///  This is used internally to determine when the cache entry should expire.
    /// </remarks>
    public static ResolvedExpiry Resolve(this DistributedCacheEntryOptions options, DateTimeOffset nowUtc)
    {
        var absolute = options.AbsoluteExpiration?.ToUniversalTime();

        if (absolute is null && options.AbsoluteExpirationRelativeToNow is not null)
            absolute = nowUtc.Add(options.AbsoluteExpirationRelativeToNow.Value);

        if (absolute is null && (options.SlidingExpiration is null || options.SlidingExpiration.Value == TimeSpan.Zero))
            throw new ArgumentException("At least one of absolute, relative or sliding expiration must be provided.");

        var initialExpiry = absolute is null
            ? nowUtc.Add(options.SlidingExpiration!.Value)
            : options.SlidingExpiration is null
                ? absolute.Value
                : Min(absolute.Value, nowUtc.Add(options.SlidingExpiration.Value));

        return new ResolvedExpiry(
            ExpiresAtUtc: initialExpiry,
            AbsoluteExpiresAtUtc: absolute,
            SlidingExpiration: options.SlidingExpiration);

    }

    private static DateTimeOffset Min(DateTimeOffset a, DateTimeOffset b)
        => a <= b ? a : b;
}
