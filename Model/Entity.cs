namespace Domain.Infrastructure.Model;

/// <summary>
/// 领域实体的基类
/// </summary>
public class Entity : IEquatable<Entity>
{
    public string Id { get; set; }
    
    public Entity()
    {
        Id = Guid.NewGuid().ToString("N");
    }
    
    public bool Equals(Entity? other)
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
        return Equals((Entity)obj);
    }
    
    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }

    public void SetId(string id)
    {
        Id = id;
    }
}