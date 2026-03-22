#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using Godot;
using Sideline.Facet.Runtime;

/// <summary>
/// Facet 编辑器主工作区页面。
/// 使用紧凑工具栏加整页顶层页签，避免把多个工具硬塞进同一块内容区域。
/// </summary>
[Tool]
public partial class FacetMainScreen : PanelContainer
{
    private const int MaxVisibleEntries = 150;
    private const int HistoryLimit = 10;
    private const int MaxObserverEntries = 12;

    private GridContainer _logMetricsGrid = null!;
    private HSplitContainer _logSplit = null!;
    private PanelContainer _logSidebarCard = null!;
    private PanelContainer _logContentCard = null!;
    private PanelContainer _retentionCard = null!;
    private PanelContainer _hotReloadIntroCard = null!;
    private PanelContainer _hotReloadEvidenceCard = null!;
    private PanelContainer _layoutLabIntroCard = null!;
    private PanelContainer _layoutLabEvidenceCard = null!;
    private PanelContainer _runtimeDiagnosticsIntroCard = null!;
    private PanelContainer _runtimeDiagnosticsSnapshotCard = null!;
    private PanelContainer _runtimeDiagnosticsValidationCard = null!;
    private PanelContainer _runtimeDiagnosticsObserverCard = null!;
    private PanelContainer _totalMetricCard = null!;
    private PanelContainer _filteredMetricCard = null!;
    private PanelContainer _sessionsMetricCard = null!;
    private PanelContainer _categoriesMetricCard = null!;
    private TabContainer _workspaceTabs = null!;

    private Label _totalValueLabel = null!;
    private Label _filteredValueLabel = null!;
    private Label _sessionsValueLabel = null!;
    private Label _categoriesValueLabel = null!;
    private Label _logSummaryLabel = null!;
    private Label _pathLabel = null!;
    private Label _retentionLabel = null!;
    private Label _hotReloadBridgeStatusLabel = null!;
    private Label _hotReloadBridgePathsLabel = null!;
    private Label _layoutLabBridgeStatusLabel = null!;
    private Label _layoutLabBridgePathsLabel = null!;
    private Label _runtimeDiagnosticsStatusLabel = null!;
    private Label _runtimeDiagnosticsPathsLabel = null!;
    private Label _runtimeDiagnosticsObserverSummaryLabel = null!;
    private RichTextLabel _entriesLabel = null!;
    private RichTextLabel _hotReloadEvidenceLabel = null!;
    private RichTextLabel _layoutLabEvidenceLabel = null!;
    private RichTextLabel _runtimeDiagnosticsSnapshotLabel = null!;
    private RichTextLabel _runtimeDiagnosticsValidationLabel = null!;
    private RichTextLabel _runtimeDiagnosticsObserverLabel = null!;
    private LineEdit _sessionFilter = null!;
    private LineEdit _categoryFilter = null!;
    private LineEdit _runtimeDiagnosticsFocusFilter = null!;
    private OptionButton _levelFilter = null!;
    private OptionButton _runtimeDiagnosticsObserverChannelFilter = null!;
    private CheckButton _autoRefreshToggle = null!;
    private Button _refreshButton = null!;
    private Button _openLogButton = null!;
    private Button _currentPageReloadTestButton = null!;
    private Button _dungeonReloadTestButton = null!;
    private Button _refreshHotReloadStatusButton = null!;
    private Button _openGeneratedLayoutLabButton = null!;
    private Button _openTemplateLayoutLabButton = null!;
    private Button _refreshLayoutLabStatusButton = null!;
    private Button _runtimeDiagnosticsUseCurrentPageButton = null!;
    private Button _runtimeDiagnosticsClearFocusButton = null!;
    private bool _isUiReady;
    private double _autoRefreshElapsedSeconds;

    public override void _Ready()
    {
        try
        {
            FacetEditorDiagnostics.Info("MainScreen", $"Ready parent={GetParent()?.GetType().Name ?? "<null>"}");
            Name = "FacetMainScreen";
            SizeFlagsHorizontal = SizeFlags.ExpandFill;
            SizeFlagsVertical = SizeFlags.ExpandFill;
            CustomMinimumSize = Vector2.Zero;

            ResolveUi();
            ConfigureUi();

            _isUiReady = true;
            SetProcess(true);
            RefreshNow();
            UpdateResponsiveLayout();
            CallDeferred(nameof(UpdateResponsiveLayout));
        }
        catch (Exception exception)
        {
            FacetEditorDiagnostics.Error("MainScreen", "Ready failed.", exception);
            throw;
        }
    }

    public override void _Notification(int what)
    {
        base._Notification(what);

        if (!_isUiReady)
        {
            return;
        }

        if (what == NotificationResized)
        {
            UpdateResponsiveLayout();
        }
    }

    public override void _Process(double delta)
    {
        base._Process(delta);

        if (!_isUiReady || !_autoRefreshToggle.ButtonPressed || !IsVisibleInTree())
        {
            _autoRefreshElapsedSeconds = 0.0d;
            return;
        }

        _autoRefreshElapsedSeconds += delta;
        if (_autoRefreshElapsedSeconds < 1.0d)
        {
            return;
        }

        _autoRefreshElapsedSeconds = 0.0d;
        RefreshNow();
    }

    public void EnsureViewportLayout()
    {
        if (_isUiReady)
        {
            UpdateResponsiveLayout();
        }
    }

    public void LogLayoutSnapshot()
    {
        try
        {
            FacetEditorDiagnostics.Info(
                "MainScreen",
                $"LayoutSnapshot visible={Visible} size={Size} min={CustomMinimumSize} parentSize={(GetParent() as Control)?.Size} tabsSize={_workspaceTabs?.Size} logSplitSize={_logSplit?.Size} sidebarSize={_logSidebarCard?.Size} logContentSize={_logContentCard?.Size}");
        }
        catch (Exception exception)
        {
            FacetEditorDiagnostics.Error("MainScreen", "LogLayoutSnapshot failed.", exception);
        }
    }

