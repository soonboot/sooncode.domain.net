using System.Reflection;
using Domain.Infrastructure.Annotations;
using Domain.Infrastructure.Monitor;

namespace Domain.Infrastructure.Model;

public class GenerateReport<T> : IGenerateReport<T> where T : DomainModel<T>
{
    private readonly Monitor.Monitor _monitor = Monitor.Monitor.Instance;
    private readonly IDomainReportRepository<T>? _repository;
    private readonly Type? _reportClass;

    private GenerateReport()
    {
        _repository = null;
        _reportClass = null;
    }

    public GenerateReport(Type tClass, IDomainReportRepository<T> repository)
    {
        _repository = repository;
        _reportClass = tClass;

        if (!tClass.IsDefined(typeof(DomainReportAttribute), true)) return;

        var annotation = (DomainReportAttribute?)tClass.GetCustomAttribute(typeof(DomainReportAttribute));
        var modelClass = annotation?.Model;

        if (modelClass != null)
        {
            _monitor.ListenEntity(modelClass)
                .Add(en =>
                {
                    if (en.TargetEntity is DomainModel<T> tEntity)
                        Add(tEntity);
                })
                .Modify(en =>
                {
                    if (en.TargetEntity is DomainModel<T> tEntity)
                        Modify(tEntity);
                })
                .Delete(en =>
                {
                    if (en.TargetEntity is DomainModel<T> tEntity)
                        Delete(tEntity);
                });
        }

    }
    private T GetModel(DomainModel<T> obj)
    {
        if (_reportClass == null) return null;

        T? model = default;

        try
        {
            model = (T)Activator.CreateInstance(_reportClass);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }

        if (model is IDomainReportModel<T> reportModel)
        {
            model = (T)reportModel.GetModel(obj);
        }
        else if (model != null)
        {
            obj.ToEntity(model);
        }

        return model;
    }

    public void Add(DomainModel<T> obj)
    {
        var model = GetModel(obj);
        if (model != null && _repository != null)
        {
            _repository.Add(model);
        }
    }

    public void Modify(DomainModel<T> obj)
    {
        var model = GetModel(obj);
        if (model != null && _repository != null)
        {
            _repository.Modify(model);
        }
    }

    public void Delete(DomainModel<T> obj)
    {
        if (_reportClass == null || _repository == null) return;

        object? report = null;

        try
        {
            report = Activator.CreateInstance(_reportClass);
            var method = _reportClass.GetMethod("setId", new[] { typeof(string) });
            method?.Invoke(report, new[] { obj.Id });
        }
        catch (Exception)
        {
        }

        if (report != null)
        {
            _repository.Delete((T)report);
        }
    }

    public bool Clear()
    {
        if (_repository != null)
        {
            return _repository.Clear();
        }
        return false;
    }
}
