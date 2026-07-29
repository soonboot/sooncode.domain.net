namespace Domain.Infrastructure.Annotations;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
public class LookupAttribute : Attribute
{
    public Type FromModel { get; set; } = null!;
    public string LocalField { get; set; } = string.Empty;
    public string FromField { get; set; } = string.Empty;
}
