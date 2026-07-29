using Domain.Infrastructure.Model;
using System.Collections.Generic;

namespace Domain.Infrastructure.Finder;

public class Page<T>
{
    public long TotalElements { get; set; }
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
    public List<T> Content { get; set; } = new List<T>();
    
    public long getTotalElements() => TotalElements;
    public void setTotalElements(long totalElements) => TotalElements = totalElements;
    
    public int getPageIndex() => PageIndex;
    public void setPageIndex(int pageIndex) => PageIndex = pageIndex;
    
    public int getPageSize() => PageSize;
    public void setPageSize(int pageSize) => PageSize = pageSize;
    
    public List<T> getContent() => Content;
    public void setContent(List<T> content) => Content = content;
}
