using System.Net.Http.Json;
using Microsoft.JSInterop;
using NexusDashboard.Shared.Models;

namespace NexusDashboard.Client.Services;

/// <summary>
/// Singleton state store for the currently loaded log data.
/// Components subscribe to OnDataChanged to re-render when data is replaced.
/// Persists the last selected bundle in localStorage so it survives page refresh.
/// </summary>
public class LogDataService
{
    private readonly HttpClient _http;
    private readonly IJSRuntime  _js;
    private const string StorageKey = "nexus_active_bundle";

    public LogDataService(HttpClient http, IJSRuntime js)
    {
        _http = http;
        _js   = js;
    }

    public LogQueryResult? CurrentData { get; private set; }
    public bool IsLoaded  => CurrentData is not null;
    public bool IsLoading { get; private set; }

    public List<LogBundleInfo> Bundles       { get; private set; } = [];
    public bool                BundlesLoaded { get; private set; }
    public string?             ActiveBundleId   { get; private set; }
    /// <summary>Display name of the active dataset ("E-Commerce Platform", "Pasted Log", or null for sample).</summary>
    public string?             ActiveBundleName { get; private set; }

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
        CurrentData      = await _http.GetFromJsonAsync<LogQueryResult>($"api/logs/bundles/{id}");
        ActiveBundleName = Bundles.FirstOrDefault(b => b.Id == id)?.Name ?? id;
        IsLoading        = false;
        await _js.InvokeVoidAsync("localStorage.setItem", StorageKey, id);
        OnDataChanged?.Invoke();
    }

    // ── Single-source helpers ────────────────────────────────────────────────

    /// <summary>Loads the built-in sample log data from the API.</summary>
    public async Task LoadSampleAsync()
    {
        IsLoading        = true;
        ActiveBundleId   = null;
        ActiveBundleName = null;
        OnDataChanged?.Invoke();
        CurrentData = await _http.GetFromJsonAsync<LogQueryResult>("api/logs");
        IsLoading   = false;
        await _js.InvokeVoidAsync("localStorage.removeItem", StorageKey);
        OnDataChanged?.Invoke();
    }

    /// <summary>Sends raw log text to the API for parsing and stores the result.</summary>
    public async Task ParseAsync(string logText)
    {
        IsLoading        = true;
        ActiveBundleId   = null;
        ActiveBundleName = "Pasted Log";
        OnDataChanged?.Invoke();
        var response = await _http.PostAsJsonAsync("api/logs/parse", new ParseRequest { Content = logText });
        response.EnsureSuccessStatusCode();
        CurrentData = await response.Content.ReadFromJsonAsync<LogQueryResult>();
        IsLoading   = false;
        await _js.InvokeVoidAsync("localStorage.removeItem", StorageKey);
        OnDataChanged?.Invoke();
    }

    /// <summary>
    /// Called on app start. Restores the last selected bundle from localStorage;
    /// falls back to the built-in sample if nothing was saved or the bundle no longer exists.
    /// </summary>
    public async Task RestoreLastSessionAsync()
    {
        string? savedId = null;
        try { savedId = await _js.InvokeAsync<string?>("localStorage.getItem", StorageKey); }
        catch { /* JS not available yet — fall through to sample */ }

        if (!string.IsNullOrWhiteSpace(savedId))
        {
            try
            {
                // Bundles must be loaded first so we can resolve the display name
                await FetchBundlesAsync();
                await LoadBundleAsync(savedId);
                return;
            }
            catch
            {
                // Bundle no longer exists or API error — clear persisted key and load sample
                try { await _js.InvokeVoidAsync("localStorage.removeItem", StorageKey); } catch { }
            }
        }

        await LoadSampleAsync();
    }
}
