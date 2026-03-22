#nullable enable

using System;
using System.IO;
using System.Text;
using System.Text.Json;
using Godot;

namespace Sideline.Facet.Runtime
{
    /// <summary>
    /// Facet 编辑器工作台与运行时之间的轻量文件桥。
    /// 用于在编辑器主面板中发起热重载测试请求，并由运行时回写最近一次处理状态。
    /// </summary>
    public static class FacetHotReloadLabBridge
    {
        public const string CommandCurrentPageRoundTrip = "current_page_round_trip";
        public const string CommandDungeonRoundTrip = "dungeon_round_trip";

        public const string StateIdle = "idle";
        public const string StateRequested = "requested";
        public const string StateRunning = "running";
        public const string StateCompleted = "completed";
        public const string StateFailed = "failed";
        public const string StateIgnored = "ignored";

        private const string LabDirectoryName = "facet-lab";
        private const string RequestFileName = "hot-reload-request.json";
        private const string StatusFileName = "hot-reload-status.json";

        private static readonly UTF8Encoding Utf8WithoutBom = new(false);
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        public static string GetLabDirectoryPath()
        {
            return ProjectSettings.GlobalizePath($"user://{LabDirectoryName}");
        }

        public static string GetRequestPath()
        {
            return Path.Combine(GetLabDirectoryPath(), RequestFileName);
        }

        public static string GetStatusPath()
        {
            return Path.Combine(GetLabDirectoryPath(), StatusFileName);
        }

        public static FacetHotReloadLabRequest CreateRequest(string command, string issuedBy)
        {
            return new FacetHotReloadLabRequest
            {
                RequestId = Guid.NewGuid().ToString("N"),
                Command = command,
                IssuedBy = issuedBy,
                IssuedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
            };
        }

        public static FacetHotReloadLabStatus CreateRequestedStatus(FacetHotReloadLabRequest request, string message)
        {
            return new FacetHotReloadLabStatus
            {
                RequestId = request.RequestId,
                Command = request.Command,
                State = StateRequested,
                Message = message,
                IssuedBy = request.IssuedBy,
                IssuedAtUtc = request.IssuedAtUtc,
                UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
            };
        }

        public static bool TryLoadRequest(out FacetHotReloadLabRequest? request)
        {
            return TryLoad(GetRequestPath(), out request);
        }

        public static bool TryLoadStatus(out FacetHotReloadLabStatus? status)
        {
            return TryLoad(GetStatusPath(), out status);
        }

        public static void SaveRequest(FacetHotReloadLabRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            Save(GetRequestPath(), request);
        }

        public static void SaveStatus(FacetHotReloadLabStatus status)
        {
            ArgumentNullException.ThrowIfNull(status);
            Save(GetStatusPath(), status);
        }

        public static void DeleteRequest()
        {
            DeleteFileIfExists(GetRequestPath());
        }

        public static bool IsPending(FacetHotReloadLabRequest request, FacetHotReloadLabStatus? status)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (status == null)
            {
                return true;
            }

            if (!string.Equals(request.RequestId, status.RequestId, StringComparison.Ordinal))
            {
                return true;
            }

            return string.Equals(status.State, StateRequested, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsTerminalState(string? state)
        {
            return string.Equals(state, StateCompleted, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(state, StateFailed, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(state, StateIgnored, StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryLoad<TValue>(string path, out TValue? value) where TValue : class
        {
            value = null;

            if (!File.Exists(path))
            {
                return false;
            }

            try
            {
                string json = File.ReadAllText(path, Utf8WithoutBom);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return false;
                }

                value = JsonSerializer.Deserialize<TValue>(json, JsonOptions);
                return value != null;
            }
            catch
            {
                value = null;
                return false;
            }
        }

        private static void Save<TValue>(string path, TValue value)
        {
            string directoryPath = Path.GetDirectoryName(path) ?? GetLabDirectoryPath();
            Directory.CreateDirectory(directoryPath);

            string json = JsonSerializer.Serialize(value, JsonOptions);
            File.WriteAllText(path, json, Utf8WithoutBom);
        }

        private static void DeleteFileIfExists(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    /// <summary>
    /// 编辑器工作台发往运行时的热重载测试请求。
    /// </summary>
    public sealed class FacetHotReloadLabRequest
    {
        public string RequestId { get; init; } = string.Empty;

        public string Command { get; init; } = string.Empty;

        public string IssuedBy { get; init; } = string.Empty;

        public string IssuedAtUtc { get; init; } = string.Empty;
    }

    /// <summary>
    /// 运行时回写给编辑器工作台的热重载测试状态。
    /// </summary>
    public sealed class FacetHotReloadLabStatus
    {
        public string RequestId { get; init; } = string.Empty;

        public string Command { get; init; } = string.Empty;

        public string State { get; init; } = string.Empty;

        public bool? Success { get; init; }

        public string Message { get; init; } = string.Empty;

        public string IssuedBy { get; init; } = string.Empty;

        public string IssuedAtUtc { get; init; } = string.Empty;

        public string UpdatedAtUtc { get; init; } = string.Empty;

        public string RuntimeSessionId { get; init; } = string.Empty;

        public string RuntimePageId { get; init; } = string.Empty;
    }
}
