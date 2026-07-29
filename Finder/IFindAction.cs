using Domain.Infrastructure.Model;
using System.Collections.Generic;

namespace Domain.Infrastructure.Finder;

public interface IFindAction<T> where T : DomainModel<T>
{
    T? First(Sort sort);
    T? First();
    List<T> List(Sort sort);
    List<T> List();
    IDictionary<string, T> Map(string fieldKey);
    List<T> Top(int num, Sort sort);
    List<T> Top(int num);
    Page<T> Page(int pageSize, int pageIndex, Sort sort);
    Page<T> Page(int pageSize, int pageIndex);
    long Count();
    IDictionary<string, long> Count(string[] groupField);
    List<TResult> Distinct<TResult>(string field);
    IDictionary<string, double> Sum(string field, string[] groupField);
    IDictionary<string, double> Avg(string field, string[] groupField);
    IDictionary<string, double> Max(string field, string[] groupField);
    IDictionary<string, double> Min(string field, string[] groupField);
}
