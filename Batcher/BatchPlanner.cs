using Domain.Infrastructure.Model;

namespace Domain.Infrastructure.Batcher;

/// <summary>
/// 将批量命令转换为按操作语义分组的计划。与 Java 版 <c>BatchPlanner</c> 对齐。
/// <para>不依赖具体数据库；重复操作在入口处直接拒绝。</para>
/// </summary>
public sealed class BatchPlanner
{
    /// <summary>生成批量计划，重复流名称直接拒绝。</summary>
    public BatchPlan Plan(IReadOnlyList<BatchOperation> operations)
    {
        var adds = new List<BatchOperation>();
        var modifies = new List<BatchOperation>();
        var deletes = new List<BatchOperation>();
        if (operations is null) return new BatchPlan(adds, modifies, deletes);

        var streamNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var op in operations)
        {
            Validate(op);
            if (!streamNames.Add(op.StreamName()))
            {
                throw new DomainException($"同一批量中实体 ID 不能重复:{op.Entity.Id}");
            }

            switch (op.Type)
            {
                case BatchOperation.OperationType.Add: adds.Add(op); break;
                case BatchOperation.OperationType.Modify: modifies.Add(op); break;
                case BatchOperation.OperationType.Delete: deletes.Add(op); break;
                default: throw new DomainException($"未知批量操作类型:{op.Type}");
            }
        }

        return new BatchPlan(adds, modifies, deletes);
    }

    private static void Validate(BatchOperation? operation)
    {
        if (operation is null) throw new DomainException("批量操作不能为 null");
        if (operation.Entity is null) throw new DomainException("批量操作实体不能为 null");
    }
}
