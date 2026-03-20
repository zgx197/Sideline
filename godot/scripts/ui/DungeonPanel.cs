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
/// 地下城模式面板。
/// 通过 Projection 驱动页面状态，并使用模板化复杂列表展示运行时指标。
/// </summary>
public partial class DungeonPanel : PanelContainer, IUIPageLifecycle
{
    private PanelContainer _metricsPanel = null!;
    private Label _titleLabel = null!;
    private Label _statusLabel = null!;
    private Label _metricsTitleLabel = null!;
    private Label _metricsEmptyLabel = null!;
    private VBoxContainer _metricsListContainer = null!;
    private Control _metricsItemTemplate = null!;
    private Button _switchButton = null!;

    private IDisposable? _clientShellSubscription;
    private IDisposable? _metricsSubscription;
    private UIContext? _pageContext;
    private ClientShellProjection? _currentShellProjection;
    private FacetRuntimeMetricListProjection? _currentMetricsProjection;
    private IUIComponentBindingScope? _metricsPanelBindings;
    private IUIComplexListBinding<FacetRuntimeMetricItem>? _metricsListBinding;
    private bool _nodesBound;
    private bool _bindingsRegistered;

    [Signal]
    public delegate void SwitchToIdleRequestedEventHandler();

    public override void _ExitTree()
    {
        ReleaseProjectionSubscriptions();
    }

