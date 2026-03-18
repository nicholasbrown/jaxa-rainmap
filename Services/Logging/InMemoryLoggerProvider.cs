using Microsoft.Extensions.Logging;
using JaxaRainmap.Models;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;
using AppLogLevel = JaxaRainmap.Models.LogLevel;

namespace JaxaRainmap.Services.Logging;

public class InMemoryLoggerProvider : ILoggerProvider
{
    private readonly LogBuffer _buffer;

    public InMemoryLoggerProvider(LogBuffer buffer)
    {
        _buffer = buffer;
    }

    public ILogger CreateLogger(string categoryName)
    {
        // Shorten category: "JaxaRainmap.Services.GsmapService" → "GsmapService"
        var shortCategory = categoryName.Contains('.')
            ? categoryName[(categoryName.LastIndexOf('.') + 1)..]
            : categoryName;

        return new InMemoryLogger(shortCategory, _buffer);
    }

    public void Dispose() { }
}

public class InMemoryLogger : ILogger
{
    private readonly string _category;
    private readonly LogBuffer _buffer;

    public InMemoryLogger(string category, LogBuffer buffer)
    {
        _category = category;
        _buffer = buffer;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(MsLogLevel logLevel) => logLevel != MsLogLevel.None;

    public void Log<TState>(MsLogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        var entry = new LogEntry
        {
            Timestamp = DateTime.UtcNow,
            Level = (AppLogLevel)(int)logLevel,
            Category = _category,
            Message = formatter(state, exception),
            Exception = exception?.ToString()
        };

        _buffer.Add(entry);
    }
}

/// <summary>
/// Thread-safe ring buffer that stores the last N log entries.
/// Singleton — shared between the logger provider and the log viewer UI.
/// </summary>
public class LogBuffer
{
    private readonly LogEntry[] _entries;
    private readonly int _capacity;
    private int _head;
    private int _count;
    private readonly object _lock = new();

    public event Action? OnNewEntry;

    public LogBuffer(int capacity = 500)
    {
        _capacity = capacity;
        _entries = new LogEntry[capacity];
    }

    public void Add(LogEntry entry)
    {
        lock (_lock)
        {
            _entries[_head] = entry;
            _head = (_head + 1) % _capacity;
            if (_count < _capacity) _count++;
        }

        OnNewEntry?.Invoke();
    }

    public List<LogEntry> GetAll()
    {
        lock (_lock)
        {
            var result = new List<LogEntry>(_count);
            var start = _count < _capacity ? 0 : _head;
            for (int i = 0; i < _count; i++)
            {
                result.Add(_entries[(start + i) % _capacity]);
            }
            return result;
        }
    }

    public int Count
    {
        get { lock (_lock) return _count; }
    }

    public string ExportAll()
    {
        var entries = GetAll();
        return string.Join("\n", entries.Select(e => e.ToString()));
    }
}
