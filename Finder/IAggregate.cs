namespace Domain.Infrastructure.Finder;

public interface IAggregate
{
    object Group(string[] fields);
}
