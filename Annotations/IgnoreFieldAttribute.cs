namespace Domain.Infrastructure.Annotations;

[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Field | AttributeTargets.Property)]
public class IgnoreFieldAttribute : Attribute
{
}