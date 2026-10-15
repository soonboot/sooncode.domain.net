namespace Domain.Infrastructure.Trash;

/// <summary>回收站记录，与 Java 版 <c>TrashRecord</c> 对齐。</summary>
public sealed class TrashRecord
{
    /// <summary>流名称（实体全名-实体 Id）。</summary>
    public string StreamName { get; set; } = string.Empty;

    /// <summary>实体类型全限定名。</summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>实体主键。</summary>
    public string EntityId { get; set; } = string.Empty;

    /// <summary>快照文档。</summary>
    public IDictionary<string, object?>? SnapshotDoc { get; set; }

    /// <summary>删除时间（Utc）。</summary>
    public DateTime DeletedAt { get; set; } = DateTime.UtcNow;
}
