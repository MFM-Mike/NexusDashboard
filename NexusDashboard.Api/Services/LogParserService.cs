using System.Text.RegularExpressions;
using System.Xml;
using NexusDashboard.Shared.Models;

namespace NexusDashboard.Api.Services;

public class LogParserService
{
    // Matches: 2024-01-15 10:30:00,123 [Thread-1] ERROR MyApp.Service - Message
    private static readonly Regex PlainTextPattern = new(
        @"^(?<date>\d{4}-\d{2}-\d{2}[\s_T]\d{2}:\d{2}:\d{2}[,\.]\d{3})\s+\[(?<thread>[^\]]+)\]\s+(?<level>DEBUG|INFO|WARN|ERROR|FATAL)\s+(?<logger>\S+)\s+-\s+(?<message>.*)$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    // Alternative: 2024-01-15 10:30:00,123 ERROR MyApp.Service [Thread-1] - Message
    private static readonly Regex PlainTextAltPattern = new(
        @"^(?<date>\d{4}-\d{2}-\d{2}[\s_T]\d{2}:\d{2}:\d{2}[,\.]\d{3})\s+(?<level>DEBUG|INFO|WARN|ERROR|FATAL)\s+(?<logger>\S+)\s+\[(?<thread>[^\]]+)\]\s+-\s+(?<message>.*)$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    public ParsedLogFile Parse(LogFileInfo fileInfo, string content)
    {
        var result = new ParsedLogFile { FileInfo = fileInfo };

        if (content.TrimStart().StartsWith("<log4net") || content.Contains("<log4net:event"))
        {
            result.DetectedFormat = LogFileFormat.Log4NetXml;
            result.Entries = ParseXml(content, out var xmlError);
            result.ParseError = xmlError;
        }
        else
        {
            result.DetectedFormat = LogFileFormat.PlainText;
            result.Entries = ParsePlainText(content);
        }

        // Assign sequential IDs
        for (int i = 0; i < result.Entries.Count; i++)
            result.Entries[i].Id = i + 1;

        result.Summary = BuildSummary(result.Entries);
        return result;
    }

    private List<LogEntry> ParseXml(string content, out string? error)
    {
        error = null;
        var entries = new List<LogEntry>();

        try
        {
            // Wrap in a root element if it's a fragment
            string xml = content.TrimStart();
            if (!xml.StartsWith("<?xml") && !xml.StartsWith("<log4net>"))
                xml = $"<log4net xmlns:log4net=\"http://logging.apache.org/log4net/\">{xml}</log4net>";

            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore };
            using var reader = XmlReader.Create(new StringReader(xml), settings);
            var doc = new XmlDocument();
            doc.Load(reader);

            var ns = new XmlNamespaceManager(doc.NameTable);
            ns.AddNamespace("l4n", "http://logging.apache.org/log4net/");

            // Try both namespaced and non-namespaced event nodes
            var events = doc.SelectNodes("//l4n:event", ns)
                         ?? doc.SelectNodes("//event");

            if (events == null) return entries;

            foreach (XmlNode evt in events)
            {
                var entry = new LogEntry
                {
                    Logger   = evt.Attributes?["logger"]?.Value ?? "",
                    Thread   = evt.Attributes?["thread"]?.Value ?? "",
                    Domain   = evt.Attributes?["domain"]?.Value,
                    Username = evt.Attributes?["username"]?.Value,
                };

                var tsStr = evt.Attributes?["timestamp"]?.Value;
                if (DateTime.TryParse(tsStr, out var ts))
                    entry.Timestamp = ts;

                var levelStr = evt.Attributes?["level"]?.Value ?? "";
                if (Enum.TryParse<LogLevel>(levelStr, true, out var lvl))
                    entry.Level = lvl;

                var msgNode = evt.SelectSingleNode("l4n:message", ns)
                           ?? evt.SelectSingleNode("message");
                entry.Message = msgNode?.InnerText ?? "";

                var exNode = evt.SelectSingleNode("l4n:exception", ns)
                          ?? evt.SelectSingleNode("exception");
                var exText = exNode?.InnerText?.Trim();
                if (!string.IsNullOrEmpty(exText))
                    entry.Exception = exText;

                // Properties
                var props = evt.SelectNodes("l4n:properties/l4n:data", ns)
                         ?? evt.SelectNodes("properties/data");
                if (props != null)
                {
                    foreach (XmlNode prop in props)
                    {
                        var name = prop.Attributes?["name"]?.Value ?? "";
                        var val  = prop.Attributes?["value"]?.Value ?? "";
                        if (!string.IsNullOrEmpty(name))
                            entry.Properties[name] = val;
                    }
                }

                entries.Add(entry);
            }
        }
        catch (Exception ex)
        {
            error = $"XML parse error: {ex.Message}";
        }

        return entries;
    }

    private List<LogEntry> ParsePlainText(string content)
    {
        var entries = new List<LogEntry>();
        var lines = content.Split('\n');
        LogEntry? current = null;
        var exceptionLines = new List<string>();

        void FinalizeEntry()
        {
            if (current == null) return;
            if (exceptionLines.Count > 0)
                current.Exception = string.Join("\n", exceptionLines).Trim();
            entries.Add(current);
            current = null;
            exceptionLines.Clear();
        }

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd();
            if (string.IsNullOrWhiteSpace(line)) continue;

            var match = PlainTextPattern.Match(line)
                     ?? PlainTextAltPattern.Match(line);

            if (!match.Success)
                match = PlainTextAltPattern.Match(line);

            if (match.Success)
            {
                FinalizeEntry();

                var dateStr = match.Groups["date"].Value.Replace(',', '.');
                DateTime.TryParse(dateStr, out var ts);

                Enum.TryParse<LogLevel>(match.Groups["level"].Value, true, out var level);

                current = new LogEntry
                {
                    Timestamp = ts,
                    Level     = level,
                    Logger    = match.Groups["logger"].Value,
                    Thread    = match.Groups["thread"].Value,
                    Message   = match.Groups["message"].Value,
                };
            }
            else if (current != null)
            {
                // Continuation line — stack trace or multi-line message
                if (line.StartsWith("   at ") || line.StartsWith("\tat ") ||
                    line.Contains("Exception:") || line.StartsWith("   ---"))
                {
                    exceptionLines.Add(line);
                }
                else
                {
                    current.Message += " " + line;
                }
            }
        }

        FinalizeEntry();
        return entries;
    }

