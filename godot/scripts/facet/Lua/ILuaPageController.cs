#nullable enable

namespace Sideline.Facet.Lua
{
    /// <summary>
    /// Lua 页面控制器统一生命周期接口。
    /// MoonSharp 控制器和后续可能接入的其他 Lua 宿主都遵守这组入口。
    /// </summary>
    public interface ILuaPageController
    {
        void OnInit(LuaApiBridge api);

        void OnShow(LuaApiBridge api);

        void OnRefresh(LuaApiBridge api);

        void OnHide(LuaApiBridge api);

        void OnDispose(LuaApiBridge api);
    }
}