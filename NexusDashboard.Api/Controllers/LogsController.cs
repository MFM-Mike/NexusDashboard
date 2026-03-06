using Microsoft.AspNetCore.Mvc;
using NexusDashboard.Api.Services;
using NexusDashboard.Shared.Models;

namespace NexusDashboard.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LogsController : ControllerBase
{
    private readonly LogParserService _parser;

    public LogsController(LogParserService parser) => _parser = parser;

    /// <summary>Returns built-in sample log data for demonstration.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(LogQueryResult), StatusCodes.Status200OK)]
    public ActionResult<LogQueryResult> GetSample() => Ok(_parser.GetSampleData());

    /// <summary>Returns the catalogue of available log bundles.</summary>
    [HttpGet("bundles")]
    [ProducesResponseType(typeof(List<LogBundleInfo>), StatusCodes.Status200OK)]
    public ActionResult<List<LogBundleInfo>> GetBundles() => Ok(_parser.GetBundles());

    /// <summary>Loads, merges, and returns all log sources for a named bundle.</summary>
    [HttpGet("bundles/{id}")]
    [ProducesResponseType(typeof(LogQueryResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<LogQueryResult> GetBundle(string id)
    {
        var bundles = _parser.GetBundles();
        if (!bundles.Any(b => b.Id == id))
            return NotFound($"Bundle '{id}' not found.");

        return Ok(_parser.LoadBundle(id));
    }

    /// <summary>Parses raw log4net-formatted text and returns structured entries + summary.</summary>
    [HttpPost("parse")]
    [ProducesResponseType(typeof(LogQueryResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<LogQueryResult> Parse([FromBody] ParseRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.Content))
            return BadRequest("Log content must not be empty.");

        return Ok(_parser.ParseLogContent(request.Content));
    }
}