    public LogSummary BuildSummary(List<LogEntry> entries)
    {
        if (entries.Count == 0)
            return new LogSummary();

        var summary = new LogSummary
        {
            TotalEntries  = entries.Count,
            ErrorCount    = entries.Count(e => e.Level == LogLevel.ERROR),
            WarnCount     = entries.Count(e => e.Level == LogLevel.WARN),
            FatalCount    = entries.Count(e => e.Level == LogLevel.FATAL),
            EarliestEntry = entries.Min(e => e.Timestamp),
            LatestEntry   = entries.Max(e => e.Timestamp),
            UniqueLoggers = entries.Select(e => e.Logger).Distinct().OrderBy(l => l).ToList(),
            UniqueThreads = entries.Select(e => e.Thread).Distinct().OrderBy(t => t).ToList(),
            TopErrors     = entries
                .Where(e => e.Level is LogLevel.ERROR or LogLevel.FATAL)
                .OrderByDescending(e => e.Timestamp)
                .Take(10)
                .ToList()
        };

        summary.LevelCounts = Enum.GetValues<LogLevel>()
            .Select(l => new LogLevelCount { Level = l, Count = entries.Count(e => e.Level == l) })
            .ToList();

        summary.TimeSeries = BuildTimeSeries(entries);
        return summary;
    }

    private List<LogTimeSeriesPoint> BuildTimeSeries(List<LogEntry> entries)
    {
        if (entries.Count == 0) return new();

        var earliest = entries.Min(e => e.Timestamp);
        var latest   = entries.Max(e => e.Timestamp);
        var span     = latest - earliest;

        // Pick bucket size based on total span
        TimeSpan bucketSize = span.TotalDays switch
        {
            > 30  => TimeSpan.FromDays(1),
            > 7   => TimeSpan.FromHours(6),
            > 1   => TimeSpan.FromHours(1),
            > 0.5 => TimeSpan.FromMinutes(15),
            _     => TimeSpan.FromMinutes(5)
        };

        var buckets = new Dictionary<DateTime, LogTimeSeriesPoint>();

        foreach (var entry in entries)
        {
            var bucket = new DateTime(
                (entry.Timestamp.Ticks / bucketSize.Ticks) * bucketSize.Ticks,
                entry.Timestamp.Kind);

            if (!buckets.TryGetValue(bucket, out var point))
            {
                point = new LogTimeSeriesPoint { Bucket = bucket };
                buckets[bucket] = point;
            }

            switch (entry.Level)
            {
                case LogLevel.DEBUG: point.Debug++; break;
                case LogLevel.INFO:  point.Info++;  break;
                case LogLevel.WARN:  point.Warn++;  break;
                case LogLevel.ERROR: point.Error++; break;
                case LogLevel.FATAL: point.Fatal++; break;
            }
        }

        return buckets.Values.OrderBy(p => p.Bucket).ToList();
    }

    public PagedLogResult ApplyFilter(List<LogEntry> entries, LogFilter filter)
    {
        var query = entries.AsEnumerable();

        if (filter.Levels.Count > 0)
            query = query.Where(e => filter.Levels.Contains(e.Level));

        if (filter.StartDate.HasValue)
            query = query.Where(e => e.Timestamp >= filter.StartDate.Value);

        if (filter.EndDate.HasValue)
            query = query.Where(e => e.Timestamp <= filter.EndDate.Value);

        if (!string.IsNullOrWhiteSpace(filter.LoggerFilter))
            query = query.Where(e => e.Logger.Contains(filter.LoggerFilter, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(filter.MessageSearch))
            query = query.Where(e =>
                e.Message.Contains(filter.MessageSearch, StringComparison.OrdinalIgnoreCase) ||
                (e.Exception?.Contains(filter.MessageSearch, StringComparison.OrdinalIgnoreCase) ?? false));

        if (!string.IsNullOrWhiteSpace(filter.ThreadFilter))
            query = query.Where(e => e.Thread.Contains(filter.ThreadFilter, StringComparison.OrdinalIgnoreCase));

        if (filter.OnlyWithExceptions)
            query = query.Where(e => !string.IsNullOrEmpty(e.Exception));

        var ordered = query.OrderByDescending(e => e.Timestamp).ToList();
        var total   = ordered.Count;
        var paged   = ordered
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToList();

        return new PagedLogResult
        {
            Entries     = paged,
            TotalCount  = total,
            PageNumber  = filter.PageNumber,
            PageSize    = filter.PageSize
        };
    }
}
