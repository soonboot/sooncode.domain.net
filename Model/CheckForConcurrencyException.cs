namespace Domain.Infrastructure.Model;

/// <summary>乐观并发冲突异常：期望版本号与实际版本不一致时抛出。</summary>
public sealed class CheckForConcurrencyException : DomainException
{
    public CheckForConcurrencyException(string message) : base(message)
    {
    }

    public CheckForConcurrencyException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
