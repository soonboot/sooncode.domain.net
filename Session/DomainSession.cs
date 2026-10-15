using System.Collections.Concurrent;
using Domain.Infrastructure.Annotations;
using Domain.Infrastructure.Batcher;
using Domain.Infrastructure.Model;
using Domain.Infrastructure.Monitor;

namespace Domain.Infrastructure.Session;

/// <summary>
/// 领域会话：会话内所有 add/save/delete 先收集，commit 时同一批量提交。
/// 与 Java 版 <c>session.DomainSession</c> 对齐。
/// <para>.NET 规范：file-scoped namespace + XML docs + <c>ArgumentNullException.ThrowIfNull</c>；使用 <c>AsyncLocal</c> 隔离（见 <see cref="SessionManager"/>）。</para>
/// <para>约束：<c>DomainSession</c> 与 <see cref="Batcher{T}"/> 不能在同一异步上下文混用，请二选一。</para>
/// </summary>
public class DomainSession : ISession
{
    private readonly ConcurrentQueue<ISessionFunction> _functions = new();
    private readonly List<Entity> _entities = new();
    private readonly List<BatchOperation> _batchOperations = new();
    private ISessionComplete? _successFunction;

    /// <summary>创建空会话。</summary>
    public DomainSession()
    {
    }

    /// <inheritdoc/>
    public void Add(Entity entity)
    {
        if (BatcherCapture.HasActiveScope)
            throw new DomainException($"DomainSession 与 Batcher 不能在同一线程内混用，请二选一；实体: {(entity is null ? "null" : entity.Id)}");
        if (entity is null) return;
        if (!_entities.Contains(entity))
        {
            SessionManager.Put(entity, this);
            _entities.Add(entity);
        }
    }

    /// <inheritdoc/>
    public void SetSessionFunction(ISessionFunction function)
    {
        ArgumentNullException.ThrowIfNull(function);
        _functions.Enqueue(function);
    }

    /// <inheritdoc/>
    public void SetSessionFunction(Action func)
    {
        ArgumentNullException.ThrowIfNull(func);
        _functions.Enqueue(new ActionSessionFunction(func));
    }

    /// <summary>
    /// 收集批量操作（供仓储在会话作用域内调用）。
    /// 同一实例按最终意图合并：ADD 后再 MODIFY 仍走新增；出现 DELETE 以删除为准；已 DELETE 后再变更抛错。
    /// 不同对象但同类+同 ID 视为重复（避免同一会话加载两次同一聚合根后双写）。
    /// </summary>
    public void AddOperation(BatchOperation operation)
    {
        if (operation is null) return;
        if (BatcherCapture.HasActiveScope)
            throw new DomainException($"DomainSession 与 Batcher 不能在同一线程内混用，请二选一；实体: {operation.Entity?.Id}");
        var entity = operation.Entity;
        if (entity is null) return;

        for (var i = 0; i < _batchOperations.Count; i++)
        {
            var existing = _batchOperations[i];
            if (ReferenceEquals(existing.Entity, entity))
            {
                var oldType = existing.Type;
                var newType = operation.Type;
                if (oldType == newType) return; // 同类型重复收集幂等忽略
                if (oldType == BatchOperation.OperationType.Delete)
                    throw new DomainException($"同一会话中已删除实体不能再变更:{entity.Id}");
                if (newType == BatchOperation.OperationType.Delete)
                {
                    _batchOperations[i] = operation;
                    return;
                }
                if (oldType == BatchOperation.OperationType.Add) return; // 新增后的修改仍随新增一次性写入
                _batchOperations[i] = operation; // MODIFY 后收到 ADD：以（重新）创建语义为准
                return;
            }
        }

        var key = OperationKey(entity);
        foreach (var existing in _batchOperations)
        {
            if (!ReferenceEquals(existing.Entity, entity) && key.Equals(OperationKey(existing.Entity), StringComparison.Ordinal))
                throw new DomainException($"同一会话中实体 ID 不能重复:{entity.Id}");
        }

        _batchOperations.Add(operation);
        if (!_entities.Contains(entity))
        {
            SessionManager.Put(entity, this);
            _entities.Add(entity);
        }
    }

