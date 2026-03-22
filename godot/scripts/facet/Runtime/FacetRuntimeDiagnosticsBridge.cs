#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using Godot;

namespace Sideline.Facet.Runtime
{
    /// <summary>
    /// Facet 运行时诊断快照文件桥。
    /// 用于把运行时注册表、活动页面、Projection、Lua 脚本与红点树摘要输出给编辑器工作台读取。
    /// </summary>
    public static class FacetRuntimeDiagnosticsBridge
    {
        private const string LabDirectoryName = "facet-lab";
        private const string SnapshotFileName = "runtime-diagnostics.json";

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

        public static string GetSnapshotPath()
        {
            return Path.Combine(GetLabDirectoryPath(), SnapshotFileName);
        }

        public static bool TryLoadSnapshot(out FacetRuntimeDiagnosticsSnapshot? snapshot)
        {
            snapshot = null;
            string path = GetSnapshotPath();
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

                snapshot = JsonSerializer.Deserialize<FacetRuntimeDiagnosticsSnapshot>(json, JsonOptions);
                return snapshot != null;
            }
            catch
            {
                snapshot = null;
                return false;
            }
        }

        public static void SaveSnapshot(FacetRuntimeDiagnosticsSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);

            string path = GetSnapshotPath();
            string directoryPath = Path.GetDirectoryName(path) ?? GetLabDirectoryPath();
            Directory.CreateDirectory(directoryPath);

            string json = JsonSerializer.Serialize(snapshot, JsonOptions);
            File.WriteAllText(path, json, Utf8WithoutBom);
        }
    }

    /// <summary>
    /// Facet 运行时诊断快照。
    /// </summary>
    public sealed class FacetRuntimeDiagnosticsSnapshot
    {
        public string RuntimeSessionId { get; init; } = string.Empty;

        public string UpdatedAtUtc { get; init; } = string.Empty;

        public string CurrentPageId { get; init; } = string.Empty;

        public int BackStackDepth { get; init; }

        public int RegisteredPageCount { get; init; }

        public List<FacetRuntimeRegisteredPageSnapshot> RegisteredPages { get; init; } = new();

        public int ActiveRuntimeCount { get; init; }

        public List<FacetRuntimePageRuntimeSnapshot> ActiveRuntimes { get; init; } = new();

        public int ProjectionCount { get; init; }

        public List<string> ProjectionKeys { get; init; } = new();

        public int LuaRegisteredScriptCount { get; init; }

        public List<string> LuaRegisteredScripts { get; init; } = new();

        public int RedDotRegisteredPathCount { get; init; }

        public List<string> RedDotPaths { get; init; } = new();

        public int ValidationResultCount { get; init; }

        public int ValidationPassedCount { get; init; }

        public int ValidationWarningCount { get; init; }

        public int ValidationFailedCount { get; init; }

        public List<FacetRuntimeValidationResultSnapshot> ValidationResults { get; init; } = new();
    }

    /// <summary>
    /// 页面注册表条目快照。
    /// </summary>
    public sealed class FacetRuntimeRegisteredPageSnapshot
    {
        public string PageId { get; init; } = string.Empty;

        public string LayoutType { get; init; } = string.Empty;

        public string LayoutPath { get; init; } = string.Empty;

        public string Layer { get; init; } = string.Empty;

        public string CachePolicy { get; init; } = string.Empty;

        public string ControllerScript { get; init; } = string.Empty;
    }

    /// <summary>
    /// 活动页面运行时条目快照。
    /// </summary>
    public sealed class FacetRuntimePageRuntimeSnapshot
    {
        public string PageId { get; init; } = string.Empty;

        public bool IsCurrentPage { get; init; }

        public string State { get; init; } = string.Empty;

        public string LayoutType { get; init; } = string.Empty;

        public string ControllerScript { get; init; } = string.Empty;

        public bool HasLuaController { get; init; }

        public string LuaControllerVersionToken { get; init; } = string.Empty;

        public string PageRootPath { get; init; } = string.Empty;

        public FacetRuntimeBindingScopeSnapshot? BindingScope { get; init; }
    }

    /// <summary>
    /// Binding 作用域摘要快照。
    /// </summary>
    public sealed class FacetRuntimeBindingScopeSnapshot
    {
        public string ScopeId { get; init; } = string.Empty;

        public int BindingCount { get; init; }

        public int RefreshCount { get; init; }

        public string LastRefreshReason { get; init; } = string.Empty;
    }

    /// <summary>
    /// 运行时校验结果快照。
    /// </summary>
    public sealed class FacetRuntimeValidationResultSnapshot
    {
        public string RuleId { get; init; } = string.Empty;

        public string Severity { get; init; } = string.Empty;

        public string Status { get; init; } = string.Empty;

        public string Subject { get; init; } = string.Empty;

        public string Message { get; init; } = string.Empty;
    }
}
