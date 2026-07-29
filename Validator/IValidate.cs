using Domain.Infrastructure.Monitor;

namespace Domain.Infrastructure.Validator;

public interface IValidate
{
    ModelValidateFailException? Validate(FuncType funcType);
}
