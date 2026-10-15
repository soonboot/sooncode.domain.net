using Domain.Infrastructure.Model;

namespace Domain.Infrastructure.Model;

/// <summary>
/// Lookup 联动处理器（兼容保留）。
/// <para>正本已迁移至 <see cref="Lookup.LookupHandler"/>（与 Java 版 <c>lookup.LookupHandler</c> 包结构对齐），保留此类仅为源码兼容，新代码请直接使用 Lookup 命名空间。</para>
/// </summary>
[Obsolete("已迁移至 Lookup.LookupHandler，新代码请使用 Domain.Infrastructure.Lookup.LookupHandler。")]
public class LookupHandler : Lookup.LookupHandler
{
    /// <summary>兼容构造（程序集+命名空间扫描）。</summary>
    public LookupHandler(string assemblyName, string targetNamespace, IDomainRepository domainRepository)
        : base(assemblyName, targetNamespace, domainRepository)
    {
    }

    /// <summary>兼容构造（显式事件源仓储）。</summary>
    public LookupHandler(string assemblyName, string targetNamespace, IDomainRepository domainRepository, IEventSourcingRepository? sourceRepo)
        : base(assemblyName, targetNamespace, domainRepository, sourceRepo)
    {
    }

    /// <summary>兼容构造（全量注入，含批量写优化器）。</summary>
    public LookupHandler(string assemblyName, string targetNamespace, IDomainRepository domainRepository, IEventSourcingRepository? sourceRepo, Lookup.ILookupBulkWriter? bulkWriter)
        : base(assemblyName, targetNamespace, domainRepository, sourceRepo, bulkWriter)
    {
    }
}
