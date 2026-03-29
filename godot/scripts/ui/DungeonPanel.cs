#nullable enable

using System;
using System.Collections.Generic;
using Godot;
using Sideline.Facet.Extensions.RedDot;
using Sideline.Facet.Projection;
using Sideline.Facet.Projection.Client;
using Sideline.Facet.Runtime;
using Sideline.Facet.Runtime.Debug;
using Sideline.Facet.UI;

/// <summary>
/// 地下城模式面板。
/// 仅保留业务界面与调试入口，不再在页面内承载运行时指标与实验工具。
/// </summary>
public partial class DungeonPanel : PanelContainer, IUIPageLifecycle
{
    private const string LuaTitleStateKey = "facet.dungeon.title";
    private const string LuaStatusStateKey = "facet.dungeon.status";
    private const string LuaPrimaryActionLabelStateKey = "facet.dungeon.primary_action_label";
    private const string LuaPrimaryActionEnabledStateKey = "facet.dungeon.primary_action_enabled";
    private const string DungeonPageRedDotPath = "client.dungeon";

    private Label _titleLabel = null!;
    private Label _titleRedDotBadgeLabel = null!;
    private Label _statusLabel = null!;
    private Button _switchButton = null!;
    private Button _openRuntimeDebugButton = null!;

    private IDisposable? _clientShellSubscription;
    private IDisposable? _redDotSubscription;
    private UIContext? _pageContext;
    private ClientShellProjection? _currentShellProjection;
    private bool _nodesBound;
    private bool _bindingsRegistered;
    private bool _hasDungeonPageRedDot;

    [Signal]
    public delegate void SwitchToIdleRequestedEventHandler();

    public override void _ExitTree()
    {
        ReleaseProjectionSubscriptions();
    }

    public void OnPageInitialize(UIContext context)
    {
        _pageContext = context;
        EnsureNodesResolved(context);
        EnsureBindingsRegistered(context);
        BindFacetProjection();
        ClientLog.Info("DungeonPanel", "DungeonPanel 生命周期 Initialize。", CreateLifecyclePayload(context));
    }

    public void OnPageShow(UIContext context)
    {
        _pageContext = context;
        EnsureNodesResolved(context);
        EnsureBindingsRegistered(context);

        if (_clientShellSubscription == null)
        {
            BindFacetProjection();
        }

        RefreshView("page.show");
        ClientLog.Info("DungeonPanel", "DungeonPanel 生命周期 Show。", CreateLifecyclePayload(context));
    }

    public void OnPageRefresh(UIContext context)
    {
        _pageContext = context;
        EnsureNodesResolved(context);
        EnsureBindingsRegistered(context);

        if (_clientShellSubscription == null)
        {
            BindFacetProjection();
        }

        RefreshView("page.refresh");
        ClientLog.Info("DungeonPanel", "DungeonPanel 生命周期 Refresh。", CreateLifecyclePayload(context));
    }

    public void OnPageHide(UIContext context)
    {
        _pageContext = context;
        ReleaseProjectionSubscriptions();
        ClientLog.Info("DungeonPanel", "DungeonPanel 生命周期 Hide。", CreateLifecyclePayload(context));
    }

    public void OnPageDispose(UIContext context)
    {
        _pageContext = context;
        ReleaseProjectionSubscriptions();
        ClientLog.Info("DungeonPanel", "DungeonPanel 生命周期 Dispose。", CreateLifecyclePayload(context));
    }

    public void BindFacetProjection()
    {
        ReleaseProjectionSubscriptions();

        ProjectionStore? projectionStore = GetProjectionStore();
        if (projectionStore == null)
        {
            RefreshView("projection.pending");
            ClientLog.Warning("DungeonPanel", "FacetHost 尚未初始化，Projection 绑定延后。", null);
            return;
        }

        _clientShellSubscription = projectionStore.Subscribe(Sideline.Facet.Projection.Diagnostics.FacetProjectionKeys.ClientShell, OnClientShellChanged);
        BindRedDotProjection();

        bool initialClientShellApplied = false;
        if (projectionStore.TryGet(Sideline.Facet.Projection.Diagnostics.FacetProjectionKeys.ClientShell, out ClientShellProjection? shellProjection) &&
            shellProjection != null &&
            ShouldApplyInitialClientShellProjection(shellProjection))
        {
            ApplyClientShellProjection(shellProjection);
            initialClientShellApplied = true;
        }
        else
        {
            RefreshView("projection.initial.shell_missing_or_mismatch");
        }

        ClientLog.Info(
            "DungeonPanel",
            "DungeonPanel Projection 绑定完成。",
            new Dictionary<string, object?>
            {
                ["clientShellKey"] = Sideline.Facet.Projection.Diagnostics.FacetProjectionKeys.ClientShell.ToString(),
                ["redDotPath"] = DungeonPageRedDotPath,
                ["initialClientShellApplied"] = initialClientShellApplied,
            });
    }

