namespace Domain.Infrastructure.Monitor;

/// <summary>
/// 实体变更通知中心，与 Java 版 <c>monitor.EntityNotice</c> 对齐。
/// <para>.NET 规范：file-scoped namespace + XML docs + <c>ArgumentNullException.ThrowIfNull</c> + 强类型触发器。</para>
/// </summary>
public class EntityNotice
{
    private readonly Dictionary<string, List<EntityNoticeTrigger>> _entities = new(StringComparer.Ordinal);

    /// <summary>按实体类型监听。</summary>
    public EntityNoticeTrigger Listen(Type cla)
    {
        ArgumentNullException.ThrowIfNull(cla);
        return Listen(cla.FullName ?? cla.Name);
    }

    /// <summary>按实体类全名监听。</summary>
    public EntityNoticeTrigger Listen(string className)
    {
        ArgumentNullException.ThrowIfNull(className);
        if (!_entities.TryGetValue(className, out var triggers))
        {
            triggers = new List<EntityNoticeTrigger>();
            _entities[className] = triggers;
        }

        var trigger = new EntityNoticeTrigger();
        triggers.Add(trigger);
        return trigger;
    }

    /// <summary>通知实体变更（无旧实体）。</summary>
    public void Notice(Model.Entity entity, FuncType funcType)
    {
        ArgumentNullException.ThrowIfNull(entity);
        Notice(entity, null, funcType);
    }

    /// <summary>通知实体变更（带旧实体，用于 modify 对比）。</summary>
    public void Notice(Model.Entity entity, Model.Entity? oldEntity, FuncType funcType)
    {
        ArgumentNullException.ThrowIfNull(entity);
        var className = entity.GetType().FullName;
        if (className is null || !_entities.TryGetValue(className, out var triggers) || triggers.Count == 0)
            return;

        foreach (var trigger in triggers)
        {
            if (trigger.TriggerFuncs is null || !trigger.TriggerFuncs.TryGetValue(funcType, out var func))
                continue;
            var model = new FunctionModel
            {
                SourceEntity = oldEntity,
                TargetEntity = entity,
            };
            func(model);
        }
    }

    /// <summary>泛型通知（类型化实体），与 Java 版泛型 Notice 对齐。</summary>
    public void Notice<T>(Model.DomainModel<T> entity, FuncType funcType) where T : Model.DomainModel<T>
        => Notice((Model.Entity)entity, funcType);

    /// <summary>泛型通知（带旧实体）。</summary>
    public void Notice<T>(Model.DomainModel<T> entity, Model.DomainModel<T>? oldEntity, FuncType funcType) where T : Model.DomainModel<T>
        => Notice((Model.Entity)entity, oldEntity, funcType);

    /// <summary>实体监听触发器，与 Java 版 <c>EntityNotice.Trigger</c> 对齐。</summary>
    public class EntityNoticeTrigger
    {
        /// <summary>按操作类型注册的监听函数。</summary>
        public Dictionary<FuncType, Action<FunctionModel>> TriggerFuncs { get; } = new();

        /// <summary>注册新增监听（委托写法）。</summary>
        public EntityNoticeTrigger Add(Action<FunctionModel> func)
        {
            ArgumentNullException.ThrowIfNull(func);
            TriggerFuncs[FuncType.add] = func;
            return this;
        }

        /// <summary>注册新增监听（接口写法，兼容 Java 函数式接口）。</summary>
        public EntityNoticeTrigger Add(IEntityFunction func)
        {
            ArgumentNullException.ThrowIfNull(func);
            return Add(func.Run);
        }

        /// <summary>注册修改监听（委托写法）。</summary>
        public EntityNoticeTrigger Modify(Action<FunctionModel> func)
        {
            ArgumentNullException.ThrowIfNull(func);
            TriggerFuncs[FuncType.modify] = func;
            return this;
        }

        /// <summary>注册修改监听（接口写法）。</summary>
        public EntityNoticeTrigger Modify(IEntityFunction func)
        {
            ArgumentNullException.ThrowIfNull(func);
            return Modify(func.Run);
        }

        /// <summary>注册删除监听（委托写法）。</summary>
        public EntityNoticeTrigger Delete(Action<FunctionModel> func)
        {
            ArgumentNullException.ThrowIfNull(func);
            TriggerFuncs[FuncType.delete] = func;
            return this;
        }

        /// <summary>注册删除监听（接口写法）。</summary>
        public EntityNoticeTrigger Delete(IEntityFunction func)
        {
            ArgumentNullException.ThrowIfNull(func);
            return Delete(func.Run);
        }

        /// <summary>注册重放监听（委托写法）。</summary>
        public EntityNoticeTrigger Replay(Action<FunctionModel> func)
        {
            ArgumentNullException.ThrowIfNull(func);
            TriggerFuncs[FuncType.replay] = func;
            return this;
        }

        /// <summary>注册重放监听（接口写法）。</summary>
        public EntityNoticeTrigger Replay(IEntityFunction func)
        {
            ArgumentNullException.ThrowIfNull(func);
            return Replay(func.Run);
        }
    }
}
