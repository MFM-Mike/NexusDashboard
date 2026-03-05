namespace NexusDashboard.Shared.Models;

public enum LogLevel
{
    DEBUG,
    INFO,
    WARN,
    ERROR,
    FATAL
}

public class LogEntry
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; }
    public LogLevel Level { get; set; }
    public string Logger { get; set; } = "";
    public string Thread { get; set; } = "";
    public string Message { get; set; } = "";
    public string? Exception { get; set; }
    public string? Domain { get; set; }
    public string? Username { get; set; }
    public Dictionary<string, string> Properties { get; set; } = new();
}

public class LogFilter
{
    public LogLevel? MinLevel { get; set; }
    public LogLevel? MaxLevel { get; set; }
    public List<LogLevel> Levels { get; set; } = new();
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? LoggerFilter { get; set; }
    public string? MessageSearch { get; set; }
    public string? ThreadFilter { get; set; }
    public bool OnlyWithExceptions { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 100;
}

public class LogLevelCount
{
    public LogLevel Level { get; set; }
    public int Count { get; set; }
    public string Color => Level switch
    {
        LogLevel.DEBUG => "#6c757d",
        LogLevel.INFO  => "#0d6efd",
        LogLevel.WARN  => "#ffc107",
        LogLevel.ERROR => "#dc3545",
        LogLevel.FATAL => "#6f42c1",
        _ => "#6c757d"
    };
}

public class LogTimeSeriesPoint
{
    public DateTime Bucket { get; set; }
    public int Debug { get; set; }
    public int Info { get; set; }
    public int Warn { get; set; }
    public int Error { get; set; }
    public int Fatal { get; set; }
    public int Total => Debug + Info + Warn + Error + Fatal;
}

public class LogSummary
{
    public int TotalEntries { get; set; }
    public int ErrorCount { get; set; }
    public int WarnCount { get; set; }
    public int FatalCount { get; set; }
    public DateTime? EarliestEntry { get; set; }
    public DateTime? LatestEntry { get; set; }
    public List<LogLevelCount> LevelCounts { get; set; } = new();
    public List<string> UniqueLoggers { get; set; } = new();
    public List<string> UniqueThreads { get; set; } = new();
    public List<LogTimeSeriesPoint> TimeSeries { get; set; } = new();
    public List<LogEntry> TopErrors { get; set; } = new();
}

public class LogFileInfo
{
    public string FileName { get; set; } = "";
    public string FullPath { get; set; } = "";
    public long FileSizeBytes { get; set; }
    public DateTime LastModified { get; set; }
    public string FileSizeDisplay => FileSizeBytes switch
    {
        < 1024 => $"{FileSizeBytes} B",
        < 1024 * 1024 => $"{FileSizeBytes / 1024.0:F1} KB",
        _ => $"{FileSizeBytes / (1024.0 * 1024):F1} MB"
    };
}

public class ParsedLogFile
{
    public LogFileInfo FileInfo { get; set; } = new();
    public List<LogEntry> Entries { get; set; } = new();
    public LogSummary Summary { get; set; } = new();
    public string? ParseError { get; set; }
    public LogFileFormat DetectedFormat { get; set; }
}

public enum LogFileFormat
{
    Unknown,
    Log4NetXml,
    PlainText
}

public class PagedLogResult
{
    public List<LogEntry> Entries { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}