    private static string OperationKey(Entity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return $"{entity.GetType().FullName}-{entity.Id}";
    }

    /// <summary>实例作用域（类型化）：自动 commit/rollback；亦可直接用 <see cref="SessionExtensions.Run"/>。</summary>
    public void RunWith(Action<DomainSession> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        try
        {
            action(this);
            Commit();
        }
        catch
        {
            try { Rollback(); } catch { }
            throw;
        }
    }

    /// <summary>带返回值的作用域。</summary>
    public R CallWith<R>(Func<DomainSession, R> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        try
        {
            var result = action(this);
            Commit();
            return result;
        }
        catch
        {
            try { Rollback(); } catch { }
            throw;
        }
    }

    /// <summary>静态快捷：无需手动 new，自动 commit/rollback。</summary>
    public static void Run(Action<DomainSession> action) => new DomainSession().RunWith(action);

    /// <summary>静态快捷别名（兼容）。</summary>
    public static void RunStatic(Action<DomainSession> action) => Run(action);

    /// <summary>静态带返回值快捷。</summary>
    public static R Call<R>(Func<DomainSession, R> action) => new DomainSession().CallWith(action);

    /// <summary>静态带返回值快捷别名（兼容）。</summary>
    public static R CallStatic<R>(Func<DomainSession, R> action) => Call(action);

    /// <inheritdoc/>
    public void Commit()
    {
        // 兜底：session.Add(e) 登记了、但从未经过 repository.* 收集的 pending 实体，
        // 按待持久化事件的 EventBoot(StoreFunc) 推断操作类型，避免 commit 静默丢写。
        CollectPendingEntitiesWithoutOperation();
        if (_batchOperations.Count != 0)
        {
            var toCommit = _batchOperations.ToList();
            try
            {
                var batchRepo = Monitor.Monitor.Instance?.GetBatchRepository();
                if (batchRepo is not null)
                {
                    var atomic = Config.InfraConfig.IsAtomic;
                    batchRepo.PersistBatch(toCommit, atomic);
                    foreach (var op in toCommit)
                    {
                        MarkCommitted(op.Entity);
                        TriggerMonitor(op);
                    }
                }
                else
                {
                    var domainRepo = Monitor.Monitor.Instance?.GetDomainRepository();
                    if (domainRepo is null)
                        throw new DomainException("DomainSession 提交失败：Monitor 未初始化，请先调用 Monitor.New() 并配置领域仓储/数据库连接");
                    // 无 BatchStore 回退：先解除会话绑定，否则 repository.add/save/delete 会再次命中 SessionManager 而重新延期，导致静默丢写。
                    var detached = _entities.ToList();
                    foreach (var e in detached) SessionManager.Remove(e);
                    try
                    {
                        foreach (var op in toCommit)
                        {
                            switch (op.Type)
                            {
                                case BatchOperation.OperationType.Add:
                                    InvokeRepository(op.Entity, domainRepo, BatchOperation.OperationType.Add);
                                    break;
                                case BatchOperation.OperationType.Modify:
                                    InvokeRepository(op.Entity, domainRepo, BatchOperation.OperationType.Modify);
                                    break;
                                case BatchOperation.OperationType.Delete:
                                    InvokeRepository(op.Entity, domainRepo, BatchOperation.OperationType.Delete);
                                    break;
                            }
                            MarkCommitted(op.Entity);
                            TriggerMonitor(op);
                        }
                    }
                    catch
                    {
                        // 失败恢复绑定，以便调用方重试或回滚。
                        foreach (var en in detached)
                        {
                            if (!SessionManager.Contains(en)) SessionManager.Put(en, this);
                        }
                        throw;
                    }
                }

                // 成功后才清理批量队列，失败保留以便重试。
                _batchOperations.Clear();
            }
            catch
            {
                // 失败保留 _batchOperations/_entities/_functions 以便调用方重试或回滚，不做清理。
                throw;
            }
        }

        while (_functions.TryDequeue(out var function)) function.Run();
        foreach (var entity in _entities) SessionManager.Remove(entity);
        _successFunction?.Run(_entities);
        _entities.Clear();
    }

