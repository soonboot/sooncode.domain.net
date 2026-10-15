using Domain.Infrastructure.Model;
using Domain.Infrastructure.Monitor;
using Domain.Infrastructure.Session;

namespace Domain.Infrastructure.Batcher;

/// <summary>
/// 领域批量操作门面，与 Java 版 <c>Batcher</c> 对齐。
/// <para>
/// 同时支持显式收集与领域行为收集：
/// <code>
/// var batcher = new Batcher&lt;User&gt;(repository);
/// var result = batcher.Run(b => { user1.ChangeName("A"); user2.ChangeName("B"); });
/// </code>
/// 在 <c>Run</c> 作用域内，<see cref="DomainModel{T}"/> 产生的事件不会触发单条持久化，
/// 而是收集到当前 Batcher 中，<c>Execute</c> 时一次性提交。
/// </para>
/// <para>.NET 规范：使用 <see cref="AsyncLocal{T}"/> 实现异步安全的当前作用域（替代 Java ThreadLocal）。</para>
/// </summary>
/// <typeparam name="T">领域模型类型。</typeparam>
public sealed class Batcher<T> : IBatcher<T>, IBatcherCapture where T : DomainModel<T>
{
    private static readonly AsyncLocal<Batcher<T>?> CurrentHolder = new();

    internal static Batcher<T>? Current => CurrentHolder.Value;

    private Type? _entityClass;
    private readonly IDomainRepository _repository;

    Type? IBatcherCapture.EntityClass => _entityClass;

    IDomainRepository IBatcherCapture.Repository => _repository;

    bool IBatcherCapture.TryCollect(Entity entity, BatchOperation.OperationType type)
    {
        Register(type, entity);
        return true;
    }

    void IBatcherCapture.DeferEventNotice(DomainEvent @event, Entity entity) => DeferEventNotice(@event, entity);
    private readonly BatchRepository<T> _batchRepository;
    private readonly List<BatchOperation> _operations = new();
    private readonly Dictionary<Entity, BatchOperation.OperationType> _operationTypes = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<string> _operationKeys = new(StringComparer.Ordinal);
    private BatchOptions _options = BatchOptions.Defaults();
    private int _skippedCount;

    /// <summary>创建批量操作门面（从 Monitor 获取默认仓储）。</summary>
    public Batcher(Type? entityClass) : this(entityClass, RepositoryFromMonitor())
    {
    }

    /// <summary>创建不预设实体类型的批量作用域。</summary>
    public Batcher(IDomainRepository repository) : this(null, repository)
    {
    }

    /// <summary>创建批量操作门面。</summary>
    public Batcher(Type? entityClass, IDomainRepository repository)
    {
        _entityClass = entityClass;
        _repository = repository ?? throw new DomainException("批量操作仓库未配置");
        _batchRepository = new BatchRepository<T>(repository);
    }

    private static IDomainRepository RepositoryFromMonitor()
    {
        var repo = Monitor.Monitor.Instance?.GetDomainRepository();
        return repo ?? throw new DomainException("批量操作仓库未配置");
    }

    /// <inheritdoc/>
    public IBatcher<T> Options(BatchOptions options)
    {
        _options = options ?? BatchOptions.Defaults();
        return this;
    }

    /// <inheritdoc/>
    public BatchOptions GetOptions() => _options;

    /// <inheritdoc/>
    public IBatcher<T> Add(T entity)
    {
        Register(BatchOperation.OperationType.Add, entity);
        return this;
    }

    /// <inheritdoc/>
    public IBatcher<T> AddAll(IEnumerable<T> entities)
    {
        if (entities is not null) foreach (var e in entities) Add(e);
        return this;
    }

    /// <inheritdoc/>
    public IBatcher<T> Save(T entity)
    {
        Register(BatchOperation.OperationType.Modify, entity);
        return this;
    }

    /// <inheritdoc/>
    public IBatcher<T> SaveAll(IEnumerable<T> entities)
    {
        if (entities is not null) foreach (var e in entities) Save(e);
        return this;
    }

    /// <inheritdoc/>
    public IBatcher<T> Delete(T entity)
    {
        Register(BatchOperation.OperationType.Delete, entity);
        return this;
    }

    /// <inheritdoc/>
    public IBatcher<T> DeleteAll(IEnumerable<T> entities)
    {
        if (entities is not null) foreach (var e in entities) Delete(e);
        return this;
    }

    /// <inheritdoc/>
    public IBatcher<T> DeleteByIds(IEnumerable<string> ids)
    {
        if (ids is null) return this;
        if (_entityClass is null) throw new DomainException("按 ID 批量删除必须提供实体类型");
        foreach (var id in ids)
        {
            if (string.IsNullOrEmpty(id)) throw new DomainException("批量删除实体 ID 不能为空");
            var entity = _repository.FindById<T>(id);
            if (entity is null)
            {
                _skippedCount++;
                continue;
            }
            Register(BatchOperation.OperationType.Delete, entity);
        }
        return this;
    }

