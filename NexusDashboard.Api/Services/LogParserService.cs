using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using NexusDashboard.Shared.Models;

namespace NexusDashboard.Api.Services;

public class LogParserService
{
    private readonly IWebHostEnvironment _env;

    public LogParserService(IWebHostEnvironment env) => _env = env;

    // Matches the standard log4net PatternLayout:
    //   %date [%thread] %-5level %logger - %message
    // Example:
    //   2024-03-05 10:23:45,123 [1] INFO  MyApp.Services.UserService - User logged in
    private static readonly Regex EntryPattern = new(
        @"^(\d{4}-\d{2}-\d{2}[ T]\d{2}:\d{2}:\d{2}[,\.]\d{3})\s+\[([^\]]*)\]\s+(TRACE|DEBUG|INFO|WARN|WARNING|ERROR|FATAL)\s+(\S+)\s+-\s+(.+)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // ── Bundle catalogue ─────────────────────────────────────────────────────

    public List<LogBundleInfo> GetBundles() =>
    [
        new()
        {
            Id           = "ecommerce",
            Name         = "E-Commerce Platform",
            Description  = "High-traffic retail backend handling product browsing, cart operations, and order processing.",
            Scenario     = "Database connection pool exhaustion during peak hours followed by payment gateway timeouts.",
            Icon         = "🛒",
            Sources      = ["web-api", "order-service", "inventory"],
            ApproxEntries = 300,
        },
        new()
        {
            Id           = "auth",
            Name         = "Auth & Identity Service",
            Description  = "Authentication and user management service covering login, token lifecycle, and profile operations.",
            Scenario     = "Brute-force attack triggers account lockouts, followed by a JWT signing key rotation incident.",
            Icon         = "🔐",
            Sources      = ["auth-api", "user-service"],
            ApproxEntries = 260,
        },
        new()
        {
            Id           = "pipeline",
            Name         = "Data Pipeline",
            Description  = "ETL pipeline ingesting from Salesforce, Stripe, and webhooks; processing and exporting to downstream consumers.",
            Scenario     = "Salesforce schema drift breaks ingestion; processor hits OutOfMemoryException on oversized batch.",
            Icon         = "🔄",
            Sources      = ["ingestion", "processor", "export"],
            ApproxEntries = 280,
        },
        new()
        {
            Id           = "payments",
            Name         = "Payment Gateway",
            Description  = "Payment processing and fraud detection covering charges, refunds, settlement, and real-time risk scoring.",
            Scenario     = "Stripe connectivity loss triggers PayPal fallback; card-testing attack detected and mitigated.",
            Icon         = "💳",
            Sources      = ["transactions", "fraud"],
            ApproxEntries = 270,
        },
    ];

    // ── Bundle loader ────────────────────────────────────────────────────────

    public LogQueryResult LoadBundle(string id)
    {
        var bundle = GetBundles().FirstOrDefault(b => b.Id == id);
        if (bundle is null)
            return new LogQueryResult();

        var bundleDir = Path.Combine(_env.ContentRootPath, "SampleLogs", id);
        var allEntries = new List<LogEntry>();
        int globalId = 1;

        foreach (var sourceName in bundle.Sources)
        {
            var filePath = Path.Combine(bundleDir, sourceName + ".log");
            if (!File.Exists(filePath)) continue;

            var content = File.ReadAllText(filePath);
            var sourceEntries = ParseToEntries(content, sourceName, ref globalId);
            allEntries.AddRange(sourceEntries);
        }

        allEntries = [.. allEntries.OrderBy(e => e.Timestamp)];
        // Re-assign sequential IDs after merge-sort
        for (int i = 0; i < allEntries.Count; i++) allEntries[i].Id = i + 1;

        return BuildResult(allEntries);
    }

    // ── Public API ───────────────────────────────────────────────────────────

    public LogQueryResult GetSampleData() => BuildResult(GenerateSampleEntries());

    public LogQueryResult ParseLogContent(string content)
    {
        int id = 1;
        var entries = ParseToEntries(content, "", ref id);
        return BuildResult(entries);
    }

    // ── Core parser ──────────────────────────────────────────────────────────

    private List<LogEntry> ParseToEntries(string content, string source, ref int id)
    {
        var entries = new List<LogEntry>();
        var lines = content.Split('\n');
        LogEntry? current = null;
        var exBuf = new List<string>();

        foreach (var raw in lines)
        {
            var line = raw.TrimEnd('\r');
            var m = EntryPattern.Match(line);

            if (m.Success)
            {
                Flush(ref current, exBuf, entries);
                var ts = m.Groups[1].Value.Replace(',', '.');
                DateTime.TryParse(ts, out var timestamp);

                current = new LogEntry
                {
                    Id        = id++,
                    Timestamp = timestamp,
                    Thread    = m.Groups[2].Value.Trim(),
                    Level     = NormalizeLevel(m.Groups[3].Value),
                    Logger    = m.Groups[4].Value.Trim(),
                    Message   = m.Groups[5].Value.Trim(),
                    Source    = source,
                };
            }
            else if (current != null && IsStackTraceLine(line))
            {
                exBuf.Add(line);
            }
        }
        Flush(ref current, exBuf, entries);
        return entries;
    }

    // ── Parsing helpers ──────────────────────────────────────────────────────

    private static void Flush(ref LogEntry? entry, List<string> exBuf, List<LogEntry> list)
    {
        if (entry is null) return;
        if (exBuf.Count > 0)
        {
            entry.Exception = string.Join('\n', exBuf);
            exBuf.Clear();
        }
        list.Add(entry);
        entry = null;
    }

    private static bool IsStackTraceLine(string line) =>
        line.StartsWith("   at ") ||
        line.StartsWith("\tat ") ||
        line.StartsWith("System.") ||
        line.StartsWith("Microsoft.") ||
        line.StartsWith("Stripe.") ||
        line.StartsWith("Amazon.") ||
        line.StartsWith("---") ||
        (line.Length > 0 && char.IsWhiteSpace(line[0]) && line.TrimStart().StartsWith("at "));

    private static string NormalizeLevel(string raw) =>
        raw.Trim().ToUpperInvariant() switch
        {
            "WARNING" => "WARN",
            var l     => l
        };

    // ── Summary builder ──────────────────────────────────────────────────────

    private static LogQueryResult BuildResult(List<LogEntry> entries) =>
        new() { Entries = entries, Summary = BuildSummary(entries) };

    private static LogSummary BuildSummary(List<LogEntry> entries)
    {
        if (entries.Count == 0) return new LogSummary();

        int fatal = entries.Count(e => e.Level == "FATAL");
        int error = entries.Count(e => e.Level == "ERROR");
        int warn  = entries.Count(e => e.Level == "WARN");
        int info  = entries.Count(e => e.Level == "INFO");
        int debug = entries.Count(e => e.Level == "DEBUG");
        int trace = entries.Count(e => e.Level == "TRACE");

        var loggers = entries.Select(e => e.Logger).Distinct().OrderBy(l => l).ToList();
        var sources = entries.Select(e => e.Source).Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();
        var first   = entries.Min(e => e.Timestamp);
        var last    = entries.Max(e => e.Timestamp);
        var span    = last - first;

        var timeline   = BuildTimeline(entries, first, last);
        var topLoggers = entries
            .GroupBy(e => e.Logger)
            .Select(g => new LoggerStat
            {
                Logger     = g.Key,
                Count      = g.Count(),
                ErrorCount = g.Count(e => e.Level is "ERROR" or "FATAL"),
            })
            .OrderByDescending(l => l.Count)
            .Take(10)
            .ToList();

        double total = entries.Count;
        var levelBreakdown = new List<CategoryData>();
        if (fatal > 0) levelBreakdown.Add(new() { Name = "FATAL", Percentage = fatal / total * 100, Value = fatal, Color = "#8B0000" });
        if (error > 0) levelBreakdown.Add(new() { Name = "ERROR", Percentage = error / total * 100, Value = error, Color = "#FF6B6B" });
        if (warn  > 0) levelBreakdown.Add(new() { Name = "WARN",  Percentage = warn  / total * 100, Value = warn,  Color = "#FFB547" });
        if (info  > 0) levelBreakdown.Add(new() { Name = "INFO",  Percentage = info  / total * 100, Value = info,  Color = "#00E5BE" });
        if (debug > 0) levelBreakdown.Add(new() { Name = "DEBUG", Percentage = debug / total * 100, Value = debug, Color = "#7B61FF" });
        if (trace > 0) levelBreakdown.Add(new() { Name = "TRACE", Percentage = trace / total * 100, Value = trace, Color = "#5a6280" });

        return new LogSummary
        {
            TotalEntries   = entries.Count,
            FatalCount     = fatal,
            ErrorCount     = error,
            WarnCount      = warn,
            InfoCount      = info,
            DebugCount     = debug,
            TraceCount     = trace,
            UniqueLoggers  = loggers,
            Sources        = sources,
            FirstEntry     = first,
            LastEntry      = last,
            Timeline       = timeline,
            TopLoggers     = topLoggers,
            LevelBreakdown = levelBreakdown,
            TimelineData   = timeline.Select(t => new ChartDataPoint
            {
                Label          = t.Label,
                Value          = t.Total,
                SecondaryValue = t.Errors + t.Warnings,
            }).ToList(),
            KpiCards = new()
            {
                new() { Title = "Total Entries",  Value = entries.Count.ToString("N0"),        Icon = "📋", AccentColor = "#7B61FF", Change = $"{loggers.Count} loggers", IsPositive = true },
                new() { Title = "Errors / Fatal", Value = (error + fatal).ToString("N0"),      Icon = "🔴", AccentColor = "#FF6B6B", Change = $"{(error + fatal) / total * 100:F1}% of total", IsPositive = error + fatal == 0 },
                new() { Title = "Warnings",       Value = warn.ToString("N0"),                 Icon = "🟡", AccentColor = "#FFB547", Change = $"{warn / total * 100:F1}% of total",              IsPositive = warn == 0 },
                new() { Title = "Time Span",      Value = FormatSpan(span),                    Icon = "🕐", AccentColor = "#00E5BE", Change = $"{first:HH:mm} – {last:HH:mm}",                   IsPositive = true },
            },
        };
    }

    private static List<TimelineBucket> BuildTimeline(List<LogEntry> entries, DateTime first, DateTime last)
    {
        var span = last - first;
        TimeSpan bucketSize;
        string   fmt;

        if      (span.TotalMinutes <= 60) { bucketSize = TimeSpan.FromMinutes(5);  fmt = "HH:mm"; }
        else if (span.TotalHours   <= 12) { bucketSize = TimeSpan.FromMinutes(30); fmt = "HH:mm"; }
        else if (span.TotalHours   <= 48) { bucketSize = TimeSpan.FromHours(1);    fmt = "HH:mm"; }
        else                              { bucketSize = TimeSpan.FromHours(6);    fmt = "MM/dd HH:mm"; }

        var mins    = (int)bucketSize.TotalMinutes;
        var cursor  = new DateTime(first.Year, first.Month, first.Day,
                                   first.Hour, first.Minute / mins * mins, 0);
        var buckets = new List<TimelineBucket>();

        while (cursor <= last)
        {
            var next   = cursor + bucketSize;
            var bucket = entries.Where(e => e.Timestamp >= cursor && e.Timestamp < next).ToList();
            buckets.Add(new TimelineBucket
            {
                Label    = cursor.ToString(fmt),
                Time     = cursor,
                Total    = bucket.Count,
                Errors   = bucket.Count(e => e.Level is "ERROR" or "FATAL"),
                Warnings = bucket.Count(e => e.Level == "WARN"),
            });
            cursor = next;
        }
        return buckets;
    }

    private static string FormatSpan(TimeSpan s) =>
        s.TotalSeconds < 60  ? $"{(int)s.TotalSeconds}s"   :
        s.TotalMinutes < 60  ? $"{(int)s.TotalMinutes}m"   :
        s.TotalHours   < 24  ? $"{s.TotalHours:F1}h"       :
                               $"{s.TotalDays:F1}d";

    // ── Sample data ──────────────────────────────────────────────────────────

    private static List<LogEntry> GenerateSampleEntries()
    {
        var list = new List<LogEntry>();
        int id   = 1;
        var T    = DateTime.Today.AddHours(8); // 08:00 today

        void Add(DateTime t, string lvl, string logger, string msg, string? ex = null) =>
            list.Add(new() { Id = id++, Timestamp = t, Level = lvl, Logger = logger,
                             Thread = ((id % 8) + 1).ToString(), Message = msg, Exception = ex });

        // ── Startup ──────────────────────────────────────────────────────────
        Add(T,              "INFO",  "Microsoft.AspNetCore.Hosting.Internal.WebHost", "Application started. Press Ctrl+C to shut down.");
        Add(T.AddSeconds(1),"INFO",  "Microsoft.AspNetCore.Hosting.Internal.WebHost", "Hosting environment: Production");
        Add(T.AddSeconds(1),"INFO",  "MyApp.Startup",                                 "Connecting to database: Server=prod-db-01;Database=MyAppDb");
        Add(T.AddSeconds(2),"INFO",  "MyApp.Startup",                                 "Database connection established successfully");
        Add(T.AddSeconds(2),"INFO",  "MyApp.Startup",                                 "Redis cache initialized at redis://prod-cache-01:6379");
        Add(T.AddSeconds(3),"INFO",  "MyApp.Startup",                                 "RabbitMQ connected to prod-mq-01:5672");
        Add(T.AddSeconds(3),"INFO",  "MyApp.Startup",                                 "Application ready. Listening on http://0.0.0.0:8080");

        // ── Request handling ─────────────────────────────────────────────────
        T = T.AddMinutes(5);
        Add(T,              "INFO",  "MyApp.Api.Controllers.UsersController",          "GET /api/users — 200 OK (42ms)");
        Add(T.AddSeconds(3),"DEBUG", "MyApp.Data.Repositories.UserRepository",        "Executing: SELECT * FROM Users WHERE IsActive=1 LIMIT 50");
        Add(T.AddSeconds(4),"INFO",  "MyApp.Api.Controllers.UsersController",         "Returned 48 users");

        T = T.AddMinutes(2);
        Add(T,              "INFO",  "MyApp.Api.Controllers.OrdersController",        "POST /api/orders — Creating order for user 1042");
        Add(T.AddSeconds(1),"DEBUG", "MyApp.Services.OrderService",                  "Validating order: 3 items, total $149.97");
        Add(T.AddSeconds(2),"DEBUG", "MyApp.Data.Repositories.OrderRepository",      "INSERT INTO Orders (UserId, Total, Status) VALUES (1042, 149.97, 'Pending')");
        Add(T.AddSeconds(2),"INFO",  "MyApp.Services.OrderService",                  "Order #ORD-8821 created successfully");
        Add(T.AddSeconds(3),"INFO",  "MyApp.Services.EmailService",                  "Queuing confirmation email for user 1042");
        Add(T.AddSeconds(3),"DEBUG", "MyApp.Infrastructure.Messaging.RabbitMqPublisher", "Published to queue 'email-notifications' (correlationId: a3f9b12c)");
        Add(T.AddSeconds(4),"INFO",  "MyApp.Api.Controllers.OrdersController",       "POST /api/orders — 201 Created (ORD-8821, 387ms)");

        // ── Cache activity ────────────────────────────────────────────────────
        T = T.AddMinutes(3);
        Add(T,              "DEBUG", "MyApp.Infrastructure.Caching.RedisCache",       "Cache HIT: user_profile:1042 (TTL: 847s remaining)");
        Add(T.AddSeconds(5),"DEBUG", "MyApp.Infrastructure.Caching.RedisCache",       "Cache MISS: product_catalog:v3 — fetching from database");
        Add(T.AddSeconds(6),"DEBUG", "MyApp.Data.Repositories.ProductRepository",    "Executing: SELECT * FROM Products WHERE IsActive=1");
        Add(T.AddSeconds(8),"DEBUG", "MyApp.Infrastructure.Caching.RedisCache",       "Cache SET: product_catalog:v3 (TTL: 3600s, 18.4 KB)");

        // ── Cache latency warnings ────────────────────────────────────────────
        T = T.AddMinutes(5);
        Add(T,              "WARN",  "MyApp.Infrastructure.Caching.RedisCache",       "Connection latency high: 284ms (threshold: 200ms)");
        Add(T.AddSeconds(30),"WARN", "MyApp.Infrastructure.Caching.RedisCache",      "Connection latency high: 312ms (threshold: 200ms)");
        Add(T.AddMinutes(1), "INFO", "MyApp.Infrastructure.Caching.RedisCache",      "Latency normalized: 48ms");

        // ── 404s ─────────────────────────────────────────────────────────────
        T = T.AddMinutes(8);
        Add(T,              "WARN",  "MyApp.Api.Controllers.UsersController",          "GET /api/users/9999 — 404 Not Found: User does not exist");
        Add(T.AddSeconds(10),"WARN", "MyApp.Api.Controllers.ProductsController",      "GET /api/products/9876 — 404 Not Found: Product not found or inactive");

        // ── SMTP error ────────────────────────────────────────────────────────
        T = T.AddMinutes(10);
        Add(T, "ERROR", "MyApp.Services.EmailService",
            "Failed to send confirmation email for ORD-8830: SMTP connection refused",
            "System.Net.Mail.SmtpException: Connection refused\r\n" +
            "   at System.Net.Mail.SmtpClient.Connect(String host, Int32 port)\r\n" +
            "   at MyApp.Services.EmailService.SendAsync(EmailMessage msg) in EmailService.cs:line 87\r\n" +
            "   at MyApp.Services.OrderService.NotifyCustomerAsync(Order order) in OrderService.cs:line 134");
        Add(T.AddSeconds(1),"WARN",  "MyApp.Services.OrderService",                  "Email notification failed for ORD-8830 — will retry in 5 minutes");

        // ── DB connection failure ─────────────────────────────────────────────
        T = T.AddMinutes(15);
        Add(T, "ERROR", "MyApp.Data.Repositories.UserRepository",
            "Query failed after 3 retries — Cannot connect to database server",
            "System.Data.SqlClient.SqlException: A connection attempt failed because the connected party did not\r\n" +
            "properly respond after a period of time. 10.0.1.5:1433\r\n" +
            "   at System.Data.SqlClient.SqlConnection.Open()\r\n" +
            "   at MyApp.Data.Repositories.UserRepository.GetByIdAsync(Int32 id) in UserRepository.cs:line 54");
        Add(T.AddSeconds(1),"ERROR", "MyApp.Api.Controllers.UsersController",         "GET /api/users/3301 — 503 Service Unavailable: Database unreachable");
        Add(T.AddSeconds(35),"INFO", "MyApp.Data.Repositories.UserRepository",         "Database connection restored after 30s outage");

        // ── Fatal crash ───────────────────────────────────────────────────────
        T = T.AddMinutes(30);
        Add(T, "FATAL", "MyApp.Infrastructure.ExceptionHandler",
            "Unhandled application exception — initiating graceful shutdown",
            "System.OutOfMemoryException: Insufficient memory to continue the execution of the program.\r\n" +
            "   at System.String.Concat(String[] values)\r\n" +
            "   at MyApp.Services.ReportService.GenerateFullReport(ReportOptions opts) in ReportService.cs:line 445\r\n" +
            "   at MyApp.BackgroundServices.ReportGeneratorService.ExecuteAsync(CancellationToken ct) in ReportGeneratorService.cs:line 72");
        Add(T.AddSeconds(2),"INFO",  "MyApp.Startup",                                  "Application shutdown complete");

        return list.OrderBy(e => e.Timestamp).ToList();
    }
}
