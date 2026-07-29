using Domain.Infrastructure.Model;
using System.Collections.Generic;

namespace Domain.Infrastructure.Finder;

public interface IFindRepository<T> where T : DomainModel<T>
{
    T? First(FindHelper fields, Sort sort);
    List<T?> List(FindHelper fields, Sort sort);
    IDictionary<string, T?> Map(FindHelper fields, string fieldKey);
    List<T> Top(FindHelper fields, int num, Sort sort);
    Page<T> Page(FindHelper fields, int pageSize, int pageIndex, Sort sort);
    long Count(FindHelper fields);
    IDictionary<string, long> Count(FindHelper fields, string[] groupField);
    List<TResult> Distinct<TResult>(FindHelper fields, string field);
    IDictionary<string, double> Sum(FindHelper fields, string field, string[] groupField);
    IDictionary<string, double> Avg(FindHelper fields, string field, string[] groupField);
    IDictionary<string, double> Max(FindHelper fields, string field, string[] groupField);
    IDictionary<string, double> Min(FindHelper fields, string field, string[] groupField);
}
