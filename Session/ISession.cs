using Domain.Infrastructure.Model;

namespace Domain.Infrastructure.Session;

/// <summary>
/// 会话契约，与 Java 版 <c>session.ISession</c> 对齐。
/// <para>.NET 规范：file-scoped namespace + XML docs；<c>Commit</c> 为主方法，<c>Complete</c> 为兼容别名。</para>
/// </summary>
public interface ISession
{
    /// <summary>登记实体到会话。</summary>
    void Add(Entity entity);

    /// <summary>提交会话（批量持久化 + 执行延迟函数 + 清理绑定）。</summary>
    void Commit();

    /// <summary>回滚会话（丢弃批量队列与延迟函数，解除绑定）。</summary>
    void Rollback();

    /// <summary>当前会话登记的实体。</summary>
    List<Entity> GetEntitys();

    /// <summary>注册提交成功回调。</summary>
    void OnSuccess(ISessionComplete function);

    /// <summary>注册延迟执行的会话函数（接口写法）。</summary>
    void SetSessionFunction(ISessionFunction function);

    /// <summary>注册延迟执行的会话函数（委托写法，便于 lambda）。</summary>
    void SetSessionFunction(Action func);
}

/// <summary><see cref="ISession"/> 函数式作用域扩展，与 Java 版默认方法 <c>run/call</c> 对齐。</summary>
public static class SessionExtensions
{
    /// <summary>函数式作用域：action 内对本会话操作，成功自动提交，异常自动回滚。</summary>
    public static void Run(this ISession session, Action<ISession> action)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(action);
        try
        {
            action(session);
            session.Commit();
        }
        catch
        {
            try { session.Rollback(); } catch { }
            throw;
        }
    }

    /// <summary>兼容别名：等价于 <c>Commit</c>（与旧 .NET 命名及 Java 版 commit 对齐）。</summary>
    [Obsolete("请使用 Commit（与 Java 版 ISession.commit 对齐）。")]
    public static void Complete(this ISession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.Commit();
    }

    /// <summary>带返回值的作用域。</summary>
    public static R Call<R>(this ISession session, Func<ISession, R> action)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(action);
        try
        {
            var result = action(session);
            session.Commit();
            return result;
        }
        catch
        {
            try { session.Rollback(); } catch { }
            throw;
        }
    }
}
