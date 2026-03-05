using Microsoft.AspNetCore.Mvc;
using NexusDashboard.Api.Services;
using NexusDashboard.Shared.Models;

namespace NexusDashboard.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LogsController : ControllerBase
{
    private readonly LogFileStore _store;
    private readonly LogParserService _parser;

    public LogsController(LogFileStore store, LogParserService parser)
    {
        _store  = store;
        _parser = parser;
    }

    /// <summary>List all currently loaded log files.</summary>
    [HttpGet("files")]
    public IActionResult GetFiles()
    {
        var infos = _store.Files.Values.Select(f => f.FileInfo).ToList();
        return Ok(infos);
    }

    /// <summary>Upload a log file for parsing.</summary>
    [HttpPost("upload")]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file provided.");

        using var reader  = new StreamReader(file.OpenReadStream());
        var content       = await reader.ReadToEndAsync();
        var parsed        = _store.LoadFromContent(file.FileName, content);

        return Ok(new { key = file.FileName, summary = parsed.Summary, format = parsed.DetectedFormat, error = parsed.ParseError });
    }

    /// <summary>Load a log file by absolute path on the server.</summary>
    [HttpPost("load-path")]
    public IActionResult LoadPath([FromBody] LoadPathRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Path))
            return BadRequest("Path is required.");

        if (!System.IO.File.Exists(request.Path))
            return NotFound($"File not found: {request.Path}");

        var parsed = _store.LoadFromPath(request.Path);
        return Ok(new { key = request.Path, summary = parsed.Summary, format = parsed.DetectedFormat, error = parsed.ParseError });
    }

    /// <summary>Get summary statistics for a loaded file.</summary>
    [HttpGet("{key}/summary")]
    public IActionResult GetSummary(string key)
    {
        key = Uri.UnescapeDataString(key);
        if (!_store.Files.TryGetValue(key, out var file))
            return NotFound();

        return Ok(file.Summary);
    }

    /// <summary>Query entries with optional filtering and paging.</summary>
    [HttpPost("{key}/entries")]
    public IActionResult GetEntries(string key, [FromBody] LogFilter filter)
    {
        key = Uri.UnescapeDataString(key);
        if (!_store.Files.TryGetValue(key, out var file))
            return NotFound();

        var result = _parser.ApplyFilter(file.Entries, filter);
        return Ok(result);
    }

    /// <summary>Remove a loaded file from memory.</summary>
    [HttpDelete("{key}")]
    public IActionResult Remove(string key)
    {
        key = Uri.UnescapeDataString(key);
        _store.Remove(key);
        return NoContent();
    }
}

public record LoadPathRequest(string Path);
