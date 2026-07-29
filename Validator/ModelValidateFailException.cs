using Domain.Infrastructure.Model;

namespace Domain.Infrastructure.Validator;

public class ModelValidateFailException(string message) : DomainException(message);
