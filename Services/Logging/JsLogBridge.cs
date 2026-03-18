using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace JaxaRainmap.Services.Logging;

/// <summary>
/// Bridges JavaScript console errors and animation callbacks into C#.
/// </summary>
public static class JsLogBridge
{
    private static ILogger? _logger;

    /// <summary>Callback fired when prebuffer progress updates.</summary>
    public static event Action<int, int>? OnBufferProgress;

    /// <summary>Callback fired when animation frame changes in JS.</summary>
    public static event Action<int>? OnAnimFrameChanged;

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

    [JSInvokable]
    public static void OnBufferProgressCallback(int loaded, int total)
    {
        OnBufferProgress?.Invoke(loaded, total);
    }

    [JSInvokable]
    public static void OnAnimFrameChangedCallback(int frameIndex)
    {
        OnAnimFrameChanged?.Invoke(frameIndex);
    }
}
