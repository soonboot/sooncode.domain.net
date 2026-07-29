using Domain.Infrastructure.Model;

namespace Domain.Infrastructure.Repository.Mongo;

public class MongoConnection : IDBConnection
{
    private IMongoDBDao? _dao;
    private string? _dbName;

    public MongoConnection(string connectionString)
    {
    }

    public MongoConnection(string host, int port, string databaseName)
    {
        _dao = new MongoDBImpl(host, port);
        _dbName = databaseName;
        SetInstance(new MongoEventSourcingRepository(_dao, _dbName));
    }

    public MongoConnection(string host, int port, string databaseName, string username, string password)
    {
        _dao = new MongoDBImpl(host, port, username, password);
        _dbName = databaseName;
        SetInstance(new MongoEventSourcingRepository(_dao, _dbName));
    }

    internal MongoConnection(string host, int port, string databaseName, MongoEventSourcingRepository repository)
    {
        _dao = new MongoDBImpl(host, port);
        _dbName = databaseName;
        SetInstance(repository);
    }

    internal MongoConnection(string host, int port, string databaseName, string username, string password, MongoEventSourcingRepository repository)
    {
        _dao = new MongoDBImpl(host, port, username, password);
        _dbName = databaseName;
        SetInstance(repository);
    }

    private void SetInstance(MongoEventSourcingRepository repository)
    {
        MongoSingle.GetInstance().MongoDB = _dao;
        MongoSingle.GetInstance().DbName = _dbName;
        MongoSingle.GetInstance().Repository = repository;
    }

    public IEventSourcingRepository? GetRepository()
    {
        return MongoSingle.GetInstance().GetRepository();
    }
}