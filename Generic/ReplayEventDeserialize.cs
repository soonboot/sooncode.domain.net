using System.Text.Json;
using System.Text.Json.Serialization;
using Domain.Infrastructure.Model;

namespace Domain.Infrastructure.Generic;

/// <summary>
/// <see cref="ReplayEvent"/> 的多态反序列化器，与 Java 版 <c>ReplayEventDeserialize</c> 对齐。
/// <para>依据 <c>wclass</c>（CLR 类型全名）还原 <see cref="Entity"/> 真实类型。</para>
/// </summary>
public sealed class ReplayEventDeserializeConverter : JsonConverter<Entity>
{
    /// <inheritdoc/>
    public override Entity? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;
        if (root.TryGetProperty("wclass", out var wclassProp))
        {
            var typeName = wclassProp.GetString();
            if (!string.IsNullOrEmpty(typeName))
            {
                var type = Type.GetType(typeName);
                if (type is not null)
                {
                    return (Entity?)JsonSerializer.Deserialize(root.GetRawText(), type, options);
                }
            }
        }
        return null;
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, Entity value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("wclass", value.GetType().AssemblyQualifiedName);
        foreach (var prop in value.GetType().GetProperties())
        {
            if (!prop.CanRead) continue;
            writer.WritePropertyName(prop.Name);
            JsonSerializer.Serialize(writer, prop.GetValue(value), options);
        }
        writer.WriteEndObject();
    }
}
