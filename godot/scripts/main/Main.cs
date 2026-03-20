#nullable enable

using System.Collections.Generic;
using Godot;
using Sideline.Facet.Projection;
using Sideline.Facet.Projection.Client;
using Sideline.Facet.Projection.Diagnostics;
using Sideline.Facet.Runtime;

/// <summary>
/// 主场景控制器。
/// 负责连接窗口模式、页面显隐和 Facet Projection 的最小演示链路。
/// </summary>
public partial class Main : Node
{
    /// <summary>
    /// 窗口管理器，负责挂机/地下城模式下的窗口尺寸与样式切换。
    /// </summary>
    private WindowManager _windowManager = null!;

    /// <summary>
    /// 挂机模式面板实例。
    /// </summary>
    private IdlePanel _idlePanel = null!;

    /// <summary>
    /// 地下城模式面板实例。
    /// </summary>
    private DungeonPanel _dungeonPanel = null!;

    /// <summary>
    /// 主场景准备完成后绑定窗口事件、页面信号，并发布首帧 Projection。
    /// </summary>
    public override void _Ready()
    {
        _windowManager = GetNode<WindowManager>("WindowManager");
        _idlePanel = GetNode<IdlePanel>("CanvasLayer/IdlePanel");
        _dungeonPanel = GetNode<DungeonPanel>("CanvasLayer/DungeonPanel");

        _windowManager.ModeChanged += OnModeChanged;
        _idlePanel.Connect(IdlePanel.SignalName.SwitchToDungeonRequested, Callable.From(OnSwitchToDungeon));
        _dungeonPanel.Connect(DungeonPanel.SignalName.SwitchToIdleRequested, Callable.From(OnSwitchToIdle));

        ShowPanel(WindowManager.GameMode.Idle);
        PublishClientShellProjection(WindowManager.GameMode.Idle);
        _idlePanel.BindFacetProjection();
        _dungeonPanel.BindFacetProjection();

        ClientLog.Info(
            "Main",
            "Sideline Phase 0 启动完成",
            new Dictionary<string, object?>
            {
                ["scenePath"] = GetPath().ToString(),
                ["currentMode"] = WindowManager.GameMode.Idle.ToString(),
                ["idlePanelVisible"] = _idlePanel.Visible,
                ["dungeonPanelVisible"] = _dungeonPanel.Visible,
                ["projectionBound"] = true,
            });
    }

    /// <summary>
    /// 在窗口模式切换后同步刷新页面可见性，并重新发布页面状态 Projection。
    /// </summary>
    private void OnModeChanged(int mode)
    {
        WindowManager.GameMode gameMode = (WindowManager.GameMode)mode;
        ShowPanel(gameMode);
        PublishClientShellProjection(gameMode);
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
    /// 根据当前模式切换两个页面的可见性。
    /// </summary>
    private void ShowPanel(WindowManager.GameMode mode)
    {
        _idlePanel.Visible = mode == WindowManager.GameMode.Idle;
        _dungeonPanel.Visible = mode == WindowManager.GameMode.Dungeon;
    }

    /// <summary>
    /// 生成并写入客户端壳层 Projection。
    /// 该 Projection 统一承载页面标题、状态文案、主按钮文字和区块显隐策略。
    /// </summary>
    private void PublishClientShellProjection(WindowManager.GameMode mode)
    {
        if (FacetHost.Instance?.IsInitialized != true)
        {
            return;
        }

        ClientShellProjection projection = mode == WindowManager.GameMode.Idle
            ? new ClientShellProjection(
                title: "Sideline / 挂机",
                status: "自动收集资源 / Auto collecting",
                primaryActionLabel: "进入地下城 / Dungeon",
                mode: "Idle",
                isPrimaryActionEnabled: true,
                showRuntimeSummary: true,
                showMetricsList: false)
            : new ClientShellProjection(
                title: "Sideline / 地下城",
                status: "Projection 驱动战斗窗口 / Projection-driven battle panel",
                primaryActionLabel: "返回挂机 / Idle",
                mode: "Dungeon",
                isPrimaryActionEnabled: true,
                showRuntimeSummary: false,
                showMetricsList: true);

        ProjectionStore projectionStore = FacetHost.Instance.Context.ProjectionStore;
        projectionStore.Set(FacetProjectionKeys.ClientShell, projection, "Main.ModeChanged");

        ClientLog.Info(
            "Main",
            "ClientShellProjection 已发布",
            new Dictionary<string, object?>
            {
                ["mode"] = projection.Mode,
                ["primaryActionEnabled"] = projection.IsPrimaryActionEnabled,
                ["showRuntimeSummary"] = projection.ShowRuntimeSummary,
                ["showMetricsList"] = projection.ShowMetricsList,
            });
    }
}
