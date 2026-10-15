using Domain.Infrastructure.Monitor;

namespace Domain.Infrastructure.Model;

/// <summary>
/// 实体通知中心（兼容保留）。
/// <para>已迁移至 <see cref="Monitor.EntityNotice"/>，保留此类仅为二进制/源码兼容，新代码请直接使用 Monitor 命名空间。</para>
/// </summary>
[Obsolete("已迁移至 Monitor.EntityNotice，新代码请使用 Domain.Infrastructure.Monitor.EntityNotice。")]
public class EntityNotice : Monitor.EntityNotice
{
    /// <summary>兼容触发器（继承 Monitor 触发器）。</summary>
    [Obsolete("请使用 Monitor.EntityNotice.EntityNoticeTrigger。")]
    public new class EntityNoticeTrigger : Monitor.EntityNotice.EntityNoticeTrigger
    {
    }
}

/// <summary>
/// 领域事件通知中心（兼容保留）。
/// <para>已迁移至 <see cref="Monitor.EventNotice"/>，保留此类仅为二进制/源码兼容。</para>
/// </summary>
[Obsolete("已迁移至 Monitor.EventNotice，新代码请使用 Domain.Infrastructure.Monitor.EventNotice。")]
public class EventNotice : Monitor.EventNotice
{
    /// <summary>兼容触发器（继承 Monitor 触发器）。</summary>
    [Obsolete("请使用 Monitor.EventNotice.EventNoticeTrigger。")]
    public new class EventNoticeTrigger : Monitor.EventNotice.EventNoticeTrigger
    {
    }
}
