using System.Linq;
using MongoDB.Bson;
using Domain.Infrastructure.Model;

namespace Domain.Infrastructure.Finder;

public abstract class ConditionNode
{
    public abstract BsonDocument ToBson(string prefix);

    public class FieldCondition : ConditionNode
    {
        public string FieldName { get; }
        public FindHelper.ValueType ValueType { get; }

        public FieldCondition(string fieldName, FindHelper.ValueType valueType)
        {
            FieldName = fieldName;
            ValueType = valueType;
        }

        public override BsonDocument ToBson(string prefix)
        {
            return Repository.Mongo.FindBuild.TranslateField(prefix, FieldName, ValueType);
        }
    }

    public class AndNode : ConditionNode
    {
        public List<ConditionNode> Children { get; } = new List<ConditionNode>();

        public void Add(ConditionNode child)
        {
            if (child != null)
                Children.Add(child);
        }

        public override BsonDocument ToBson(string prefix)
        {
            if (Children.Count == 0)
                throw new DomainException("and group cannot be empty");

            if (Children.Count == 1)
                return Children[0].ToBson(prefix);

            var arr = new BsonArray(Children.Select(c => c.ToBson(prefix)));
            return new BsonDocument("$and", arr);
        }
    }

    public class OrNode : ConditionNode
    {
        public List<ConditionNode> Children { get; } = new List<ConditionNode>();

        public void Add(ConditionNode child)
        {
            if (child != null)
                Children.Add(child);
        }

        public override BsonDocument ToBson(string prefix)
        {
            if (Children.Count == 0)
                throw new DomainException("or group cannot be empty");

            if (Children.Count == 1)
                return Children[0].ToBson(prefix);

            var arr = new BsonArray(Children.Select(c => c.ToBson(prefix)));
            return new BsonDocument("$or", arr);
        }
    }
}
