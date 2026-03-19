#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using Godot;

[Tool]
public partial class FacetMainScreen : Control
{
    private const int MaxVisibleEntries = 150;
    private const int HistoryLimit = 10;

    private Label _pathLabel = null!;
    private Label _summaryLabel = null!;
    private Label _totalValueLabel = null!;
    private Label _filteredValueLabel = null!;
    private Label _sessionsValueLabel = null!;
    private Label _categoriesValueLabel = null!;
    private Label _retentionLabel = null!;
    private LineEdit _sessionFilter = null!;
    private LineEdit _categoryFilter = null!;
    private OptionButton _levelFilter = null!;
    private CheckButton _autoRefreshToggle = null!;
    private RichTextLabel _entriesLabel = null!;
    private Timer _refreshTimer = null!;
    private TabContainer _workspaceTabs = null!;
    private Button _refreshButton = null!;
    private Button _openLogButton = null!;
    private VBoxContainer _root = null!;
    private GridContainer _metricsGrid = null!;
    private HSplitContainer _contentSplit = null!;
    private PanelContainer _sidebarCard = null!;
    private ScrollContainer _sidebarScroll = null!;
    private bool _isUiReady;

    public override void _Ready()
    {
        try
        {
            FacetEditorDiagnostics.Info("MainScreen", $"Ready parent={GetParent()?.GetType().Name ?? "<null>"}");
            Name = "FacetMainScreen";
            SizeFlagsHorizontal = SizeFlags.ExpandFill;
            SizeFlagsVertical = SizeFlags.ExpandFill;
            CustomMinimumSize = new Vector2(960.0f, 640.0f);
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            BuildUi();
            _isUiReady = true;
            EnsureViewportLayout();
            RefreshNow();
            UpdateResponsiveLayout();
            CallDeferred(nameof(EnsureViewportLayout));
            CallDeferred(nameof(UpdateResponsiveLayout));
        }
        catch (Exception exception)
        {
            FacetEditorDiagnostics.Error("MainScreen", "Ready failed.", exception);
            throw;
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
            _retentionLabel.Text = $"保留策略 / Retention: 当前活动日志 + 最近 {HistoryLimit} 次历史会话";

            if (!File.Exists(logPath))
            {
                UpdateMetrics(0, 0, 0, 0);
                _summaryLabel.Text = "结构化日志已启用 / Structured logging is ready, but no active log file exists yet.";
                _entriesLabel.Text = "运行一次主场景后，这里会显示当前会话的 facet-structured.jsonl。\nRun the main scene once to populate the active structured log.";
                return;
            }

            List<FacetEditorLogEntry> allEntries = LoadEntries(logPath);
            List<FacetEditorLogEntry> filteredEntries = FilterEntries(allEntries);
            RenderEntries(allEntries, filteredEntries);
        }
        catch (Exception exception)
        {
            FacetEditorDiagnostics.Error("MainScreen", "RefreshNow failed.", exception);
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
            EnsureViewportLayout();
            UpdateResponsiveLayout();
        }
    }

    public void EnsureViewportLayout()
    {
        try
        {
            if (GetParent() is not Control parentControl)
            {
                return;
            }

            Vector2 parentSize = parentControl.Size;
            if (parentSize.X <= 0.0f || parentSize.Y <= 0.0f)
            {
                return;
            }

            CustomMinimumSize = parentSize;
            Position = Vector2.Zero;
            Size = parentSize;
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        }
        catch (Exception exception)
        {
            FacetEditorDiagnostics.Error("MainScreen", "EnsureViewportLayout failed.", exception);
        }
    }

    public void LogLayoutSnapshot()
    {
        try
        {
            FacetEditorDiagnostics.Info(
                "MainScreen",
                $"LayoutSnapshot visible={Visible} size={Size} min={CustomMinimumSize} parentSize={(GetParent() as Control)?.Size} children={GetChildCount()} rootMin={_root?.CustomMinimumSize} rootSize={_root?.Size}");
        }
        catch (Exception exception)
        {
            FacetEditorDiagnostics.Error("MainScreen", "LogLayoutSnapshot failed.", exception);
        }
    }

    private void BuildUi()
    {
        AddThemeStyleboxOverride("panel", CreateScreenPanel());

        MarginContainer chrome = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        chrome.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        chrome.AddThemeConstantOverride("margin_left", 24);
        chrome.AddThemeConstantOverride("margin_top", 24);
        chrome.AddThemeConstantOverride("margin_right", 24);
        chrome.AddThemeConstantOverride("margin_bottom", 24);
        AddChild(chrome);

        _root = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ThemeTypeVariation = "MarginContainer",
        };
        chrome.AddChild(_root);