    /// <summary>
    /// 绑定 Facet Projection。
    /// 这里同时监听 ClientShell 与 RuntimeMetrics 两类 Projection。
    /// </summary>
    public void BindFacetProjection()
    {
        ReleaseProjectionSubscriptions();

        if (FacetHost.Instance?.IsInitialized != true)
        {
            _currentShellProjection = null;
            _currentMetricsProjection = null;
            RefreshView("projection.pending");
            ClientLog.Warning("DungeonPanel", "FacetHost 尚未初始化，Projection 绑定延后。", null);
            return;
        }

        ProjectionStore projectionStore = FacetHost.Instance.Context.ProjectionStore;
        _clientShellSubscription = projectionStore.Subscribe(FacetProjectionKeys.ClientShell, OnClientShellChanged);
        _metricsSubscription = projectionStore.Subscribe(FacetProjectionKeys.RuntimeMetrics, OnRuntimeMetricsChanged);

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
                    "DungeonPanel",
                    "DungeonPanel 跳过了不匹配当前页面模式的初始 ClientShellProjection。",
                    new Dictionary<string, object?>
                    {
                        ["expectedMode"] = GetExpectedShellMode(),
                        ["actualMode"] = shellProjection.Mode,
                    });
                ApplyShellFallback("projection.initial.shell_mismatch");
            }
        }
        else
        {
            ApplyShellFallback("projection.initial.shell_missing");
        }

        bool initialMetricsApplied = false;
        if (projectionStore.TryGet(FacetProjectionKeys.RuntimeMetrics, out FacetRuntimeMetricListProjection? metricsProjection) && metricsProjection != null)
        {
            ApplyRuntimeMetricsProjection(metricsProjection);
            LogInitialRuntimeMetricsProjection(metricsProjection);
            initialMetricsApplied = true;
        }
        else
        {
            ApplyMetricsPlaceholder("projection.initial.metrics_missing");
        }

        ClientLog.Info(
            "DungeonPanel",
            "DungeonPanel Projection 绑定完成。",
            new Dictionary<string, object?>
            {
                ["clientShellKey"] = FacetProjectionKeys.ClientShell.ToString(),
                ["runtimeMetricsKey"] = FacetProjectionKeys.RuntimeMetrics.ToString(),
                ["initialClientShellApplied"] = initialClientShellApplied,
                ["initialMetricsApplied"] = initialMetricsApplied,
            });
    }

    public void OnPageInitialize(UIContext context)
    {
        _pageContext = context;
        EnsureNodesResolved(context);
        EnsureBindingsRegistered(context);
        BindFacetProjection();
        ClientLog.Info("DungeonPanel", "DungeonPanel 生命周期 Initialize。", CreateLifecyclePayload(context, _metricsPanelBindings, _metricsListBinding));
    }

    public void OnPageShow(UIContext context)
    {
        _pageContext = context;
        EnsureNodesResolved(context);
        EnsureBindingsRegistered(context);
        if (_clientShellSubscription == null || _metricsSubscription == null)
        {
            BindFacetProjection();
        }

        RefreshView("page.show");
        ClientLog.Info("DungeonPanel", "DungeonPanel 生命周期 Show。", CreateLifecyclePayload(context, _metricsPanelBindings, _metricsListBinding));
    }

    public void OnPageRefresh(UIContext context)
    {
        _pageContext = context;
        EnsureNodesResolved(context);
        EnsureBindingsRegistered(context);
        if (_clientShellSubscription == null || _metricsSubscription == null)
        {
            BindFacetProjection();
        }

        RefreshView("page.refresh");
        ClientLog.Info("DungeonPanel", "DungeonPanel 生命周期 Refresh。", CreateLifecyclePayload(context, _metricsPanelBindings, _metricsListBinding));
    }

    public void OnPageHide(UIContext context)
    {
        _pageContext = context;
        ReleaseProjectionSubscriptions();
        ClientLog.Info("DungeonPanel", "DungeonPanel 生命周期 Hide。", CreateLifecyclePayload(context, _metricsPanelBindings, _metricsListBinding));
    }

    public void OnPageDispose(UIContext context)
    {
        _pageContext = context;
        ReleaseProjectionSubscriptions();
        _metricsPanelBindings = null;
        _metricsListBinding = null;
        ClientLog.Info("DungeonPanel", "DungeonPanel 生命周期 Dispose。", CreateLifecyclePayload(context, null, null));
    }

    private void EnsureNodesResolved(UIContext? context)
    {
        if (_nodesBound)
        {
            return;
        }

        UINodeResolver? resolver = context?.Resolver as UINodeResolver;
        _metricsPanel = ResolveRequiredNode<PanelContainer>(resolver, "MetricsPanel", "%MetricsPanel");
        _titleLabel = ResolveRequiredNode<Label>(resolver, "TitleLabel", "%TitleLabel");
        _statusLabel = ResolveRequiredNode<Label>(resolver, "StatusLabel", "%StatusLabel");
        _metricsTitleLabel = ResolveRequiredNode<Label>(resolver, "MetricsTitleLabel", "%MetricsTitleLabel");
        _metricsEmptyLabel = ResolveRequiredNode<Label>(resolver, "MetricsEmptyLabel", "%MetricsEmptyLabel");
        _metricsListContainer = ResolveRequiredNode<VBoxContainer>(resolver, "MetricsListContainer", "%MetricsListContainer");
        _metricsItemTemplate = ResolveRequiredNode<Control>(resolver, "MetricsItemTemplate", "%MetricsItemTemplate");
        _switchButton = ResolveRequiredNode<Button>(resolver, "SwitchButton", "%SwitchButton");

        _statusLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _metricsTitleLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _metricsEmptyLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _metricsItemTemplate.Visible = false;

        _nodesBound = true;
        ApplyShellFallback("nodes.resolved.shell_fallback");
        ApplyMetricsPlaceholder("nodes.resolved.metrics_placeholder");
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
            _bindingsRegistered = true;
            return;
        }

        bindings.BindText("TitleLabel", GetTitleText);
        bindings.BindText("StatusLabel", GetStatusText);
        bindings.BindText("SwitchButton", GetPrimaryActionText);
        bindings.BindInteractable("SwitchButton", IsPrimaryActionEnabled);
        bindings.BindCommand("SwitchButton", OnSwitchPressed);

        if (context?.Resolver is UINodeResolver resolver)
        {
            UINodeResolver metricsResolver = resolver.CreateSubtreeResolver("MetricsPanel");
            _metricsPanelBindings = bindings.CreateComponentScope("metrics-panel", metricsResolver);
            _metricsPanelBindings.BindVisibility("MetricsPanel", ShouldShowMetricsPanel);
            _metricsPanelBindings.BindText("MetricsTitleLabel", GetMetricsTitle);
            _metricsListBinding = _metricsPanelBindings.BindComplexList(
                "MetricsListContainer",
                "MetricsItemTemplate",
                GetMetricItems,
                new MetricsItemBindingAdapter(),
                "MetricsEmptyLabel");
        }

        _bindingsRegistered = true;
        bindings.RefreshAll("binding.registered");

        ClientLog.Info(
            "DungeonPanel",
            "DungeonPanel Binding 已注册。",
            new Dictionary<string, object?>
            {
                ["pageId"] = context?.PageId,
                ["bindingCount"] = bindings.Count,
                ["bindingRefreshCount"] = bindings.RefreshCount,
                ["bindingScopeId"] = bindings.ScopeId,
                ["metricsPanelScopeId"] = _metricsPanelBindings?.ScopeId,
                ["metricsListItemCount"] = _metricsListBinding?.ItemCount ?? 0,
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
        _metricsPanel.Visible = ShouldShowMetricsPanel();
        _metricsTitleLabel.Text = GetMetricsTitle();
        RenderLegacyMetricItems();
    }

    private string GetTitleText()
    {
        return _currentShellProjection?.Title ?? "Sideline / 地下城";
    }

    private string GetStatusText()
    {
        return _currentShellProjection?.Status ?? "Projection 驱动战斗窗口 / Projection-driven battle panel";
    }

    private string GetPrimaryActionText()
    {
        return _currentShellProjection?.PrimaryActionLabel ?? "返回挂机 / Idle";
    }

    private bool IsPrimaryActionEnabled()
    {
        return _currentShellProjection?.IsPrimaryActionEnabled != false;
    }

    private bool ShouldShowMetricsPanel()
    {
        return _currentShellProjection?.ShowMetricsList ?? true;
    }

    private string GetMetricsTitle()
    {
        return _currentMetricsProjection?.Title ?? "运行时指标 / Runtime Metrics";
    }

    private IReadOnlyList<FacetRuntimeMetricItem> GetMetricItems()
    {
        return _currentMetricsProjection?.Items ?? Array.Empty<FacetRuntimeMetricItem>();
    }

    private void ApplyShellFallback(string reason)
    {
        _currentShellProjection = null;
        RefreshView(reason);
    }

    private void ApplyMetricsPlaceholder(string reason)
    {
        _currentMetricsProjection = null;
        RefreshView(reason);
    }

    private void OnClientShellChanged(ProjectionChange change)
    {
        if (change.CurrentValue is ClientShellProjection projection)
        {
            ApplyClientShellProjection(projection);
            ClientLog.Info(
                "DungeonPanel",
                "DungeonPanel 收到 ClientShellProjection 变更。",
                new Dictionary<string, object?>
                {
                    ["mode"] = projection.Mode,
                    ["showMetricsList"] = projection.ShowMetricsList,
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
            "DungeonPanel",
            "DungeonPanel 已应用初始 ClientShellProjection。",
            new Dictionary<string, object?>
            {
                ["mode"] = projection.Mode,
                ["showMetricsList"] = projection.ShowMetricsList,
                ["primaryActionEnabled"] = projection.IsPrimaryActionEnabled,
            });
    }

    private void OnRuntimeMetricsChanged(ProjectionChange change)
    {
        if (change.Kind == ProjectionChangeKind.Removed)
        {
            ApplyMetricsPlaceholder("projection.runtime_metrics.removed");
            return;
        }

        if (change.CurrentValue is FacetRuntimeMetricListProjection projection)
        {
            ApplyRuntimeMetricsProjection(projection);
            ClientLog.Info(
                "DungeonPanel",
                "DungeonPanel 收到 RuntimeMetricsProjection 变更。",
                new Dictionary<string, object?>
                {
                    ["itemCount"] = projection.Items.Count,
                    ["updatedAtUtc"] = projection.UpdatedAtUtc.ToString("O"),
                });
        }
    }

    private void ApplyRuntimeMetricsProjection(FacetRuntimeMetricListProjection projection)
    {
        _currentMetricsProjection = projection;
        RefreshView("projection.runtime_metrics");
    }

    private void LogInitialRuntimeMetricsProjection(FacetRuntimeMetricListProjection projection)
    {
        ClientLog.Info(
            "DungeonPanel",
            "DungeonPanel 已应用初始 RuntimeMetricsProjection。",
            new Dictionary<string, object?>
            {
                ["itemCount"] = projection.Items.Count,
                ["updatedAtUtc"] = projection.UpdatedAtUtc.ToString("O"),
            });
    }

    private void ReleaseProjectionSubscriptions()
    {
        _clientShellSubscription?.Dispose();
        _clientShellSubscription = null;
        _metricsSubscription?.Dispose();
        _metricsSubscription = null;
    }

    private void RenderLegacyMetricItems()
    {
        IReadOnlyList<FacetRuntimeMetricItem> items = GetMetricItems();

        foreach (Node child in _metricsListContainer.GetChildren())
        {
            if (!ReferenceEquals(child, _metricsItemTemplate))
            {
                child.QueueFree();
            }
        }

        _metricsItemTemplate.Visible = false;
        _metricsListContainer.Visible = items.Count > 0;
        _metricsEmptyLabel.Visible = items.Count == 0;

        for (int index = 0; index < items.Count; index++)
        {
            if (_metricsItemTemplate.Duplicate() is not Control itemRoot)
            {
                continue;
            }

            itemRoot.Name = $"LegacyMetricItem_{index}";
            itemRoot.Visible = true;
            _metricsListContainer.AddChild(itemRoot);

            itemRoot.GetNode<Label>("MetricsItemMargin/MetricsItemRow/MetricLabel").Text = items[index].Label;
            itemRoot.GetNode<Label>("MetricsItemMargin/MetricsItemRow/MetricValueLabel").Text = items[index].Value;
            itemRoot.GetNode<Label>("MetricsItemMargin/MetricsItemRow/MetricStatusLabel").Text = items[index].Key;
        }
    }

    private static Dictionary<string, object?> CreateLifecyclePayload(
        UIContext context,
        IUIComponentBindingScope? metricsPanelBindings,
        IUIComplexListBinding<FacetRuntimeMetricItem>? metricsListBinding)
    {
        UIBindingDiagnosticsSnapshot? bindingDiagnostics = context.Bindings?.GetDiagnosticsSnapshot();
        UIBindingDiagnosticsSnapshot? metricsPanelDiagnostics = metricsPanelBindings?.GetDiagnosticsSnapshot();
        return new Dictionary<string, object?>
        {
            ["pageId"] = context.PageId,
            ["layer"] = context.Layer,
            ["argumentCount"] = context.Arguments.Count,
            ["bindingCount"] = bindingDiagnostics?.BindingCount ?? 0,
            ["bindingRefreshCount"] = bindingDiagnostics?.RefreshCount ?? 0,
            ["bindingLastReason"] = bindingDiagnostics?.LastRefreshReason,
            ["metricsPanelScopeId"] = metricsPanelDiagnostics?.ScopeId,
            ["metricsPanelBindingCount"] = metricsPanelDiagnostics?.BindingCount ?? 0,
            ["metricsListItemCount"] = metricsListBinding?.ItemCount ?? 0,
        };
    }

    private bool ShouldApplyInitialClientShellProjection(ClientShellProjection projection)
    {
        return string.Equals(projection.Mode, GetExpectedShellMode(), StringComparison.OrdinalIgnoreCase);
    }

    private static string GetExpectedShellMode()
    {
        return "Dungeon";
    }

    private void OnSwitchPressed()
    {
        EmitSignal("SwitchToIdleRequested");
    }

    /// <summary>
    /// 指标项模板适配器。
    /// 每个列表项都会获得独立组件作用域，用于绑定标签、数值和诊断标识。
    /// </summary>
    private sealed class MetricsItemBindingAdapter : IUIComplexListAdapter<FacetRuntimeMetricItem>
    {
        public string GetItemKey(FacetRuntimeMetricItem item, int index)
        {
            return item.Key;
        }

        public void BindItem(IUIComponentBindingScope itemScope, FacetRuntimeMetricItem item, int index)
        {
            itemScope.BindText("MetricLabel", () => item.Label);
            itemScope.BindText("MetricValueLabel", () => item.Value);
            itemScope.BindText("MetricStatusLabel", () => $"Key: {item.Key} / #{index + 1}");
        }
    }
}