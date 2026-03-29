#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Sideline.Facet.Extensions.RedDot;
using Sideline.Facet.Lua;
using Sideline.Facet.Projection;
using Sideline.Facet.Projection.Diagnostics;
using Sideline.Facet.Runtime;

namespace Sideline.Facet.Runtime.Debug
{
    /// <summary>
    /// 运行时调试中用到的固定红点路径。
    /// </summary>
    public static class FacetRuntimeDebugPaths
    {
        public const string IdleManualRedDotPath = FacetRedDotLabPaths.IdleManual;
        public const string DungeonManualRedDotPath = FacetRedDotLabPaths.DungeonManual;
    }

    /// <summary>
    /// Runtime Debug 页面聚合快照。
    /// </summary>
    public sealed class FacetRuntimeDebugSnapshot
    {
        public FacetRuntimeDiagnosticsSnapshot? Diagnostics { get; init; }

        public FacetRuntimeMetricListProjection? Metrics { get; init; }

        public FacetHotReloadLabStatus? HotReloadStatus { get; init; }

        public FacetLayoutLabStatus? LayoutLabStatus { get; init; }

        public IReadOnlyList<FacetRuntimeDebugLogEntry> RecentLogs { get; init; } = Array.Empty<FacetRuntimeDebugLogEntry>();
    }

    /// <summary>
    /// Runtime Debug 页面展示用的精简日志条目。
    /// </summary>
    public sealed class FacetRuntimeDebugLogEntry
    {
        public string TimestampUtc { get; init; } = string.Empty;

        public string Level { get; init; } = string.Empty;

        public string Category { get; init; } = string.Empty;

        public string Message { get; init; } = string.Empty;
    }

    /// <summary>
    /// 运行时调试服务，统一负责调试快照读取和常用调试动作。
    /// </summary>
    public static class FacetRuntimeDebugService
    {
        private static readonly UTF8Encoding Utf8WithoutBom = new(false);

        public static FacetRuntimeDebugSnapshot Capture(FacetServices? services, int recentLogCount = 10)
        {
            FacetRuntimeDiagnosticsBridge.TryLoadSnapshot(out FacetRuntimeDiagnosticsSnapshot? diagnostics);
            FacetHotReloadLabBridge.TryLoadStatus(out FacetHotReloadLabStatus? hotReloadStatus);
            FacetLayoutLabBridge.TryLoadStatus(out FacetLayoutLabStatus? layoutLabStatus);

            return new FacetRuntimeDebugSnapshot
            {
                Diagnostics = diagnostics,
                Metrics = LoadRuntimeMetricsProjection(services),
                HotReloadStatus = hotReloadStatus,
                LayoutLabStatus = layoutLabStatus,
                RecentLogs = LoadRecentLogs(services, recentLogCount),
            };
        }

        public static bool TryOpenRuntimeDebug(IUIPageNavigator? navigator, string? sourcePageId, out string statusMessage)
        {
            if (navigator == null)
            {
                statusMessage = $"{FormatStatusTimestamp()} 无法打开 Runtime Debug，页面导航器未就绪。";
                return false;
            }

            Dictionary<string, object?> arguments = new(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(sourcePageId))
            {
                arguments["sourcePageId"] = sourcePageId;
            }

            navigator.Open(UIPageIds.RuntimeDebug, arguments, pushHistory: true);
            statusMessage = $"{FormatStatusTimestamp()} 已打开 Runtime Debug。";
            return true;
        }

        public static bool TryGoBack(IUIPageNavigator? navigator, out string statusMessage)
        {
            if (navigator == null)
            {
                statusMessage = $"{FormatStatusTimestamp()} 返回失败，页面导航器未就绪。";
                return false;
            }

            bool succeeded = navigator.GoBack();
            statusMessage = succeeded
                ? $"{FormatStatusTimestamp()} 已返回上一页。"
                : $"{FormatStatusTimestamp()} 当前没有可返回的页面。";
            return succeeded;
        }

        public static bool TryOpenGeneratedLayoutLab(IUIPageNavigator? navigator, out string statusMessage)
        {
            return TryOpenPage(navigator, UIPageIds.GeneratedLayoutLab, "已打开 Generated Layout Lab。", out statusMessage);
        }

        public static bool TryOpenTemplateLayoutLab(IUIPageNavigator? navigator, out string statusMessage)
        {
            return TryOpenPage(navigator, UIPageIds.TemplateLayoutLab, "已打开 Template Layout Lab。", out statusMessage);
        }

        public static bool TryRunCurrentPageHotReloadTest(FacetServices? services, string reason, out string statusMessage)
        {
            bool accepted = TryRunHotReloadTest(services, scriptId: null, reason);
            statusMessage = accepted
                ? $"{FormatStatusTimestamp()} 已触发当前页面 Lua 热重载测试，请查看运行日志。"
                : $"{FormatStatusTimestamp()} 当前页面 Lua 热重载测试未启动，请检查运行时状态。";
            return accepted;
        }

        public static bool TryRunDungeonHotReloadTest(FacetServices? services, string reason, out string statusMessage)
        {
            bool accepted = TryRunHotReloadTest(services, FacetLuaScriptIds.DungeonRuntimeController, reason);
            statusMessage = accepted
                ? $"{FormatStatusTimestamp()} 已触发地下城页面 Lua 热重载测试，请查看运行日志。"
                : $"{FormatStatusTimestamp()} 地下城页面 Lua 热重载测试未启动，请检查运行时状态。";
            return accepted;
        }

