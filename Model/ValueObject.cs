namespace Domain.Infrastructure.Model;

/// <summary>
/// 值对象的基类
/// </summary>
/// <typeparam name="T">值对象的类型</typeparam>
public abstract class ValueObject<T> : IEquatable<ValueObject<T>>
{
    protected T? Value { get; set; }

    public abstract T GetValue();

    public bool Equals(ValueObject<T>? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return EqualityComparer<T>.Default.Equals(Value, other.Value);
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((ValueObject<T>)obj);
    }

    public override int GetHashCode()
    {
        return EqualityComparer<T>.Default.GetHashCode(Value!);
    }
}
