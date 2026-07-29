using Domain.Infrastructure.Model;

namespace Domain.Infrastructure.Generic;

public class RebuildReport
{
    private enum Operator
    {
        add,
        modify,
        delete
    }

    private int _sort = 0;
    private readonly Dictionary<int, Type> _entityMap = new Dictionary<int, Type>();
    private readonly Dictionary<int, object> _respositoryMap = new Dictionary<int, object>();
    private readonly Dictionary<int, (object Report, Operator Op)> _reportMap = new Dictionary<int, (object, Operator)>();

    public RebuildReport()
    {
    }

    public static RebuildReport New()
    {
        return new RebuildReport();
    }

    public RebuildReport Add(Type tClass, object repository, object report)
    {
        _entityMap[_sort] = tClass;
        _respositoryMap[_sort] = repository;
        _reportMap[_sort] = (report, Operator.add);
        _sort++;
        return this;
    }

    public RebuildReport Modify(Type tClass, object repository, object report)
    {
        _entityMap[_sort] = tClass;
        _respositoryMap[_sort] = repository;
        _reportMap[_sort] = (report, Operator.modify);
        _sort++;
        return this;
    }

    public RebuildReport Delete(Type tClass, object repository, object report)
    {
        _entityMap[_sort] = tClass;
        _respositoryMap[_sort] = repository;
        _reportMap[_sort] = (report, Operator.delete);
        _sort++;
        return this;
    }

    public void Build()
    {
        for (int i = 0; i < _sort; i++)
        {
            var entityType = _entityMap[i];
            var repository = _respositoryMap[i];
            var (report, op) = _reportMap[i];

            var snapshotList = GetSnapshotList(repository, entityType);
            if (snapshotList == null) continue;

            foreach (var entity in snapshotList)
            {
                switch (op)
                {
                    case Operator.add:
                        CallAdd(report, entity);
                        break;
                    case Operator.modify:
                        CallModify(report, entity);
                        break;
                    case Operator.delete:
                        CallDelete(report, entity);
                        break;
                }
            }
        }
    }

    private void CallAdd(object report, object entity)
    {
        var method = report.GetType().GetMethod("Add", new[] { typeof(object) });
        method?.Invoke(report, new[] { entity });
    }

    private void CallModify(object report, object entity)
    {
        var method = report.GetType().GetMethod("Modify", new[] { typeof(object) });
        method?.Invoke(report, new[] { entity });
    }

    private void CallDelete(object report, object entity)
    {
        var method = report.GetType().GetMethod("Delete", new[] { typeof(object) });
        method?.Invoke(report, new[] { entity });
    }

    public void ReGenerate(Type tClass, object report, object repository)
    {
        var clearMethod = report.GetType().GetMethod("Clear");
        var clearResult = clearMethod?.Invoke(report, null) as bool?;
        if (clearResult != true)
            throw new DomainException("清空报告错误");
        AppendGenerate(tClass, report, repository);
    }

    public void AppendGenerate(Type tClass, object report, object repository)
    {
        var list = GetSnapshotList(repository, tClass);
        if (list == null) return;

        foreach (var en in list)
        {
            CallAdd(report, en);
        }
    }

    private IEnumerable<object>? GetSnapshotList(object repository, Type entityType)
    {
        var method = repository.GetType().GetMethod("GetSnapshotList", new[] { typeof(Type) });
        if (method != null)
        {
            var result = method.Invoke(repository, new[] { entityType });
            if (result is IEnumerable<object> list)
                return list;
        }
        return null;
    }
}