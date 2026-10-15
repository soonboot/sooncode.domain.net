namespace Domain.Infrastructure.Batcher;

/// <summary>批量仓储能力提供者（可选能力），与 Java 版 <c>IBatchRepositoryProvider</c> 对齐。</summary>
public interface IBatchRepositoryProvider
{
    /// <summary>获取批量仓储。</summary>
    IBatchRepository? GetBatchRepository();
}
