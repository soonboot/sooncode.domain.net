using Domain.Infrastructure.Finder;

namespace Domain.Infrastructure.Model;

public interface IEventStore
{
    void CreateNewStream(string streamName, IEnumerable<DomainEvent> domainEvents, Type cla);
    void AppendEventToStream(string streamName, IEnumerable<DomainEvent> domainEvents, int? expectedVersion, Type cla);
    void AppendEventToStream(string streamName, IEnumerable<DomainEvent> domainEvents, Type cla);
    void Invalid(string streamName, IEnumerable<DomainEvent> domainEvents, int? expectedVersion, Type cla);
    List<DomainEvent>? GetStream(string streamName, int fromVersion, int toVersion);
    Finder.Page<EventWrapper> GetStream(string modelType, string eventType, string creater, int pageSize, int pageIndex);
    void SaveSnapshot(string id, Entity snapshot);
    void DeleteSnapshot(string streamId,Type cla);
    T? GetLatestSnapshot<T>(string id) where T : DomainModel<T>;
    List<T> GetSnapshotList<T>(string streamType) where T : DomainModel<T>;
}
