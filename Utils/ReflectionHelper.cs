using System.ComponentModel;
using System.Reflection;

namespace Domain.Infrastructure.Utils;

/// <summary>
/// 反射/Bean 工具，与 Java 版 <c>ReflectUtils</c> 对齐（.NET 规范命名：ReflectionHelper）。
/// <para>.NET 规范：使用 <see cref="PropertyDescriptor"/> 与表达式缓存，避免重复反射。</para>
/// </summary>
public static class ReflectionHelper
{
    /// <summary>获取所有可读写的公共属性描述符。</summary>
    public static PropertyDescriptorCollection GetBeanProperties(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return TypeDescriptor.GetProperties(type);
    }

    /// <summary>获取所有可读属性（Getter）。</summary>
    public static IReadOnlyList<PropertyDescriptor> GetBeanGetters(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        // TypeDescriptor 仅返回可读属性，此处与 Java 版 getBeanGetters(true,false) 对齐。
        return GetBeanProperties(type).Cast<PropertyDescriptor>().ToList();
    }

    /// <summary>获取所有可写属性（Setter），与 Java 版 getBeanSetters 对齐。</summary>
    public static IReadOnlyList<PropertyDescriptor> GetBeanSetters(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return GetBeanProperties(type).Cast<PropertyDescriptor>().Where(p => !p.IsReadOnly).ToList();
    }

    /// <summary>获取可写属性。</summary>
    public static IReadOnlyList<PropertyInfo> GetWritableProperties(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanWrite).ToList();
    }

    /// <summary>按参数类型查找构造函数（包含非公开），失败抛 <see cref="CodeGenerationException"/>。</summary>
    public static ConstructorInfo GetConstructor(Type type, Type[] parameterTypes)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(parameterTypes);
        try
        {
            var ctor = type.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, parameterTypes, null);
            if (ctor is null)
                throw new CodeGenerationException($"找不到构造函数: {type.FullName}({string.Join(",", parameterTypes.Select(t => t.Name))})");
            return ctor;
        }
        catch (Exception ex) when (ex is not CodeGenerationException)
        {
            throw new CodeGenerationException(ex);
        }
    }

    /// <summary>按构造参数创建实例（对齐 Java 版 newInstance(type, parameterTypes, args)）。</summary>
    public static object NewInstance(Type type, Type[] parameterTypes, object?[] args)
        => NewInstance(GetConstructor(type, parameterTypes), args);

    /// <summary>按指定构造函数创建实例。</summary>
    public static object NewInstance(ConstructorInfo ctor, object?[] args)
    {
        ArgumentNullException.ThrowIfNull(ctor);
        try
        {
            return ctor.Invoke(args);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw new CodeGenerationException(ex.InnerException);
        }
        catch (Exception ex) when (ex is not CodeGenerationException)
        {
            throw new CodeGenerationException(ex);
        }
    }

    /// <summary>安全创建实例，失败时抛出 <see cref="CodeGenerationException"/>。</summary>
    public static object NewInstance(Type type)
    {
        try
        {
            return Activator.CreateInstance(type)
                ?? throw new CodeGenerationException($"无法实例化类型: {type.FullName}");
        }
        catch (Exception ex) when (ex is not CodeGenerationException)
        {
            throw new CodeGenerationException(ex);
        }
    }

    /// <summary>安全创建实例（泛型）。</summary>
    public static T NewInstance<T>() where T : new() => new();
}


/// <summary>
/// 历史命名兼容壳：Java 版叫 <c>ReflectUtils</c>，.NET 规范命名为 <see cref="ReflectionHelper"/>。
/// 新代码请直接使用 <see cref="ReflectionHelper"/>。
/// </summary>
[System.Obsolete("请使用 ReflectionHelper（Java ReflectUtils 的 .NET 规范命名）。")]
public static class ReflectUtils
{
    public static System.ComponentModel.PropertyDescriptorCollection GetBeanProperties(System.Type type) => ReflectionHelper.GetBeanProperties(type);
    public static System.Collections.Generic.IReadOnlyList<System.ComponentModel.PropertyDescriptor> GetBeanGetters(System.Type type) => ReflectionHelper.GetBeanGetters(type);
    public static System.Collections.Generic.IReadOnlyList<System.ComponentModel.PropertyDescriptor> GetBeanSetters(System.Type type) => ReflectionHelper.GetBeanSetters(type);
    public static System.Collections.Generic.IReadOnlyList<System.Reflection.PropertyInfo> GetWritableProperties(System.Type type) => ReflectionHelper.GetWritableProperties(type);
    public static System.Reflection.ConstructorInfo GetConstructor(System.Type type, System.Type[] parameterTypes) => ReflectionHelper.GetConstructor(type, parameterTypes);
    public static object NewInstance(System.Type type, System.Type[] parameterTypes, object?[] args) => ReflectionHelper.NewInstance(type, parameterTypes, args);
    public static object NewInstance(System.Reflection.ConstructorInfo ctor, object?[] args) => ReflectionHelper.NewInstance(ctor, args);
    public static object NewInstance(System.Type type) => ReflectionHelper.NewInstance(type);
    public static T NewInstance<T>() where T : new() => ReflectionHelper.NewInstance<T>();
}
