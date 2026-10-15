namespace Domain.Infrastructure.Repository.Mongo;

/// <summary>
/// Mongo 持久层排序方向（驱动层取值：ASC=1 / DESC=-1），与 Java 版 <c>repository.mongo.SortEnum</c> 对齐。
/// <para>
/// 注意：它与查询 DSL <see cref="Finder.Sort"/> / <see cref="Finder.Sort.SortType"/> 不是重复定义：
/// 前者是领域查询意图（asc/desc），后者是 Mongo 驱动可直接使用的持久化值，
/// 两者经由 <c>FindBuild.Sort(Finder.Sort, prefix)</c> 显式映射（asc→ASC，desc→DESC）。请勿合并或删除其中之一。
/// </para>
/// </summary>
public enum SortEnum
{
    /// <summary>升序，对应 Mongo 排序值 1。</summary>
    ASC = 1,
    /// <summary>降序，对应 Mongo 排序值 -1。</summary>
    DESC = -1
}
