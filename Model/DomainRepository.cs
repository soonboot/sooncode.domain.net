using System.Reflection;
using Domain.Infrastructure.Annotations;
using Domain.Infrastructure.Finder;
using Domain.Infrastructure.Monitor;
using Domain.Infrastructure.Session;
using Domain.Infrastructure.Validator;

namespace Domain.Infrastructure.Model;

public class DomainRepository : IDomainRepository
{
    protected IEventStore? _eventStore;
    protected Trash.Trash? _trash;

    public DomainRepository(IEventStore eventStore)
        : this(eventStore, null)
    {
    }

    public DomainRepository(IEventStore eventStore, Trash.Trash? trash)
    {
        _eventStore = eventStore;
        _trash = trash;
    }

    /// <summary>设置回收站门面（可选）。</summary>
    public void SetTrashRepository(Trash.Trash trash) => _trash = trash;

    protected DomainRepository()
    {
    }

    /// <summary>按 Id 查询快照；命中时回填 <c>__startVersion__</c> 乐观锁基线（与 Java 版 findByID 对齐）。</summary>
    public DomainModel<T> FindById<T>(string id) where T : DomainModel<T>
    {
        var streamName = StreamNameFor(typeof(T), id);
        var snapshot = _eventStore?.GetLatestSnapshot<T>(streamName);
        if (snapshot is DomainModel<T> dm)
        {
            dm.__startVersion__ = dm.GetVersion();
            return dm;
        }

        return default!;
    }

    public void Add<T>(DomainModel<T> entity) where T : DomainModel<T>
    {
        Add(entity, null, true);
    }

    public void Add<T>(DomainModel<T> entity, IGenerateReport<T>? report) where T : DomainModel<T>
    {
        Add(entity, report, true);
    }

    public void Add<T>(DomainModel<T> entity, IGenerateReport<T>? report, bool monitor) where T : DomainModel<T>
    {
        ArgumentNullException.ThrowIfNull(entity);
        // 批量作用域内显式 API 直接收集，不做单条持久化（与 Java 版 BatchRepository.capture 对齐）。
        if (Batcher.BatcherCapture.TryCapture(entity, Monitor.FuncType.add, this)) return;
        if (entity.__stored__) return;
        var isSkip = IsSkipEventSourcing(entity);
        var streamName = StreamNameFor(typeof(T), entity.Id);

        if (SessionManager.Contains(entity))
        {
            var session = SessionManager.Get(entity);
            // 会话内收集为 BatchOperation，Commit 时统一事务提交，保证原子性。
            if (session is DomainSession ds)
            {
                ds.AddOperation(new Batcher.BatchOperation(
                    Batcher.BatchOperation.OperationType.Add, entity, null, null, null, isSkip, true));
            }
            else
            {
                session?.SetSessionFunction(() =>
                {
                    SaveSnapshot(entity);
                    if (!isSkip)
                        CreateNewStream(entity, streamName);
                    else
                        entity.MarkEventsPersisted(entity.GetPendingEvents());
                    entity.MarkStored();
                });
            }
        }
        else if (TryAtomicSingle(Batcher.BatchOperation.OperationType.Add, entity, null, isSkip, null))
        {
            // 批量层已完成校验、快照、MarkEventsPersisted/MarkStored。
        }
        else
        {
            // 无批量存储回退：为减少不一致窗口，先写事件/元数据再写快照（与 Java 版注释对齐）。
            if (!isSkip)
                CreateNewStream(entity, streamName);
            else
                entity.MarkEventsPersisted(entity.GetPendingEvents());
            SaveSnapshot(entity);
            entity.MarkStored();
        }

        try
        {
            report?.Add(entity);
        }
        catch (Exception)
        {
            throw;
        }

        if (monitor && Domain.Infrastructure.Monitor.Monitor.Instance != null)
        {
            try
            {
                Domain.Infrastructure.Monitor.Monitor.Instance.Notice<T>(entity, FuncType.add);
            }
            catch (Exception)
            {
                throw;
            }
        }
    }

    public void Save<T>(DomainModel<T> entity) where T : DomainModel<T>
    {
        Save(entity, null, true);
    }

    public void Save<T>(DomainModel<T> entity, IGenerateReport<T>? report) where T : DomainModel<T>
    {
        Save(entity, report, true);
    }

    public void Save<T>(DomainModel<T> entity, IGenerateReport<T>? report, bool monitor) where T : DomainModel<T>
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (Batcher.BatcherCapture.TryCapture(entity, Monitor.FuncType.modify, this)) return;
        if (entity.__stored__) return;
        DomainModel<T>? oldEntity = FindById<T>(entity.Id);
        var isSkip = IsSkipEventSourcing(entity);
        var streamName = StreamNameFor(typeof(T), entity.Id);

