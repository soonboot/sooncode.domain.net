using System.Text.Json.Serialization;
using Domain.Infrastructure.Annotations;

namespace Domain.Infrastructure.Model;

public class EventWrapper
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("eventType")]
    public Type? EventType { get; set; }

    [JsonPropertyName("event")]
    public DomainEvent? Event { get; set; }

    [JsonPropertyName("streamId")]
    public string StreamId { get; set; }

    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("createDate")]
    public DateTime CreateDate { get; set; }

    [JsonPropertyName("creater")]
    public Creater? Creater { get; set; }

    [JsonPropertyName("description")]
    public DescriptionModel? Description { get; set; }

    public EventWrapper()
    {
    }

    public EventWrapper(DomainEvent @event, int eventVersion, DateTime createDate, string streamId, Dictionary<string, object>? creater, Dictionary<string, object>? description)
    {
        Id = $"{streamId}-{eventVersion}";
        Event = @event;
        EventType = @event?.GetType();
        StreamId = streamId;
        Version = eventVersion;
        CreateDate = createDate;

        if (creater != null && creater.TryGetValue("id", out var id) && creater.TryGetValue("name", out var name))
        {
            Creater = new Creater(name?.ToString(), id?.ToString());
        }

        Description = new DescriptionModel();
        if (description != null)
        {
            if (description.TryGetValue("eventDescription", out var eventDesc))
                Description.EventDescription = eventDesc?.ToString();
            if (description.TryGetValue("sourceModelDescription", out var sourceDesc))
                Description.SourceModelDescription = sourceDesc?.ToString();
        }
    }

    public EventWrapper(DomainEvent @event, int eventVersion, string streamId, Type? sourceClass)
    {
        Id = $"{streamId}-{eventVersion}";
        Event = @event;
        EventType = @event?.GetType();
        StreamId = streamId;
        Version = eventVersion;
        CreateDate = DateTime.UtcNow;

        Description = new DescriptionModel();

        if (@event != null && @event.GetType().IsDefined(typeof(DescriptionAttribute), true))
        {
            var attr = @event.GetType().GetCustomAttributes(typeof(DescriptionAttribute), true).FirstOrDefault() as DescriptionAttribute;
            Description.EventDescription = attr?.Value;
        }

        if (sourceClass != null && sourceClass.IsDefined(typeof(DescriptionAttribute), true))
        {
            var attr = sourceClass.GetCustomAttributes(typeof(DescriptionAttribute), true).FirstOrDefault() as DescriptionAttribute;
            Description.SourceModelDescription = attr?.Value;
        }

        var createrGetter = Monitor.Monitor.Instance?.GetCreaterGetter();
        if (createrGetter != null)
        {
            Creater = createrGetter.GetCurrUser();
        }
    }
}

public class DescriptionModel
{
    [JsonPropertyName("eventDescription")]
    public string? EventDescription { get; set; }

    [JsonPropertyName("sourceModelDescription")]
    public string? SourceModelDescription { get; set; }
}