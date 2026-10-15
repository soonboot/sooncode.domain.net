using Domain.Infrastructure.Monitor;

namespace Domain.Infrastructure.Model;

/// <summary>
/// 事件监视器（兼容别名）。新代码请直接使用 <see cref="Monitor.Monitor"/>。
/// 与 Java 版 <c>EventMonitor</c>（已废弃）对齐。
/// </summary>
[Obsolete("请直接使用 Monitor.Monitor。")]
public sealed class EventMonitor
{
    private static readonly Lazy<EventMonitor> LazyInstance = new(() => new EventMonitor());

    private EventMonitor()
    {
        Monitor.Monitor.New();
    }

    /// <summary>获取单例。</summary>
    public static EventMonitor New() => LazyInstance.Value;
}