        if (SessionManager.Contains(entity))
        {
            var session = SessionManager.Get(entity);
            if (session is DomainSession ds)
            {
                int? expected = entity.__startVersion__ == 0 ? null : entity.__startVersion__;
                ds.AddOperation(new Batcher.BatchOperation(
                    Batcher.BatchOperation.OperationType.Modify, entity, oldEntity, null, expected, isSkip, true));
            }
            else
            {
                session?.SetSessionFunction(() =>
                {
                    SaveSnapshot(entity);
                    if (!isSkip)
                        AppendToStream(entity, streamName);
                    else
                        entity.MarkEventsPersisted(entity.GetPendingEvents());
                    entity.MarkStored();
                });
            }
        }
        else
        {
            int? expected = entity.__startVersion__ == 0 ? null : entity.__startVersion__;
            if (!TryAtomicSingle(Batcher.BatchOperation.OperationType.Modify, entity, oldEntity, isSkip, expected))
            {
                // 回退路径：快照+事件非同一事务（EventStore 层已补 CAS）。
                if (!isSkip)
                    AppendToStream(entity, streamName);
                else
                    entity.MarkEventsPersisted(entity.GetPendingEvents());
                SaveSnapshot(entity);
                entity.MarkStored();
            }
        }

        try
        {
            report?.Modify(entity);
        }
        catch (Exception)
        {
            throw;
        }

