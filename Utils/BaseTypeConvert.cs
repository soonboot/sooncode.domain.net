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
    
    public static object? Def(Type cla)
    {
        if (DefMap.ContainsKey(cla))
            return DefMap[cla];
        return null;
    }
}
