using Domain.Infrastructure.Model;
using Domain.Infrastructure.Monitor;
using Domain.Infrastructure.Session;

namespace Domain.Infrastructure.Batcher;

/// <summary>
/// 跨聚合批量作用域（非泛型），与 Java 版 <c>Batcher&lt;?&gt;</c> 对齐。
/// <para>
/// 用法与泛型 <c>Batcher&lt;T&gt;</c> 一致，但一批次可混装多种聚合根：
/// <code>
/// var batcher = new MixedBatcher(repository);
/// var result = batcher.Run(b => { b.Add(user); b.Save(order); });
/// </code>
/// 在 <c>Run</c> 作用域内领域行为产生的事件不会触发单条持久化，而是收集到当前作用域，<c>Execute</c> 时一次性提交。
/// </para>
/// <para>.NET 规范：使用 <see cref="System.Threading.AsyncLocal{T}"/> 实现异步安全的当前作用域（替代 Java ThreadLocal）。</para>
/// </summary>
public sealed class MixedBatcher : IMixedBatcher, IBatcherCapture
{
    private Type? _entityClass;
    private readonly IDomainRepository _repository;
    private readonly MixedBatchRepository _batchRepository;
    private readonly List<BatchOperation> _operations = new();
    private readonly Dictionary<Entity, BatchOperation.OperationType> _operationTypes = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<string> _operationKeys = new(StringComparer.Ordinal);
    private BatchOptions _options = BatchOptions.Defaults();
    private int _skippedCount;
    private readonly List<(DomainEvent Event, Entity Entity)> _deferredNotices = new();
    private readonly Dictionary<DomainEvent, Entity> _deferredNoticeKeys = new(ReferenceEqualityComparer.Instance);

    Type? IBatcherCapture.EntityClass => null;

    IDomainRepository IBatcherCapture.Repository => _repository;

    bool IBatcherCapture.TryCollect(Entity entity, BatchOperation.OperationType type)
    {
        Register(type, entity);
        return true;
    }

    void IBatcherCapture.DeferEventNotice(DomainEvent @event, Entity entity) => DeferEventNotice(@event, entity);

    /// <summary>创建跨聚合批量作用域（不限实体类型）。</summary>
    public MixedBatcher(IDomainRepository repository)
    {
        _repository = repository ?? throw new DomainException("批量操作仓库未配置");
        _batchRepository = new MixedBatchRepository(repository);
    }

    /// <summary>创建带实体类型约束的跨聚合作用域（与 Java 版 <c>Batcher(entityClass, repository)</c> 对齐）。</summary>
    public MixedBatcher(Type? entityClass, IDomainRepository repository)
        : this(repository)
    {
        _entityClass = entityClass;
    }

    /// <inheritdoc/>
    public IMixedBatcher Options(BatchOptions options)
    {
        _options = options ?? BatchOptions.Defaults();
        return this;
    }

    /// <inheritdoc/>
    public BatchOptions GetOptions() => _options;

    /// <inheritdoc/>
    public IMixedBatcher Add(Entity entity)
    {
        Register(BatchOperation.OperationType.Add, entity);
        return this;
    }

    /// <inheritdoc/>
    public IMixedBatcher AddAll(IEnumerable<Entity> entities)
    {
        if (entities is not null) foreach (var e in entities) Add(e);
        return this;
    }

    /// <inheritdoc/>
    public IMixedBatcher Save(Entity entity)
    {
        Register(BatchOperation.OperationType.Modify, entity);
        return this;
    }

    /// <inheritdoc/>
    public IMixedBatcher SaveAll(IEnumerable<Entity> entities)
    {
        if (entities is not null) foreach (var e in entities) Save(e);
        return this;
    }

    /// <inheritdoc/>
    public IMixedBatcher Delete(Entity entity)
    {
        Register(BatchOperation.OperationType.Delete, entity);
        return this;
    }

    /// <inheritdoc/>
    public IMixedBatcher DeleteAll(IEnumerable<Entity> entities)
    {
        if (entities is not null) foreach (var e in entities) Delete(e);
        return this;
    }

    /// <inheritdoc/>
    public BatchResult<Entity> Run(Action<IMixedBatcher> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Activate(() => action(this));
        return Execute();
    }

    /// <inheritdoc/>
    /// <remarks>与泛型 <c>Batcher&lt;T&gt;.Execute</c> 同语义：已持久化实体计为跳过；通知在写入成功后发送。</remarks>
    public BatchResult<Entity> Execute()
    {
        _options.Validate();
        if (_operations.Count == 0)
        {
            var empty = new BatchResult<Entity>();
            for (var i = 0; i < _skippedCount; i++) empty.IncrementSkipped();
            _skippedCount = 0;
            _deferredNotices.Clear();
            _deferredNoticeKeys.Clear();
            return empty;
        }

        var pending = new List<BatchOperation>();
        var skipped = new BatchResult<Entity>();
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
            BatchResult<Entity> result;
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

    private BatchResult<Entity> ExecuteContinue(List<BatchOperation> pending, List<BatchOperation> succeededOperations)
    {
        ArgumentNullException.ThrowIfNull(pending);
        ArgumentNullException.ThrowIfNull(succeededOperations);
        var result = new BatchResult<Entity>();
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
                result.AddFailure(operation.Entity, operation.Type, ex);
            }
        }

        return result;
    }

    private void CollectEntityEvents(IReadOnlyList<BatchOperation> pending)
    {
        ArgumentNullException.ThrowIfNull(pending);
        foreach (var operation in pending)
        {
            if (operation.Entity is IDomainModel dm)
            {
                foreach (var @event in dm.GetEvents())
                {
                    if (@event is not null) DeferEventNotice(@event, operation.Entity);
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
            switch (operation.Type)
            {
                case BatchOperation.OperationType.Add:
                    Monitor.Monitor.Instance.Notice(operation.Entity, FuncType.add);
                    break;
                case BatchOperation.OperationType.Modify:
                    Monitor.Monitor.Instance.Notice(operation.Entity, operation.OldEntity, FuncType.modify);
                    break;
                default:
                    Monitor.Monitor.Instance.Notice(operation.Entity, FuncType.delete);
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
        if (entity is not IDomainModel)
            throw new DomainException($"批量操作实体必须是领域聚合根:{entity.GetType().FullName}");
        var entityType = entity.GetType();
        if (_entityClass is not null && !_entityClass.IsAssignableFrom(entityType))
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
        _operations.Add(new BatchOperation(type, entity, null, ExpectedVersionOf(type, entity), skipEventSourcing: false, trash: true));
    }

    private static int? ExpectedVersionOf(BatchOperation.OperationType type, Entity entity)
    {
        if (type == BatchOperation.OperationType.Add) return null;
        return entity is IDomainModel dm && dm.StartVersion != 0 ? dm.StartVersion : null;
    }

    private void Activate(Action action)
    {
        var previousCapture = BatcherCapture.Current;
        if (previousCapture is not null && !ReferenceEquals(previousCapture, (IBatcherCapture)this))
        {
            throw new DomainException("不支持嵌套批量作用域");
        }
        BatcherCapture.Current = this;
        try
        {
            action();
        }
        finally
        {
            BatcherCapture.Current = previousCapture;
        }
    }

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
            Monitor.Monitor.Instance.Notice(e, en);
        }
    }
}
