#nullable enable

using System.Collections.Generic;
using System.Text.Json;
using Godot;
using Sideline.Facet.Runtime;

/// <summary>
/// 客户端层统一日志入口。
/// 优先复用 Facet 结构化日志链路；若宿主尚未初始化，则回退到普通 Godot 输出。
/// </summary>
internal static class ClientLog
{
    public static void Info(string category, string message, IReadOnlyDictionary<string, object?>? payload = null)
    {
        Log(FacetLogLevel.Info, category, message, payload);
    }

    public static void Warning(string category, string message, IReadOnlyDictionary<string, object?>? payload = null)
    {
        Log(FacetLogLevel.Warning, category, message, payload);
    }

    public static void Error(string category, string message, IReadOnlyDictionary<string, object?>? payload = null)
    {
        Log(FacetLogLevel.Error, category, message, payload);
    }

    private static void Log(FacetLogLevel level, string category, string message, IReadOnlyDictionary<string, object?>? payload)
    {
        string normalizedCategory = $"Client.{category}";

        if (FacetHost.Instance?.Logger is IFacetLogger logger)
        {
            logger.Log(level, normalizedCategory, message, payload);
            return;
        }

        string formatted = FormatFallbackLine(level, category, message, payload);
        switch (level)
        {
            case FacetLogLevel.Warning:
                GD.PushWarning(formatted);
                break;
            case FacetLogLevel.Error:
                GD.PushError(formatted);
                break;
            default:
                GD.Print(formatted);
                break;
        }
    }

    private static string FormatFallbackLine(
        FacetLogLevel level,
        string category,
        string message,
        IReadOnlyDictionary<string, object?>? payload)
    {
        if (payload == null || payload.Count == 0)
        {
            return $"[Client][{level}][{category}] {message}";
        }

        return $"[Client][{level}][{category}] {message} Payload={JsonSerializer.Serialize(payload)}";
    }
}
