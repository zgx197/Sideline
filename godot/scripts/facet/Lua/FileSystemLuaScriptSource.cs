#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Sideline.Facet.Lua
{
    /// <summary>
    /// 基于文件系统的 Lua 脚本源。
    /// 负责把脚本标识解析为真实文件，并为热重载生成稳定版本标记。
    /// </summary>
    public sealed class FileSystemLuaScriptSource : ILuaWritableScriptSource
    {
        private static readonly UTF8Encoding Utf8NoBom = new(false);

        private readonly string _projectRootPath;
        private readonly HashSet<string> _registeredScripts;

        public FileSystemLuaScriptSource(string projectRootPath, IEnumerable<string> registeredScripts)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(projectRootPath);
            ArgumentNullException.ThrowIfNull(registeredScripts);

            _projectRootPath = projectRootPath;
            _registeredScripts = new HashSet<string>(registeredScripts, StringComparer.OrdinalIgnoreCase);
        }

        public bool TryGetScript(string scriptId, out LuaScriptAsset? scriptAsset)
        {
            scriptAsset = null;
            if (!TryResolveExistingScriptPath(scriptId, out string? sourcePath) ||
                string.IsNullOrWhiteSpace(sourcePath))
            {
                return false;
            }

            byte[] bytes = File.ReadAllBytes(sourcePath);
            string sourceCode = Encoding.UTF8.GetString(bytes);
            string versionToken = ComputeVersionToken(bytes);
            scriptAsset = new LuaScriptAsset(scriptId, sourcePath, sourceCode, versionToken);
            return true;
        }

        public bool TryGetVersionToken(string scriptId, out string? versionToken)
        {
            versionToken = null;
            if (!TryResolveExistingScriptPath(scriptId, out string? sourcePath) ||
                string.IsNullOrWhiteSpace(sourcePath))
            {
                return false;
            }

            versionToken = ComputeVersionToken(File.ReadAllBytes(sourcePath));
            return true;
        }

        public IReadOnlyCollection<string> GetRegisteredScripts()
        {
            return _registeredScripts;
        }

        public bool CanWriteScript(string scriptId)
        {
            return TryResolveExistingScriptPath(scriptId, out _);
        }

        public bool TryWriteScript(string scriptId, string sourceCode, out LuaScriptAsset? scriptAsset)
        {
            ArgumentNullException.ThrowIfNull(sourceCode);

            scriptAsset = null;
            if (!TryResolveExistingScriptPath(scriptId, out string? sourcePath) ||
                string.IsNullOrWhiteSpace(sourcePath))
            {
                return false;
            }

            File.WriteAllText(sourcePath, sourceCode, Utf8NoBom);
            return TryGetScript(scriptId, out scriptAsset);
        }

        private bool TryResolveExistingScriptPath(string scriptId, out string? sourcePath)
        {
            sourcePath = null;
            if (!_registeredScripts.Contains(scriptId))
            {
                return false;
            }

            string resolvedPath = ResolveScriptPath(scriptId);
            if (!Path.IsPathRooted(resolvedPath) || !File.Exists(resolvedPath))
            {
                return false;
            }

            sourcePath = resolvedPath;
            return true;
        }

        private string ResolveScriptPath(string scriptId)
        {
            if (scriptId.StartsWith("res://", StringComparison.OrdinalIgnoreCase))
            {
                string relativePath = scriptId["res://".Length..]
                    .Replace('/', Path.DirectorySeparatorChar)
                    .TrimStart(Path.DirectorySeparatorChar);
                return Path.Combine(_projectRootPath, relativePath);
            }

            if (Path.IsPathRooted(scriptId))
            {
                return scriptId;
            }

            return Path.Combine(_projectRootPath, scriptId.Replace('/', Path.DirectorySeparatorChar));
        }

        private static string ComputeVersionToken(byte[] bytes)
        {
            byte[] hash = SHA256.HashData(bytes);
            return Convert.ToHexString(hash);
        }
    }
}
