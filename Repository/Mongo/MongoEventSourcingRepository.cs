using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;
using Domain.Infrastructure.Model;


namespace Domain.Infrastructure.Repository.Mongo;

public class MongoEventSourcingRepository : IEventSourcingRepository
{
    private IMongoDBDao? _dao;
    private string? _dbName;
    private const string EventMetadata = "eventMetadata";
    private const string EventSource = "eventSource";
    private const string EventSnapshot = "eventSnapshot";
    public const int VER = 15;

    public MongoEventSourcingRepository(string host, int port, string dbName)
    {
        var mongoConnection = new MongoConnection(host, port, dbName, this);
        _dao = MongoSingle.GetInstance().MongoDB;
        _dbName = dbName;
    }

    public MongoEventSourcingRepository(string host, int port, string user, string password, string dbName)
    {
        var mongoConnection = new MongoConnection(host, port, dbName, user, password, this);
        _dao = MongoSingle.GetInstance().MongoDB;
        _dbName = dbName;
    }

    internal MongoEventSourcingRepository(IMongoDBDao dao, string dbName)
    {
        _dao = dao;
        _dbName = dbName;
    }

    public void AddMetadata(EventStream stream)
    {
        if (_dao == null || _dbName == null) return;

        var col = _dao.GetCollection(_dbName, EventMetadata);

        var map = new Dictionary<string, object>
        {
            { "id", stream.Id },
            { "version", stream.Version },
            { "invalid", stream.IsInvalid },
            { "type", stream.EntityType?.FullName ?? "" },
            { "createDate", stream.CreateDate },
            { "ver", VER }
        };

        _dao.AddOne(col, map);
    }

    public void UpdateMetadata(EventStream stream)
    {
        if (_dao == null || _dbName == null) return;

        var col = _dao.GetCollection(_dbName, EventMetadata);
        var map = new Dictionary<string, object>
        {
            { "version", stream.Version },
            { "invalid", stream.IsInvalid }
        };

        var filter = new Dictionary<string, object> { { "id", stream.Id } };
        _dao.Update(col, filter, map);
    }

    public void SaveStream(EventWrapper stream)
    {
        if (_dao == null || _dbName == null) return;

        var col = _dao.GetCollection(_dbName, EventSource);

        var map = new Dictionary<string, object>
        {
            { "id", stream.Id },
            { "version", stream.Version },
            { "streamId", stream.StreamId },
            { "event", MongoJsonUtil.ToJsonObject(stream.Event) },
            { "eventType", stream.EventType?.FullName ?? "" },
            { "creater", stream.Creater != null ? MongoJsonUtil.ToJsonObject(stream.Creater) : "{}" },
            { "createDate", stream.CreateDate },
            { "description", stream.Description != null ? MongoJsonUtil.ToJsonObject(stream.Description) : "{}" }
        };

        _dao.AddOne(col, map);
    }

    public EventStream? LoadMetadata(string streamName)
    {
        if (_dao == null || _dbName == null) return null;

        var col = _dao.GetCollection(_dbName, EventMetadata);
        var filter = new Dictionary<string, object> { { "id", streamName } };

        var doc = _dao.FindFirst(col, new BsonDocument(filter));
        if (doc == null) return null;

        try
        {
            var typeName = doc.Contains("type") ? doc["type"].AsString : "";
            var entityType = !string.IsNullOrEmpty(typeName) ? Type.GetType(typeName) : null;

            return new EventStream(
                doc["id"].AsString,
                doc["version"].AsInt32,
                doc["invalid"].AsInt32,
                entityType!,
                doc["createDate"].ToUniversalTime()
            );
        }
        catch
        {
            return null;
        }
    }

