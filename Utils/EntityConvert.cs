using System.Collections;
using System.ComponentModel;
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

    /// <summary>
    /// 按属性实际类型（含 <c>List&lt;ValueObject&gt;</c> 泛型元素类型）转换值。
    /// 事件参数与实体反序列化都必须经过同一套转换，否则泛型集合会被原样写成 <c>List&lt;Dictionary&gt;</c>。
    /// 与 Java 版 <c>convertPropertyValue</c> 对齐。
    /// </summary>
    public static object? ConvertPropertyValue(object? value, PropertyDescriptor property)
    {
        ArgumentNullException.ThrowIfNull(property);
        if (value is null) return null;
        var targetType = property.PropertyType;
        Type? genericType = property.ComponentType == property.PropertyType
            ? null
            : ResolveGenericType(property);
        return ConvertMapValueToTarget(value, targetType, genericType);
    }

    private static Type? ResolveGenericType(PropertyDescriptor property)
    {
        // 优先读方法泛型返回/参数，与 Java 版 getReadMethod/getWriteMethod 逻辑对齐。
        var prop = property.ComponentType.GetProperty(property.Name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (prop is null) return null;
        if (prop.GetGetMethod() is { } getter) return getter.ReturnType;
        var setter = prop.GetSetMethod(true);
        var ps = setter?.GetParameters();
        if (ps is { Length: 1 }) return ps[0].ParameterType;
        return prop.PropertyType;
    }

    private static object? ConvertMapValueToTarget(object value, Type targetType, Type? genericType)
    {
        ArgumentNullException.ThrowIfNull(targetType);
        if (targetType == typeof(string)) return value.ToString();
        if (IsSingleValue(value) || IsSingleValueType(value.GetType()))
            return BaseTypeConvert.ConvertValue(value, targetType);
        if (typeof(IDictionary).IsAssignableFrom(targetType) && value is IDictionary) return value;
        if (targetType.IsArray) return ConvertToArray(value, targetType, genericType);
        if (value.GetType().IsArray && typeof(IEnumerable).IsAssignableFrom(targetType))
            return ConvertArrayToCollection((Array)value, targetType, genericType);
        if (typeof(IList).IsAssignableFrom(targetType) && value is IEnumerable seq)
        {
            var elementType = CollectionElementType(genericType);
            var result = NewListForTarget(targetType, 0);
            foreach (var item in seq) result.Add(ConvertGenericValue(item, elementType));
            return result;
        }
        if (typeof(IEnumerable).IsAssignableFrom(targetType) && value is IEnumerable seq2 && targetType != typeof(string))
        {
            var elementType = CollectionElementType(genericType);
            var result = NewListForTarget(targetType, 0);
            foreach (var item in seq2) result.Add(ConvertGenericValue(item, elementType));
            return result;
        }
        if (targetType.IsInstanceOfType(value)) return value;
        if (IsDomainType(value.GetType()))
        {
            if (value is IDictionary<string, object> map)
            {
                var nested = ReflectionHelper.NewInstance(targetType);
                MapToEntity(new Dictionary<string, object>(map.ToDictionary(kv => kv.Key, kv => (object)kv.Value!)), nested);
                return nested;
            }
            if (targetType.IsInstanceOfType(value)) return value;
            var snapshot = EntityToMap(value);
            var cross = ReflectionHelper.NewInstance(targetType);
            MapToEntity(new Dictionary<string, object>(snapshot.ToDictionary(kv => kv.Key, kv => (object)kv.Value!)), cross);
            return cross;
        }
        if (IsDomainType(targetType) && value is IDictionary<string, object> targetMap)
        {
            var nested = ReflectionHelper.NewInstance(targetType);
            MapToEntity(new Dictionary<string, object>(targetMap.ToDictionary(kv => kv.Key, kv => (object)kv.Value!)), nested);
            return nested;
        }
        return value;
    }

    private static object ConvertToArray(object value, Type targetType, Type? genericType)
    {
        var elementType = targetType.GetElementType() ?? typeof(object);
        var items = value is IEnumerable seq && value is not string
            ? seq.Cast<object?>().ToList()
            : new List<object?> { value };
        var array = Array.CreateInstance(elementType, items.Count);
        for (var i = 0; i < items.Count; i++)
            array.SetValue(items[i] is null ? null : ConvertGenericValue(items[i], elementType), i);
        return array;
    }

    private static object ConvertArrayToCollection(Array value, Type targetType, Type? genericType)
    {
        var result = NewListForTarget(targetType, value.Length);
        var elementType = CollectionElementType(genericType);
        foreach (var item in value) result.Add(ConvertGenericValue(item, elementType));
        return result;
    }

    private static IList NewListForTarget(Type targetType, int capacity)
    {
        if (!targetType.IsInterface && !targetType.IsAbstract)
        {
            try
            {
                if (Activator.CreateInstance(targetType) is IList list) return list;
            }
            catch { }
        }
        return new List<object?>(capacity);
    }

    private static object? ConvertGenericValue(object? value, Type? targetType)
    {
        if (value is null || targetType is null || targetType == typeof(object)) return value;
        if (targetType.IsInstanceOfType(value)) return value;
        if (value is IDictionary<string, object> map && IsDomainType(targetType))
        {
            var nested = ReflectionHelper.NewInstance(targetType);
            MapToEntity(new Dictionary<string, object>(map.ToDictionary(kv => kv.Key, kv => (object)kv.Value!)), nested);
            return nested;
        }
        try
        {
            if (targetType == typeof(string)) return value.ToString();
            if (value is IConvertible) return Convert.ChangeType(value, targetType);
        }
        catch { }
        return value;
    }

    private static Type? CollectionElementType(Type? genericType)
    {
        if (genericType is null) return null;
        if (genericType.IsArray) return genericType.GetElementType();
        if (genericType.IsGenericType) return genericType.GetGenericArguments().FirstOrDefault();
        return null;
    }

    private static bool IsDomainType(Type type)
    {
        if (type == typeof(string)) return false;
        var t = type;
        while (t is not null)
        {
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(DomainModel<>)) return true;
            t = t.BaseType;
        }
        var valBase = type;
        while (valBase is not null)
        {
            if (valBase.IsGenericType && valBase.GetGenericTypeDefinition().FullName == typeof(ValueObject<object>).GetGenericTypeDefinition().FullName)
                return true;
            valBase = valBase.BaseType;
        }
        return type.Name == "SimpleObject";
    }

    private static bool IsSingleValueType(Type type)
        => type == typeof(string) || type.IsPrimitive || type.IsEnum || type.IsValueType;

    /// <summary>字段是否应忽略（含 <see cref="IgnoreFieldAttribute"/> 与事件流基础设施字段）。</summary>
    public static bool IsIgnoredField(Type clazz, string fieldName)
    {
        ArgumentNullException.ThrowIfNull(clazz);
        var prop = clazz.GetProperty(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (prop is null) return false;
        return prop.GetCustomAttribute<IgnoreFieldAttribute>() is not null;
    }

    /// <summary>实体间拷贝（严格模式关闭，空值也覆盖），与 Java 版 EntityToEntity 对齐。</summary>
    public static void EntityToEntity(object sourceObj, object targetObj) => CopyProperties(sourceObj, targetObj, strict: false);

    /// <summary>兼容别名（Java 版拼写 copyPropertys）。</summary>
    public static void CopyPropertys(object sourceObj, object targetObj) => CopyProperties(sourceObj, targetObj, strict: false);

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
