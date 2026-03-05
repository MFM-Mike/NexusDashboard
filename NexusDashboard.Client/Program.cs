using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using NexusDashboard.Client;
using NexusDashboard.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri("https://localhost:7000/")
});

builder.Services.AddScoped<DashboardDataService>();
builder.Services.AddScoped<ThemeService>();

await builder.Build().RunAsync();
