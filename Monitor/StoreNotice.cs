using Domain.Infrastructure.Annotations;
using Domain.Infrastructure.Model;

namespace Domain.Infrastructure.Monitor;

public class StoreNotice
{
    private readonly IDomainRepository? _domainRepository;

    public StoreNotice()
    {
    }

    public StoreNotice(IDomainRepository repository)
    {
        _domainRepository = repository;
    }

    public void Notice<T>(DomainModel<T> entity, EventBootAttribute? annotation) where T : DomainModel<T>
    {
        if (annotation == null || _domainRepository == null) return;
        switch (annotation.StoreFunc)
        {
            case FuncType.add:
                _domainRepository.Add<T>(entity);
                break;
            case FuncType.modify:
                _domainRepository.Save<T>(entity);
                break;
            case FuncType.delete:
                _domainRepository.Delete<T>(entity);
                break;
            case FuncType.replay:
                int version = GetEntityVersion(entity);
                _domainRepository.Replay<T>(entity.Id, version);
                break;
            case FuncType.none:
                return;
            default:
                throw new DomainException("请求的操作类型有误:" + annotation.StoreFunc);
        }
    }

    private int GetEntityVersion(Entity entity)
    {
        try
        {
            var method = entity.GetType().GetMethod("GetVersion");
            if (method != null)
            {
                var result = method.Invoke(entity, null);
                if (result != null)
                    return (int)result;
            }
        }
        catch { }
        return 0;
    }
}
