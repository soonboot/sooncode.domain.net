using MongoDB.Bson;
using MongoDB.Driver;

namespace Domain.Infrastructure.Repository.Mongo;

public interface IMongoDBDao
{
    IMongoDatabase? GetDb(string dbName);
    IMongoCollection<BsonDocument> GetCollection(string dbName, string collectionName);
    bool AddOne(IMongoCollection<BsonDocument> collection, IDictionary<string, object> map);
    bool AddMany(IMongoCollection<BsonDocument> collection, List<IDictionary<string, object>> list);
    long Delete(IMongoCollection<BsonDocument> collection, IDictionary<string, object> filter);
    long DeleteById(IMongoCollection<BsonDocument> coll, string id);
    BsonDocument? FindFirst(IMongoCollection<BsonDocument> coll, BsonDocument filter);
    BsonDocument? FindFirst(IMongoCollection<BsonDocument> coll, BsonDocument filter, IDictionary<string, SortEnum>? sort);
    long Count(IMongoCollection<BsonDocument> coll, BsonDocument filter);
    IAsyncEnumerable<BsonDocument> FindTop(IMongoCollection<BsonDocument> coll, BsonDocument filter, IDictionary<string, SortEnum>? sort, int num);
    List<BsonDocument> FindTopSync(IMongoCollection<BsonDocument> coll, BsonDocument filter, IDictionary<string, SortEnum>? sort, int num);
    IAsyncEnumerable<BsonDocument> Find(IMongoCollection<BsonDocument> coll, BsonDocument filter, IDictionary<string, SortEnum>? sort);
    List<BsonDocument> FindSync(IMongoCollection<BsonDocument> coll, BsonDocument filter, IDictionary<string, SortEnum>? sort);
    Task<IAsyncCursor<BsonDocument>> FindAsync(IMongoCollection<BsonDocument> coll, FilterDefinition<BsonDocument> filter, SortDefinition<BsonDocument>? sort = null);
    Task<IAsyncCursor<BsonDocument>> FindByPageAsync(IMongoCollection<BsonDocument> coll, FilterDefinition<BsonDocument> filter, int pageIndex, int pageSize, SortDefinition<BsonDocument>? sort = null);
    IAsyncEnumerable<BsonDocument> FindByPage(IMongoCollection<BsonDocument> coll, BsonDocument filter, int pageIndex, int pageSize, IDictionary<string, SortEnum>? sort);
    List<BsonDocument> FindByPageSync(IMongoCollection<BsonDocument> coll, BsonDocument filter, int pageIndex, int pageSize, IDictionary<string, SortEnum>? sort);
    BsonDocument? FindById(IMongoCollection<BsonDocument> coll, string id);
    void Update(IMongoCollection<BsonDocument> coll, IDictionary<string, object> filter, IDictionary<string, object> newData);
    void UpdateById(IMongoCollection<BsonDocument> coll, string id, IDictionary<string, object> newData);
    void DropCollection(string dbName, string collName);
    bool IsExit(IMongoCollection<BsonDocument> collection, BsonDocument filter);
    Task<List<object>> Distinct(IMongoCollection<BsonDocument> collection, BsonDocument filter, string field);
    IAsyncEnumerable<BsonDocument> Statistics(IMongoCollection<BsonDocument> coll, BsonDocument filter, string statisticField, IDictionary<string, string>? groupField, string statisticType);
    List<BsonDocument> StatisticsSync(IMongoCollection<BsonDocument> coll, BsonDocument filter, string statisticField, IDictionary<string, string>? groupField, string statisticType);
}
