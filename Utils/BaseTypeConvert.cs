using System.Globalization;

namespace Domain.Infrastructure.Utils;

public static class BaseTypeConvert
{
    private static readonly Dictionary<Type, object> DefMap = new Dictionary<Type, object>
    {
        { typeof(int), 0 },
        { typeof(long), 0L },
        { typeof(float), 0f },
        { typeof(double), 0.0 },
        { typeof(bool), false },
        { typeof(string), "" }
    };
    
    public static object? ConvertTo(string o, Type cla)
    {
        if (cla == typeof(string))
        {
            return o;
        }
        else if (cla == typeof(int) || cla == typeof(Int32))
        {
            return int.Parse(o);
        }
        else if (cla == typeof(long) || cla == typeof(Int64))
        {
            return long.Parse(o);
        }
        else if (cla == typeof(float) || cla == typeof(Single))
        {
            return float.Parse(o);
        }
        else if (cla == typeof(double) || cla == typeof(Double))
        {
            return double.Parse(o);
        }
        else if (cla == typeof(bool) || cla == typeof(Boolean))
        {
            return bool.Parse(o);
        }
        else if (cla == typeof(DateTime))
        {
            return DateTime.ParseExact(o, DatePattern.PARSE_PATTERNS, CultureInfo.InvariantCulture, DateTimeStyles.None);
        }
        else if (cla == typeof(DateOnly))
        {
            return DateOnly.Parse(o);
        }
        else if (cla == typeof(TimeOnly))
        {
            return TimeOnly.Parse(o);
        }
        else if (cla == typeof(DateTimeOffset))
        {
            return DateTimeOffset.Parse(o);
        }
        else
            return o;
    }
    
    /// <summary>是否为单值类型（对齐 Java 版 isSingleValueType）。</summary>
    public static bool IsSingleValueType(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return type == typeof(string) || type.IsPrimitive || type.IsEnum || type.IsValueType;
    }

    /// <summary>通用单值转换（对齐 Java 版 convertValue）。</summary>
    public static object? ConvertValue(object value, Type targetType)
    {
        ArgumentNullException.ThrowIfNull(targetType);
        if (value is null) return null;
        if (targetType.IsInstanceOfType(value)) return value;
        try
        {
            if (targetType == typeof(string)) return value.ToString();
            if (value is IConvertible) return Convert.ChangeType(value, targetType);
            if (value is string s) return ConvertTo(s, targetType);
        }
        catch { }
        return value;
    }

    public static object? Def(Type cla)
    {
        if (DefMap.ContainsKey(cla))
            return DefMap[cla];
        return null;
    }
}
