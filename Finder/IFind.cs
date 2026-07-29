using Domain.Infrastructure.Model;
using System.Collections.Generic;

namespace Domain.Infrastructure.Finder;

public interface IFind<T> where T : DomainModel<T>
{
    IFindWrapper<T> ByField(string name, object value);
    IFindWrapper<T> ByField(string name, object value, OType type);
    IFindWrapper<T> ByMap(IDictionary<string, object> map);
    IFindWrapper<T> ByModel(DomainModel<T> model);
    DomainModel<T>? ById(string id);
}
