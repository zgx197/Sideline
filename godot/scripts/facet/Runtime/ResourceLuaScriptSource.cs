#nullable enable

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Godot;
using Sideline.Facet.Lua;

namespace Sideline.Facet.Lua
{
    /// <summary>
    /// 基于 Godot 资源路径的 Lua 脚本源。
    /// 主要用于导出后的运行时环境，此时脚本文件位于 PCK 中而非项目源码目录。
    /// </summary>
    public sealed class ResourceLuaScriptSource : ILuaScriptSource
    {
        private readonly HashSet<string> _registeredScripts;

        public ResourceLuaScriptSource(IEnumerable<string> registeredScripts)
        {
            ArgumentNullException.ThrowIfNull(registeredScripts);

            _registeredScripts = new HashSet<string>(registeredScripts, StringComparer.OrdinalIgnoreCase);
        }

        public bool TryGetScript(string scriptId, out LuaScriptAsset? scriptAsset)
        {
            scriptAsset = null;
            if (!TryReadScriptBytes(scriptId, out byte[] bytes))
            {
                return false;
            }

            string sourceCode = Encoding.UTF8.GetString(bytes);
            string versionToken = ComputeVersionToken(bytes);
            scriptAsset = new LuaScriptAsset(scriptId, scriptId, sourceCode, versionToken);
            return true;
        }

        public bool TryGetVersionToken(string scriptId, out string? versionToken)
        {
            versionToken = null;
            if (!TryReadScriptBytes(scriptId, out byte[] bytes))
            {
                return false;
            }

            versionToken = ComputeVersionToken(bytes);
            return true;
        }

        public IReadOnlyCollection<string> GetRegisteredScripts()
        {
            return _registeredScripts;
        }

        private bool TryReadScriptBytes(string scriptId, out byte[] bytes)
        {
            bytes = Array.Empty<byte>();
            if (!_registeredScripts.Contains(scriptId) ||
                !scriptId.StartsWith("res://", StringComparison.OrdinalIgnoreCase) ||
                !FileAccess.FileExists(scriptId))
            {
                return false;
            }

            bytes = FileAccess.GetFileAsBytes(scriptId);
            return true;
        }

        private static string ComputeVersionToken(byte[] bytes)
        {
            byte[] hash = SHA256.HashData(bytes);
            return Convert.ToHexString(hash);
        }
    }
}