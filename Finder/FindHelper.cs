namespace Domain.Infrastructure.Finder;

public class FindHelper
{
    private IDictionary<string, List<ValueType>> _andMap = new Dictionary<string, List<ValueType>>();
    private IDictionary<string, List<ValueType>> _orMap = new Dictionary<string, List<ValueType>>();
    private string[] _group = Array.Empty<string>();
    private bool _removeIsDefault;
    private ConditionNode? _rootCondition;

    public FindHelper(bool removeIsDefault)
    {
        _removeIsDefault = removeIsDefault;
    }

    public void PutAnd(string key, object value, OType? type)
    {
        if (_removeIsDefault && EqDefault(value)) return;

        if (!_andMap.ContainsKey(key))
        {
            _andMap[key] = new List<ValueType>();
        }

        if (type == null && value is string)
        {
            type = OType.contains;
        }

        _andMap[key].Add(new ValueType(value, type));
    }

    public void PutAnd(IDictionary<string, object>? map)
    {
        if (map == null || map.Count == 0) return;

        foreach (var m in map)
        {
            PutAnd(m.Key, m.Value, null);
        }
    }

    public void PutAnd(string key, object value)
    {
        PutAnd(key, value, null);
    }

    public void PutOr(string key, object value, OType? type)
    {
        if (_removeIsDefault && EqDefault(value)) return;

        if (!_orMap.ContainsKey(key))
        {
            _orMap[key] = new List<ValueType>();
        }

        if (type == null && value is string)
        {
            type = OType.contains;
        }

        _orMap[key].Add(new ValueType(value, type));
    }

    public void PutOr(IDictionary<string, object>? map)
    {
        if (map == null || map.Count == 0) return;

        foreach (var m in map)
        {
            PutOr(m.Key, m.Value, null);
        }
    }

    public void PutOr(string key, object value)
    {
        PutOr(key, value, null);
    }

    public void SetGroup(string[] group)
    {
        _group = group;
    }

    public string[] GetGroup()
    {
        return _group;
    }

    public IDictionary<string, List<ValueType>> GetAndMap()
    {
        return _andMap;
    }

    public IDictionary<string, List<ValueType>> GetOrMap()
    {
        return _orMap;
    }

    public void AddRootNode(ConditionNode node)
    {
        _rootCondition = node;
    }

    public ConditionNode? GetRootCondition()
    {
        return _rootCondition;
    }

    private bool EqDefault(object? o)
    {
        if (o == null) return true;

        if (o is string s && s == "")
            return true;
        else if (o is int i && i == 0)
            return true;
        else if (o is long l && l == 0)
            return true;
        else if (o is float f && Math.Abs(f) < 0.0001)
            return true;
        else if (o is double d && Math.Abs(d) < 0.0001)
            return true;

        return false;
    }

    public class ValueType
    {
        public object Value { get; set; }
        public OType? Type { get; set; }

        public ValueType(object value, OType? type)
        {
            Value = value;
            Type = type;
        }
    }
}
