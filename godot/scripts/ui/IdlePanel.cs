#nullable enable

using System;
using System.Collections.Generic;
using Godot;
using Sideline.Facet.Projection;
using Sideline.Facet.Projection.Client;
using Sideline.Facet.Projection.Diagnostics;
using Sideline.Facet.Runtime;

/// <summary>
/// 挂机模式面板：显示资源收集状态，并演示由 Projection 驱动的页面刷新。
/// </summary>
public partial class IdlePanel : PanelContainer
{
    /// <summary>
    /// Projection 展示区域及其关键控件。
    /// </summary>
    private PanelContainer _facetProjectionPanel = null!;
    private Label _titleLabel = null!;
    private Label _statusLabel = null!;
    private Label _resourceLabel = null!;
    private Label _facetProjectionLabel = null!;
    private Button _switchButton = null!;
    private Button _closeButton = null!;

    /// <summary>
    /// Projection 订阅句柄，面板退出时需要释放。
    /// </summary>
    private IDisposable? _runtimeProbeSubscription;
    private IDisposable? _clientShellSubscription;

    /// <summary>
    /// 当前生效的客户端壳层 Projection。
    /// </summary>
    private ClientShellProjection? _currentShellProjection;

    /// <summary>
    /// 当前挂机金币数和本地计时器。
    /// </summary>
    private int _gold;
    private double _timer;

    [Signal]
    public delegate void SwitchToDungeonRequestedEventHandler();

    /// <summary>
    /// 初始化挂机面板，并建立节点引用、按钮事件和占位文案。
    /// </summary>
    public override void _Ready()
    {
        _facetProjectionPanel = GetNode<PanelContainer>("%FacetProjectionPanel");
        _titleLabel = GetNode<Label>("%TitleLabel");
        _statusLabel = GetNode<Label>("%StatusLabel");
        _resourceLabel = GetNode<Label>("%ResourceLabel");
        _facetProjectionLabel = GetNode<Label>("%FacetProjectionLabel");
        _switchButton = GetNode<Button>("%SwitchButton");
        _closeButton = GetNode<Button>("%CloseButton");

        _switchButton.Pressed += OnSwitchPressed;
        _closeButton.Pressed += OnClosePressed;

        _statusLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _facetProjectionLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        ApplyProjectionPlaceholder();
        UpdateDisplay();
    }

    /// <summary>
    /// 面板退出时释放 Projection 订阅，避免重复进入场景后出现多次回调。
    /// </summary>
    public override void _ExitTree()
    {
        _runtimeProbeSubscription?.Dispose();
        _runtimeProbeSubscription = null;
        _clientShellSubscription?.Dispose();
        _clientShellSubscription = null;
    }

    /// <summary>
    /// 每秒更新一次本地金币计数，用于演示 Projection 与页面本地状态可以共存。
    /// </summary>
    public override void _Process(double delta)
    {
        _timer += delta;
        if (_timer >= 1.0)
        {
            _timer -= 1.0;
            _gold++;
            UpdateDisplay();
        }
    }

    /// <summary>
    /// 绑定 Facet Projection。
    /// 这里同时监听 RuntimeProbe 和 ClientShell 两类投影。
    /// </summary>
    public void BindFacetProjection()
    {
        _runtimeProbeSubscription?.Dispose();
        _runtimeProbeSubscription = null;
        _clientShellSubscription?.Dispose();
        _clientShellSubscription = null;

        if (FacetHost.Instance?.IsInitialized != true)
        {
            _facetProjectionLabel.Text = "Facet Projection 未就绪 / Projection pending";
            ClientLog.Warning("IdlePanel", "FacetHost 尚未初始化，Projection 绑定延后。", null);
            return;
        }

        ProjectionStore projectionStore = FacetHost.Instance.Context.ProjectionStore;
        _runtimeProbeSubscription = projectionStore.Subscribe(FacetProjectionKeys.RuntimeProbe, OnRuntimeProbeChanged);
        _clientShellSubscription = projectionStore.Subscribe(FacetProjectionKeys.ClientShell, OnClientShellChanged);

        bool initialRuntimeProbeApplied = false;
        if (projectionStore.TryGet(FacetProjectionKeys.RuntimeProbe, out FacetRuntimeProbeProjection? runtimeProjection) && runtimeProjection != null)
        {
            ApplyRuntimeProbeProjection(runtimeProjection);
            LogInitialRuntimeProbeProjection(runtimeProjection);
            initialRuntimeProbeApplied = true;
        }
        else
        {
            _facetProjectionLabel.Text = "Facet Projection 已绑定 / Waiting for data";
        }

        bool initialClientShellApplied = false;
        if (projectionStore.TryGet(FacetProjectionKeys.ClientShell, out ClientShellProjection? shellProjection) && shellProjection != null)
        {
            ApplyClientShellProjection(shellProjection);
            LogInitialClientShellProjection(shellProjection);
            initialClientShellApplied = true;
        }
        else
        {
            UpdateDisplay();
        }

        ClientLog.Info(
            "IdlePanel",
            "IdlePanel Projection 绑定完成。",
            new Dictionary<string, object?>
            {
                ["runtimeProbeKey"] = FacetProjectionKeys.RuntimeProbe.ToString(),
                ["clientShellKey"] = FacetProjectionKeys.ClientShell.ToString(),
                ["initialRuntimeProbeApplied"] = initialRuntimeProbeApplied,
                ["initialClientShellApplied"] = initialClientShellApplied,
            });
    }

