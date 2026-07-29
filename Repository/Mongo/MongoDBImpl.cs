using MongoDB.Bson;
using MongoDB.Driver;

namespace Domain.Infrastructure.Repository.Mongo;

public class MongoDBImpl : IMongoDBDao
{
    private readonly MongoDBUtil _dbUtil;

    public MongoDBImpl(string host, int port)
    {
        _dbUtil = new MongoDBUtil(host, port);
    }

    public MongoDBImpl(string host, int port, string user, string password)
    {
        _dbUtil = new MongoDBUtil(host, port, user, password);
    }

    public IMongoDatabase? GetDb(string dbName)
    {
        if (!string.IsNullOrEmpty(dbName))
        {
            return _dbUtil.GetDatabase(dbName);
        }
        return null;
    }

    public IMongoCollection<BsonDocument> GetCollection(string dbName, string collectionName)
    {
        if (string.IsNullOrEmpty(collectionName) || string.IsNullOrEmpty(dbName))
        {
            return null!;
        }

        var database = _dbUtil.GetDatabase(dbName);
        if (database == null) return null!;

        return database.GetCollection<BsonDocument>(collectionName);
    }

    public bool AddOne(IMongoCollection<BsonDocument> collection, IDictionary<string, object> map)
    {
        var document = new BsonDocument(map);
        collection.InsertOne(document);
        return true;
    }

    public bool AddMany(IMongoCollection<BsonDocument> collection, List<IDictionary<string, object>> list)
    {
        var docList = list.Select(m => new BsonDocument(m)).ToList();
        collection.InsertMany(docList);
        return true;
    }

    public long Delete(IMongoCollection<BsonDocument> collection, IDictionary<string, object> filter)
    {
        var bson = new BsonDocument(filter);
        var result = collection.DeleteMany(bson);
        return result.DeletedCount;
    }

    public long DeleteById(IMongoCollection<BsonDocument> coll, string id)
    {
        if (!ObjectId.TryParse(id, out var objectId))
        {
            return 0;
        }

        var filter = Builders<BsonDocument>.Filter.Eq("_id", objectId);
        var result = coll.DeleteOne(filter);
        return result.DeletedCount;
    }

    public BsonDocument? FindFirst(IMongoCollection<BsonDocument> coll, BsonDocument filter)
    {
        return coll.Find(filter).FirstOrDefault();
    }

    public BsonDocument? FindFirst(IMongoCollection<BsonDocument> coll, BsonDocument filter, IDictionary<string, SortEnum>? sort)
    {
        if (sort == null)
        {
            return coll.Find(filter).FirstOrDefault();
        }
        else
        {
            var sortBson = new BsonDocument();
            foreach (var s in sort)
            {
                sortBson.Add(s.Key, (int)s.Value);
            }
            return coll.Find(filter).Sort(sortBson).FirstOrDefault();
        }
    }

    public long Count(IMongoCollection<BsonDocument> coll, BsonDocument filter)
    {
        return coll.CountDocuments(filter);
    }

    public async IAsyncEnumerable<BsonDocument> FindTop(IMongoCollection<BsonDocument> coll, BsonDocument filter, IDictionary<string, SortEnum>? sort, int num)
    {
        foreach (var doc in FindTopSync(coll, filter, sort, num))
            yield return doc;
    }

    public List<BsonDocument> FindTopSync(IMongoCollection<BsonDocument> coll, BsonDocument filter, IDictionary<string, SortEnum>? sort, int num)
    {
        if (sort == null)
        {
            return coll.Find(filter).Limit(num).ToList();
        }
        else
        {
            var sortBson = new BsonDocument();
            foreach (var s in sort)
                sortBson.Add(s.Key, (int)s.Value);
            return coll.Find(filter).Sort(sortBson).Limit(num).ToList();
        }
    }

    public async IAsyncEnumerable<BsonDocument> Find(IMongoCollection<BsonDocument> coll, BsonDocument filter, IDictionary<string, SortEnum>? sort)
    {
        foreach (var doc in FindSync(coll, filter, sort))
            yield return doc;
    }

    public List<BsonDocument> FindSync(IMongoCollection<BsonDocument> coll, BsonDocument filter, IDictionary<string, SortEnum>? sort)
    {
        if (sort == null)
        {
            return coll.Find(filter).ToList();
        }
        else
        {
            var sortBson = new BsonDocument();
            foreach (var s in sort)
                sortBson.Add(s.Key, (int)s.Value);
            return coll.Find(filter).Sort(sortBson).ToList();
        }
    }

