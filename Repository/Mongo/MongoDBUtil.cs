using MongoDB.Driver;

namespace Domain.Infrastructure.Repository.Mongo;

public class MongoDBUtil
{
    private MongoClient? _client;
    private readonly string _host;
    private readonly int _port;
    private readonly string? _username;
    private readonly string? _password;
    
    public MongoDBUtil(string host, int port)
    {
        _host = host;
        _port = port;
    }
    
    public MongoDBUtil(string host, int port, string username, string password)
    {
        _host = host;
        _port = port;
        _username = username;
        _password = password;
    }
    
    public void CloseDB()
    {
        _client = null;
    }
    
    public void CreateClient()
    {
        var settings = MongoClientSettings.FromUrl(new MongoUrl($"mongodb://{_host}:{_port}"));
        _client = new MongoClient(settings);
    }
    
    public void CreateAuthenticatedClient()
    {
        if (_username == null || _password == null)
        {
            CreateClient();
            return;
        }
        
        var connectionString = $"mongodb://{_username}:{_password}@{_host}:{_port}";
        _client = new MongoClient(connectionString);
    }
    
    public IMongoDatabase? GetDatabase(string databaseName)
    {
        if (_client == null)
        {
            if (_username == null)
                CreateClient();
            else
                CreateAuthenticatedClient();
        }
        
        return _client?.GetDatabase(databaseName);
    }
}
