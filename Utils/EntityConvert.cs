using System.Collections;
using System.Reflection;
using Domain.Infrastructure.Annotations;
using Domain.Infrastructure.Model;

namespace Domain.Infrastructure.Utils;

public static class EntityConvert
{
    public static IDictionary<string, object?> EntityToMap(object sourceObj)
    {
        return EntityToMap(sourceObj, new Dictionary<object, bool>(ReferenceEqualityComparer.Instance));
    }

    private static IDictionary<string, object?> EntityToMap(object sourceObj, Dictionary<object, bool> visiting)
    {
        if (sourceObj == null) return null;

        if (visiting.TryGetValue(sourceObj, out _))
        {
            var marker = new Dictionary<string, object?>
            {
                ["@cycleRef"] = sourceObj.GetType().FullName
            };
            return marker;
        }

        visiting[sourceObj] = true;

        try
        {
            var map = new Dictionary<string, object?>();
            var properties = sourceObj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead);

            foreach (var property in properties)
            {
                if (property.GetCustomAttribute<IgnoreFieldAttribute>() != null)
                    continue;

                var value = property.GetValue(sourceObj);
                map[property.Name] = ConvertValue(value, visiting);
            }

            return map;
        }
        finally
        {
            visiting.Remove(sourceObj);
        }
    }

    private static object? ConvertValue(object? value, Dictionary<object, bool> visiting)
    {
        if (value == null) return null;

        var type = value.GetType();

        if (IsSingleValue(value))
            return value;

        if (value is IDictionary dict)
        {
            var result = new Dictionary<string, object?>();
            foreach (var key in dict.Keys)
            {
                result[key.ToString()!] = ConvertValue(dict[key], visiting);
            }
            return result;
        }

        if (value is IList list)
        {
            var result = new List<object?>();
            foreach (var item in list)
            {
                result.Add(ConvertValue(item, visiting));
            }
            return result;
        }

        if (value is IEnumerable enumerable and not string)
        {
            var result = new List<object?>();
            foreach (var item in enumerable)
            {
                result.Add(ConvertValue(item, visiting));
            }
            return result;
        }

        if (value.GetType().IsSubclassOf(typeof(DomainModel<>).GetGenericTypeDefinition()) || value is ValueObject<object> || value.GetType().IsClass)
        {
            return EntityToMap(value, visiting);
        }

        return value;
    }

    private static bool IsSingleValue(object value)
    {
        return value is string
            || value is int or long or float or double or decimal
            || value is bool
            || value is short or byte or char
            || value is DateTime or DateTimeOffset or TimeSpan
            || value is Enum
            || value.GetType().IsPrimitive
            || value.GetType().IsValueType;
    }

    public static void MapToEntity(IDictionary<string, object> map, object target)
    {
        if (map == null || target == null) return;

        var targetProps = target.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite)
            .ToDictionary(p => p.Name);

        foreach (var kv in map)
        {
            if (!targetProps.TryGetValue(kv.Key, out var targetProp))
                continue;

            if (targetProp.GetCustomAttribute<IgnoreFieldAttribute>() != null)
                continue;

            var value = kv.Value;
            if (value == null) continue;

            try
            {
                if (targetProp.PropertyType.IsInstanceOfType(value))
                {
                    targetProp.SetValue(target, value);
                }
                else if (targetProp.PropertyType == typeof(string))
                {
                    targetProp.SetValue(target, value.ToString());
                }
                else if (value is IConvertible)
                {
                    targetProp.SetValue(target, Convert.ChangeType(value, targetProp.PropertyType));
                }
                else if (value is IDictionary dict
                    && (targetProp.PropertyType.IsClass && targetProp.PropertyType != typeof(string)))
                {
                    var nested = Activator.CreateInstance(targetProp.PropertyType);
                    var nestedDict = new Dictionary<string, object>();
                    foreach (var key in dict.Keys)
                        nestedDict[key.ToString()!] = dict[key]!;
                    MapToEntity(nestedDict, nested!);
                    targetProp.SetValue(target, nested);
                }
            }
            catch
            {
            }
        }
    }

    public static void CopyProperties(object source, object target, bool strict = true)
    {
        if (source == null || target == null) return;

        var sourceProps = source.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead);
        var targetProps = target.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite)
            .ToDictionary(p => p.Name);

        foreach (var sourceProp in sourceProps)
        {
            if (sourceProp.GetCustomAttribute<IgnoreFieldAttribute>() != null)
                continue;

            if (!targetProps.TryGetValue(sourceProp.Name, out var targetProp))
                continue;

            var value = sourceProp.GetValue(source);

            if (strict)
            {
                if (value == null) continue;
                if (value is string s && string.IsNullOrEmpty(s)) continue;
            }

            try
            {
                if (value == null || targetProp.PropertyType.IsInstanceOfType(value))
                {
                    targetProp.SetValue(target, value);
                }
                else if (value is IConvertible)
                {
                    targetProp.SetValue(target, Convert.ChangeType(value, targetProp.PropertyType));
                }
            }
            catch
            {
            }
        }
    }
}
