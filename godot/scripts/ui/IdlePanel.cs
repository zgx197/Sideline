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
/// 挂机模式面板。
/// 仅保留业务界面与调试入口，不再承载运行时实验面板。
/// </summary>
public partial class IdlePanel : PanelContainer, IUIPageLifecycle
{
    private const string LuaTitleStateKey = "facet.idle.title";
    private const string LuaStatusStateKey = "facet.idle.status";
    private const string LuaPrimaryActionLabelStateKey = "facet.idle.primary_action_label";
    private const string LuaPrimaryActionEnabledStateKey = "facet.idle.primary_action_enabled";
    private const string LuaResourceTextStateKey = "facet.idle.resource_text";
    private const string IdlePageRedDotPath = "client.idle";

    private Label _titleLabel = null!;
    private Label _titleRedDotBadgeLabel = null!;
    private Label _statusLabel = null!;
    private Label _resourceLabel = null!;
    private Button _switchButton = null!;
    private Button _openRuntimeDebugButton = null!;
    private Button _closeButton = null!;

    private IDisposable? _clientShellSubscription;
    private IDisposable? _redDotSubscription;
    private ClientShellProjection? _currentShellProjection;
    private UIContext? _pageContext;
    private bool _isPageShown;
    private bool _nodesBound;
    private bool _bindingsRegistered;
    private bool _hasIdlePageRedDot;
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
        if (_timer >= 1.0d)
        {
            _timer -= 1.0d;
            _gold++;
            RefreshView("idle.gold_tick");
        }
    }

    public void OnPageInitialize(UIContext context)
    {
        _pageContext = context;
        EnsureNodesResolved(context);
        EnsureBindingsRegistered(context);
        BindFacetProjection();
        ClientLog.Info("IdlePanel", "IdlePanel 生命周期 Initialize。", CreateLifecyclePayload(context));
    }

    public void OnPageShow(UIContext context)
    {
        _pageContext = context;
        EnsureNodesResolved(context);
        EnsureBindingsRegistered(context);
        _isPageShown = true;
        SetProcess(true);

        if (_clientShellSubscription == null)
        {
            BindFacetProjection();
        }

        RefreshView("page.show");
        ClientLog.Info("IdlePanel", "IdlePanel 生命周期 Show。", CreateLifecyclePayload(context));
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
        ClientLog.Info("IdlePanel", "IdlePanel 生命周期 Refresh。", CreateLifecyclePayload(context));
    }

    public void OnPageHide(UIContext context)
    {
        _pageContext = context;
        _isPageShown = false;
        SetProcess(false);
        ReleaseProjectionSubscriptions();
        ClientLog.Info("IdlePanel", "IdlePanel 生命周期 Hide。", CreateLifecyclePayload(context));
    }

    public void OnPageDispose(UIContext context)
    {
        _pageContext = context;
        _isPageShown = false;
        SetProcess(false);
        ReleaseProjectionSubscriptions();
        ClientLog.Info("IdlePanel", "IdlePanel 生命周期 Dispose。", CreateLifecyclePayload(context));
    }

    public void BindFacetProjection()
    {
        ReleaseProjectionSubscriptions();

        ProjectionStore? projectionStore = GetProjectionStore();
        if (projectionStore == null)
        {
            RefreshView("projection.pending");
            ClientLog.Warning("IdlePanel", "FacetHost 尚未初始化，Projection 绑定延后。", null);
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
            "IdlePanel",
            "IdlePanel Projection 绑定完成。",
            new Dictionary<string, object?>
            {
                ["clientShellKey"] = Sideline.Facet.Projection.Diagnostics.FacetProjectionKeys.ClientShell.ToString(),
                ["redDotPath"] = IdlePageRedDotPath,
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
        _resourceLabel = ResolveRequiredNode<Label>(resolver, "ResourceLabel", "%ResourceLabel");
        _switchButton = ResolveRequiredNode<Button>(resolver, "SwitchButton", "%SwitchButton");
        _openRuntimeDebugButton = ResolveRequiredNode<Button>(resolver, "OpenRuntimeDebugButton", "%OpenRuntimeDebugButton");
        _closeButton = ResolveRequiredNode<Button>(resolver, "CloseButton", "%CloseButton");

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
            ConnectButtonPressed(_closeButton, OnClosePressed);
            _bindingsRegistered = true;
            return;
        }

        bindings.BindCommand("SwitchButton", OnSwitchPressed);
        bindings.BindCommand("OpenRuntimeDebugButton", OnOpenRuntimeDebugPressed);
        bindings.BindCommand("CloseButton", OnClosePressed);

        if (!useLuaBindings)
        {
            bindings.BindText("TitleLabel", GetTitleText);
            bindings.BindVisibility("TitleRedDotBadgeLabel", HasIdlePageRedDot);
            bindings.BindText("StatusLabel", GetStatusText);
            bindings.BindText("SwitchButton", GetPrimaryActionText);
            bindings.BindInteractable("SwitchButton", IsPrimaryActionEnabled);
            bindings.BindText("ResourceLabel", GetResourceText);
        }

        _bindingsRegistered = true;
        SyncLuaState();
        bindings.RefreshAll("binding.registered");

        ClientLog.Info(
            "IdlePanel",
            "IdlePanel Binding 已注册。",
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
        _titleRedDotBadgeLabel.Visible = HasIdlePageRedDot();
        _statusLabel.Text = GetStatusText();
        _resourceLabel.Text = GetResourceText();
        _switchButton.Text = GetPrimaryActionText();
        _switchButton.Disabled = !IsPrimaryActionEnabled();
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

    private bool HasIdlePageRedDot()
    {
        return _hasIdlePageRedDot;
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
        _pageContext.Lua.SetStateString(LuaResourceTextStateKey, GetResourceText());
    }

    private void OnClientShellChanged(ProjectionChange change)
    {
        if (change.CurrentValue is not ClientShellProjection projection)
        {
            return;
        }

        ApplyClientShellProjection(projection);
        ClientLog.Info(
            "IdlePanel",
            "IdlePanel 收到 ClientShellProjection 变更。",
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
            _hasIdlePageRedDot = false;
            RefreshView("red_dot.pending");
            return;
        }

        IRedDotService resolvedRedDotService = redDotService!;
        _redDotSubscription = resolvedRedDotService.Subscribe(IdlePageRedDotPath, OnRedDotChanged);
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
        _hasIdlePageRedDot =
            redDotService.TryGetSnapshot(IdlePageRedDotPath, out RedDotNodeSnapshot snapshot) &&
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
        return string.Equals(projection.Mode, "Idle", StringComparison.OrdinalIgnoreCase);
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
            ["redDotPagePath"] = IdlePageRedDotPath,
            ["redDotActive"] = context?.Lua?.GetRedDot(IdlePageRedDotPath, false) ?? false,
        };
    }

    private void OnSwitchPressed()
    {
        EmitSignal(SignalName.SwitchToDungeonRequested);
    }

    private void OnClosePressed()
    {
        GetTree().Quit();
    }

    private void OnOpenRuntimeDebugPressed()
    {
        IUIPageNavigator? navigator = _pageContext?.Services.TryGet(out IUIPageNavigator? resolvedNavigator) == true
            ? resolvedNavigator
            : null;

        bool opened = FacetRuntimeDebugService.TryOpenRuntimeDebug(navigator, _pageContext?.PageId, out string statusMessage);
        ClientLog.Info(
            "IdlePanel",
            "IdlePanel 请求打开 Runtime Debug。",
            new Dictionary<string, object?>
            {
                ["opened"] = opened,
                ["currentPageId"] = _pageContext?.PageId,
                ["statusMessage"] = statusMessage,
            });
    }
}
