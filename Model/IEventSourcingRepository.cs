using Domain.Infrastructure.Finder;

namespace Domain.Infrastructure.Model;

public interface IEventSourcingRepository
{
    void AddMetadata(EventStream stream);
    void UpdateMetadata(EventStream stream);
    void SaveStream(EventWrapper stream);
    EventStream? LoadMetadata(string streamName);
    List<EventWrapper> GetStream(string streamName, int? fromVersion, int? toVersion);
    Finder.Page<EventWrapper> GetStream(string modelType, string eventType, string creater, int pageSize, int pageIndex);
    void SaveSnapshotWrapper(SnapshotWrapper eventStream, string modelCollection);
    void DeleteSnapshotWrapper(string streamId, string modelCollection);
    SnapshotWrapper? GetSnapshotWrapper(string streamId, string modelCollection);
    List<SnapshotWrapper> GetSnapshotWrapperList(string streamType, string modelCollection);
    IDictionary<string, object>? GetSnapshotDoc(string streamType, string modelCollection);
}
