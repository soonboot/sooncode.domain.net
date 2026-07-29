using Domain.Infrastructure.Model;
using System.Collections.Generic;

namespace Domain.Infrastructure.Finder;

internal class FindWrapper<T> : IFindWrapper<T>, IAddField<T>, IAggregate where T : DomainModel<T>
{
    private readonly FindHelper _fields;
    // private string[] _groupFields;
    private readonly IFindRepository<T> _repository;

    public FindWrapper(IFindRepository<T> repository, bool removeIsDefault)
    {
        _fields = new FindHelper(removeIsDefault);
        _repository = repository;
    }

    public IFindWrapper<T> And(string name, object value)
    {
        _fields.PutAnd(name, value);
        return this;
    }

    public IFindWrapper<T> And(IDictionary<string, object> map)
    {
        _fields.PutAnd(map);
        return this;
    }

    public IFindWrapper<T> Or(string name, object value, OType oType)
    {
        _fields.PutOr(name, value, oType);
        return this;
    }

    public IFindWrapper<T> Or(string name, object value)
    {
        _fields.PutOr(name, value);
        return this;
    }

    public IFindWrapper<T> Or(IDictionary<string, object> map)
    {
        _fields.PutOr(map);
        return this;
    }

    public IFindWrapper<T> ByField(string name, object value, OType type)
    {
        _fields.PutAnd(name,value, type);
        return this;
    }

    public IFindWrapper<T> ByMap(IDictionary<string, object> map)
    {
        _fields.PutAnd(map);
        return this;
    }

    public IFindWrapper<T> And(string name, object value, OType oType)
    {
        _fields.PutAnd(name, value, oType);
        return this;
    }

    public T First(Sort sort)
    {
        return _repository.First(_fields, sort);
    }

    public T First()
    {
        return _repository.First(_fields, null);
    }

    public List<T> List(Sort sort)
    {
        return _repository.List(_fields, sort);
    }

    public List<T> List()
    {
        return _repository.List(_fields, null);
    }

    public IDictionary<string, T> Map(string fieldKey)
    {
        return _repository.Map(_fields, fieldKey);
    }

    public List<T> Top(int num, Sort sort)
    {
        return _repository.Top(_fields, num, sort);
    }

    public List<T> Top(int num)
    {
        return _repository.Top(_fields, num, null);
    }

    public Page<T> Page(int pageSize, int pageIndex, Sort sort)
    {
        return _repository.Page(_fields, pageSize, pageIndex, sort);
    }

    public Page<T> Page(int pageSize, int pageIndex)
    {
        return _repository.Page(_fields, pageSize, pageIndex, null);
    }

    public long Count()
    {
        return _repository.Count(_fields);
    }

    public IDictionary<string, long> Count(string[] groupField)
    {
        return _repository.Count(_fields, groupField);
    }

    public List<TResult> Distinct<TResult>(string field)
    {
        return _repository.Distinct<TResult>(_fields, field);
    }

    public IDictionary<string, double> Sum(string field, string[] groupField)
    {
        return _repository.Sum(_fields, field, groupField);
    }

    public IDictionary<string, double> Avg(string field, string[] groupField)
    {
        return _repository.Avg(_fields, field, groupField);
    }

    public IDictionary<string, double> Max(string field, string[] groupField)
    {
        return _repository.Max(_fields, field, groupField);
    }

    public IDictionary<string, double> Min(string field, string[] groupField)
    {
        return _repository.Min(_fields, field, groupField);
    }

    public IFindWrapper<T> Add(string name, object value)
    {
        _fields.PutAnd(name, value);
        return this;
    }

    public IFindWrapper<T> Add(string name, object value, OType oType)
    {
        _fields.PutAnd(name, value, oType);
        return this;
    }

    public IFindWrapper<T> Add(IDictionary<string, object> map)
    {
        _fields.PutAnd(map);
        return this;
    }

    public IFindWrapper<T> AndGroup(Action<ConditionGroup<T>> sub)
    {
        var builder = new ConditionGroupBuilder<T>();
        sub(builder);
        var node = builder.Build();
        _fields.AddRootNode(node);
        return this;
    }

    public IFindWrapper<T> OrGroup(Action<ConditionGroup<T>> sub)
    {
        var builder = new ConditionGroupBuilder<T>(new ConditionNode.OrNode());
        sub(builder);
        var node = builder.Build();
        _fields.AddRootNode(node);
        return this;
    }

    public object Group(string[] fields)
    {
        _fields.SetGroup(fields);
        return this;
    }
}
