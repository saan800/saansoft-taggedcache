using SaanSoft.TaggedCache.Base;

namespace SaanSoft.TaggedCache.AwsDynamoDb;

public class DynamoDbTaggedCacheOptions : BaseTaggedCacheOptions
{
    private string _cacheTableName = "Cache";
    private string _tagTableName = "CacheTags";
    private int _maxRetries = 2;

    /// <summary>
    /// Name of the dynamoDB table to use for storing cache entries.
    /// </summary>
    /// <remarks>
    /// Default: "Cache"
    /// </remarks>
    public string CacheTableName
    {
        get => _cacheTableName;
        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            _cacheTableName = value;
        }
    }

    /// <summary>
    /// Name of the dynamoDB table to use for storing cache tag entries.
    /// </summary>
    /// <remarks>
    /// Default: "CacheTags"
    /// </remarks>
    public string TagTableName
    {
        get => _tagTableName;
        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            _tagTableName = value;
        }
    }

    /// <summary>
    /// When reading or writing to DynamoDB, max number of retries on the operation.
    /// </summary>
    /// <remarks>
    /// Default: 2
    /// Must be 0 or greater.
    /// </remarks>
    public int MaxRetries
    {
        get => _maxRetries;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _maxRetries = value;
        }
    }
}