        _root.AddChild(BuildHeaderCard());
        _root.AddChild(BuildMetricsRow());

        _contentSplit = new HSplitContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        _contentSplit.SplitOffsets = new[] { 360 };
        _root.AddChild(_contentSplit);

        _contentSplit.AddChild(BuildSidebar());
        _contentSplit.AddChild(BuildLogSurface());

        _refreshTimer = new Timer
        {
            WaitTime = 1.0,
            OneShot = false,
            Autostart = true,
        };
        _refreshTimer.Timeout += OnRefreshTimerTimeout;
        AddChild(_refreshTimer);
    }

    private Control BuildHeaderCard()
    {
        PanelContainer card = CreateCardPanel();

        MarginContainer padding = CreateInnerMargin(20, 18, 20, 18);
        card.AddChild(padding);

        VBoxContainer content = CreateVBox(8);
        padding.AddChild(content);

        Label eyebrowLabel = new()
        {
            Text = "客户端工具 / Client Tooling",
        };
        eyebrowLabel.AddThemeColorOverride("font_color", new Color("6ea8ff"));
        content.AddChild(eyebrowLabel);

        Label titleLabel = new()
        {
            Text = "Facet 工作台 / Facet Workspace",
        };
        titleLabel.AddThemeFontSizeOverride("font_size", 28);
        titleLabel.AddThemeColorOverride("font_color", new Color("f3f6fb"));
        content.AddChild(titleLabel);

        Label introLabel = new()
        {
            Text = "用于查看客户端结构化诊断信息，并为后续 UI 注册、红点树和页面调试提供统一入口。\nA unified workspace for structured diagnostics today and future UI tooling tomorrow.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        introLabel.AddThemeColorOverride("font_color", new Color("a8b3c7"));
        introLabel.AddThemeFontSizeOverride("font_size", 15);
        content.AddChild(introLabel);

        return card;
    }

    private Control BuildMetricsRow()
    {
        _metricsGrid = new GridContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Columns = 4,
        };
        _metricsGrid.AddThemeConstantOverride("h_separation", 12);
        _metricsGrid.AddThemeConstantOverride("v_separation", 12);

        (_totalValueLabel, PanelContainer totalCard) = CreateMetricCard("总日志 / Total Entries", "0");
        (_filteredValueLabel, PanelContainer filteredCard) = CreateMetricCard("过滤结果 / Filtered View", "0");
        (_sessionsValueLabel, PanelContainer sessionsCard) = CreateMetricCard("会话数 / Sessions", "0");
        (_categoriesValueLabel, PanelContainer categoriesCard) = CreateMetricCard("分类数 / Categories", "0");

        _metricsGrid.AddChild(totalCard);
        _metricsGrid.AddChild(filteredCard);
        _metricsGrid.AddChild(sessionsCard);
        _metricsGrid.AddChild(categoriesCard);

        return _metricsGrid;
    }

    private Control BuildSidebar()
    {
        _sidebarCard = CreateCardPanel();
        _sidebarCard.CustomMinimumSize = new Vector2(320.0f, 0.0f);

        MarginContainer padding = CreateInnerMargin(18, 18, 18, 18);
        _sidebarCard.AddChild(padding);

        _sidebarScroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        padding.AddChild(_sidebarScroll);

        VBoxContainer content = CreateVBox(16);
        content.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _sidebarScroll.AddChild(content);

        content.AddChild(CreateSectionTitle("控制台 / Control Surface", "刷新、文件访问与查询过滤。\nRefresh controls, file access, and query filters."));

        HBoxContainer actionRow = new();
        actionRow.AddThemeConstantOverride("separation", 10);
        content.AddChild(actionRow);

        _refreshButton = new Button
        {
            Text = "刷新 / Refresh",
            FocusMode = FocusModeEnum.None,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _refreshButton.Pressed += RefreshNow;
        actionRow.AddChild(_refreshButton);

        _openLogButton = new Button
        {
            Text = "打开日志目录 / Open Logs",
            FocusMode = FocusModeEnum.None,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _openLogButton.Pressed += OnOpenUserLogsPressed;
        actionRow.AddChild(_openLogButton);

        _autoRefreshToggle = new CheckButton
        {
            Text = "自动刷新 / Auto Refresh",
            ButtonPressed = true,
            FocusMode = FocusModeEnum.None,
        };
        content.AddChild(_autoRefreshToggle);

        content.AddChild(CreateDivider());
        content.AddChild(CreateSectionTitle("日志源 / Log Source", "当前活动结构化日志文件路径。\nCurrent active structured log path."));

        _pathLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _pathLabel.AddThemeColorOverride("font_color", new Color("9aa7bc"));
        content.AddChild(_pathLabel);

        _retentionLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _retentionLabel.AddThemeColorOverride("font_color", new Color("8dc9a8"));
        Control retentionCard = CreateRetentionStatusCard();
        content.AddChild(retentionCard);

        content.AddChild(CreateDivider());
        content.AddChild(CreateSectionTitle("筛选器 / Filters", "按会话、分类和级别缩小范围。\nNarrow the stream by session, category, and level."));

        content.AddChild(CreateLabeledField("会话前缀 / Session Prefix", out _sessionFilter, "例如 / e.g. abcd1234"));
        _sessionFilter.TextChanged += OnFilterChanged;

        content.AddChild(CreateLabeledField("分类前缀 / Category Prefix", out _categoryFilter, "例如 / e.g. Client.WindowManager"));
        _categoryFilter.TextChanged += OnFilterChanged;

        VBoxContainer levelField = CreateVBox(6);
        Label levelLabel = new() { Text = "最小级别 / Minimum Level" };
        levelLabel.AddThemeColorOverride("font_color", new Color("c9d2e3"));
        levelField.AddChild(levelLabel);
        _levelFilter = new OptionButton
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        AddLevelItem("Trace / 跟踪", 0);
        AddLevelItem("Debug / 调试", 1);
        AddLevelItem("Info / 信息", 2);
        AddLevelItem("Warning / 警告", 3);
        AddLevelItem("Error / 错误", 4);
        _levelFilter.ItemSelected += OnLevelSelected;
        levelField.AddChild(_levelFilter);
        content.AddChild(levelField);

        content.AddChild(CreateDivider());
        content.AddChild(CreateSectionTitle("说明 / Notes", "当前页面聚焦诊断能力，同时约束日志存储规模。\nThis workspace focuses on diagnostics while keeping storage bounded."));

        Label notesLabel = new()
        {
            Text = "结构化日志与编辑器日志都会保留一个当前活动文件，并自动归档历史，仅保留最近 10 次。\nBoth runtime and editor logs keep one active file plus the latest 10 archived sessions.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        notesLabel.AddThemeColorOverride("font_color", new Color("9aa7bc"));
        content.AddChild(notesLabel);

        return _sidebarCard;
    }

    private Control BuildLogSurface()
    {
        PanelContainer surface = CreateCardPanel();

        MarginContainer padding = CreateInnerMargin(18, 18, 18, 18);
        surface.AddChild(padding);

        VBoxContainer content = CreateVBox(12);
        padding.AddChild(content);

        HBoxContainer headerRow = new();
        headerRow.AddThemeConstantOverride("separation", 10);
        content.AddChild(headerRow);

        VBoxContainer titleColumn = CreateVBox(4);
        titleColumn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        headerRow.AddChild(titleColumn);

        Label titleLabel = new()
        {
            Text = "结构化日志流 / Structured Log Stream",
        };
        titleLabel.AddThemeFontSizeOverride("font_size", 18);
        titleLabel.AddThemeColorOverride("font_color", new Color("eef2fa"));
        titleColumn.AddChild(titleLabel);

        _summaryLabel = new Label();
        _summaryLabel.AddThemeColorOverride("font_color", new Color("9aa7bc"));
        titleColumn.AddChild(_summaryLabel);

        Label liveBadge = new()
        {
            Text = "实时 / LIVE",
        };
        liveBadge.AddThemeColorOverride("font_color", new Color("7ce0b8"));
        liveBadge.AddThemeFontSizeOverride("font_size", 13);
        headerRow.AddChild(liveBadge);

        _workspaceTabs = new TabContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        content.AddChild(_workspaceTabs);

        _workspaceTabs.AddChild(BuildLogTab());
        _workspaceTabs.AddChild(BuildFutureTab());

        return surface;
    }

    private Control BuildLogTab()
    {
        VBoxContainer logTab = CreateVBox(0);
        logTab.Name = "日志 Log";
        logTab.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        logTab.SizeFlagsVertical = SizeFlags.ExpandFill;

        _entriesLabel = new RichTextLabel
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ScrollActive = true,
            SelectionEnabled = true,
            FitContent = false,
            BbcodeEnabled = true,
        };
        _entriesLabel.AddThemeStyleboxOverride("normal", CreateLogSurfaceStyle());
        _entriesLabel.AddThemeColorOverride("default_color", new Color("d7deea"));
        logTab.AddChild(_entriesLabel);

        return logTab;
    }

    private Control BuildFutureTab()
    {
        PanelContainer futureTab = CreateCardPanel();
        futureTab.Name = "扩展 Reserved";

        MarginContainer padding = CreateInnerMargin(20, 18, 20, 18);
        futureTab.AddChild(padding);

        VBoxContainer content = CreateVBox(10);
        padding.AddChild(content);

        content.AddChild(CreateSectionTitle("预留模块 / Reserved Modules", "Facet 后续会继续扩展更多客户端工具，不再与日志堆在同一面板。\nFuture client tools will land here without crowding the log view."));

        Label placeholder = new()
        {
            Text = "计划中的子模块 / Planned modules:\n• 页面注册 / Page Registry\n• 红点树 / Red Dot Tree\n• 资源诊断 / Asset Diagnostics\n• 热更新状态 / Hot Reload Status",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        placeholder.AddThemeColorOverride("font_color", new Color("a7b4c8"));
        content.AddChild(placeholder);

        return futureTab;
    }

    private static VBoxContainer CreateVBox(int separation)
    {
        VBoxContainer box = new();
        box.AddThemeConstantOverride("separation", separation);
        return box;
    }

    private static MarginContainer CreateInnerMargin(int left, int top, int right, int bottom)
    {
        MarginContainer margin = new();
        margin.AddThemeConstantOverride("margin_left", left);
        margin.AddThemeConstantOverride("margin_top", top);
        margin.AddThemeConstantOverride("margin_right", right);
        margin.AddThemeConstantOverride("margin_bottom", bottom);
        return margin;
    }

    private static PanelContainer CreateCardPanel()
    {
        PanelContainer panel = new();
        panel.AddThemeStyleboxOverride("panel", CreateCardStyle());
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        panel.SizeFlagsVertical = SizeFlags.ExpandFill;
        return panel;
    }

    private Control CreateRetentionStatusCard()
    {
        PanelContainer card = new();
        card.AddThemeStyleboxOverride("panel", CreateStatusCardStyle());

        MarginContainer padding = CreateInnerMargin(12, 12, 12, 12);
        card.AddChild(padding);

        VBoxContainer content = CreateVBox(4);
        padding.AddChild(content);

        Label titleLabel = new()
        {
            Text = "保留策略 / Retention Policy",
        };
        titleLabel.AddThemeColorOverride("font_color", new Color("dff6e8"));
        titleLabel.AddThemeFontSizeOverride("font_size", 15);
        content.AddChild(titleLabel);

        content.AddChild(_retentionLabel);

        Label detailLabel = new()
        {
            Text = "避免 C 盘日志无限增长。\nPrevent unbounded growth under C drive user logs.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        detailLabel.AddThemeColorOverride("font_color", new Color("b4d8c1"));
        content.AddChild(detailLabel);

        return card;
    }

    private static StyleBoxFlat CreateScreenPanel()
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
            CornerRadiusTopLeft = 12,
            CornerRadiusTopRight = 12,
            CornerRadiusBottomRight = 12,
            CornerRadiusBottomLeft = 12,
            ContentMarginLeft = 0,
            ContentMarginTop = 0,
            ContentMarginRight = 0,
            ContentMarginBottom = 0,
        };
    }

    private static StyleBoxFlat CreateMetricValueStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = new Color("202a38"),
            BorderColor = new Color("324154"),
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
            CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10,
            CornerRadiusBottomRight = 10,
            CornerRadiusBottomLeft = 10,
            ContentMarginLeft = 14,
            ContentMarginTop = 14,
            ContentMarginRight = 14,
            ContentMarginBottom = 14,
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

    private static Control CreateDivider()
    {
        HSeparator separator = new();
        separator.AddThemeColorOverride("separator", new Color("2d3747"));
        return separator;
    }

    private static Control CreateSectionTitle(string title, string description)
    {
        VBoxContainer section = CreateVBox(4);

        Label titleLabel = new()
        {
            Text = title,
        };
        titleLabel.AddThemeFontSizeOverride("font_size", 17);
        titleLabel.AddThemeColorOverride("font_color", new Color("eef2fa"));
        section.AddChild(titleLabel);

        Label descriptionLabel = new()
        {
            Text = description,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        descriptionLabel.AddThemeColorOverride("font_color", new Color("8f9db3"));
        section.AddChild(descriptionLabel);

        return section;
    }

    private static Control CreateLabeledField(string title, out LineEdit field, string placeholder)
    {
        VBoxContainer wrapper = CreateVBox(6);

        Label label = new()
        {
            Text = title,
        };
        label.AddThemeColorOverride("font_color", new Color("c9d2e3"));
        wrapper.AddChild(label);

        field = new LineEdit
        {
            PlaceholderText = placeholder,
            ClearButtonEnabled = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        wrapper.AddChild(field);

        return wrapper;
    }

    private static (Label ValueLabel, PanelContainer Card) CreateMetricCard(string title, string initialValue)
    {
        PanelContainer card = CreateCardPanel();
        card.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        card.SizeFlagsVertical = SizeFlags.ShrinkCenter;

        MarginContainer padding = CreateInnerMargin(16, 14, 16, 14);
        card.AddChild(padding);

        VBoxContainer content = CreateVBox(6);
        padding.AddChild(content);

        Label titleLabel = new()
        {
            Text = title,
        };
        titleLabel.AddThemeColorOverride("font_color", new Color("95a3b8"));
        content.AddChild(titleLabel);

        PanelContainer valuePlate = new();
        valuePlate.AddThemeStyleboxOverride("panel", CreateMetricValueStyle());
        content.AddChild(valuePlate);

        MarginContainer valuePadding = CreateInnerMargin(12, 10, 12, 10);
        valuePlate.AddChild(valuePadding);

        Label valueLabel = new()
        {
            Text = initialValue,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        valueLabel.AddThemeFontSizeOverride("font_size", 24);
        valueLabel.AddThemeColorOverride("font_color", new Color("f3f6fb"));
        valuePadding.AddChild(valueLabel);

        return (valueLabel, card);
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

    private void OnRefreshTimerTimeout()
    {
        if (_autoRefreshToggle.ButtonPressed && IsVisibleInTree())
        {
            RefreshNow();
        }
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
        foreach (string line in File.ReadLines(logPath, Encoding.UTF8))
        {
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

            if (!string.IsNullOrWhiteSpace(sessionPrefix) && !entry.SessionId.StartsWith(sessionPrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(categoryPrefix) && !entry.Category.StartsWith(categoryPrefix, StringComparison.OrdinalIgnoreCase))
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
        _summaryLabel.Text = $"当前显示最近 {Math.Min(filteredEntries.Count, MaxVisibleEntries)} 条，过滤后共 {filteredEntries.Count} 条。 / Showing the latest {Math.Min(filteredEntries.Count, MaxVisibleEntries)} of {filteredEntries.Count} filtered entries.";

        if (filteredEntries.Count == 0)
        {
            _entriesLabel.Text = "当前筛选条件下没有匹配日志。\nNo structured logs match the current filters.";
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

    private void UpdateResponsiveLayout()
    {
        float viewportWidth = Size.X;
        float viewportHeight = Size.Y;
        if (viewportWidth <= 0.0f || viewportHeight <= 0.0f)
        {
            return;
        }

        _metricsGrid.Columns = viewportWidth switch
        {
            >= 1400.0f => 4,
            >= 900.0f => 2,
            _ => 1,
        };

        _sidebarCard.CustomMinimumSize = new Vector2(viewportWidth >= 1100.0f ? 320.0f : 280.0f, 0.0f);
        _contentSplit.SplitOffsets = new[]
        {
            viewportWidth switch
            {
                >= 1500.0f => 380,
                >= 1200.0f => 340,
                _ => 300,
            },
        };

        _root.CustomMinimumSize = Vector2.Zero;

        float sidebarMinHeight = (float)Math.Max(260.0, viewportHeight - 300.0f);
        _sidebarScroll.CustomMinimumSize = new Vector2(0.0f, sidebarMinHeight);

        float logSurfaceMinHeight = (float)Math.Max(320.0, viewportHeight - 320.0f);
        _workspaceTabs.CustomMinimumSize = new Vector2(0.0f, logSurfaceMinHeight);
        _entriesLabel.CustomMinimumSize = new Vector2(0.0f, (float)Math.Max(260.0, logSurfaceMinHeight - 24.0f));
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

