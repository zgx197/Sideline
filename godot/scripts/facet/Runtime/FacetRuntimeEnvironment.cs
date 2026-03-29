#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using Sideline.Facet.Lua;

namespace Sideline.Facet.Runtime
{
    /// <summary>
    /// Facet 运行时环境描述与装配入口。
    /// 统一封装编辑器态与导出态的路径解析和脚本源选择逻辑。
    /// </summary>
    public sealed class FacetRuntimeEnvironment
    {
        private const string UserScheme = "user://";
        private const string ResourceScheme = "res://";

        private FacetRuntimeEnvironment(bool isEditor, string? projectRootPath, string executableDirectory)
        {
            IsEditor = isEditor;
            ProjectRootPath = string.IsNullOrWhiteSpace(projectRootPath) ? null : projectRootPath;
            ExecutableDirectory = string.IsNullOrWhiteSpace(executableDirectory)
                ? Directory.GetCurrentDirectory()
                : executableDirectory;
        }

        public bool IsEditor { get; }

        public string? ProjectRootPath { get; }

        public string ExecutableDirectory { get; }

        public bool UsesPackagedResources => string.IsNullOrWhiteSpace(ProjectRootPath);

        public static FacetRuntimeEnvironment Detect()
        {
            string executablePath = OS.GetExecutablePath();
            string executableDirectory = Path.GetDirectoryName(executablePath) ?? Directory.GetCurrentDirectory();
            string projectRootPath = ProjectSettings.GlobalizePath(ResourceScheme);

            return new FacetRuntimeEnvironment(
                OS.HasFeature("editor"),
                projectRootPath,
                executableDirectory);
        }

        public ILuaScriptSource CreateLuaScriptSource(IEnumerable<string> registeredScripts)
        {
            ArgumentNullException.ThrowIfNull(registeredScripts);

            return UsesPackagedResources
                ? new ResourceLuaScriptSource(registeredScripts)
                : new FileSystemLuaScriptSource(ProjectRootPath!, registeredScripts);
        }

        public string ResolveLogPath(string configuredPath, string defaultFileName)
        {
            if (string.IsNullOrWhiteSpace(defaultFileName))
            {
                throw new ArgumentException("日志文件名不能为空。", nameof(defaultFileName));
            }

            string normalizedPath = string.IsNullOrWhiteSpace(configuredPath)
                ? $"{UserScheme}logs/{defaultFileName}"
                : configuredPath;

            if (IsEditor)
            {
                return normalizedPath;
            }

            if (normalizedPath.StartsWith(UserScheme, StringComparison.OrdinalIgnoreCase) ||
                normalizedPath.StartsWith(ResourceScheme, StringComparison.OrdinalIgnoreCase))
            {
                return Path.Combine(ExecutableDirectory, "logs", Path.GetFileName(normalizedPath));
            }

            if (Path.IsPathRooted(normalizedPath))
            {
                return normalizedPath;
            }

            return Path.GetFullPath(Path.Combine(ExecutableDirectory, normalizedPath));
        }
    }
}
