using Domain.Infrastructure.Model;

namespace Domain.Infrastructure.Finder;

public class ConditionGroupBuilder<T> : ConditionGroup<T> where T : DomainModel<T>
{
    private readonly ConditionNode _container;

    public ConditionGroupBuilder()
    {
        _container = new ConditionNode.AndNode();
    }

    public ConditionGroupBuilder(ConditionNode container)
    {
        _container = container;
    }

    private void AddChild(ConditionNode child)
    {
        if (_container is ConditionNode.AndNode andNode)
            andNode.Add(child);
        else if (_container is ConditionNode.OrNode orNode)
            orNode.Add(child);
    }

    public ConditionGroup<T> And(string name, object value)
    {
        return And(name, value, OType.eq);
    }

    public ConditionGroup<T> And(string name, object value, OType type)
    {
        if (string.IsNullOrEmpty(name))
            throw new DomainException("condition field name cannot be empty");
        AddChild(new ConditionNode.FieldCondition(name, new FindHelper.ValueType(value, type)));
        return this;
    }

    public ConditionGroup<T> Or(string name, object value)
    {
        return Or(name, value, OType.eq);
    }

    public ConditionGroup<T> Or(string name, object value, OType type)
    {
        if (string.IsNullOrEmpty(name))
            throw new DomainException("condition field name cannot be empty");
        AddChild(new ConditionNode.FieldCondition(name, new FindHelper.ValueType(value, type)));
        return this;
    }

    public ConditionGroup<T> And(IDictionary<string, object> map)
    {
        if (map != null)
        {
            foreach (var e in map)
                And(e.Key, e.Value, OType.eq);
        }
        return this;
    }

    public ConditionGroup<T> Or(IDictionary<string, object> map)
    {
        if (map != null)
        {
            foreach (var e in map)
                Or(e.Key, e.Value, OType.eq);
        }
        return this;
    }

    public ConditionGroup<T> AndGroup(Action<ConditionGroup<T>> sub)
    {
        if (sub == null)
            throw new DomainException("andGroup lambda cannot be null");
        var subContainer = new ConditionNode.AndNode();
        var subBuilder = new ConditionGroupBuilder<T>(subContainer);
        sub(subBuilder);
        AddChild(subContainer);
        return this;
    }

    public ConditionGroup<T> OrGroup(Action<ConditionGroup<T>> sub)
    {
        if (sub == null)
            throw new DomainException("orGroup lambda cannot be null");
        var subContainer = new ConditionNode.OrNode();
        var subBuilder = new ConditionGroupBuilder<T>(subContainer);
        sub(subBuilder);
        AddChild(subContainer);
        return this;
    }

    public ConditionNode Build()
    {
        return _container;
    }
}
