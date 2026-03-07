using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Moq;
using NexusDashboard.Api.Services;
using NexusDashboard.Shared.Models;
using Xunit;

namespace NexusDashboard.Tests.Unit;

public class LogParserServiceTests
{
    private readonly LogParserService _sut;

    // ── Helpers ──────────────────────────────────────────────────────────────

    // Valid log4net line template used throughout the tests
    private const string Line1 =
        "2026-03-05 08:00:01,012 [1] INFO  MyApp.Services.UserService - User logged in";
    private const string Line2 =
        "2026-03-05 08:00:02,500 [2] WARN  MyApp.Services.UserService - Session expiring soon";
    private const string Line3 =
        "2026-03-05 08:00:03,999 [3] ERROR MyApp.Data.OrderRepository - Insert failed";

    private const string StackLine1 = "   at MyApp.Data.OrderRepository.InsertAsync() in Repo.cs:line 42";
    private const string StackLine2 = "   at MyApp.Services.OrderService.CreateAsync() in Service.cs:line 18";

    public LogParserServiceTests()
    {
        var env = new Mock<IWebHostEnvironment>();
        // ContentRootPath only used by LoadBundle; unit tests don't call it
        env.Setup(e => e.ContentRootPath).Returns(AppContext.BaseDirectory);
        _sut = new LogParserService(env.Object);
    }

    // ── ParseLogContent – empty / whitespace ─────────────────────────────────

    [Fact]
    public void ParseLogContent_EmptyString_ReturnsEmptyEntries()
    {
        var result = _sut.ParseLogContent("");
        result.Entries.Should().BeEmpty();
    }

    [Fact]
    public void ParseLogContent_WhitespaceOnly_ReturnsEmptyEntries()
    {
        var result = _sut.ParseLogContent("   \n\t\n  ");
        result.Entries.Should().BeEmpty();
    }

    [Fact]
    public void ParseLogContent_EmptyString_ReturnsDefaultSummary()
    {
        var result = _sut.ParseLogContent("");
        result.Summary.TotalEntries.Should().Be(0);
        result.Summary.ErrorCount.Should().Be(0);
        result.Summary.UniqueLoggers.Should().BeEmpty();
    }

    // ── ParseLogContent – single entry field extraction ──────────────────────

    [Fact]
    public void ParseLogContent_SingleValidLine_ParsesTimestamp()
    {
        var result = _sut.ParseLogContent(Line1);
        result.Entries.Should().HaveCount(1);
        result.Entries[0].Timestamp.Should().Be(new DateTime(2026, 3, 5, 8, 0, 1, 12));
    }

    [Fact]
    public void ParseLogContent_SingleValidLine_ParsesLevel()
    {
        var result = _sut.ParseLogContent(Line1);
        result.Entries[0].Level.Should().Be("INFO");
    }

    [Fact]
    public void ParseLogContent_SingleValidLine_ParsesLogger()
    {
        var result = _sut.ParseLogContent(Line1);
        result.Entries[0].Logger.Should().Be("MyApp.Services.UserService");
    }

    [Fact]
    public void ParseLogContent_SingleValidLine_ParsesMessage()
    {
        var result = _sut.ParseLogContent(Line1);
        result.Entries[0].Message.Should().Be("User logged in");
    }

    [Fact]
    public void ParseLogContent_SingleValidLine_ParsesThread()
    {
        var result = _sut.ParseLogContent(Line1);
        result.Entries[0].Thread.Should().Be("1");
    }

    [Fact]
    public void ParseLogContent_SingleValidLine_IdIsOne()
    {
        var result = _sut.ParseLogContent(Line1);
        result.Entries[0].Id.Should().Be(1);
    }

    [Fact]
    public void ParseLogContent_SingleValidLine_SourceIsEmpty()
    {
        // ParseLogContent always passes "" as source
        var result = _sut.ParseLogContent(Line1);
        result.Entries[0].Source.Should().Be("");
    }

    // ── Level normalisation ──────────────────────────────────────────────────

