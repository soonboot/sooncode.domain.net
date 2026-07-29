using Domain.Infrastructure.Monitor;

namespace Domain.Infrastructure.Annotations;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Enum | AttributeTargets.Struct)]
public class EventBootAttribute : Attribute
{
    public FuncType StoreFunc { get; set; }
    public string[] Params { get; set; }
    public bool KeepAll { get; set; }
    
    public EventBootAttribute(FuncType storeFunc, string[] @params = default!, bool keepAll = false)
    {
        StoreFunc = storeFunc;
        Params = @params ?? Array.Empty<string>();
        KeepAll = keepAll;
    }
}