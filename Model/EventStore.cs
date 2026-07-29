
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using Domain.Infrastructure.Annotations;

namespace Domain.Infrastructure.Model;

public class EventStore : IEventStore
{
    private static IEventSourcingRepository? _repository;
    private readonly ConcurrentDictionary<Type, string> _snapshotNames = new();

    public EventStore(IEventSourcingRepository repository)
    {
        _repository = repository;
    }

    public void CreateNewStream(string streamName, IEnumerable<DomainEvent> domainEvents, Type cla)
    {
        var eventStream = new EventStream(streamName, cla);
        eventStream.CreateDate = DateTime.UtcNow;
        _repository?.AddMetadata(eventStream);
        AppendEventToStream(streamName, domainEvents, cla);
    }

    public void AppendEventToStream(string streamName, IEnumerable<DomainEvent> domainEvents, int? expectedVersion, Type cla)
    {
        var eventList = domainEvents.ToList();
        if (eventList.Count == 0) return;

        var eventStream = _repository?.LoadMetadata(streamName);
        if (eventStream == null)
            throw new DomainException("没有找到元数据:" + streamName);

        if (eventStream.IsInvalid == 1)
            throw new DomainException("数据已经失效:" + streamName);

        if (expectedVersion.HasValue)
        {
            CheckForConcurrencyError(expectedVersion.Value, eventStream);
        }

        foreach (var @event in eventList)
        {
            var wrapper = eventStream.RegisterEvent(@event, cla);
            _repository?.SaveStream(wrapper);
        }

        _repository?.UpdateMetadata(eventStream);
    }

    public void AppendEventToStream(string streamName, IEnumerable<DomainEvent> domainEvents, Type cla)
    {
        AppendEventToStream(streamName, domainEvents, null, cla);
    }

    public void Invalid(string streamName, IEnumerable<DomainEvent> domainEvents, int? expectedVersion, Type cla)
    {
        AppendEventToStream(streamName, domainEvents, expectedVersion, cla);
        var eventStream = _repository?.LoadMetadata(streamName);
        if (eventStream != null)
        {
            eventStream.IsInvalid = 1;
            _repository?.UpdateMetadata(eventStream);
        }
    }

    public List<DomainEvent>? GetStream(string streamName, int fromVersion, int toVersion)
    {
        var eventWrappers = _repository?.GetStream(streamName, fromVersion, toVersion);
        if (eventWrappers == null || eventWrappers.Count == 0) return null;

        var events = new List<DomainEvent>();
        foreach (var wrapper in eventWrappers)
        {
            if (wrapper.Event != null)
                events.Add(wrapper.Event);
        }
        return events;
    }

    public Finder.Page<EventWrapper> GetStream(string modelType, string eventType, string creater, int pageSize, int pageIndex)
    {
        var result = _repository?.GetStream(modelType, eventType, creater, pageSize, pageIndex);
        if (result != null)
            return result;
        return new Finder.Page<EventWrapper>();
    }

    public void SaveSnapshot(string id, Entity snapshot)
    {
        var eventWrapper = new SnapshotWrapper(id, snapshot);
        _repository?.SaveSnapshotWrapper(eventWrapper, GetCollectionName(snapshot.GetType()));
    }

    public void DeleteSnapshot(string streamId,Type cla)
    {
        _repository?.DeleteSnapshotWrapper(streamId, GetCollectionName(cla));
    }

    public T? GetLatestSnapshot<T>(string id) where T : DomainModel<T>
    {
        var latestSnapshot = _repository?.GetSnapshotWrapper(id, GetCollectionName(typeof(T)));
        if (latestSnapshot == null)
            return null;
        return latestSnapshot.Snapshot as T;
    }

    public List<T> GetSnapshotList<T>(string streamType) where T : DomainModel<T>
    {
        var wrapperList = _repository?.GetSnapshotWrapperList(streamType, GetCollectionName(typeof(T)));
        var result = new List<T>();
        if (wrapperList == null) return result;

        foreach (var wrapper in wrapperList)
        {
            if (wrapper.Snapshot != null)
                result.Add((T)wrapper.Snapshot);
        }
        return result;
    }

    private static void CheckForConcurrencyError(int expectedVersion, EventStream stream)
    {
        int lastUpdatedVersion = stream.Version ?? 0;
        if (lastUpdatedVersion != expectedVersion)
        {
            string error = string.Format("预期版本号: {0}。 找到的版本号: {1}", expectedVersion, lastUpdatedVersion);
            throw new DomainException(error);
        }
    }
    private string GetCollectionName(Type cType)
    {
        return _snapshotNames.GetOrAdd(cType, _ =>
        {
            string collectionName = "";
            if (cType.IsDefined(typeof(ModelSnapshotAttribute), true))
            {
                var modelSnapshot = cType.GetCustomAttribute<ModelSnapshotAttribute>();
                if (modelSnapshot != null)
                {
                    collectionName = modelSnapshot.Value ?? "";
                    if (string.IsNullOrEmpty(collectionName))
                    {
                        collectionName = modelSnapshot.CollectionName ?? "";
                    }
                }
            }
            return collectionName;
        });
    }
}
