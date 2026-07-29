namespace Domain.Infrastructure.Finder;

public class Sort
{
    private readonly List<KeyValuePair<string, SortType>> _sort = new List<KeyValuePair<string, SortType>>();
    
    public static Sort ASC(string name)
    {
        return new Sort(name, SortType.asc);
    }
    
    public static Sort DESC(string name)
    {
        return new Sort(name, SortType.desc);
    }
    
    public Sort Asc(string name)
    {
        return Add(name, SortType.asc);
    }
    
    public Sort Desc(string name)
    {
        return Add(name, SortType.desc);
    }
    
    public Sort(string name, SortType type)
    {
        _sort.Add(new KeyValuePair<string, SortType>(name, type));
    }
    
    public Sort Add(string name, SortType type)
    {
        _sort.Add(new KeyValuePair<string, SortType>(name, type));
        return this;
    }
    
    public List<KeyValuePair<string, SortType>> Get()
    {
        return _sort;
    }
    
    public enum SortType
    {
        desc,
        asc
    }
}
