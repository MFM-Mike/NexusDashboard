using NexusDashboard.Api.Services;
using NexusDashboard.Shared.Models;

namespace NexusDashboard.Client.Services;

public class DashboardStateService
{
    private readonly LogFileStore _store;
    private readonly LogParserService _parser;

    public DashboardStateService(LogFileStore store, LogParserService parser)
    {
        _store  = store;
        _parser = parser;
    }

    public string? ActiveFileKey { get; private set; }
    public ParsedLogFile? ActiveFile => ActiveFileKey != null && _store.Files.TryGetValue(ActiveFileKey, out var f) ? f : null;

    public event Action? OnStateChanged;

    public IEnumerable<ParsedLogFile> LoadedFiles => _store.Files.Values;

    public async Task<ParsedLogFile> UploadFileAsync(string fileName, Stream stream)
    {
        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();
        var parsed = _store.LoadFromContent(fileName, content);
        ActiveFileKey = fileName;
        OnStateChanged?.Invoke();
        return parsed;
    }

    public ParsedLogFile LoadFromPath(string path)
    {
        var parsed = _store.LoadFromPath(path);
        ActiveFileKey = path;
        OnStateChanged?.Invoke();
        return parsed;
    }

    public void SetActiveFile(string key)
    {
        ActiveFileKey = key;
        OnStateChanged?.Invoke();
    }

    public void RemoveFile(string key)
    {
        _store.Remove(key);
        if (ActiveFileKey == key)
            ActiveFileKey = _store.Files.Keys.FirstOrDefault();
        OnStateChanged?.Invoke();
    }

    public PagedLogResult QueryEntries(LogFilter filter)
    {
        if (ActiveFile == null)
            return new PagedLogResult();

        return _parser.ApplyFilter(ActiveFile.Entries, filter);
    }
}
