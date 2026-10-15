using Domain.Infrastructure.Model;

namespace Domain.Infrastructure.Monitor;

/// <summary>实体监听函数模型（泛型），与 Java 版 <c>EntityFunctionModel</c> 对齐。</summary>
/// <typeparam name="T">实体类型。</typeparam>
public sealed class EntityFunctionModel<T> where T : Entity
{
    /// <summary>变更前实体（新增时为 null）。</summary>
    public T? SourceEntity { get; set; }

    /// <summary>变更后实体。</summary>
    public T? TargetEntity { get; set; }
}

/// <summary>实体监听函数，与 Java 版 <c>IEntityFunction</c> 对齐。</summary>
/// <param name="model">实体变更模型。</param>
public delegate void EntityFunction(FunctionModel model);

