using Domain.Infrastructure.Finder;
using Domain.Infrastructure.Monitor;

namespace Domain.Infrastructure.Model;

/// <summary>
/// 基础领域模型存储库
/// </summary>
public interface IDomainRepository
{
    DomainModel<T> FindById<T>(string id) where T : DomainModel<T>;
    void Add<T>(DomainModel<T> entity) where T : DomainModel<T>;
    void Add<T>(DomainModel<T> entity, IGenerateReport<T> report) where T : DomainModel<T>;
    void Add<T>(DomainModel<T> entity, IGenerateReport<T> report, bool monitor) where T : DomainModel<T>;
    void Save<T>(DomainModel<T> entity) where T : DomainModel<T>;
    void Save<T>(DomainModel<T> entity, IGenerateReport<T> report) where T : DomainModel<T>;
    void Save<T>(DomainModel<T> entity, IGenerateReport<T> report, bool monitor) where T : DomainModel<T>;
    void Delete<T>(DomainModel<T> entity) where T : DomainModel<T>;
    void Delete<T>(DomainModel<T> entity, IGenerateReport<T> report) where T : DomainModel<T>;
    void Delete<T>(DomainModel<T> entity, IGenerateReport<T> report, bool monitor) where T : DomainModel<T>;
    Page<EventWrapper> GetEventStream<T>(Creater creater, int pageSize, int pageIndex) where T : DomainModel<T>;
    DomainModel<T> Replay<T>(string id, int toVersion) where T : DomainModel<T>;
    void SaveSnapshot<T>(DomainModel<T> entity) where T : DomainModel<T>;
    void DeleteSnapshot<T>(DomainModel<T> entity) where T : DomainModel<T>;
    IEnumerable<DomainModel<T>> GetSnapshotList<T>() where T : DomainModel<T>;
}
