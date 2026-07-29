namespace Domain.Infrastructure.Annotations;

[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public class SkipEventSourcingAttribute : Attribute
{
    public bool Value { get; }

    public SkipEventSourcingAttribute(bool value = false)
    {
        Value = value;
    }
}