    [Fact]
    public void ParseLogContent_WarningLevel_NormalizesToWARN()
    {
        var line = "2026-03-05 08:00:01,012 [1] WARNING MyApp.Services.X - Something bad";
        var result = _sut.ParseLogContent(line);
        result.Entries.Should().HaveCount(1);
        result.Entries[0].Level.Should().Be("WARN");
    }

    [Theory]
    [InlineData("TRACE")]
    [InlineData("DEBUG")]
    [InlineData("INFO")]
    [InlineData("WARN")]
    [InlineData("ERROR")]
    [InlineData("FATAL")]
    public void ParseLogContent_AllSupportedLevels_ParsedCorrectly(string level)
    {
        var line = $"2026-03-05 08:00:01,012 [1] {level} MyApp.Services.X - msg";
        var result = _sut.ParseLogContent(line);
        result.Entries.Should().HaveCount(1);
        result.Entries[0].Level.Should().Be(level == "WARNING" ? "WARN" : level);
    }

    // ── Timestamp formats ────────────────────────────────────────────────────

    [Fact]
    public void ParseLogContent_TimestampWithComma_ParsedCorrectly()
    {
        // log4net uses comma as milliseconds separator
        var line = "2026-03-05 08:15:30,123 [5] INFO MyApp.X - msg";
        var result = _sut.ParseLogContent(line);
        result.Entries[0].Timestamp.Millisecond.Should().Be(123);
        result.Entries[0].Timestamp.Second.Should().Be(30);
    }

    [Fact]
    public void ParseLogContent_TimestampWithDot_ParsedCorrectly()
    {
        var line = "2026-03-05 08:15:30.456 [5] INFO MyApp.X - msg";
        var result = _sut.ParseLogContent(line);
        result.Entries[0].Timestamp.Millisecond.Should().Be(456);
    }

    // ── Stack trace attachment ───────────────────────────────────────────────

    [Fact]
    public void ParseLogContent_StackTrace_AttachedToImmediatelyPrecedingEntry()
    {
        var content = string.Join("\n", Line3, StackLine1, StackLine2);
        var result = _sut.ParseLogContent(content);

        result.Entries.Should().HaveCount(1);
        result.Entries[0].Exception.Should().Contain("InsertAsync");
        result.Entries[0].Exception.Should().Contain("CreateAsync");
    }

    [Fact]
    public void ParseLogContent_StackTrace_NotAttachedToSubsequentEntry()
    {
        // Two entries: first has stack trace, second should have no exception
        var content = string.Join("\n", Line3, StackLine1, Line1);
        var result = _sut.ParseLogContent(content);

        result.Entries.Should().HaveCount(2);
        result.Entries[0].Exception.Should().Contain("InsertAsync");
        result.Entries[1].Exception.Should().BeNullOrEmpty();
    }

    [Fact]
    public void ParseLogContent_EntryWithNoStackTrace_HasNullException()
    {
        var result = _sut.ParseLogContent(Line1);
        result.Entries[0].Exception.Should().BeNullOrEmpty();
    }

    [Theory]
    [InlineData("   at System.Runtime.ExecuteMethod()")]
    [InlineData("\tat MyApp.Something.DoThing()")]
    [InlineData("System.NullReferenceException: Object reference not set")]
    [InlineData("Microsoft.EntityFrameworkCore.DbUpdateException: ...")]
    [InlineData("Stripe.StripeException: API connection failed")]
    [InlineData("Amazon.S3.AmazonS3Exception: Request timeout")]
    [InlineData("--- End of inner exception stack trace ---")]
    public void ParseLogContent_VariousStackTraceFormats_RecognisedAndAttached(string stackLine)
    {
        var content = string.Join("\n", Line3, stackLine);
        var result = _sut.ParseLogContent(content);

        result.Entries.Should().HaveCount(1);
        result.Entries[0].Exception.Should().Be(stackLine);
    }

    // ── Sequential IDs ───────────────────────────────────────────────────────

    [Fact]
    public void ParseLogContent_MultipleEntries_AssignsSequentialIds()
    {
        var content = string.Join("\n", Line1, Line2, Line3);
        var result = _sut.ParseLogContent(content);

        result.Entries.Select(e => e.Id).Should().BeEquivalentTo(new[] { 1, 2, 3 },
            opts => opts.WithStrictOrdering());
    }

