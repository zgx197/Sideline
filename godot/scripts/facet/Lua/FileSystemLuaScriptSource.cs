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
    public sealed class FileSystemLuaScriptSource : ILuaScriptSource
    {
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
            if (!_registeredScripts.Contains(scriptId))
            {
                return false;
            }

            string sourcePath = ResolveScriptPath(scriptId);
            if (!File.Exists(sourcePath))
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
            if (!_registeredScripts.Contains(scriptId))
            {
                return false;
            }

            string sourcePath = ResolveScriptPath(scriptId);
            if (!File.Exists(sourcePath))
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