namespace Domain.Infrastructure.Model;

public interface IDBConnection
{
    IEventSourcingRepository? GetRepository();
}
