using Domain.Infrastructure.Model;

namespace Domain.Infrastructure.Monitor;

/// <summary>实体监听函数接口，与 Java 版 <c>monitor.IEntityFunction</c> 对齐（函数式接口）。</summary>
public interface IEntityFunction
{
    /// <summary>执行业体监听。</summary>
    /// <param name="model">实体变更模型。</param>
    void Run(FunctionModel model);
}