    // ── Malformed lines ──────────────────────────────────────────────────────

    [Fact]
    public void ParseLogContent_MalformedLines_Skipped()
    {
        var content = string.Join("\n",
            "this is not a valid log line",
            Line1,
            "another garbage line",
            Line2);

        var result = _sut.ParseLogContent(content);
        result.Entries.Should().HaveCount(2);
    }

    [Fact]
    public void ParseLogContent_BlankLines_Skipped()
    {
        var content = string.Join("\n", "", Line1, "", "", Line2, "");
        var result = _sut.ParseLogContent(content);
        result.Entries.Should().HaveCount(2);
    }

    // ── Multiple entries + ordering ──────────────────────────────────────────

    [Fact]
    public void ParseLogContent_MultipleEntries_ParsedInOrder()
    {
        var content = string.Join("\n", Line1, Line2, Line3);
        var result = _sut.ParseLogContent(content);

        result.Entries.Should().HaveCount(3);
        result.Entries[0].Level.Should().Be("INFO");
        result.Entries[1].Level.Should().Be("WARN");
        result.Entries[2].Level.Should().Be("ERROR");
    }

    // ── Summary – level counts ───────────────────────────────────────────────

    [Fact]
    public void ParseLogContent_Summary_TotalEntriesMatchesEntryCount()
    {
        var content = string.Join("\n", Line1, Line2, Line3);
        var result = _sut.ParseLogContent(content);
        result.Summary.TotalEntries.Should().Be(result.Entries.Count);
    }

    [Fact]
    public void ParseLogContent_Summary_LevelCounts_AccuratelyReflectEntries()
    {
        var content = string.Join("\n",
            Line1,                                                                         // INFO
            Line2,                                                                         // WARN
            Line3,                                                                         // ERROR
            "2026-03-05 09:00:00,000 [1] FATAL MyApp.X - crash",                         // FATAL
            "2026-03-05 09:01:00,000 [1] DEBUG MyApp.X - debug msg",                     // DEBUG
            "2026-03-05 09:02:00,000 [1] TRACE MyApp.X - trace msg");                    // TRACE

        var s = _sut.ParseLogContent(content).Summary;

        s.InfoCount.Should().Be(1);
        s.WarnCount.Should().Be(1);
        s.ErrorCount.Should().Be(1);
        s.FatalCount.Should().Be(1);
        s.DebugCount.Should().Be(1);
        s.TraceCount.Should().Be(1);
        s.TotalEntries.Should().Be(6);
    }

    [Fact]
    public void ParseLogContent_Summary_LevelBreakdown_OnlyIncludesPresentLevels()
    {
        // Only INFO entries → breakdown should not contain ERROR, WARN, etc.
        var content = Line1;
        var breakdown = _sut.ParseLogContent(content).Summary.LevelBreakdown;

        breakdown.Should().HaveCount(1);
        breakdown[0].Name.Should().Be("INFO");
    }

    [Fact]
    public void ParseLogContent_Summary_LevelBreakdown_PercentageSumsToHundred()
    {
        var content = string.Join("\n", Line1, Line2, Line3);
        var breakdown = _sut.ParseLogContent(content).Summary.LevelBreakdown;

        breakdown.Sum(b => b.Percentage).Should().BeApproximately(100.0, 0.01);
    }

    // ── Summary – loggers ────────────────────────────────────────────────────

    [Fact]
    public void ParseLogContent_Summary_UniqueLoggers_ListedAlphabetically()
    {
        var content = string.Join("\n", Line3, Line1, Line2); // order: OrderRepo, UserService, UserService
        var loggers = _sut.ParseLogContent(content).Summary.UniqueLoggers;

        loggers.Should().BeInAscendingOrder();
    }

    [Fact]
    public void ParseLogContent_Summary_UniqueLoggers_NoDuplicates()
    {
        // Line1 and Line2 both use MyApp.Services.UserService
        var content = string.Join("\n", Line1, Line2, Line3);
        var loggers = _sut.ParseLogContent(content).Summary.UniqueLoggers;

        loggers.Should().OnlyHaveUniqueItems();
        loggers.Should().HaveCount(2); // UserService + OrderRepository
    }

