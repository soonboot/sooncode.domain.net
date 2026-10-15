using Domain.Infrastructure.Model;
using Domain.Infrastructure.Monitor;
using Domain.Infrastructure.Session;
using Domain.Infrastructure.Trash;

namespace Domain.Infrastructure.Batcher;

/// <summary>
/// 批量操作的统一持久化入口，与 Java 版 <c>BatchRepository</c> 对齐。
/// <para>Batcher 负责收集和编排操作，本类负责校验、准备以及提交操作。</para>
/// </summary>
/// <typeparam name="T">领域模型类型。</typeparam>
public sealed class BatchRepository<T> where T : DomainModel<T>
{
    private readonly IDomainRepository _repository;
    private readonly IBatchStore? _batchStore;
    private Trash.Trash? _trash;

    /// <summary>创建基于普通领域仓储的批量仓储。</summary>
    public BatchRepository(IDomainRepository repository) : this(repository, null, null)
    {
    }

    /// <summary>创建由指定批量存储器负责批量持久化的批量仓储。</summary>
    public BatchRepository(IDomainRepository repository, IBatchStore? batchStore, Trash.Trash? trash)
    {
        _repository = repository ?? throw new DomainException("批量操作仓储未配置");
        _batchStore = batchStore;
        _trash = trash;
    }

    public void SetTrashRepository(Trash.Trash trash) => _trash = trash;

    public IBatchStore? GetBatchStore() => _batchStore;

    /// <summary>统一提交批量操作。</summary>
    public BatchResult<T> PersistOperations(IReadOnlyList<BatchOperation> operations, BatchOptions? options)
    {
        var result = new BatchResult<T>();
        if (operations is null || operations.Count == 0) return result;
        options ??= BatchOptions.Defaults();
        options.Validate();

        if (_batchStore is not null)
        {
            return PersistWithBatchStore(operations, options);
        }

        return PersistOneByOne(operations, options);
    }

    private BatchResult<T> PersistWithBatchStore(IReadOnlyList<BatchOperation> operations, BatchOptions options)
    {
        var result = new BatchResult<T>();
        var plan = new BatchPlanner().Plan(operations);
        // 与 Java 版 persistWithBatchStore 对齐：先做批量预处理（校验 + 待持久化事件审计快照），一次提交。
        foreach (var pre in plan.All())
        {
            if (pre.Entity is T preEntity)
            {
                ValidateEntity(preEntity, pre.GetFuncType());
                preEntity.PreparePendingEventSnapshots();
            }
        }
        try
        {
            _batchStore!.PersistBatch(plan.All().ToList(), options.Atomic);
            foreach (var op in plan.All())
            {
                if (op.Entity is T entity)
                {
                    MarkStored(entity);
                    result.IncrementSuccess();
                    if (options.Monitor)
                    {
                        TriggerMonitor(entity, op);
                    }
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
                if (op.Entity is T entity)
                {
                    result.AddFailure(entity, op.Type, ex);
                }
            }
        }

        return result;
    }

    private BatchResult<T> PersistOneByOne(IReadOnlyList<BatchOperation> operations, BatchOptions options)
    {
        var result = new BatchResult<T>();
        var plan = new BatchPlanner().Plan(operations);
        foreach (var op in plan.All())
        {
            if (op.Entity is not T entity)
            {
                result.IncrementSkipped();
                continue;
            }

            try
            {
                // OneByOne 回退必须走直写内核：DomainRepository.Add/Save/Delete 会再次经
                // BatcherCapture/TryAtomicSingle 进入批量层（Monitor 单例），造成二次提交与重复 MarkStored。
                // 与 Java 版 persistOneByOne 直接 repository.add/save/delete 直写语义对齐。
                if (_repository is Model.DomainRepository direct)
                {
                    switch (op.Type)
                    {
                        case BatchOperation.OperationType.Add:
                            direct.AddDirect(entity);
                            break;
                        case BatchOperation.OperationType.Modify:
                            direct.SaveDirect(entity);
                            break;
                        case BatchOperation.OperationType.Delete:
                            direct.DeleteDirect(entity);
                            break;
                        default:
                            throw new DomainException($"未知批量操作类型:{op.Type}");
                    }
                }
                else
                {
                    switch (op.Type)
                    {
                        case BatchOperation.OperationType.Add:
                            _repository.Add(entity, null!, monitor: false);
                            break;
                        case BatchOperation.OperationType.Modify:
                            _repository.Save(entity, null!, monitor: false);
                            break;
                        case BatchOperation.OperationType.Delete:
                            _repository.Delete(entity, null!, monitor: false);
                            break;
                        default:
                            throw new DomainException($"未知批量操作类型:{op.Type}");
                    }

                    MarkStored(entity);
                }
                result.IncrementSuccess();
                if (options.Monitor)
                {
                    TriggerMonitor(entity, op);
                }
            }
            catch (Exception ex)
            {
                if (options.FailureMode == FailureMode.FailFast) throw;
                result.AddFailure(entity, op.Type, ex is Exception e ? e : new DomainException(ex.Message));
            }
        }

        return result;
    }

    /// <summary>仅在真实写入成功后推进持久化边界（事件前缀 + stored 标志）。与 Java 版 markEventsPersisted/markStored 对齐；直写内核路径已自行标记，此处仅用于自定义仓储回退。</summary>
    private static void MarkStored(T entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        // 批量提交成功意味着全部待持久化事件已落库，直接快进到末尾（失败时不调用以便重试）。
        entity.MarkEventsPersisted(entity.GetPendingEvents());
        entity.MarkStored();
    }

    private static void TriggerMonitor(T entity, BatchOperation op)
    {
        var monitor = Monitor.Monitor.Instance;
        if (monitor is null) return;
        var funcType = op.GetFuncType();
        monitor.Notice(entity, funcType);
        foreach (var @event in entity.GetEvents())
        {
            monitor.Notice(@event, entity);
        }
    }

    private static void ValidateEntity(T entity, Monitor.FuncType funcType)
    {
        if (entity is Validator.IValidate validate)
        {
            var ex = validate.Validate(funcType);
            if (ex is not null) throw ex;
        }
    }

    /// <summary>供领域仓储在批量作用域内拦截单条 CRUD 调用。</summary>
    public static bool Capture(Entity entity, FuncType funcType, IDomainRepository repository)
        => Batcher.Capture(entity, funcType, repository);
}
