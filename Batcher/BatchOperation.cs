using Domain.Infrastructure.Annotations;
using Domain.Infrastructure.Model;
using Domain.Infrastructure.Monitor;

namespace Domain.Infrastructure.Batcher;

/// <summary>批量操作项，与 Java 版 <c>BatchOperation</c> 对齐。</summary>
public sealed class BatchOperation
{
    /// <summary>批量操作类型。</summary>
    public enum OperationType
    {
        /// <summary>新增。</summary>
        Add,
        /// <summary>修改。</summary>
        Modify,
        /// <summary>删除。</summary>
        Delete,
    }

    /// <summary>操作类型。</summary>
    public OperationType Type { get; }

    /// <summary>操作实体。</summary>
    public Entity Entity { get; }

    /// <summary>变更前实体（延迟加载时可为 null）。</summary>
    public Entity? OldEntity { get; }

    /// <summary>期望版本号（乐观并发）。</summary>
    public int? ExpectedVersion { get; }

    /// <summary>是否跳过事件溯源。</summary>
    public bool SkipEventSourcing { get; }

    /// <summary>删除时是否进回收站。</summary>
    public bool Trash { get; }

    /// <summary>收集期事件快照（可为空；提交时以 <c>Entity.GetEvents()</c> 为准）。与 Java 版 <c>BatchOperation.events</c> 对齐。</summary>
    public IReadOnlyList<Model.DomainEvent> Events { get; }

    public BatchOperation(OperationType type, Entity entity, Entity? oldEntity = null, int? expectedVersion = null, bool skipEventSourcing = false, bool trash = true)
        : this(type, entity, oldEntity, null, expectedVersion, skipEventSourcing, trash)
    {
    }

    public BatchOperation(OperationType type, Entity entity, Entity? oldEntity, IReadOnlyList<Model.DomainEvent>? events, int? expectedVersion, bool skipEventSourcing, bool trash)
    {
        Type = type;
        Entity = entity ?? throw new ArgumentNullException(nameof(entity));
        OldEntity = oldEntity;
        Events = events is null ? Array.Empty<Model.DomainEvent>() : events.ToList().AsReadOnly();
        ExpectedVersion = expectedVersion;
        SkipEventSourcing = skipEventSourcing;
        Trash = trash;
    }

    /// <summary>流名称：实体全名-实体 Id。</summary>
    public string StreamName() => $"{Entity.GetType().FullName}-{Entity.Id}";

    /// <summary>快照集合名（解析 <see cref="ModelSnapshotAttribute"/>）。</summary>
    public string SnapshotCollection()
    {
        var attr = Entity.GetType().GetCustomAttributes(typeof(ModelSnapshotAttribute), inherit: true)
            .FirstOrDefault() as ModelSnapshotAttribute;
        if (attr is not null)
        {
            if (!string.IsNullOrEmpty(attr.Value)) return attr.Value;
            if (!string.IsNullOrEmpty(attr.CollectionName)) return attr.CollectionName!;
        }
        return "eventSnapshot";
    }

    /// <summary>将当前实体转换为统一快照模型。</summary>
    public SnapshotWrapper Snapshot() => new(StreamName(), Entity);

    /// <summary>映射到 <see cref="FuncType"/>。</summary>
    public FuncType GetFuncType() => Type switch
    {
        OperationType.Add => FuncType.add,
        OperationType.Modify => FuncType.modify,
        OperationType.Delete => FuncType.delete,
        _ => throw new DomainException($"未知批量操作类型:{Type}"),
    };
}
