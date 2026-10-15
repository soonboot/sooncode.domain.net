using Domain.Infrastructure.Annotations;
using Domain.Infrastructure.Finder;
using Domain.Infrastructure.Repository.Mongo;
using Domain.Infrastructure.Model;
using Domain.Infrastructure.Session;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;

namespace Domain.Infrastructure.Lookup;

/// <summary>
/// Lookup 快照重建与联动更新处理器，与 Java 版 <c>lookup.LookupHandler</c> 对齐。
/// <para>正本位于 <c>Domain.Infrastructure.Lookup</c>（与 Java 包结构对齐）；<c>Model.LookupHandler</c> 仅为历史兼容壳。</para>
/// <para>.NET 规范：file-scoped namespace + XML docs；调优参数单一数据源为 <c>InfraConfig</c>（构造后动态生效，需配合 <see cref="RefreshConfig"/>）。</para>
/// </summary>
public class LookupHandler
{
    private readonly IEventSourcingRepository? _sourceRepo;
    private readonly ILookupBulkWriter? _bulkWriter;
    private readonly Monitor.Monitor _monitor = Monitor.Monitor.Instance;
    private readonly IDomainRepository _domainRepository;
    private readonly ConcurrentDictionary<Type, List<LookupHelper>> _lookupMap = new();
    private readonly ConcurrentDictionary<Type, string> _snapshotNames = new();
    private readonly ConcurrentDictionary<string, KeyValuePair<object, MethodInfo>> _finderMethodCache = new();
    private int _pageSize = Config.InfraConfig.LookupPageSize;
    private int _asyncThreshold = Config.InfraConfig.LookupAsyncThreshold;
    private long _coalesceWindowMs = Config.InfraConfig.LookupCoalesceWindowMs;
    private int _bulkBatchSize = Config.InfraConfig.LookupBulkBatchSize;
    private volatile bool _shutdown;
    private readonly CancellationTokenSource _cts = new();
    private static readonly AsyncLocal<HashSet<string>?> ReentrancyGuard = new();
    private readonly ConcurrentDictionary<string, long> _coalesceTimestamps = new(StringComparer.Ordinal);

    /// <summary>Lookup 分页大小（默认跟随 <c>InfraConfig</c>，可单独覆盖）。</summary>
    public int PageSize => _pageSize;

    /// <summary>Lookup 异步阈值。</summary>
    public int AsyncThreshold => _asyncThreshold;

    /// <summary>Lookup 高频去重窗口（毫秒）。</summary>
    public long CoalesceWindowMs => _coalesceWindowMs;

    /// <summary>Lookup 批量写每批大小（快照回写分批，与 Java 版 bulkBatchSize 对齐）。</summary>
    public int BulkBatchSize => _bulkBatchSize;

    /// <summary>后台任务取消令牌（<see cref="Shutdown"/> 后取消）。</summary>
    public CancellationToken ShutdownToken => _cts.Token;

    /// <summary>是否已关闭。</summary>
    public bool IsTerminated => _shutdown;

    public LookupHandler(string assemblyName, string targetNamespace, IDomainRepository domainRepository)
        : this(assemblyName, targetNamespace, domainRepository, MongoSingle.GetInstance().GetRepository())
    {
    }

    /// <summary>显式注入事件源仓储（便于单测/非 Mongo 存储），与 Java 版 <c>LookupHandler(packageName, repository, sourceRepo)</c> 对齐。</summary>
    public LookupHandler(string assemblyName, string targetNamespace, IDomainRepository domainRepository, IEventSourcingRepository? sourceRepo)
        : this(assemblyName, targetNamespace, domainRepository, sourceRepo, null)
    {
    }

    /// <summary>
    /// 全量注入（含批量写优化器），与 Java 版 <c>LookupHandler(packageName, repository, sourceRepo, bulkWriter)</c> 对齐。
    /// <para>.NET 规范：显式依赖注入便于单测；null 时按 sourceRepo 自动创建回退实现。</para>
    /// </summary>
    public LookupHandler(string assemblyName, string targetNamespace, IDomainRepository domainRepository, IEventSourcingRepository? sourceRepo, ILookupBulkWriter? bulkWriter)
    {
        _domainRepository = domainRepository ?? throw new ArgumentNullException(nameof(domainRepository));
        _sourceRepo = sourceRepo;
        _bulkWriter = bulkWriter;
        RefreshConfig();
        var classList = GetClassListByAnnotation(assemblyName, targetNamespace, typeof(LookupModelAttribute));
        _lookupMap.Clear();
        _ = ReBuildSnapshotAsync(classList);
    }

