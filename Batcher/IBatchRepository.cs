namespace Domain.Infrastructure.Batcher;

/// <summary>
/// 批量持久化仓储接口，与 Java 版 <c>IBatchRepository</c> 对齐。
/// <para>与单实体的事件溯源仓储分离，具体数据库可独立提供批量写入、事务与 bulk 优化。</para>
/// </summary>
public interface IBatchRepository
{
    /// <summary>持久化批量操作。</summary>
    /// <param name="operations">已准备好的批量操作。</param>
    /// <param name="atomic">是否使用事务。</param>
    void PersistBatch(IReadOnlyList<BatchOperation> operations, bool atomic);
}
