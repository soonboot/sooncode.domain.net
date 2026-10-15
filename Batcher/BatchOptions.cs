using Domain.Infrastructure.Config;

namespace Domain.Infrastructure.Batcher;

/// <summary>
/// 批量持久化选项，与 Java 版 <c>BatchOptions</c> 对齐。
/// <para>.NET 规范：不可变默认值 + 流畅构造，默认从 <see cref="InfraConfig"/> 读取事务开关。</para>
/// </summary>
public sealed class BatchOptions
{
    /// <summary>是否使用事务（要求副本集/分片集群）。默认跟随 <see cref="InfraConfig.IsAtomic"/>。</summary>
    public bool Atomic { get; private set; }

    /// <summary>批量提交成功后是否触发 Monitor 监听。默认 <c>true</c>。</summary>
    public bool Monitor { get; private set; } = true;

    /// <summary>失败处理策略，默认 <see cref="FailureMode.Continue"/>。</summary>
    public FailureMode FailureMode { get; private set; } = FailureMode.Continue;

    /// <summary>是否触发监听（语义化别名）。</summary>
    public bool NotifyListeners => Monitor;

    /// <summary>是否为静默模式（不触发监听）。</summary>
    public bool IsSilent => !Monitor;

    public BatchOptions() : this(InfraConfig.IsAtomic)
    {
    }

    private BatchOptions(bool atomic)
    {
        Atomic = atomic;
    }

    /// <summary>创建默认选项（读取全局事务开关）。</summary>
    public static BatchOptions Defaults() => new(InfraConfig.IsAtomic);

    /// <summary>设置是否使用事务。</summary>
    public BatchOptions WithAtomic(bool atomic)
    {
        Atomic = atomic;
        return this;
    }

    /// <summary>设置是否触发监听。</summary>
    public BatchOptions WithMonitor(bool monitor)
    {
        Monitor = monitor;
        return this;
    }

    /// <summary>语义化别名：设置是否触发监听。</summary>
    public BatchOptions WithNotifyListeners(bool notify) => WithMonitor(notify);

    /// <summary>设置是否静默写入（不触发监听），与 <see cref="WithMonitor"/> 互为反义。</summary>
    public BatchOptions WithSilent(bool silent) => WithMonitor(!silent);

    /// <summary>设置失败处理策略。</summary>
    public BatchOptions WithFailureMode(FailureMode failureMode)
    {
        FailureMode = failureMode;
        return this;
    }

    /// <summary>校验选项组合有效性（所有 atomic/failureMode 组合均合法，语义由执行器区分）。</summary>
    public void Validate()
    {
    }
}