        if (monitor && Domain.Infrastructure.Monitor.Monitor.Instance != null)
        {
            try
            {
                Domain.Infrastructure.Monitor.Monitor.Instance.Notice(entity, oldEntity!, FuncType.modify);
            }
            catch (Exception)
            {
                throw;
            }
        }
    }

    public void Delete<T>(DomainModel<T> entity) where T : DomainModel<T>
    {
        Delete(entity, null, true);
    }

    public void Delete<T>(DomainModel<T> entity, IGenerateReport<T>? report) where T : DomainModel<T>
    {
        Delete(entity, report, true);
    }

    public void Delete<T>(DomainModel<T> entity, IGenerateReport<T>? report, bool monitor) where T : DomainModel<T>
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (Batcher.BatcherCapture.TryCapture(entity, Monitor.FuncType.delete, this)) return;
        if (entity.__stored__) return;
        var isSkip = IsSkipEventSourcing(entity);
        var streamName = StreamNameFor(typeof(T), entity.Id);

        if (SessionManager.Contains(entity))
        {
            var session = SessionManager.Get(entity);
            if (session is DomainSession ds)
            {
                int? expected = entity.__startVersion__ == 0 ? null : entity.__startVersion__;
                ds.AddOperation(new Batcher.BatchOperation(
                    Batcher.BatchOperation.OperationType.Delete, entity, null, null, expected, isSkip, true));
            }
            else
            {
                session?.SetSessionFunction(() =>
                {
                    ValidateEntity(entity, FuncType.delete);
                    _trash?.SaveToTrash(typeof(T), streamName, entity.Id);
                    DeleteSnapshot(entity);
                    if (!isSkip)
                        InvalidStream(entity, streamName);
                    else
                        entity.MarkEventsPersisted(entity.GetPendingEvents());
                    entity.MarkStored();
                });
            }
        }
        else
        {
            int? expected = entity.__startVersion__ == 0 ? null : entity.__startVersion__;
            if (!TryAtomicSingle(Batcher.BatchOperation.OperationType.Delete, entity, null, isSkip, expected))
            {
                // 回退路径：trash+快照删除+事件失效非事务（delete 的 CAS 已在 Invalid 中补偿）。
                ValidateEntity(entity, FuncType.delete);
                if (!isSkip)
                    InvalidStream(entity, streamName);
                else
                    entity.MarkEventsPersisted(entity.GetPendingEvents());
                _trash?.SaveToTrash(typeof(T), streamName, entity.Id);
                DeleteSnapshot(entity);
                entity.MarkStored();
            }
        }

        try
        {
            report?.Delete(entity);
        }
        catch (Exception)
        {
            throw;
        }

        if (monitor && Domain.Infrastructure.Monitor.Monitor.Instance != null)
        {
            try
            {
                Domain.Infrastructure.Monitor.Monitor.Instance.Notice(entity, FuncType.delete);
            }
            catch (Exception)
            {
                throw;
            }
        }
    }

    /// <summary>
    /// 不进批量事务的直写内核（供 <c>BatchRepository.PersistOneByOne</c> / <c>DomainSession</c> 回退路径使用，避免经 <c>TryAtomicSingle</c> 二次进入批量层）。
    /// 与 Java 版 <c>persistOneByOne</c> 直接 <c>repository.add/save/delete</c> 直写语义对齐；调用方负责外层编排与 <c>Monitor</c> 触发。
    /// </summary>
    internal void AddDirect<T>(T entity) where T : DomainModel<T>
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (entity.__stored__) return;
        var isSkip = IsSkipEventSourcing(entity);
        var streamName = StreamNameFor(typeof(T), entity.Id);
        if (!isSkip)
            CreateNewStream(entity, streamName);
        else
            entity.MarkEventsPersisted(entity.GetPendingEvents());
        SaveSnapshot(entity);
        entity.MarkStored();
    }

    /// <summary>直写内核：修改（见 <see cref="AddDirect{T}"/>）。</summary>
    internal void SaveDirect<T>(T entity) where T : DomainModel<T>
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (entity.__stored__) return;
        var isSkip = IsSkipEventSourcing(entity);
        var streamName = StreamNameFor(typeof(T), entity.Id);
        if (!isSkip)
            AppendToStream(entity, streamName);
        else
            entity.MarkEventsPersisted(entity.GetPendingEvents());
        SaveSnapshot(entity);
        entity.MarkStored();
    }

    /// <summary>直写内核：删除（见 <see cref="AddDirect{T}"/>）。</summary>
    internal void DeleteDirect<T>(T entity) where T : DomainModel<T>
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (entity.__stored__) return;
        var isSkip = IsSkipEventSourcing(entity);
        var streamName = StreamNameFor(typeof(T), entity.Id);
        ValidateEntity(entity, Monitor.FuncType.delete);
        if (!isSkip)
            InvalidStream(entity, streamName);
        else
            entity.MarkEventsPersisted(entity.GetPendingEvents());
        _trash?.SaveToTrash(typeof(T), streamName, entity.Id);
        DeleteSnapshot(entity);
        entity.MarkStored();
    }

    public DomainModel<T> Replay<T>(string id, int toVersion) where T : DomainModel<T>
    {
        var streamName = StreamNameFor(typeof(T), id);
        int fromEventNumber = 0;
        int toEventNumber = toVersion;

        T? entity = _eventStore?.GetLatestSnapshot<T>(streamName);
        if (entity == null)
        {
            try
            {
                entity = Activator.CreateInstance<T>();
                if (entity != null)
                    entity.SetId(id);
            }
            catch
            {
                return default;
            }
        }

        if (!IsSkipEventSourcing(entity!))
        {
            var events = _eventStore?.GetStream(streamName, fromEventNumber, toEventNumber);
            if (events != null && entity is DomainModel<T> dm)
            {
                dm.Replay(events, fromEventNumber, toEventNumber);
            }
        }

        try
        {
            Domain.Infrastructure.Monitor.Monitor.Instance.Notice(entity, FuncType.replay);
        }
        catch (Exception ex)
        {
            throw;
        }

        return entity;
    }

    public void SaveSnapshot<T>(DomainModel<T> entity) where T : DomainModel<T>
    {
        var id = StreamNameFor(typeof(T), entity.Id);
        _eventStore?.SaveSnapshot(id, entity);
    }

    public void DeleteSnapshot<T>(DomainModel<T> entity) where T : DomainModel<T>
    {
        var streamId = StreamNameFor(typeof(T), entity.Id);
        _eventStore?.DeleteSnapshot(streamId, typeof(T));
    }

    public IEnumerable<DomainModel<T>> GetSnapshotList<T>()
        where T : DomainModel<T>
    {
        var snapshotList = _eventStore?.GetSnapshotList<T>(typeof(T).FullName ?? "");
        var result = new List<T>();

        if (snapshotList != null)
        {
            foreach (var snapshot in snapshotList)
            {
                if (snapshot != null)
                    result.Add(snapshot);
            }
        }

        return result;
    }

    public Finder.Page<EventWrapper> GetEventStream<T>(Creater creater, int pageSize, int pageIndex)
        where T : DomainModel<T>
    {
        string? modelType = typeof(T).FullName;
        string? streamType = typeof(T).FullName;
        string? createrId = creater?.Id;

        var result = _eventStore?.GetStream(modelType!, streamType!, createrId!, pageSize, pageIndex);
        if (result != null)
        {
            return (Finder.Page<EventWrapper>)result;
        }

        return new Finder.Page<EventWrapper>();
    }

    /// <summary>
    /// 单条原子提交：通过批量事务保证快照+事件+元数据同一事务且带版本 CAS。
    /// 成功返回 true（已由批量层完成 MarkEventsPersisted/MarkStored），无批量存储时返回 false 走回退路径。
    /// 与 Java 版 <c>tryAtomicSingle</c> 对齐。
    /// </summary>
    private bool TryAtomicSingle(Batcher.BatchOperation.OperationType type, Entity entity, Entity? oldEntity, bool skipES, int? expectedVersion)
    {
        ArgumentNullException.ThrowIfNull(entity);
        var monitor = Monitor.Monitor.Instance;
        if (monitor is null) return false;
        var batchRepo = monitor.GetBatchRepository();
        if (batchRepo is null) return false;

        if (type == Batcher.BatchOperation.OperationType.Modify && oldEntity is null && entity is not null)
        {
            // MODIFY 需要 oldEntity 做校验时尝试回查（泛型反射兼容）。
            try
            {
                var mid = entity.GetType().GetMethod("FindByIdWrapper");
            }
            catch { }
        }

        var op = new Batcher.BatchOperation(type, entity, oldEntity, null, expectedVersion, skipES, _trash is not null && _trash.IsEnabled);
        var opts = new Batcher.BatchOptions()
            .WithAtomic(Config.InfraConfig.IsAtomic)
            .WithFailureMode(Batcher.FailureMode.FailFast)
            .WithMonitor(false);
        batchRepo.PersistBatch(new List<Batcher.BatchOperation> { op }, opts.Atomic);

        // 成功后同步 __startVersion__ 到新版本，保持会话外乐观锁基准（Entity 基类无版本字段，反射兼容泛型）。
        try
        {
            var versionMethod = entity.GetType().GetMethod("GetVersion");
            var startField = entity.GetType().GetField("__startVersion__");
            if (versionMethod?.Invoke(entity, null) is int v && startField is not null)
                startField.SetValue(entity, v);
        }
        catch { }

        return true;
    }

    private void CreateNewStream<T>(DomainModel<T> entity, string streamName) where T : DomainModel<T>
    {
        var events = PrepareEventSnapshots(entity);
        _eventStore?.CreateNewStream(streamName, events, entity.GetType());
        entity.MarkEventsPersisted(events);
    }

    private void AppendToStream<T>(DomainModel<T> entity, string streamName) where T : DomainModel<T>
    {
        var events = PrepareEventSnapshots(entity);
        _eventStore?.AppendEventToStream(streamName, events, GetExpectedVersion(entity.__startVersion__), entity.GetType());
        entity.MarkEventsPersisted(events);
    }

    private void InvalidStream<T>(DomainModel<T> entity, string streamName) where T : DomainModel<T>
    {
        var events = PrepareEventSnapshots(entity);
        _eventStore?.Invalid(streamName, events, GetExpectedVersion(entity.__startVersion__), entity.GetType());
        entity.MarkEventsPersisted(events);
    }

    /// <summary>
    /// 为本次待写入事件准备最终实体审计快照，不改变事件定义参数。与 Java 版 <c>prepareEventSnapshots</c> 对齐。
    /// 必须在实体最终状态确定且领域校验通过后、事件真正写入前调用。
    /// </summary>
    public IReadOnlyList<DomainEvent> PrepareEventSnapshots<T>(DomainModel<T> entity) where T : DomainModel<T>
    {
        ArgumentNullException.ThrowIfNull(entity);
        return entity.PreparePendingEventSnapshots();
    }

    /// <summary>历史方法名，语义同 <see cref="PrepareEventSnapshots{T}"/>，保留仅为兼容。</summary>
    [Obsolete("请使用 PrepareEventSnapshots（与 Java 版 convertEventParam→prepareEventSnapshots 更名对齐）。")]
    public IReadOnlyList<DomainEvent> ConvertEventParam<T>(DomainModel<T> entity) where T : DomainModel<T>
        => PrepareEventSnapshots(entity);

    private int? GetExpectedVersion(int startVersion)
    {
        return startVersion == 0 ? null : startVersion;
    }

    private string StreamNameFor(Type c, string id)
    {
        return $"{c.FullName}-{id}";
    }

    private void ValidateEntity(Entity entity, FuncType funcType)
    {
        if (entity is IValidate validate)
        {
            var exception = validate.Validate(funcType);
            if (exception != null)
                throw exception;
        }
    }

    private static bool IsSkipEventSourcing(Entity entity)
    {
        var attr = entity.GetType().GetCustomAttribute<SkipEventSourcingAttribute>(true);
        return attr != null && attr.Value;
    }
}
