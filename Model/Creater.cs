using System.Text.Json.Serialization;

namespace Domain.Infrastructure.Model;

/// <summary>
/// 创建者信息类
/// </summary>
public class Creater
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("payload")]
    public string? Payload { get; set; }

    /// <summary>
    /// 无参构造函数
    /// </summary>
    public Creater() {}

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="name">名称</param>
    /// <param name="id">ID</param>
    public Creater(string? name, string? id)
    {
        Name = name ?? string.Empty;
        Id = id ?? string.Empty;
        Payload = string.Empty;
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="name">名称</param>
    /// <param name="id">ID</param>
    /// <param name="payload">附加数据</param>
    public Creater(string name, string id, string payload)
    {
        Name = name;
        Id = id;
        Payload = payload;
    }
}
