using Domain.Infrastructure.Model;

namespace Domain.Infrastructure.Batcher;

/// <summary>
/// 非泛型（跨聚合）批量操作接口，与 Java 版 <c>Batcher&lt;?&gt;</c> 对齐。
/// <para>泛型 <see cref="IBatcher{T}"/> 将一批次限定为单一 CLR 类型；本接口允许同一批次混装多种聚合根，实现真正的跨聚合批量语义。</para>
/// </summary>
public interface IMixedBatcher
{
    /// <summary>显式收集新增实体（任意聚合类型）。</summary>
    IMixedBatcher Add(Entity entity);

    /// <summary>显式收集新增实体集合。</summary>
    IMixedBatcher AddAll(IEnumerable<Entity> entities);

    /// <summary>显式收集修改实体。</summary>
    IMixedBatcher Save(Entity entity);

    /// <summary>显式收集修改实体集合。</summary>
    IMixedBatcher SaveAll(IEnumerable<Entity> entities);

    /// <summary>显式收集删除实体。</summary>
    IMixedBatcher Delete(Entity entity);

    /// <summary>显式收集删除实体集合。</summary>
    IMixedBatcher DeleteAll(IEnumerable<Entity> entities);

    /// <summary>设置提交选项。</summary>
    IMixedBatcher Options(BatchOptions options);

    /// <summary>获取提交选项。</summary>
    BatchOptions GetOptions();

    /// <summary>在作用域内执行业务动作并提交（领域行为收集 + 显式收集）。</summary>
    BatchResult<Entity> Run(Action<IMixedBatcher> action);

    /// <summary>提交已收集的操作。</summary>
    BatchResult<Entity> Execute();
}
