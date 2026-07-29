using System.Collections.Concurrent;
using System.Reflection;
using Domain.Infrastructure.Annotations;
using Domain.Infrastructure.Utils;

namespace Domain.Infrastructure.Model;

public abstract class DomainEvent : IEquatable<DomainEvent>
{
    private static readonly ConcurrentDictionary<Type, Dictionary<string, PropertyInfo>> PropertiesCache = new();

    public string Id { get; set; }
    public IDictionary<string, object> DynamicParams { get; set; } = new Dictionary<string, object>();

    private Dictionary<string, PropertyInfo> Properties => PropertiesCache.GetOrAdd(GetType(), type =>
    {
        var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite)
            .OrderBy(p => p.Name)
            .ToList();

        var map = new Dictionary<string, PropertyInfo>();
        foreach (var property in props)
        {
            if (property.Name != nameof(DynamicParams) && property.Name != nameof(Id))
                map[property.Name] = property;
        }
        return map;
    });

    protected DomainEvent() { }

    public DomainEvent(string aggregateId)
    {
        Id = aggregateId;
    }

    public DomainEvent(string aggregateId, Entity obj) : this(obj)
    {
        Id = aggregateId;
    }

    public DomainEvent(string aggregateId, IDictionary<string, object> map) : this(map)
    {
        Id = aggregateId;
    }

    public DomainEvent(Entity obj)
    {
        ConvertParam(obj);
    }

    public DomainEvent(IDictionary<string, object> map)
    {
        ConvertParam(map);
    }

    private bool CheckParam(string fieldName, IDictionary<string, object> map)
    {
        var propertyInfo = GetType().GetProperty(fieldName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        if (propertyInfo != null)
        {
            if (propertyInfo.GetCustomAttribute<IgnoreFieldAttribute>() != null)
            {
                map.Remove(fieldName);
                return true;
            }

            if (propertyInfo.GetCustomAttribute<NotRequiredAttribute>() != null)
                return true;
        }

        var property = Properties.GetValueOrDefault(fieldName);
        if (property != null && property.GetCustomAttribute<NotRequiredAttribute>() != null)
            return true;

        if (!map.ContainsKey(fieldName))
            throw new DomainException($"缺少参数：{fieldName}");

        return true;
    }

    public void ConvertParam(Entity obj)
    {
        var map = EntityConvert.EntityToMap(obj);
        var dict = new Dictionary<string, object>();
        foreach (var kv in map)
        {
            if (kv.Value != null)
                dict[kv.Key] = kv.Value;
        }
        ConvertParam(dict);
    }

    public void ConvertParam(IDictionary<string, object> map)
    {
        var eventBootAttr = GetType().GetCustomAttribute<EventBootAttribute>();
        if (eventBootAttr != null)
        {
            foreach (var p in eventBootAttr.Params)
                CheckParam(p, map);
        }

        foreach (var kv in Properties)
            CheckParam(kv.Key, map);

        foreach (var kv in map)
            Set(kv.Key, kv.Value);
    }

    public void ProjectiveEntity<T>(DomainModel<T> en) where T : DomainModel<T>
    {
        var entityProps = en.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite)
            .ToList();

        foreach (var prop in entityProps)
        {
            if (prop.Name == nameof(Entity.Id)) continue;
            if (!HasField(prop.Name)) continue;
            var value = Get(prop.Name);
            if (value != null)
                SetPropertyValue(en, prop, value);
        }

        if (DynamicParams.Count > 0)
        {
            foreach (var dp in DynamicParams)
            {
                var targetProp = entityProps.FirstOrDefault(p => p.Name == dp.Key);
                if (targetProp != null && targetProp.CanWrite && dp.Value != null)
                    SetPropertyValue(en, targetProp, dp.Value);
            }
        }
    }

    public void Set(string fieldName, object value)
    {
        if (!Properties.TryGetValue(fieldName, out var property))
        {
            var eventBootAttr = GetType().GetCustomAttribute<EventBootAttribute>();
            if (eventBootAttr != null)
            {
                if (eventBootAttr.Params.Contains(fieldName))
                {
                    DynamicParams[fieldName] = value;
                    return;
                }

                if (eventBootAttr.KeepAll)
                {
                    DynamicParams[fieldName] = value;
                    return;
                }
            }
            return;
        }

        if (value == null) return;
        SetPropertyValue(this, property, value);
    }

    private void SetPropertyValue(object en, PropertyInfo property, object value)
    {
        var propertyType = property.PropertyType;

        try
        {
            if (!property.CanWrite || value == null) return;

            if (propertyType.IsInstanceOfType(value))
            {
                property.SetValue(en, value);
                return;
            }

            if (typeof(System.Collections.IDictionary).IsAssignableFrom(propertyType) && value is System.Collections.IDictionary)
            {
                property.SetValue(en, value);
                return;
            }

            if (typeof(System.Collections.IList).IsAssignableFrom(propertyType) && value is System.Collections.IList)
            {
                property.SetValue(en, value);
                return;
            }

            if (value is System.Collections.IDictionary dictValue
                && (propertyType.IsSubclassOf(typeof(DomainModel<>).GetGenericTypeDefinition())
                    || propertyType.IsSubclassOf(typeof(ValueObject<>).GetGenericTypeDefinition())
                    || propertyType == typeof(Entity)))
            {
                var nested = Activator.CreateInstance(propertyType);
                var nestedDict = new Dictionary<string, object>();
                foreach (var key in dictValue.Keys)
                    nestedDict[key.ToString()!] = dictValue[key];
                EntityConvert.MapToEntity(nestedDict, nested!);
                property.SetValue(en, nested);
                return;
            }

            if (value != null && !string.IsNullOrWhiteSpace(value.ToString()))
                property.SetValue(en, Convert.ChangeType(value, propertyType));
        }
        catch (Exception ex)
        {
            throw new DomainException($"对象转换失败：{value} to {property.Name}", innerException: ex);
        }
    }

    public object Get(string fieldName)
    {
        if (Properties.TryGetValue(fieldName, out var property))
            return property.GetValue(this);

        DynamicParams.TryGetValue(fieldName, out var value);
        return value;
    }

    public bool HasField(string fieldName)
    {
        return Properties.ContainsKey(fieldName) || DynamicParams.ContainsKey(fieldName);
    }

    public bool Equals(DomainEvent? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return Id == other.Id;
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((DomainEvent)obj);
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }
}
