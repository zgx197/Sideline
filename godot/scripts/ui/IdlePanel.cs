#nullable enable

using System;
using System.Collections.Generic;
using Godot;
using Sideline.Facet.Projection;
using Sideline.Facet.Projection.Client;
using Sideline.Facet.Projection.Diagnostics;
using Sideline.Facet.Runtime;
using Sideline.Facet.UI;

/// <summary>
/// 挂机模式面板。
/// 展示资源收集状态，并演示页面级 Binding 与组件级 Binding 的组合使用方式。
/// </summary>
public partial class IdlePanel : PanelContainer, IUIPageLifecycle
{
    private PanelContainer _facetProjectionPanel = null!;
    private Label _titleLabel = null!;
    private Label _statusLabel = null!;
    private Label _resourceLabel = null!;
    private Label _facetProjectionLabel = null!;
    private Button _switchButton = null!;
    private Button _closeButton = null!;

    private IDisposable? _runtimeProbeSubscription;
    private IDisposable? _clientShellSubscription;
    private ClientShellProjection? _currentShellProjection;
    private string _runtimeProbeSummaryText = "Facet Runtime / 等待数据";
    private UIContext? _pageContext;
    private IUIComponentBindingScope? _runtimeSummaryBindings;
    private bool _isPageShown;
    private bool _nodesBound;
    private bool _bindingsRegistered;
    private int _gold;
    private double _timer;

    [Signal]
    public delegate void SwitchToDungeonRequestedEventHandler();

    public override void _Ready()
    {
        SetProcess(false);
    }

    public override void _ExitTree()
    {
        ReleaseProjectionSubscriptions();
    }

    public override void _Process(double delta)
    {
        if (!_isPageShown)
        {
            return;
        }

        _timer += delta;
        if (_timer >= 1.0)
        {
            _timer -= 1.0;
            _gold++;
            RefreshView("idle.gold_tick");
        }
    }