    public void RefreshNow()
    {
        if (!_isUiReady)
        {
            return;
        }

        try
        {
            string logPath = ProjectSettings.GlobalizePath("user://logs/facet-structured.jsonl");
            _pathLabel.Text = logPath;
            _retentionLabel.Text = $"当前活动日志 + 最近 {HistoryLimit} 次历史会话";

            if (!File.Exists(logPath))
            {
                UpdateMetrics(0, 0, 0, 0);
                _logSummaryLabel.Text = "结构化日志已启用，但当前还没有活动日志文件。先运行一次主场景，再回到这里查看。";
                _entriesLabel.Text = "运行一次主场景后，这里会显示当前会话的 facet-structured.jsonl。";
                RefreshHotReloadLabStatus(new List<FacetEditorLogEntry>());
                RefreshLayoutLabStatus(new List<FacetEditorLogEntry>());
                RefreshRuntimeDiagnosticsStatus(new List<FacetEditorLogEntry>());
                return;
            }

            List<FacetEditorLogEntry> allEntries = LoadEntries(logPath);
            List<FacetEditorLogEntry> filteredEntries = FilterEntries(allEntries);
            RenderEntries(allEntries, filteredEntries);
            RefreshHotReloadLabStatus(allEntries);
            RefreshLayoutLabStatus(allEntries);
            RefreshRuntimeDiagnosticsStatus(allEntries);
        }
        catch (Exception exception)
        {
            FacetEditorDiagnostics.Error("MainScreen", "RefreshNow failed.", exception);
            throw;
        }
    }

    private void ResolveUi()
    {
        _logMetricsGrid = ResolveRequiredNode<GridContainer>("%LogMetricsGrid");
        _logSplit = ResolveRequiredNode<HSplitContainer>("%LogSplit");
        _logSidebarCard = ResolveRequiredNode<PanelContainer>("%LogSidebarCard");
        _logContentCard = ResolveRequiredNode<PanelContainer>("%LogContentCard");
        _retentionCard = ResolveRequiredNode<PanelContainer>("%RetentionCard");
        _hotReloadIntroCard = ResolveRequiredNode<PanelContainer>("%HotReloadIntroCard");
        _hotReloadEvidenceCard = ResolveRequiredNode<PanelContainer>("%HotReloadEvidenceCard");
        _layoutLabIntroCard = ResolveRequiredNode<PanelContainer>("%LayoutLabIntroCard");
        _layoutLabEvidenceCard = ResolveRequiredNode<PanelContainer>("%LayoutLabEvidenceCard");
        _runtimeDiagnosticsIntroCard = ResolveRequiredNode<PanelContainer>("%RuntimeDiagnosticsIntroCard");
        _runtimeDiagnosticsSnapshotCard = ResolveRequiredNode<PanelContainer>("%RuntimeDiagnosticsSnapshotCard");
        _runtimeDiagnosticsValidationCard = ResolveRequiredNode<PanelContainer>("%RuntimeDiagnosticsValidationCard");
        _runtimeDiagnosticsObserverCard = ResolveRequiredNode<PanelContainer>("%RuntimeDiagnosticsObserverCard");
        _totalMetricCard = ResolveRequiredNode<PanelContainer>("%TotalMetricCard");
        _filteredMetricCard = ResolveRequiredNode<PanelContainer>("%FilteredMetricCard");
        _sessionsMetricCard = ResolveRequiredNode<PanelContainer>("%SessionsMetricCard");
        _categoriesMetricCard = ResolveRequiredNode<PanelContainer>("%CategoriesMetricCard");
        _workspaceTabs = ResolveRequiredNode<TabContainer>("%WorkspaceTabs");

        _totalValueLabel = ResolveRequiredNode<Label>("%TotalValueLabel");
        _filteredValueLabel = ResolveRequiredNode<Label>("%FilteredValueLabel");
        _sessionsValueLabel = ResolveRequiredNode<Label>("%SessionsValueLabel");
        _categoriesValueLabel = ResolveRequiredNode<Label>("%CategoriesValueLabel");
        _logSummaryLabel = ResolveRequiredNode<Label>("%LogSummaryLabel");
        _pathLabel = ResolveRequiredNode<Label>("%PathLabel");
        _retentionLabel = ResolveRequiredNode<Label>("%RetentionLabel");
        _hotReloadBridgeStatusLabel = ResolveRequiredNode<Label>("%HotReloadBridgeStatusLabel");
        _hotReloadBridgePathsLabel = ResolveRequiredNode<Label>("%HotReloadBridgePathsLabel");
        _layoutLabBridgeStatusLabel = ResolveRequiredNode<Label>("%LayoutLabBridgeStatusLabel");
        _layoutLabBridgePathsLabel = ResolveRequiredNode<Label>("%LayoutLabBridgePathsLabel");
        _runtimeDiagnosticsStatusLabel = ResolveRequiredNode<Label>("%RuntimeDiagnosticsStatusLabel");
        _runtimeDiagnosticsPathsLabel = ResolveRequiredNode<Label>("%RuntimeDiagnosticsPathsLabel");
        _runtimeDiagnosticsObserverSummaryLabel = ResolveRequiredNode<Label>("%RuntimeDiagnosticsObserverSummaryLabel");
        _entriesLabel = ResolveRequiredNode<RichTextLabel>("%EntriesLabel");
        _hotReloadEvidenceLabel = ResolveRequiredNode<RichTextLabel>("%HotReloadEvidenceLabel");
        _layoutLabEvidenceLabel = ResolveRequiredNode<RichTextLabel>("%LayoutLabEvidenceLabel");
        _runtimeDiagnosticsSnapshotLabel = ResolveRequiredNode<RichTextLabel>("%RuntimeDiagnosticsSnapshotLabel");
        _runtimeDiagnosticsValidationLabel = ResolveRequiredNode<RichTextLabel>("%RuntimeDiagnosticsValidationLabel");
        _runtimeDiagnosticsObserverLabel = ResolveRequiredNode<RichTextLabel>("%RuntimeDiagnosticsObserverLabel");
        _sessionFilter = ResolveRequiredNode<LineEdit>("%SessionFilter");
        _categoryFilter = ResolveRequiredNode<LineEdit>("%CategoryFilter");
        _runtimeDiagnosticsFocusFilter = ResolveRequiredNode<LineEdit>("%RuntimeDiagnosticsFocusFilter");
        _levelFilter = ResolveRequiredNode<OptionButton>("%LevelFilter");
        _runtimeDiagnosticsObserverChannelFilter = ResolveRequiredNode<OptionButton>("%RuntimeDiagnosticsObserverChannelFilter");
        _autoRefreshToggle = ResolveRequiredNode<CheckButton>("%AutoRefreshToggle");
        _refreshButton = ResolveRequiredNode<Button>("%RefreshButton");
        _openLogButton = ResolveRequiredNode<Button>("%OpenLogButton");
        _currentPageReloadTestButton = ResolveRequiredNode<Button>("%CurrentPageReloadTestButton");
        _dungeonReloadTestButton = ResolveRequiredNode<Button>("%DungeonReloadTestButton");
        _refreshHotReloadStatusButton = ResolveRequiredNode<Button>("%RefreshHotReloadStatusButton");
        _openGeneratedLayoutLabButton = ResolveRequiredNode<Button>("%OpenGeneratedLayoutLabButton");
        _openTemplateLayoutLabButton = ResolveRequiredNode<Button>("%OpenTemplateLayoutLabButton");
        _refreshLayoutLabStatusButton = ResolveRequiredNode<Button>("%RefreshLayoutLabStatusButton");
        _runtimeDiagnosticsUseCurrentPageButton = ResolveRequiredNode<Button>("%RuntimeDiagnosticsUseCurrentPageButton");
        _runtimeDiagnosticsClearFocusButton = ResolveRequiredNode<Button>("%RuntimeDiagnosticsClearFocusButton");
    }

