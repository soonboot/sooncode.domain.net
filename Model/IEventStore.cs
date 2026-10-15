using Domain.Infrastructure.Finder;

namespace Domain.Infrastructure.Model;

public interface IEventStore
{
    void CreateNewStream(string streamName, IEnumerable<DomainEvent> domainEvents, Type cla);
    void AppendEventToStream(string streamName, IEnumerable<DomainEvent> domainEvents, int? expectedVersion, Type cla);
    void AppendEventToStream(string streamName, IEnumerable<DomainEvent> domainEvents, Type cla);
    void Invalid(string streamName, IEnumerable<DomainEvent> domainEvents, int? expectedVersion, Type cla);
    /// <summary>重新激活已失效的流（回收站恢复时使用）。</summary>
    void Reactivate(string streamName);
    List<DomainEvent>? GetStream(string streamName, int fromVersion, int toVersion);
    Finder.Page<EventWrapper> GetStream(string modelType, string eventType, string creater, int pageSize, int pageIndex);
    void SaveSnapshot(string id, Entity snapshot);
    void DeleteSnapshot(string streamId,Type cla);
    T? GetLatestSnapshot<T>(string id) where T : DomainModel<T>;
    List<T> GetSnapshotList<T>(string streamType) where T : DomainModel<T>;
}
