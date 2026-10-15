using Domain.Infrastructure.Model;
using Domain.Infrastructure.Monitor;

namespace Domain.Infrastructure.Batcher;

/// <summary>批量收集器内部契约（非泛型，避免泛型静态状态碎片）。</summary>
internal interface IBatcherCapture
{
    /// <summary>实体类型约束（null 表示尚未绑定，首个实体绑定）。</summary>
    Type? EntityClass { get; }

    /// <summary>所属仓储。</summary>
    IDomainRepository Repository { get; }

    /// <summary>尝试收集，成功返回 true。</summary>
    bool TryCollect(Entity entity, BatchOperation.OperationType type);

    /// <summary>延迟领域事件通知。</summary>
    void DeferEventNotice(DomainEvent @event, Entity entity);
}

/// <summary>当前批量作用域（异步安全），供领域模型与仓储拦截调用。</summary>
public static class BatcherCapture
{
    private static readonly AsyncLocal<IBatcherCapture?> CurrentHolder = new();

    internal static IBatcherCapture? Current
    {
        get => CurrentHolder.Value;
        set => CurrentHolder.Value = value;
    }

    /// <summary>尝试将领域行为收集到当前批量作用域；收集成功返回 true（调用方应跳过单条持久化）。</summary>
    public static bool TryCapture(Entity entity, FuncType funcType, IDomainRepository? repository = null)
    {
        var current = CurrentHolder.Value;
        if (current is null) return false;
        if (Session.SessionManager.Contains(entity))
        {
            throw new DomainException($"批量 Batcher 与 DomainSession 不能在同一实体上混用: {entity.Id}，请二选一");
        }
        if (repository is not null && !ReferenceEquals(current.Repository, repository)) return false;
        var type = ToOperationType(funcType);
        if (type is null) return false;
        if (current.EntityClass is not null && !current.EntityClass.IsAssignableFrom(entity.GetType())) return false;
        return current.TryCollect(entity, type.Value);
    }

    /// <summary>供仓储显式单条 API 收集没有伴随事件的操作。</summary>
    public static bool TryCaptureOperation(Entity entity, BatchOperation.OperationType type, IDomainRepository? repository = null)
    {
        ArgumentNullException.ThrowIfNull(entity);
        var current = CurrentHolder.Value;
        if (current is null) return false;
        if (repository is not null && !ReferenceEquals(current.Repository, repository)) return false;
        if (current.EntityClass is not null && !current.EntityClass.IsAssignableFrom(entity.GetType())) return false;
        return current.TryCollect(entity, type);
    }

    /// <summary>延迟领域事件通知到批量提交后。</summary>
    public static bool TryDeferEventNotice(DomainEvent @event, Entity entity)
    {
        var current = CurrentHolder.Value;
        if (current is null) return false;
        current.DeferEventNotice(@event, entity);
        return true;
    }

    /// <summary>当前异步上下文是否存在批量作用域。</summary>
    public static bool HasActiveScope => CurrentHolder.Value is not null;

    /// <summary>当前批量收集器（供会话互斥与诊断使用）。</summary>
    public static object? CurrentScope => CurrentHolder.Value;

    internal static BatchOperation.OperationType? ToOperationType(FuncType funcType) => funcType switch
    {
        FuncType.add => BatchOperation.OperationType.Add,
        FuncType.modify => BatchOperation.OperationType.Modify,
        FuncType.delete => BatchOperation.OperationType.Delete,
        _ => null,
    };
}
