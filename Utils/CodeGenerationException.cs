namespace Domain.Infrastructure.Utils;

/// <summary>代码/反射生成异常，与 Java 版 <c>CodeGenerationException</c> 对齐。</summary>
public sealed class CodeGenerationException : Exception
{
    public CodeGenerationException(Exception cause)
        : base($"{cause.GetType().Name}-->{cause.Message}", cause)
    {
    }

    public CodeGenerationException(string message) : base(message)
    {
    }

    public CodeGenerationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
