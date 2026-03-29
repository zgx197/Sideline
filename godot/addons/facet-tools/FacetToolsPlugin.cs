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
            ReleaseMainScreen();
        }
        catch (Exception exception)
        {
            FacetEditorDiagnostics.Error("Plugin", "EnterTree failed.", exception);
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
            ReleaseMainScreen();
        }
        catch (Exception exception)
        {
            FacetEditorDiagnostics.Error("Plugin", "ExitTree failed.", exception);
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

        if (!visible)
        {
            ReleaseMainScreen();
            return;
        }

        try
        {
            FacetMainScreen mainScreen = RecreateMainScreen();
            mainScreen.Visible = true;
            mainScreen.EnsureViewportLayout();
            mainScreen.RefreshNow();
            mainScreen.LogLayoutSnapshot();
        }
        catch (Exception exception)
        {
            FacetEditorDiagnostics.Error("Plugin", $"MakeVisible failed. visible={visible}", exception);
            _mainScreen?.ShowToolError("Facet 主工作区刷新失败，请查看 user://logs/facet-editor.log。", exception);
            ReleaseMainScreen();
        }
    }

    public override bool _Build()
    {
        FacetEditorDiagnostics.Info("Plugin", "Build requested, releasing main screen.");
        ReleaseMainScreen();
        return true;
    }

    private FacetMainScreen RecreateMainScreen()
    {
        ReleaseMainScreen();
        return CreateMainScreen();
    }

    private FacetMainScreen CreateMainScreen()
    {
        PackedScene mainScreenScene = GD.Load<PackedScene>(MainScreenScenePath)
            ?? throw new InvalidOperationException($"Facet main screen scene load failed: {MainScreenScenePath}");

        FacetMainScreen mainScreen = mainScreenScene.Instantiate<FacetMainScreen>();
        mainScreen.Name = "FacetMainScreen";
        mainScreen.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        mainScreen.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        mainScreen.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        mainScreen.CustomMinimumSize = Vector2.Zero;
        mainScreen.Hide();
        EditorInterface.Singleton.GetEditorMainScreen().AddChild(mainScreen);
        _mainScreen = mainScreen;
        return mainScreen;
    }

    private void ReleaseMainScreen()
    {
        FacetMainScreen? mainScreen = _mainScreen;
        _mainScreen = null;

        if (mainScreen == null)
        {
            return;
        }

        if (GodotObject.IsInstanceValid(mainScreen))
        {
            mainScreen.Visible = false;
            mainScreen.SetProcess(false);
            mainScreen.GetParent()?.RemoveChild(mainScreen);
            mainScreen.QueueFree();
        }
    }
}
