#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using Godot;
using Sideline.Facet.Projection;
using Sideline.Facet.Projection.Client;
using Sideline.Facet.Projection.Diagnostics;
using Sideline.Facet.Runtime;

/// <summary>
/// 地下城模式面板：通过 Projection 驱动页面状态与指标列表刷新。
/// </summary>
public partial class DungeonPanel : PanelContainer
{
    /// <summary>
    /// 指标展示区域及其关键控件。
    /// </summary>
    private PanelContainer _metricsPanel = null!;
    private Label _titleLabel = null!;
    private Label _statusLabel = null!;
    private Label _metricsTitleLabel = null!;
    private Label _metricsListLabel = null!;
    private Button _switchButton = null!;

    /// <summary>
    /// Projection 订阅句柄，面板退出时需要释放。
    /// </summary>
    private IDisposable? _clientShellSubscription;
    private IDisposable? _metricsSubscription;

    [Signal]
    public delegate void SwitchToIdleRequestedEventHandler();

    /// <summary>
    /// 初始化地下城面板，并设置基础占位文案与按钮事件。
    /// </summary>
    public override void _Ready()
    {
        _metricsPanel = GetNode<PanelContainer>("%MetricsPanel");
        _titleLabel = GetNode<Label>("%TitleLabel");
        _statusLabel = GetNode<Label>("%StatusLabel");
        _metricsTitleLabel = GetNode<Label>("%MetricsTitleLabel");
        _metricsListLabel = GetNode<Label>("%MetricsListLabel");
        _switchButton = GetNode<Button>("%SwitchButton");

        _switchButton.Pressed += OnSwitchPressed;
        _statusLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _metricsTitleLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _metricsListLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;

        ApplyShellFallback();
        ApplyMetricsPlaceholder();
    }

    /// <summary>
    /// 面板退出时释放 Projection 订阅，避免重复回调。
    /// </summary>
    public override void _ExitTree()
    {
        _clientShellSubscription?.Dispose();
        _clientShellSubscription = null;
        _metricsSubscription?.Dispose();
        _metricsSubscription = null;
    }

    /// <summary>
    /// 绑定 Facet Projection。
    /// 这里同时监听 ClientShell 和 RuntimeMetrics 两类投影。
    /// </summary>
    public void BindFacetProjection()
    {
        _clientShellSubscription?.Dispose();
        _clientShellSubscription = null;
        _metricsSubscription?.Dispose();
        _metricsSubscription = null;

        if (FacetHost.Instance?.IsInitialized != true)
        {
            ApplyMetricsPlaceholder();
            ClientLog.Warning("DungeonPanel", "FacetHost 尚未初始化，Projection 绑定延后。", null);
            return;
        }

        ProjectionStore projectionStore = FacetHost.Instance.Context.ProjectionStore;
        _clientShellSubscription = projectionStore.Subscribe(FacetProjectionKeys.ClientShell, OnClientShellChanged);
        _metricsSubscription = projectionStore.Subscribe(FacetProjectionKeys.RuntimeMetrics, OnRuntimeMetricsChanged);

        bool initialClientShellApplied = false;
        if (projectionStore.TryGet(FacetProjectionKeys.ClientShell, out ClientShellProjection? shellProjection) && shellProjection != null)
        {
            ApplyClientShellProjection(shellProjection);
            LogInitialClientShellProjection(shellProjection);
            initialClientShellApplied = true;
        }
        else
        {
            ApplyShellFallback();
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
            ApplyMetricsPlaceholder();
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

    /// <summary>
    /// 当壳层 Projection 尚未到达时，使用本地兜底文案保证页面仍可阅读。
    /// </summary>
    private void ApplyShellFallback()
    {
        _titleLabel.Text = "Sideline / 地下城";
        _statusLabel.Text = "Projection 驱动战斗窗口 / Projection-driven battle panel";
        _switchButton.Text = "返回挂机 / Idle";
        _switchButton.Disabled = false;
    }

    /// <summary>
    /// 在指标 Projection 尚未就绪前先显示占位文案。
    /// </summary>
    private void ApplyMetricsPlaceholder()
    {
        _metricsTitleLabel.Text = "运行时指标 / Runtime Metrics";
        _metricsListLabel.Text = "等待指标数据 / Waiting for metrics...";
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

    /// <summary>
    /// 应用客户端壳层 Projection 到地下城界面。
    /// </summary>
    private void ApplyClientShellProjection(ClientShellProjection projection)
    {
        _titleLabel.Text = projection.Title;
        _statusLabel.Text = projection.Status;
        _switchButton.Text = projection.PrimaryActionLabel;
        _switchButton.Disabled = projection.IsPrimaryActionEnabled == false;
        _metricsPanel.Visible = projection.ShowMetricsList;
    }

    /// <summary>
    /// 记录初次应用 ClientShellProjection 的日志，便于验证初始化同步链路。
    /// </summary>
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

    /// <summary>
    /// 响应 RuntimeMetricsProjection 变化。
    /// </summary>
    private void OnRuntimeMetricsChanged(ProjectionChange change)
    {
        if (change.Kind == ProjectionChangeKind.Removed)
        {
            ApplyMetricsPlaceholder();
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

    /// <summary>
    /// 把运行时指标 Projection 渲染为地下城面板中的列表文本。
    /// </summary>
    private void ApplyRuntimeMetricsProjection(FacetRuntimeMetricListProjection projection)
    {
        _metricsTitleLabel.Text = projection.Title;

        StringBuilder builder = new();
        foreach (FacetRuntimeMetricItem item in projection.Items)
        {
            builder.Append("- ");
            builder.Append(item.Label);
            builder.Append(": ");
            builder.AppendLine(item.Value);
        }

        _metricsListLabel.Text = builder.ToString().TrimEnd();
    }

    /// <summary>
    /// 记录初次应用 RuntimeMetricsProjection 的日志，便于核对初始数据是否完整。
    /// </summary>
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

    /// <summary>
    /// 触发切换回挂机模式的信号。
    /// </summary>
    private void OnSwitchPressed()
    {
        EmitSignal(SignalName.SwitchToIdleRequested);
    }
}
