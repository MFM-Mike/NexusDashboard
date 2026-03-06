# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

Both the API and Client must run simultaneously in separate terminals.

```bash
# Build the entire solution
dotnet build NexusDashboard.sln

# Run the API (https://localhost:7000, http://localhost:5000)
dotnet run --project NexusDashboard.Api

# Run the Blazor WASM client (https://localhost:7001, http://localhost:5001)
dotnet run --project NexusDashboard.Client
```

Swagger UI is available at `https://localhost:7000/swagger` when running in Development.

There are no tests in this project.

## Architecture

This is a .NET 8 solution with three projects:

- **NexusDashboard.Api** — ASP.NET Core Web API. Serves data at `GET /api/dashboard`, `GET /api/dashboard/kpis`, and `GET /api/dashboard/transactions`. CORS is locked to the Client origins above.
- **NexusDashboard.Client** — Blazor WebAssembly SPA. `HttpClient.BaseAddress` is hardcoded to `https://localhost:7000/`. Registers `DashboardDataService` (HTTP wrapper) and `ThemeService`.
- **NexusDashboard.Shared** — Class library with shared models (`DashboardSummary`, `KpiCard`, `Transaction`, `ChartDataPoint`, `CategoryData`) referenced by both projects.

### Data flow

`DashboardDataService` on the API generates all data in-memory (seeded random, no database). The client's `DashboardDataService` is a thin `HttpClient` wrapper that calls `api/dashboard`. There is an intentional 400 ms `Task.Delay` in `Index.razor` before the fetch to show the loading spinner.

### Theming

`ThemeService` (scoped in WASM) holds a boolean `IsDark` (default `true`) and fires `OnThemeChanged`. `MainLayout` subscribes to this event and toggles the CSS class `dark`/`light` on the root `.app-shell` div. All styling uses CSS custom properties defined in `wwwroot/css/app.css` under `.app-shell.dark` and `.app-shell.light` — no CSS framework is used.

### Charts

Both the revenue line chart (`RevenueChart.razor`) and category donut chart (`CategoryBreakdown.razor`) are rendered as inline SVG directly in Razor — no charting library dependency.
