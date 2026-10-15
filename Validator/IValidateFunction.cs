namespace Domain.Infrastructure.Validator;

/// <summary>声明式校验函数，与 Java 版 <c>IValidateFunction</c> 对齐。</summary>
public interface IValidateFunction
{
    /// <summary>执行校验，返回是否通过。</summary>
    bool Run();
}
