using Domain.Infrastructure.Model;

namespace Domain.Infrastructure.Session;

public interface ISessionComplete
{
    void Run(List<Entity> entities);
}
