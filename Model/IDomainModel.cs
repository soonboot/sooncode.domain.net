using Domain.Infrastructure.Annotations;

namespace Domain.Infrastructure.Model;

/// <summary>
/// 领域聚合根的非泛型持久化切面（与 Java 版 <c>DomainModel</c> 原始类型用法对齐）。
/// <para>供 <c>DomainSession</c>、批量仓储、Lookup 等基础设施以非泛型方式访问持久化状态，避免反射与字符串方法名。</para>
/// <para>wire-compatible 说明：<c>__stored__/__startVersion__</c> 字段名故意保留 Java 序列化兼容，勿按 .NET 命名规范改名。</para>
/// </summary>
public interface IDomainModel
{
    /// <summary>Java 兼容字段 <c>__stored__</c> 的属性视图（已 <c>[IgnoreField]</c>，不进快照）。</summary>
    bool StoredFlag { get; set; }

    /// <summary>Java 兼容字段 <c>__startVersion__</c> 的属性视图（乐观锁基线，已 <c>[IgnoreField]</c>）。</summary>
    int StartVersion { get; set; }

    /// <summary>当前版本号。</summary>
    int GetVersion();

    /// <summary>事件流只读视图。</summary>
    IReadOnlyList<DomainEvent> GetEvents();

    /// <summary>尚未成功持久化的事件。</summary>
    IReadOnlyList<DomainEvent> GetPendingEvents();

    /// <summary>为待持久化事件生成审计快照。</summary>
    IReadOnlyList<DomainEvent> PreparePendingEventSnapshots();

    /// <summary>是否已全部落库。</summary>
    bool IsStored();

    /// <summary>标记已落库（仅持久层在真实写入成功后调用）。</summary>
    void MarkStored();

    /// <summary>推进持久化边界（必须为待持久化前缀）。</summary>
    void MarkEventsPersisted(IReadOnlyList<DomainEvent>? persistedEvents);
}