    private void EnsureNodesResolved(UIContext? context)
    {
        if (_nodesBound)
        {
            return;
        }

        UINodeResolver? resolver = context?.Resolver as UINodeResolver;
        _titleLabel = ResolveRequiredNode<Label>(resolver, "TitleLabel", "%TitleLabel");
        _titleRedDotBadgeLabel = ResolveRequiredNode<Label>(resolver, "TitleRedDotBadgeLabel", "%TitleRedDotBadgeLabel");
        _statusLabel = ResolveRequiredNode<Label>(resolver, "StatusLabel", "%StatusLabel");
        _switchButton = ResolveRequiredNode<Button>(resolver, "SwitchButton", "%SwitchButton");
        _openRuntimeDebugButton = ResolveRequiredNode<Button>(resolver, "OpenRuntimeDebugButton", "%OpenRuntimeDebugButton");

        _statusLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _nodesBound = true;
        RefreshView("nodes.resolved");
    }

    private void EnsureBindingsRegistered(UIContext? context)
    {
        if (_bindingsRegistered)
        {
            return;
        }

        IUIBindingScope? bindings = context?.Bindings;
        bool useLuaBindings = context?.Lua != null;
        if (bindings == null)
        {
            ConnectButtonPressed(_switchButton, OnSwitchPressed);
            ConnectButtonPressed(_openRuntimeDebugButton, OnOpenRuntimeDebugPressed);
            _bindingsRegistered = true;
            return;
        }

        bindings.BindCommand("SwitchButton", OnSwitchPressed);
        bindings.BindCommand("OpenRuntimeDebugButton", OnOpenRuntimeDebugPressed);

        if (!useLuaBindings)
        {
            bindings.BindText("TitleLabel", GetTitleText);
            bindings.BindVisibility("TitleRedDotBadgeLabel", HasDungeonPageRedDot);
            bindings.BindText("StatusLabel", GetStatusText);
            bindings.BindText("SwitchButton", GetPrimaryActionText);
            bindings.BindInteractable("SwitchButton", IsPrimaryActionEnabled);
        }

        _bindingsRegistered = true;
        SyncLuaState();
        bindings.RefreshAll("binding.registered");

        ClientLog.Info(
            "DungeonPanel",
            "DungeonPanel Binding 已注册。",
            CreateBindingRegistrationPayload(context, useLuaBindings));
    }

    private static void ConnectButtonPressed(Button button, Action handler)
    {
        Callable callable = Callable.From(handler);
        if (!button.IsConnected(Button.SignalName.Pressed, callable))
        {
            button.Connect(Button.SignalName.Pressed, callable);
        }
    }

    private TNode ResolveRequiredNode<TNode>(UINodeResolver? resolver, string key, string fallbackPath)
        where TNode : Node
    {
        return resolver?.GetRequired<TNode>(key) ?? GetNode<TNode>(fallbackPath);
    }

    private ProjectionStore? GetProjectionStore()
    {
        return _pageContext?.RuntimeContext.ProjectionStore;
    }

    private bool TryGetRedDotService(out IRedDotService? redDotService)
    {
        redDotService = null;
        return _pageContext?.Services.TryGet(out redDotService) == true && redDotService != null;
    }