    public List<EventWrapper> GetStream(string streamName, int? fromVersion, int? toVersion)
    {
        if (_dao == null || _dbName == null) return new List<EventWrapper>();

        var col = _dao.GetCollection(_dbName, EventSource);

        var events = new List<EventWrapper>();

        var filter = new BsonDocument { { "streamId", streamName } };
        if (fromVersion.HasValue)
            filter["version"] = new BsonDocument("$gte", fromVersion.Value);
        if (toVersion.HasValue)
        {
            if (filter.Contains("version"))
                ((BsonDocument)filter["version"])["$lte"] = toVersion.Value;
            else
                filter["version"] = new BsonDocument("$lte", toVersion.Value);
        }

        var sort = new Dictionary<string, SortEnum> { { "version", SortEnum.ASC } };
        var docs = _dao.FindSync(col, filter, sort);

        foreach (var doc in docs)
        {
            try
            {
                var eventDoc = doc["event"].AsBsonDocument;
                var eventTypeName = doc["eventType"].AsString;
                var eventType = !string.IsNullOrEmpty(eventTypeName) ? Type.GetType(eventTypeName) : null;

                if (eventType != null)
                {
                    var domainEvent = MongoJsonUtil.ConvertToObject(eventDoc, eventType) as DomainEvent;
                    if (domainEvent == null) continue;

                    Dictionary<string, object> createrMap = new();
                    if (doc.Contains("creater") && doc["creater"] != BsonNull.Value)
                    {
                        var createrDoc = doc["creater"].AsBsonDocument;
                        foreach (var element in createrDoc)
                            createrMap[element.Name] = element.Value.ToString() ?? "";
                    }

                    Dictionary<string, object> descMap = new();
                    if (doc.Contains("description") && doc["description"] != BsonNull.Value)
                    {
                        var descDoc = doc["description"].AsBsonDocument;
                        foreach (var element in descDoc)
                            descMap[element.Name] = element.Value.ToString() ?? "";
                    }

                    var wrapper = new EventWrapper(
                        domainEvent,
                        doc["version"].AsInt32,
                        doc.Contains("createDate") ? doc["createDate"].ToUniversalTime() : DateTime.UtcNow,
                        doc["streamId"].AsString,
                        createrMap,
                        descMap
                    );
                    events.Add(wrapper);
                }
            }
            catch { }
        }

        return events;
    }

    public Finder.Page<EventWrapper> GetStream(string modelType, string eventType, string creater, int pageSize, int pageIndex)
    {
        if (_dao == null || _dbName == null) return new Finder.Page<EventWrapper>();

        var col = _dao.GetCollection(_dbName, EventSource);

        var filter = new BsonDocument();

        if (!string.IsNullOrEmpty(modelType))
            filter["streamId"] = new BsonRegularExpression(".*" + Regex.Escape(modelType) + ".*");

        if (!string.IsNullOrEmpty(eventType))
            filter["eventType"] = eventType;

        if (!string.IsNullOrEmpty(creater))
            filter["creater.id"] = creater;

        var sortMap = new Dictionary<string, SortEnum> { { "createDate", SortEnum.DESC } };

        var events = new List<EventWrapper>();

        var docs = _dao.FindByPageSync(col, filter, pageIndex, pageSize, sortMap);

        foreach (var doc in docs)
        {
            try
            {
                var eventDoc = doc["event"].AsBsonDocument;
                var eventTypeName = doc["eventType"].AsString;
                var eventDataType = !string.IsNullOrEmpty(eventTypeName) ? Type.GetType(eventTypeName) : null;

                if (eventDataType != null)
                {
                    var domainEvent = MongoJsonUtil.ConvertToObject(eventDoc, eventDataType) as DomainEvent;
                    if (domainEvent == null) continue;

                    Dictionary<string, object> createrMap = new();
                    if (doc.Contains("creater") && doc["creater"] != BsonNull.Value)
                    {
                        var createrDoc = doc["creater"].AsBsonDocument;
                        foreach (var element in createrDoc)
                            createrMap[element.Name] = element.Value.ToString() ?? "";
                    }

                    Dictionary<string, object> descMap = new();
                    if (doc.Contains("description") && doc["description"] != BsonNull.Value)
                    {
                        var descDoc = doc["description"].AsBsonDocument;
                        foreach (var element in descDoc)
                            descMap[element.Name] = element.Value.ToString() ?? "";
                    }

                    var wrapper = new EventWrapper(
                        domainEvent,
                        doc["version"].AsInt32,
                        doc.Contains("createDate") ? doc["createDate"].ToUniversalTime() : DateTime.UtcNow,
                        doc["streamId"].AsString,
                        createrMap,
                        descMap
                    );
                    events.Add(wrapper);
                }
            }
            catch { }
        }

        var total = _dao.Count(col, new BsonDocument());

        return new Finder.Page<EventWrapper>
        {
            PageSize = pageSize,
            PageIndex = pageIndex,
            TotalElements = total,
            Content = events
        };
    }

