using Domain.Infrastructure.Model;
using System.Reflection;

namespace Domain.Infrastructure.Finder;

internal class Find<T> : IFind<T> where T : DomainModel<T>
{
    private readonly IDomainRepository? _repository;
    private readonly IAddField<T> _addField;

    internal Find(IAddField<T> addField)
    {
        _repository = Monitor.Monitor.Instance.GetDomainRepository();
        _addField = addField;
    }

    public DomainModel<T>? ById(string id)
    {
        return _repository!.FindById<T>(id);
    }

    public IFindWrapper<T> ByField(string name, object value)
    {
        return _addField.Add(name, value);
    }

    public IFindWrapper<T> ByField(string name, object value, OType type)
    {
        return _addField.Add(name, value, type);
    }

    public IFindWrapper<T> ByMap(IDictionary<string, object> map)
    {
        return _addField.Add(map);
    }

    public IFindWrapper<T> ByModel(DomainModel<T> model)
    {
        if (model == null) return _addField.Add(null);

        var properties = model.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead);

        var map = new Dictionary<string, object>();
        foreach (var p in properties)
        {
            var value = p.GetValue(model);
            if (value == null) continue;

            if (p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(ValueObject<>))
            {
                var valueObjectType = typeof(ValueObject<>).MakeGenericType(p.PropertyType.GetGenericArguments()[0]);
                var getValueMethod = valueObjectType.GetMethod("GetValue");
                map[p.Name] = getValueMethod?.Invoke(value, null);
            }
            else
            {
                map[p.Name] = value;
            }
        }

        return _addField.Add(map);
    }
}
