using Domain.Infrastructure.Model;

namespace Domain.Infrastructure.Monitor;

/// <summary>领域事件监听函数模型（泛型），与 Java 版 <c>EventFunctionModel</c> 对齐。</summary>
/// <typeparam name="TEvent">事件类型。</typeparam>
/// <typeparam name="TEntity">实体类型。</typeparam>
public sealed class GenericEventFunctionModel<TEvent, TEntity>
    where TEvent : DomainEvent
    where TEntity : Entity
{
    /// <summary>触发的领域事件。</summary>
    public TEvent? Event { get; set; }

    /// <summary>关联实体。</summary>
    public TEntity? Entity { get; set; }
}

/// <summary>领域事件监听函数，与 Java 版 <c>IEventFunction</c> 对齐。</summary>
/// <param name="entity">关联实体。</param>
/// <param name="event">领域事件。</param>
public delegate void EventFunction(Entity entity, DomainEvent @event);

