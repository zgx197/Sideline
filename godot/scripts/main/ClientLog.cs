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
    /// <summary>
    /// 写入一条信息级客户端日志。
    /// </summary>
    public static void Info(string category, string message, IReadOnlyDictionary<string, object?>? payload = null)
    {
        Log(FacetLogLevel.Info, category, message, payload);
    }

    /// <summary>
    /// 写入一条警告级客户端日志。
    /// </summary>
    public static void Warning(string category, string message, IReadOnlyDictionary<string, object?>? payload = null)
    {
        Log(FacetLogLevel.Warning, category, message, payload);
    }

    /// <summary>
    /// 写入一条错误级客户端日志。
    /// </summary>
    public static void Error(string category, string message, IReadOnlyDictionary<string, object?>? payload = null)
    {
        Log(FacetLogLevel.Error, category, message, payload);
    }

    /// <summary>
    /// 统一分发客户端日志。
    /// 已初始化时写入 FacetLogger，未初始化时降级到 Godot 控制台输出。
    /// </summary>
    private static void Log(FacetLogLevel level, string category, string message, IReadOnlyDictionary<string, object?>? payload)
    {
        string normalizedCategory = $"Client.{category}";

        if (FacetHost.Instance?.Logger is IFacetLogger logger)
        {
            logger.Log(level, normalizedCategory, message, payload);
            return;
        }

        FacetPlainTextLogEncoding.EnsureGodotLogUtf8Bom();
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

    /// <summary>
    /// 构造 Godot 控制台回退日志文本。
    /// 当结构化日志不可用时，尽量保持输出格式与主链路接近。
    /// </summary>
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
