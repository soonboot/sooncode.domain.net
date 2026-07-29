using System.Text;
using System.Text.Json;
using System.Reflection;
using System.Collections;

namespace Domain.Infrastructure.Utils;

public static class JsonUtil
{
    private const string MAP_KEY_PLACE_HOLDER = "MAP_KEY_PLACE_HOLDER";

    public static string ToJsonString(object? obj)
    {
        if (obj == null) return "{}";

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        return JsonSerializer.Serialize(obj, options);
    }

    public static T? ConvertToObject<T>(IDictionary<string, object>? map) where T : class
    {
        if (map == null) return default;

        var result = Activator.CreateInstance<T>();
        if (result == null) return default;

        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var prop in properties)
        {
            if (!prop.CanWrite) continue;

            if (map.TryGetValue(prop.Name, out var value) && value != null)
            {
                try
                {
                    var targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

                    if (value is JsonElement jsonElement)
                    {
                        var convertedValue = ConvertJsonElement(jsonElement, targetType);
                        prop.SetValue(result, convertedValue);
                    }
                    else
                    {
                        var convertedValue = Convert.ChangeType(value, targetType);
                        prop.SetValue(result, convertedValue);
                    }
                }
                catch { }
            }
        }

        return result;
    }

    private static object? ConvertJsonElement(JsonElement element, Type targetType)
    {
        return targetType.Name switch
        {
            "String" => element.GetString() ?? "",
            "Int32" => element.GetInt32(),
            "Int64" => element.GetInt64(),
            "Boolean" => element.GetBoolean(),
            "Double" => element.GetDouble(),
            "Decimal" => element.GetDecimal(),
            "DateTime" => element.GetDateTime(),
            _ => element.GetString()
        };
    }

    public static object? BuildObject(Type clazz)
    {
        try
        {
            var ob = Activator.CreateInstance(clazz);
            if (IsBaseType(clazz))
                return ob;

            var allProperties = GetAllProperties(clazz);

            foreach (var property in allProperties)
            {
                var fieldType = property.PropertyType;

                if (fieldType.IsArray)
                {
                    var trueTypeOfArray = fieldType.GetElementType();
                    if (trueTypeOfArray != null)
                    {
                        var arr = Array.CreateInstance(trueTypeOfArray, 0);
                        property.SetValue(ob, arr);
                    }
                }
                else if (IsListType(fieldType))
                {
                    var trueTypeOfList = GetGenericType(property);
                    if (trueTypeOfList != null)
                    {
                        var listObj = BuildObject(trueTypeOfList);
                        if (listObj != null)
                        {
                            var listType = typeof(List<>).MakeGenericType(trueTypeOfList);
                            var list = (IList)Activator.CreateInstance(listType)!;
                            list.Add(listObj);
                            property.SetValue(ob, list);
                        }
                    }
                }
                else if (IsMapType(fieldType))
                {
                    var trueTypeOfMap = GetGenericValueType(property);
                    if (trueTypeOfMap != null)
                    {
                        var mapObj = BuildObject(trueTypeOfMap);
                        if (mapObj != null)
                        {
                            var mapType = typeof(Dictionary<,>).MakeGenericType(typeof(string), trueTypeOfMap);
                            var map = (IDictionary)Activator.CreateInstance(mapType)!;
                            map[MAP_KEY_PLACE_HOLDER] = mapObj;
                            property.SetValue(ob, map);
                        }
                    }
                }
                else if (!IsBaseType(fieldType))
                {
                    var obj = BuildObject(fieldType);
                    property.SetValue(ob, obj);
                }
            }

            return ob;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static List<PropertyInfo> GetAllProperties(Type type)
    {
        var properties = new List<PropertyInfo>();

        var currentType = type;
        while (currentType != null && currentType != typeof(object))
        {
            properties.AddRange(currentType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
            currentType = currentType.BaseType;
        }

        return properties;
    }

    private static Type? GetGenericType(PropertyInfo property)
    {
        if (property.PropertyType.IsGenericType)
        {
            var args = property.PropertyType.GetGenericArguments();
            if (args.Length > 0)
                return args[0];
        }
        return null;
    }

    private static Type? GetGenericValueType(PropertyInfo property)
    {
        if (property.PropertyType.IsGenericType)
        {
            var args = property.PropertyType.GetGenericArguments();
            if (args.Length > 1)
                return args[1];
        }
        return null;
    }

    private static bool IsArrayType(Type type)
    {
        return type.IsArray;
    }

    private static bool IsMapType(Type type)
    {
        return type.Name == "Dictionary`2" || type.FullName?.Contains("Map") == true;
    }

    private static bool IsListType(Type type)
    {
        return type.Name == "List`1" || type.FullName?.Contains("List") == true;
    }

    private static bool IsBaseType(Type type)
    {
        if (type.IsPrimitive)
            return true;
        else if (type.IsEnum)
            return true;
        else if (type == typeof(string))
            return true;
        else if (type == typeof(StringBuilder))
            return true;
        else if (type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte))
            return true;
        else if (type == typeof(double) || type == typeof(float))
            return true;
        else if (type == typeof(decimal))
            return true;
        else if (type == typeof(bool))
            return true;
        else if (type == typeof(DateTime))
            return true;

        return false;
    }
}
