using Domain.Infrastructure.Model;

namespace Domain.Infrastructure.Finder;

public interface ConditionGroup<T> where T : DomainModel<T>
{
    ConditionGroup<T> And(string name, object value);
    ConditionGroup<T> And(string name, object value, OType type);
    ConditionGroup<T> Or(string name, object value);
    ConditionGroup<T> Or(string name, object value, OType type);
    ConditionGroup<T> And(IDictionary<string, object> map);
    ConditionGroup<T> Or(IDictionary<string, object> map);
    ConditionGroup<T> AndGroup(Action<ConditionGroup<T>> sub);
    ConditionGroup<T> OrGroup(Action<ConditionGroup<T>> sub);
    ConditionNode Build();
}
