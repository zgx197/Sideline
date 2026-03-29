#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Godot;

/// <summary>
/// Facet 编辑器主工作台首页。
/// 只承载编辑期入口、静态校验、目录导航与文档入口，不再承担运行时实验职责。
/// </summary>
[Tool]
public partial class FacetMainScreen : PanelContainer
{
    private FacetEditorWorkspacePaths _workspacePaths = null!;
    private GridContainer _summaryGrid = null!;
    private GridContainer _sectionsGrid = null!;
    private Label _overviewSummaryLabel = null!;
    private Label _overviewDetailsLabel = null!;
    private Label _validationSummaryLabel = null!;
    private RichTextLabel _validationDetailsLabel = null!;
    private Label _workspaceSummaryLabel = null!;
    private Label _docsSummaryLabel = null!;
    private Button _refreshButton = null!;
    private Button _openLogsButton = null!;
    private Button _openDocsDirectoryButton = null!;
    private Button _openFacetToolsDirectoryButton = null!;
    private Button _openFacetRuntimeDirectoryButton = null!;
    private Button _openRuntimeUiDirectoryButton = null!;
    private Button _openRefactorPlanButton = null!;
    private Button _openAiWorkflowButton = null!;
    private Button _openAssemblyLayoutButton = null!;
    private Button _openWorkflowButton = null!;
    private bool _isUiReady;
    private bool _eventsConnected;

    public override void _Ready()
    {
        try
        {
            FacetEditorDiagnostics.Info("MainScreen", $"Ready parent={GetParent()?.GetType().Name ?? "<null>"}");
            Name = "FacetMainScreen";
            SetAnchorsPreset(LayoutPreset.FullRect);
            SizeFlagsHorizontal = SizeFlags.ExpandFill;
            SizeFlagsVertical = SizeFlags.ExpandFill;
            CustomMinimumSize = Vector2.Zero;
            _workspacePaths = FacetEditorWorkspacePaths.Create();

            ResolveUi();
            ConfigureUi();
            _isUiReady = true;

            RefreshNow();
            UpdateResponsiveLayout();
        }
        catch (Exception exception)
        {
            FacetEditorDiagnostics.Error("MainScreen", "Ready failed.", exception);
            ShowToolError("Facet 主工作台初始化失败，请查看 user://logs/facet-editor.log。", exception);
        }
    }

