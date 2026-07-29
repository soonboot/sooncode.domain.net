using System.Text.Json.Serialization;

namespace Domain.Infrastructure.Model;

/// <summary>
/// 事件流, 也是事件元数据的类结构
/// </summary>
public class EventStream
{
    [JsonPropertyName("id")]
    public string Id { get; private set; }
    
    [JsonPropertyName("version")]
    public int? Version { get; private set; }
    
    [JsonIgnore]
    public Type EntityType { get; private set; }
    
    [JsonPropertyName("entityType")]
    public string EntityTypeName => EntityType?.AssemblyQualifiedName;
    
    [JsonPropertyName("isInvalid")]
    public int IsInvalid { get; internal set; } = 0;
    
    [JsonPropertyName("createDate")]
    public DateTime CreateDate { get; set; }
    
    /// <summary>
    /// 空构造器, 为反序列化使用
    /// </summary>
    [JsonConstructor]
    private EventStream() {}
    
    /// <summary>
    /// 构造器 新建业务实体对象时,需要构建元数据
    /// </summary>
    /// <param name="id">实体对象ID</param>
    /// <param name="cla">实体类型</param>
    public EventStream(string id, Type cla)
    {
        Id = id;
        Version = 0;
        EntityType = cla;
    }
    
    /// <summary>
    /// 构造器
    /// </summary>
    /// <param name="id">实体对象ID</param>
    /// <param name="version">当前版本</param>
    /// <param name="invalid">元数据是否失败, 其实就是删除</param>
    /// <param name="cla">实体的类型</param>
    /// <param name="createDate">创建日期</param>
    public EventStream(string id, int? version, int invalid, Type cla, DateTime createDate)
    {
        Id = id;
        Version = version;
        IsInvalid = invalid;
        EntityType = cla;
        CreateDate = createDate;
    }
    
    /// <summary>
    /// 失效, 使用元数据失效, 其实就是这个数据被删除了
    /// </summary>
    /// <returns>事件流对象</returns>
    public EventStream Invalid()
    {
        IsInvalid = 1;
        return this;
    }
    
    /// <summary>
    /// 注册事件, 注册事件会生成事件包装器, 方便对事件流进行存储
    /// </summary>
    /// <param name="event">事件对象</param>
    /// <param name="sourceClass">源类类型</param>
    /// <returns>事件包装器</returns>
    public EventWrapper RegisterEvent(DomainEvent @event, Type sourceClass)
    {
        Version = (Version ?? 0) + 1;
        return new EventWrapper(@event, Version.Value, Id, sourceClass);
    }
}