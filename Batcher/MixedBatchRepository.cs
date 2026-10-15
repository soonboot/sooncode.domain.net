using Domain.Infrastructure.Model;
using Domain.Infrastructure.Monitor;
using Domain.Infrastructure.Validator;

namespace Domain.Infrastructure.Batcher;

/// <summary>
/// 跨聚合批量持久化入口（非泛型），与 Java 版 <c>BatchRepository&lt;?&gt;</c> 通配语义对齐。
/// <para>泛型 <see cref="BatchRepository{T}"/> 将一批次限定为单一 CLR 类型；本类接受任意聚合根混装，
/// 经 <see cref="IDomainModel"/> 强类型切面访问持久化状态（无反射、无字符串方法名）。</para>
/// </summary>
public sealed class MixedBatchRepository
{
    private readonly IDomainRepository _repository;
    private readonly IBatchStore? _batchStore;

    /// <summary>创建基于普通领域仓储的跨聚合批量仓储。</summary>
    public MixedBatchRepository(IDomainRepository repository)
        : this(repository, null)
    {
    }

    /// <summary>创建由指定批量存储器负责批量持久化的跨聚合批量仓储。</summary>
    public MixedBatchRepository(IDomainRepository repository, IBatchStore? batchStore)
    {
        _repository = repository ?? throw new DomainException("批量操作仓储未配置");
        _batchStore = batchStore;
    }

    public IBatchStore? GetBatchStore() => _batchStore;

    /// <summary>统一提交跨聚合批量操作。</summary>
    public BatchResult<Entity> PersistOperations(IReadOnlyList<BatchOperation> operations, BatchOptions? options)
    {
        var result = new BatchResult<Entity>();
        if (operations is null || operations.Count == 0) return result;
        options ??= BatchOptions.Defaults();
        options.Validate();

        if (_batchStore is not null)
        {
            return PersistWithBatchStore(operations, options);
        }

        return PersistOneByOne(operations, options);
    }

    private BatchResult<Entity> PersistWithBatchStore(IReadOnlyList<BatchOperation> operations, BatchOptions options)
    {
        var result = new BatchResult<Entity>();
        var plan = new BatchPlanner().Plan(operations);
        foreach (var pre in plan.All())
        {
            if (pre.Entity is IDomainModel preDm)
            {
                ValidateEntity(pre.Entity, pre.GetFuncType());
                preDm.PreparePendingEventSnapshots();
            }
        }
        try
        {
            _batchStore!.PersistBatch(plan.All().ToList(), options.Atomic);
            foreach (var op in plan.All())
            {
                if (op.Entity is IDomainModel dm)
                {
                    dm.MarkEventsPersisted(dm.GetPendingEvents());
                    dm.MarkStored();
                    result.IncrementSuccess();
                    if (options.Monitor) TriggerMonitor(op);
                }
                else
                {
                    result.IncrementSkipped();
                }
            }
        }
        catch (Exception ex)
        {
            if (options.FailureMode == FailureMode.FailFast) throw;
            foreach (var op in plan.All())
            {
                result.AddFailure(op.Entity, op.Type, ex);
            }
        }

        return result;
    }

    private BatchResult<Entity> PersistOneByOne(IReadOnlyList<BatchOperation> operations, BatchOptions options)
    {
        var result = new BatchResult<Entity>();
        var plan = new BatchPlanner().Plan(operations);
        foreach (var op in plan.All())
        {
            var entity = op.Entity;
            if (entity is not IDomainModel)
            {
                result.IncrementSkipped();
                continue;
            }

            try
            {
                // 直写内核优先，避免经公开 Add/Save/Delete 二次进入批量层（见泛型 BatchRepository 注释）。
                if (!TryDirectKernel(op))
                {
                    DirectFallback(op);
                }

                result.IncrementSuccess();
                if (options.Monitor) TriggerMonitor(op);
            }
            catch (Exception ex)
            {
                if (options.FailureMode == FailureMode.FailFast) throw;
                result.AddFailure(entity, op.Type, ex);
            }
        }

        return result;
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, System.Reflection.MethodInfo[]> DirectMethodCache = new();

    private bool TryDirectKernel(BatchOperation op)
    {
        if (_repository is not Model.DomainRepository) return false;
        var methodName = op.Type switch
        {
            BatchOperation.OperationType.Add => "AddDirect",
            BatchOperation.OperationType.Modify => "SaveDirect",
            BatchOperation.OperationType.Delete => "DeleteDirect",
            _ => throw new DomainException($"未知批量操作类型:{op.Type}"),
        };
        var methods = DirectMethodCache.GetOrAdd(_repository.GetType(), _ => _repository.GetType().GetMethods(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance));
        var candidate = methods.FirstOrDefault(m => m.Name == methodName && m.IsGenericMethod && m.GetParameters().Length == 1);
        if (candidate is null) return false;
        try
        {
            candidate.MakeGenericMethod(op.Entity.GetType()).Invoke(_repository, new object[] { op.Entity });
            return true;
        }
        catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw ex.InnerException;
        }
    }

    private void DirectFallback(BatchOperation op)
    {
        // 自定义仓储回退：经公开泛型 Add/Save/Delete（monitor:false），同程序集泛型分发。
        var methodName = op.Type switch
        {
            BatchOperation.OperationType.Add => "Add",
            BatchOperation.OperationType.Modify => "Save",
            BatchOperation.OperationType.Delete => "Delete",
            _ => throw new DomainException($"未知批量操作类型:{op.Type}"),
        };
        var methods = DirectMethodCache.GetOrAdd(_repository.GetType(), _ => _repository.GetType().GetMethods(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance));
        var candidate = methods.FirstOrDefault(m => m.Name == methodName && m.IsGenericMethod && m.GetParameters().Length == 3);
        if (candidate is null)
            throw new DomainException($"领域仓储缺少方法:{methodName}(entity, report, monitor)");
        try
        {
            candidate.MakeGenericMethod(op.Entity.GetType()).Invoke(_repository, new object?[] { op.Entity, null, false });
            if (op.Entity is IDomainModel dm)
            {
                dm.MarkEventsPersisted(dm.GetPendingEvents());
                dm.MarkStored();
            }
        }
        catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw ex.InnerException;
        }
    }

    private static void TriggerMonitor(BatchOperation op)
    {
        var monitor = Monitor.Monitor.Instance;
        if (monitor is null) return;
        switch (op.Type)
        {
            case BatchOperation.OperationType.Add:
                monitor.Notice(op.Entity, FuncType.add);
                break;
            case BatchOperation.OperationType.Modify:
                monitor.Notice(op.Entity, op.OldEntity, FuncType.modify);
                break;
            default:
                monitor.Notice(op.Entity, FuncType.delete);
                break;
        }
        if (op.Entity is IDomainModel dm)
        {
            foreach (var @event in dm.GetEvents())
            {
                if (@event is not null) monitor.Notice(@event, op.Entity);
            }
        }
    }

    private static void ValidateEntity(Entity entity, FuncType funcType)
    {
        if (entity is IValidate validate)
        {
            var ex = validate.Validate(funcType);
            if (ex is not null) throw ex;
        }
    }
}
