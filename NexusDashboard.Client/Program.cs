using NexusDashboard.Api.Services;
using NexusDashboard.Client.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Reuse API services directly (Blazor Server has server-side access)
builder.Services.AddSingleton<LogParserService>();
builder.Services.AddSingleton<LogFileStore>();
builder.Services.AddScoped<DashboardStateService>();

var app = builder.Build();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<NexusDashboard.Client.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();
