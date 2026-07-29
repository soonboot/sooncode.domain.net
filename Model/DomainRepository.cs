using System.Reflection;
using Domain.Infrastructure.Annotations;
using Domain.Infrastructure.Finder;
using Domain.Infrastructure.Monitor;
using Domain.Infrastructure.Session;
using Domain.Infrastructure.Validator;

namespace Domain.Infrastructure.Model;

public class DomainRepository : IDomainRepository
{
    protected IEventStore? _eventStore;

    public DomainRepository(IEventStore eventStore)
    {
        _eventStore = eventStore;
    }

    protected DomainRepository()
    {
    }

    public DomainModel<T> FindById<T>(string id) where T : DomainModel<T>
    {
        var streamName = StreamNameFor(typeof(T), id);
        var snapshot = _eventStore?.GetLatestSnapshot<T>(streamName);
        if (snapshot != null)
        {
            return snapshot;
        }

        return default;
    }

    public void Add<T>(DomainModel<T> entity) where T : DomainModel<T>
    {
        Add(entity, null, true);
    }

    public void Add<T>(DomainModel<T> entity, IGenerateReport<T>? report) where T : DomainModel<T>
    {
        Add(entity, report, true);
    }

    public void Add<T>(DomainModel<T> entity, IGenerateReport<T>? report, bool monitor) where T : DomainModel<T>
    {
        if (entity.__stored__) return;
        ValidateEntity(entity, FuncType.add);
        var isSkip = IsSkipEventSourcing(entity);
        var streamName = StreamNameFor(typeof(T), entity.Id);

        if (SessionManager.Contains(entity))
        {
            var session = SessionManager.Get(entity);
            session?.SetSessionFunction(() =>
            {
                SaveSnapshot(entity);
                if (!isSkip)
                    _eventStore?.CreateNewStream(streamName, entity.GetEvents(), entity.GetType());
            });
        }
        else
        {
            SaveSnapshot(entity);
            if (!isSkip)
                _eventStore?.CreateNewStream(streamName, entity.GetEvents(), entity.GetType());
        }

        entity.__stored__ = true;

        try
        {
            report?.Add(entity);
        }
        catch (Exception)
        {
            throw;
        }

        if (monitor && Domain.Infrastructure.Monitor.Monitor.Instance != null)
        {
            try
            {
                Domain.Infrastructure.Monitor.Monitor.Instance.Notice<T>(entity, FuncType.add);
            }
            catch (Exception)
            {
                throw;
            }
        }
    }

    public void Save<T>(DomainModel<T> entity) where T : DomainModel<T>
    {
        Save(entity, null, true);
    }

    public void Save<T>(DomainModel<T> entity, IGenerateReport<T>? report) where T : DomainModel<T>
    {
        Save(entity, report, true);
    }

    public void Save<T>(DomainModel<T> entity, IGenerateReport<T>? report, bool monitor) where T : DomainModel<T>
    {
        if (entity.__stored__) return;
        ValidateEntity(entity, FuncType.modify);
        DomainModel<T>? oldEntity = FindById<T>(entity.Id);
        var isSkip = IsSkipEventSourcing(entity);
        var streamName = StreamNameFor(typeof(T), entity.Id);

        if (SessionManager.Contains(entity))
        {
            var session = SessionManager.Get(entity);
            session?.SetSessionFunction(() =>
            {
                SaveSnapshot(entity);
                if (!isSkip)
                    _eventStore?.AppendEventToStream(streamName, entity.GetEvents(),
                        GetExpectedVersion(entity.__startVersion__), entity.GetType());
            });
        }
        else
        {
            SaveSnapshot(entity);
            if (!isSkip)
                _eventStore?.AppendEventToStream(streamName, entity.GetEvents(),
                    GetExpectedVersion(entity.__startVersion__), entity.GetType());
        }

        entity.__stored__ = true;

        try
        {
            report?.Modify(entity);
        }
        catch (Exception)
        {
            throw;
        }

        if (monitor && Domain.Infrastructure.Monitor.Monitor.Instance != null)
        {
            try
            {
                Domain.Infrastructure.Monitor.Monitor.Instance.Notice(entity, oldEntity!, FuncType.modify);
            }
            catch (Exception)
            {
                throw;
            }
        }
    }

    public void Delete<T>(DomainModel<T> entity) where T : DomainModel<T>
    {
        Delete(entity, null, true);
    }

    public void Delete<T>(DomainModel<T> entity, IGenerateReport<T>? report) where T : DomainModel<T>
    {
        Delete(entity, report, true);
    }

