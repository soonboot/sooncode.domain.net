using System.Diagnostics;
using Domain.Infrastructure.Model;
using Domain.Infrastructure.Trash;
using Domain.Infrastructure.Utils;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Domain.Infrastructure.Repository.Mongo;

/// <summary>
/// Mongo 回收站仓储实现，与 Java 版 <c>MongoTrashRepository</c> 对齐。
/// </summary>
public sealed class MongoTrashRepository : ITrashRepository
{
    private readonly IMongoDBDao _dao;
    private readonly string _dbName;
    private const string TrashCollection = "eventTrash";

    public MongoTrashRepository(IMongoDBDao dao, string dbName)
    {
        _dao = dao ?? throw new ArgumentNullException(nameof(dao));
        _dbName = string.IsNullOrEmpty(dbName) ? throw new ArgumentException("dbName 不能为空", nameof(dbName)) : dbName;
        MongoIndexInitializer.EnsureCoreIndexes(dao, dbName);
    }

    /// <summary>从默认连接创建（框架默认 Mongo 连接）。</summary>
    public static MongoTrashRepository? FromDefault()
    {
        var single = MongoSingle.GetInstance();
        if (single.MongoDB is null || string.IsNullOrEmpty(single.DbName)) return null;
        return new MongoTrashRepository(single.MongoDB, single.DbName);
    }

    /// <inheritdoc/>
    public void Save(Type entityClass, string streamName, string entityId)
    {
        try
        {
            var collection = _dao.GetCollection(_dbName, TrashCollection);
            if (collection is null) return;
            // 读取原快照集合中的快照文档。
            var snapshotCollection = MongoDocumentMapper.GetSnapshotCollection(entityClass);
            var snapshotCol = _dao.GetCollection(_dbName, snapshotCollection);
            BsonDocument? snapshotDoc = null;
            try
            {
                snapshotDoc = snapshotCol?.Find(new BsonDocument("streamId", streamName)).FirstOrDefault();
            }
            catch { /* 快照不存在时仍记录空文档 */ }

            var doc = new BsonDocument
            {
                ["streamId"] = streamName,
                ["entityType"] = entityClass.FullName ?? entityClass.Name,
                ["entityId"] = entityId,
                ["snapshotDoc"] = snapshotDoc ?? new BsonDocument(),
                ["deletedAt"] = DateTime.UtcNow,
            };
            var filter = Builders<BsonDocument>.Filter.Eq("streamId", streamName);
            collection.ReplaceOne(filter, doc, new ReplaceOptions { IsUpsert = true });
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"[Trash] 保存回收站失败 stream={streamName}: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public IDictionary<string, object?>? FindByStreamId(string streamId)
    {
        var collection = _dao.GetCollection(_dbName, TrashCollection);
        if (collection is null) return null;
        var doc = collection.Find(new BsonDocument("streamId", streamId)).FirstOrDefault();
        return doc is null ? null : BsonToMap(doc);
    }

    /// <inheritdoc/>
    public IDictionary<string, object?>? RestoreSnapshot(string streamId, Type entityClass)
    {
        var trashDoc = FindByStreamId(streamId);
        if (trashDoc is null) return null;
        try
        {
            var snapshotCollection = MongoDocumentMapper.GetSnapshotCollection(entityClass);
            var snapshotCol = _dao.GetCollection(_dbName, snapshotCollection);
            if (snapshotCol is not null && trashDoc.TryGetValue("snapshotDoc", out var snapObj) &&
                snapObj is IDictionary<string, object?> snapMap)
            {
                var bson = ToBson(snapMap);
                var filter = Builders<BsonDocument>.Filter.Eq("streamId", streamId);
                snapshotCol.ReplaceOne(filter, bson, new ReplaceOptions { IsUpsert = true });
            }
        }
        catch (Exception ex)
        {
            throw new DomainException($"回收站恢复快照失败:{streamId} {ex.Message}", ex);
        }
        RemoveByStreamId(streamId);
        return trashDoc;
    }

    /// <inheritdoc/>
    public IReadOnlyList<IDictionary<string, object?>> ListByEntityType(string entityType, int pageIndex, int pageSize)
    {
        var collection = _dao.GetCollection(_dbName, TrashCollection);
        if (collection is null) return Array.Empty<IDictionary<string, object?>>();
        var filter = Builders<BsonDocument>.Filter.Eq("entityType", entityType);
        var docs = collection.Find(new BsonDocument("entityType", entityType)).Skip(pageIndex * pageSize).Limit(pageSize).ToList();
        return docs.Select(BsonToMap).ToList();
    }

    /// <inheritdoc/>
    public long CountByEntityType(string entityType)
    {
        var collection = _dao.GetCollection(_dbName, TrashCollection);
        if (collection is null) return 0;
        return collection.CountDocuments(Builders<BsonDocument>.Filter.Eq("entityType", entityType));
    }

    /// <inheritdoc/>
    public IReadOnlyList<IDictionary<string, object?>> ListAll(int pageIndex, int pageSize)
    {
        var collection = _dao.GetCollection(_dbName, TrashCollection);
        if (collection is null) return Array.Empty<IDictionary<string, object?>>();
        var docs = collection.Find(Builders<BsonDocument>.Filter.Empty).Skip(pageIndex * pageSize).Limit(pageSize).ToList();
        return docs.Select(BsonToMap).ToList();
    }

    /// <inheritdoc/>
    public long CountAll()
    {
        var collection = _dao.GetCollection(_dbName, TrashCollection);
        if (collection is null) return 0;
        return collection.CountDocuments(Builders<BsonDocument>.Filter.Empty);
    }

    /// <inheritdoc/>
    public void RemoveByStreamId(string streamId)
    {
        var collection = _dao.GetCollection(_dbName, TrashCollection);
        collection?.DeleteOne(Builders<BsonDocument>.Filter.Eq("streamId", streamId));
    }

    private static IDictionary<string, object?> BsonToMap(BsonDocument doc)
    {
        var map = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var el in doc.Elements)
        {
            map[el.Name] = BsonToObject(el.Value);
        }
        return map;
    }

    private static object? BsonToObject(BsonValue value) => value switch
    {
        BsonNull => null,
        BsonDocument d => (object)BsonToMap(d),
        BsonArray a => a.Select(BsonToObject).ToList(),
        BsonObjectId oid => oid.ToString(),
        _ => BsonTypeMapper.MapToDotNetValue(value),
    };

    private static BsonDocument ToBson(IDictionary<string, object?> map)
    {
        var doc = new BsonDocument();
        foreach (var kv in map)
        {
            if (kv.Value is IDictionary<string, object?> inner)
            {
                doc[kv.Key] = ToBson(inner);
            }
            else
            {
                doc[kv.Key] = kv.Value is null ? BsonNull.Value : BsonValue.Create(kv.Value);
            }
        }
        return doc;
    }
}
