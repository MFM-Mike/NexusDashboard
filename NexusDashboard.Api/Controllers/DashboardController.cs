using Microsoft.AspNetCore.Mvc;
using NexusDashboard.Api.Services;
using NexusDashboard.Shared.Models;

namespace NexusDashboard.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly DashboardDataService _dataService;

    public DashboardController(DashboardDataService dataService)
    {
        _dataService = dataService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(DashboardSummary), StatusCodes.Status200OK)]
    public ActionResult<DashboardSummary> Get() => Ok(_dataService.GetDashboardData());

    [HttpGet("kpis")]
    [ProducesResponseType(typeof(List<KpiCard>), StatusCodes.Status200OK)]
    public ActionResult<List<KpiCard>> GetKpis() => Ok(_dataService.GetDashboardData().KpiCards);

    [HttpGet("transactions")]
    [ProducesResponseType(typeof(List<Transaction>), StatusCodes.Status200OK)]
    public ActionResult<List<Transaction>> GetTransactions() => Ok(_dataService.GetDashboardData().RecentTransactions);
}