    /// <summary>
    /// 绑定 Facet Projection。
    /// 这里同时监听 RuntimeProbe 与 ClientShell 两类 Projection。
    /// </summary>
    public void BindFacetProjection()
    {
        ReleaseProjectionSubscriptions();

        if (FacetHost.Instance?.IsInitialized != true)
        {
            _runtimeProbeSummaryText = "Facet Projection 未就绪 / Projection pending";
            RefreshView("projection.pending");
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
            _runtimeProbeSummaryText = "Facet Projection 已绑定 / Waiting for data";
            RefreshView("projection.initial.runtime_missing");
        }

        bool initialClientShellApplied = false;
        if (projectionStore.TryGet(FacetProjectionKeys.ClientShell, out ClientShellProjection? shellProjection) && shellProjection != null)
        {
            if (ShouldApplyInitialClientShellProjection(shellProjection))
            {
                ApplyClientShellProjection(shellProjection);
                LogInitialClientShellProjection(shellProjection);
                initialClientShellApplied = true;
            }
            else
            {
                ClientLog.Info(
                    "IdlePanel",
                    "IdlePanel 跳过了不匹配当前页面模式的初始 ClientShellProjection。",
                    new Dictionary<string, object?>
                    {
                        ["expectedMode"] = GetExpectedShellMode(),
                        ["actualMode"] = shellProjection.Mode,
                    });
                RefreshView("projection.initial.shell_mismatch");
            }
        }
        else
        {
            RefreshView("projection.initial.shell_missing");
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

    public void OnPageInitialize(UIContext context)
    {
        _pageContext = context;
        EnsureNodesResolved(context);
        EnsureBindingsRegistered(context);
        BindFacetProjection();
        ClientLog.Info("IdlePanel", "IdlePanel 生命周期 Initialize。", CreateLifecyclePayload(context, _runtimeSummaryBindings));
    }

    public void OnPageShow(UIContext context)
    {
        _pageContext = context;
        EnsureNodesResolved(context);
        EnsureBindingsRegistered(context);
        _isPageShown = true;
        SetProcess(true);

        if (_runtimeProbeSubscription == null || _clientShellSubscription == null)
        {
            BindFacetProjection();
        }

        RefreshView("page.show");
        ClientLog.Info("IdlePanel", "IdlePanel 生命周期 Show。", CreateLifecyclePayload(context, _runtimeSummaryBindings));
    }

    public void OnPageRefresh(UIContext context)
    {
        _pageContext = context;
        EnsureNodesResolved(context);
        EnsureBindingsRegistered(context);
        if (_runtimeProbeSubscription == null || _clientShellSubscription == null)
        {
            BindFacetProjection();
        }

        RefreshView("page.refresh");
        ClientLog.Info("IdlePanel", "IdlePanel 生命周期 Refresh。", CreateLifecyclePayload(context, _runtimeSummaryBindings));
    }

    public void OnPageHide(UIContext context)
    {
        _pageContext = context;
        _isPageShown = false;
        SetProcess(false);
        ReleaseProjectionSubscriptions();
        ClientLog.Info("IdlePanel", "IdlePanel 生命周期 Hide。", CreateLifecyclePayload(context, _runtimeSummaryBindings));
    }

    public void OnPageDispose(UIContext context)
    {
        _pageContext = context;
        _isPageShown = false;
        SetProcess(false);
        ReleaseProjectionSubscriptions();
        _runtimeSummaryBindings = null;
        ClientLog.Info("IdlePanel", "IdlePanel 生命周期 Dispose。", CreateLifecyclePayload(context, null));
    }

    private void EnsureNodesResolved(UIContext? context)
    {
        if (_nodesBound)
        {
            return;
        }

        UINodeResolver? resolver = context?.Resolver as UINodeResolver;
        _facetProjectionPanel = ResolveRequiredNode<PanelContainer>(resolver, "FacetProjectionPanel", "%FacetProjectionPanel");
        _titleLabel = ResolveRequiredNode<Label>(resolver, "TitleLabel", "%TitleLabel");
        _statusLabel = ResolveRequiredNode<Label>(resolver, "StatusLabel", "%StatusLabel");
        _resourceLabel = ResolveRequiredNode<Label>(resolver, "ResourceLabel", "%ResourceLabel");
        _facetProjectionLabel = ResolveRequiredNode<Label>(resolver, "FacetProjectionLabel", "%FacetProjectionLabel");
        _switchButton = ResolveRequiredNode<Button>(resolver, "SwitchButton", "%SwitchButton");
        _closeButton = ResolveRequiredNode<Button>(resolver, "CloseButton", "%CloseButton");

        _statusLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _facetProjectionLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _nodesBound = true;
        ApplyProjectionPlaceholder();
        RefreshView("nodes.resolved");
    }

    private void EnsureBindingsRegistered(UIContext? context)
    {
        if (_bindingsRegistered)
        {
            return;
        }

        IUIBindingScope? bindings = context?.Bindings;
        if (bindings == null)
        {
            ConnectButtonPressed(_switchButton, OnSwitchPressed);
            ConnectButtonPressed(_closeButton, OnClosePressed);
            _bindingsRegistered = true;
            return;
        }

        bindings.BindText("TitleLabel", GetTitleText);
        bindings.BindText("StatusLabel", GetStatusText);
        bindings.BindText("SwitchButton", GetPrimaryActionText);
        bindings.BindInteractable("SwitchButton", IsPrimaryActionEnabled);
        bindings.BindText("ResourceLabel", GetResourceText);
        bindings.BindCommand("SwitchButton", OnSwitchPressed);
        bindings.BindCommand("CloseButton", OnClosePressed);

        if (context?.Resolver is UINodeResolver resolver)
        {
            UINodeResolver runtimeSummaryResolver = resolver.CreateSubtreeResolver("FacetProjectionPanel");
            _runtimeSummaryBindings = bindings.CreateComponentScope("runtime-summary", runtimeSummaryResolver);
            _runtimeSummaryBindings.BindVisibility("FacetProjectionPanel", ShouldShowRuntimeSummary);
            _runtimeSummaryBindings.BindText("FacetProjectionLabel", GetRuntimeProbeSummaryText);
        }

        _bindingsRegistered = true;
        bindings.RefreshAll("binding.registered");

        ClientLog.Info(
            "IdlePanel",
            "IdlePanel Binding 已注册。",
            new Dictionary<string, object?>
            {
                ["pageId"] = context?.PageId,
                ["bindingCount"] = bindings.Count,
                ["bindingRefreshCount"] = bindings.RefreshCount,
                ["bindingScopeId"] = bindings.ScopeId,
                ["runtimeSummaryScopeId"] = _runtimeSummaryBindings?.ScopeId,
                ["hasResolver"] = context?.Resolver != null,
            });
    }

    private TNode ResolveRequiredNode<TNode>(UINodeResolver? resolver, string key, string fallbackPath) where TNode : Node
    {
        return resolver?.GetRequired<TNode>(key) ?? GetNode<TNode>(fallbackPath);
    }

    private static void ConnectButtonPressed(Button button, Action handler)
    {
        Callable callable = Callable.From(handler);
        if (!button.IsConnected(Button.SignalName.Pressed, callable))
        {
            button.Connect(Button.SignalName.Pressed, callable);
        }
    }

    private void RefreshView(string? reason = null)
    {
        if (_pageContext?.Bindings != null)
        {
            _pageContext.Bindings.RefreshAll(reason);
            return;
        }

        ApplyLegacyView();
    }

    private void ApplyLegacyView()
    {
        _titleLabel.Text = GetTitleText();
        _statusLabel.Text = GetStatusText();
        _switchButton.Text = GetPrimaryActionText();
        _switchButton.Disabled = !IsPrimaryActionEnabled();
        _resourceLabel.Text = GetResourceText();
        _facetProjectionPanel.Visible = ShouldShowRuntimeSummary();
        _facetProjectionLabel.Text = GetRuntimeProbeSummaryText();
    }

    private string GetTitleText()
    {
        return _currentShellProjection?.Title ?? "Sideline / 挂机";
    }

    private string GetStatusText()
    {
        return _currentShellProjection?.Status ?? "自动收集资源 / Auto collecting";
    }

    private string GetPrimaryActionText()
    {
        return _currentShellProjection?.PrimaryActionLabel ?? "进入地下城 / Dungeon";
    }

    private bool IsPrimaryActionEnabled()
    {
        return _currentShellProjection?.IsPrimaryActionEnabled != false;
    }

    private string GetResourceText()
    {
        return $"金币 / Gold: {_gold}";
    }

    private bool ShouldShowRuntimeSummary()
    {
        return _currentShellProjection?.ShowRuntimeSummary ?? true;
    }

    private string GetRuntimeProbeSummaryText()
    {
        return _runtimeProbeSummaryText;
    }

    private void ApplyProjectionPlaceholder()
    {
        _runtimeProbeSummaryText = "Facet Runtime / 等待数据";
    }

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

    private void ApplyClientShellProjection(ClientShellProjection projection)
    {
        _currentShellProjection = projection;
        RefreshView("projection.client_shell");
    }

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

    private void OnRuntimeProbeChanged(ProjectionChange change)
    {
        if (change.Kind == ProjectionChangeKind.Removed)
        {
            _runtimeProbeSummaryText = "Facet Runtime / Projection removed";
            RefreshView("projection.runtime_probe.removed");
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

    private void ApplyRuntimeProbeProjection(FacetRuntimeProbeProjection projection)
    {
        _runtimeProbeSummaryText =
            $"Facet Runtime / 运行时\n" +
            $"Session: {ShortenSessionId(projection.SessionId)}\n" +
            $"Records: {projection.RecordedCount}  Reload: {(projection.HotReloadEnabled ? "On" : "Off")}\n" +
            $"Cache: {(projection.PageCacheEnabled ? "On" : "Off")} ({projection.PageCacheCapacity})";
        RefreshView("projection.runtime_probe");
    }

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

    private void ReleaseProjectionSubscriptions()
    {
        _runtimeProbeSubscription?.Dispose();
        _runtimeProbeSubscription = null;
        _clientShellSubscription?.Dispose();
        _clientShellSubscription = null;
    }

    private static Dictionary<string, object?> CreateLifecyclePayload(UIContext context, IUIComponentBindingScope? runtimeSummaryBindings)
    {
        UIBindingDiagnosticsSnapshot? bindingDiagnostics = context.Bindings?.GetDiagnosticsSnapshot();
        UIBindingDiagnosticsSnapshot? runtimeSummaryDiagnostics = runtimeSummaryBindings?.GetDiagnosticsSnapshot();
        return new Dictionary<string, object?>
        {
            ["pageId"] = context.PageId,
            ["layer"] = context.Layer,
            ["argumentCount"] = context.Arguments.Count,
            ["bindingCount"] = bindingDiagnostics?.BindingCount ?? 0,
            ["bindingRefreshCount"] = bindingDiagnostics?.RefreshCount ?? 0,
            ["bindingLastReason"] = bindingDiagnostics?.LastRefreshReason,
            ["runtimeSummaryScopeId"] = runtimeSummaryDiagnostics?.ScopeId,
            ["runtimeSummaryBindingCount"] = runtimeSummaryDiagnostics?.BindingCount ?? 0,
        };
    }

    private bool ShouldApplyInitialClientShellProjection(ClientShellProjection projection)
    {
        return string.Equals(projection.Mode, GetExpectedShellMode(), StringComparison.OrdinalIgnoreCase);
    }

    private static string GetExpectedShellMode()
    {
        return "Idle";
    }

    private static string ShortenSessionId(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || sessionId.Length <= 8)
        {
            return sessionId;
        }

        return sessionId[..8];
    }

    private void OnSwitchPressed()
    {
        EmitSignal("SwitchToDungeonRequested");
    }

    private void OnClosePressed()
    {
        GetTree().Quit();
    }
}