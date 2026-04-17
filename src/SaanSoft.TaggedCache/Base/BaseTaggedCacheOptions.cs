using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace SaanSoft.TaggedCache.Base;

public abstract class BaseTaggedCacheOptions : ITaggedCacheOptions
{
    private decimal _slidingRefreshThresholdFraction = 0.25m;
    private string _tagKeyPrefix = "tag:";

    public decimal SlidingRefreshThresholdFraction
    {
        get => _slidingRefreshThresholdFraction;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, 1.0m);
            _slidingRefreshThresholdFraction = value;
        }
    }

    public string TagKeyPrefix
    {
        get => _tagKeyPrefix;
        set
        {
            var val = value.Trim();
            if (string.IsNullOrWhiteSpace(val))
            {
                _tagKeyPrefix = "";
                return;
            }

            _tagKeyPrefix = val;
        }
    }

    public DistributedCacheEntryOptions DefaultCacheOptions { get; set; } = new DistributedCacheEntryOptions
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
    };

    public JsonSerializerOptions JsonSerializerOptions { get; set; } = new JsonSerializerOptions(JsonSerializerDefaults.Web);

}
