namespace Domain.Infrastructure.Annotations;

[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Field | AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public class NotRequiredAttribute : Attribute
{
}