    // ── Summary – time range ─────────────────────────────────────────────────

    [Fact]
    public void ParseLogContent_Summary_FirstAndLastEntry_Correct()
    {
        var content = string.Join("\n", Line1, Line2, Line3);
        var s = _sut.ParseLogContent(content).Summary;

        s.FirstEntry.Should().Be(new DateTime(2026, 3, 5, 8, 0, 1, 12));
        s.LastEntry.Should().Be(new DateTime(2026, 3, 5, 8, 0, 3, 999));
    }

    // ── Summary – KPI cards ──────────────────────────────────────────────────

    [Fact]
    public void ParseLogContent_Summary_KpiCards_AlwaysFourCards()
    {
        var result = _sut.ParseLogContent(Line1);
        result.Summary.KpiCards.Should().HaveCount(4);
    }

    [Fact]
    public void ParseLogContent_Summary_KpiCards_TotalEntriesCardIsFirst()
    {
        var result = _sut.ParseLogContent(Line1);
        result.Summary.KpiCards[0].Title.Should().Be("Total Entries");
        result.Summary.KpiCards[0].Value.Should().Be("1");
    }

    // ── Summary – timeline ───────────────────────────────────────────────────

    [Fact]
    public void ParseLogContent_Summary_Timeline_CoversFullRange()
    {
        // 3 entries over ~3 seconds → should produce at least one bucket
        var content = string.Join("\n", Line1, Line2, Line3);
        var timeline = _sut.ParseLogContent(content).Summary.Timeline;

        timeline.Should().NotBeEmpty();
        timeline.Should().BeInAscendingOrder(b => b.Time);
    }

    [Fact]
    public void ParseLogContent_Summary_Timeline_BucketCountsSumToTotalEntries()
    {
        var content = string.Join("\n", Line1, Line2, Line3);
        var result  = _sut.ParseLogContent(content);

        result.Summary.Timeline.Sum(b => b.Total)
            .Should().Be(result.Summary.TotalEntries);
    }

    [Fact]
    public void ParseLogContent_Summary_Timeline_Uses5MinBuckets_ForSpanUnder60Min()
    {
        // Entries 10 minutes apart → span < 60 min → 5-minute buckets
        var c1 = "2026-03-05 08:00:00,000 [1] INFO MyApp.X - a";
        var c2 = "2026-03-05 08:10:00,000 [1] INFO MyApp.X - b";

        var timeline = _sut.ParseLogContent(string.Join("\n", c1, c2)).Summary.Timeline;

        // Each bucket covers 5 minutes; labels are formatted as "HH:mm"
        timeline.Should().NotBeEmpty();
        var gap = timeline.Skip(1).First().Time - timeline.First().Time;
        gap.TotalMinutes.Should().Be(5);
    }

    // ── Summary – sources ────────────────────────────────────────────────────

    [Fact]
    public void ParseLogContent_Summary_Sources_EmptyForParsedText()
    {
        // ParseLogContent sets source = "" so Sources should be empty
        var result = _sut.ParseLogContent(Line1);
        result.Summary.Sources.Should().BeEmpty();
    }

    // ── GetBundles ───────────────────────────────────────────────────────────

    [Fact]
    public void GetBundles_ReturnsFourBundles()
    {
        _sut.GetBundles().Should().HaveCount(4);
    }

    [Fact]
    public void GetBundles_AllBundleIds_AreUnique()
    {
        var ids = _sut.GetBundles().Select(b => b.Id);
        ids.Should().OnlyHaveUniqueItems();
    }

    [Theory]
    [InlineData("ecommerce")]
    [InlineData("auth")]
    [InlineData("pipeline")]
    [InlineData("payments")]
    public void GetBundles_ExpectedId_IsPresent(string id)
    {
        _sut.GetBundles().Should().Contain(b => b.Id == id);
    }

