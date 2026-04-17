using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Caching.Distributed;
using SaanSoft.TaggedCache.Base;
using System.Text.Json;

namespace SaanSoft.TaggedCache.AwsDynamoDb;

public class DynamoDbTaggedCache(IAmazonDynamoDB dynamoDb, DynamoDbTaggedCacheOptions cacheOptions) : BaseTaggedCache<DynamoDbCacheRecord, string>(cacheOptions), ITaggedCache
{
    public override async Task SetAsync<T>(string cacheKey, T value, DistributedCacheEntryOptions? options = null, IReadOnlyCollection<string>? tags = null, CancellationToken ct = default)
    {
        var normalizedCacheKey = NormalizeCacheKey(cacheKey);
        options ??= cacheOptions.DefaultCacheOptions;

        var existing = await GetRecordInternalAsync(normalizedCacheKey, ct);

        var nowUtc = DateTimeOffset.UtcNow;
        var resolved = options.Resolve(nowUtc);

        var newTags = NormalizeTags(tags);
        var obsoleteTags = (existing?.Tags ?? Array.Empty<string>()).Except(newTags, StringComparer.OrdinalIgnoreCase).ToList();

        var payload = JsonSerializer.Serialize(value, cacheOptions.JsonSerializerOptions);

        var record = new DynamoDbCacheRecord
        {
            CacheKey = normalizedCacheKey,
            Payload = payload,
            ExpiresAtUtc = resolved.ExpiresAtUtc,
            AbsoluteExpiresAtUtc = resolved.AbsoluteExpiresAtUtc,
            SlidingExpiration = resolved.SlidingExpiration,
            Tags = newTags
        };

        await UpsertRecordInternalAsync(record, obsoleteTags, ct);
    }
    
    protected override async Task<HashSet<string>> GetCacheKeysForTagAsync(string normalizedTag, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedTag);

        var cacheKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, AttributeValue>? lastEvaluatedKey = null;