        public static bool TryToggleManualRedDot(FacetServices? services, string path, string targetName, out string statusMessage)
        {
            if (!TryGetManualRedDotProvider(services, out ManualRedDotProvider? manualProvider) || manualProvider == null)
            {
                statusMessage = $"{FormatStatusTimestamp()} 红点测试 Provider 未就绪。";
                return false;
            }

            manualProvider.Toggle(path);
            bool active = manualProvider.IsActive(path);
            statusMessage = $"{FormatStatusTimestamp()} 已切换 {targetName} 测试红点，当前状态 {(active ? "On" : "Off")}。";
            return true;
        }

        public static bool TryClearManualRedDots(FacetServices? services, out string statusMessage)
        {
            if (!TryGetManualRedDotProvider(services, out ManualRedDotProvider? manualProvider) || manualProvider == null)
            {
                statusMessage = $"{FormatStatusTimestamp()} 红点测试 Provider 未就绪。";
                return false;
            }

            manualProvider.ClearAll();
            statusMessage = $"{FormatStatusTimestamp()} 已清空运行时测试红点状态。";
            return true;
        }

        public static bool GetManualRedDotState(FacetServices? services, string path)
        {
            return TryGetManualRedDotProvider(services, out ManualRedDotProvider? manualProvider) &&
                   manualProvider != null &&
                   manualProvider.IsActive(path);
        }

        public static bool TryGetRedDotSnapshot(FacetServices? services, string path, out RedDotNodeSnapshot snapshot)
        {
            snapshot = default!;
            return services?.TryGet(out IRedDotService? redDotService) == true &&
                   redDotService != null &&
                   redDotService.TryGetSnapshot(path, out snapshot);
        }

        private static bool TryOpenPage(
            IUIPageNavigator? navigator,
            string pageId,
            string successMessage,
            out string statusMessage)
        {
            if (navigator == null)
            {
                statusMessage = $"{FormatStatusTimestamp()} 页面导航器未就绪。";
                return false;
            }

            navigator.Open(pageId, arguments: null, pushHistory: true);
            statusMessage = $"{FormatStatusTimestamp()} {successMessage}";
            return true;
        }

        private static IReadOnlyList<FacetRuntimeDebugLogEntry> LoadRecentLogs(FacetServices? services, int recentLogCount)
        {
            string logPath = ResolveStructuredLogPath(services);
            if (string.IsNullOrWhiteSpace(logPath) || !File.Exists(logPath))
            {
                return Array.Empty<FacetRuntimeDebugLogEntry>();
            }

            string[] lines;
            try
            {
                lines = File.ReadAllLines(logPath, Utf8WithoutBom);
            }
            catch
            {
                return Array.Empty<FacetRuntimeDebugLogEntry>();
            }

            List<FacetRuntimeDebugLogEntry> entries = new();
            foreach (string line in lines.Reverse())
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                if (TryParseLogEntry(line, out FacetRuntimeDebugLogEntry? entry) && entry != null)
                {
                    entries.Add(entry);
                }

                if (entries.Count >= Math.Max(1, recentLogCount))
                {
                    break;
                }
            }

            return entries;
        }

        private static FacetRuntimeMetricListProjection? LoadRuntimeMetricsProjection(FacetServices? services)
        {
            if (services?.TryGet(out ProjectionStore? projectionStore) != true || projectionStore == null)
            {
                return null;
            }

            return projectionStore.TryGet(FacetProjectionKeys.RuntimeMetrics, out FacetRuntimeMetricListProjection? metricsProjection)
                ? metricsProjection
                : null;
        }

        private static bool TryParseLogEntry(string jsonLine, out FacetRuntimeDebugLogEntry? entry)
        {
            entry = null;

            try
            {
                using JsonDocument document = JsonDocument.Parse(jsonLine);
                JsonElement root = document.RootElement;

                entry = new FacetRuntimeDebugLogEntry
                {
                    TimestampUtc = TryGetProperty(root, "TimestampUtc") ?? string.Empty,
                    Level = TryGetProperty(root, "Level") ?? string.Empty,
                    Category = TryGetProperty(root, "Category") ?? string.Empty,
                    Message = TryGetProperty(root, "Message") ?? string.Empty,
                };
                return true;
            }
            catch
            {
                entry = null;
                return false;
            }
        }

        private static string? TryGetProperty(JsonElement root, string name)
        {
            if (!root.TryGetProperty(name, out JsonElement value))
            {
                return null;
            }

            return value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : value.ToString();
        }

        private static bool TryRunHotReloadTest(FacetServices? services, string? scriptId, string reason)
        {
            return services?.TryGet(out LuaHotReloadTestService? testService) == true &&
                   testService != null &&
                   testService.TryRunRoundTripTest(scriptId, reason);
        }

        private static string ResolveStructuredLogPath(FacetServices? services)
        {
            if (services?.TryGet(out FacetConfig? config) == true && config != null)
            {
                return config.StructuredLogPath;
            }

            FacetRuntimeEnvironment environment = FacetRuntimeEnvironment.Detect();
            return environment.ResolveLogPath("user://logs/facet-structured.jsonl", "facet-structured.jsonl");
        }

        private static bool TryGetManualRedDotProvider(FacetServices? services, out ManualRedDotProvider? manualProvider)
        {
            manualProvider = null;
            return services?.TryGet(out manualProvider) == true;
        }

        private static string FormatStatusTimestamp()
        {
            return DateTime.Now.ToString("HH:mm:ss");
        }
    }
}
