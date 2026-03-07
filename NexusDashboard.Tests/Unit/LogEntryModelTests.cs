using FluentAssertions;
using NexusDashboard.Shared.Models;
using Xunit;

namespace NexusDashboard.Tests.Unit;

public class LogEntryModelTests
{
    // ── ShortLogger ──────────────────────────────────────────────────────────

    [Fact]
    public void ShortLogger_ReturnsFinalSegment_WhenLoggerHasDots()
    {
        var entry = new LogEntry { Logger = "MyApp.Services.UserService" };
        entry.ShortLogger.Should().Be("UserService");
    }

    [Fact]
    public void ShortLogger_ReturnsFullName_WhenLoggerHasNoDots()
    {
        var entry = new LogEntry { Logger = "Startup" };
        entry.ShortLogger.Should().Be("Startup");
    }

    [Fact]
    public void ShortLogger_HandlesOneSegment_WithTrailingDot()
    {
        // Logger ending in dot is unusual but shouldn't throw
        var entry = new LogEntry { Logger = "MyApp." };
        entry.ShortLogger.Should().Be("");
    }

    [Fact]
    public void ShortLogger_HandlesEmptyLogger()
    {
        var entry = new LogEntry { Logger = "" };
        entry.ShortLogger.Should().Be("");
    }

    // ── LoggerStat.ShortLogger ───────────────────────────────────────────────

    [Fact]
    public void LoggerStat_ShortLogger_ReturnsFinalSegment()
    {
        var stat = new LoggerStat { Logger = "MyApp.Api.Controllers.OrdersController" };
        stat.ShortLogger.Should().Be("OrdersController");
    }

    [Fact]
    public void LoggerStat_ErrorRate_IsZero_WhenCountIsZero()
    {
        var stat = new LoggerStat { Count = 0, ErrorCount = 0 };
        stat.ErrorRate.Should().Be(0);
    }

    [Fact]
    public void LoggerStat_ErrorRate_IsCorrect_WhenCountIsNonZero()
    {
        var stat = new LoggerStat { Count = 10, ErrorCount = 2 };
        stat.ErrorRate.Should().BeApproximately(20.0, 0.001);
    }

    [Fact]
    public void LoggerStat_ErrorRate_IsHundredPercent_WhenAllErrors()
    {
        var stat = new LoggerStat { Count = 5, ErrorCount = 5 };
        stat.ErrorRate.Should().BeApproximately(100.0, 0.001);
    }
}