        do
        {
            var response = await dynamoDb.QueryAsync(new QueryRequest
            {
                TableName = cacheOptions.TagTableName,
                KeyConditionExpression = "#tag = :tag",
                ExpressionAttributeNames = new Dictionary<string, string>
                {
                    ["#tag"] = "Tag"
                },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":tag"] = new() { S = normalizedTag }
                },
                ExclusiveStartKey = lastEvaluatedKey
            }, ct);

            foreach (var item in response.Items)
            {
                if (item.TryGetValue("CacheKey", out var keyAttr) && !string.IsNullOrWhiteSpace(keyAttr.S))
                    cacheKeys.Add(keyAttr.S);
            }

            lastEvaluatedKey = response.LastEvaluatedKey;
        }
        while (lastEvaluatedKey is { Count: > 0 });

        return cacheKeys;
    }

    protected override async Task<DynamoDbCacheRecord?> GetRecordInternalAsync(string normalizedCacheKey, CancellationToken ct)
    {
        var response = await dynamoDb.GetItemAsync(new GetItemRequest
        {
            TableName = cacheOptions.CacheTableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["CacheKey"] = new() { S = normalizedCacheKey }
            }
        }, ct);

        if (response.Item is null || response.Item.Count == 0)
            return null;

        var item = response.Item;

        return new DynamoDbCacheRecord
        {
            CacheKey = item["CacheKey"].S,
            Payload = item["Payload"].S,
            ExpiresAtUtc = DateTimeOffset.FromUnixTimeSeconds(long.Parse(item["ExpiresAtUnix"].N)),
            AbsoluteExpiresAtUtc = item.TryGetValue("AbsoluteExpiresAtUnix", out var abs)
                ? DateTimeOffset.FromUnixTimeSeconds(long.Parse(abs.N))
                : null,
            SlidingExpiration = item.TryGetValue("SlidingExpirationSeconds", out var slide)
                ? TimeSpan.FromSeconds(long.Parse(slide.N))
                : null,
            Tags = item.TryGetValue("Tags", out var tags) && tags.SS is not null
                ? tags.SS.ToArray()
                : Array.Empty<string>()
        };
    }

    //TODO: GetManyRecordsInternalAsync, RemoveManyRecordsInternalAsync

    protected override async Task RemoveRecordInternalAsync(string normalizedCacheKey, DynamoDbCacheRecord? existing, CancellationToken ct)
    {
        var transactItems = new List<TransactWriteItem>
        {
            new()
            {
                Delete = new Delete
                {
                    TableName = cacheOptions.CacheTableName,
                    Key = new Dictionary<string, AttributeValue>
                    {
                        ["CacheKey"] = new() { S = normalizedCacheKey }
                    }
                }
            }
        };

        if (existing is not null)
        {
            foreach (var tag in existing.Tags)
            {
                transactItems.Add(new TransactWriteItem
                {
                    Delete = new Delete
                    {
                        TableName = cacheOptions.TagTableName,
                        Key = new Dictionary<string, AttributeValue>
                        {
                            ["Tag"] = new() { S = tag },
                            ["CacheKey"] = new() { S = normalizedCacheKey }
                        }
                    }
                });
            }
        }

        foreach (var batch in transactItems.Batch(100))
        {
            await ExecuteTransactWriteWithRetryAsync(batch, ct);
        }
    }

    protected override async Task UpsertRecordInternalAsync(DynamoDbCacheRecord record, IReadOnlyCollection<string> oldTags, CancellationToken ct)
    {
        var resolved = new ResolvedExpiry(record.ExpiresAtUtc, record.AbsoluteExpiresAtUtc, record.SlidingExpiration);

        var transactItems = BuildSetTransaction(
            cacheKey: record.CacheKey,
            payload: record.Payload,
            resolved: resolved,
            newTags: record.Tags,
            oldTags: oldTags);

        foreach (var batch in transactItems.Batch(100))
        {
            await ExecuteTransactWriteWithRetryAsync(batch, ct);
        }
    }

    protected override async Task UpdateExpiryInternalAsync(string normalizedCacheKey, DateTimeOffset expiresAtUtc, DateTimeOffset? absoluteExpiresAtUtc, TimeSpan? slidingExpiration, CancellationToken ct)
    {
        var record = await GetRecordInternalAsync(normalizedCacheKey, ct);
        if (record == null || expiresAtUtc <= DateTimeOffset.UtcNow)
        {
            await RemoveRecordInternalAsync(normalizedCacheKey, record, ct);
            return;
        }

        var expiresAtUnix = record.ExpiresAtUtc.ToUnixTimeSeconds();
        var absoluteExpiresAtUnix = record.AbsoluteExpiresAtUtc?.ToUnixTimeSeconds();
        var slidingSeconds = record.SlidingExpiration is null
            ? (long?)null
            : checked((long)record.SlidingExpiration.Value.TotalSeconds);

        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var setParts = new List<string>
        {
            "ExpiresAtUnix = :exp"
        };

        var removeParts = new List<string>();
        var attributeValues = new Dictionary<string, AttributeValue>
        {
            [":exp"] = new() { N = expiresAtUnix.ToString() },
        };

        if (absoluteExpiresAtUnix.HasValue)
        {
            setParts.Add("AbsoluteExpiresAtUnix = :absExp");
            attributeValues[":absExp"] = new AttributeValue
            {
                N = absoluteExpiresAtUnix.Value.ToString()
            };
        }
        else
        {
            removeParts.Add("AbsoluteExpiresAtUnix");
        }

        if (slidingSeconds.HasValue)
        {
            setParts.Add("SlidingExpirationSeconds = :sliding");
            attributeValues[":sliding"] = new AttributeValue
            {
                N = slidingSeconds.Value.ToString()
            };
        }
        else
        {
            removeParts.Add("SlidingExpirationSeconds");
        }

        var updateExpression = $"SET {string.Join(", ", setParts)}";
        if (removeParts.Count > 0)
            updateExpression += $" REMOVE {string.Join(", ", removeParts)}";

        await dynamoDb.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = cacheOptions.CacheTableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["CacheKey"] = new() { S = record.CacheKey }
            },
            UpdateExpression = updateExpression,
            ExpressionAttributeValues = attributeValues
        }, ct);
    }

    private List<TransactWriteItem> BuildSetTransaction(
        string cacheKey,
        string payload,
        ResolvedExpiry resolved,
        IReadOnlyCollection<string> newTags,
        IReadOnlyCollection<string> oldTags)
    {
        var expiresAtUnix = resolved.ExpiresAtUtc.ToUnixTimeSeconds();

        var cacheItem = new Dictionary<string, AttributeValue>
        {
            ["CacheKey"] = new() { S = cacheKey },
            ["Payload"] = new() { S = payload },
            ["ExpiresAtUnix"] = new() { N = expiresAtUnix.ToString() }
        };

        if (newTags.Count > 0)
            cacheItem["Tags"] = new() { SS = newTags.ToList() };

        if (resolved.AbsoluteExpiresAtUtc is { } absolute)
        {
            cacheItem["AbsoluteExpiresAtUnix"] =
                new AttributeValue { N = absolute.ToUnixTimeSeconds().ToString() };
        }

        if (resolved.SlidingExpiration is { } sliding)
        {
            cacheItem["SlidingExpirationSeconds"] =
                new AttributeValue { N = ((long)sliding.TotalSeconds).ToString() };
        }

        var transactItems = new List<TransactWriteItem>
        {
            new()
            {
                Put = new Put
                {
                    TableName = cacheOptions.CacheTableName,
                    Item = cacheItem
                }
            }
        };

        foreach (var removedTag in oldTags.Except(newTags, StringComparer.Ordinal))
        {
            transactItems.Add(new TransactWriteItem
            {
                Delete = new Delete
                {
                    TableName = cacheOptions.TagTableName,
                    Key = new Dictionary<string, AttributeValue>
                    {
                        ["Tag"] = new() { S = removedTag },
                        ["CacheKey"] = new() { S = cacheKey }
                    }
                }
            });
        }

        foreach (var tag in newTags)
        {
            transactItems.Add(new TransactWriteItem
            {
                Put = new Put
                {
                    TableName = cacheOptions.TagTableName,
                    Item = new Dictionary<string, AttributeValue>
                    {
                        ["Tag"] = new() { S = tag },
                        ["CacheKey"] = new() { S = cacheKey },
                        ["ExpiresAtUnix"] = new() { N = expiresAtUnix.ToString() }
                    }
                }
            });
        }

        return transactItems;
    }

    private async Task ExecuteTransactWriteWithRetryAsync(List<TransactWriteItem> transactItems, CancellationToken ct)
    {
        var attempt = 0;

        while (true)
        {
            try
            {
                await dynamoDb.TransactWriteItemsAsync(new TransactWriteItemsRequest
                {
                    TransactItems = transactItems
                }, ct);

                return;
            }
            catch (TransactionCanceledException) when (attempt < cacheOptions.MaxRetries)
            {
                attempt++;
                await Task.Delay(attempt.Backoff(), ct);
            }
            catch (ProvisionedThroughputExceededException) when (attempt < cacheOptions.MaxRetries)
            {
                attempt++;
                await Task.Delay(attempt.Backoff(), ct);
            }
            catch (RequestLimitExceededException) when (attempt < cacheOptions.MaxRetries)
            {
                attempt++;
                await Task.Delay(attempt.Backoff(), ct);
            }
        }
    }
}
