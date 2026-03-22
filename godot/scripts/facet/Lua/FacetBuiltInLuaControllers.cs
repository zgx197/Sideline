#nullable enable

using System;
using System.Collections.Generic;
using Sideline.Facet.Application;
using Sideline.Facet.Application.Diagnostics;

namespace Sideline.Facet.Lua
{
    /// <summary>
    /// 阶段 8 内置控制器注册表。
    /// 先通过 C# 控制器模拟 Lua 页面脚本，确保宿主边界、生命周期和日志链路都已跑通。
    /// </summary>
    public static class FacetBuiltInLuaControllers
    {
        public static IReadOnlyDictionary<string, Func<ILuaPageController>> CreateFactories()
        {
            return new Dictionary<string, Func<ILuaPageController>>(StringComparer.OrdinalIgnoreCase)
            {
                [FacetLuaScriptIds.IdleRuntimeController] = static () => new IdleRuntimeController(),
                [FacetLuaScriptIds.DungeonRuntimeController] = static () => new DungeonRuntimeController(),
            };
        }

        private sealed class IdleRuntimeController : ILuaPageController
        {
            private int _refreshCount;

            public void OnInit(LuaApiBridge api)
            {
                bool hasTitleLabel = api.TryResolve<object>("TitleLabel", out _);
                bool hasSwitchButton = api.TryResolve<object>("SwitchButton", out _);
                AppResult<FacetRuntimeProbeStatusSnapshot> statusResult = api.Query(new FacetRuntimeProbeStatusQuery());

                api.LogInfo(
                    "Lua 控制器 OnInit。",
                    new Dictionary<string, object?>
                    {
                        ["controller"] = "IdleRuntimeController",
                        ["hasTitleLabel"] = hasTitleLabel,
                        ["hasSwitchButton"] = hasSwitchButton,
                        ["probeQuerySuccess"] = statusResult.IsSuccess,
                        ["probeRecordedCount"] = statusResult.Value?.RecordedCount,
                    });

                api.RefreshBindings("lua.controller.init");
            }

            public void OnShow(LuaApiBridge api)
            {
                api.LogInfo(
                    "Lua 控制器 OnShow。",
                    new Dictionary<string, object?>
                    {
                        ["controller"] = "IdleRuntimeController",
                        ["canGoBack"] = api.CanGoBack,
                    });
            }

            public void OnRefresh(LuaApiBridge api)
            {
                _refreshCount++;
                api.LogInfo(
                    "Lua 控制器 OnRefresh。",
                    new Dictionary<string, object?>
                    {
                        ["controller"] = "IdleRuntimeController",
                        ["refreshCount"] = _refreshCount,
                    });
            }

            public void OnHide(LuaApiBridge api)
            {
                api.LogInfo(
                    "Lua 控制器 OnHide。",
                    new Dictionary<string, object?>
                    {
                        ["controller"] = "IdleRuntimeController",
                        ["backStackDepth"] = api.BackStackDepth,
                    });
            }

            public void OnDispose(LuaApiBridge api)
            {
                api.LogInfo(
                    "Lua 控制器 OnDispose。",
                    new Dictionary<string, object?>
                    {
                        ["controller"] = "IdleRuntimeController",
                    });
            }
        }

        private sealed class DungeonRuntimeController : ILuaPageController
        {
            private int _refreshCount;

            public void OnInit(LuaApiBridge api)
            {
                bool hasMetricsPanel = api.TryResolve<object>("MetricsPanel", out _);
                bool hasMetricsList = api.TryResolve<object>("MetricsListContainer", out _);

                api.LogInfo(
                    "Lua 控制器 OnInit。",
                    new Dictionary<string, object?>
                    {
                        ["controller"] = "DungeonRuntimeController",
                        ["hasMetricsPanel"] = hasMetricsPanel,
                        ["hasMetricsList"] = hasMetricsList,
                    });
            }

            public void OnShow(LuaApiBridge api)
            {
                api.LogInfo(
                    "Lua 控制器 OnShow。",
                    new Dictionary<string, object?>
                    {
                        ["controller"] = "DungeonRuntimeController",
                        ["canGoBack"] = api.CanGoBack,
                    });
            }

            public void OnRefresh(LuaApiBridge api)
            {
                _refreshCount++;
                AppResult<FacetRuntimeProbeSnapshot> probeResult = api.Query(new FacetRuntimeProbeQuery());

                api.LogInfo(
                    "Lua 控制器 OnRefresh。",
                    new Dictionary<string, object?>
                    {
                        ["controller"] = "DungeonRuntimeController",
                        ["refreshCount"] = _refreshCount,
                        ["probeQuerySuccess"] = probeResult.IsSuccess,
                        ["sessionId"] = probeResult.Value?.SessionId,
                    });
            }

            public void OnHide(LuaApiBridge api)
            {
                api.LogInfo(
                    "Lua 控制器 OnHide。",
                    new Dictionary<string, object?>
                    {
                        ["controller"] = "DungeonRuntimeController",
                        ["backStackDepth"] = api.BackStackDepth,
                    });
            }

            public void OnDispose(LuaApiBridge api)
            {
                api.LogInfo(
                    "Lua 控制器 OnDispose。",
                    new Dictionary<string, object?>
                    {
                        ["controller"] = "DungeonRuntimeController",
                    });
            }
        }
    }
}