using Domain.Infrastructure.Model;

namespace Domain.Infrastructure.Batcher;

/// <summary>批量操作统一接口，与 Java 版 <c>IBatcher</c> 对齐。</summary>
/// <typeparam name="T">领域模型类型。</typeparam>
public interface IBatcher<T> where T : DomainModel<T>
{
    /// <summary>显式收集新增实体。</summary>
    IBatcher<T> Add(T entity);

    /// <summary>显式收集新增实体集合。</summary>
    IBatcher<T> AddAll(IEnumerable<T> entities);

    /// <summary>显式收集修改实体。</summary>
    IBatcher<T> Save(T entity);

    /// <summary>显式收集修改实体集合。</summary>
    IBatcher<T> SaveAll(IEnumerable<T> entities);

    /// <summary>显式收集删除实体。</summary>
    IBatcher<T> Delete(T entity);

    /// <summary>显式收集删除实体集合。</summary>
    IBatcher<T> DeleteAll(IEnumerable<T> entities);

    /// <summary>按 Id 收集删除操作。</summary>
    IBatcher<T> DeleteByIds(IEnumerable<string> ids);

    /// <summary>设置提交选项。</summary>
    IBatcher<T> Options(BatchOptions options);

    /// <summary>获取提交选项。</summary>
    BatchOptions GetOptions();

    /// <summary>在作用域内执行业务动作并提交（领域行为收集 + 显式收集）。</summary>
    BatchResult<T> Run(Action<IBatcher<T>> action);

    /// <summary>提交已收集的操作。</summary>
    BatchResult<T> Execute();
}
