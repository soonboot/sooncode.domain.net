using System.Text.Json.Serialization;
using Domain.Infrastructure.Model;

namespace Domain.Infrastructure.Monitor;

public class FunctionModel
{
    public Entity? SourceEntity { get; set; }

    public Entity? TargetEntity { get; set; }
}
public class EventFunctionModel
{
    [JsonPropertyName("event")]
    public DomainEvent? Event { get; set; }

    [JsonPropertyName("entity")]
    public Entity? Entity { get; set; }
}
