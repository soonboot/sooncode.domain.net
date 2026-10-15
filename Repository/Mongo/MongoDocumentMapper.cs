using Domain.Infrastructure.Annotations;
using Domain.Infrastructure.Model;
using Domain.Infrastructure.Utils;

namespace Domain.Infrastructure.Repository.Mongo;

/// <summary>
/// Mongo 文档映射器，与 Java 版 <c>MongoDocumentMapper</c> 对齐。
/// <para>负责快照/事件包装与字典文档之间的转换，以及快照集合名解析。</para>
/// </summary>
public static class MongoDocumentMapper
{
    public const string EventSnapshot = "eventSnapshot";
    public const string EventTrash = "eventTrash";

    /// <summary>解析快照集合名（优先 <see cref="ModelSnapshotAttribute"/>）。</summary>
    public static string GetSnapshotCollection(Type modelType)
    {
        var attr = modelType.GetCustomAttributes(typeof(ModelSnapshotAttribute), inherit: true)
            .FirstOrDefault() as ModelSnapshotAttribute;
        if (attr is not null)
        {
            if (!string.IsNullOrEmpty(attr.Value)) return attr.Value;
            if (!string.IsNullOrEmpty(attr.CollectionName)) return attr.CollectionName!;
        }
        return EventSnapshot;
    }

    /// <summary>将快照包装转换为可持久化的字典文档。</summary>
    public static IDictionary<string, object?> ToSnapshotDocument(SnapshotWrapper wrapper)
    {
        var map = EntityConvert.EntityToMap(wrapper.Snapshot);
        return new Dictionary<string, object?>
        {
            ["streamId"] = wrapper.StreamId,
            ["snapshot"] = map,
            ["snapshotDoc"] = map,
            ["entityType"] = wrapper.Snapshot.GetType().FullName,
            ["updatedAt"] = DateTime.UtcNow,
        };
    }

    /// <summary>从回收站文档中提取快照字典。</summary>
    public static IDictionary<string, object?>? ExtractSnapshot(IDictionary<string, object?> trashDoc)
    {
        if (trashDoc.TryGetValue("snapshotDoc", out var doc) && doc is IDictionary<string, object?> inner)
        {
            if (inner.TryGetValue("snapshot", out var snap) && snap is IDictionary<string, object?> snapshot)
            {
                return snapshot;
            }
            return inner;
        }
        return null;
    }
}
