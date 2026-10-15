namespace Domain.Infrastructure.Validator;

/// <summary>声明式校验入口，与 Java 版 <c>Validator</c> 对齐。</summary>
public class DomainValidator
{
    /// <summary>校验布尔条件。</summary>
    public static ValidatorClass Validate(bool condition, string failMessage)
        => new ValidatorClass().Validate(condition, failMessage);

    /// <summary>校验委托。</summary>
    public static ValidatorClass Validate(Func<bool> func, string failMessage)
    {
        ArgumentNullException.ThrowIfNull(func);
        return new ValidatorClass().Validate(func(), failMessage);
    }

    /// <summary>校验函数式接口（对齐 Java <c>IValidateFunction</c> 重载）。</summary>
    public static ValidatorClass Validate(IValidateFunction func, string failMessage)
    {
        ArgumentNullException.ThrowIfNull(func);
        return new ValidatorClass().Validate(func.Run(), failMessage);
    }

    /// <summary>链式校验器。</summary>
    public class ValidatorClass
    {
        /// <summary>校验布尔条件，不通过抛出 <see cref="ModelValidateFailException"/>。</summary>
        public ValidatorClass Validate(bool condition, string failMessage)
        {
            if (!condition)
                throw new ModelValidateFailException(failMessage);
            return this;
        }

        /// <summary>校验委托。</summary>
        public ValidatorClass Validate(Func<bool> func, string failMessage)
        {
            ArgumentNullException.ThrowIfNull(func);
            return Validate(func(), failMessage);
        }

        /// <summary>校验函数式接口。</summary>
        public ValidatorClass Validate(IValidateFunction func, string failMessage)
        {
            ArgumentNullException.ThrowIfNull(func);
            return Validate(func.Run(), failMessage);
        }
    }
}

/// <summary>兼容别名：历史名称 <c>Validator</c>，新代码请使用 <see cref="DomainValidator"/>。</summary>
[Obsolete("请使用 DomainValidator。")]
public class Validator : DomainValidator
{
}
