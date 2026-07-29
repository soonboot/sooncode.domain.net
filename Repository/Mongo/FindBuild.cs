using System.Text.RegularExpressions;
using MongoDB.Bson;
using Domain.Infrastructure.Finder;
using Domain.Infrastructure.Model;
using System.ComponentModel;
using System.Linq;

namespace Domain.Infrastructure.Repository.Mongo;

public class FindBuild
{
    private FindHelper? _findHelper;
    private string _prefix = "";
    private Type? _tClass;
    private readonly Dictionary<string, PropertyDescriptor> _properties = new Dictionary<string, PropertyDescriptor>();

    private FindBuild()
    {
    }

    public static BsonDocument Build<T>(FindHelper findHelper, string prefix)
    {
        var build = new FindBuild();
        build._tClass = typeof(T);
        build._findHelper = findHelper;
        build._prefix = prefix;
        return build.BuildBson();
    }

    public static Dictionary<string, SortEnum>? Sort(Finder.Sort sort, string prefix)
    {
        if (sort == null) return null;

        var hashSort = sort.Get();
        var hashMap = new Dictionary<string, SortEnum>();

        foreach (var en in hashSort)
        {
            var sortValue = en.Value == Finder.Sort.SortType.asc ? SortEnum.ASC : SortEnum.DESC;
            hashMap[prefix + en.Key] = sortValue;
        }

        return hashMap;
    }

    public static BsonDocument TranslateField(string prefix, string fieldName, FindHelper.ValueType valueType)
    {
        var build = new FindBuild();
        var key = prefix + fieldName;
        var vt = valueType;
        return build.Build(vt);
    }

    private BsonDocument BuildBson()
    {
        if (_tClass != null)
        {
            var properties = GetBeanGetters(_tClass);
            foreach (var p in properties)
                _properties[p.Name] = p;
        }

        var bson = new BsonDocument();

        if (_findHelper == null) return bson;

        var rootCondition = _findHelper.GetRootCondition();
        if (rootCondition != null)
        {
            MergeInto(bson, rootCondition.ToBson(_prefix));
        }

        var andValues = Build(_findHelper.GetAndMap());
        var orValues = Build(_findHelper.GetOrMap());

        if (andValues.Count > 0)
            bson.Add("$and", andValues);
        if (orValues.Count > 0)
            bson.Add("$or", orValues);

        return bson;
    }

    private static void MergeInto(BsonDocument target, BsonDocument source)
    {
        foreach (var element in source)
        {
            if (target.Contains(element.Name))
            {
                if (element.Name == "$and" || element.Name == "$or")
                {
                    var existing = target[element.Name].AsBsonArray;
                    foreach (var item in element.Value.AsBsonArray)
                        existing.Add(item);
                }
            }
            else
            {
                target[element.Name] = element.Value;
            }
        }
    }

    private PropertyDescriptor[] GetBeanGetters(Type type)
    {
        return TypeDescriptor.GetProperties(type).Cast<PropertyDescriptor>().ToArray();
    }

    private BsonArray Build(IDictionary<string, List<FindHelper.ValueType>> fields)
    {
        var values = new BsonArray();

        if (fields == null) return values;

        foreach (var field in fields)
        {
            var vtList = field.Value;
            if (vtList == null || vtList.Count == 0) continue;

            if (vtList.Count == 1)
            {
                if (vtList[0].Type == null)
                    values.Add(new BsonDocument(GetKey(field), vtList[0].Value?.ToString()));
                else
                    values.Add(new BsonDocument(GetKey(field), Build(vtList[0])));
            }
            else
            {
                foreach (var vt in vtList)
                {
                    if (vt.Type == null)
                        values.Add(new BsonDocument(GetKey(field), vt.Value?.ToString()));
                    else
                        values.Add(new BsonDocument(GetKey(field), Build(vt)));
                }
            }
        }

        return values;
    }

    private BsonDocument Build(FindHelper.ValueType vt)
    {
        var bson = new BsonDocument();

        if (vt.Type == null)
            return bson;

        var type = vt.Type.Value;

        if (type == Finder.OType.contains)
        {
            var escapedValue = Regex.Escape(vt.Value?.ToString() ?? "");
            bson.Add(O(type), "^.*" + escapedValue + ".*$");
        }
        else
            bson.Add(O(type), vt.Value?.ToString() ?? "");

        return bson;
    }

    private string O(Finder.OType type)
    {
        return type switch
        {
            Finder.OType.eq => "$eq",
            Finder.OType.neq => "$ne",
            Finder.OType.gt => "$gt",
            Finder.OType.gte => "$gte",
            Finder.OType.lt => "$lt",
            Finder.OType.lte => "$lte",
            Finder.OType.@in => "$in",
            Finder.OType.nin => "$nin",
            Finder.OType.contains => "$regex",
            _ => "$eq"
        };
    }

    private string GetKey(KeyValuePair<string, List<FindHelper.ValueType>> field)
    {
        var key = field.Key.Split('.')[0];
        if (!_properties.TryGetValue(key, out var property))
            throw new DomainException("没有找到对应的字段名：" + key);

        var propertyType = property.PropertyType;
        if (propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(ValueObject<>))
        {
            return _prefix + field.Key + ".value";
        }
        else
            return _prefix + field.Key;
    }
}
