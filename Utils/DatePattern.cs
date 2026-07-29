namespace Domain.Infrastructure.Utils;

public static class DatePattern
{
    public static readonly string[] PARSE_PATTERNS = new string[]
    {
        DATE,
        DATETIME,
        DATETIME_MM,
        DATETIME_SSS,
        SYS_DATE,
        SYS_DATETIME,
        SYS_DATETIME_MM,
        SYS_DATETIME_SSS
    };
    
    public const string DATE = "yyyy-MM-dd";
    public const string DATETIME = "yyyy-MM-dd HH:mm:ss";
    public const string DATETIME_MM = "yyyy-MM-dd HH:mm";
    public const string DATETIME_SSS = "yyyy-MM-dd HH:mm:ss.SSS";
    public const string TIME = "HH:mm";
    public const string TIME_SS = "HH:mm:ss";
    public const string SYS_DATE = "yyyy/MM/dd";
    public const string SYS_DATETIME = "yyyy/MM/dd HH:mm:ss";
    public const string SYS_DATETIME_MM = "yyyy/MM/dd HH:mm";
    public const string SYS_DATETIME_SSS = "yyyy/MM/dd HH:mm:ss.SSS";
    public const string NONE_DATE = "yyyyMMdd";
    public const string NONE_DATETIME = "yyyyMMddHHmmss";
    public const string NONE_DATETIME_MM = "yyyyMMddHHmm";
    public const string NONE_DATETIME_SSS = "yyyyMMddHHmmssSSS";
}