    public void Delete<T>(DomainModel<T> entity, IGenerateReport<T>? report, bool monitor) where T : DomainModel<T>
    {
        if (entity.__stored__) return;
        ValidateEntity(entity, FuncType.delete);
        var isSkip = IsSkipEventSourcing(entity);
        var streamName = StreamNameFor(typeof(T), entity.Id);

        if (SessionManager.Contains(entity))
        {
            var session = SessionManager.Get(entity);
            session?.SetSessionFunction(() =>
            {
                DeleteSnapshot(entity);
                if (!isSkip)
                    _eventStore?.Invalid(streamName, entity.GetEvents(), GetExpectedVersion(entity.__startVersion__),
                        entity.GetType());
            });
        }
        else
        {
            DeleteSnapshot(entity);
            if (!isSkip)
                _eventStore?.Invalid(streamName, entity.GetEvents(), GetExpectedVersion(entity.__startVersion__),
                    entity.GetType());
        }

        entity.__stored__ = true;

        try
        {
            report?.Delete(entity);
        }
        catch (Exception)
        {
            throw;
        }

        if (monitor && Domain.Infrastructure.Monitor.Monitor.Instance != null)
        {
            try
            {
                Domain.Infrastructure.Monitor.Monitor.Instance.Notice(entity, FuncType.delete);
            }
            catch (Exception)
            {
                throw;
            }
        }
    }

    public DomainModel<T> Replay<T>(string id, int toVersion) where T : DomainModel<T>
    {
        var streamName = StreamNameFor(typeof(T), id);
        int fromEventNumber = 0;
        int toEventNumber = toVersion;

        T? entity = _eventStore?.GetLatestSnapshot<T>(streamName);
        if (entity == null)
        {
            try
            {
                entity = Activator.CreateInstance<T>();
                if (entity != null)
                    entity.SetId(id);
            }
            catch
            {
                return default;
            }
        }

        if (!IsSkipEventSourcing(entity!))
        {
            var events = _eventStore?.GetStream(streamName, fromEventNumber, toEventNumber);
            if (events != null && entity is DomainModel<T> dm)
            {
                dm.Replay(events, fromEventNumber, toEventNumber);
            }
        }

        try
        {
            Domain.Infrastructure.Monitor.Monitor.Instance.Notice(entity, FuncType.replay);
        }
        catch (Exception ex)
        {
            throw;
        }

        return entity;
    }

    public void SaveSnapshot<T>(DomainModel<T> entity) where T : DomainModel<T>
    {
        var id = StreamNameFor(typeof(T), entity.Id);
        _eventStore?.SaveSnapshot(id, entity);
    }

    public void DeleteSnapshot<T>(DomainModel<T> entity) where T : DomainModel<T>
    {
        var streamId = StreamNameFor(typeof(T), entity.Id);
        _eventStore?.DeleteSnapshot(streamId, typeof(T));
    }

    public IEnumerable<DomainModel<T>> GetSnapshotList<T>()
        where T : DomainModel<T>
    {
        var snapshotList = _eventStore?.GetSnapshotList<T>(typeof(T).FullName ?? "");
        var result = new List<T>();

        if (snapshotList != null)
        {
            foreach (var snapshot in snapshotList)
            {
                if (snapshot != null)
                    result.Add(snapshot);
            }
        }

        return result;
    }

    public Finder.Page<EventWrapper> GetEventStream<T>(Creater creater, int pageSize, int pageIndex)
        where T : DomainModel<T>
    {
        string? modelType = typeof(T).FullName;
        string? streamType = typeof(T).FullName;
        string? createrId = creater?.Id;

        var result = _eventStore?.GetStream(modelType!, streamType!, createrId!, pageSize, pageIndex);
        if (result != null)
        {
            return (Finder.Page<EventWrapper>)result;
        }

        return new Finder.Page<EventWrapper>();
    }

    private int? GetExpectedVersion(int startVersion)
    {
        return startVersion == 0 ? null : startVersion;
    }

    private string StreamNameFor(Type c, string id)
    {
        return $"{c.FullName}-{id}";
    }

    private void ValidateEntity(Entity entity, FuncType funcType)
    {
        if (entity is IValidate validate)
        {
            var exception = validate.Validate(funcType);
            if (exception != null)
                throw exception;
        }
    }

    private static bool IsSkipEventSourcing(Entity entity)
    {
        var attr = entity.GetType().GetCustomAttribute<SkipEventSourcingAttribute>(true);
        return attr != null && attr.Value;
    }
}
