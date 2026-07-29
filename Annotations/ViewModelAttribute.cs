namespace Domain.Infrastructure.Annotations;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, Inherited = true, AllowMultiple = false)]
public class ViewModelAttribute : Attribute
{
}
