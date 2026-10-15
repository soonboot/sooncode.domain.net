using System.Diagnostics;
using Domain.Infrastructure.Config;
using Domain.Infrastructure.Lookup;
using Domain.Infrastructure.Model;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Domain.Infrastructure.Repository.Mongo;

/// <summary>
/// Lookup 专用的 Mongo 批量写入实现，不走 <see cref="Batcher.Batcher{T}"/>。
/// <para>语义：BEST_EFFORT 的 <c>BulkWrite(ReplaceOne upsert, ordered:false)</c>，直接操作快照集合。</para>
/// 与 Java 版 <c>MongoLookupBulkWriter</c> 对齐。
/// </summary>
public sealed class MongoLookupBulkWriter : ILookupBulkWriter
{
    private const int DefaultBatchSize = 500;
    private readonly IMongoDBDao _dao;
    private readonly string _dbName;
    private int _batchSize;

    public MongoLookupBulkWriter(IMongoDBDao dao, string dbName, int batchSize = DefaultBatchSize)
    {
        _dao = dao ?? throw new ArgumentException("MongoLookupBulkWriter: dao 不能为空", nameof(dao));
        _dbName = string.IsNullOrEmpty(dbName) ? throw new ArgumentException("MongoLookupBulkWriter: dbName 不能为空", nameof(dbName)) : dbName;
        _batchSize = batchSize > 0 ? batchSize : DefaultBatchSize;
    }

    /// <summary>动态刷新：重读 <see cref="InfraConfig.LookupBulkBatchSize"/>。</summary>
    public void Refresh()
    {
        var n = InfraConfig.LookupBulkBatchSize;
        if (n > 0) _batchSize = n;
    }

    /// <inheritdoc/>
    public int BulkSaveSnapshots(Type modelType, IReadOnlyList<Entity> entities)
    {
        if (entities is null || entities.Count == 0) return 0;
        ArgumentNullException.ThrowIfNull(modelType);
        var collectionName = MongoDocumentMapper.GetSnapshotCollection(modelType);
        MongoIndexInitializer.EnsureSnapshotIndexes(_dao, _dbName, collectionName);
        var collection = _dao.GetCollection(_dbName, collectionName);
        if (collection is null)
        {
            Trace.TraceError($"[Lookup][Bulk] 获取集合失败 db={_dbName} collection={collectionName}");
            return 0;
        }

        var effectiveBatch = InfraConfig.LookupBulkBatchSize > 0 ? InfraConfig.LookupBulkBatchSize : _batchSize;
        if (effectiveBatch <= 0) effectiveBatch = DefaultBatchSize;

        var success = 0;
        foreach (var chunk in entities.Chunk(effectiveBatch))
        {
            var writes = new List<WriteModel<BsonDocument>>(chunk.Length);
            foreach (var entity in chunk)
            {
                var wrapper = new SnapshotWrapper($"{modelType.FullName}-{entity.Id}", entity);
                var docMap = MongoDocumentMapper.ToSnapshotDocument(wrapper);
                var bson = ToBson(docMap);
                var filter = Builders<BsonDocument>.Filter.Eq("streamId", wrapper.StreamId);
                writes.Add(new ReplaceOneModel<BsonDocument>(filter, bson) { IsUpsert = true });
            }

            try
            {
                var result = collection.BulkWrite(writes, new BulkWriteOptions { IsOrdered = false }, default);
                success += (int)(result.ModifiedCount + result.Upserts.Count + result.InsertedCount);
            }
            catch (MongoBulkWriteException<BsonDocument> ex)
            {
                var failed = ex.WriteErrors?.Count ?? 0;
                success += Math.Max(0, writes.Count - failed);
                Trace.TraceWarning($"[Lookup][Bulk] 部分写入失败 collection={collectionName}: {ex.Message}");
            }
            catch (Exception ex)
            {
                Trace.TraceError($"[Lookup][Bulk] 批量写入失败 collection={collectionName}: {ex.Message}");
                // 降级逐条，保证最终一致性。
                foreach (var entity in chunk)
                {
                    try
                    {
                        var wrapper = new SnapshotWrapper($"{modelType.FullName}-{entity.Id}", entity);
                        var bson = ToBson(MongoDocumentMapper.ToSnapshotDocument(wrapper));
                        var filter = Builders<BsonDocument>.Filter.Eq("streamId", wrapper.StreamId);
                        collection.ReplaceOne(filter, bson, new ReplaceOptions { IsUpsert = true });
                        success++;
                    }
                    catch (Exception inner)
                    {
                        Trace.TraceError($"[Lookup] 降级逐条保存失败 id={entity.Id}: {inner.Message}");
                    }
                }
            }
        }

        return success;
    }

    private static BsonDocument ToBson(IDictionary<string, object?> map)
    {
        var doc = new BsonDocument();
        foreach (var kv in map)
        {
            doc[kv.Key] = kv.Value is null ? BsonNull.Value : BsonValue.Create(kv.Value);
        }
        return doc;
    }
}
