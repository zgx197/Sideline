#nullable enable

using System;
using System.Collections.Generic;

namespace Sideline.Facet.Lua
{
    /// <summary>
    /// 内存版 Lua 脚本源。
    /// 主要用于测试或离线构造脚本文本，不再负责直接创建控制器实例。
    /// </summary>
    public sealed class InMemoryLuaScriptSource : ILuaScriptSource
    {
        private readonly Dictionary<string, LuaScriptAsset> _scripts;

        public InMemoryLuaScriptSource(IReadOnlyDictionary<string, string> scripts)
        {
            ArgumentNullException.ThrowIfNull(scripts);

            _scripts = new Dictionary<string, LuaScriptAsset>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, string> pair in scripts)
            {
                string versionToken = $"memory:{pair.Value.GetHashCode():X8}";
                _scripts[pair.Key] = new LuaScriptAsset(pair.Key, pair.Key, pair.Value, versionToken);
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
    }
}