    /// <inheritdoc/>
    public BatchResult<T> Run(Action<IBatcher<T>> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Activate(() => action(this));
        return Execute();
    }

    /// <inheritdoc/>
    /// <remarks>
    /// 与 Java 版 <c>Batcher.execute</c> 对齐：
    /// <list type="bullet">
    /// <item>先按 <c>IDomainModel.IsStored()</c> 过滤已持久化实体（计为跳过，与 MixedBatcher/DomainSession 一致）；</item>
    /// <item><c>Continue</c> 模式逐项提交，单项失败记 <c>failure</c> 不影响后续；<c>atomic=true</c> 时语义为“每个操作原子”；</item>
    /// <item><c>FailFast</c> 模式整体提交，失败时保留队列以便修正后重试；</item>
    /// <item>领域事件通知必须在批量写入成功、实体标记为 stored 之后发送。</item>
    /// </list>
    /// </remarks>
    public BatchResult<T> Execute()
    {
        _options.Validate();
        if (_operations.Count == 0)
        {
            var empty = new BatchResult<T>();
            for (var i = 0; i < _skippedCount; i++) empty.IncrementSkipped();
            _skippedCount = 0;
            _deferredNotices.Clear();
            _deferredNoticeKeys.Clear();
            return empty;
        }

        var pending = new List<BatchOperation>();
        var skipped = new BatchResult<T>();
        foreach (var operation in _operations)
        {
            if (operation.Entity is IDomainModel dm && dm.IsStored()) skipped.IncrementSkipped();
            else pending.Add(operation);
        }

        if (pending.Count == 0)
        {
            for (var i = 0; i < _skippedCount; i++) skipped.IncrementSkipped();
            _skippedCount = 0;
            _operations.Clear();
            _operationTypes.Clear();
            _operationKeys.Clear();
            _deferredNotices.Clear();
            _deferredNoticeKeys.Clear();
            return skipped;
        }

        try
        {
            BatchResult<T> result;
            if (_options.FailureMode == FailureMode.Continue)
            {
                var succeeded = new List<BatchOperation>();
                result = ExecuteContinue(pending, succeeded);
                for (var i = 0; i < _skippedCount; i++) result.IncrementSkipped();
                for (var i = 0; i < skipped.SkippedCount; i++) result.IncrementSkipped();
                _skippedCount = 0;
                _operations.Clear();
                _operationTypes.Clear();
                _operationKeys.Clear();
                FlushEventNotices();
                NotifyEntityOperations(succeeded);
                return result;
            }

            CollectEntityEvents(pending);
            result = _batchRepository.PersistOperations(pending, _options);
            for (var i = 0; i < _skippedCount; i++) result.IncrementSkipped();
            for (var i = 0; i < skipped.SkippedCount; i++) result.IncrementSkipped();
            _skippedCount = 0;
            _operations.Clear();
            _operationTypes.Clear();
            _operationKeys.Clear();
            FlushEventNotices();
            NotifyEntityOperations(pending);
            return result;
        }
        catch
        {
            // 写入失败时保留队列，便于调用方修正后重试。
            throw;
        }
    }

    /// <summary>
    /// CONTINUE 模式按操作逐项提交，每项拥有独立持久化边界，单项失败不影响后续项。
    /// </summary>
    private BatchResult<T> ExecuteContinue(List<BatchOperation> pending, List<BatchOperation> succeededOperations)
    {
        ArgumentNullException.ThrowIfNull(pending);
        ArgumentNullException.ThrowIfNull(succeededOperations);
        var result = new BatchResult<T>();
        foreach (var operation in pending)
        {
            try
            {
                CollectEntityEvents(new List<BatchOperation> { operation });
                var one = _batchRepository.PersistOperations(new List<BatchOperation> { operation }, _options);
                result.Merge(one);
                if (one.SuccessCount > 0) succeededOperations.Add(operation);
                else DiscardEventNotices(operation.Entity);
            }
            catch (Exception ex)
            {
                DiscardEventNotices(operation.Entity);
                if (operation.Entity is T entity) result.AddFailure(entity, operation.Type, ex);
                else result.IncrementSkipped();
            }
        }

        return result;
    }

    private void CollectEntityEvents(IReadOnlyList<BatchOperation> pending)
    {
        ArgumentNullException.ThrowIfNull(pending);
        foreach (var operation in pending)
        {
            if (operation.Entity is DomainModel<T> dm)
            {
                foreach (var @event in dm.GetEvents())
                {
                    if (@event is DomainEvent de) DeferEventNotice(de, dm);
                }
            }
        }
    }

