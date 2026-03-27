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
    private bool _loadFailed;

    /// <summary>
    /// 插件进入编辑器树时初始化主工作区，并把入口挂到编辑器主界面与工具菜单。
    /// </summary>
    public override void _EnterTree()
    {
        FacetEditorDiagnostics.Info("Plugin", "EnterTree");
        _loadFailed = false;

        try
        {
            CleanupMainScreen();
        }
        catch (Exception exception)
        {
            FacetEditorDiagnostics.Error("Plugin", "EnterTree failed.", exception);
            _loadFailed = true;
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
            CleanupMainScreen();
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
            CleanupMainScreen();
            return;
        }

        try
        {
            EnsureMainScreenCreated();
            if (_mainScreen == null)
            {
                return;
            }

            _mainScreen.Visible = visible;
            _mainScreen.EnsureViewportLayout();
            _mainScreen.RefreshNow();
            _mainScreen.LogLayoutSnapshot();
        }
        catch (Exception exception)
        {
            FacetEditorDiagnostics.Error("Plugin", $"MakeVisible failed. visible={visible}", exception);
            _mainScreen?.ShowToolError("Facet 主工作区刷新失败，请查看 user://logs/facet-editor.log。", exception);
            CleanupMainScreen();
        }
    }

    private void EnsureMainScreenCreated()
    {
        if (_mainScreen != null)
        {
            if (GodotObject.IsInstanceValid(_mainScreen))
            {
                return;
            }

            _mainScreen = null;
        }

        PackedScene mainScreenScene = GD.Load<PackedScene>(MainScreenScenePath)
            ?? throw new InvalidOperationException($"Facet main screen scene load failed: {MainScreenScenePath}");

        _mainScreen = mainScreenScene.Instantiate<FacetMainScreen>();
        _mainScreen.Name = "FacetMainScreen";
        _mainScreen.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _mainScreen.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _mainScreen.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        _mainScreen.CustomMinimumSize = Vector2.Zero;
        _mainScreen.Hide();
        EditorInterface.Singleton.GetEditorMainScreen().AddChild(_mainScreen);
    }

    private void CleanupMainScreen()
    {
        if (_mainScreen == null)
        {
            return;
        }

        if (GodotObject.IsInstanceValid(_mainScreen))
        {
            _mainScreen.GetParent()?.RemoveChild(_mainScreen);
            _mainScreen.QueueFree();
        }

        _mainScreen = null;
    }
}
