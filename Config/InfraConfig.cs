namespace Domain.Infrastructure.Config;

/// <summary>
/// 基础设施全局配置，控制事务/批量与 Lookup 调优行为。
/// <para>
/// 优先级（从高到低）：显式代码设置 &gt; 环境变量 &gt; 配置文件 &gt; 默认值。
/// 与 Java 版 <c>InfraConfig</c> 对齐：默认关闭事务（单机 Mongo 开箱即用），
/// 生产副本集/分片集群需显式开启。
/// </para>
/// </summary>
public static class InfraConfig
{
    private static readonly object SyncRoot = new();
    private static bool _atomic = InitAtomic();
    private static int _lookupPageSize = InitInt("DOMAIN_LOOKUP_PAGESIZE", "domain.lookup.pageSize", 500);
    private static int _lookupAsyncThreshold = InitInt("DOMAIN_LOOKUP_ASYNCTHRESHOLD", "domain.lookup.asyncThreshold", 10);
    private static long _lookupCoalesceWindowMs = InitLong("DOMAIN_LOOKUP_COALESCEWINDOWMS", "domain.lookup.coalesceWindowMs", 500L);
    private static int _lookupBulkBatchSize = InitInt("DOMAIN_LOOKUP_BULKBATCHSIZE", "domain.lookup.bulkBatchSize", 500);

    /// <summary>当前全局是否使用事务（原子提交）。单机 Mongo 应为 <c>false</c>。</summary>
    public static bool IsAtomic
    {
        get { lock (SyncRoot) { return _atomic; } }
    }

    /// <summary>别名：是否使用事务。</summary>
    public static bool IsTransactional => IsAtomic;

    /// <summary>设置全局是否使用事务，建议在 <c>Monitor.New()</c> 之后、配置数据库连接之前调用。</summary>
    public static void SetAtomic(bool enabled)
    {
        lock (SyncRoot) { _atomic = enabled; }
    }

    /// <summary>别名：设置是否使用事务。</summary>
    public static void SetTransactional(bool enabled) => SetAtomic(enabled);

    /// <summary>Lookup 分页大小，默认 500。</summary>
    public static int LookupPageSize => _lookupPageSize;

    /// <summary>Lookup 异步阈值，小于该扇出同步执行，默认 10。</summary>
    public static int LookupAsyncThreshold => _lookupAsyncThreshold;

    /// <summary>Lookup 高频去重窗口（毫秒），默认 500。</summary>
    public static long LookupCoalesceWindowMs => _lookupCoalesceWindowMs;

    /// <summary>Lookup 批量写每批大小，默认 500。</summary>
    public static int LookupBulkBatchSize => _lookupBulkBatchSize;

    public static void SetLookupPageSize(int pageSize)
    {
        if (pageSize <= 0) throw new ArgumentOutOfRangeException(nameof(pageSize), "lookupPageSize 必须 > 0");
        _lookupPageSize = pageSize;
    }

    public static void SetLookupAsyncThreshold(int threshold)
    {
        if (threshold < 0) throw new ArgumentOutOfRangeException(nameof(threshold), "lookupAsyncThreshold 不能为负");
        _lookupAsyncThreshold = threshold;
    }

    public static void SetLookupCoalesceWindowMs(long ms)
    {
        if (ms < 0) throw new ArgumentOutOfRangeException(nameof(ms), "lookupCoalesceWindowMs 不能为负");
        _lookupCoalesceWindowMs = ms;
    }

    public static void SetLookupBulkBatchSize(int batchSize)
    {
        if (batchSize <= 0) throw new ArgumentOutOfRangeException(nameof(batchSize), "lookupBulkBatchSize 必须 > 0");
        _lookupBulkBatchSize = batchSize;
    }

    /// <summary>供测试重置为默认值（重新读取环境变量）。</summary>
    public static void Reset()
    {
        lock (SyncRoot)
        {
            _atomic = InitAtomic();
            _lookupPageSize = InitInt("DOMAIN_LOOKUP_PAGESIZE", "domain.lookup.pageSize", 500);
            _lookupAsyncThreshold = InitInt("DOMAIN_LOOKUP_ASYNCTHRESHOLD", "domain.lookup.asyncThreshold", 10);
            _lookupCoalesceWindowMs = InitLong("DOMAIN_LOOKUP_COALESCEWINDOWMS", "domain.lookup.coalesceWindowMs", 500L);
            _lookupBulkBatchSize = InitInt("DOMAIN_LOOKUP_BULKBATCHSIZE", "domain.lookup.bulkBatchSize", 500);
        }
    }

    private static bool InitAtomic()
    {
        var v = Environment.GetEnvironmentVariable("DOMAIN_INFRA_ATOMIC")
            ?? Environment.GetEnvironmentVariable("DOMAIN_MONGO_ATOMIC")
            ?? Environment.GetEnvironmentVariable("DOMAIN_INFRA_TRANSACTIONAL")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENV_DOMAIN_INFRA_ATOMIC");
        if (v is null) return false;
        var t = v.Trim().ToLowerInvariant();
        return t is "true" or "1" or "yes";
    }

    private static int InitInt(string envKey, string altKey, int def)
    {
        var v = Environment.GetEnvironmentVariable(envKey)
            ?? Environment.GetEnvironmentVariable(envKey.Replace("_", ".").ToUpperInvariant())
            ?? Environment.GetEnvironmentVariable(altKey.ToUpperInvariant().Replace(".", "_"));
        if (v is not null && int.TryParse(v.Trim(), out var n) && n > 0) return n;
        return def;
    }

    private static long InitLong(string envKey, string altKey, long def)
    {
        var v = Environment.GetEnvironmentVariable(envKey)
            ?? Environment.GetEnvironmentVariable(altKey.ToUpperInvariant().Replace(".", "_"));
        if (v is not null && long.TryParse(v.Trim(), out var n) && n >= 0) return n;
        return def;
    }
}