    private void RefreshView(string? reason = null)
    {
        SyncLuaState();
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
        _titleRedDotBadgeLabel.Visible = HasDungeonPageRedDot();
        _statusLabel.Text = GetStatusText();
        _switchButton.Text = GetPrimaryActionText();
        _switchButton.Disabled = !IsPrimaryActionEnabled();
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

    private bool HasDungeonPageRedDot()
    {
        return _hasDungeonPageRedDot;
    }

    private void SyncLuaState()
    {
        if (_pageContext?.Lua == null)
        {
            return;
        }

        _pageContext.Lua.SetStateString(LuaTitleStateKey, GetTitleText());
        _pageContext.Lua.SetStateString(LuaStatusStateKey, GetStatusText());
        _pageContext.Lua.SetStateString(LuaPrimaryActionLabelStateKey, GetPrimaryActionText());
        _pageContext.Lua.SetStateBoolean(LuaPrimaryActionEnabledStateKey, IsPrimaryActionEnabled());
    }

    private void OnClientShellChanged(ProjectionChange change)
    {
        if (change.CurrentValue is not ClientShellProjection projection)
        {
            return;
        }

        ApplyClientShellProjection(projection);
        ClientLog.Info(
            "DungeonPanel",
            "DungeonPanel 收到 ClientShellProjection 变更。",
            new Dictionary<string, object?>
            {
                ["mode"] = projection.Mode,
                ["primaryActionEnabled"] = projection.IsPrimaryActionEnabled,
            });
    }

    private void ApplyClientShellProjection(ClientShellProjection projection)
    {
        _currentShellProjection = projection;
        RefreshView("projection.client_shell");
    }

    private void BindRedDotProjection()
    {
        _redDotSubscription?.Dispose();
        _redDotSubscription = null;

        if (!TryGetRedDotService(out IRedDotService? redDotService))
        {
            _hasDungeonPageRedDot = false;
            RefreshView("red_dot.pending");
            return;
        }

        IRedDotService resolvedRedDotService = redDotService!;
        _redDotSubscription = resolvedRedDotService.Subscribe(DungeonPageRedDotPath, OnRedDotChanged);
        ApplyRedDotSnapshot(resolvedRedDotService, "red_dot.initial");
    }

    private void OnRedDotChanged(RedDotChange change)
    {
        if (!TryGetRedDotService(out IRedDotService? redDotService))
        {
            return;
        }

        ApplyRedDotSnapshot(redDotService!, $"red_dot.changed:{change.Source}");
    }

    private void ApplyRedDotSnapshot(IRedDotService redDotService, string reason)
    {
        _hasDungeonPageRedDot =
            redDotService.TryGetSnapshot(DungeonPageRedDotPath, out RedDotNodeSnapshot snapshot) &&
            snapshot.HasRedDot;
        RefreshView(reason);
    }

    private void ReleaseProjectionSubscriptions()
    {
        _clientShellSubscription?.Dispose();
        _clientShellSubscription = null;
        _redDotSubscription?.Dispose();
        _redDotSubscription = null;
    }

    private bool ShouldApplyInitialClientShellProjection(ClientShellProjection projection)
    {
        return string.Equals(projection.Mode, "Dungeon", StringComparison.OrdinalIgnoreCase);
    }

    private static Dictionary<string, object?> CreateBindingRegistrationPayload(UIContext? context, bool useLuaBindings)
    {
        Dictionary<string, object?> payload = CreateLifecyclePayload(context);
        payload["hasResolver"] = context?.Resolver != null;
        payload["useLuaBindings"] = useLuaBindings;
        return payload;
    }

    private static Dictionary<string, object?> CreateLifecyclePayload(UIContext? context)
    {
        UIBindingDiagnosticsSnapshot? bindingDiagnostics = context?.Bindings?.GetDiagnosticsSnapshot();

        return new Dictionary<string, object?>
        {
            ["pageId"] = context?.PageId,
            ["layer"] = context?.Layer,
            ["argumentCount"] = context?.Arguments.Count ?? 0,
            ["bindingCount"] = bindingDiagnostics?.BindingCount ?? 0,
            ["bindingRefreshCount"] = bindingDiagnostics?.RefreshCount ?? 0,
            ["bindingLastReason"] = bindingDiagnostics?.LastRefreshReason,
            ["redDotPagePath"] = DungeonPageRedDotPath,
            ["redDotActive"] = context?.Lua?.GetRedDot(DungeonPageRedDotPath, false) ?? false,
        };
    }

    private void OnSwitchPressed()
    {
        EmitSignal(SignalName.SwitchToIdleRequested);
    }

    private void OnOpenRuntimeDebugPressed()
    {
        IUIPageNavigator? navigator = _pageContext?.Services.TryGet(out IUIPageNavigator? resolvedNavigator) == true
            ? resolvedNavigator
            : null;

        bool opened = FacetRuntimeDebugService.TryOpenRuntimeDebug(navigator, _pageContext?.PageId, out string statusMessage);
        ClientLog.Info(
            "DungeonPanel",
            "DungeonPanel 请求打开 Runtime Debug。",
            new Dictionary<string, object?>
            {
                ["opened"] = opened,
                ["currentPageId"] = _pageContext?.PageId,
                ["statusMessage"] = statusMessage,
            });
    }
}
