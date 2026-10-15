namespace Domain.Infrastructure.Monitor;

/// <summary>
/// 领域事件通知中心，与 Java 版 <c>monitor.EventNotice</c> 对齐。
/// <para>.NET 规范：file-scoped namespace + XML docs + 强类型触发器。</para>
/// </summary>
public class EventNotice
{
    private readonly Dictionary<string, List<EventNoticeTrigger>> _events = new(StringComparer.Ordinal);

    /// <summary>按事件类型监听。</summary>
    public EventNoticeTrigger Listen(Type cla)
    {
        ArgumentNullException.ThrowIfNull(cla);
        return Listen(cla.FullName ?? cla.Name);
    }

    /// <summary>按事件类全名监听。</summary>
    public EventNoticeTrigger Listen(string className)
    {
        ArgumentNullException.ThrowIfNull(className);
        if (!_events.TryGetValue(className, out var triggers))
        {
            triggers = new List<EventNoticeTrigger>();
            _events[className] = triggers;
        }

        var trigger = new EventNoticeTrigger();
        triggers.Add(trigger);
        return trigger;
    }

    /// <summary>通知领域事件（非泛型核心路径）。</summary>
    public void Notice(Model.DomainEvent @event, Model.Entity entity)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(entity);
        var className = @event.GetType().FullName;
        if (className is null || !_events.TryGetValue(className, out var triggers) || triggers.Count == 0)
            return;

        foreach (var trigger in triggers)
        {
            if (trigger.TriggerFunc is null) continue;
            trigger.TriggerFunc(entity, @event);
        }
    }

    /// <summary>泛型通知（类型化实体）。</summary>
    public void Notice<T>(Model.DomainEvent @event, Model.DomainModel<T> entity) where T : Model.DomainModel<T>
        => Notice(@event, (Model.Entity)entity);

    /// <summary>领域事件触发器，与 Java 版 <c>EventNotice.Trigger</c> 对齐。</summary>
    public class EventNoticeTrigger
    {
        internal EventFunction? TriggerFunc { get; private set; }

        /// <summary>注册事件触发函数（委托写法，对齐 Java <c>IEventFunction</c> 签名）。</summary>
        public void Trigger(EventFunction func)
        {
            ArgumentNullException.ThrowIfNull(func);
            TriggerFunc = func;
        }

        /// <summary>注册事件触发函数（接口写法）。</summary>
        public void Trigger(IEventFunction func)
        {
            ArgumentNullException.ThrowIfNull(func);
            TriggerFunc = func.Run;
        }

        /// <summary>注册事件触发函数（模型写法，兼容旧 <c>Action&lt;EventFunctionModel&gt;</c>）。</summary>
        public void Trigger(Action<EventFunctionModel> func)
        {
            ArgumentNullException.ThrowIfNull(func);
            TriggerFunc = (entity, @event) => func(new EventFunctionModel { Entity = entity, Event = @event });
        }
    }
}
