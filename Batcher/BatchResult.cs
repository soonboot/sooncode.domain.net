using Domain.Infrastructure.Model;

namespace Domain.Infrastructure.Batcher;

/// <summary>批量操作结果，与 Java 版 <c>BatchResult</c> 对齐。</summary>
/// <typeparam name="T">领域模型类型。</typeparam>
public sealed class BatchResult<T> where T : Entity
{
    private readonly List<BatchFailure<T>> _failures = new();

    /// <summary>成功条数。</summary>
    public int SuccessCount { get; private set; }

    /// <summary>跳过条数。</summary>
    public int SkippedCount { get; private set; }

    /// <summary>失败条数。</summary>
    public int FailureCount => _failures.Count;

    /// <summary>失败项只读列表。</summary>
    public IReadOnlyList<BatchFailure<T>> Failures => _failures.AsReadOnly();

    /// <summary>总数 = 成功 + 跳过 + 失败。</summary>
    public int TotalCount => SuccessCount + SkippedCount + _failures.Count;

    /// <summary>是否全部成功。</summary>
    public bool IsSuccess => FailureCount == 0;

    internal void IncrementSuccess() => SuccessCount++;

    internal void IncrementSkipped() => SkippedCount++;

    internal void AddFailure(T entity, BatchOperation.OperationType operationType, Exception exception)
        => _failures.Add(new BatchFailure<T>(entity, operationType, exception));

    internal void Merge(BatchResult<T> other)
    {
        if (other is null) return;
        SuccessCount += other.SuccessCount;
        SkippedCount += other.SkippedCount;
        _failures.AddRange(other._failures);
    }
}
