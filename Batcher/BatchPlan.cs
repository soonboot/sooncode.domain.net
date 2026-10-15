namespace Domain.Infrastructure.Batcher;

/// <summary>
/// 批量操作计划：只描述操作语义，不包含任何数据库对象。
/// 各列表保持原始批次中的相对顺序，创建后不可修改。与 Java 版 <c>BatchPlan</c> 对齐。
/// </summary>
public sealed class BatchPlan
{
    /// <summary>新增操作（保持相对顺序）。</summary>
    public IReadOnlyList<BatchOperation> Adds { get; }

    /// <summary>修改操作（保持相对顺序）。</summary>
    public IReadOnlyList<BatchOperation> Modifies { get; }

    /// <summary>删除操作（保持相对顺序）。</summary>
    public IReadOnlyList<BatchOperation> Deletes { get; }

    public BatchPlan(IReadOnlyList<BatchOperation> adds, IReadOnlyList<BatchOperation> modifies, IReadOnlyList<BatchOperation> deletes)
    {
        Adds = adds ?? throw new ArgumentNullException(nameof(adds));
        Modifies = modifies ?? throw new ArgumentNullException(nameof(modifies));
        Deletes = deletes ?? throw new ArgumentNullException(nameof(deletes));
    }

    /// <summary>全部操作（按 Add/Modify/Delete 分组顺序）。</summary>
    public IEnumerable<BatchOperation> All() => Adds.Concat(Modifies).Concat(Deletes);

    /// <summary>操作总数。</summary>
    public int Count => Adds.Count + Modifies.Count + Deletes.Count;
}
