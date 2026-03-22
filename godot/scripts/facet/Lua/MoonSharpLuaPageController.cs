#nullable enable

using System;
using MoonSharp.Interpreter;

namespace Sideline.Facet.Lua
{
    /// <summary>
    /// 基于 MoonSharp 的真实 Lua 页面控制器。
    /// 约定脚本使用全局函数形式暴露页面生命周期入口。
    /// </summary>
    public sealed class MoonSharpLuaPageController : ILuaPageController
    {
        private static bool _userDataRegistered;

        private readonly Script _script;
        private readonly LuaScriptAsset _scriptAsset;

        public MoonSharpLuaPageController(LuaScriptAsset scriptAsset)
        {
            ArgumentNullException.ThrowIfNull(scriptAsset);

            EnsureUserDataRegistered();
            _scriptAsset = scriptAsset;
            _script = new Script(CoreModules.Preset_SoftSandbox);
            _script.DoString(scriptAsset.SourceCode, null, scriptAsset.SourcePath);
        }

        public void OnInit(LuaApiBridge api)
        {
            InvokeIfExists("OnInit", api);
        }

        public void OnShow(LuaApiBridge api)
        {
            InvokeIfExists("OnShow", api);
        }

        public void OnRefresh(LuaApiBridge api)
        {
            InvokeIfExists("OnRefresh", api);
        }

        public void OnHide(LuaApiBridge api)
        {
            InvokeIfExists("OnHide", api);
        }

        public void OnDispose(LuaApiBridge api)
        {
            InvokeIfExists("OnDispose", api);
        }

        private void InvokeIfExists(string functionName, LuaApiBridge api)
        {
            DynValue function = _script.Globals.Get(functionName);
            if (function.IsNil() || function.Type != DataType.Function)
            {
                return;
            }

            _script.Call(function, api);
        }

        private static void EnsureUserDataRegistered()
        {
            if (_userDataRegistered)
            {
                return;
            }

            UserData.RegisterType<LuaApiBridge>();
            UserData.RegisterType<LuaBindingScopeBridge>();
            UserData.RegisterType<LuaBindingScopeBridge.LuaStructuredListItem>();
            _userDataRegistered = true;
        }
    }
}
