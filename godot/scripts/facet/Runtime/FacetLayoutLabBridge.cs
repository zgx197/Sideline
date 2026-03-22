#nullable enable

using System;
using System.IO;
using System.Text;
using System.Text.Json;
using Godot;

namespace Sideline.Facet.Runtime
{
    /// <summary>
    /// Facet 编辑器工作台与运行时之间的布局实验室文件桥。
    /// 用于从编辑器面板请求运行时直接打开阶段 11 样例页面，并回写最近一次处理状态。
    /// </summary>
    public static class FacetLayoutLabBridge
    {
        public const string CommandOpenGeneratedLayoutLab = "open_generated_layout_lab";
        public const string CommandOpenTemplateLayoutLab = "open_template_layout_lab";

        public const string StateIdle = "idle";
        public const string StateRequested = "requested";
        public const string StateRunning = "running";
        public const string StateCompleted = "completed";
        public const string StateFailed = "failed";
        public const string StateIgnored = "ignored";

        private const string LabDirectoryName = "facet-lab";
        private const string RequestFileName = "layout-lab-request.json";
        private const string StatusFileName = "layout-lab-status.json";

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

        public static FacetLayoutLabRequest CreateRequest(string command, string issuedBy)
        {
            return new FacetLayoutLabRequest
            {
                RequestId = Guid.NewGuid().ToString("N"),
                Command = command,
                IssuedBy = issuedBy,
                IssuedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
            };
        }

        public static FacetLayoutLabStatus CreateRequestedStatus(FacetLayoutLabRequest request, string message)
        {
            return new FacetLayoutLabStatus
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

        public static bool TryLoadRequest(out FacetLayoutLabRequest? request)
        {
            return TryLoad(GetRequestPath(), out request);
        }

        public static bool TryLoadStatus(out FacetLayoutLabStatus? status)
        {
            return TryLoad(GetStatusPath(), out status);
        }

        public static void SaveRequest(FacetLayoutLabRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            Save(GetRequestPath(), request);
        }

        public static void SaveStatus(FacetLayoutLabStatus status)
        {
            ArgumentNullException.ThrowIfNull(status);
            Save(GetStatusPath(), status);
        }

        public static void DeleteRequest()
        {
            DeleteFileIfExists(GetRequestPath());
        }

        public static bool IsPending(FacetLayoutLabRequest request, FacetLayoutLabStatus? status)
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
    /// 编辑器工作台发往运行时的布局实验室请求。
    /// </summary>
    public sealed class FacetLayoutLabRequest
    {
        public string RequestId { get; init; } = string.Empty;

        public string Command { get; init; } = string.Empty;

        public string IssuedBy { get; init; } = string.Empty;

        public string IssuedAtUtc { get; init; } = string.Empty;
    }

    /// <summary>
    /// 运行时回写给编辑器工作台的布局实验室状态。
    /// </summary>
    public sealed class FacetLayoutLabStatus
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
