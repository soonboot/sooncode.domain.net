namespace Domain.Infrastructure.Model;

public interface IDomainReportModel<T> where T : DomainModel<T>
{
    DomainModel<T> GetModel(DomainModel<T> entity);
}
