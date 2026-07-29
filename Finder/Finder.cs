using Domain.Infrastructure.Model;
using System.Collections.Generic;
using Domain.Infrastructure.Repository.Mongo;

namespace Domain.Infrastructure.Finder;

public class Finder<T> : IFind<T>, IFindAction<T>, IAggregate where T : DomainModel<T>
{
    private readonly FindWrapper<T> _findWrapper;
    private readonly IFind<T> _find;

    public Finder() : this(true)
    {
    }

    public Finder(bool removeIsDefault)
    {
        var findWrapper = new FindWrapper<T>(new FindRepository<T>(), removeIsDefault);
        _findWrapper = findWrapper;
        _find = new Find<T>(findWrapper);
    }

    public DomainModel<T>? ById(string id)
    {
        return _find.ById(id);
    }

    public IFindWrapper<T> ByField(string name, object value)
    {
        return _find.ByField(name, value);
    }

    public IFindWrapper<T> ByField(string name, object value, OType type)
    {
        return _find.ByField(name, value, type);
    }

    public IFindWrapper<T> ByMap(IDictionary<string, object> map)
    {
        return _find.ByMap(map);
    }

    public IFindWrapper<T> ByModel(DomainModel<T> model)
    {
        return _find.ByModel(model);
    }

    public T First(Sort sort)
    {
        return _findWrapper.First(sort);
    }

    public T First()
    {
        return _findWrapper.First();
    }

    public List<T> List(Sort sort)
    {
        return _findWrapper.List(sort);
    }

    public List<T> List()
    {
        return _findWrapper.List();
    }

    public IDictionary<string, T> Map(string fieldKey)
    {
        return _findWrapper.Map(fieldKey);
    }

    public List<T> Top(int num, Sort sort)
    {
        return _findWrapper.Top(num, sort);
    }

    public List<T> Top(int num)
    {
        return _findWrapper.Top(num);
    }

    public Page<T> Page(int pageSize, int pageIndex, Sort sort)
    {
        return _findWrapper.Page(pageSize, pageIndex, sort);
    }

    public Page<T> Page(int pageSize, int pageIndex)
    {
        return _findWrapper.Page(pageSize, pageIndex);
    }

    public long Count()
    {
        return _findWrapper.Count();
    }

    public IDictionary<string, long> Count(string[] groupField)
    {
        return _findWrapper.Count(groupField);
    }

    public List<TResult> Distinct<TResult>(string field)
    {
        return _findWrapper.Distinct<TResult>(field);
    }

    public IDictionary<string, double> Sum(string field, string[] groupField)
    {
        return _findWrapper.Sum(field, groupField);
    }

    public IDictionary<string, double> Avg(string field, string[] groupField)
    {
        return _findWrapper.Avg(field, groupField);
    }

    public IDictionary<string, double> Max(string field, string[] groupField)
    {
        return _findWrapper.Max(field, groupField);
    }

    public IDictionary<string, double> Min(string field, string[] groupField)
    {
        return _findWrapper.Min(field, groupField);
    }

    public IFindWrapper<T> AndGroup(Action<ConditionGroup<T>> sub)
    {
        return _findWrapper.AndGroup(sub);
    }

    public IFindWrapper<T> OrGroup(Action<ConditionGroup<T>> sub)
    {
        return _findWrapper.OrGroup(sub);
    }

    public object Group(string[] fields)
    {
        return _findWrapper.Group(fields);
    }
}
