using Domain.Infrastructure.Model;

namespace Domain.Infrastructure.Session;

/// <summary>
/// 会话管理器，与 Java 版 <c>SessionManager</c> 对齐。
/// <para>.NET 规范：使用 <see cref="AsyncLocal{T}"/> 实现异步安全的当前作用域（替代 Java ThreadLocal）。</para>
/// </summary>
public static class SessionManager
{
    private static readonly AsyncLocal<Dictionary<string, ISession>?> Holder = new();

    private static Dictionary<string, ISession> Map => Holder.Value ??= new Dictionary<string, ISession>(StringComparer.Ordinal);

    /// <summary>获取实体绑定的会话，不存在返回 null。</summary>
    public static ISession? Get(Entity? model)
    {
        if (model is null) return null;
        return Map.TryGetValue(GetKey(model), out var session) ? session : null;
    }

    /// <summary>绑定实体到会话。</summary>
    public static void Put(Entity? model, ISession? session)
    {
        if (model is null || session is null) return;
        Map[GetKey(model)] = session;
    }

    /// <summary>移除实体绑定；映射为空时释放 Holder，避免泄漏。</summary>
    public static void Remove(Entity? model)
    {
        if (model is null || Holder.Value is null) return;
        Map.Remove(GetKey(model));
        if (Map.Count == 0) Holder.Value = null;
    }

    /// <summary>实体是否处于会话中。</summary>
    public static bool Contains(Entity? model)
    {
        if (model is null || Holder.Value is null) return false;
        return Map.ContainsKey(GetKey(model));
    }

    /// <summary>当前上下文是否存在活动会话。</summary>
    public static bool HasActiveSession() => Holder.Value is { Count: > 0 };

    /// <summary>清空当前上下文的全部会话绑定。</summary>
    public static void Clear() => Holder.Value = null;

    private static string GetKey(Entity model) => $"{model.GetType().FullName}_{model.Id}";
}