    public override void _ExitTree()
    {
        DisconnectUiEvents();
        _isUiReady = false;
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
                $"LayoutSnapshot visible={Visible} size={Size} min={CustomMinimumSize} parentSize={(GetParent() as Control)?.Size} summaryGrid={_summaryGrid?.Size} sectionsGrid={_sectionsGrid?.Size}");
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
            RefreshOverview();
            RefreshValidation();
            RefreshWorkspace();
            RefreshDocs();
        }
        catch (Exception exception)
        {
            FacetEditorDiagnostics.Error("MainScreen", "RefreshNow failed.", exception);
            ShowToolError("Facet 主工作台刷新失败，请查看 user://logs/facet-editor.log。", exception);
        }
    }

    public void ShowToolError(string summary, Exception exception)
    {
        string detail = $"{summary}\n{exception.GetType().Name}: {exception.Message}";

        _overviewSummaryLabel.Text = "Facet 编辑器首页状态异常";
        _overviewDetailsLabel.Text = detail;
        _validationSummaryLabel.Text = "静态校验未完成";
        _validationDetailsLabel.Text = detail;
        _workspaceSummaryLabel.Text = detail;
        _docsSummaryLabel.Text = detail;
    }

    private void ResolveUi()
    {
        _summaryGrid = ResolveRequiredNode<GridContainer>("%SummaryGrid");
        _sectionsGrid = ResolveRequiredNode<GridContainer>("%SectionsGrid");
        _overviewSummaryLabel = ResolveRequiredNode<Label>("%OverviewSummaryLabel");
        _overviewDetailsLabel = ResolveRequiredNode<Label>("%OverviewDetailsLabel");
        _validationSummaryLabel = ResolveRequiredNode<Label>("%ValidationSummaryLabel");
        _validationDetailsLabel = ResolveRequiredNode<RichTextLabel>("%ValidationDetailsLabel");
        _workspaceSummaryLabel = ResolveRequiredNode<Label>("%WorkspaceSummaryLabel");
        _docsSummaryLabel = ResolveRequiredNode<Label>("%DocsSummaryLabel");
        _refreshButton = ResolveRequiredNode<Button>("%RefreshButton");
        _openLogsButton = ResolveRequiredNode<Button>("%OpenLogsButton");
        _openDocsDirectoryButton = ResolveRequiredNode<Button>("%OpenDocsDirectoryButton");
        _openFacetToolsDirectoryButton = ResolveRequiredNode<Button>("%OpenFacetToolsDirectoryButton");
        _openFacetRuntimeDirectoryButton = ResolveRequiredNode<Button>("%OpenFacetRuntimeDirectoryButton");
        _openRuntimeUiDirectoryButton = ResolveRequiredNode<Button>("%OpenRuntimeUiDirectoryButton");
        _openRefactorPlanButton = ResolveRequiredNode<Button>("%OpenRefactorPlanButton");
        _openAiWorkflowButton = ResolveRequiredNode<Button>("%OpenAiWorkflowButton");
        _openAssemblyLayoutButton = ResolveRequiredNode<Button>("%OpenAssemblyLayoutButton");
        _openWorkflowButton = ResolveRequiredNode<Button>("%OpenWorkflowButton");
    }

    private void ConfigureUi()
    {
        ApplyTheme();
        ConnectUiEvents();
    }

    private void ApplyTheme()
    {
        AddThemeStyleboxOverride("panel", CreateScreenPanelStyle());

        foreach (PanelContainer card in GetTree().GetNodesInGroup("facet_editor_card"))
        {
            card.AddThemeStyleboxOverride("panel", CreateCardStyle());
        }

        _overviewSummaryLabel.AddThemeColorOverride("font_color", new Color("f3f6fb"));
        _validationSummaryLabel.AddThemeColorOverride("font_color", new Color("f3f6fb"));
        _workspaceSummaryLabel.AddThemeColorOverride("font_color", new Color("f3f6fb"));
        _docsSummaryLabel.AddThemeColorOverride("font_color", new Color("f3f6fb"));

        _overviewSummaryLabel.AddThemeFontSizeOverride("font_size", 22);
        _validationSummaryLabel.AddThemeFontSizeOverride("font_size", 22);
        _workspaceSummaryLabel.AddThemeFontSizeOverride("font_size", 22);
        _docsSummaryLabel.AddThemeFontSizeOverride("font_size", 22);

        _overviewDetailsLabel.AddThemeColorOverride("font_color", new Color("a8b3c7"));
        _workspaceSummaryLabel.AddThemeColorOverride("font_color", new Color("d7deea"));
        _docsSummaryLabel.AddThemeColorOverride("font_color", new Color("d7deea"));
        _validationDetailsLabel.AddThemeColorOverride("default_color", new Color("d7deea"));
        _validationDetailsLabel.AddThemeStyleboxOverride("normal", CreateSurfaceStyle());
    }

    private void ConnectUiEvents()
    {
        if (_eventsConnected)
        {
            return;
        }

        _refreshButton.Pressed += RefreshNow;
        _openLogsButton.Pressed += OnOpenLogsPressed;
        _openDocsDirectoryButton.Pressed += OnOpenDocsDirectoryPressed;
        _openFacetToolsDirectoryButton.Pressed += OnOpenFacetToolsDirectoryPressed;
        _openFacetRuntimeDirectoryButton.Pressed += OnOpenFacetRuntimeDirectoryPressed;
        _openRuntimeUiDirectoryButton.Pressed += OnOpenRuntimeUiDirectoryPressed;
        _openRefactorPlanButton.Pressed += OnOpenRefactorPlanPressed;
        _openAiWorkflowButton.Pressed += OnOpenAiWorkflowPressed;
        _openAssemblyLayoutButton.Pressed += OnOpenAssemblyLayoutPressed;
        _openWorkflowButton.Pressed += OnOpenWorkflowPressed;
        _eventsConnected = true;
    }

    private void DisconnectUiEvents()
    {
        if (!_eventsConnected)
        {
            return;
        }

        _refreshButton.Pressed -= RefreshNow;
        _openLogsButton.Pressed -= OnOpenLogsPressed;
        _openDocsDirectoryButton.Pressed -= OnOpenDocsDirectoryPressed;
        _openFacetToolsDirectoryButton.Pressed -= OnOpenFacetToolsDirectoryPressed;
        _openFacetRuntimeDirectoryButton.Pressed -= OnOpenFacetRuntimeDirectoryPressed;
        _openRuntimeUiDirectoryButton.Pressed -= OnOpenRuntimeUiDirectoryPressed;
        _openRefactorPlanButton.Pressed -= OnOpenRefactorPlanPressed;
        _openAiWorkflowButton.Pressed -= OnOpenAiWorkflowPressed;
        _openAssemblyLayoutButton.Pressed -= OnOpenAssemblyLayoutPressed;
        _openWorkflowButton.Pressed -= OnOpenWorkflowPressed;
        _eventsConnected = false;
    }

    private void RefreshOverview()
    {
        bool hasFacetTools = Directory.Exists(_workspacePaths.FacetToolsDirectoryPath);
        bool hasFacetRuntime = Directory.Exists(_workspacePaths.FacetRuntimeDirectoryPath);
        bool hasRuntimeUi = Directory.Exists(_workspacePaths.RuntimeUiDirectoryPath);
        bool hasRefactorPlan = File.Exists(_workspacePaths.RefactorPlanPath);
        bool hasAiWorkflow = File.Exists(_workspacePaths.AiWorkflowPath);
        bool hasWorkflow = File.Exists(_workspacePaths.WorkflowPath);

        int readyCount = 0;
        readyCount += hasFacetTools ? 1 : 0;
        readyCount += hasFacetRuntime ? 1 : 0;
        readyCount += hasRuntimeUi ? 1 : 0;
        readyCount += hasRefactorPlan ? 1 : 0;
        readyCount += hasAiWorkflow ? 1 : 0;
        readyCount += hasWorkflow ? 1 : 0;

        _overviewSummaryLabel.Text = $"{readyCount}/6 个基础入口已就绪";
        _overviewDetailsLabel.Text =
            "Facet Main Panel 已收敛为编辑器首页。\n" +
            "运行时实验与调试职责不再放在这里，而是回归运行时窗口与后续 Runtime Debug 工具。\n" +
            "阶段 5 已冻结：当前不拆 Facet.Godot / Facet.Editor 程序集，先保持脚本归属清晰并继续压缩编辑器热区。\n" +
            $"用户日志目录: {_workspacePaths.LogsDirectoryPath}";
    }

    private void RefreshValidation()
    {
        List<FacetEditorValidationItem> items = FacetEditorValidationCatalog.Build(_workspacePaths);
        int passedCount = 0;
        foreach (FacetEditorValidationItem item in items)
        {
            if (item.IsSuccess)
            {
                passedCount++;
            }
        }

        _validationSummaryLabel.Text = $"{passedCount}/{items.Count} 项静态检查通过";

        StringBuilder builder = new();
        foreach (FacetEditorValidationItem item in items)
        {
            string icon = item.IsSuccess ? "[color=#7ce0b8]PASS[/color]" : "[color=#ff8d8d]FAIL[/color]";
            builder.Append(icon);
            builder.Append("  ");
            builder.Append(EscapeBbcode(item.Title));
            builder.Append('\n');
            builder.Append("    ");
            builder.Append(EscapeBbcode(item.Detail));
            builder.Append("\n\n");
        }

        builder.Append("[color=#a8b3c7]阶段 2 已冻结：Facet Main Panel 只承载编辑期入口、静态校验、目录导航与文档入口。[/color]\n");
        builder.Append("[color=#a8b3c7]阶段 5 已冻结：当前维持 Sideline 主程序集承载 Godot 直接加载脚本，通过目录与职责分层约束代替过早拆分新 csproj。[/color]");
        _validationDetailsLabel.Text = builder.ToString().TrimEnd();
    }

    private void RefreshWorkspace()
    {
        List<string> missingTargets = new();
        if (!Directory.Exists(_workspacePaths.FacetToolsDirectoryPath))
        {
            missingTargets.Add("facet-tools");
        }

        if (!Directory.Exists(_workspacePaths.FacetRuntimeDirectoryPath))
        {
            missingTargets.Add("scripts/facet/Runtime");
        }

        if (!Directory.Exists(_workspacePaths.RuntimeUiDirectoryPath))
        {
            missingTargets.Add("scripts/ui");
        }

        _workspaceSummaryLabel.Text = missingTargets.Count == 0
            ? "编辑期入口目录已就绪，可直接从此处跳转到插件、运行时宿主与界面层目录。"
            : $"以下目录缺失，需要先修复项目结构：{string.Join(", ", missingTargets)}";

        _openFacetToolsDirectoryButton.Disabled = !Directory.Exists(_workspacePaths.FacetToolsDirectoryPath);
        _openFacetRuntimeDirectoryButton.Disabled = !Directory.Exists(_workspacePaths.FacetRuntimeDirectoryPath);
        _openRuntimeUiDirectoryButton.Disabled = !Directory.Exists(_workspacePaths.RuntimeUiDirectoryPath);
        _openDocsDirectoryButton.Disabled = !Directory.Exists(_workspacePaths.DocsDirectoryPath);
    }

    private void RefreshDocs()
    {
        List<string> docs = new();
        docs.Add(BuildDocStatusLine("FacetRefactorPlan", _workspacePaths.RefactorPlanPath));
        docs.Add(BuildDocStatusLine("FacetAIUIWorkflow", _workspacePaths.AiWorkflowPath));
        docs.Add(BuildDocStatusLine("AssemblyLayout", _workspacePaths.AssemblyLayoutPath));
        docs.Add(BuildDocStatusLine("Workflow", _workspacePaths.WorkflowPath));

        _docsSummaryLabel.Text =
            "当前首页只保留编辑期工作流入口。\n" +
            string.Join("\n", docs);

        _openRefactorPlanButton.Disabled = !File.Exists(_workspacePaths.RefactorPlanPath);
        _openAiWorkflowButton.Disabled = !File.Exists(_workspacePaths.AiWorkflowPath);
        _openAssemblyLayoutButton.Disabled = !File.Exists(_workspacePaths.AssemblyLayoutPath);
        _openWorkflowButton.Disabled = !File.Exists(_workspacePaths.WorkflowPath);
    }

    private void UpdateResponsiveLayout()
    {
        float viewportWidth = Size.X;
        if (viewportWidth <= 0.0f)
        {
            return;
        }

        _summaryGrid.Columns = viewportWidth >= 1120.0f ? 2 : 1;
        _sectionsGrid.Columns = viewportWidth >= 1380.0f ? 2 : 1;
    }

    private void OnOpenLogsPressed()
    {
        OpenPath(_workspacePaths.LogsDirectoryPath, "OpenLogs");
    }

    private void OnOpenDocsDirectoryPressed()
    {
        OpenPath(_workspacePaths.DocsDirectoryPath, "OpenDocsDirectory");
    }

    private void OnOpenFacetToolsDirectoryPressed()
    {
        OpenPath(_workspacePaths.FacetToolsDirectoryPath, "OpenFacetToolsDirectory");
    }

    private void OnOpenFacetRuntimeDirectoryPressed()
    {
        OpenPath(_workspacePaths.FacetRuntimeDirectoryPath, "OpenFacetRuntimeDirectory");
    }

    private void OnOpenRuntimeUiDirectoryPressed()
    {
        OpenPath(_workspacePaths.RuntimeUiDirectoryPath, "OpenRuntimeUiDirectory");
    }

    private void OnOpenRefactorPlanPressed()
    {
        OpenPath(_workspacePaths.RefactorPlanPath, "OpenRefactorPlan");
    }

    private void OnOpenAiWorkflowPressed()
    {
        OpenPath(_workspacePaths.AiWorkflowPath, "OpenAiWorkflow");
    }

    private void OnOpenAssemblyLayoutPressed()
    {
        OpenPath(_workspacePaths.AssemblyLayoutPath, "OpenAssemblyLayout");
    }

    private void OnOpenWorkflowPressed()
    {
        OpenPath(_workspacePaths.WorkflowPath, "OpenWorkflow");
    }

    private void OpenPath(string path, string actionName)
    {
        try
        {
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                throw new FileNotFoundException($"Facet editor target not found: {path}");
            }

            FacetEditorDiagnostics.Info("MainScreen", $"{actionName} path={path}");
            Error error = OS.ShellOpen(path);
            if (error != Error.Ok)
            {
                throw new InvalidOperationException($"ShellOpen failed: {error}");
            }
        }
        catch (Exception exception)
        {
            FacetEditorDiagnostics.Error("MainScreen", $"{actionName} failed.", exception);
            ShowToolError("Facet 编辑器入口打开失败，请查看 user://logs/facet-editor.log。", exception);
        }
    }

    private static string BuildDocStatusLine(string name, string path)
    {
        return File.Exists(path)
            ? $"[已就绪] {name}"
            : $"[缺失] {name}: {path}";
    }

    private static string EscapeBbcode(string value)
    {
        return value
            .Replace("[", "[lb]")
            .Replace("]", "[rb]");
    }

    private static StyleBoxFlat CreateScreenPanelStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = new Color("10141b"),
            ContentMarginLeft = 0,
            ContentMarginTop = 0,
            ContentMarginRight = 0,
            ContentMarginBottom = 0,
        };
    }

    private static StyleBoxFlat CreateCardStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = new Color("161d28"),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            BorderColor = new Color("263244"),
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

    private static StyleBoxFlat CreateSurfaceStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = new Color("0f141b"),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            BorderColor = new Color("253246"),
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

    private TNode ResolveRequiredNode<TNode>(string path) where TNode : Node
    {
        TNode? node = GetNodeOrNull<TNode>(path);
        if (node == null)
        {
            throw new InvalidOperationException($"FacetMainScreen required node missing: {path}");
        }

        return node;
    }

}
