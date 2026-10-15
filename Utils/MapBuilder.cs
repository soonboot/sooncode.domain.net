namespace Domain.Infrastructure.Utils;

/// <summary>字典构造器，与 Java 版 <c>Utils.MapBuilder</c> 对齐。</summary>
public sealed class MapBuilder : Dictionary<string, object?>
{
    public MapBuilder(string key, object? value) : base(StringComparer.Ordinal)
    {
        this[key] = value;
    }

    /// <summary>链式添加键值。</summary>
    public MapBuilder AddEntry(string key, object? value)
    {
        this[key] = value;
        return this;
    }
}

/// <summary>通用工具入口，与 Java 版 <c>Utils</c> 对齐。</summary>
public static class MapUtil
{
    /// <summary>创建字典构造器。</summary>
    public static MapBuilder MapBuilder(string key, object? value) => new(key, value);
}

/// <summary>泛型键值对，与 Java 版 <c>Utils.KeyValuePair</c> 对齐。</summary>
/// <typeparam name="K">键类型。</typeparam>
/// <typeparam name="V">值类型。</typeparam>
public sealed class KeyValuePair<K, V> : IEquatable<KeyValuePair<K, V>>
{
    public K Key { get; set; }
    public V Value { get; set; }

    public KeyValuePair(K key, V value)
    {
        Key = key;
        Value = value;
    }

    public bool Equals(KeyValuePair<K, V>? other)
    {
        if (other is null) return false;
        return EqualityComparer<K>.Default.Equals(Key, other.Key) &&
               EqualityComparer<V>.Default.Equals(Value, other.Value);
    }

    public override bool Equals(object? obj) => obj is KeyValuePair<K, V> other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Key, Value);
}