    /// <summary>
    /// 根据当前页面本地状态和壳层 Projection 更新顶部展示文案。
    /// </summary>
    private void UpdateDisplay()
    {
        _titleLabel.Text = _currentShellProjection?.Title ?? "Sideline / 挂机";
        _statusLabel.Text = _currentShellProjection?.Status ?? "自动收集资源 / Auto collecting";
        _switchButton.Text = _currentShellProjection?.PrimaryActionLabel ?? "进入地下城 / Dungeon";
        _switchButton.Disabled = _currentShellProjection?.IsPrimaryActionEnabled == false;
        _resourceLabel.Text = $"金币 / Gold: {_gold}";
    }

    /// <summary>
    /// 在 Projection 尚未到达前先展示明确占位，便于验证绑定链路是否生效。
    /// </summary>
    private void ApplyProjectionPlaceholder()
    {
        _facetProjectionLabel.Text = "Facet Runtime / 等待数据";
    }

    /// <summary>
    /// 响应 ClientShellProjection 变化。
    /// </summary>
    private void OnClientShellChanged(ProjectionChange change)
    {
        if (change.CurrentValue is ClientShellProjection projection)
        {
            ApplyClientShellProjection(projection);
            ClientLog.Info(
                "IdlePanel",
                "IdlePanel 收到 ClientShellProjection 变更。",
                new Dictionary<string, object?>
                {
                    ["mode"] = projection.Mode,
                    ["showRuntimeSummary"] = projection.ShowRuntimeSummary,
                    ["primaryActionEnabled"] = projection.IsPrimaryActionEnabled,
                });
        }
    }

    /// <summary>
    /// 应用客户端壳层 Projection 到挂机界面。
    /// </summary>
    private void ApplyClientShellProjection(ClientShellProjection projection)
    {
        _currentShellProjection = projection;
        _facetProjectionPanel.Visible = projection.ShowRuntimeSummary;
        UpdateDisplay();
    }

    /// <summary>
    /// 记录初次应用 ClientShellProjection 的日志，便于验证初始化同步路径。
    /// </summary>
    private void LogInitialClientShellProjection(ClientShellProjection projection)
    {
        ClientLog.Info(
            "IdlePanel",
            "IdlePanel 已应用初始 ClientShellProjection。",
            new Dictionary<string, object?>
            {
                ["mode"] = projection.Mode,
                ["showRuntimeSummary"] = projection.ShowRuntimeSummary,
                ["primaryActionEnabled"] = projection.IsPrimaryActionEnabled,
            });
    }

    /// <summary>
    /// 响应 RuntimeProbeProjection 变化。
    /// </summary>
    private void OnRuntimeProbeChanged(ProjectionChange change)
    {
        if (change.Kind == ProjectionChangeKind.Removed)
        {
            _facetProjectionLabel.Text = "Facet Runtime / Projection removed";
            return;
        }

        if (change.CurrentValue is FacetRuntimeProbeProjection projection)
        {
            ApplyRuntimeProbeProjection(projection);
            ClientLog.Info(
                "IdlePanel",
                "IdlePanel 收到 RuntimeProbeProjection 变更。",
                new Dictionary<string, object?>
                {
                    ["sessionId"] = ShortenSessionId(projection.SessionId),
                    ["recordedCount"] = projection.RecordedCount,
                });
        }
    }

    /// <summary>
    /// 将 RuntimeProbeProjection 转换成挂机面板中的诊断摘要文本。
    /// </summary>
    private void ApplyRuntimeProbeProjection(FacetRuntimeProbeProjection projection)
    {
        _facetProjectionLabel.Text =
            $"Facet Runtime / 运行时\n" +
            $"Session: {ShortenSessionId(projection.SessionId)}\n" +
            $"Records: {projection.RecordedCount}  Reload: {(projection.HotReloadEnabled ? "On" : "Off")}\n" +
            $"Cache: {(projection.PageCacheEnabled ? "On" : "Off")} ({projection.PageCacheCapacity})";
    }

    /// <summary>
    /// 记录初次应用 RuntimeProbeProjection 的日志，便于区分初始化拉取和后续订阅变更。
    /// </summary>
    private void LogInitialRuntimeProbeProjection(FacetRuntimeProbeProjection projection)
    {
        ClientLog.Info(
            "IdlePanel",
            "IdlePanel 已应用初始 RuntimeProbeProjection。",
            new Dictionary<string, object?>
            {
                ["sessionId"] = ShortenSessionId(projection.SessionId),
                ["recordedCount"] = projection.RecordedCount,
                ["hasSnapshot"] = projection.HasSnapshot,
            });
    }

    /// <summary>
    /// 缩短会话标识，避免摘要面板占据过多横向空间。
    /// </summary>
    private static string ShortenSessionId(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || sessionId.Length <= 8)
        {
            return sessionId;
        }

        return sessionId[..8];
    }

    /// <summary>
    /// 触发切换到地下城模式的信号。
    /// </summary>
    private void OnSwitchPressed()
    {
        EmitSignal(SignalName.SwitchToDungeonRequested);
    }

    /// <summary>
    /// 关闭当前客户端进程。
    /// </summary>
    private void OnClosePressed()
    {
        GetTree().Quit();
    }
}
