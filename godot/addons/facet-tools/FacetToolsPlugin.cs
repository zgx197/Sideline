#nullable enable

using System;
using Godot;

/// <summary>
/// Facet 编辑器插件入口。
/// 负责把 Facet 主工作区注册到 Godot 编辑器主界面，并处理页签显隐与工具菜单入口。
/// </summary>
[Tool]
public partial class FacetToolsPlugin : EditorPlugin
{
    /// <summary>
    /// Godot 主工作区页签名称。
    /// </summary>
    private const string PluginName = "Facet";

    /// <summary>
    /// 顶部工具菜单中的命令名称。
    /// </summary>
    private const string ToolMenuTitle = "Open Facet Workspace";

    /// <summary>
    /// Facet 主工作区场景路径。
    /// 使用场景驱动的容器布局，避免继续在代码中手拼复杂界面。
    /// </summary>
    private const string MainScreenScenePath = "res://addons/facet-tools/FacetMainScreen.tscn";

    /// <summary>
    /// Facet 主工作区控件实例。
    /// 插件进入编辑器树时创建，退出时销毁。
    /// </summary>
    private FacetMainScreen? _mainScreen;

    /// <summary>
    /// 插件进入编辑器树时初始化主工作区，并把入口挂到编辑器主界面与工具菜单。
    /// </summary>
    public override void _EnterTree()
    {
        FacetEditorDiagnostics.Info("Plugin", "EnterTree");

        try
        {
            PackedScene mainScreenScene = GD.Load<PackedScene>(MainScreenScenePath)
                ?? throw new InvalidOperationException($"Facet main screen scene load failed: {MainScreenScenePath}");

            _mainScreen = mainScreenScene.Instantiate<FacetMainScreen>();
            _mainScreen.Name = "FacetMainScreen";
            _mainScreen.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            _mainScreen.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            _mainScreen.CustomMinimumSize = Vector2.Zero;
            _mainScreen.Hide();
            EditorInterface.Singleton.GetEditorMainScreen().AddChild(_mainScreen);

            AddToolMenuItem(ToolMenuTitle, Callable.From(OpenFacetWorkspace));

            // 保留一个环境变量入口，便于自动化排查编辑器布局问题时直接拉起 Facet 工作区。
            if (System.Environment.GetEnvironmentVariable("SIDELINE_FACET_AUTO_OPEN") == "1")
            {
                CallDeferred(nameof(OpenFacetWorkspaceForDiagnostics));
            }
        }
        catch (Exception exception)
        {
            FacetEditorDiagnostics.Error("Plugin", "EnterTree failed.", exception);
            throw;
        }
    }

    /// <summary>
    /// 插件退出编辑器树时移除工具菜单并释放主工作区控件。
    /// </summary>
    public override void _ExitTree()
    {
        FacetEditorDiagnostics.Info("Plugin", "ExitTree");

        try
        {
            RemoveToolMenuItem(ToolMenuTitle);

            if (_mainScreen != null)
            {
                _mainScreen.GetParent()?.RemoveChild(_mainScreen);
                _mainScreen.QueueFree();
                _mainScreen = null;
            }
        }
        catch (Exception exception)
        {
            FacetEditorDiagnostics.Error("Plugin", "ExitTree failed.", exception);
            throw;
        }
    }

    /// <summary>
    /// 声明插件提供一个主工作区页签。
    /// </summary>
    public override bool _HasMainScreen()
    {
        return true;
    }

    /// <summary>
    /// 返回 Godot 编辑器中显示的主工作区名称。
    /// </summary>
    public override string _GetPluginName()
    {
        return PluginName;
    }

    /// <summary>
    /// 返回 Godot 编辑器为该工作区显示的图标。
    /// 这里复用编辑器内置 Node 图标，避免额外维护图标资源。
    /// </summary>
    public override Texture2D _GetPluginIcon()
    {
        return EditorInterface.Singleton.GetBaseControl().GetThemeIcon("Node", "EditorIcons");
    }

    /// <summary>
    /// Godot 在切换主工作区可见性时调用。
    /// 可见时主动刷新一次页面并校准布局，避免页签首次打开为空白或尺寸异常。
    /// </summary>
    public override void _MakeVisible(bool visible)
    {
        FacetEditorDiagnostics.Info("Plugin", $"MakeVisible visible={visible}");

        if (_mainScreen == null)
        {
            FacetEditorDiagnostics.Warning("Plugin", "Main screen is null during MakeVisible.");
            return;
        }

        try
        {
            _mainScreen.Visible = visible;
            if (visible)
            {
                _mainScreen.EnsureViewportLayout();
                _mainScreen.RefreshNow();
                _mainScreen.CallDeferred(nameof(FacetMainScreen.EnsureViewportLayout));
                _mainScreen.CallDeferred(nameof(FacetMainScreen.LogLayoutSnapshot));
            }
        }
        catch (Exception exception)
        {
            FacetEditorDiagnostics.Error("Plugin", $"MakeVisible failed. visible={visible}", exception);
            throw;
        }
    }

    /// <summary>
    /// 打开 Facet 主工作区页签。
    /// </summary>
    private void OpenFacetWorkspace()
    {
        FacetEditorDiagnostics.Info("Plugin", "OpenFacetWorkspace");
        EditorInterface.Singleton.SetMainScreenEditor(PluginName);
    }

    /// <summary>
    /// 诊断场景专用入口。
    /// 当前逻辑与普通打开行为一致，但保留独立方法便于后续扩展自动诊断流程。
    /// </summary>
    private void OpenFacetWorkspaceForDiagnostics()
    {
        OpenFacetWorkspace();
    }
}
