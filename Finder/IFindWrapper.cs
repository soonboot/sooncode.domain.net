using Domain.Infrastructure.Model;
using System.Collections.Generic;

namespace Domain.Infrastructure.Finder;

public interface IFindWrapper<T> : IFindAction<T> where T : DomainModel<T>
{
    IFindWrapper<T> And(string name, object value, OType oType);
    IFindWrapper<T> And(string name, object value);
    IFindWrapper<T> And(IDictionary<string, object> map);
    IFindWrapper<T> Or(string name, object value, OType oType);
    IFindWrapper<T> Or(string name, object value);
    IFindWrapper<T> Or(IDictionary<string, object> map);
    IFindWrapper<T> ByField(string name, object value, OType type);
    IFindWrapper<T> ByMap(IDictionary<string, object> map);
    IFindWrapper<T> AndGroup(Action<ConditionGroup<T>> sub);
    IFindWrapper<T> OrGroup(Action<ConditionGroup<T>> sub);
}
