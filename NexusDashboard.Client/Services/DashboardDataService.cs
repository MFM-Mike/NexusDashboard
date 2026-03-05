using System.Net.Http.Json;
using NexusDashboard.Shared.Models;

namespace NexusDashboard.Client.Services;

public class DashboardDataService
{
    private readonly HttpClient _http;

    public DashboardDataService(HttpClient http)
    {
        _http = http;
    }

    public async Task<DashboardSummary?> GetDashboardDataAsync()
    {
        return await _http.GetFromJsonAsync<DashboardSummary>("api/dashboard");
    }
}
