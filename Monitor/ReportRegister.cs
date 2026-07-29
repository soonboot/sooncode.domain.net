using Domain.Infrastructure.Model;

namespace Domain.Infrastructure.Monitor;

public class ReportRegister
{
    private readonly Dictionary<Type, object> _repoMap = new Dictionary<Type, object>();
    
    internal ReportRegister()
    {
    }
    
    public ReportRegister Add<T>(Type modelClass, IDomainReportRepository<T> repository) where T : DomainModel<T>
    {
        new GenerateReport<T>(modelClass, repository);
        return this;
    }
}
