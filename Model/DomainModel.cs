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
public abstract class DomainModel<T> : Entity where T : DomainModel<T>
{
    [IgnoreField]
    protected List<DomainEvent> Events { get; set; }
    public bool __stored__  = true;
    private int __version__ =0;
    public int __startVersion__  = 0;

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
            throw new DomainException($"执行相关对象的when方法时错误:{ex.InnerException?.Message}", innerException: ex.InnerException);
        }
        catch (Exception ex)
        {
            throw new DomainException($"执行相关对象的when方法时错误: {ex.Message}", innerException: ex);
        }
    }

    private static MethodInfo? FindWhenMethod(Type aggregateType, Type eventType)
    {
        var clz = aggregateType;

        while (clz != null && clz != typeof(DomainModel<>))
        {
            var whenMethod = clz.GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == "when" && m.GetParameters().Length == 1);

            if (whenMethod != null)
            {
                var paramType = whenMethod.GetParameters()[0].ParameterType;
                if (paramType.IsAssignableFrom(eventType))
                {
                    return whenMethod;
                }
            }

            clz = clz.BaseType;
        }

        return null;
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
}
