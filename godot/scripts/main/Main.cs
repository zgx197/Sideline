#nullable enable

using System.Collections.Generic;
using Godot;
using Sideline.Facet.Projection;
using Sideline.Facet.Projection.Client;
using Sideline.Facet.Projection.Diagnostics;
using Sideline.Facet.Runtime;

/// <summary>
/// 主场景控制器。
/// 负责连接窗口模式、Facet 页面运行时与 Projection 演示链路。
/// </summary>
public partial class Main : Node
{
    /// <summary>
    /// 窗口管理器，负责挂机/地下城模式下的窗口尺寸与样式切换。
    /// </summary>
    private WindowManager _windowManager = null!;

    /// <summary>
    /// 页面挂载根节点。
    /// 当前阶段直接复用 Main 场景中的 CanvasLayer，后续可扩展为分层挂载容器。
    /// </summary>
    private CanvasLayer _pageMountRoot = null!;

    /// <summary>
    /// Facet 页面管理器。
    /// </summary>
    private UIManager? _uiManager;

    /// <summary>
    /// 主场景准备完成后绑定窗口事件，并通过 UIManager 打开默认页面。
    /// </summary>
    public override void _Ready()
    {
        _windowManager = GetNode<WindowManager>("WindowManager");
        _pageMountRoot = GetNode<CanvasLayer>("CanvasLayer");

        Callable modeChangedCallable = Callable.From<int>(OnModeChanged);
        if (!_windowManager.IsConnected("ModeChanged", modeChangedCallable))
        {
            _windowManager.Connect("ModeChanged", modeChangedCallable);
        }

        BindPageRuntime();
        OpenPageForMode(WindowManager.GameMode.Idle, pushHistory: false);
        PublishClientShellProjection(WindowManager.GameMode.Idle);

        ClientLog.Info(
            "Main",
            "Sideline \u9636\u6bb5 5 \u9875\u9762\u8fd0\u884c\u65f6\u5df2\u63a5\u5165\u3002",
            new Dictionary<string, object?>
            {
                ["scenePath"] = GetPath().ToString(),
                ["currentMode"] = WindowManager.GameMode.Idle.ToString(),
                ["currentPageId"] = _uiManager?.CurrentPageId,
                ["backStackDepth"] = _uiManager?.BackStackDepth,
                ["currentPageState"] = _uiManager?.CurrentRuntime?.State.ToString(),
                ["bindingCount"] = _uiManager?.CurrentRuntime?.BindingScope?.Count ?? 0,
            });

        ClientLog.Info(
            "Main",
            "Facet \u9636\u6bb5 7 Binding \u7cfb\u7edf\u5df2\u63a5\u5165\u3002",
            new Dictionary<string, object?>
            {
                ["currentPageId"] = _uiManager?.CurrentPageId,
                ["hasBindings"] = _uiManager?.CurrentRuntime?.BindingScope != null,
                ["bindingCount"] = _uiManager?.CurrentRuntime?.BindingScope?.Count ?? 0,
            });

        ClientLog.Info(
            "Main",
            "Facet \u9636\u6bb5 8 Lua \u5bbf\u4e3b\u5df2\u63a5\u5165\u3002",
            new Dictionary<string, object?>
            {
                ["currentPageId"] = _uiManager?.CurrentPageId,
                ["hasLuaController"] = _uiManager?.CurrentRuntime?.HasLuaController,
                ["luaControllerScript"] = _uiManager?.CurrentRuntime?.LuaControllerScript,
                ["hasLuaBridge"] = _uiManager?.CurrentRuntime?.Context.Lua != null,
            });
    }

    /// <summary>
    /// 绑定 Facet 页面运行时。
    /// 如果宿主尚未初始化，则退回旧的可见性切换路径，保证主场景仍然可用。
    /// </summary>
    private void BindPageRuntime()
    {
        if (FacetHost.Instance?.IsInitialized != true)
        {
            ClientLog.Warning("Main", "FacetHost \u5c1a\u672a\u521d\u59cb\u5316\uff0cUIManager \u7ed1\u5b9a\u8df3\u8fc7\uff0c\u5c06\u4f7f\u7528\u53ef\u89c1\u6027\u515c\u5e95\u903b\u8f91\u3002", null);
            return;
        }

        _uiManager = FacetHost.Instance.GetRequired<UIManager>();
        _uiManager.AttachMountRoot(_pageMountRoot);
    }

    /// <summary>
    /// 在窗口模式切换后同步刷新当前页面，并重新发布页面状态 Projection。
    /// </summary>
    private void OnModeChanged(int mode)
    {
        WindowManager.GameMode gameMode = (WindowManager.GameMode)mode;
        if (!TryGoBackForMode(gameMode))
        {
            OpenPageForMode(gameMode, pushHistory: true);
        }

        PublishClientShellProjection(gameMode);
    }

    /// <summary>
    /// 当窗口模式切回挂机时，优先尝试走页面返回栈，而不是再次向栈中压入一条新记录。
    /// </summary>
    private bool TryGoBackForMode(WindowManager.GameMode mode)
    {
        if (_uiManager == null || mode != WindowManager.GameMode.Idle)
        {
            return false;
        }

        if (_uiManager.CurrentPageId != UIPageIds.Dungeon || !_uiManager.CanGoBack)
        {
            return false;
        }

        bool succeeded = _uiManager.GoBack();
        if (succeeded)
        {
            ClientLog.Info(
                "Main",
                "Main \u5df2\u901a\u8fc7\u8fd4\u56de\u6808\u6062\u590d\u9875\u9762\u3002",
                new Dictionary<string, object?>
                {
                    ["mode"] = mode.ToString(),
                    ["pageId"] = _uiManager.CurrentPageId,
                    ["backStackDepth"] = _uiManager.BackStackDepth,
                    ["currentPageState"] = _uiManager.CurrentRuntime?.State.ToString(),
                    ["hasLuaController"] = _uiManager.CurrentRuntime?.HasLuaController,
                });
        }

        return succeeded;
    }

