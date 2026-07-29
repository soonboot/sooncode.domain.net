using Domain.Infrastructure.Annotations;
using Domain.Infrastructure.Model;

namespace Domain.Infrastructure.Monitor;

public class Monitor
{
    private static Monitor? _instance;

    public static Monitor Instance => _instance ??= new Monitor();

    public static Monitor New() => Instance;

    private ICreaterGetter? _createrGetter;
    private IDomainRepository? _domainRepository;
    private IEventStore? _eventStore;
    private StoreNotice? _storeNotice;
    private ReportRegister? _reportRegister;
    private readonly EntityNotice _entityNotice = new EntityNotice();
    private readonly EventNotice _eventNotice = new EventNotice();

    private Monitor() {}

    public ICreaterGetter? GetCreaterGetter()
    {
        return _createrGetter;
    }

    public void Store<T>(DomainModel<T> entity, EventBootAttribute? annotation) where T : DomainModel<T>
    {
        if (_eventStore != null && _storeNotice != null)
        {
            _storeNotice.Notice<T>(entity, annotation);
        }
    }

    public void Notice<T>(DomainModel<T> entity, FuncType funcType) where T : DomainModel<T>
    {
        _entityNotice.Notice<T>(entity, funcType);
    }

    public void Notice<T>(DomainModel<T> entity, DomainModel<T> oldEntity, FuncType funcType) where T : DomainModel<T>
    {
        _entityNotice.Notice<T>(entity, oldEntity, funcType);
    }

    public void Notice<T>(DomainEvent @event, DomainModel<T> entity) where T : DomainModel<T>
    {
        _eventNotice.Notice<T>(@event, entity);
    }

    public EventNotice.EventNoticeTrigger ListenEvent(Type cla)
    {
        return _eventNotice.Listen(cla);
    }

    public EntityNotice.EntityNoticeTrigger ListenEntity(Type cla)
    {
        return _entityNotice.Listen(cla);
    }

    public void ConfigCreater(ICreaterGetter listen)
    {
        _createrGetter = listen;
    }

    public void ConfigDBConnection(IDBConnection dbConnection)
    {
        var eventRepository = dbConnection.GetRepository();
        _eventStore=new EventStore(eventRepository);
        _domainRepository = new DomainRepository(_eventStore);
        _storeNotice = new StoreNotice(_domainRepository);
    }

    public ReportRegister RegisterReport<T>(Type modelClass, IDomainReportRepository<T> repository) where T : DomainModel<T>
    {
        if (_reportRegister == null)
            _reportRegister = new ReportRegister();
        return _reportRegister.Add(modelClass, repository);
    }

    public void RegisterLookupModel(string assemblyName,string targetNamespace)
    {
        new LookupHandler(assemblyName,targetNamespace,_domainRepository);
    }
    public IEventStore GetEventStore()
    {
        return _eventStore;
    }

    public IDomainRepository? GetDomainRepository()
    {
        return _domainRepository;
    }
}

