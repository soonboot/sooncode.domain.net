namespace Domain.Infrastructure.Annotations;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Enum | AttributeTargets.Struct)]
public class DomainReportAttribute : Attribute
{
    public Type Model { get; }
    
    public DomainReportAttribute(Type model)
    {
        Model = model;
    }
}