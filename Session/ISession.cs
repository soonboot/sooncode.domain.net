using Domain.Infrastructure.Model;

namespace Domain.Infrastructure.Session;

public interface ISession
{
    void Add(Entity entity);
    void SetSessionFunction(Action func);
    void Complete();
    void Rollback();
    List<Entity> GetEntitys();
    void OnSuccess(ISessionComplete function);
}
