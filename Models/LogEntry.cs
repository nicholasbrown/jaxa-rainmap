namespace JaxaRainmap.Models;

public class LogEntry
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public LogLevel Level { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Exception { get; set; }

    public string LevelShort => Level switch
    {
        LogLevel.Trace => "TRC",
        LogLevel.Debug => "DBG",
        LogLevel.Information => "INF",
        LogLevel.Warning => "WRN",
        LogLevel.Error => "ERR",
        LogLevel.Critical => "CRT",
        _ => "???"
    };

    public string LevelCss => Level switch
    {
        LogLevel.Trace => "log-trace",
        LogLevel.Debug => "log-debug",
        LogLevel.Information => "log-info",
        LogLevel.Warning => "log-warning",
        LogLevel.Error or LogLevel.Critical => "log-error",
        _ => ""
    };

    public override string ToString()
    {
        var line = $"[{Timestamp:HH:mm:ss.fff}] [{LevelShort}] [{Category}] {Message}";
        if (!string.IsNullOrEmpty(Exception))
            line += $"\n  Exception: {Exception}";
        return line;
    }
}

public enum LogLevel
{
    Trace = 0,
    Debug = 1,
    Information = 2,
    Warning = 3,
    Error = 4,
    Critical = 5,
    None = 6
}