    private void NotifyEntityOperations(IReadOnlyList<BatchOperation> pending)
    {
        ArgumentNullException.ThrowIfNull(pending);
        if (!_options.Monitor || Monitor.Monitor.Instance is null) return;
        foreach (var operation in pending)
        {
            if (operation.Entity is not DomainModel<T> dm) continue;
            switch (operation.Type)
            {
                case BatchOperation.OperationType.Add:
                    Monitor.Monitor.Instance.Notice(dm, FuncType.add);
                    break;
                case BatchOperation.OperationType.Modify:
                    if (operation.OldEntity is DomainModel<T> old)
                        Monitor.Monitor.Instance.Notice(dm, old, FuncType.modify);
                    else
                        Monitor.Monitor.Instance.Notice(dm, FuncType.modify);
                    break;
                default:
                    Monitor.Monitor.Instance.Notice(dm, FuncType.delete);
                    break;
            }
        }
    }

    private void DiscardEventNotices(Entity entity)
    {
        _deferredNotices.RemoveAll(n => ReferenceEquals(n.Entity, entity));
        var dead = _deferredNoticeKeys.Where(kv => ReferenceEquals(kv.Value, entity)).Select(kv => kv.Key).ToList();
        foreach (var k in dead) _deferredNoticeKeys.Remove(k);
    }

    internal void Register(BatchOperation.OperationType type, Entity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        var entityType = entity.GetType();
        if (_entityClass is null)
        {
            _entityClass = entityType;
        }
        else if (!_entityClass.IsAssignableFrom(entityType))
        {
            throw new DomainException($"批量操作实体类型不匹配:{entityType.FullName}");
        }

        if (_operationTypes.TryGetValue(entity, out var oldType))
        {
            if (oldType != type) throw new DomainException($"同一批量中同一实体不能混用操作类型:{entity.Id}");
            return;
        }

        var key = $"{entityType.FullName}-{entity.Id}";
        if (!_operationKeys.Add(key))
        {
            throw new DomainException($"同一批量中实体 ID 不能重复:{entity.Id}");
        }

        _operationTypes[entity] = type;
        int? expectedVersion = type == BatchOperation.OperationType.Add ? null : GetStartVersion(entity);
        _operations.Add(new BatchOperation(type, entity, null, expectedVersion, skipEventSourcing: false, trash: true));
    }

    private static int? GetStartVersion(Entity entity)
        => entity is IDomainModel dm && dm.StartVersion != 0 ? dm.StartVersion : null;

    private void Activate(Action action)
    {
        var previous = CurrentHolder.Value;
        var previousCapture = BatcherCapture.Current;
        if (previous is not null && !ReferenceEquals(previous, this))
        {
            throw new DomainException("不支持嵌套批量作用域");
        }
        if (previousCapture is not null && !ReferenceEquals(previousCapture, (IBatcherCapture)this))
        {
            throw new DomainException("不支持嵌套批量作用域");
        }
        CurrentHolder.Value = this;
        BatcherCapture.Current = this;
        try
        {
            action();
        }
        finally
        {
            CurrentHolder.Value = previous;
            BatcherCapture.Current = previousCapture;
        }
    }

    private readonly List<(DomainEvent Event, Entity Entity)> _deferredNotices = new();
    private readonly Dictionary<DomainEvent, Entity> _deferredNoticeKeys = new(ReferenceEqualityComparer.Instance);

    internal void DeferEventNotice(DomainEvent @event, Entity entity)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(entity);
        if (_deferredNoticeKeys.ContainsKey(@event)) return;
        _deferredNoticeKeys[@event] = entity;
        _deferredNotices.Add((@event, entity));
    }

    private void FlushEventNotices()
    {
        if (!_options.Monitor || Monitor.Monitor.Instance is null)
        {
            _deferredNotices.Clear();
            _deferredNoticeKeys.Clear();
            return;
        }

        var pending = _deferredNotices.ToList();
        _deferredNotices.Clear();
        _deferredNoticeKeys.Clear();
        foreach (var (e, en) in pending)
        {
            if (en is T typed) Monitor.Monitor.Instance.Notice(e, typed);
        }
    }

    /// <summary>当前批量作用域（供会话互斥与诊断使用）。</summary>
    public static Batcher<T>? CurrentScope => CurrentHolder.Value;
}

/// <summary>批量作用域静态拦截入口（供 <see cref="DomainModel{T}"/> 与仓储调用）。</summary>
public static class Batcher
{
    /// <summary>尝试将领域行为收集到当前批量作用域；收集成功返回 <c>true</c>（调用方应跳过单条持久化）。</summary>
    public static bool Capture(Entity entity, FuncType funcType, IDomainRepository? repository)
        => BatcherCapture.TryCapture(entity, funcType, repository);

    /// <summary>供仓储显式单条 API 收集没有伴随事件的操作。</summary>
    public static bool CaptureOperation(Entity entity, BatchOperation.OperationType type, IDomainRepository? repository)
        => BatcherCapture.TryCaptureOperation(entity, type, repository);

    /// <summary>当前线程/异步上下文是否存在批量作用域。</summary>
    public static bool HasActiveScope => BatcherCapture.HasActiveScope;
}
