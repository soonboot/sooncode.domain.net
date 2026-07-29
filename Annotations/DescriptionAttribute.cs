namespace Domain.Infrastructure.Annotations;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Enum | AttributeTargets.Struct)]
public class DescriptionAttribute : Attribute
{
    public string Value { get; }
    
    public DescriptionAttribute(string value = "")
    {
        Value = value;
    }
}