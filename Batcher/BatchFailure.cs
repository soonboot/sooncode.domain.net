using Domain.Infrastructure.Model;

namespace Domain.Infrastructure.Batcher;

/// <summary>批量操作中的单项失败信息，与 Java 版 <c>BatchFailure</c> 对齐。</summary>
/// <typeparam name="T">领域模型类型。</typeparam>
public sealed class BatchFailure<T> where T : Entity
{
    /// <summary>失败实体。</summary>
    public T Entity { get; }

    /// <summary>操作类型。</summary>
    public BatchOperation.OperationType OperationType { get; }

    /// <summary>异常。</summary>
    public Exception Exception { get; }

    /// <summary>异常消息。</summary>
    public string? Message => Exception?.Message;

    public BatchFailure(T entity, BatchOperation.OperationType operationType, Exception exception)
    {
        Entity = entity;
        OperationType = operationType;
        Exception = exception;
    }
}
