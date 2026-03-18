using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace JaxaRainmap.Services.Logging;

/// <summary>
/// Bridges JavaScript console errors into the C# ILogger system.
/// JS calls DotNet.invokeMethodAsync('JaxaRainmap', 'OnJsLog', level, category, message)
/// </summary>
public static class JsLogBridge
{
    private static ILogger? _logger;

    public static void Initialize(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger("JS");
    }

    [JSInvokable]
    public static void OnJsLog(string level, string category, string message)
    {
        if (_logger is null) return;

        var fullMessage = $"[{category}] {message}";

        switch (level.ToLowerInvariant())
        {
            case "error":
                _logger.LogError("{JsMessage}", fullMessage);
                break;
            case "warn":
                _logger.LogWarning("{JsMessage}", fullMessage);
                break;
            case "info":
                _logger.LogInformation("{JsMessage}", fullMessage);
                break;
            default:
                _logger.LogDebug("{JsMessage}", fullMessage);
                break;
        }
    }
}