    /// <summary>
    /// 根据当前模式打开对应页面。
    /// 阶段 5 开始，这一步会进入统一生命周期和返回栈管理。
    /// </summary>
    private void OpenPageForMode(WindowManager.GameMode mode, bool pushHistory)
    {
        string pageId = GetPageIdForMode(mode);

        if (_uiManager != null)
        {
            UIPageRuntime runtime = _uiManager.Open(pageId, arguments: null, pushHistory: pushHistory);
            ConnectPageSignals(runtime.PageRoot);

            ClientLog.Info(
                "Main",
                "Main \u5df2\u901a\u8fc7 UIManager \u6253\u5f00\u9875\u9762\u3002",
                new Dictionary<string, object?>
                {
                    ["mode"] = mode.ToString(),
                    ["pageId"] = pageId,
                    ["pushHistory"] = pushHistory,
                    ["backStackDepth"] = _uiManager.BackStackDepth,
                    ["currentPageState"] = runtime.State.ToString(),
                    ["bindingCount"] = runtime.BindingScope?.Count ?? 0,
                    ["hasBindings"] = runtime.BindingScope != null,
                    ["hasLuaController"] = runtime.HasLuaController,
                    ["luaControllerScript"] = runtime.LuaControllerScript,
                });
            return;
        }

        Control? idlePanel = _pageMountRoot.GetNodeOrNull<Control>("IdlePanel");
        Control? dungeonPanel = _pageMountRoot.GetNodeOrNull<Control>("DungeonPanel");
        if (idlePanel != null)
        {
            idlePanel.Visible = mode == WindowManager.GameMode.Idle;
        }

        if (dungeonPanel != null)
        {
            dungeonPanel.Visible = mode == WindowManager.GameMode.Dungeon;
        }
    }

    /// <summary>
    /// 把窗口模式映射为页面标识。
    /// </summary>
    private static string GetPageIdForMode(WindowManager.GameMode mode)
    {
        return mode == WindowManager.GameMode.Idle
            ? UIPageIds.Idle
            : UIPageIds.Dungeon;
    }

    /// <summary>
    /// 将页面信号绑定到主场景控制器。
    /// 页面运行时会缓存页面实例，因此这里需要避免重复绑定。
    /// </summary>
    private void ConnectPageSignals(Control pageRoot)
    {
        if (pageRoot is IdlePanel idlePanel)
        {
            Callable switchToDungeonCallable = Callable.From(OnSwitchToDungeon);
            if (!idlePanel.IsConnected("SwitchToDungeonRequested", switchToDungeonCallable))
            {
                idlePanel.Connect("SwitchToDungeonRequested", switchToDungeonCallable);
            }

            return;
        }

        if (pageRoot is DungeonPanel dungeonPanel)
        {
            Callable switchToIdleCallable = Callable.From(OnSwitchToIdle);
            if (!dungeonPanel.IsConnected("SwitchToIdleRequested", switchToIdleCallable))
            {
                dungeonPanel.Connect("SwitchToIdleRequested", switchToIdleCallable);
            }
        }
    }

    /// <summary>
    /// 响应挂机面板的“进入地下城”请求。
    /// 当前直接复用窗口模式切换作为最小行为演示。
    /// </summary>
    private void OnSwitchToDungeon()
    {
        _windowManager.ToggleMode();
    }

    /// <summary>
    /// 响应地下城面板的“返回挂机”请求。
    /// </summary>
    private void OnSwitchToIdle()
    {
        _windowManager.ToggleMode();
    }

    /// <summary>
    /// 生成并写入客户端壳层 Projection。
    /// 该 Projection 统一承载页面标题、状态文案、主按钮文案和区域显隐策略。
    /// </summary>
    private void PublishClientShellProjection(WindowManager.GameMode mode)
    {
        if (FacetHost.Instance?.IsInitialized != true)
        {
            return;
        }

        ClientShellProjection projection = mode == WindowManager.GameMode.Idle
            ? new ClientShellProjection(
                title: "Sideline / \u6302\u673a",
                status: "\u81ea\u52a8\u6536\u96c6\u8d44\u6e90 / Auto collecting",
                primaryActionLabel: "\u8fdb\u5165\u5730\u4e0b\u57ce / Dungeon",
                mode: "Idle",
                isPrimaryActionEnabled: true,
                showRuntimeSummary: true,
                showMetricsList: false)
            : new ClientShellProjection(
                title: "Sideline / \u5730\u4e0b\u57ce",
                status: "Projection \u9a71\u52a8\u6218\u6597\u7a97\u53e3 / Projection-driven battle panel",
                primaryActionLabel: "\u8fd4\u56de\u6302\u673a / Idle",
                mode: "Dungeon",
                isPrimaryActionEnabled: true,
                showRuntimeSummary: false,
                showMetricsList: true);

        ProjectionStore projectionStore = FacetHost.Instance.Context.ProjectionStore;
        projectionStore.Set(FacetProjectionKeys.ClientShell, projection, "Main.ModeChanged");

        ClientLog.Info(
            "Main",
            "ClientShellProjection \u5df2\u53d1\u5e03\u3002",
            new Dictionary<string, object?>
            {
                ["mode"] = projection.Mode,
                ["pageId"] = GetPageIdForMode(mode),
                ["primaryActionEnabled"] = projection.IsPrimaryActionEnabled,
                ["showRuntimeSummary"] = projection.ShowRuntimeSummary,
                ["showMetricsList"] = projection.ShowMetricsList,
            });
    }
}