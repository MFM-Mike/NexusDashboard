namespace NexusDashboard.Shared.Models;

public class KpiCard
{
    public string Title { get; set; } = "";
    public string Value { get; set; } = "";
    public string Change { get; set; } = "";
    public bool IsPositive { get; set; }
    public string Icon { get; set; } = "";
    public string AccentColor { get; set; } = "";
}

public class ChartDataPoint
{
    public string Label { get; set; } = "";
    public double Value { get; set; }
    public double SecondaryValue { get; set; }
}

public class Transaction
{
    public string Id { get; set; } = "";
    public string Customer { get; set; } = "";
    public string Product { get; set; } = "";
    public decimal Amount { get; set; }
    public DateTime Date { get; set; }
    public string Status { get; set; } = "";
}

public class CategoryData
{
    public string Name { get; set; } = "";
    public double Percentage { get; set; }
    public string Color { get; set; } = "";
    public double Value { get; set; }
}

public class DashboardSummary
{
    public List<KpiCard> KpiCards { get; set; } = new();
    public List<ChartDataPoint> RevenueData { get; set; } = new();
    public List<Transaction> RecentTransactions { get; set; } = new();
    public List<CategoryData> Categories { get; set; } = new();
    public int TotalUsers { get; set; }
    public double ConversionRate { get; set; }
}
