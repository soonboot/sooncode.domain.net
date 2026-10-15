using Domain.Infrastructure.Annotations;
using Domain.Infrastructure.Generic;
using Domain.Infrastructure.Monitor;
using Domain.Infrastructure.Utils;
using System.Linq;
using System.Reflection;

namespace Domain.Infrastructure.Model;

/// <summary>
/// 聚合基类, 也是事件溯源模式的聚合基类
/// </summary>
/// <typeparam name="T">聚合类型</typeparam>
public abstract class DomainModel<T> : Entity, IDomainModel where T : DomainModel<T>
{
    [IgnoreField]
    protected List<DomainEvent> Events { get; set; }
    public bool __stored__ = true;
    private int __version__ = 0;
    public int __startVersion__ = 0;

    /// <inheritdoc/>
    [IgnoreField]
    public bool StoredFlag { get => __stored__; set => __stored__ = value; }

    /// <inheritdoc/>
    [IgnoreField]
    public int StartVersion { get => __startVersion__; set => __startVersion__ = value; }
    [IgnoreField]
    private int _persistedEventCount;

    /// <summary>
    /// 构造器
    /// </summary>
    protected DomainModel()
    {
        Events = new List<DomainEvent>();
    }

    public void Replay(List<DomainEvent> events, int fromVersion, int toVersion)
    {
        if (events != null && events.Count > 0)
        {
            var sorted = events.OrderBy(e => e is ReplayEvent re ? re.FromVersion : 0).ToList();
            foreach (var @event in sorted)
            {
                Apply(@event);
            }
        }

        Causes(new ReplayEvent(Id, this, fromVersion, toVersion));
    }

    public void ReplayFromStore()
    {
        throw new DomainException("DomainModel.ReplayFromStore() must be overridden by subclass");
    }

    protected void When(ReplayEvent @event)
    {
        // 这里需要实现属性拷贝逻辑，类似于 Java 中的 EntityConvert.copyPropertys
        var source = @event.Data;
        var target = this;

        var sourceProperties = source.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead);

