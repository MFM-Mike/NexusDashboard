using NexusDashboard.Shared.Models;

namespace NexusDashboard.Api.Services;

public class DashboardDataService
{
    private readonly Random _rng = new(42);

    public DashboardSummary GetDashboardData()
    {
        return new DashboardSummary
        {
            TotalUsers = 24_831,
            ConversionRate = 3.67,
            KpiCards = new()
            {
                new() { Title = "Total Revenue",   Value = "$1,284,930", Change = "+18.2%", IsPositive = true,  Icon = "💰", AccentColor = "#00E5BE" },
                new() { Title = "Active Users",    Value = "24,831",     Change = "+9.4%",  IsPositive = true,  Icon = "👥", AccentColor = "#7B61FF" },
                new() { Title = "New Orders",      Value = "3,742",      Change = "+5.1%",  IsPositive = true,  Icon = "📦", AccentColor = "#FF6B6B" },
                new() { Title = "Churn Rate",      Value = "2.14%",      Change = "-0.3%",  IsPositive = true,  Icon = "📉", AccentColor = "#FFB547" },
            },
            RevenueData = GenerateRevenueData(),
            RecentTransactions = GenerateTransactions(),
            Categories = new()
            {
                new() { Name = "Software",  Percentage = 42.3, Value = 543_700, Color = "#00E5BE" },
                new() { Name = "Services",  Percentage = 27.8, Value = 357_000, Color = "#7B61FF" },
                new() { Name = "Hardware",  Percentage = 18.5, Value = 237_700, Color = "#FF6B6B" },
                new() { Name = "Support",   Percentage = 11.4, Value = 146_530, Color = "#FFB547" },
            }
        };
    }

    private List<ChartDataPoint> GenerateRevenueData()
    {
        var months = new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
        double prev = 80_000;
        return months.Select(m =>
        {
            prev = prev * (0.9 + _rng.NextDouble() * 0.35);
            return new ChartDataPoint
            {
                Label = m,
                Value = Math.Round(prev),
                SecondaryValue = Math.Round(prev * (0.55 + _rng.NextDouble() * 0.25))
            };
        }).ToList();
    }

    private List<Transaction> GenerateTransactions()
    {
        var customers = new[] { "Acme Corp", "BrightPath Ltd", "CloudNine Inc", "DataForge", "EdgeWave", "FusionTech", "GlobalSoft", "HorizonAI" };
        var products  = new[] { "Enterprise Plan", "Pro License", "Support Bundle", "API Credits", "Training Kit", "Analytics Suite" };
        var statuses  = new[] { "Completed", "Completed", "Completed", "Pending", "Refunded" };

        return Enumerable.Range(0, 10).Select(i => new Transaction
        {
            Id       = $"TXN-{10050 + i}",
            Customer = customers[_rng.Next(customers.Length)],
            Product  = products[_rng.Next(products.Length)],
            Amount   = Math.Round((decimal)(_rng.NextDouble() * 4900 + 100), 2),
            Date     = DateTime.Now.AddHours(-(i * 4 + _rng.Next(3))),
            Status   = statuses[_rng.Next(statuses.Length)]
        }).ToList();
    }
}
