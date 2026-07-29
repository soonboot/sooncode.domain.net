using MongoDB.Driver;
using MongoDB.Bson;
using Domain.Infrastructure.Model;

namespace Domain.Infrastructure.Repository.Mongo;

public class MongoSingle
{
    private MongoSingle() { }
    
    public IMongoDBDao? MongoDB { get; set; }
    public string? DbName { get; set; }
    public IEventSourcingRepository? Repository { get; set; }
    
    private static MongoSingle? _instance;
    
    public static MongoSingle GetInstance()
    {
        return New();
    }
    
    public IEventSourcingRepository? GetRepository()
    {
        return Repository;
    }
    
    public static MongoSingle New()
    {
        if (_instance == null)
        {
            _instance = new MongoSingle();
        }
        return _instance;
    }
    
    public IMongoDatabase? GetMongoDB()
    {
        return MongoDB?.GetDb(DbName ?? "");
    }
    
    public IMongoCollection<BsonDocument>? GetSnapshotCollection()
    {
        if (MongoDB == null || string.IsNullOrEmpty(DbName)) return null;
        return MongoDB.GetCollection(DbName, "eventSnapshot");
    }
}
