using System.Diagnostics;
using Domain.Infrastructure.Batcher;
using Domain.Infrastructure.Model;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Domain.Infrastructure.Repository.Mongo;

/// <summary>
/// Mongo 批量仓储实现，与 Java 版 <c>repository.mongo.BatchRepository</c> 对齐，
/// 并对应 Java <c>Monitor.getBatchRepository()</c> 在 Mongo 连接下的装配位点
/// （.NET：<c>Monitor.ConfigBatchRepository()/GetBatchRepository()</c> 单例缓存 + <c>ConfigDBConnection</c> 按 <c>IBatchRepositoryProvider</c> 检测装配）。
/// <para>
/// 将 <see cref="BatchPlan"/>（Add/Modify/Delete 分组）映射为快照集合的 bulk 写入。
/// <c>atomic=true</c> 时尝试 Mongo 事务（要求副本集/分片集群），单机不支持时回退为逐批写入并记录警告。
/// </para>
/// </summary>
public sealed class MongoBatchRepository : IBatchRepository
{
    private readonly IMongoDBDao _dao;
    private readonly string _dbName;

    public MongoBatchRepository(IMongoDBDao dao, string dbName)
    {
        _dao = dao ?? throw new ArgumentNullException(nameof(dao));
        _dbName = string.IsNullOrEmpty(dbName) ? throw new ArgumentException("dbName 不能为空", nameof(dbName)) : dbName;
    }

    /// <inheritdoc/>
    public void PersistBatch(IReadOnlyList<BatchOperation> operations, bool atomic)
    {
        if (operations is null || operations.Count == 0) return;
        var plan = new BatchPlanner().Plan(operations);

        if (atomic && TryPersistWithTransaction(plan))
        {
            return;
        }

        if (atomic)
        {
            Trace.TraceWarning("[Batch] 事务不可用（需副本集/分片集群），已回退为非事务批量写入。");
        }

        PersistPlan(plan);
    }

    private bool TryPersistWithTransaction(BatchPlan plan)
    {
        try
        {
            var db = _dao.GetDb(_dbName);
            if (db is null) return false;
            using var session = db.Client.StartSession();
            try
            {
                session.StartTransaction();
                PersistPlan(plan, session);
                session.CommitTransaction();
                return true;
            }
            catch
            {
                try { session.AbortTransaction(); } catch { /* 忽略回滚异常 */ }
                throw;
            }
        }
        catch (Exception ex) when (IsTransactionUnsupported(ex))
        {
            return false;
        }
    }

    private static bool IsTransactionUnsupported(Exception ex)
    {
        var msg = ex.Message ?? string.Empty;
        return msg.Contains("Transaction numbers are only allowed", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("replica set", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("Transaction", StringComparison.OrdinalIgnoreCase);
    }

    private void PersistPlan(BatchPlan plan, IClientSessionHandle? session = null)
    {
        // 按快照集合分组，减少 bulk 次数；事件流仍走仓储语义，此处只处理快照（事件由上层仓储补写）。
        var groups = plan.All().GroupBy(op => op.SnapshotCollection());
        foreach (var group in groups)
        {
            var collection = _dao.GetCollection(_dbName, group.Key);
            if (collection is null) continue;
            MongoIndexInitializer.EnsureSnapshotIndexes(_dao, _dbName, group.Key);

            var writes = new List<WriteModel<BsonDocument>>();
            var deletes = new List<BatchOperation>();
            foreach (var op in group)
            {
                switch (op.Type)
                {
                    case BatchOperation.OperationType.Add:
                    case BatchOperation.OperationType.Modify:
                        {
                            var bson = ToBson(op.Snapshot());
                            var filter = Builders<BsonDocument>.Filter.Eq("streamId", op.StreamName());
                            writes.Add(new ReplaceOneModel<BsonDocument>(filter, bson) { IsUpsert = true });
                            break;
                        }
                    case BatchOperation.OperationType.Delete:
                        deletes.Add(op);
                        break;
                }
            }

            if (writes.Count > 0)
            {
                if (session is null)
                {
                    collection.BulkWrite(writes, new BulkWriteOptions { IsOrdered = false }, default);
                }
                else
                {
                    collection.BulkWrite(session, writes, new BulkWriteOptions { IsOrdered = false }, default);
                }
            }

            foreach (var op in deletes)
            {
                var filter = Builders<BsonDocument>.Filter.Eq("streamId", op.StreamName());
                if (session is null) collection.DeleteOne(filter);
                else collection.DeleteOne(session, filter);
            }
        }
    }

    private static BsonDocument ToBson(SnapshotWrapper wrapper)
    {
        var map = MongoDocumentMapper.ToSnapshotDocument(wrapper);
        var doc = new BsonDocument { ["streamId"] = wrapper.StreamId };
        foreach (var kv in map)
        {
            if (kv.Key == "streamId") continue;
            doc[kv.Key] = kv.Value is null ? BsonNull.Value : BsonValue.Create(kv.Value);
        }
        return doc;
    }
}
