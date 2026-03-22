#nullable enable

using System;
using System.Collections.Generic;
using Sideline.Facet.Lua;

namespace Sideline.Facet.Runtime
{
    /// <summary>
    /// Lua 热重载协调器。
    /// 负责轮询当前所有页面运行时，并在检测到脚本版本变化时触发页面级控制器重建。
    /// </summary>
    public sealed class LuaReloadCoordinator
    {
        private readonly UIManager _uiManager;
        private readonly IFacetLogger? _logger;

        public LuaReloadCoordinator(UIManager uiManager, IFacetLogger? logger = null)
        {
            ArgumentNullException.ThrowIfNull(uiManager);

            _uiManager = uiManager;
            _logger = logger;
        }

        /// <summary>
        /// 执行一次 Lua 热重载轮询。
        /// </summary>
        public int Poll(string reason = "poll")
        {
            IReadOnlyList<UIPageRuntime> runtimes = _uiManager.GetPageRuntimesSnapshot();
            int reloadedCount = 0;
            int checkedCount = 0;

            foreach (UIPageRuntime runtime in runtimes)
            {
                if (!runtime.HasLuaController)
                {
                    continue;
                }

                checkedCount++;
                if (!runtime.NeedsLuaHotReload())
                {
                    continue;
                }

                if (runtime.TryReloadLuaController(reason, out LuaReloadResult? result) &&
                    result != null &&
                    result.Reloaded)
                {
                    reloadedCount++;
                    continue;
                }

                if (result != null && !string.IsNullOrWhiteSpace(result.ErrorMessage))
                {
                    _logger?.Warning(
                        "Lua.HotReload",
                        "页面 Lua 热重载未完成。",
                        new Dictionary<string, object?>
                        {
                            ["pageId"] = result.PageId,
                            ["scriptId"] = result.ScriptId,
                            ["reason"] = result.Reason,
                            ["pageState"] = result.PageState,
                            ["oldVersionToken"] = result.OldVersionToken,
                            ["newVersionToken"] = result.NewVersionToken,
                            ["errorMessage"] = result.ErrorMessage,
                        });
                }
            }

            if (reloadedCount > 0)
            {
                _logger?.Info(
                    "Lua.HotReload",
                    "本轮 Lua 热重载轮询已完成。",
                    new Dictionary<string, object?>
                    {
                        ["checkedRuntimeCount"] = checkedCount,
                        ["reloadedRuntimeCount"] = reloadedCount,
                        ["reason"] = reason,
                    });
            }

            return reloadedCount;
        }
    }
}