    /// <summary>重新读取 <c>InfraConfig</c> 调优参数（构造后动态生效入口）。</summary>
    public void RefreshConfig()
    {
        _pageSize = Config.InfraConfig.LookupPageSize;
        _asyncThreshold = Config.InfraConfig.LookupAsyncThreshold;
        _coalesceWindowMs = Config.InfraConfig.LookupCoalesceWindowMs;
        _bulkBatchSize = Config.InfraConfig.LookupBulkBatchSize;
        _bulkWriter?.Refresh();
    }

    /// <summary>关闭后台联动并取消未完成的后台任务（幂等；与 Java 版 shutdown/静态调度器清理对齐）。</summary>
    public void Shutdown()
    {
        _shutdown = true;
        try { _cts.Cancel(); } catch (ObjectDisposedException) { }
    }

    private static bool EnterGuard(Entity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        var set = ReentrancyGuard.Value ??= new HashSet<string>(StringComparer.Ordinal);
        return set.Add($"{entity.GetType().FullName}-{entity.Id}");
    }

    private static void LeaveGuard(Entity entity)
    {
        if (entity is null || ReentrancyGuard.Value is null) return;
        ReentrancyGuard.Value.Remove($"{entity.GetType().FullName}-{entity.Id}");
    }

    private bool ShouldCoalesce(string dedupKey)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (_coalesceTimestamps.TryGetValue(dedupKey, out var last) && now - last < _coalesceWindowMs) return true;
        _coalesceTimestamps[dedupKey] = now;
        return false;
    }

    private async Task ReBuildSnapshotAsync(List<Type>? classList)
    {
        if (classList == null) return;

        var tasks = classList.Select(cla =>
            Task.Run(async () =>
            {
                var lookups = InitLookup(cla);
                if (lookups != null)
                {
                    await UpdateEntitiesInBackgroundAsync(cla, lookups);
                }
            }));

        await Task.WhenAll(tasks);
        if (!_cts.IsCancellationRequested) MonitorEntity();
    }

    private async Task UpdateEntitiesInBackgroundAsync(Type cla, IDictionary<Type, LookupHelper> helperMap)
    {
        var pageSize = _pageSize;
        int pageIndex = 0;

        while (true)
        {
            try
            {
                var page = await Task.Run(() => GetPage(cla, pageSize, pageIndex));
                if (page?.Content == null || !page.Content.Any()) return;

                foreach (var entity in page.Content.Cast<Entity>())
                {
                    UpdateEntity(cla, entity, helperMap);
                }

                if (page.Content.Count() < pageSize)
                    return;

                pageIndex++;
            }
            catch
            {
                break;
            }
        }
    }

    private List<Type>? GetClassListByAnnotation(string assemblyName, string targetNamespace, Type annotationType)
    {
        var assembly = Assembly.Load(assemblyName);
        var classList = assembly.GetTypes()
            .Where(t =>
                t.IsClass &&
                !t.IsAbstract &&
                t.Namespace != null &&
                t.Namespace.StartsWith(targetNamespace) &&
                t.GetCustomAttributes(annotationType, true).Any())
            .ToList();

        return classList.Count > 0 ? classList : null;
    }

    private string GetCollectionName(Type cType)
    {
        return _snapshotNames.GetOrAdd(cType, _ =>
        {
            string collectionName = "";
            if (cType.IsDefined(typeof(ModelSnapshotAttribute), true))
            {
                var modelSnapshot = cType.GetCustomAttribute<ModelSnapshotAttribute>();
                if (modelSnapshot != null)
                {
                    collectionName = modelSnapshot.Value ?? "";
                    if (string.IsNullOrEmpty(collectionName))
                    {
                        collectionName = modelSnapshot.CollectionName ?? "";
                    }
                }
            }
            return collectionName;
        });
    }

    private IDictionary<Type, LookupHelper>? InitLookup(Type cla)
    {
        var snapshot = _sourceRepo?.GetSnapshotDoc(cla.FullName ?? "", GetCollectionName(cla));
        var properties = cla.GetProperties();
        bool needUpdate = false;
        var helperMap = new Dictionary<Type, LookupHelper>();

        foreach (var property in properties)
        {
            try
            {
                if (!property.IsDefined(typeof(LookupAttribute), true)) continue;

                var lookup = property.GetCustomAttribute<LookupAttribute>();
                if (lookup == null) continue;

                ValidateLookupAnnotation(cla, property, lookup);

                LookupHelper helper;
                if (!helperMap.ContainsKey(lookup.FromModel))
                {
                    helper = new LookupHelper(cla);
                    helper.FromModel = lookup.FromModel;
                    helperMap[lookup.FromModel] = helper;
                }
                else
                {
                    helper = helperMap[lookup.FromModel];
                }

                helper.AddField(lookup.LocalField, property);

                if (snapshot != null && !snapshot.ContainsKey(property.Name))
                    needUpdate = true;
            }
            catch
            {
            }
        }

        foreach (var map in helperMap)
        {
            List<LookupHelper> lookups;
            if (_lookupMap.TryGetValue(map.Key, out var existingLookups))
                lookups = existingLookups;
            else
                lookups = new List<LookupHelper>();

            lookups.Add(map.Value);
            _lookupMap[map.Key] = lookups;
        }

        MonitorEntity(cla, helperMap);
        return needUpdate ? helperMap : null;
    }

    /// <summary>
    /// 校验 <c>Lookup</c> 注解完整性（fromModel/localField/fromField 非空、类型约束、无自环、字段可读写）。
    /// 与 Java 版 <c>validateLookupAnnotation</c> 对齐，配置错误早暴露。
    /// </summary>
    private static void ValidateLookupAnnotation(Type localModel, PropertyInfo field, LookupAttribute lookup)
    {
        ArgumentNullException.ThrowIfNull(localModel);
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(lookup);
        if (lookup.FromModel is null)
            throw new DomainException($"[Lookup] {localModel.FullName}.{field.Name} fromModel 不能为空");
        if (string.IsNullOrWhiteSpace(lookup.LocalField))
            throw new DomainException($"[Lookup] {localModel.FullName}.{field.Name} localField 不能为空");
        if (string.IsNullOrWhiteSpace(lookup.FromField))
            throw new DomainException($"[Lookup] {localModel.FullName}.{field.Name} fromField 不能为空");
        if (!typeof(Entity).IsAssignableFrom(lookup.FromModel))
            throw new DomainException($"[Lookup] {localModel.FullName}.{field.Name} fromModel 必须继承 Entity: {lookup.FromModel.FullName}");
        if (localModel == lookup.FromModel)
            throw new DomainException($"[Lookup] {localModel.FullName}.{field.Name} fromModel 不能与本地模型相同，避免自环");
        if (localModel.GetProperty(lookup.LocalField) is not { CanRead: true })
            throw new DomainException($"[Lookup] {localModel.FullName}.{field.Name} localField='{lookup.LocalField}' 不存在或不可读");
        if (lookup.FromModel.GetProperty(lookup.FromField) is not { CanRead: true })
            throw new DomainException($"[Lookup] {localModel.FullName}.{field.Name} fromField='{lookup.FromField}' 在 {lookup.FromModel.FullName} 中不存在或不可读");
        if (!field.CanWrite)
            throw new DomainException($"[Lookup] {localModel.FullName}.{field.Name} 目标字段无写方法");
    }

    /// <summary>构造高频去重键（fromId + 本地模型 + 更新字段 + 删/改标记）。与 Java 版 <c>buildDedupKey</c> 对齐。</summary>
    private static string BuildDedupKey(Entity fromEntity, LookupHelper helper, IReadOnlyList<PropertyInfo> updateFields, bool isDelete)
    {
        ArgumentNullException.ThrowIfNull(fromEntity);
        ArgumentNullException.ThrowIfNull(helper);
        var fields = updateFields is null ? string.Empty : string.Join(",", updateFields.Select(f => f.Name).OrderBy(n => n, StringComparer.Ordinal));
        return $"{fromEntity.GetType().FullName}:{fromEntity.Id}:{(helper.LocalModel?.FullName ?? "?")}:{fields}:{(isDelete ? "D" : "U")}";
    }

    private PropertyDescriptor? GetPropertyDescriptor(Type type, string propertyName)
    {
        var properties = TypeDescriptor.GetProperties(type);
        return properties[propertyName];
    }

    private void MonitorEntity(Type cla, IDictionary<Type, LookupHelper> helperMap)
    {
        // 正向填充同样加高频去重 + 重入守卫（与反向 RunUpdateSnapshotAsync 对称；Java 版双向均受保护）。
        _monitor.ListenEntity(cla).Add(en => GuardedForwardUpdate(cla, entity: en.TargetEntity, helperMap));
        _monitor.ListenEntity(cla).Modify(en => GuardedForwardUpdate(cla, entity: en.TargetEntity, helperMap));
    }

    private void GuardedForwardUpdate(Type cla, Entity? entity, IDictionary<Type, LookupHelper> helperMap)
    {
        if (entity is null || _shutdown || _cts.IsCancellationRequested) return;
        var dedupKey = $"FWD:{cla.FullName}:{entity.Id}";
        if (ShouldCoalesce(dedupKey)) return;
        if (!EnterGuard(entity)) return;
        try
        {
            if (SessionManager.Contains(entity))
            {
                var session = SessionManager.Get(entity);
                session?.SetSessionFunction(() =>
                {
                    try { UpdateEntity(cla, entity, helperMap); }
                    finally { LeaveGuard(entity); }
                });
            }
            else
            {
                UpdateEntity(cla, entity, helperMap);
            }
        }
        finally
        {
            if (!SessionManager.Contains(entity)) LeaveGuard(entity);
        }
    }

    private void MonitorEntity()
    {
        foreach (var lookupEntry in _lookupMap)
        {
            var listen = lookupEntry.Key;
            var helpers = lookupEntry.Value;

            _monitor.ListenEntity(listen).Modify(en =>
            {
                var entity = en.TargetEntity;
                if (entity is null) return;
                if (SessionManager.Contains(entity))
                {
                    var session = SessionManager.Get(entity);
                    session?.SetSessionFunction(() =>
                    {
                        foreach (var helper in helpers)
                        {
                            foreach (var localPropertys in helper.Propertys)
                            {
                                _ = RunUpdateSnapshotAsync(entity, localPropertys.Key, helper, localPropertys.Value,
                                    false);
                            }
                        }
                    });
                }
                else
                {
                    foreach (var helper in helpers)
                    {
                        foreach (var localPropertys in helper.Propertys)
                        {
                            _ = RunUpdateSnapshotAsync(entity, localPropertys.Key, helper, localPropertys.Value, false);
                        }
                    }
                }
            });

            _monitor.ListenEntity(listen).Delete(en =>
            {
                var entity = en.TargetEntity;
                if (entity != null && SessionManager.Contains(entity))
                {
                    var session = SessionManager.Get(entity);
                    session?.SetSessionFunction(() =>
                    {
                        foreach (var helper in helpers)
                        {
                            foreach (var localPropertys in helper.Propertys)
                            {
                                _ = RunUpdateSnapshotAsync(entity, localPropertys.Key, helper, localPropertys.Value,
                                    true);
                            }
                        }
                    });
                }
                else if (entity is not null)
                {
                    foreach (var helper in helpers)
                    {
                        foreach (var localPropertys in helper.Propertys)
                        {
                            _ = RunUpdateSnapshotAsync(entity, localPropertys.Key, helper, localPropertys.Value, true);
                        }
                    }
                }
            });
        }
    }

    private async Task RunUpdateSnapshotAsync(Entity entity, string localField, LookupHelper helper,
        List<PropertyInfo> updateProperties, bool isDelete)
    {
        if (entity == null) return;
        if (_shutdown) return;
        var dedupKey = BuildDedupKey(entity, helper, updateProperties, isDelete);
        if (ShouldCoalesce(dedupKey)) return;
        if (!EnterGuard(entity)) return;
        try
        {

        var finder = CreateFinderAndCallByField(helper.LocalModel!, localField, entity.Id);
        if (finder == null) return;

        long count = GetCount(finder);

        if (count < _asyncThreshold)
        {
            var objs = await Task.Run(() => GetList(finder));
            UpdateSnapshot(entity, objs, helper, updateProperties, isDelete);
            return;
        }

        await UpdateSnapshotInBatchesAsync(entity, finder, helper, updateProperties, isDelete);
        }
        finally
        {
            LeaveGuard(entity);
        }
    }

    private async Task UpdateSnapshotInBatchesAsync(Entity entity, object finder, LookupHelper helper,
        List<PropertyInfo> updateProperties, bool isDelete)
    {
        var pageSize = _pageSize;
        int pageIndex = 0;

        while (true)
        {
            var page = await Task.Run(() => GetPage(finder, pageSize, pageIndex));
            if (page == null) return;

            var content = GetPageContent(page);
            if (!content.Any()) return;

            UpdateSnapshot(entity, content, helper, updateProperties, isDelete);

            if (content.Count < pageSize)
                return;

            pageIndex++;
        }
    }

    private object? CreateFinderAndCallByField(Type modelType, string fieldName, object value)
    {

        try
        {
            var finder=_finderMethodCache.GetOrAdd(modelType.FullName+"ByField",key=>
            {
                var finderType = typeof(Finder<>).MakeGenericType(modelType);
                var finder = Activator.CreateInstance(finderType);
                var byFieldFinderMethod = finderType.GetMethod("ByField", [typeof(string), typeof(object)]);
                if (finder is null || byFieldFinderMethod is null) throw new DomainException("[Lookup] Finder.ByField 解析失败:" + modelType.FullName);
                return new KeyValuePair<object, MethodInfo>(finder, byFieldFinderMethod);
            });
            return finder.Value?.Invoke(finder.Key, new[] { fieldName, value });
        }
        catch
        {
            return null;
        }
    }

    private long GetCount(object finder)
    {
        try
        {
            var countMethod = finder.GetType().GetMethod("Count");
            var result = countMethod?.Invoke(finder, null);
            if (result is long l) return l;
            if (result is int i) return i;
            return 0;
        }
        catch
        {
            return 0;
        }
    }

    private List<Entity> GetList(object finder)
    {
        try
        {
            var listMethod = finder.GetType().GetMethod("List");
            var result = listMethod?.Invoke(finder, null);
            if (result is System.Collections.IEnumerable enumerable)
                return enumerable.Cast<Entity>().ToList();
            return new List<Entity>();
        }
        catch
        {
            return new List<Entity>();
        }
    }

    private Page<Entity>? GetPage(object finder, int pageSize, int pageIndex)
    {
        try
        {
            var pageMethod = finder.GetType().GetMethod("Page", new[] { typeof(int), typeof(int) });
            return pageMethod?.Invoke(finder, new object[] { pageSize, pageIndex }) as Page<Entity>;
        }
        catch
        {
            return null;
        }
    }

    private Page<Entity>? GetPage(Type modelType, int pageSize, int pageIndex)
    {
        try
        {
            var finderType = typeof(Finder<>).MakeGenericType(modelType);
            var finder = Activator.CreateInstance(finderType);
            var pageMethod = finderType.GetMethod("Page", new[] { typeof(int), typeof(int) });
            return pageMethod?.Invoke(finder, new object[] { pageSize, pageIndex }) as Page<Entity>;
        }
        catch
        {
            return null;
        }
    }

    private List<Entity> GetPageContent(object page)
    {
        var result = new List<Entity>();
        try
        {
            var contentProperty = page.GetType().GetProperty("Content");
            var content = contentProperty?.GetValue(page);
            if (content is System.Collections.IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                {
                    if (item is Entity entity)
                        result.Add(entity);
                }
            }
        }
        catch
        {
        }

        return result;
    }

    private Entity? FindById(Type modelType, string id)
    {
        try
        {
            var finderType = typeof(Finder<>).MakeGenericType(modelType);
            var finder = Activator.CreateInstance(finderType);
            var byIdMethod = finderType.GetMethod("ById");
            var result = byIdMethod?.Invoke(finder, new[] { id });
            return result as Entity;
        }
        catch
        {
            return null;
        }
    }

    private object? GetDefaultValue(Type type)
    {
        if (type.IsValueType)
            return Activator.CreateInstance(type);
        return null;
    }

    private object? ReadPropertyValue(object target, PropertyDescriptor property)
    {
        try
        {
            return property.GetValue(target);
        }
        catch
        {
            return null;
        }
    }

    private void WritePropertyValue(object target, PropertyDescriptor property, object? value)
    {
        try
        {
            property.SetValue(target, value);
        }
        catch
        {
        }
    }

    internal void UpdateEntity(Type cla, Entity entity, IDictionary<Type, LookupHelper> helperMap)
    {
        if (entity == null) return;

        foreach (var map in helperMap)
        {
            var helper = map.Value;
            try
            {
                foreach (var localPropertys in helper.Propertys)
                {
                    try
                    {
                        var localProperty = GetPropertyDescriptor(cla, localPropertys.Key);
                        if (localProperty == null) continue;

                        string fromId = "";
                        Entity? fromEntity = null;

                        var value = ReadPropertyValue(entity, localProperty);
                        fromId = value?.ToString() ?? "";

                        if (!string.IsNullOrEmpty(fromId) && helper.FromModel != null)
                            fromEntity = FindById(helper.FromModel, fromId);

                        foreach (var field in localPropertys.Value)
                        {
                            try
                            {
                                var lookup = field.GetCustomAttribute<LookupAttribute>();
                                if (lookup == null) continue;

                                var targetProperty = GetPropertyDescriptor(helper.LocalModel!, field.Name);
                                if (targetProperty == null) continue;

                                if (fromEntity == null)
                                {
                                    WritePropertyValue(entity, targetProperty,
                                        GetDefaultValue(targetProperty.PropertyType));
                                }
                                else
                                {
                                    if (helper.FromModel is null) continue;
                                    var sourceProperty = GetPropertyDescriptor(helper.FromModel, lookup.FromField);
                                    if (sourceProperty != null)
                                    {
                                        var sourceValue = ReadPropertyValue(fromEntity, sourceProperty);
                                        WritePropertyValue(entity, targetProperty, sourceValue);
                                    }
                                }
                            }
                            catch
                            {
                            }
                        }
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
        }

        SaveSnapshot(entity);
    }

    private void UpdateSnapshot(Entity fromEntity, List<Entity> localEntities, LookupHelper helper,
        List<PropertyInfo> updateProperties, bool delete)
    {
        if (fromEntity == null || localEntities == null) return;

        foreach (var model in localEntities)
        {
            foreach (var property in updateProperties)
            {
                try
                {
                    var lookup = property.GetCustomAttribute<LookupAttribute>();
                    if (lookup == null) continue;

                    var targetProperty = GetPropertyDescriptor(helper.LocalModel!, property.Name);
                    if (targetProperty == null) continue;

                    if (delete)
                    {
                        WritePropertyValue(model, targetProperty, GetDefaultValue(targetProperty.PropertyType));
                    }
                    else
                    {
                        var sourceProperty = GetPropertyDescriptor(helper.FromModel!, lookup.FromField);
                        if (sourceProperty != null)
                        {
                            var sourceValue = ReadPropertyValue(fromEntity, sourceProperty);
                            WritePropertyValue(model, targetProperty, sourceValue);
                        }
                    }
                }
                catch
                {
                }
            }

            if (_bulkWriter is null || _bulkBatchSize <= 1)
            {
                SaveSnapshot(model);
            }
        }

        if (_bulkWriter is not null && _bulkBatchSize > 1 && helper.LocalModel is not null)
        {
            for (var i = 0; i < localEntities.Count; i += _bulkBatchSize)
            {
                var batch = localEntities.Skip(i).Take(_bulkBatchSize).ToList();
                if (batch.Count == 0) break;
                try
                {
                    _bulkWriter.BulkSaveSnapshots(helper.LocalModel, batch);
                }
                catch
                {
                    foreach (var model in batch) SaveSnapshot(model);
                }
            }
        }
    }

    private void SaveSnapshot(Entity? entity)
    {
        if (entity == null) return;
        var method = _domainRepository.GetType().GetMethod("SaveSnapshot")?.MakeGenericMethod(entity.GetType());
        ;
        method?.Invoke(_domainRepository, [entity]);
    }

    public class LookupHelper
    {
        public Type? LocalModel { get; set; }
        public Type? FromModel { get; set; }
        public Dictionary<string, List<PropertyInfo>> Propertys { get; } = new();

        public LookupHelper(Type cla)
        {
            LocalModel = cla;
        }

        public void AddField(string localProperty, PropertyInfo propertyInfo)
        {
            if (!Propertys.ContainsKey(localProperty))
                Propertys[localProperty] = new List<PropertyInfo>();
            Propertys[localProperty].Add(propertyInfo);
        }
    }
}
