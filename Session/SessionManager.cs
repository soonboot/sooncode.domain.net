using Domain.Infrastructure.Model;

namespace Domain.Infrastructure.Session;

public static class SessionManager
{
    private static readonly Dictionary<string, ISession> _sessions = new Dictionary<string, ISession>();

    public static ISession? Get(Entity model)
    {
        var key = GetKey(model);
        return _sessions.GetValueOrDefault(key);
    }

    public static void Put(Entity model, ISession session)
    {
        var key = GetKey(model);
        _sessions[key] = session;
    }

    public static void Remove(Entity model)
    {
        var key = GetKey(model);
        _sessions.Remove(key);
    }

    public static bool Contains(Entity model)
    {
        var key = GetKey(model);
        return _sessions.ContainsKey(key);
    }

    private static string GetKey(Entity model)
    {
        return $"{model.GetType().FullName}_{model.Id}";
    }
}
