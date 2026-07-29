namespace Domain.Infrastructure.Model;

/// <summary>
/// 报告生成接口
/// </summary>
/// <typeparam name="T">领域模型类型</typeparam>
public interface IGenerateReport<T> where T : DomainModel<T>
{
    void Add(DomainModel<T> obj);
    void Modify(DomainModel<T> obj);
    void Delete(DomainModel<T> obj);
    bool Clear();
}
