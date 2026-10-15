using System.Reflection;

namespace Domain.Infrastructure.Utils;

/// <summary>
/// 程序集/类型扫描工具，与 Java 版 <c>ClassUtil</c> 对齐。
/// <para>.NET 规范：基于 <see cref="Assembly"/> 而非类路径扫描，提供按命名空间与特性过滤能力。</para>
/// </summary>
public static class ClassUtil
{
    /// <summary>获取指定程序集中、指定命名空间下的所有类。</summary>
    public static IReadOnlyList<Type> GetClassList(Assembly assembly, string @namespace, bool recursive = true)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        return assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.Namespace is not null &&
                (recursive ? t.Namespace.StartsWith(@namespace, StringComparison.Ordinal) : t.Namespace == @namespace))
            .ToList();
    }

    /// <summary>按程序集名称获取命名空间下的所有类。</summary>
    public static IReadOnlyList<Type>? GetClassList(string assemblyName, string targetNamespace, bool recursive = true)
    {
        try
        {
            var assembly = Assembly.Load(new AssemblyName(assemblyName));
            return GetClassList(assembly, targetNamespace, recursive);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>获取带有指定特性的所有类。</summary>
    public static IReadOnlyList<Type> GetClassListByAttribute<TAttribute>(Assembly assembly, string @namespace, bool recursive = true)
        where TAttribute : Attribute
    {
        return GetClassList(assembly, @namespace, recursive)
            .Where(t => t.GetCustomAttributes(typeof(TAttribute), inherit: true).Length != 0)
            .ToList();
    }

    /// <summary>按程序集名称获取带有指定特性的所有类（空结果返回 <c>null</c>，与 Java 语义对齐）。</summary>
    public static IReadOnlyList<Type>? GetClassListByAttribute(string assemblyName, string targetNamespace, Type attributeType)
    {
        try
        {
            var assembly = Assembly.Load(new AssemblyName(assemblyName));
            var list = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && t.Namespace is not null &&
                    t.Namespace.StartsWith(targetNamespace, StringComparison.Ordinal) &&
                    t.GetCustomAttributes(attributeType, inherit: true).Length != 0)
                .ToList();
            return list.Count > 0 ? list : null;
        }
        catch
        {
            return null;
        }
    }
}
