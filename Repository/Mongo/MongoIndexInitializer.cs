using MongoDB.Bson;
using MongoDB.Driver;

namespace Domain.Infrastructure.Repository.Mongo;

/// <summary>
/// Mongo 索引初始化器，与 Java 版 <c>MongoIndexInitializer</c> 对齐。
/// <para>幂等创建核心集合索引，失败仅记录不中断启动。</para>
/// </summary>
public static class MongoIndexInitializer
{
    private static readonly HashSet<string> Initialized = new(StringComparer.Ordinal);
    private static readonly object SyncRoot = new();

    /// <summary>确保核心集合索引存在（幂等）。</summary>
    public static void EnsureCoreIndexes(IMongoDBDao dao, string dbName)
    {
        EnsureCollectionIndexes(dao, dbName, "eventMetadata",
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("id"),
                new CreateIndexOptions { Unique = true }));
        EnsureCollectionIndexes(dao, dbName, "eventSource",
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("id")));
        EnsureCollectionIndexes(dao, dbName, MongoDocumentMapper.EventSnapshot,
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("streamId"),
                new CreateIndexOptions { Unique = true }));
        EnsureCollectionIndexes(dao, dbName, MongoDocumentMapper.EventTrash,
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("streamId"),
                new CreateIndexOptions { Unique = true }));
    }

    /// <summary>确保指定快照集合索引存在（幂等）。</summary>
    public static void EnsureSnapshotIndexes(IMongoDBDao dao, string dbName, string collectionName)
    {
        if (string.IsNullOrEmpty(collectionName)) return;
        EnsureCollectionIndexes(dao, dbName, collectionName,
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("streamId"),
                new CreateIndexOptions { Unique = true }));
    }

    private static void EnsureCollectionIndexes(IMongoDBDao dao, string dbName, string collectionName, params CreateIndexModel<BsonDocument>[] indexes)
    {
        var key = $"{dbName}.{collectionName}";
        lock (SyncRoot)
        {
            if (!Initialized.Add(key)) return;
        }

        try
        {
            var collection = dao.GetCollection(dbName, collectionName);
            if (collection is null) return;
            foreach (var index in indexes)
            {
                try { collection.Indexes.CreateOne(index); }
                catch { /* 索引已存在或单机限制：忽略 */ }
            }
        }
        catch
        {
            lock (SyncRoot) { Initialized.Remove(key); }
        }
    }
}
