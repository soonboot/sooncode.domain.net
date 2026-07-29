using System.Text.Json.Serialization;

namespace Domain.Infrastructure.Model;

public class SnapshotWrapper
{
    [JsonPropertyName("streamId")]
    public string? StreamId { get; set; }

    [JsonPropertyName("snapshot")]
    public Entity? Snapshot { get; set; }

    [JsonPropertyName("snapshotType")]
    public Type? SnapshotType { get; set; }

    [JsonPropertyName("createDate")]
    public DateTime CreateDate { get; set; }

    [JsonPropertyName("id")]
    public string? Id => StreamId ?? string.Empty;

    public SnapshotWrapper()
    {
    }

    public SnapshotWrapper(string streamId, Entity snapshot)
    {
        StreamId = streamId;
        Snapshot = snapshot;
        SnapshotType = snapshot.GetType();
        CreateDate = DateTime.UtcNow;
    }

    public SnapshotWrapper(string streamId, string streamType, int version, Entity data)
    {
        StreamId = streamId;
        SnapshotType = data.GetType();
        Snapshot = data;
        CreateDate = DateTime.UtcNow;
    }
}
