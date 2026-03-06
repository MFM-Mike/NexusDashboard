namespace NexusDashboard.Shared.Models;

// ── Log Models ──────────────────────────────────────────────────────────────

public class LogEntry
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = "";
    public string Logger { get; set; } = "";
    public string Thread { get; set; } = "";
    public string Message { get; set; } = "";
    public string? Exception { get; set; }
    public string Source { get; set; } = "";   // which log file within a bundle

    public string ShortLogger => Logger.Contains('.')
        ? Logger[(Logger.LastIndexOf('.') + 1)..]
        : Logger;
}

public class LogSummary
{
    public int TotalEntries { get; set; }
    public int FatalCount { get; set; }
    public int ErrorCount { get; set; }
    public int WarnCount { get; set; }
    public int InfoCount { get; set; }
    public int DebugCount { get; set; }
    public int TraceCount { get; set; }
    public List<string> UniqueLoggers { get; set; } = new();
    public List<string> Sources { get; set; } = new();   // distinct source names
    public DateTime? FirstEntry { get; set; }
    public DateTime? LastEntry { get; set; }
    public List<TimelineBucket> Timeline { get; set; } = new();
    public List<LoggerStat> TopLoggers { get; set; } = new();

    // Pre-built for chart components
    public List<KpiCard> KpiCards { get; set; } = new();
    public List<CategoryData> LevelBreakdown { get; set; } = new();
    public List<ChartDataPoint> TimelineData { get; set; } = new();
}

public class TimelineBucket
{
    public string Label { get; set; } = "";
    public DateTime Time { get; set; }
    public int Total { get; set; }
    public int Errors { get; set; }
    public int Warnings { get; set; }
}

public class LoggerStat
{
    public string Logger { get; set; } = "";
    public string ShortLogger => Logger.Contains('.')
        ? Logger[(Logger.LastIndexOf('.') + 1)..]
        : Logger;
    public int Count { get; set; }
    public int ErrorCount { get; set; }
    public double ErrorRate => Count > 0 ? (double)ErrorCount / Count * 100 : 0;
}

public class LogQueryResult
{
    public List<LogEntry> Entries { get; set; } = new();
    public LogSummary Summary { get; set; } = new();
}

public class ParseRequest
{
    public string Content { get; set; } = "";
}

public class LogBundleInfo
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Scenario { get; set; } = "";   // short narrative teaser
    public string Icon { get; set; } = "";
    public List<string> Sources { get; set; } = new();  // source display names
    public int ApproxEntries { get; set; }
}

// ── Shared chart/KPI models (still used by components) ──────────────────────

public class KpiCard
{
    public string Title { get; set; } = "";
    public string Value { get; set; } = "";
    public string Change { get; set; } = "";
    public bool IsPositive { get; set; }
    public string Icon { get; set; } = "";
    public string AccentColor { get; set; } = "";
}

public class ChartDataPoint
{
    public string Label { get; set; } = "";
    public double Value { get; set; }
    public double SecondaryValue { get; set; }
}

public class CategoryData
{
    public string Name { get; set; } = "";
    public double Percentage { get; set; }
    public string Color { get; set; } = "";
    public double Value { get; set; }
}
