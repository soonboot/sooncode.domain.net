namespace Domain.Infrastructure.Batcher;

/// <summary>批量持久化存储接口，与 Java 版 <c>IBatchStore</c> 对齐。</summary>
public interface IBatchStore
{
    /// <summary>持久化已准备好的批量操作。</summary>
    void PersistBatch(IReadOnlyList<BatchOperation> operations, bool atomic);
}
