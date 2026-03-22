#nullable enable

using System.Collections.Generic;

namespace Sideline.Facet.Lua
{
    /// <summary>
    /// Lua 脚本来源抽象。
    /// 当前阶段既支持从真实文件读取脚本，也保留其他脚本来源的扩展空间。
    /// </summary>
    public interface ILuaScriptSource
    {
        bool TryGetScript(string scriptId, out LuaScriptAsset? scriptAsset);

        bool TryGetVersionToken(string scriptId, out string? versionToken);

        IReadOnlyCollection<string> GetRegisteredScripts();
    }
}