        var targetProperties = target.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite);

        foreach (var sourceProperty in sourceProperties)
        {
            var targetProperty = targetProperties.FirstOrDefault(p => p.Name == sourceProperty.Name);
            if (targetProperty != null && targetProperty.PropertyType.IsAssignableFrom(sourceProperty.PropertyType))
            {
                targetProperty.SetValue(target, sourceProperty.GetValue(source));
            }
        }
    }

    /// <summary>
    /// 应用事件, 会调用聚合对象的when方法, 通过传入不同的事件对象,调用不同的方法
    /// </summary>
    /// <param name="event">事件对象</param>
    private void Apply(DomainEvent @event)
    {
        var eventType = @event.GetType();
        var method = FindWhenMethod(GetType(), eventType);

        if (method == null)
        {
            @event.ProjectiveEntity((T)this);
            AddVersion();
            return;
        }

        try
        {
            method.Invoke(this, new[] { @event });
            AddVersion();
        }
        catch (DomainException)
        {
            throw;
        }
        catch (TargetInvocationException ex)
        {
            throw new DomainException($"执行相关对象的when方法时错误:{ex.InnerException?.Message}", innerException: (Exception?)ex.InnerException ?? ex);
        }
        catch (Exception ex)
        {
            throw new DomainException($"执行相关对象的when方法时错误: {ex.Message}", innerException: ex);
        }
    }

    /// <summary>
    /// 查找事件处理方法 <c>when</c>（大小写不敏感：兼容 Java 小写 <c>when</c> 与 .NET 规范大写 <c>When</c>）。
    /// 同一聚合存在多个重载时选择参数类型最具体（继承距离最近）者；找不到返回 null 由调用方回退投影。
    /// </summary>
    private static MethodInfo? FindWhenMethod(Type aggregateType, Type eventType)
    {
        ArgumentNullException.ThrowIfNull(aggregateType);
        ArgumentNullException.ThrowIfNull(eventType);
        MethodInfo? best = null;
        var bestDepth = int.MaxValue;
        for (var clz = aggregateType; clz is not null && clz != typeof(object); clz = clz.BaseType)
        {
            if (clz.IsGenericType && clz.GetGenericTypeDefinition() == typeof(DomainModel<>)) break;
            foreach (var m in clz.GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (!string.Equals(m.Name, "when", StringComparison.OrdinalIgnoreCase)) continue;
                var ps = m.GetParameters();
                if (ps.Length != 1) continue;
                var paramType = ps[0].ParameterType;
                if (!paramType.IsAssignableFrom(eventType)) continue;
                var depth = InheritanceDistance(eventType, paramType);
                if (depth < bestDepth)
                {
                    bestDepth = depth;
                    best = m;
                    if (depth == 0) return best;
                }
            }
        }

        return best;
    }

    private static int InheritanceDistance(Type derived, Type @base)
    {
        var depth = 0;
        for (var c = derived; c is not null && c != @base; c = c.BaseType) depth++;
        return depth;
    }

    /// <summary>
    /// 增加事件的版本号
    /// </summary>
    /// <returns>版本号</returns>
    protected int AddVersion()
    {
        __version__++;
        return __version__;
    }

    public void Add()
    {
        Causes(new BasicAddEvent(), this);
    }

    public void Update()
    {
        Causes(new BasicModifyEvent(), this);
    }

    public void Delete()
    {
        Causes(new BasicDeleteEvent(), this);
    }

    public void Replay(int toVersion)
    {
        Causes(new ReplayEvent(Id, this, 0, toVersion));
    }

    public void Replay(int fromVersion, int toVersion)
    {
        Causes(new ReplayEvent(Id, this, fromVersion, toVersion));
    }

    public void Replay()
    {
        Causes(new ReplayEvent(Id, this, 0, __version__ - 1));
    }

    /// <summary>
    /// 事件起因, 注册事件到事件列表中, 同时应用事件.
    /// </summary>
    /// <param name="event">事件对象</param>
    protected void Causes(DomainEvent @event)
    {
        if (string.IsNullOrEmpty(@event.Id))
        {
            @event.Id = Id;
        }

        Events.Add(@event);
        __stored__ = false;

        bool applySuccess = false;
        try
        {
            Apply(@event);
            applySuccess = true;
        }
        finally
        {
            if (!applySuccess)
            {
                Events.Remove(@event);
            }
        }

        var eventBootAttr = @event.GetType().GetCustomAttribute<EventBootAttribute>();
        var monitor = Monitor.Monitor.Instance;
        // 与 Java 版 causes 对齐：无 Monitor 时按无仓储维度捕获到 Batcher；
        // 有 Monitor 时走 StoreNotice（内部按仓储维度捕获 + 会话延期 + 单条落库），此处不提前返回。
        if (monitor is null)
        {
            var ft0 = eventBootAttr?.StoreFunc ?? FuncType.none;
            if (ft0 is FuncType.add or FuncType.modify or FuncType.delete)
                Batcher.BatcherCapture.TryCapture(this, ft0);
        }

        if (eventBootAttr != null && monitor != null)
        {
            var ft = eventBootAttr.StoreFunc;
            InvokeLifecycleHook(ft, true, @event);
            BeforeStore(@event);
            monitor.Store<T>(this, eventBootAttr);
            AfterStore(@event);
            InvokeLifecycleHook(ft, false, @event);
        }

        if (monitor != null)
        {
            // 批量作用域内延迟到 Execute/Commit 后再通知，与 Java 版 Batcher.current().deferEventNotice 对齐。
            if (Batcher.BatcherCapture.HasActiveScope)
                Batcher.BatcherCapture.TryDeferEventNotice(@event, this);
            else
                monitor.Notice<T>(@event, this);
        }
    }

    protected void Causes(DomainEvent @event, IDictionary<string, object> param)
    {
        @event.ConvertParam(param);
        Causes(@event);
    }

    protected void Causes(DomainEvent @event, Entity objParam)
    {
        @event.ConvertParam(objParam);
        Causes(@event);
    }

    protected void Causes(Type eventType)
    {
        var @event = GetEvent(eventType);
        Causes(@event);
    }

    protected void Causes(Type eventType, Entity objParam)
    {
        var @event = GetEvent(eventType);
        @event.ConvertParam(objParam);
        Causes(@event);
    }

    protected void Causes(Type eventType, IDictionary<string, object> param)
    {
        var @event = GetEvent(eventType);
        @event.ConvertParam(param);
        Causes(@event);
    }

    public IDictionary<string, object?> ToMap()
    {
        return EntityConvert.EntityToMap(this);
    }

    public void ToEntity(object targetObj)
    {
        EntityConvert.CopyProperties(this, targetObj, true);
    }

    private void InvokeLifecycleHook(FuncType funcType, bool before, DomainEvent @event)
    {
        if (before)
        {
            switch (funcType)
            {
                case FuncType.add:
                    BeforeAdd(@event); break;
                case FuncType.modify:
                    BeforeUpdate(@event); break;
                case FuncType.delete:
                    BeforeDelete(@event); break;
            }
        }
        else
        {
            switch (funcType)
            {
                case FuncType.add:
                    AfterAdd(@event); break;
                case FuncType.modify:
                    AfterUpdate(@event); break;
                case FuncType.delete:
                    AfterDelete(@event); break;
            }
        }
    }

    protected virtual void BeforeAdd(DomainEvent @event) { }
    protected virtual void AfterAdd(DomainEvent @event) { }
    protected virtual void BeforeUpdate(DomainEvent @event) { }
    protected virtual void AfterUpdate(DomainEvent @event) { }
    protected virtual void BeforeDelete(DomainEvent @event) { }
    protected virtual void AfterDelete(DomainEvent @event) { }
    protected virtual void BeforeStore(DomainEvent @event) { }
    protected virtual void AfterStore(DomainEvent @event) { }

    private DomainEvent GetEvent(Type eventType)
    {
        DomainEvent? @event = null;

        try
        {
            @event = (DomainEvent)Activator.CreateInstance(eventType)!;
        }
        catch (Exception ex)
        {
            throw new DomainException($"生成事件异常,不能为空的事件: {ex.Message}", innerException: ex);
        }

        if (@event == null)
        {
            throw new DomainException("生成事件异常,不能为空的事件");
        }

        return @event;
    }

    public int GetVersion()
    {
        return __version__;
    }

    internal void SetVersion(int version)
    {
        __version__ = version;
    }

    public IReadOnlyList<DomainEvent> GetEvents()
    {
        return Events.AsReadOnly();
    }

    /// <summary>当前尚未成功持久化的事件（防御性副本）。与 Java 版 <c>getPendingEvents</c> 对齐。</summary>
    public IReadOnlyList<DomainEvent> GetPendingEvents()
    {
        var start = Math.Min(_persistedEventCount, Events.Count);
        return Events.Skip(start).ToList().AsReadOnly();
    }

    /// <summary>在最终状态确定后为本次待持久化事件生成审计快照。与 Java 版 <c>preparePendingEventSnapshots</c> 对齐。</summary>
    public IReadOnlyList<DomainEvent> PreparePendingEventSnapshots()
    {
        var pending = GetPendingEvents();
        foreach (var e in pending)
            e?.ConvertModelSnapshot(this);
        return pending;
    }

    /// <summary>是否已全部落库（无待持久化事件）。与 Java 版 <c>isStored</c> 对齐。</summary>
    public bool IsStored() => __stored__ && _persistedEventCount >= Events.Count;

    /// <summary>仅在对应事件真实写入成功后由持久层调用。与 Java 版 <c>markStored</c> 对齐。</summary>
    public void MarkStored()
    {
        _persistedEventCount = Events.Count;
        __stored__ = true;
    }

    /// <summary>
    /// 仅在对应事件真正写入成功后推进持久化边界（必须为待持久化前缀）。与 Java 版 <c>markEventsPersisted</c> 对齐。
    /// 写入失败时不应调用，重试仍可拿到完整待持久化事件。
    /// </summary>
    public void MarkEventsPersisted(IReadOnlyList<DomainEvent>? persistedEvents)
    {
        if (persistedEvents is null || persistedEvents.Count == 0)
        {
            __stored__ = _persistedEventCount >= Events.Count;
            return;
        }

        var start = _persistedEventCount;
        if (start + persistedEvents.Count > Events.Count)
            throw new DomainException("持久化事件超出实体事件列表范围");
        for (var i = 0; i < persistedEvents.Count; i++)
        {
            if (!ReferenceEquals(Events[start + i], persistedEvents[i]))
                throw new DomainException("持久化事件不是实体待持久化事件的连续前缀");
        }

        _persistedEventCount += persistedEvents.Count;
        __stored__ = _persistedEventCount >= Events.Count;
    }
}