    [Fact]
    public void GetBundles_AllBundles_HaveNonEmptyRequiredFields()
    {
        foreach (var b in _sut.GetBundles())
        {
            b.Id.Should().NotBeNullOrWhiteSpace(because: $"bundle at index has no Id");
            b.Name.Should().NotBeNullOrWhiteSpace();
            b.Description.Should().NotBeNullOrWhiteSpace();
            b.Icon.Should().NotBeNullOrWhiteSpace();
            b.Sources.Should().NotBeEmpty();
            b.ApproxEntries.Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public void GetBundles_Ecommerce_HasThreeSources()
    {
        var bundle = _sut.GetBundles().Single(b => b.Id == "ecommerce");
        bundle.Sources.Should().BeEquivalentTo(new[] { "web-api", "order-service", "inventory" });
    }

    [Fact]
    public void GetBundles_Auth_HasTwoSources()
    {
        var bundle = _sut.GetBundles().Single(b => b.Id == "auth");
        bundle.Sources.Should().BeEquivalentTo(new[] { "auth-api", "user-service" });
    }

    // ── GetSampleData ────────────────────────────────────────────────────────

    [Fact]
    public void GetSampleData_ReturnsNonEmptyEntries()
    {
        var result = _sut.GetSampleData();
        result.Entries.Should().NotBeEmpty();
    }

    [Fact]
    public void GetSampleData_EntriesAreSortedByTimestamp()
    {
        var result = _sut.GetSampleData();
        result.Entries.Should().BeInAscendingOrder(e => e.Timestamp);
    }

    [Fact]
    public void GetSampleData_HasAtLeastOneFatalEntry()
    {
        var result = _sut.GetSampleData();
        result.Entries.Should().Contain(e => e.Level == "FATAL");
    }

    [Fact]
    public void GetSampleData_HasAtLeastOneErrorEntry()
    {
        var result = _sut.GetSampleData();
        result.Entries.Should().Contain(e => e.Level == "ERROR");
    }

    [Fact]
    public void GetSampleData_HasAtLeastOneEntryWithException()
    {
        var result = _sut.GetSampleData();
        result.Entries.Should().Contain(e => !string.IsNullOrEmpty(e.Exception));
    }

    [Fact]
    public void GetSampleData_Summary_TotalEntriesIsConsistent()
    {
        var result = _sut.GetSampleData();
        result.Summary.TotalEntries.Should().Be(result.Entries.Count);
    }

    [Fact]
    public void GetSampleData_Summary_LevelCountsSumToTotal()
    {
        var s = _sut.GetSampleData().Summary;
        var sum = s.FatalCount + s.ErrorCount + s.WarnCount + s.InfoCount + s.DebugCount + s.TraceCount;
        sum.Should().Be(s.TotalEntries);
    }

    [Fact]
    public void GetSampleData_Summary_TopLoggers_NoMoreThanTen()
    {
        var result = _sut.GetSampleData();
        result.Summary.TopLoggers.Should().HaveCountLessOrEqualTo(10);
    }

    // ── FormatSpan (tested via KpiCards Time Span card) ──────────────────────

    [Theory]
    [InlineData(30,  "s")]   // 30 seconds  → Xs
    [InlineData(90,  "m")]   // 90 seconds  → Xm
    [InlineData(3700,"h")]   // ~1 hour     → X.Xh
    public void FormatSpan_CorrectSuffix_BasedOnDuration(int totalSeconds, string expectedSuffix)
    {
        // Build two entries exactly totalSeconds apart
        var base1 = $"2026-03-05 08:00:00,000 [1] INFO MyApp.X - start";
        var ts = TimeSpan.FromSeconds(totalSeconds);
        var h  = (int)ts.TotalHours;
        var m  = ts.Minutes;
        var s  = ts.Seconds;
        var base2 = $"2026-03-05 {8 + h:D2}:{m:D2}:{s:D2},000 [1] INFO MyApp.X - end";

        var kpis = _sut.ParseLogContent(string.Join("\n", base1, base2)).Summary.KpiCards;
        var spanCard = kpis.Should().ContainSingle(k => k.Title == "Time Span").Subject;

        spanCard.Value.Should().EndWith(expectedSuffix);
    }
}
