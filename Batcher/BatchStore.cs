using Domain.Infrastructure.Model;

namespace Domain.Infrastructure.Batcher;

/// <summary>
/// 事件溯源仓储的批量存储适配器，与 Java 版 <c>BatchStore</c> 对齐。
/// <para>批量入口位于 Batcher 包，底层事件溯源仓储只负责实际存储实现。</para>
/// </summary>
public sealed class BatchStore : IBatchStore
{
    private readonly IBatchRepository _repository;

    public BatchStore(IBatchRepository repository)
    {
        _repository = repository ?? throw new DomainException("批量存储仓储未配置");
    }

    /// <inheritdoc/>
    public void PersistBatch(IReadOnlyList<BatchOperation> operations, bool atomic)
        => _repository.PersistBatch(operations, atomic);
}
