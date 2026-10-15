using Domain.Infrastructure.Model;

namespace Domain.Infrastructure.Monitor;

/// <summary>领域事件监听函数接口，与 Java 版 <c>monitor.IEventFunction</c> 对齐（函数式接口）。</summary>
public interface IEventFunction
{
    /// <summary>执行事件监听。</summary>
    /// <param name="entity">关联实体。</param>
    /// <param name="event">领域事件。</param>
    void Run(Entity entity, DomainEvent @event);
}
