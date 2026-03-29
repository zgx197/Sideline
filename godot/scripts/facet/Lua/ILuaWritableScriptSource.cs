#nullable enable

namespace Sideline.Facet.Lua
{
    /// <summary>
    /// 支持写回的 Lua 脚本源抽象。
    /// 仅开发态文件系统脚本源等可变后端应实现该接口。
    /// </summary>
    public interface ILuaWritableScriptSource : ILuaScriptSource
    {
        bool CanWriteScript(string scriptId);

        bool TryWriteScript(string scriptId, string sourceCode, out LuaScriptAsset? scriptAsset);
    }
}
