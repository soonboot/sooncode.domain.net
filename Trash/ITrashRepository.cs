namespace Domain.Infrastructure.Trash;

/// <summary>回收站仓储接口，与 Java 版 <c>ITrashRepository</c> 对齐。</summary>
public interface ITrashRepository
{
    /// <summary>删除实体时，将完整快照保存到回收站。</summary>
    void Save(Type entityClass, string streamName, string entityId);

    /// <summary>查询回收站中指定流的记录，不存在返回 null。</summary>
    IDictionary<string, object?>? FindByStreamId(string streamId);

    /// <summary>将回收站快照恢复到原快照集合，并删除回收站记录。</summary>
    IDictionary<string, object?>? RestoreSnapshot(string streamId, Type entityClass);

    /// <summary>分页查询指定实体类型的回收站记录。</summary>
    IReadOnlyList<IDictionary<string, object?>> ListByEntityType(string entityType, int pageIndex, int pageSize);

    /// <summary>统计指定实体类型的回收站记录数。</summary>
    long CountByEntityType(string entityType);

    /// <summary>分页查询全部回收站记录。</summary>
    IReadOnlyList<IDictionary<string, object?>> ListAll(int pageIndex, int pageSize);

    /// <summary>统计全部回收站记录数。</summary>
    long CountAll();

    /// <summary>根据流名称删除回收站记录。</summary>
    void RemoveByStreamId(string streamId);
}
