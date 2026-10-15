using Domain.Infrastructure.Model;

namespace Domain.Infrastructure.Monitor;

/// <summary>
/// Lookup 联动处理器（兼容保留）。
/// <para>正本为 <see cref="Lookup.LookupHandler"/>（镜像 Java 版 <c>monitor.LookupHandler @Deprecated</c> 模式：Java 只保留一层兼容壳指向 <c>lookup.LookupHandler</c>），保留此类仅为二进制/源码兼容，新代码请直接使用 Lookup 命名空间。</para>
/// </summary>
[Obsolete("已迁移至 Lookup.LookupHandler，新代码请使用 Domain.Infrastructure.Lookup.LookupHandler。")]
public class LookupHandler : Model.LookupHandler
{
    /// <summary>兼容构造（程序集+命名空间扫描）。</summary>
    public LookupHandler(string assemblyName, string targetNamespace, IDomainRepository repository)
        : base(assemblyName, targetNamespace, repository)
    {
    }

    /// <summary>兼容构造（显式事件源仓储）。</summary>
    public LookupHandler(string assemblyName, string targetNamespace, IDomainRepository repository, IEventSourcingRepository? sourceRepo)
        : base(assemblyName, targetNamespace, repository, sourceRepo)
    {
    }

    /// <summary>兼容构造（全量注入，含批量写优化器）。</summary>
    public LookupHandler(string assemblyName, string targetNamespace, IDomainRepository repository, IEventSourcingRepository? sourceRepo, Lookup.ILookupBulkWriter? bulkWriter)
        : base(assemblyName, targetNamespace, repository, sourceRepo, bulkWriter)
    {
    }
}
