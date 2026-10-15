using Domain.Infrastructure.Annotations;
using Domain.Infrastructure.Model;

namespace Domain.Infrastructure.Monitor;

public class Monitor
{
    private static Monitor? _instance;

    public static Monitor Instance => _instance ??= new Monitor();

    public static Monitor New() => Instance;

    private ICreaterGetter? _createrGetter;
    private IDomainRepository? _domainRepository;
    private IEventStore? _eventStore;
    private StoreNotice? _storeNotice;
    private ReportRegister? _reportRegister;
    private Batcher.IBatchRepository? _cachedBatchRepository;
    private readonly object _batchRepoLock = new();
    private readonly EntityNotice _entityNotice = new EntityNotice();
    private readonly EventNotice _eventNotice = new EventNotice();

    private Monitor() {}

    public ICreaterGetter? GetCreaterGetter()
    {
        return _createrGetter;
    }

    public void Store<T>(DomainModel<T> entity, EventBootAttribute? annotation) where T : DomainModel<T>
    {
        // 与 Java 版 Store(domainRepository != null && storeNotice != null) 对齐：
        // 不以 _eventStore 为门控，否则 ConfigDomainRepository（自定义/测试仓储）路径下领域行为事件会被静默丢弃
        //（既不进 Batcher 收集、也不进 Session 延期、更不单条落库）。事件流持久化由仓储内部负责。
        if (_domainRepository != null && _storeNotice != null)
        {
            _storeNotice.Notice<T>(entity, annotation);
        }
    }

    public void Notice<T>(DomainModel<T> entity, FuncType funcType) where T : DomainModel<T>
    {
        _entityNotice.Notice<T>(entity, funcType);
    }

    public void Notice<T>(DomainModel<T> entity, DomainModel<T> oldEntity, FuncType funcType) where T : DomainModel<T>
    {
        _entityNotice.Notice<T>(entity, oldEntity, funcType);
    }

    public void Notice<T>(DomainEvent @event, DomainModel<T> entity) where T : DomainModel<T>
    {
        _eventNotice.Notice<T>(@event, entity);
    }

    /// <summary>通知实体变更（非泛型：跨聚合批量/会话兜底路径，委托同名非泛型核心）。</summary>
    public void Notice(Entity entity, FuncType funcType)
    {
        _entityNotice.Notice(entity, funcType);
    }

    /// <summary>通知实体变更（非泛型，带旧实体）。</summary>
    public void Notice(Entity entity, Entity? oldEntity, FuncType funcType)
    {
        _entityNotice.Notice(entity, oldEntity, funcType);
    }

    /// <summary>通知领域事件（非泛型：跨聚合批量事件刷新路径）。</summary>
    public void Notice(DomainEvent @event, Entity entity)
    {
        _eventNotice.Notice(@event, entity);
    }

    public EventNotice.EventNoticeTrigger ListenEvent(Type cla)
    {
        return _eventNotice.Listen(cla);
    }

    public EntityNotice.EntityNoticeTrigger ListenEntity(Type cla)
    {
        return _entityNotice.Listen(cla);
    }

    public void ConfigCreater(ICreaterGetter listen)
    {
        _createrGetter = listen;
    }

    public void ConfigDBConnection(IDBConnection dbConnection)
        => ConfigDBConnection(dbConnection, (Batcher.BatchOptions?)null);

    public void ConfigDBConnection(IDBConnection dbConnection, bool atomic)
    {
        Config.InfraConfig.SetAtomic(atomic);
        ConfigDBConnection(dbConnection, (Batcher.BatchOptions?)null);
    }

    public void ConfigDBConnection(IDBConnection dbConnection, Batcher.BatchOptions? options)
    {
        ArgumentNullException.ThrowIfNull(dbConnection);
        if (options is not null)
        {
            Config.InfraConfig.SetAtomic(options.Atomic);
        }
        var eventRepository = dbConnection.GetRepository();
        _eventStore = eventRepository is null ? null : new EventStore(eventRepository);
        Trash.Trash? trash = _eventStore is null
            ? null
            : new Trash.Trash(_eventStore, Repository.Mongo.MongoTrashRepository.FromDefault());
        _domainRepository = _eventStore is null ? null : new DomainRepository(_eventStore, trash);
        _storeNotice = _domainRepository is null ? null : new StoreNotice(_domainRepository);
        lock (_batchRepoLock) { _cachedBatchRepository = null; }

        // 与 Java 版 ConfigDBConnection 对齐：若连接提供批量仓储能力，则其事务/批量路径由
        // GetBatchRepository（MongoSingle/自定义 Provider）提供，此处无需额外持有包装器。
        // 显式触达一次 Provider 以暴露配置错误（null 表示无批量能力，走单条回退路径）。
        if (dbConnection is Batcher.IBatchRepositoryProvider provider)
        {
            var provided = provider.GetBatchRepository();
            if (provided is not null) lock (_batchRepoLock) { _cachedBatchRepository = provided; }
        }
    }

    /// <summary>显式配置领域仓储（便于测试/批量注入），与 Java 版 ConfigDomainRepository 对齐。</summary>
    public void ConfigDomainRepository(IDomainRepository repository)
    {
        _domainRepository = repository ?? throw new ArgumentNullException(nameof(repository));
        _storeNotice = new StoreNotice(_domainRepository);
        lock (_batchRepoLock) { _cachedBatchRepository = null; }
    }

    // ===== 全局事务/批量开关：单一数据源为 InfraConfig，Monitor 仅做委托 =====

    /// <summary>设置全局是否使用事务（需事务时设为 true，要求副本集）。建议在 New() 之后、ConfigDBConnection 之前调用。</summary>
    public Monitor SetAtomic(bool atomic)
    {
        Config.InfraConfig.SetAtomic(atomic);
        return this;
    }

