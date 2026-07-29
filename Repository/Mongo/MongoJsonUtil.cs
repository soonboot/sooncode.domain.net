using System.Text.Json;
using MongoDB.Bson;

namespace Domain.Infrastructure.Repository.Mongo;

public class MongoJsonUtil
{
    public static BsonDocument ToJsonObject(Object obj)
    {
        return BsonDocument.Parse(JsonSerializer.Serialize(obj));
    }

    public static T? ConvertToObject<T>(BsonDocument bson) where T : class
    {
        return JsonSerializer.Deserialize<T>(bson.ToString());
    }
    public static object? ConvertToObject(BsonDocument bson, Type type)
    {
        return JsonSerializer.Deserialize(bson.ToString(), type);
    }
}
