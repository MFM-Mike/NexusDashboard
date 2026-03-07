using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using NexusDashboard.Shared.Models;
using Xunit;

namespace NexusDashboard.Tests.Integration;

/// <summary>
/// Integration tests that spin up a real in-process ASP.NET Core host and
/// exercise the HTTP endpoints end-to-end (routing, serialisation, business logic).
/// </summary>
public class LogsControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public LogsControllerTests(WebApplicationFactory<Program> factory)
    {
        // Use https:// so UseHttpsRedirection never triggers a 307
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress    = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
    }

    // ── GET /api/logs ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSample_Returns200()
    {
        var response = await _client.GetAsync("/api/logs");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetSample_ReturnsNonEmptyEntries()
    {
        var result = await _client.GetFromJsonAsync<LogQueryResult>("/api/logs");
        result.Should().NotBeNull();
        result!.Entries.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetSample_ReturnsSummaryWithCorrectTotalCount()
    {
        var result = await _client.GetFromJsonAsync<LogQueryResult>("/api/logs");
        result!.Summary.TotalEntries.Should().Be(result.Entries.Count);
    }

    [Fact]
    public async Task GetSample_SummaryContainsFatalEntry()
    {
        var result = await _client.GetFromJsonAsync<LogQueryResult>("/api/logs");
        result!.Summary.FatalCount.Should().BeGreaterThan(0);
    }

    // ── GET /api/logs/bundles ────────────────────────────────────────────────

    [Fact]
    public async Task GetBundles_Returns200()
    {
        var response = await _client.GetAsync("/api/logs/bundles");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetBundles_ReturnsFourBundles()
    {
        var bundles = await _client.GetFromJsonAsync<List<LogBundleInfo>>("/api/logs/bundles");
        bundles.Should().HaveCount(4);
    }

    [Fact]
    public async Task GetBundles_AllBundles_HaveNonEmptyIds()
    {
        var bundles = await _client.GetFromJsonAsync<List<LogBundleInfo>>("/api/logs/bundles");
        bundles!.Should().AllSatisfy(b => b.Id.Should().NotBeNullOrWhiteSpace());
    }

    [Fact]
    public async Task GetBundles_ContainsExpectedBundleIds()
    {
        var bundles = await _client.GetFromJsonAsync<List<LogBundleInfo>>("/api/logs/bundles");
        var ids = bundles!.Select(b => b.Id);
        ids.Should().Contain(new[] { "ecommerce", "auth", "pipeline", "payments" });
    }

    // ── GET /api/logs/bundles/{id} – valid IDs ───────────────────────────────

    [Theory]
    [InlineData("ecommerce")]
    [InlineData("auth")]
    [InlineData("pipeline")]
    [InlineData("payments")]
    public async Task GetBundle_ValidId_Returns200(string id)
    {
        var response = await _client.GetAsync($"/api/logs/bundles/{id}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("ecommerce")]
    [InlineData("auth")]
    [InlineData("pipeline")]
    [InlineData("payments")]
    public async Task GetBundle_ValidId_ReturnsNonEmptyEntries(string id)
    {
        var result = await _client.GetFromJsonAsync<LogQueryResult>($"/api/logs/bundles/{id}");
        result!.Entries.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetBundle_Ecommerce_ContainsAllThreeSources()
    {
        var result = await _client.GetFromJsonAsync<LogQueryResult>("/api/logs/bundles/ecommerce");
        var sources = result!.Summary.Sources;
        sources.Should().Contain("web-api");
        sources.Should().Contain("order-service");
        sources.Should().Contain("inventory");
    }

    [Fact]
    public async Task GetBundle_Auth_ContainsBothSources()
    {
        var result = await _client.GetFromJsonAsync<LogQueryResult>("/api/logs/bundles/auth");
        var sources = result!.Summary.Sources;
        sources.Should().Contain("auth-api");
        sources.Should().Contain("user-service");
    }

    [Fact]
    public async Task GetBundle_Pipeline_ContainsAllThreeSources()
    {
        var result = await _client.GetFromJsonAsync<LogQueryResult>("/api/logs/bundles/pipeline");
        var sources = result!.Summary.Sources;
        sources.Should().Contain("ingestion");
        sources.Should().Contain("processor");
        sources.Should().Contain("export");
    }

    [Fact]
    public async Task GetBundle_Payments_ContainsBothSources()
    {
        var result = await _client.GetFromJsonAsync<LogQueryResult>("/api/logs/bundles/payments");
        var sources = result!.Summary.Sources;
        sources.Should().Contain("transactions");
        sources.Should().Contain("fraud");
    }

    [Fact]
    public async Task GetBundle_Ecommerce_EntriesAreSortedByTimestamp()
    {
        var result = await _client.GetFromJsonAsync<LogQueryResult>("/api/logs/bundles/ecommerce");
        result!.Entries.Should().BeInAscendingOrder(e => e.Timestamp);
    }

    [Fact]
    public async Task GetBundle_Ecommerce_EntryIdsAreSequentialFromOne()
    {
        var result = await _client.GetFromJsonAsync<LogQueryResult>("/api/logs/bundles/ecommerce");
        var ids = result!.Entries.Select(e => e.Id).ToList();
        ids.First().Should().Be(1);
        ids.Should().BeInAscendingOrder();
        ids.Last().Should().Be(ids.Count);
    }

    [Fact]
    public async Task GetBundle_Ecommerce_SummaryTotalMatchesEntryCount()
    {
        var result = await _client.GetFromJsonAsync<LogQueryResult>("/api/logs/bundles/ecommerce");
        result!.Summary.TotalEntries.Should().Be(result.Entries.Count);
    }

    [Fact]
    public async Task GetBundle_Ecommerce_HasExpectedEntryCountRange()
    {
        // Catalogue says ~300; allow generous range to account for exact file contents
        var result = await _client.GetFromJsonAsync<LogQueryResult>("/api/logs/bundles/ecommerce");
        result!.Entries.Count.Should().BeInRange(200, 500);
    }

    // ── GET /api/logs/bundles/{id} – invalid ID ──────────────────────────────

    [Fact]
    public async Task GetBundle_UnknownId_Returns404()
    {
        var response = await _client.GetAsync("/api/logs/bundles/does-not-exist");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetBundle_EmptySegment_Returns404OrMethodNotAllowed()
    {
        // "/api/logs/bundles/" could be routed to GetBundles (list) or 404
        var response = await _client.GetAsync("/api/logs/bundles/");
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    // ── POST /api/logs/parse ─────────────────────────────────────────────────

    [Fact]
    public async Task Parse_ValidLogContent_Returns200()
    {
        var request = new ParseRequest
        {
            Content = "2026-03-05 08:00:01,012 [1] INFO MyApp.X - hello"
        };
        var response = await _client.PostAsJsonAsync("/api/logs/parse", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Parse_ValidLogContent_ReturnsParsedEntry()
    {
        var request = new ParseRequest
        {
            Content = "2026-03-05 08:00:01,012 [1] INFO MyApp.X - hello"
        };
        var result = await _client.PostAsJsonAsync("/api/logs/parse", request)
                                  .ContinueWith(t => t.Result.Content.ReadFromJsonAsync<LogQueryResult>())
                                  .Unwrap();

        result!.Entries.Should().HaveCount(1);
        result.Entries[0].Level.Should().Be("INFO");
        result.Entries[0].Message.Should().Be("hello");
    }

    [Fact]
    public async Task Parse_MultipleLines_ReturnsAllEntries()
    {
        var content = string.Join("\n",
            "2026-03-05 08:00:01,012 [1] INFO  MyApp.X - one",
            "2026-03-05 08:00:02,000 [2] ERROR MyApp.Y - two",
            "2026-03-05 08:00:03,000 [3] WARN  MyApp.Z - three");

        var result = await _client.PostAsJsonAsync("/api/logs/parse", new ParseRequest { Content = content })
                                  .ContinueWith(t => t.Result.Content.ReadFromJsonAsync<LogQueryResult>())
                                  .Unwrap();

        result!.Entries.Should().HaveCount(3);
    }

    [Fact]
    public async Task Parse_WithStackTrace_AttachesExceptionToEntry()
    {
        var content = string.Join("\n",
            "2026-03-05 08:00:01,012 [1] ERROR MyApp.X - Failure",
            "   at MyApp.X.DoThing() in X.cs:line 10");

        var result = await _client.PostAsJsonAsync("/api/logs/parse", new ParseRequest { Content = content })
                                  .ContinueWith(t => t.Result.Content.ReadFromJsonAsync<LogQueryResult>())
                                  .Unwrap();

        result!.Entries.Should().HaveCount(1);
        result.Entries[0].Exception.Should().Contain("DoThing");
    }

    [Fact]
    public async Task Parse_EmptyContent_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/logs/parse",
            new ParseRequest { Content = "" });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Parse_WhitespaceContent_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/logs/parse",
            new ParseRequest { Content = "   \n\t  " });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Parse_NullBody_Returns400()
    {
        var response = await _client.PostAsJsonAsync<ParseRequest?>("/api/logs/parse", null);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Parse_ValidContent_SummaryLevelCountsMatchEntries()
    {
        var content = string.Join("\n",
            "2026-03-05 08:00:01,000 [1] INFO  MyApp.X - a",
            "2026-03-05 08:00:02,000 [1] INFO  MyApp.X - b",
            "2026-03-05 08:00:03,000 [1] ERROR MyApp.X - c");

        var result = await _client.PostAsJsonAsync("/api/logs/parse", new ParseRequest { Content = content })
                                  .ContinueWith(t => t.Result.Content.ReadFromJsonAsync<LogQueryResult>())
                                  .Unwrap();

        result!.Summary.InfoCount.Should().Be(2);
        result.Summary.ErrorCount.Should().Be(1);
        result.Summary.TotalEntries.Should().Be(3);
    }
}