    /// <summary>别名：设置是否使用事务。</summary>
    public Monitor SetTransactional(bool transactional) => SetAtomic(transactional);

    /// <summary>链式：设置事务开关后返回自身。</summary>
    public Monitor WithAtomic(bool atomic) => SetAtomic(atomic);

    /// <summary>当前全局是否使用事务。</summary>
    public bool IsAtomic() => Config.InfraConfig.IsAtomic;

    /// <summary>别名：当前是否使用事务。</summary>
    public bool IsTransactional() => Config.InfraConfig.IsTransactional;

    /// <summary>Lookup 分页大小，默认 500。</summary>
    public Monitor SetLookupPageSize(int pageSize)
    {
        Config.InfraConfig.SetLookupPageSize(pageSize);
        return this;
    }

    public int GetLookupPageSize() => Config.InfraConfig.LookupPageSize;

    /// <summary>Lookup 异步阈值，默认 10。</summary>
    public Monitor SetLookupAsyncThreshold(int threshold)
    {
        Config.InfraConfig.SetLookupAsyncThreshold(threshold);
        return this;
    }

    public int GetLookupAsyncThreshold() => Config.InfraConfig.LookupAsyncThreshold;

    /// <summary>Lookup 高频去重窗口（毫秒），默认 500。</summary>
    public Monitor SetLookupCoalesceWindowMs(long ms)
    {
        Config.InfraConfig.SetLookupCoalesceWindowMs(ms);
        return this;
    }

    public long GetLookupCoalesceWindowMs() => Config.InfraConfig.LookupCoalesceWindowMs;

    /// <summary>Lookup 批量写每批大小，默认 500。</summary>
    public Monitor SetLookupBulkBatchSize(int batchSize)
    {
        Config.InfraConfig.SetLookupBulkBatchSize(batchSize);
        return this;
    }

    public int GetLookupBulkBatchSize() => Config.InfraConfig.LookupBulkBatchSize;

    /// <summary>链式批量配置 Lookup。</summary>
    public Monitor WithLookupConfig(int pageSize, int asyncThreshold, long coalesceMs, int bulkBatchSize)
    {
        SetLookupPageSize(pageSize);
        SetLookupAsyncThreshold(asyncThreshold);
        SetLookupCoalesceWindowMs(coalesceMs);
        SetLookupBulkBatchSize(bulkBatchSize);
        return this;
    }

    /// <summary>
    /// 获取批量仓储（单例缓存，保证同一事务/会话亲和性；与 Java 版 <c>Monitor.getBatchRepository()</c> 持有单例对齐）。
    /// <para>wire-compatible 说明：字段名 <c>__stored__/__startVersion__</c>、<c>FuncType</c> 小写均为故意保留的 Java 线兼容要求，勿按 .NET 命名规范改名。</para>
    /// </summary>
    public Batcher.IBatchRepository? GetBatchRepository()
    {
        lock (_batchRepoLock)
        {
            if (_cachedBatchRepository is not null) return _cachedBatchRepository;
            var single = Repository.Mongo.MongoSingle.GetInstance();
            if (single.MongoDB is null || string.IsNullOrEmpty(single.DbName)) return null;
            _cachedBatchRepository = new Repository.Mongo.MongoBatchRepository(single.MongoDB, single.DbName);
            return _cachedBatchRepository;
        }
    }

    /// <summary>显式注入批量仓储（便于测试/自定义存储；注入后 <see cref="GetBatchRepository"/> 直接返回该单例）。</summary>
    public Monitor ConfigBatchRepository(Batcher.IBatchRepository? batchRepository)
    {
        lock (_batchRepoLock) { _cachedBatchRepository = batchRepository; }
        return this;
    }

    public ReportRegister RegisterReport<T>(Type modelClass, IDomainReportRepository<T> repository) where T : DomainModel<T>
    {
        if (_reportRegister == null)
            _reportRegister = new ReportRegister();
        return _reportRegister.Add(modelClass, repository);
    }

    /// <summary>注册 Lookup 模型（程序集+命名空间扫描），与 Java 版 RegisterLookupModel 对齐。</summary>
    public Lookup.LookupHandler? RegisterLookupModel(string assemblyName, string targetNamespace)
    {
        ArgumentNullException.ThrowIfNull(assemblyName);
        ArgumentNullException.ThrowIfNull(targetNamespace);
        if (_domainRepository is null) return null;
        return new Lookup.LookupHandler(assemblyName, targetNamespace, _domainRepository);
    }

    /// <summary>
    /// 注册 Lookup 模型并显式注入事件溯源仓储，彻底解耦默认连接。
    /// 与 Java 版 <c>RegisterLookupModel(packageName, sourceRepo)</c> 对齐，适用于单测/非 Mongo 存储。
    /// </summary>
    public Lookup.LookupHandler? RegisterLookupModel(string assemblyName, string targetNamespace, IEventSourcingRepository sourceRepo)
    {
        ArgumentNullException.ThrowIfNull(assemblyName);
        ArgumentNullException.ThrowIfNull(targetNamespace);
        if (_domainRepository is null) return null;
        return new Lookup.LookupHandler(assemblyName, targetNamespace, _domainRepository, sourceRepo);
    }

    /// <summary>历史兼容：旧命名仅为触发创建，与 <see cref="RegisterLookupModel(string,string)"/> 等价。</summary>
    [Obsolete("请使用 RegisterLookupModel（与 Java 版命名对齐）。")]
    public void ListenCreater(ICreaterGetter listen) => ConfigCreater(listen);
    public IEventStore GetEventStore()
    {
        return _eventStore;
    }

    public IDomainRepository? GetDomainRepository()
    {
        return _domainRepository;
    }
}

