using Domain.Infrastructure.Model;
using System.Collections.Generic;

namespace Domain.Infrastructure.Finder;

public interface IAddField<T> where T : DomainModel<T>
{
    IFindWrapper<T> Add(string name, object value);
    IFindWrapper<T> Add(string name, object value, OType type);
    IFindWrapper<T> Add(IDictionary<string, object> map);
}
