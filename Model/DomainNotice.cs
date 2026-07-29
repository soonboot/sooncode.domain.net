using Domain.Infrastructure.Monitor;

namespace Domain.Infrastructure.Model;

public class EntityNotice
{
    private readonly Dictionary<string, List<EntityNoticeTrigger>> _entities = new ();

    public EntityNoticeTrigger Listen(Type cla)
    {
        return Listen(cla.FullName ?? cla.Name);
    }

    public EntityNoticeTrigger Listen(string className)
    {
        List<EntityNoticeTrigger> triggers;
        if (_entities.ContainsKey(className))
            triggers = _entities[className];
        else
            triggers = new List<EntityNoticeTrigger>();

        var trigger = new EntityNoticeTrigger();
        triggers.Add(trigger);
        _entities[className] = triggers;
        return trigger;
    }

    public void Notice<T>(DomainModel<T> entity, FuncType funcType) where T : DomainModel<T>
    {
        Notice(entity, null, funcType);
    }

    public void Notice<T>(DomainModel<T> entity, DomainModel<T>? oldEntity, FuncType funcType) where T : DomainModel<T>
    {
        var className = entity.GetType().FullName;
        if (className == null || !_entities.TryGetValue(className, out var triggers) || triggers.Count == 0)
            return;

        if (triggers.Count == 0) return;

        foreach (var trigger in triggers)
        {
            if (trigger.TriggerFuncs == null || !trigger.TriggerFuncs.TryGetValue(funcType, out var func)) continue;

            var model = new FunctionModel
            {
                SourceEntity = oldEntity,
                TargetEntity = entity
            };

            func(model);
        }
    }

    public class EntityNoticeTrigger
    {
        public Dictionary<FuncType, Action<FunctionModel>>? TriggerFuncs { get; set; } = new ();



        public EntityNoticeTrigger Add(Action<FunctionModel> func)
        {
            if (TriggerFuncs != null)
                TriggerFuncs[FuncType.add] = func;
            return this;
        }

        public EntityNoticeTrigger Modify(Action<FunctionModel> func)
        {
            if (TriggerFuncs != null)
                TriggerFuncs[FuncType.modify] = func;
            return this;
        }

        public EntityNoticeTrigger Delete(Action<FunctionModel> func)
        {
            if (TriggerFuncs != null)
                TriggerFuncs[FuncType.delete] = func;
            return this;
        }

        public EntityNoticeTrigger Replay(Action<FunctionModel> func)
        {
            if (TriggerFuncs != null)
                TriggerFuncs[FuncType.replay] = func;
            return this;
        }
    }
}

public class EventNotice
{
    private readonly Dictionary<string, List<EventNoticeTrigger>> _events = new ();

    public EventNoticeTrigger Listen(Type cla)
    {
        return Listen(cla.FullName ?? cla.Name);
    }

    public EventNoticeTrigger Listen(string className)
    {
        if (!_events.TryGetValue(className, out var triggers))
            triggers = new List<EventNoticeTrigger>();
        var trigger = new EventNoticeTrigger();
        triggers.Add(trigger);
        _events[className] = triggers;
        return trigger;
    }

    public void Notice<T>(DomainEvent @event, DomainModel<T> entity) where T : DomainModel<T>
    {
        var className = @event.GetType().FullName;
        if (className == null || !_events.TryGetValue(className, out var triggers)) return;
        if (triggers.Count == 0) return;

        foreach (var trigger in triggers)
        {
            if (trigger.func == null) continue;
            trigger.func(new EventFunctionModel
            {
                Event = @event,
                Entity = entity
            });
        }
    }
    public class EventNoticeTrigger
    {
        public Action<EventFunctionModel> func;

        public void Trigger(Action<EventFunctionModel> _func)
        {
            func = _func;
        }
    }
}
