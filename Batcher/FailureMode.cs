namespace Domain.Infrastructure.Batcher;

/// <summary>批量操作的失败处理策略，与 Java 版 <c>FailureMode</c> 对齐。</summary>
public enum FailureMode
{
    /// <summary>单个操作失败后记录失败并继续执行后续操作。</summary>
    Continue,

    /// <summary>任意操作失败立即抛出异常，批量操作不再继续。</summary>
    FailFast,
}
