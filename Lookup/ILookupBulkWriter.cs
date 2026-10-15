using Domain.Infrastructure.Model;

namespace Domain.Infrastructure.Lookup;

/// <summary>
/// Lookup 专用的批量快照写入器（BEST_EFFORT），与 Java 版 <c>ILookupBulkWriter</c> 对齐。
/// <para>与业务批量彻底解耦：只做快照表的 ReplaceOne-upsert，不走校验/CAS/事件流/事务/Notice。</para>
/// </summary>
public interface ILookupBulkWriter
{
    /// <summary>批量保存快照（按 ReplaceOne upsert）。</summary>
    /// <param name="modelType">快照所属的模型类型，用于解析快照集合名。</param>
    /// <param name="entities">已在内存中完成字段回填的脏实体列表。</param>
    /// <returns>成功写入条数。</returns>
    int BulkSaveSnapshots(Type modelType, IReadOnlyList<Entity> entities);

    /// <summary>刷新批量写入器的动态配置。默认空实现。</summary>
    void Refresh() { }
}
