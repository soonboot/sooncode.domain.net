using MongoDB.Bson;
using Domain.Infrastructure.Finder;
using Domain.Infrastructure.Model;
using Domain.Infrastructure.Utils;
using Finder = Domain.Infrastructure.Finder;

namespace Domain.Infrastructure.Repository.Mongo;

public class FindRepository<T> : IFindRepository<T> where T : DomainModel<T>
{
    private readonly string _eventSnapshot = "eventSnapshot";
    private readonly IMongoDBDao _dao;
    private readonly string _dbName;

    public FindRepository()
    {
        _dao = MongoSingle.GetInstance().MongoDB!;
        _dbName = MongoSingle.GetInstance().DbName ?? "";
    }

    public T? First(FindHelper findHelper, Finder.Sort? sort)
    {
        var col = _dao.GetCollection(_dbName, _eventSnapshot);
        var bson = FindBuild.Build<T>(findHelper, "snapshot.");
        bson.Add("snapshotType", typeof(T).FullName);

        var sortMap = new Dictionary<string, SortEnum>();
        if (sort != null && sort.Get().Count > 0)
            sortMap = FindBuild.Sort(sort, "snapshot.") ?? new Dictionary<string, SortEnum>();
        sortMap["streamId"] = SortEnum.ASC;

        var doc = _dao.FindFirst(col, bson, sortMap);
        if (doc == null) return null;

        try
        {
            if (doc.Contains("snapshot"))
            {
                var snapshot = doc["snapshot"].AsBsonDocument;
                var entity = MongoJsonUtil.ConvertToObject<T>(snapshot);
                return entity;
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    public List<T?> List(FindHelper findHelper, Finder.Sort? sort)
    {
        var col = _dao.GetCollection(_dbName, _eventSnapshot);
        var bson = FindBuild.Build<T>(findHelper, "snapshot.");
        bson.Add("snapshotType", typeof(T).FullName);

        var sortMap = new Dictionary<string, SortEnum>();
        if (sort != null && sort.Get().Count > 0)
            sortMap = FindBuild.Sort(sort, "snapshot.") ?? new Dictionary<string, SortEnum>();
        sortMap["createDate"] = SortEnum.DESC;

        var result = new List<T?>();

        var docs = _dao.FindSync(col, bson, sortMap);
        foreach (var doc in docs)
        {
            try
            {
                if (doc.Contains("snapshot"))
                {
                    var snapshot = doc["snapshot"].AsBsonDocument;
                    var entity = MongoJsonUtil.ConvertToObject<T>(snapshot);
                    result.Add(entity);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        return result;
    }

    public List<T> Top(FindHelper findHelper, int num, Finder.Sort? sort)
    {
        var col = _dao.GetCollection(_dbName, _eventSnapshot);
        var bson = FindBuild.Build<T>(findHelper, "snapshot.");
        bson.Add("snapshotType", typeof(T).FullName);

        var sortMap = new Dictionary<string, SortEnum>();
        if (sort != null && sort.Get().Count > 0)
            sortMap = FindBuild.Sort(sort, "snapshot.") ?? new Dictionary<string, SortEnum>();
        sortMap["createDate"] = SortEnum.DESC;

        var result = new List<T>();

        var docs = _dao.FindTopSync(col, bson, sortMap, num);
        foreach (var doc in docs)
        {
            try
            {
                if (doc.Contains("snapshot"))
                {
                    var snapshot = doc["snapshot"].AsBsonDocument;
                    var entity = MongoJsonUtil.ConvertToObject<T>(snapshot);
                    result.Add(entity);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.StackTrace);
            }
        }

        return result;
    }

    public Finder.Page<T> Page(FindHelper findHelper, int pageSize, int pageIndex, Finder.Sort? sort)
    {
        var col = _dao.GetCollection(_dbName, _eventSnapshot);
        var bson = FindBuild.Build<T>(findHelper, "snapshot.");
        bson.Add("snapshotType", typeof(T).FullName);

        var sortMap = new Dictionary<string, SortEnum>();
        if (sort != null && sort.Get().Count > 0)
            sortMap = FindBuild.Sort(sort, "snapshot.") ?? new Dictionary<string, SortEnum>();
        sortMap["createDate"] = SortEnum.DESC;

        var result = new List<T>();

        var docs = _dao.FindByPageSync(col, bson, pageIndex, pageSize, sortMap);
        foreach (var doc in docs)
        {
            try
            {
                if (doc.Contains("snapshot"))
                {
                    var snapshot = doc["snapshot"].AsBsonDocument;
                    var entity = MongoJsonUtil.ConvertToObject<T>(snapshot);
                    result.Add(entity!);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.StackTrace);
            }
        }

        var total = _dao.Count(col, bson);

        return new Finder.Page<T>
        {
            PageSize = pageSize,
            PageIndex = pageIndex,
            TotalElements = total,
            Content = result
        };
    }

    public long Count(FindHelper findHelper)
    {
        var col = _dao.GetCollection(_dbName, _eventSnapshot);
        var bson = FindBuild.Build<T>(findHelper, "snapshot.");
        bson.Add("snapshotType", typeof(T).FullName);
        return _dao.Count(col, bson);
    }

    public IDictionary<string, long> Count(FindHelper findHelper, string[] groupField)
    {
        var col = _dao.GetCollection(_dbName, _eventSnapshot);
        var query = FindBuild.Build<T>(findHelper, "snapshot.");
        query.Add("snapshotType", typeof(T).FullName);

        var group = BuildGroup(groupField);

        var result = new Dictionary<string, long>();

        var docs = _dao.StatisticsSync(col, query, "1", group, "sum");
        foreach (var doc in docs)
        {
            if (doc.Contains("_id") && doc.Contains("sum"))
            {
                var key = doc["_id"].ToString();
                var value = doc["sum"].ToInt64();
                result[key!] = value;
            }
        }

        return result;
    }

    public IDictionary<string, T?> Map(FindHelper findHelper, string fieldKey)
    {
        var col = _dao.GetCollection(_dbName, _eventSnapshot);
        var bson = FindBuild.Build<T>(findHelper, "snapshot.");
        bson.Add("snapshotType", typeof(T).FullName);

        var sortMap = new Dictionary<string, SortEnum>();

        var result = new Dictionary<string, T?>();

        var docs = _dao.FindSync(col, bson, sortMap);
        foreach (var doc in docs)
        {
            try
            {
                if (doc.Contains("snapshot"))
                {
                    var snapshot = doc["snapshot"].AsBsonDocument;
                    var entity = MongoJsonUtil.ConvertToObject<T>(snapshot);
                    var key = GetFieldValue(entity, fieldKey);
                    if (key != null)
                        result[key] = entity;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.StackTrace);
            }
        }

        return result;
    }

    private static string? GetFieldValue(T? entity, string fieldKey)
    {
        if (entity == null) return null;
        var prop = typeof(T).GetProperty(fieldKey);
        if (prop == null) return null;
        var value = prop.GetValue(entity);
        return value?.ToString();
    }

    public IDictionary<string, double> Sum(FindHelper findHelper, string field, string[] groupField)
    {
        return Statistic(findHelper, field, groupField, "sum");
    }

    public IDictionary<string, double> Avg(FindHelper findHelper, string field, string[] groupField)
    {
        return Statistic(findHelper, field, groupField, "avg");
    }

    public IDictionary<string, double> Max(FindHelper findHelper, string field, string[] groupField)
    {
        return Statistic(findHelper, field, groupField, "max");
    }

    public IDictionary<string, double> Min(FindHelper findHelper, string field, string[] groupField)
    {
        return Statistic(findHelper, field, groupField, "min");
    }

    public List<TResult> Distinct<TResult>(FindHelper findHelper, string field)
    {
        var col = _dao.GetCollection(_dbName, _eventSnapshot);
        var bson = FindBuild.Build<T>(findHelper, "snapshot.");
        bson.Add("snapshotType", typeof(T).FullName);

        var result = _dao.Distinct(col, bson, "snapshot." + field).Result;
        return result.Cast<TResult>().ToList();
    }

    private Dictionary<string, string> BuildGroup(string[]? groupField)
    {
        var group = new Dictionary<string, string>();
        if (groupField == null || groupField.Length == 0) return group;

        foreach (var s in groupField)
        {
            group[s] = "$snapshot." + s;
        }
        return group;
    }

    private Dictionary<string, double> Statistic(FindHelper findHelper, string field, string[] groupField, string statisticType)
    {
        var col = _dao.GetCollection(_dbName, _eventSnapshot);
        var query = FindBuild.Build<T>(findHelper, "snapshot.");
        query.Add("snapshotType", typeof(T).FullName);

        var group = BuildGroup(groupField);

        var result = new Dictionary<string, double>();

        var docs = _dao.StatisticsSync(col, query, "$snapshot." + field, group, statisticType);
        foreach (var doc in docs)
        {
            if (doc.Contains("_id") && doc.Contains(statisticType))
            {
                var key = doc["_id"].ToString();
                var value = doc[statisticType].ToDouble();
                result[key!] = value;
            }
        }

        return result;
    }
}