    private void ConfigureUi()
    {
        ApplyTheme();
        ConfigureFilters();
        ConfigureTabTitles();

        _refreshButton.Pressed += RefreshNow;
        _openLogButton.Pressed += OnOpenUserLogsPressed;
        _currentPageReloadTestButton.Pressed += OnRunCurrentPageHotReloadTestPressed;
        _dungeonReloadTestButton.Pressed += OnRunDungeonHotReloadTestPressed;
        _refreshHotReloadStatusButton.Pressed += RefreshNow;
        _openGeneratedLayoutLabButton.Pressed += OnOpenGeneratedLayoutLabPressed;
        _openTemplateLayoutLabButton.Pressed += OnOpenTemplateLayoutLabPressed;
        _refreshLayoutLabStatusButton.Pressed += RefreshNow;
        _runtimeDiagnosticsUseCurrentPageButton.Pressed += OnUseCurrentPageDiagnosticsFocusPressed;
        _runtimeDiagnosticsClearFocusButton.Pressed += OnClearDiagnosticsFocusPressed;
    }

    private void ConfigureTabTitles()
    {
        if (_workspaceTabs.GetChildCount() < 4)
        {
            return;
        }

        _workspaceTabs.GetChild(0).Name = "日志";
        _workspaceTabs.GetChild(1).Name = "热重载";
        _workspaceTabs.GetChild(2).Name = "布局";
        _workspaceTabs.GetChild(3).Name = "诊断";
    }

    private void ApplyTheme()
    {
        AddThemeStyleboxOverride("panel", CreateScreenPanelStyle());

        foreach (PanelContainer card in new[]
                 {
                     _logSidebarCard,
                     _logContentCard,
                     _retentionCard,
                     _hotReloadIntroCard,
                     _hotReloadEvidenceCard,
                     _layoutLabIntroCard,
                     _layoutLabEvidenceCard,
                     _runtimeDiagnosticsIntroCard,
                     _runtimeDiagnosticsSnapshotCard,
                     _runtimeDiagnosticsValidationCard,
                     _runtimeDiagnosticsObserverCard,
                     _totalMetricCard,
                     _filteredMetricCard,
                     _sessionsMetricCard,
                     _categoriesMetricCard,
                 })
        {
            card.AddThemeStyleboxOverride("panel", CreateCardStyle());
        }

        _retentionCard.AddThemeStyleboxOverride("panel", CreateStatusCardStyle());
        _entriesLabel.AddThemeStyleboxOverride("normal", CreateLogSurfaceStyle());
        _hotReloadEvidenceLabel.AddThemeStyleboxOverride("normal", CreateLogSurfaceStyle());

        foreach (Label valueLabel in new[] { _totalValueLabel, _filteredValueLabel, _sessionsValueLabel, _categoriesValueLabel })
        {
            valueLabel.AddThemeFontSizeOverride("font_size", 22);
            valueLabel.AddThemeColorOverride("font_color", new Color("f3f6fb"));
        }

        _logSummaryLabel.AddThemeColorOverride("font_color", new Color("a8b3c7"));
        _pathLabel.AddThemeColorOverride("font_color", new Color("9aa7bc"));
        _retentionLabel.AddThemeColorOverride("font_color", new Color("8fd1ae"));
        _hotReloadBridgeStatusLabel.AddThemeColorOverride("font_color", new Color("d7deea"));
        _hotReloadBridgePathsLabel.AddThemeColorOverride("font_color", new Color("8f9db3"));
        _layoutLabBridgeStatusLabel.AddThemeColorOverride("font_color", new Color("d7deea"));
        _layoutLabBridgePathsLabel.AddThemeColorOverride("font_color", new Color("8f9db3"));
        _runtimeDiagnosticsStatusLabel.AddThemeColorOverride("font_color", new Color("d7deea"));
        _runtimeDiagnosticsPathsLabel.AddThemeColorOverride("font_color", new Color("8f9db3"));
        _runtimeDiagnosticsObserverSummaryLabel.AddThemeColorOverride("font_color", new Color("9aa7bc"));
        _entriesLabel.AddThemeColorOverride("default_color", new Color("d7deea"));
        _hotReloadEvidenceLabel.AddThemeColorOverride("default_color", new Color("d7deea"));
        _layoutLabEvidenceLabel.AddThemeColorOverride("default_color", new Color("d7deea"));
        _runtimeDiagnosticsSnapshotLabel.AddThemeColorOverride("default_color", new Color("d7deea"));
        _runtimeDiagnosticsValidationLabel.AddThemeColorOverride("default_color", new Color("d7deea"));
        _runtimeDiagnosticsObserverLabel.AddThemeColorOverride("default_color", new Color("d7deea"));

        _layoutLabEvidenceLabel.AddThemeStyleboxOverride("normal", CreateLogSurfaceStyle());
        _runtimeDiagnosticsSnapshotLabel.AddThemeStyleboxOverride("normal", CreateLogSurfaceStyle());
        _runtimeDiagnosticsValidationLabel.AddThemeStyleboxOverride("normal", CreateLogSurfaceStyle());
        _runtimeDiagnosticsObserverLabel.AddThemeStyleboxOverride("normal", CreateLogSurfaceStyle());
    }

    private void ConfigureFilters()
    {
        _sessionFilter.TextChanged += OnFilterChanged;
        _categoryFilter.TextChanged += OnFilterChanged;

        _levelFilter.Clear();
        AddLevelItem("Trace / 跟踪", 0);
        AddLevelItem("Debug / 调试", 1);
        AddLevelItem("Info / 信息", 2);
        AddLevelItem("Warning / 警告", 3);
        AddLevelItem("Error / 错误", 4);
        _levelFilter.Select(0);
        _levelFilter.ItemSelected += OnLevelSelected;

        _runtimeDiagnosticsFocusFilter.TextChanged += OnDiagnosticsObserverFilterChanged;
        _runtimeDiagnosticsObserverChannelFilter.Clear();
        _runtimeDiagnosticsObserverChannelFilter.AddItem("全部主题", 0);
        _runtimeDiagnosticsObserverChannelFilter.AddItem("生命周期", 1);
        _runtimeDiagnosticsObserverChannelFilter.AddItem("Projection", 2);
        _runtimeDiagnosticsObserverChannelFilter.AddItem("红点", 3);
        _runtimeDiagnosticsObserverChannelFilter.AddItem("Binding", 4);
        _runtimeDiagnosticsObserverChannelFilter.AddItem("工具", 5);
        _runtimeDiagnosticsObserverChannelFilter.Select(0);
        _runtimeDiagnosticsObserverChannelFilter.ItemSelected += OnDiagnosticsObserverChannelSelected;
    }

