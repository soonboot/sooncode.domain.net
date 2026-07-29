using Domain.Infrastructure.Annotations;
using Domain.Infrastructure.Finder;
using Domain.Infrastructure.Repository.Mongo;
using Domain.Infrastructure.Session;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;

namespace Domain.Infrastructure.Model;

public class LookupHandler
{
    private readonly IEventSourcingRepository? _sourceRepo;
    private readonly Monitor.Monitor _monitor = Monitor.Monitor.Instance;
    private readonly IDomainRepository _domainRepository;
    private readonly ConcurrentDictionary<Type, List<LookupHelper>> _lookupMap = new();
    private readonly ConcurrentDictionary<Type, string> _snapshotNames = new();
    private readonly ConcurrentDictionary<string, KeyValuePair<object,MethodInfo>> _finderMethodCache = new();

    public LookupHandler(string assemblyName, string targetNamespace, IDomainRepository domainRepository)
    {
        _domainRepository = domainRepository;
        _sourceRepo = MongoSingle.GetInstance().GetRepository();
        var classList = GetClassListByAnnotation(assemblyName, targetNamespace, typeof(LookupModelAttribute));
        _lookupMap.Clear();
        _ = ReBuildSnapshotAsync(classList);
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
        MonitorEntity();
    }

    private async Task UpdateEntitiesInBackgroundAsync(Type cla, IDictionary<Type, LookupHelper> helperMap)
    {
        const int pageSize = 100;
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

    private PropertyDescriptor? GetPropertyDescriptor(Type type, string propertyName)
    {
        var properties = TypeDescriptor.GetProperties(type);
        return properties[propertyName];
    }

    private void MonitorEntity(Type cla, IDictionary<Type, LookupHelper> helperMap)
    {
        _monitor.ListenEntity(cla).Add(en =>
        {
            var entity = en.TargetEntity;
            if (SessionManager.Contains(entity))
            {
                var session = SessionManager.Get(entity);
                session?.SetSessionFunction(() => UpdateEntity(cla, entity, helperMap));
            }
            else
            {
                UpdateEntity(cla, entity, helperMap);
            }
        });

        _monitor.ListenEntity(cla).Modify(en =>
        {
            var entity = en.TargetEntity;
            if (SessionManager.Contains(entity))
            {
                var session = SessionManager.Get(entity);
                session?.SetSessionFunction(() => UpdateEntity(cla, entity, helperMap));
            }
            else
            {
                UpdateEntity(cla, entity, helperMap);
            }
        });
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
                else
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

        var finder = CreateFinderAndCallByField(helper.LocalModel!, localField, entity.Id);
        if (finder == null) return;

        long count = GetCount(finder);

        if (count < 10)
        {
            var objs = await Task.Run(() => GetList(finder));
            UpdateSnapshot(entity, objs, helper, updateProperties, isDelete);
            return;
        }

        await UpdateSnapshotInBatchesAsync(entity, finder, helper, updateProperties, isDelete);
    }

    private async Task UpdateSnapshotInBatchesAsync(Entity entity, object finder, LookupHelper helper,
        List<PropertyInfo> updateProperties, bool isDelete)
    {
        const int pageSize = 100;
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

            SaveSnapshot(model);
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