    /// <summary>兼容别名：等价于 <see cref="Commit"/>（旧 .NET 命名）。</summary>
    [Obsolete("请使用 Commit（与 Java 版 ISession.commit 对齐）。")]
    public void Complete() => Commit();

    /// <inheritdoc/>
    public void Rollback()
    {
        _batchOperations.Clear();
        while (_functions.TryDequeue(out _)) { }
        foreach (var entity in _entities) SessionManager.Remove(entity);
        _entities.Clear();
    }

    /// <inheritdoc/>
    public List<Entity> GetEntitys() => _entities;

    /// <inheritdoc/>
    public void OnSuccess(ISessionComplete function)
    {
        _successFunction = function ?? throw new ArgumentNullException(nameof(function));
    }

    /// <summary>待提交批量操作只读视图（供诊断/测试）。</summary>
    public IReadOnlyList<BatchOperation> GetPendingOperations() => _batchOperations.AsReadOnly();

    private void CollectPendingEntitiesWithoutOperation()
    {
        if (_entities.Count == 0) return;
        foreach (var entity in _entities.ToList())
        {
            var hasOperation = _batchOperations.Any(o => ReferenceEquals(o.Entity, entity));
            if (hasOperation) continue;
            if (entity is IDomainModel dm && dm.IsStored()) continue;
            var pending = GetPendingEvents(entity);
            if (pending.Count == 0) continue;

            FuncType? last = null;
            var hasAdd = false;
            foreach (var @event in pending)
            {
                if (@event is null) continue;
                var ann = @event.GetType().GetCustomAttributes(typeof(EventBootAttribute), inherit: true).FirstOrDefault() as EventBootAttribute;
                if (ann is null) continue;
                var ft = ann.StoreFunc;
                if (ft == FuncType.replay || ft == FuncType.none) continue;
                if (ft == FuncType.add) hasAdd = true;
                last = ft;
            }

            if (last is null) continue;
            BatchOperation.OperationType type = last switch
            {
                FuncType.add => BatchOperation.OperationType.Add,
                FuncType.modify => hasAdd ? BatchOperation.OperationType.Add : BatchOperation.OperationType.Modify,
                FuncType.delete => BatchOperation.OperationType.Delete,
                _ => throw new DomainException($"无法推断会话操作类型:{entity.Id}"),
            };
            var skipES = IsSkipEventSourcing(entity);
            var startVersion = entity is IDomainModel dmv ? dmv.StartVersion : 0;
            int? expected = (type == BatchOperation.OperationType.Add || startVersion == 0) ? null : startVersion;
            _batchOperations.Add(new BatchOperation(type, entity, null, expected, skipES, true));
        }
    }

    private static bool IsStored(Entity entity)
        => entity is IDomainModel dm ? dm.IsStored() : false;

    private static IReadOnlyList<DomainEvent> GetPendingEvents(Entity entity)
        => entity is IDomainModel dm ? dm.GetPendingEvents() : new List<DomainEvent>();

    private static bool IsSkipEventSourcing(Entity entity)
    {
        var ann = entity.GetType().GetCustomAttributes(typeof(SkipEventSourcingAttribute), inherit: true).FirstOrDefault() as SkipEventSourcingAttribute;
        return ann is not null && ann.Value;
    }