    public async Task<IAsyncCursor<BsonDocument>> FindAsync(IMongoCollection<BsonDocument> coll, FilterDefinition<BsonDocument> filter, SortDefinition<BsonDocument>? sort = null)
    {
        if (sort == null)
        {
            return await coll.FindAsync(filter);
        }
        else
        {
            return await coll.FindAsync(filter, new FindOptions<BsonDocument> { Sort = sort });
        }
    }

    public async Task<IAsyncCursor<BsonDocument>> FindByPageAsync(IMongoCollection<BsonDocument> coll, FilterDefinition<BsonDocument> filter, int pageIndex, int pageSize, SortDefinition<BsonDocument>? sort = null)
    {
        var options = new FindOptions<BsonDocument>
        {
            Skip = pageIndex * pageSize,
            Limit = pageSize
        };

        if (sort != null)
        {
            options.Sort = sort;
        }

        return await coll.FindAsync(filter, options);
    }

    public async IAsyncEnumerable<BsonDocument> FindByPage(IMongoCollection<BsonDocument> coll, BsonDocument filter, int pageIndex, int pageSize, IDictionary<string, SortEnum>? sort)
    {
        foreach (var doc in FindByPageSync(coll, filter, pageIndex, pageSize, sort))
            yield return doc;
    }

    public List<BsonDocument> FindByPageSync(IMongoCollection<BsonDocument> coll, BsonDocument filter, int pageIndex, int pageSize, IDictionary<string, SortEnum>? sort)
    {
        if (sort == null)
        {
            return coll.Find(filter).Skip(pageIndex * pageSize).Limit(pageSize).ToList();
        }
        else
        {
            var sortBson = new BsonDocument();
            foreach (var s in sort)
                sortBson.Add(s.Key, (int)s.Value);
            return coll.Find(filter).Sort(sortBson).Skip(pageIndex * pageSize).Limit(pageSize).ToList();
        }
    }

    public BsonDocument? FindById(IMongoCollection<BsonDocument> coll, string id)
    {
        if (!ObjectId.TryParse(id, out var objectId))
        {
            return null;
        }

        return coll.Find(Builders<BsonDocument>.Filter.Eq("_id", objectId)).FirstOrDefault();
    }

    public void Update(IMongoCollection<BsonDocument> coll, IDictionary<string, object> filter, IDictionary<string, object> newData)
    {
        var bsonFilter = new BsonDocument(filter);
        var update = new BsonDocument("$set", new BsonDocument(newData));
        coll.UpdateOne(bsonFilter, update);
    }

    public void UpdateById(IMongoCollection<BsonDocument> coll, string id, IDictionary<string, object> newData)
    {
        if (!ObjectId.TryParse(id, out var objectId))
        {
            return;
        }

        var filter = Builders<BsonDocument>.Filter.Eq("_id", objectId);
        var update = new BsonDocument("$set", new BsonDocument(newData));
        coll.UpdateOne(filter, update);
    }

    public void DropCollection(string dbName, string collName)
    {
        var collection = GetCollection(dbName, collName);
        if (collection != null)
        {
            collection.Database.DropCollection(collName);
        }
    }

    public bool IsExit(IMongoCollection<BsonDocument> collection, BsonDocument filter)
    {
        return collection.CountDocuments(filter) > 0;
    }

    public async Task<List<object>> Distinct(IMongoCollection<BsonDocument> collection, BsonDocument filter, string field)
    {
        var result = await collection.Distinct<BsonDocument>(field, filter).ToListAsync();
        return result.Select(d => (object)d.GetValue(field)).ToList();
    }

    public async IAsyncEnumerable<BsonDocument> Statistics(
        IMongoCollection<BsonDocument> coll,
        BsonDocument filter,
        string statisticField,
        IDictionary<string, string>? groupField,
        string statisticType)
    {
        foreach (var doc in StatisticsSync(coll, filter, statisticField, groupField, statisticType))
            yield return doc;
    }

    public List<BsonDocument> StatisticsSync(
        IMongoCollection<BsonDocument> coll,
        BsonDocument filter,
        string statisticField,
        IDictionary<string, string>? groupField,
        string statisticType)
    {
        var match = new BsonDocument("$match", filter);

        var groupFieldDoc = new BsonDocument();
        if (groupField != null)
        {
            foreach (var en in groupField)
                groupFieldDoc.Add(en.Key, en.Value);
        }

        BsonDocument group;
        if (groupFieldDoc == null || groupFieldDoc.ElementCount == 0)
            group = new BsonDocument("_id", BsonNull.Value)
                .Add(statisticType, new BsonDocument("$" + statisticType, statisticField));
        else
            group = new BsonDocument("_id", groupFieldDoc)
                .Add(statisticType, new BsonDocument("$" + statisticType, statisticField));

        var pipeline = new[] { match, new BsonDocument("$group", group) };

        return coll.Aggregate<BsonDocument>(pipeline).ToList();
    }
}
