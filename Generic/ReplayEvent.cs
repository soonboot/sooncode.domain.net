using Domain.Infrastructure.Annotations;
using Domain.Infrastructure.Model;
using Domain.Infrastructure.Monitor;
using System.Text.Json.Serialization;

namespace Domain.Infrastructure.Generic;

/// <summary>
/// 事件重放
/// </summary>
[Description("事件重放")]
[EventBoot(FuncType.replay)]
public class ReplayEvent : DomainEvent
{
    [JsonPropertyName("data")]
    public Entity Data { get; set; }
    
    public Type Wclass { get; set; }
    public int FromVersion { get; set; }
    public int ToVersion { get; set; }
    
    public ReplayEvent(string aggregateId, Entity data, int fromVersion, int toVersion) : base(aggregateId)
    {
        Data = data;
        FromVersion = fromVersion;
        ToVersion = toVersion;
        Wclass = data.GetType();
    }
}