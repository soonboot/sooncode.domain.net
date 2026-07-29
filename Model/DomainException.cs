namespace Domain.Infrastructure.Model;

/// <summary>
/// 领域异常类
/// </summary>
public class DomainException : Exception
{
    public enum LevelEnum
    {
        S,
        SS,
        SSS
    }
    
    public string? Code { get; set; }
    public string? Level { get; set; }
    
    public DomainException(string message) : base(message)
    {}
    
    public DomainException(string message, string code) : base(message)
    {
        Code = code;
    }
    
    public DomainException(string message, string code, string level) : base(message)
    {
        Code = code;
        Level = level;
    }
    
    public DomainException(string message, Exception innerException) : base(message, innerException)
    {}
}