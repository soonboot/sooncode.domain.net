using System.Reflection;
using Domain.Infrastructure.Annotations;
using Domain.Infrastructure.Batcher;
using Domain.Infrastructure.Model;
using Domain.Infrastructure.Session;

namespace Domain.Infrastructure.Monitor;

/// <summary>
/// 事件驱动的持久化分发，与 Java 版 <c>monitor.StoreNotice</c> 对齐。
/// <para>.NET 规范：file-scoped namespace + XML docs + <c>ArgumentNullException.ThrowIfNull</c>。</para>
/// <para>分发顺序：批量捕获 → 会话延期 → 单条直写；会话/批量内不做单条持久化。</para>
/// </summary>
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

    /// <summary>按 <see cref="EventBootAttribute.StoreFunc"/> 分发持久化。</summary>
    public void Notice<T>(DomainModel<T> entity, EventBootAttribute? annotation) where T : DomainModel<T>
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (annotation is null || _domainRepository is null) return;

        // 批量作用域内按仓储维度捕获，延迟到 Execute 统一提交。
        if (BatcherCapture.TryCapture(entity, annotation.StoreFunc, _domainRepository)) return;

        // 会话内：事件驱动的写必须延期到 Commit，而不是直接落库。
        if (SessionManager.Contains(entity))
        {
            var session = SessionManager.Get(entity);
            if (session is DomainSession ds)
            {
                if (entity.__stored__) return;
                var type = ToOperationType(annotation.StoreFunc);
                if (type is not null)
                {
                    var skipES = IsSkipEventSourcing(entity);
                    int? expected = type == BatchOperation.OperationType.Add || entity.__startVersion__ == 0
                        ? null
                        : entity.__startVersion__;
                    ds.AddOperation(new BatchOperation(type.Value, entity, null, null, expected, skipES, true));
                    return;
                }
                // replay/none：保持原直写语义（会话批量仅支持 ADD/MODIFY/DELETE）。
            }
            else if (session is not null)
            {
                switch (annotation.StoreFunc)
                {
                    case FuncType.add:
                        session.SetSessionFunction(() => _domainRepository.Add(entity));
                        return;
                    case FuncType.modify:
                        session.SetSessionFunction(() => _domainRepository.Save(entity));
                        return;
                    case FuncType.delete:
                        session.SetSessionFunction(() => _domainRepository.Delete(entity));
                        return;
                    default:
                        break;
                }
            }
        }

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
                _domainRepository.Replay<T>(entity.Id, entity.GetVersion());
                break;
            case FuncType.none:
                return;
            default:
                throw new DomainException("请求的操作类型有误:" + annotation.StoreFunc);
        }
    }

    private static BatchOperation.OperationType? ToOperationType(FuncType funcType) => funcType switch
    {
        FuncType.add => BatchOperation.OperationType.Add,
        FuncType.modify => BatchOperation.OperationType.Modify,
        FuncType.delete => BatchOperation.OperationType.Delete,
        _ => null,
    };

    private static bool IsSkipEventSourcing(Entity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        var attr = entity.GetType().GetCustomAttribute<SkipEventSourcingAttribute>(inherit: true);
        return attr is not null && attr.Value;
    }
}
