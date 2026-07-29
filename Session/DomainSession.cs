using System.Collections.Concurrent;
using Domain.Infrastructure.Model;

namespace Domain.Infrastructure.Session;

public class DomainSession : ISession
{
    private readonly ConcurrentQueue<Action> _functions = new ConcurrentQueue<Action>();
    private readonly List<Entity> _entities = new List<Entity>();
    private ISessionComplete? _successFunction;
    
    public DomainSession()
    {
    }
    
    public void Add(Entity entity)
    {
        SessionManager.Put(entity, this);
        _entities.Add(entity);
    }
    
    public void SetSessionFunction(Action func)
    {
        _functions.Enqueue(func);
    }
    
    public void Complete()
    {
        while (_functions.TryDequeue(out var func))
        {
            func();
        }
        
        foreach (var entity in _entities)
        {
            SessionManager.Remove(entity);
        }
        
        _successFunction?.Run(_entities);
        _entities.Clear();
    }
    
    public void Rollback()
    {
        foreach (var entity in _entities)
        {
            SessionManager.Remove(entity);
        }
        _entities.Clear();
    }
    
    public List<Entity> GetEntitys()
    {
        return _entities;
    }
    
    public void OnSuccess(ISessionComplete function)
    {
        _successFunction = function;
    }
}