    public void SaveSnapshotWrapper(SnapshotWrapper snapshot, string modelCollection)
    {
        if (_dao == null || _dbName == null) return;

        var col = _dao.GetCollection(_dbName, GetCollectionName(modelCollection));

        var map = new Dictionary<string, object>
        {
            { "snapshot", MongoJsonUtil.ToJsonObject(snapshot.Snapshot!) },
            { "createDate", snapshot.CreateDate }
        };

        var filter = new Dictionary<string, object> { { "streamId", snapshot.StreamId } };

        if (_dao.IsExit(col, new BsonDocument(filter)))
        {
            _dao.Update(col, filter, map);
        }
        else
        {
            map["snapshotType"] = snapshot.SnapshotType?.FullName ?? "";
            map["streamId"] = snapshot.StreamId;
            _dao.AddOne(col, map);
        }
    }

    public void DeleteSnapshotWrapper(string streamId, string modelCollection)
    {
        if (_dao == null || _dbName == null) return;

        var col = _dao.GetCollection(_dbName, GetCollectionName(modelCollection));
        var filter = new Dictionary<string, object> { { "streamId", streamId } };
        _dao.Delete(col, filter);
    }

    public SnapshotWrapper? GetSnapshotWrapper(string streamId, string modelCollection)
    {
        if (_dao == null || _dbName == null) return null;

        var col = _dao.GetCollection(_dbName, GetCollectionName(modelCollection));
        var filter = new Dictionary<string, object> { { "streamId", streamId } };

        var doc = _dao.FindFirst(col, new BsonDocument(filter));
        if (doc == null) return null;

        try
        {
            var dataDoc = doc["snapshot"].AsBsonDocument;
            var dataTypeName = doc.Contains("snapshotType") ? doc["snapshotType"].AsString : "";
            var dataType = !string.IsNullOrEmpty(dataTypeName) ? Type.GetType(dataTypeName) : null;

            if (dataType != null)
            {
                var entity = MongoJsonUtil.ConvertToObject(dataDoc, dataType) as Entity;
                return new SnapshotWrapper(doc["streamId"].AsString, entity!);
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    public List<SnapshotWrapper> GetSnapshotWrapperList(string streamType, string modelCollection)
    {
        if (_dao == null || _dbName == null) return new List<SnapshotWrapper>();

        var col = _dao.GetCollection(_dbName, GetCollectionName(modelCollection));
        var filter = new BsonDocument { { "snapshotType", streamType } };

        var snapshots = new List<SnapshotWrapper>();

        var docs = _dao.FindSync(col, filter, null);
        foreach (var doc in docs)
        {
            try
            {
                var dataDoc = doc["snapshot"].AsBsonDocument;
                var dataTypeName = doc.Contains("snapshotType") ? doc["snapshotType"].AsString : "";
                var dataType = !string.IsNullOrEmpty(dataTypeName) ? Type.GetType(dataTypeName) : null;

                if (dataType != null)
                {
                    var entity = MongoJsonUtil.ConvertToObject(dataDoc, dataType) as Entity;
                    if (entity == null) continue;
                    var snapshot = new SnapshotWrapper(doc["streamId"].AsString, entity!);
                    snapshots.Add(snapshot);
                }
            }
            catch { }
        }

        return snapshots;
    }

    public IDictionary<string, object>? GetSnapshotDoc(string streamType, string modelCollection)
    {
        if (_dao == null || _dbName == null) return null;

        var col = _dao.GetCollection(_dbName, GetCollectionName(modelCollection));
        var filter = new Dictionary<string, object> { { "snapshotType", streamType } };

        var doc = _dao.FindFirst(col, new BsonDocument(filter));
        if (doc == null || !doc.Contains("snapshot")) return null;

        var result = new Dictionary<string, object>();
        var snapshotDoc = doc["snapshot"].AsBsonDocument;
        foreach (var element in snapshotDoc)
        {
            result[element.Name] = element.Value.ToString() ?? "";
        }

        return result;
    }

    private string GetCollectionName(string modelCollection)
    {
        if (!string.IsNullOrEmpty(modelCollection))
            return modelCollection;
        return EventSnapshot;
    }
}
