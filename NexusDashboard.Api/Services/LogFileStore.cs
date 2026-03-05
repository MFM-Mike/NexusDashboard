using NexusDashboard.Shared.Models;

namespace NexusDashboard.Api.Services;

/// <summary>
/// In-memory store for parsed log files within a session.
/// </summary>
public class LogFileStore
{
    private readonly Dictionary<string, ParsedLogFile> _files = new();
    private readonly LogParserService _parser;

    public LogFileStore(LogParserService parser)
    {
        _parser = parser;
    }

    public IReadOnlyDictionary<string, ParsedLogFile> Files => _files;

    public ParsedLogFile LoadFromPath(string path)
    {
        if (_files.TryGetValue(path, out var cached))
            return cached;

        var fi = new FileInfo(path);
        var fileInfo = new LogFileInfo
        {
            FileName      = fi.Name,
            FullPath      = fi.FullName,
            FileSizeBytes = fi.Length,
            LastModified  = fi.LastWriteTime
        };

        var content  = File.ReadAllText(path);
        var parsed   = _parser.Parse(fileInfo, content);
        _files[path] = parsed;
        return parsed;
    }

    public ParsedLogFile LoadFromContent(string fileName, string content)
    {
        var fileInfo = new LogFileInfo
        {
            FileName      = fileName,
            FullPath      = fileName,
            FileSizeBytes = System.Text.Encoding.UTF8.GetByteCount(content),
            LastModified  = DateTime.Now
        };

        var parsed       = _parser.Parse(fileInfo, content);
        _files[fileName] = parsed;
        return parsed;
    }

    public void Remove(string key) => _files.Remove(key);

    public void Clear() => _files.Clear();
}