    private static StyleBoxFlat CreateScreenPanelStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = new Color("161a22"),
        };
    }

    private static StyleBoxFlat CreateCardStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = new Color("1d2330"),
            BorderColor = new Color("2f3949"),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10,
            CornerRadiusBottomRight = 10,
            CornerRadiusBottomLeft = 10,
        };
    }

    private static StyleBoxFlat CreateStatusCardStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = new Color("1c2a27"),
            BorderColor = new Color("35584d"),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10,
            CornerRadiusBottomRight = 10,
            CornerRadiusBottomLeft = 10,
        };
    }

    private static StyleBoxFlat CreateLogSurfaceStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = new Color("151b25"),
            BorderColor = new Color("2e3847"),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomRight = 8,
            CornerRadiusBottomLeft = 8,
            ContentMarginLeft = 12,
            ContentMarginTop = 12,
            ContentMarginRight = 12,
            ContentMarginBottom = 12,
        };
    }

    private void AddLevelItem(string label, int id)
    {
        _levelFilter.AddItem(label, id);
    }

    private void OnFilterChanged(string value)
    {
        RefreshNow();
    }

    private void OnLevelSelected(long index)
    {
        RefreshNow();
    }

    private void OnDiagnosticsObserverFilterChanged(string value)
    {
        RefreshNow();
    }

    private void OnDiagnosticsObserverChannelSelected(long index)
    {
        RefreshNow();
    }

    private void OnUseCurrentPageDiagnosticsFocusPressed()
    {
        if (FacetRuntimeDiagnosticsBridge.TryLoadSnapshot(out FacetRuntimeDiagnosticsSnapshot? snapshot) &&
            snapshot != null &&
            !string.IsNullOrWhiteSpace(snapshot.CurrentPageId))
        {
            _runtimeDiagnosticsFocusFilter.Text = snapshot.CurrentPageId;
            RefreshNow();
        }
    }

    private void OnClearDiagnosticsFocusPressed()
    {
        if (string.IsNullOrWhiteSpace(_runtimeDiagnosticsFocusFilter.Text))
        {
            return;
        }

        _runtimeDiagnosticsFocusFilter.Text = string.Empty;
        RefreshNow();
    }

    private void OnOpenUserLogsPressed()
    {
        string userLogsDirectory = ProjectSettings.GlobalizePath("user://logs");
        FacetEditorDiagnostics.Info("MainScreen", $"OpenUserLogs path={userLogsDirectory}");
        OS.ShellOpen(userLogsDirectory);
    }

    private void UpdateMetrics(int totalCount, int filteredCount, int sessionCount, int categoryCount)
    {
        _totalValueLabel.Text = totalCount.ToString();
        _filteredValueLabel.Text = filteredCount.ToString();
        _sessionsValueLabel.Text = sessionCount.ToString();
        _categoriesValueLabel.Text = categoryCount.ToString();
    }

    private static List<FacetEditorLogEntry> LoadEntries(string logPath)
    {
        List<FacetEditorLogEntry> entries = new();
        using FileStream stream = new(logPath, FileMode.Open, System.IO.FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using StreamReader reader = new(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        while (!reader.EndOfStream)
        {
            string? line = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (TryParseEntry(line, out FacetEditorLogEntry? entry) && entry != null)
            {
                entries.Add(entry);
            }
        }

        return entries;
    }

    private List<FacetEditorLogEntry> FilterEntries(List<FacetEditorLogEntry> allEntries)
    {
        string sessionPrefix = _sessionFilter.Text.Trim();
        string categoryPrefix = _categoryFilter.Text.Trim();
        int minimumLevel = (int)_levelFilter.GetSelectedId();

        List<FacetEditorLogEntry> filtered = new();
        foreach (FacetEditorLogEntry entry in allEntries)
        {
            if (entry.LevelRank < minimumLevel)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(sessionPrefix) &&
                !entry.SessionId.StartsWith(sessionPrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(categoryPrefix) &&
                !entry.Category.StartsWith(categoryPrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            filtered.Add(entry);
        }

        return filtered;
    }

    private void RenderEntries(List<FacetEditorLogEntry> allEntries, List<FacetEditorLogEntry> filteredEntries)
    {
        HashSet<string> sessions = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> categories = new(StringComparer.OrdinalIgnoreCase);
        foreach (FacetEditorLogEntry entry in filteredEntries)
        {
            sessions.Add(entry.SessionId);
            categories.Add(entry.Category);
        }

        UpdateMetrics(allEntries.Count, filteredEntries.Count, sessions.Count, categories.Count);
        _logSummaryLabel.Text =
            $"当前显示最近 {Math.Min(filteredEntries.Count, MaxVisibleEntries)} 条，过滤后共 {filteredEntries.Count} 条结构化日志。";

        if (filteredEntries.Count == 0)
        {
            _entriesLabel.Text = "当前筛选条件下没有匹配日志。";
            return;
        }

        StringBuilder builder = new();
        int startIndex = Math.Max(0, filteredEntries.Count - MaxVisibleEntries);
        for (int index = startIndex; index < filteredEntries.Count; index++)
        {
            FacetEditorLogEntry entry = filteredEntries[index];
            string levelColor = GetLevelColorHex(entry.LevelRank);
            builder.Append("[color=#8fa3bf][S:");
            builder.Append(GetShortSessionId(entry.SessionId));
            builder.Append("] [E:");
            builder.Append(entry.EventId);
            builder.Append("] [/color]");
            builder.Append("[color=");
            builder.Append(levelColor);
            builder.Append("][");
            builder.Append(EscapeBbcode(entry.LevelName));
            builder.Append("][/color] ");
            builder.Append("[color=#73a7ff][");
            builder.Append(EscapeBbcode(entry.Category));
            builder.Append("][/color] ");
            builder.Append("[color=#e5ebf5]");
            builder.Append(EscapeBbcode(entry.Message));
            builder.Append("[/color]");
            builder.AppendLine();
            builder.Append("[color=#7f8da3]@ ");
            builder.Append(EscapeBbcode(entry.TimestampUtc));
            builder.Append("[/color]");
            builder.AppendLine();

            if (!string.IsNullOrWhiteSpace(entry.PayloadJson))
            {
                builder.Append("[color=#7bc7b0]payload[/color]: ");
                builder.Append(EscapeBbcode(entry.PayloadJson));
                builder.AppendLine();
            }

            if (index < filteredEntries.Count - 1)
            {
                builder.AppendLine();
                builder.AppendLine("[color=#2f3949]----------------------------------------[/color]");
                builder.AppendLine();
            }
        }

        _entriesLabel.Text = builder.ToString().TrimEnd();
    }

    private void OnRunCurrentPageHotReloadTestPressed()
    {
        QueueHotReloadLabRequest(FacetHotReloadLabBridge.CommandCurrentPageRoundTrip, "editor.workspace.current");
    }

    private void OnRunDungeonHotReloadTestPressed()
    {
        QueueHotReloadLabRequest(FacetHotReloadLabBridge.CommandDungeonRoundTrip, "editor.workspace.dungeon");
    }

    private void OnOpenGeneratedLayoutLabPressed()
    {
        QueueLayoutLabRequest(FacetLayoutLabBridge.CommandOpenGeneratedLayoutLab, "editor.workspace.layout.generated");
    }

    private void OnOpenTemplateLayoutLabPressed()
    {
        QueueLayoutLabRequest(FacetLayoutLabBridge.CommandOpenTemplateLayoutLab, "editor.workspace.layout.template");
    }

    private void QueueHotReloadLabRequest(string command, string issuedBy)
    {
        try
        {
            FacetHotReloadLabRequest request = FacetHotReloadLabBridge.CreateRequest(command, issuedBy);
            FacetHotReloadLabBridge.SaveRequest(request);
            FacetHotReloadLabBridge.SaveStatus(
                FacetHotReloadLabBridge.CreateRequestedStatus(
                    request,
                    "编辑器工作台已写入测试请求，等待运行中的客户端处理。"));

            FacetEditorDiagnostics.Info(
                "MainScreen",
                $"HotReloadLabRequest command={command} requestId={request.RequestId} issuedBy={issuedBy}");

            RefreshNow();
        }
        catch (Exception exception)
        {
            FacetEditorDiagnostics.Error("MainScreen", "QueueHotReloadLabRequest failed.", exception);
            throw;
        }
    }

    private void QueueLayoutLabRequest(string command, string issuedBy)
    {
        try
        {
            FacetLayoutLabRequest request = FacetLayoutLabBridge.CreateRequest(command, issuedBy);
            FacetLayoutLabBridge.SaveRequest(request);
            FacetLayoutLabBridge.SaveStatus(
                FacetLayoutLabBridge.CreateRequestedStatus(
                    request,
                    "编辑器工作台已写入布局实验请求，等待运行中的客户端处理。"));

            FacetEditorDiagnostics.Info(
                "MainScreen",
                $"LayoutLabRequest command={command} requestId={request.RequestId} issuedBy={issuedBy}");

            RefreshNow();
        }
        catch (Exception exception)
        {
            FacetEditorDiagnostics.Error("MainScreen", "QueueLayoutLabRequest failed.", exception);
            throw;
        }
    }

    private void RefreshHotReloadLabStatus(List<FacetEditorLogEntry> allEntries)
    {
        string requestPath = FacetHotReloadLabBridge.GetRequestPath();
        string statusPath = FacetHotReloadLabBridge.GetStatusPath();
        FacetHotReloadLabBridge.TryLoadStatus(out FacetHotReloadLabStatus? status);

        _hotReloadBridgeStatusLabel.Text = status == null
            ? "尚未检测到 Hot Reload Lab 状态文件。请先运行主场景，或在本页签中发起一次测试请求。"
            : BuildHotReloadStatusText(status);

        _hotReloadBridgePathsLabel.Text =
            $"Request: {requestPath}\n" +
            $"Status: {statusPath}";

        _hotReloadEvidenceLabel.Text = BuildHotReloadEvidenceText(allEntries);
    }

    private void RefreshLayoutLabStatus(List<FacetEditorLogEntry> allEntries)
    {
        string requestPath = FacetLayoutLabBridge.GetRequestPath();
        string statusPath = FacetLayoutLabBridge.GetStatusPath();
        FacetLayoutLabBridge.TryLoadStatus(out FacetLayoutLabStatus? status);

        _layoutLabBridgeStatusLabel.Text = status == null
            ? "尚未检测到 Layout Lab 状态文件。请先运行主场景，或在本页签中发起一次布局入口请求。"
            : BuildLayoutStatusText(status);

        _layoutLabBridgePathsLabel.Text =
            $"Request: {requestPath}\n" +
            $"Status: {statusPath}";

        _layoutLabEvidenceLabel.Text = BuildLayoutEvidenceText(allEntries);
    }

    private void RefreshRuntimeDiagnosticsStatus(List<FacetEditorLogEntry> allEntries)
    {
        string snapshotPath = FacetRuntimeDiagnosticsBridge.GetSnapshotPath();
        bool hasSnapshot = FacetRuntimeDiagnosticsBridge.TryLoadSnapshot(out FacetRuntimeDiagnosticsSnapshot? snapshot) &&
            snapshot != null;

        _runtimeDiagnosticsStatusLabel.Text = hasSnapshot
            ? BuildRuntimeDiagnosticsStatusText(snapshot!)
            : "尚未检测到运行时诊断快照。请先运行主场景，等待 FacetHost 输出阶段 12 快照。";

        _runtimeDiagnosticsPathsLabel.Text = $"Snapshot: {snapshotPath}";
        _runtimeDiagnosticsSnapshotLabel.Text = hasSnapshot
            ? BuildRuntimeDiagnosticsSnapshotText(snapshot!)
            : "运行时诊断页会在这里展示页面注册表、活动运行时、Projection 键、Lua 脚本和红点路径。";
        _runtimeDiagnosticsValidationLabel.Text = hasSnapshot
            ? BuildRuntimeDiagnosticsValidationText(snapshot!)
            : "运行一次主场景后，这里会显示运行时校验结果。";
        _runtimeDiagnosticsObserverSummaryLabel.Text = hasSnapshot
            ? BuildRuntimeDiagnosticsObserverSummaryText(snapshot!, allEntries)
            : "运行一次主场景后，这里会显示生命周期 / Projection / 红点 / Binding 的深度观察摘要。";
        _runtimeDiagnosticsObserverLabel.Text = BuildRuntimeDiagnosticsObserverText(allEntries, snapshot);
    }

    private static string BuildHotReloadStatusText(FacetHotReloadLabStatus status)
    {
        string successText = status.Success switch
        {
            true => "成功",
            false => "失败",
            _ => "未定",
        };

        return
            $"状态: {status.State}  结果: {successText}\n" +
            $"命令: {status.Command}\n" +
            $"请求: {status.RequestId}\n" +
            $"来源: {status.IssuedBy}\n" +
            $"发起时间: {status.IssuedAtUtc}\n" +
            $"更新时间: {status.UpdatedAtUtc}\n" +
            $"运行时会话: {status.RuntimeSessionId}\n" +
            $"运行时页面: {status.RuntimePageId}\n" +
            $"说明: {status.Message}";
    }

    private static string BuildHotReloadEvidenceText(List<FacetEditorLogEntry> allEntries)
    {
        List<FacetEditorLogEntry> matches = new();
        for (int index = allEntries.Count - 1; index >= 0 && matches.Count < 4; index--)
        {
            FacetEditorLogEntry entry = allEntries[index];
            if (!string.Equals(entry.Category, "Lua.HotReload.Test", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            matches.Add(entry);
        }

        if (matches.Count == 0)
        {
            return "尚未发现 Lua.HotReload.Test 结构化日志。";
        }

        matches.Reverse();

        StringBuilder builder = new();
        for (int index = 0; index < matches.Count; index++)
        {
            FacetEditorLogEntry entry = matches[index];
            builder.Append('[');
            builder.Append(entry.LevelName);
            builder.Append("] ");
            builder.Append(entry.TimestampUtc);
            builder.AppendLine();
            builder.Append(entry.Message);
            builder.AppendLine();

            if (!string.IsNullOrWhiteSpace(entry.PayloadJson))
            {
                builder.Append("payload: ");
                builder.Append(entry.PayloadJson);
                builder.AppendLine();
            }

            if (index < matches.Count - 1)
            {
                builder.AppendLine();
                builder.AppendLine("----------------------------------------");
                builder.AppendLine();
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static string BuildLayoutStatusText(FacetLayoutLabStatus status)
    {
        string successText = status.Success switch
        {
            true => "成功",
            false => "失败",
            _ => "未定",
        };

        return
            $"状态: {status.State}  结果: {successText}\n" +
            $"命令: {status.Command}\n" +
            $"请求: {status.RequestId}\n" +
            $"来源: {status.IssuedBy}\n" +
            $"发起时间: {status.IssuedAtUtc}\n" +
            $"更新时间: {status.UpdatedAtUtc}\n" +
            $"运行时会话: {status.RuntimeSessionId}\n" +
            $"运行时页面: {status.RuntimePageId}\n" +
            $"说明: {status.Message}";
    }

    private static string BuildRuntimeDiagnosticsStatusText(FacetRuntimeDiagnosticsSnapshot snapshot)
    {
        return
            $"会话: {snapshot.RuntimeSessionId}\n" +
            $"更新时间: {snapshot.UpdatedAtUtc}\n" +
            $"当前页面: {snapshot.CurrentPageId}\n" +
            $"返回栈深度: {snapshot.BackStackDepth}\n" +
            $"已注册页面: {snapshot.RegisteredPageCount}\n" +
            $"活动运行时: {snapshot.ActiveRuntimeCount}\n" +
            $"Projection: {snapshot.ProjectionCount}\n" +
            $"Lua 脚本: {snapshot.LuaRegisteredScriptCount}\n" +
            $"红点路径: {snapshot.RedDotRegisteredPathCount}\n" +
            $"校验结果: {snapshot.ValidationResultCount} 通过 {snapshot.ValidationPassedCount} / 警告 {snapshot.ValidationWarningCount} / 失败 {snapshot.ValidationFailedCount}";
    }

    private static string BuildRuntimeDiagnosticsSnapshotText(FacetRuntimeDiagnosticsSnapshot snapshot)
    {
        StringBuilder builder = new();

        builder.AppendLine("页面注册表");
        builder.AppendLine("----------------------------------------");
        AppendRegisteredPages(builder, snapshot.RegisteredPages);
        builder.AppendLine();

        builder.AppendLine("活动运行时");
        builder.AppendLine("----------------------------------------");
        AppendActiveRuntimes(builder, snapshot.ActiveRuntimes);
        builder.AppendLine();

        builder.AppendLine($"Projection 键 ({snapshot.ProjectionCount})");
        builder.AppendLine("----------------------------------------");
        AppendStringList(builder, snapshot.ProjectionKeys);
        builder.AppendLine();

        builder.AppendLine($"Lua 脚本 ({snapshot.LuaRegisteredScriptCount})");
        builder.AppendLine("----------------------------------------");
        AppendStringList(builder, snapshot.LuaRegisteredScripts);
        builder.AppendLine();

        builder.AppendLine($"红点路径 ({snapshot.RedDotRegisteredPathCount})");
        builder.AppendLine("----------------------------------------");
        AppendStringList(builder, snapshot.RedDotPaths);

        return builder.ToString().TrimEnd();
    }

    private static string BuildRuntimeDiagnosticsValidationText(FacetRuntimeDiagnosticsSnapshot snapshot)
    {
        if (snapshot.ValidationResults.Count == 0)
        {
            return "当前没有运行时校验结果。";
        }

        StringBuilder builder = new();
        foreach (FacetRuntimeValidationResultSnapshot result in snapshot.ValidationResults)
        {
            builder.Append('[');
            builder.Append(result.Status);
            builder.Append("] ");
            builder.Append(result.Subject);
            builder.Append(" / ");
            builder.Append(result.RuleId);
            builder.AppendLine();
            builder.Append("severity: ");
            builder.Append(result.Severity);
            builder.Append(" | ");
            builder.Append(result.Message);
            builder.AppendLine();
            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    private string BuildRuntimeDiagnosticsObserverSummaryText(
        FacetRuntimeDiagnosticsSnapshot snapshot,
        List<FacetEditorLogEntry> allEntries)
    {
        string focus = _runtimeDiagnosticsFocusFilter.Text.Trim();
        int channel = (int)_runtimeDiagnosticsObserverChannelFilter.GetSelectedId();
        int matchCount = CountObserverEntries(allEntries, focus, channel);
        string focusText = string.IsNullOrWhiteSpace(focus) ? "未设置" : focus;
        string channelText = _runtimeDiagnosticsObserverChannelFilter.GetItemText(_runtimeDiagnosticsObserverChannelFilter.Selected);

        return
            $"当前主题: {channelText}\n" +
            $"当前焦点: {focusText}\n" +
            $"匹配日志: {matchCount} 条\n" +
            $"当前页: {snapshot.CurrentPageId}";
    }

    private string BuildRuntimeDiagnosticsObserverText(
        List<FacetEditorLogEntry> allEntries,
        FacetRuntimeDiagnosticsSnapshot? snapshot)
    {
        if (allEntries.Count == 0)
        {
            return "当前还没有可供观察的结构化日志。";
        }

        string focus = _runtimeDiagnosticsFocusFilter.Text.Trim();
        int channel = (int)_runtimeDiagnosticsObserverChannelFilter.GetSelectedId();
        List<FacetEditorLogEntry> matches = new();

        for (int index = allEntries.Count - 1; index >= 0 && matches.Count < MaxObserverEntries; index--)
        {
            FacetEditorLogEntry entry = allEntries[index];
            if (!MatchesObserverChannel(entry, channel))
            {
                continue;
            }

            if (!MatchesObserverFocus(entry, focus))
            {
                continue;
            }

            matches.Add(entry);
        }

        if (matches.Count == 0)
        {
            string fallbackFocus = string.IsNullOrWhiteSpace(focus) ? "当前筛选" : $"焦点 `{focus}`";
            return $"{fallbackFocus} 下没有匹配的深度观察日志。";
        }

        matches.Reverse();

        StringBuilder builder = new();
        if (snapshot != null && !string.IsNullOrWhiteSpace(snapshot.CurrentPageId))
        {
            builder.Append("当前页: ");
            builder.Append(snapshot.CurrentPageId);
            builder.AppendLine();
            builder.AppendLine();
        }

        for (int index = 0; index < matches.Count; index++)
        {
            FacetEditorLogEntry entry = matches[index];
            builder.Append('[');
            builder.Append(entry.LevelName);
            builder.Append("] ");
            builder.Append(entry.Category);
            builder.Append(" @ ");
            builder.Append(entry.TimestampUtc);
            builder.AppendLine();
            builder.Append(entry.Message);
            builder.AppendLine();

            if (!string.IsNullOrWhiteSpace(entry.PayloadJson))
            {
                builder.Append("payload: ");
                builder.Append(entry.PayloadJson);
                builder.AppendLine();
            }

            if (index < matches.Count - 1)
            {
                builder.AppendLine();
                builder.AppendLine("----------------------------------------");
                builder.AppendLine();
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static string BuildLayoutEvidenceText(List<FacetEditorLogEntry> allEntries)
    {
        List<FacetEditorLogEntry> matches = new();
        for (int index = allEntries.Count - 1; index >= 0 && matches.Count < 6; index--)
        {
            FacetEditorLogEntry entry = allEntries[index];
            if (!string.Equals(entry.Category, "UI.Layout", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(entry.Category, "UI.Page", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!ContainsLayoutLabMarker(entry.Message) &&
                !ContainsLayoutLabMarker(entry.PayloadJson))
            {
                continue;
            }

            matches.Add(entry);
        }

        if (matches.Count == 0)
        {
            return "尚未发现与阶段 11 布局实验室相关的结构化日志。";
        }

        matches.Reverse();

        StringBuilder builder = new();
        for (int index = 0; index < matches.Count; index++)
        {
            FacetEditorLogEntry entry = matches[index];
            builder.Append('[');
            builder.Append(entry.LevelName);
            builder.Append("] ");
            builder.Append(entry.TimestampUtc);
            builder.AppendLine();
            builder.Append(entry.Message);
            builder.AppendLine();

            if (!string.IsNullOrWhiteSpace(entry.PayloadJson))
            {
                builder.Append("payload: ");
                builder.Append(entry.PayloadJson);
                builder.AppendLine();
            }

            if (index < matches.Count - 1)
            {
                builder.AppendLine();
                builder.AppendLine("----------------------------------------");
                builder.AppendLine();
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static void AppendRegisteredPages(StringBuilder builder, List<FacetRuntimeRegisteredPageSnapshot> pages)
    {
        if (pages.Count == 0)
        {
            builder.AppendLine("无");
            return;
        }

        foreach (FacetRuntimeRegisteredPageSnapshot page in pages)
        {
            builder.Append(page.PageId);
            builder.Append(" | ");
            builder.Append(page.LayoutType);
            builder.Append(" | ");
            builder.Append(page.CachePolicy);
            builder.Append(" | layer=");
            builder.Append(page.Layer);
            builder.Append(" | layout=");
            builder.Append(page.LayoutPath);

            if (!string.IsNullOrWhiteSpace(page.ControllerScript))
            {
                builder.Append(" | lua=");
                builder.Append(page.ControllerScript);
            }

            builder.AppendLine();
        }
    }

    private static void AppendActiveRuntimes(StringBuilder builder, List<FacetRuntimePageRuntimeSnapshot> runtimes)
    {
        if (runtimes.Count == 0)
        {
            builder.AppendLine("无");
            return;
        }

        foreach (FacetRuntimePageRuntimeSnapshot runtime in runtimes)
        {
            builder.Append(runtime.IsCurrentPage ? "* " : "- ");
            builder.Append(runtime.PageId);
            builder.Append(" | state=");
            builder.Append(runtime.State);
            builder.Append(" | layout=");
            builder.Append(runtime.LayoutType);
            builder.Append(" | lua=");
            builder.Append(runtime.HasLuaController ? "yes" : "no");

            if (!string.IsNullOrWhiteSpace(runtime.ControllerScript))
            {
                builder.Append(" | script=");
                builder.Append(runtime.ControllerScript);
            }

            if (!string.IsNullOrWhiteSpace(runtime.LuaControllerVersionToken))
            {
                builder.Append(" | version=");
                builder.Append(runtime.LuaControllerVersionToken);
            }

            builder.AppendLine();
            builder.Append("  path=");
            builder.Append(runtime.PageRootPath);
            builder.AppendLine();

            if (runtime.BindingScope != null)
            {
                builder.Append("  binding=");
                builder.Append(runtime.BindingScope.ScopeId);
                builder.Append(" | count=");
                builder.Append(runtime.BindingScope.BindingCount);
                builder.Append(" | refresh=");
                builder.Append(runtime.BindingScope.RefreshCount);

                if (!string.IsNullOrWhiteSpace(runtime.BindingScope.LastRefreshReason))
                {
                    builder.Append(" | reason=");
                    builder.Append(runtime.BindingScope.LastRefreshReason);
                }

                builder.AppendLine();
            }
        }
    }

    private static void AppendStringList(StringBuilder builder, List<string> items)
    {
        if (items.Count == 0)
        {
            builder.AppendLine("无");
            return;
        }

        foreach (string item in items)
        {
            builder.Append("- ");
            builder.Append(item);
            builder.AppendLine();
        }
    }

    private int CountObserverEntries(List<FacetEditorLogEntry> allEntries, string focus, int channel)
    {
        int count = 0;
        foreach (FacetEditorLogEntry entry in allEntries)
        {
            if (MatchesObserverChannel(entry, channel) &&
                MatchesObserverFocus(entry, focus))
            {
                count++;
            }
        }

        return count;
    }

    private static bool MatchesObserverChannel(FacetEditorLogEntry entry, int channel)
    {
        return channel switch
        {
            0 => IsObserverCategory(entry.Category),
            1 => string.Equals(entry.Category, "UI.Page.Lifecycle", StringComparison.OrdinalIgnoreCase),
            2 => entry.Category.StartsWith("Projection", StringComparison.OrdinalIgnoreCase),
            3 => entry.Category.StartsWith("RedDot", StringComparison.OrdinalIgnoreCase),
            4 => entry.Category.StartsWith("UI.Binding", StringComparison.OrdinalIgnoreCase),
            5 => entry.Category.StartsWith("Tooling", StringComparison.OrdinalIgnoreCase) ||
                 entry.Category.StartsWith("Lua.HotReload", StringComparison.OrdinalIgnoreCase),
            _ => false,
        };
    }

    private static bool IsObserverCategory(string category)
    {
        return string.Equals(category, "UI.Page.Lifecycle", StringComparison.OrdinalIgnoreCase) ||
               category.StartsWith("Projection", StringComparison.OrdinalIgnoreCase) ||
               category.StartsWith("RedDot", StringComparison.OrdinalIgnoreCase) ||
               category.StartsWith("UI.Binding", StringComparison.OrdinalIgnoreCase) ||
               category.StartsWith("Tooling", StringComparison.OrdinalIgnoreCase) ||
               category.StartsWith("Lua.HotReload", StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesObserverFocus(FacetEditorLogEntry entry, string focus)
    {
        if (string.IsNullOrWhiteSpace(focus))
        {
            return true;
        }

        return entry.Category.Contains(focus, StringComparison.OrdinalIgnoreCase) ||
               entry.Message.Contains(focus, StringComparison.OrdinalIgnoreCase) ||
               entry.PayloadJson.Contains(focus, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsLayoutLabMarker(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Contains(UIPageIds.GeneratedLayoutLab, StringComparison.OrdinalIgnoreCase) ||
               value.Contains(UIPageIds.TemplateLayoutLab, StringComparison.OrdinalIgnoreCase) ||
               value.Contains("布局实验室", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("阶段 11", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetLevelColorHex(int levelRank)
    {
        return levelRank switch
        {
            0 => "#8b9bb4",
            1 => "#6ea8ff",
            2 => "#7ce0b8",
            3 => "#f3c96b",
            4 => "#ff8d8d",
            _ => "#c8d2e4",
        };
    }

    private static string EscapeBbcode(string value)
    {
        return value
            .Replace("[", "[lb]")
            .Replace("]", "[rb]");
    }

    private static bool TryParseEntry(string jsonLine, out FacetEditorLogEntry? entry)
    {
        entry = null;

        try
        {
            using JsonDocument document = JsonDocument.Parse(jsonLine);
            JsonElement root = document.RootElement;

            string sessionId = root.TryGetProperty("SessionId", out JsonElement sessionElement) ? sessionElement.GetString() ?? string.Empty : string.Empty;
            long eventId = root.TryGetProperty("EventId", out JsonElement eventElement) ? eventElement.GetInt64() : 0;
            string timestampUtc = root.TryGetProperty("TimestampUtc", out JsonElement timeElement) ? timeElement.GetString() ?? string.Empty : string.Empty;
            string category = root.TryGetProperty("Category", out JsonElement categoryElement) ? categoryElement.GetString() ?? string.Empty : string.Empty;
            string message = root.TryGetProperty("Message", out JsonElement messageElement) ? messageElement.GetString() ?? string.Empty : string.Empty;
            int levelRank = root.TryGetProperty("Level", out JsonElement levelElement) ? GetLevelRank(levelElement) : -1;
            string levelName = GetLevelName(levelRank);
            string payloadJson = root.TryGetProperty("Payload", out JsonElement payloadElement) && payloadElement.ValueKind != JsonValueKind.Null
                ? payloadElement.GetRawText()
                : string.Empty;

            entry = new FacetEditorLogEntry(sessionId, eventId, timestampUtc, category, message, levelRank, levelName, payloadJson);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static int GetLevelRank(JsonElement levelElement)
    {
        return levelElement.ValueKind switch
        {
            JsonValueKind.Number => levelElement.GetInt32(),
            JsonValueKind.String when int.TryParse(levelElement.GetString(), out int numericLevel) => numericLevel,
            JsonValueKind.String => GetLevelRank(levelElement.GetString() ?? string.Empty),
            _ => -1,
        };
    }

    private static int GetLevelRank(string levelName)
    {
        return levelName switch
        {
            "Trace" => 0,
            "Debug" => 1,
            "Info" => 2,
            "Warning" => 3,
            "Error" => 4,
            _ => -1,
        };
    }

    private static string GetLevelName(int levelRank)
    {
        return levelRank switch
        {
            0 => "Trace",
            1 => "Debug",
            2 => "Info",
            3 => "Warning",
            4 => "Error",
            _ => levelRank.ToString(),
        };
    }

    private static string GetShortSessionId(string sessionId)
    {
        if (sessionId.Length <= 8)
        {
            return sessionId;
        }

        return sessionId[..8];
    }

    /// <summary>
    /// 只保留宽度分配，不再使用依赖高度猜测的布局计算。
    /// </summary>
    private void UpdateResponsiveLayout()
    {
        float viewportWidth = Size.X;
        if (viewportWidth <= 0.0f)
        {
            return;
        }

        _logMetricsGrid.Columns = viewportWidth switch
        {
            >= 1440.0f => 4,
            >= 980.0f => 2,
            _ => 1,
        };

        _logSidebarCard.CustomMinimumSize = new Vector2(
            viewportWidth >= 1280.0f ? 300.0f : 260.0f,
            0.0f);

        _logSplit.SplitOffsets = new[]
        {
            viewportWidth switch
            {
                >= 1440.0f => 320,
                >= 1120.0f => 290,
                _ => 250,
            },
        };
    }

    private TNode ResolveRequiredNode<TNode>(string path) where TNode : Node
    {
        TNode? node = GetNodeOrNull<TNode>(path);
        if (node == null)
        {
            throw new InvalidOperationException($"FacetMainScreen required node missing: {path}");
        }

        return node;
    }

    private sealed record FacetEditorLogEntry(
        string SessionId,
        long EventId,
        string TimestampUtc,
        string Category,
        string Message,
        int LevelRank,
        string LevelName,
        string PayloadJson);
}
