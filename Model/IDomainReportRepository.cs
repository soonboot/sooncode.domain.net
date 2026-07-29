namespace Domain.Infrastructure.Model;

public interface IDomainReportRepository<T> where T : DomainModel<T>
{
    void Add(T entity);
    void Modify(T entity);
    void Delete(T entity);
    bool Clear();
    IEnumerable<T> GetAll();
}