    private static void MarkCommitted(Entity entity)
    {
        // 与 BatchRepository 保持一致：标记已落库 + 版本快进（经 IDomainModel 强类型调用，无反射）。
        if (entity is IDomainModel dm)
        {
            dm.MarkStored();
            dm.StartVersion = dm.GetVersion();
        }
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, System.Reflection.MethodInfo[]> RepositoryDirectMethods = new();

    private static void InvokeRepository(Entity entity, IDomainRepository repository, BatchOperation.OperationType operation)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(repository);
        // 直写内核优先：DomainRepository.AddDirect/SaveDirect/DeleteDirect（internal，同程序集可解析），
        // 避免经 Add/Save/Delete 二次进入 BatcherCapture/TryAtomicSingle。与 Java 版 commit 直写语义对齐。
        var methodName = operation switch
        {
            BatchOperation.OperationType.Add => "AddDirect",
            BatchOperation.OperationType.Modify => "SaveDirect",
            BatchOperation.OperationType.Delete => "DeleteDirect",
            _ => throw new DomainException($"未知会话操作类型:{operation}"),
        };
        var entityType = entity.GetType();
        var methods = RepositoryDirectMethods.GetOrAdd(repository.GetType(), _ => repository.GetType().GetMethods(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance));
        var candidate = methods.FirstOrDefault(m => m.Name == methodName && m.IsGenericMethod && m.GetParameters().Length == 1);
        if (candidate is not null)
        {
            try
            {
                candidate.MakeGenericMethod(entityType).Invoke(repository, new object[] { entity });
                return;
            }
            catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is not null)
            {
                throw ex.InnerException;
            }
        }

        // 自定义仓储回退：经公开 Add/Save/Delete（monitor:false，调用方已解除会话绑定，不会重新延期）。
        var fallbackName = operation switch
        {
            BatchOperation.OperationType.Add => "Add",
            BatchOperation.OperationType.Modify => "Save",
            BatchOperation.OperationType.Delete => "Delete",
            _ => throw new DomainException($"未知会话操作类型:{operation}"),
        };
        var fallback = methods.FirstOrDefault(m => m.Name == fallbackName && m.IsGenericMethod && m.GetParameters().Length == 3);
        if (fallback is null)
            throw new DomainException($"领域仓储缺少方法:{fallbackName}(entity, report, monitor)");
        try
        {
            fallback.MakeGenericMethod(entityType).Invoke(repository, new object?[] { entity, null, false });
        }
        catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw ex.InnerException;
        }
    }

    private static void TriggerMonitor(BatchOperation operation)
    {
        var monitor = Monitor.Monitor.Instance;
        if (monitor is null) return;
        var entity = operation.Entity;
        var entityType = entity.GetType();
        var notice = monitor.GetType().GetMethods()
            .FirstOrDefault(m => m.Name == "Notice" && m.IsGenericMethod && m.GetParameters().Length == 2);
        if (notice is null) return;
        try
        {
            var funcType = operation.GetFuncType();
            var generic = notice.MakeGenericMethod(entityType);
            generic.Invoke(monitor, new object[] { entity, funcType });
            // 领域事件通知（与 Batcher.Execute 成功后语义一致）。
            var getEvents = entityType.GetMethod("GetEvents");
            var noticeEvent = monitor.GetType().GetMethods()
                .FirstOrDefault(m => m.Name == "Notice" && m.IsGenericMethod && m.GetParameters().Length == 2
                    && m.GetParameters()[0].ParameterType.Name.Contains("DomainEvent"));
            if (getEvents is not null && getEvents.Invoke(entity, null) is System.Collections.IEnumerable events && noticeEvent is not null)
            {
                foreach (var @event in events)
                {
                    if (@event is null) continue;
                    try
                    {
                        noticeEvent.MakeGenericMethod(entityType).Invoke(monitor, new object[] { @event, entity });
                    }
                    catch { }
                }
            }
        }
        catch { }
    }

    private sealed class ActionSessionFunction : ISessionFunction
    {
        private readonly Action _func;
        public ActionSessionFunction(Action func) => _func = func ?? throw new ArgumentNullException(nameof(func));
        public void Run() => _func();
    }
}
