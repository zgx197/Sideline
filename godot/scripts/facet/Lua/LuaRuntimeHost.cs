#nullable enable

using System;
using System.Collections.Generic;
using Sideline.Facet.Runtime;

namespace Sideline.Facet.Lua
{
    /// <summary>
    /// Facet 默认 Lua 运行时宿主。
    /// 负责从脚本源读取脚本，并通过 MoonSharp 创建页面控制器实例。
    /// </summary>
    public sealed class LuaRuntimeHost : ILuaRuntimeHost
    {
        private readonly ILuaScriptSource _scriptSource;
        private readonly ILuaRedDotBridge _redDotBridge;

        public LuaRuntimeHost(ILuaScriptSource scriptSource, ILuaRedDotBridge redDotBridge)
        {
            ArgumentNullException.ThrowIfNull(scriptSource);
            ArgumentNullException.ThrowIfNull(redDotBridge);

            _scriptSource = scriptSource;
            _redDotBridge = redDotBridge;
        }

        public bool TryCreateController(UIContext context, out LuaControllerHandle? controllerHandle)
        {
            ArgumentNullException.ThrowIfNull(context);

            controllerHandle = null;
            string? controllerScript = context.Definition.ControllerScript;
            if (string.IsNullOrWhiteSpace(controllerScript))
            {
                return false;
            }

            if (!_scriptSource.TryGetScript(controllerScript, out LuaScriptAsset? scriptAsset) || scriptAsset == null)
            {
                context.Logger.Warning(
                    "Lua.Runtime",
                    "Lua 控制器脚本不存在，页面将继续使用当前 C# 生命周期。",
                    new Dictionary<string, object?>
                    {
                        ["pageId"] = context.PageId,
                        ["controllerScript"] = controllerScript,
                    });
                return false;
            }

            try
            {
                ILuaPageController controller = new MoonSharpLuaPageController(scriptAsset);
                LuaApiBridge api = new(context, _redDotBridge);
                controllerHandle = new LuaControllerHandle(
                    scriptAsset.ScriptId,
                    scriptAsset.SourcePath,
                    scriptAsset.VersionToken,
                    controller,
                    api);

                context.AttachLua(api);
                context.Logger.Info(
                    "Lua.Runtime",
                    "Lua 控制器已创建。",
                    new Dictionary<string, object?>
                    {
                        ["pageId"] = context.PageId,
                        ["controllerScript"] = controllerScript,
                        ["sourcePath"] = scriptAsset.SourcePath,
                        ["versionToken"] = scriptAsset.VersionToken,
                        ["hasResolver"] = context.Resolver != null,
                        ["hasBindings"] = context.Bindings != null,
                        ["supportsRedDot"] = _redDotBridge.IsAvailable,
                    });
                return true;
            }
            catch (Exception exception)
            {
                context.Logger.Error(
                    "Lua.Runtime",
                    "Lua 控制器创建失败。",
                    new Dictionary<string, object?>
                    {
                        ["pageId"] = context.PageId,
                        ["controllerScript"] = controllerScript,
                        ["exceptionType"] = exception.GetType().FullName,
                        ["message"] = exception.Message,
                    });
                return false;
            }
        }

        public bool NeedsReload(LuaControllerHandle controllerHandle)
        {
            ArgumentNullException.ThrowIfNull(controllerHandle);

            if (!_scriptSource.TryGetVersionToken(controllerHandle.ScriptId, out string? currentVersionToken) ||
                string.IsNullOrWhiteSpace(currentVersionToken))
            {
                return false;
            }

            return !string.Equals(currentVersionToken, controllerHandle.VersionToken, StringComparison.Ordinal);
        }
    }
}