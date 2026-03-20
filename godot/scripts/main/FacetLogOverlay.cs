#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Godot;
using Sideline.Facet.Runtime;

/// <summary>
/// 运行时日志面板控件。
/// 当前不默认接入主场景，保留给未来明确的运行时调试入口使用。
/// </summary>
internal sealed partial class FacetLogOverlay : PanelContainer
{
    /// <summary>
    /// 面板中最多展示的最近日志条数。
    /// </summary>
    private const int MaxVisibleEntries = 24;

    /// <summary>
    /// 过滤与展示区域的关键控件。
    /// </summary>
    private LineEdit _categoryFilter = null!;
    private OptionButton _levelFilter = null!;
    private Label _summaryLabel = null!;
    private RichTextLabel _entriesLabel = null!;

    /// <summary>
    /// 当前绑定的运行时日志器；为空表示面板仅显示占位状态。
    /// </summary>
    private FacetLogger? _logger;

    /// <summary>
    /// 标记面板内容是否需要重绘，避免每帧无差别刷新富文本区域。
    /// </summary>
    private bool _isDirty = true;

    /// <summary>
    /// 初始化日志面板基础布局，但默认保持隐藏。
    /// </summary>
    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        BuildUi();
        Hide();
        RefreshView();
    }

    /// <summary>
    /// 仅在面板可见且存在新日志时刷新视图，避免每帧都重绘富文本内容。
    /// </summary>
    public override void _Process(double delta)
    {
        if (_isDirty && Visible)
        {
            RefreshView();
        }
    }

    /// <summary>
    /// 离开场景树时解除日志事件绑定，防止悬挂引用。
    /// </summary>
    public override void _ExitTree()
    {
        UnbindLogger();
    }

    /// <summary>
    /// 绑定 FacetLogger 并订阅日志事件。重复绑定同一实例时只触发一次重绘。
    /// </summary>
    public void BindLogger(FacetLogger logger)
    {
        if (ReferenceEquals(_logger, logger))
        {
            _isDirty = true;
            RefreshView();
            return;
        }

        UnbindLogger();
        _logger = logger;
        _logger.EntryLogged += OnEntryLogged;
        _isDirty = true;
        RefreshView();
    }

    /// <summary>
    /// 构建运行时日志面板布局，包括标题、过滤器与日志正文区域。
    /// </summary>
    private void BuildUi()
    {
        Name = "FacetLogOverlay";
        AnchorLeft = 1.0f;
        AnchorRight = 1.0f;
        AnchorTop = 0.0f;
        AnchorBottom = 0.0f;
        OffsetLeft = -700.0f;
        OffsetRight = -20.0f;
        OffsetTop = 20.0f;
        OffsetBottom = 420.0f;
        CustomMinimumSize = new Vector2(680.0f, 400.0f);

        StyleBoxFlat style = new()
        {
            BgColor = new Color(0.08f, 0.09f, 0.11f, 0.92f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            BorderColor = new Color(0.33f, 0.37f, 0.43f, 0.9f),
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomRight = 6,
            CornerRadiusBottomLeft = 6,
            ContentMarginLeft = 10,
            ContentMarginTop = 10,
            ContentMarginRight = 10,
            ContentMarginBottom = 10,
        };
        AddThemeStyleboxOverride("panel", style);

        VBoxContainer root = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        AddChild(root);

        HBoxContainer header = new();
        root.AddChild(header);

        Label titleLabel = new()
        {
            Text = "Facet Runtime Log Panel",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        header.AddChild(titleLabel);

        _summaryLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        header.AddChild(_summaryLabel);

        HBoxContainer filterRow = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        root.AddChild(filterRow);

        Label categoryLabel = new() { Text = "Category" };
        filterRow.AddChild(categoryLabel);

        _categoryFilter = new LineEdit
        {
            PlaceholderText = "例如 Client.WindowManager",
            ClearButtonEnabled = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _categoryFilter.TextChanged += OnCategoryFilterChanged;
        filterRow.AddChild(_categoryFilter);

        Label levelLabel = new() { Text = "Level" };
        filterRow.AddChild(levelLabel);

        _levelFilter = new OptionButton();
        AddLevelItem(FacetLogLevel.Trace);
        AddLevelItem(FacetLogLevel.Debug);
        AddLevelItem(FacetLogLevel.Info);
        AddLevelItem(FacetLogLevel.Warning);
        AddLevelItem(FacetLogLevel.Error);
        _levelFilter.ItemSelected += OnLevelFilterChanged;
        filterRow.AddChild(_levelFilter);

        _entriesLabel = new RichTextLabel
        {
            FitContent = false,
            ScrollActive = true,
            SelectionEnabled = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        root.AddChild(_entriesLabel);
    }

    private void AddLevelItem(FacetLogLevel level)
    {
        _levelFilter.AddItem(level.ToString(), (int)level);
    }

    /// <summary>
    /// 收到新日志后仅打脏标记，由下一帧统一刷新界面。
    /// </summary>
    private void OnEntryLogged(FacetLogEntry entry)
    {
        _isDirty = true;
    }

    private void OnCategoryFilterChanged(string newText)
    {
        _isDirty = true;
        RefreshView();
    }

    private void OnLevelFilterChanged(long index)
    {
        _isDirty = true;
        RefreshView();
    }

    /// <summary>
    /// 根据当前过滤条件重建可见日志文本，并刷新摘要统计。
    /// </summary>
    private void RefreshView()
    {
        _isDirty = false;

        if (_entriesLabel == null || _summaryLabel == null)
        {
            return;
        }

        if (_logger == null)
        {
            _summaryLabel.Text = "logger unavailable";
            _entriesLabel.Text = "Facet logger 尚未绑定。";
            return;
        }

        IReadOnlyList<FacetLogEntry> bufferedEntries = _logger.GetBufferedEntries();
        string categoryPrefix = _categoryFilter.Text.Trim();
        FacetLogLevel minimumLevel = (FacetLogLevel)_levelFilter.GetSelectedId();

        List<FacetLogEntry> filteredEntries = new();
        foreach (FacetLogEntry entry in bufferedEntries)
        {
            if (entry.Level < minimumLevel)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(categoryPrefix) &&
                !entry.Category.StartsWith(categoryPrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            filteredEntries.Add(entry);
        }

        int startIndex = Math.Max(0, filteredEntries.Count - MaxVisibleEntries);
        _summaryLabel.Text = $"session={GetShortSessionId(_logger.SessionId)}  visible={filteredEntries.Count}/{bufferedEntries.Count}";
        _entriesLabel.Text = BuildEntriesText(filteredEntries, startIndex);
    }

    /// <summary>
    /// 把过滤后的日志条目拼接成最终显示文本。
    /// 这里选择纯文本方式，优先保证运行时调试稳定性。
    /// </summary>
    private static string BuildEntriesText(IReadOnlyList<FacetLogEntry> entries, int startIndex)
    {
        if (entries.Count == 0)
        {
            return "没有匹配当前过滤条件的日志。";
        }

        StringBuilder builder = new();
        for (int index = startIndex; index < entries.Count; index++)
        {
            FacetLogEntry entry = entries[index];
            builder.Append('#');
            builder.Append(entry.EventId);
            builder.Append(' ');
            builder.Append('[');
            builder.Append(entry.Level);
            builder.Append("] ");
            builder.Append(entry.Category);
            builder.Append(' ');
            builder.Append(entry.Message);
            builder.Append("  @ ");
            builder.Append(entry.TimestampUtc.ToString("HH:mm:ss.fff"));
            builder.AppendLine();

            if (entry.HasPayload)
            {
                builder.Append("  payload: ");
                builder.Append(JsonSerializer.Serialize(entry.Payload));
                builder.AppendLine();
            }
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>
    /// 解除当前 logger 的事件订阅。
    /// </summary>
    private void UnbindLogger()
    {
        if (_logger != null)
        {
            _logger.EntryLogged -= OnEntryLogged;
            _logger = null;
        }
    }

    private static string GetShortSessionId(string sessionId)
    {
        if (sessionId.Length <= 8)
        {
            return sessionId;
        }

        return sessionId[..8];
    }
}
