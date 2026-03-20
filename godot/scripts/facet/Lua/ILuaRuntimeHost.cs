#nullable enable

using Sideline.Facet.Runtime;

namespace Sideline.Facet.Lua
{
    /// <summary>
    /// Lua 运行时宿主抽象。
    /// 负责解析页面脚本、创建控制器实例，并判断脚本是否发生热更新。
    /// </summary>
    public interface ILuaRuntimeHost
    {
        bool TryCreateController(UIContext context, out LuaControllerHandle? controllerHandle);

        bool NeedsReload(LuaControllerHandle controllerHandle);
    }
}