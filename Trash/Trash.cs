using Domain.Infrastructure.Finder;
using Domain.Infrastructure.Model;

namespace Domain.Infrastructure.Trash;

/// <summary>
/// 回收站业务门面，封装恢复/查询/统计逻辑，通过 <see cref="ITrashRepository"/> 与具体数据库解耦。
/// 与 Java 版 <c>Trash</c> 对齐。
/// </summary>
public sealed class Trash
{
    private readonly IEventStore _eventStore;
    private ITrashRepository? _trashRepository;

    /// <summary>构造回收站门面（显式指定仓储实现）。</summary>
    public Trash(IEventStore eventStore, ITrashRepository? trashRepository)
    {
        _eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
        _trashRepository = trashRepository;
    }

    /// <summary>显式设置数据层实现。</summary>
    public void SetTrashRepository(ITrashRepository trashRepository)
    {
        _trashRepository = trashRepository ?? throw new ArgumentNullException(nameof(trashRepository));
    }

    /// <summary>返回当前是否配置了实际的回收站存储实现。</summary>
    public bool IsEnabled => _trashRepository is not null;

    /// <summary>删除实体时，将完整快照保存到回收站。</summary>
    public void SaveToTrash(Type entityClass, string streamName, string entityId)
    {
        _trashRepository?.Save(entityClass, streamName, entityId);
    }

    /// <summary>恢复回收站中的实体。</summary>
    public T Restore<T>(string entityId) where T : DomainModel<T>
    {
        var repo = EnsureRepository();
        var streamName = StreamNameFor(typeof(T), entityId);
        var trashDoc = repo.FindByStreamId(streamName)
            ?? throw new DomainException($"Trash 未找到数据:{streamName}");
        _eventStore.Reactivate(streamName);
        repo.RestoreSnapshot(streamName, typeof(T));
        if (!trashDoc.TryGetValue("snapshotDoc", out var snapshotDocObj) ||
            snapshotDocObj is not IDictionary<string, object?> snapshotDoc ||
            !snapshotDoc.TryGetValue("snapshot", out var snapshotObj) ||
            snapshotObj is not IDictionary<string, object?> snapshot)
        {
            throw new DomainException($"Trash 快照数据异常:{streamName}");
        }

        var entity = Utils.JsonUtil.ConvertToObject<T>(snapshot.ToDictionary(kv => kv.Key, kv => kv.Value!));
        return entity ?? throw new DomainException($"Trash 反序列化失败:{streamName}");
    }

    /// <summary>分页查询指定实体类型的回收站记录。</summary>
    public Page<TrashRecord> ListTrash<T>(int pageIndex, int pageSize)
    {
        var repo = EnsureRepository();
        var entityType = typeof(T).FullName ?? typeof(T).Name;
        var docs = repo.ListByEntityType(entityType, pageIndex, pageSize);
        var total = repo.CountByEntityType(entityType);
        return BuildPage(docs, total, pageIndex, pageSize);
    }

    /// <summary>统计指定实体类型的回收站记录数。</summary>
    public long CountTrash<T>() => EnsureRepository().CountByEntityType(typeof(T).FullName ?? typeof(T).Name);

    /// <summary>分页查询全部回收站记录。</summary>
    public Page<TrashRecord> ListAllTrash(int pageIndex, int pageSize)
    {
        var repo = EnsureRepository();
        return BuildPage(repo.ListAll(pageIndex, pageSize), repo.CountAll(), pageIndex, pageSize);
    }

    /// <summary>统计全部回收站记录数。</summary>
    public long CountAllTrash() => EnsureRepository().CountAll();

    private ITrashRepository EnsureRepository()
        => _trashRepository ?? throw new DomainException("回收站仓储未配置");

    private static string StreamNameFor(Type entityType, string entityId)
        => $"{entityType.FullName}-{entityId}";

    private static Page<TrashRecord> BuildPage(IReadOnlyList<IDictionary<string, object?>> docs, long total, int pageIndex, int pageSize)
    {
        var records = docs.Select(d => new TrashRecord
        {
            StreamName = d.TryGetValue("streamName", out var s) ? s?.ToString() ?? string.Empty : string.Empty,
            EntityType = d.TryGetValue("entityType", out var e) ? e?.ToString() ?? string.Empty : string.Empty,
            EntityId = d.TryGetValue("entityId", out var id) ? id?.ToString() ?? string.Empty : string.Empty,
            SnapshotDoc = d,
        }).ToList();
        return new Page<TrashRecord>
        {
            Content = records,
            TotalElements = total,
            PageIndex = pageIndex,
            PageSize = pageSize,
        };
    }
}
