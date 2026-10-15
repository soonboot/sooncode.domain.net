namespace Domain.Infrastructure.Model;

/// <summary>
/// 事件溯源聚合基类（兼容别名）。新代码请直接继承 <see cref="DomainModel{T}"/>。
/// 与 Java 版 <c>EventSourcedAggregate</c>（已废弃）对齐。
/// </summary>
/// <typeparam name="T">聚合类型。</typeparam>
[Obsolete("请直接使用 DomainModel<T>。")]
public abstract class EventSourcedAggregate<T> : DomainModel<T>
    where T : DomainModel<T>
{
}
