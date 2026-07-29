namespace Domain.Infrastructure.Annotations;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Struct)]
public class ModelSnapshotAttribute : Attribute
{
    public string Value { get; }
    public string CollectionName { get; }

    public ModelSnapshotAttribute(string value = "", string collectionName = "")
    {
        Value = value;
        CollectionName = collectionName;
    }
}