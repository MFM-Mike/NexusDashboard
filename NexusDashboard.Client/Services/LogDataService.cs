using System.Net.Http.Json;
using NexusDashboard.Shared.Models;

namespace NexusDashboard.Client.Services;

/// <summary>
/// Singleton state store for the currently loaded log data.
/// Components subscribe to OnDataChanged to re-render when data is replaced.
/// </summary>
public class LogDataService
{
    private readonly HttpClient _http;

    public LogDataService(HttpClient http) => _http = http;

    public LogQueryResult? CurrentData { get; private set; }
    public bool IsLoaded  => CurrentData is not null;
    public bool IsLoading { get; private set; }

    public List<LogBundleInfo> Bundles       { get; private set; } = [];
    public bool                BundlesLoaded { get; private set; }
    public string?             ActiveBundleId { get; private set; }

    public event Action? OnDataChanged;

    // ── Bundle catalogue ─────────────────────────────────────────────────────

    /// <summary>Fetches the list of available bundles from the API (cached after first call).</summary>
    public async Task FetchBundlesAsync()
    {
        if (BundlesLoaded) return;
        Bundles = await _http.GetFromJsonAsync<List<LogBundleInfo>>("api/logs/bundles") ?? [];
        BundlesLoaded = true;
        OnDataChanged?.Invoke();
    }

    /// <summary>Loads all log sources for a bundle, merges them, and stores the result.</summary>
    public async Task LoadBundleAsync(string id)
    {
        IsLoading = true;
        ActiveBundleId = id;
        OnDataChanged?.Invoke();
        CurrentData = await _http.GetFromJsonAsync<LogQueryResult>($"api/logs/bundles/{id}");
        IsLoading = false;
        OnDataChanged?.Invoke();
    }

    // ── Single-source helpers ────────────────────────────────────────────────

    /// <summary>Loads the built-in sample log data from the API.</summary>
    public async Task LoadSampleAsync()
    {
        IsLoading = true;
        ActiveBundleId = null;
        OnDataChanged?.Invoke();
        CurrentData = await _http.GetFromJsonAsync<LogQueryResult>("api/logs");
        IsLoading = false;
        OnDataChanged?.Invoke();
    }

    /// <summary>Sends raw log text to the API for parsing and stores the result.</summary>
    public async Task ParseAsync(string logText)
    {
        IsLoading = true;
        ActiveBundleId = null;
        OnDataChanged?.Invoke();
        var response = await _http.PostAsJsonAsync("api/logs/parse", new ParseRequest { Content = logText });
        response.EnsureSuccessStatusCode();
        CurrentData = await response.Content.ReadFromJsonAsync<LogQueryResult>();
        IsLoading = false;
        OnDataChanged?.Invoke();
    }
}
