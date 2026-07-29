namespace Domain.Infrastructure.Validator;

public class DomainValidator
{
    public static ValidatorClass Validate(bool condition, string failMessage)
    {
        return new ValidatorClass().Validate(condition, failMessage);
    }

    public static ValidatorClass Validate(Func<bool> func, string failMessage)
    {
        return new ValidatorClass().Validate(func(), failMessage);
    }

    public class ValidatorClass
    {
        public ValidatorClass Validate(bool condition, string failMessage)
        {
            if (!condition)
                throw new ModelValidateFailException(failMessage);
            return this;
        }

        public ValidatorClass Validate(Func<bool> func, string failMessage)
        {
            return Validate(func(), failMessage);
        }
    }
}
