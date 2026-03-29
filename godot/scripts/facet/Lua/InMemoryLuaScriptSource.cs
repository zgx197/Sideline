#nullable enable

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Sideline.Facet.Lua
{
    /// <summary>
    /// 内存型 Lua 脚本源。
    /// 主要用于测试或离线构造脚本文本。
    /// </summary>
    public sealed class InMemoryLuaScriptSource : ILuaWritableScriptSource
    {
        private readonly Dictionary<string, LuaScriptAsset> _scripts;

        public InMemoryLuaScriptSource(IReadOnlyDictionary<string, string> scripts)
        {
            ArgumentNullException.ThrowIfNull(scripts);

            _scripts = new Dictionary<string, LuaScriptAsset>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, string> pair in scripts)
            {
                _scripts[pair.Key] = CreateAsset(pair.Key, pair.Value);
            }
        }

        public bool TryGetScript(string scriptId, out LuaScriptAsset? scriptAsset)
        {
            return _scripts.TryGetValue(scriptId, out scriptAsset);
        }

        public bool TryGetVersionToken(string scriptId, out string? versionToken)
        {
            versionToken = null;
            if (!_scripts.TryGetValue(scriptId, out LuaScriptAsset? scriptAsset) || scriptAsset == null)
            {
                return false;
            }

            versionToken = scriptAsset.VersionToken;
            return true;
        }

        public IReadOnlyCollection<string> GetRegisteredScripts()
        {
            return _scripts.Keys;
        }

        public bool CanWriteScript(string scriptId)
        {
            return _scripts.ContainsKey(scriptId);
        }

        public bool TryWriteScript(string scriptId, string sourceCode, out LuaScriptAsset? scriptAsset)
        {
            ArgumentNullException.ThrowIfNull(sourceCode);

            scriptAsset = null;
            if (!_scripts.ContainsKey(scriptId))
            {
                return false;
            }

            scriptAsset = CreateAsset(scriptId, sourceCode);
            _scripts[scriptId] = scriptAsset;
            return true;
        }

        private static LuaScriptAsset CreateAsset(string scriptId, string sourceCode)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(sourceCode);
            string versionToken = Convert.ToHexString(SHA256.HashData(bytes));
            return new LuaScriptAsset(scriptId, scriptId, sourceCode, versionToken);
        }
    }
}
