#nullable enable

using System;
using Godot;

[Tool]
public partial class FacetToolsPlugin : EditorPlugin
{
    private const string PluginName = "Facet";
    private const string ToolMenuTitle = "Open Facet Workspace";

    private FacetMainScreen? _mainScreen;

    public override void _EnterTree()
    {
        FacetEditorDiagnostics.Info("Plugin", "EnterTree");

        try
        {
            _mainScreen = new FacetMainScreen();
            _mainScreen.Name = "FacetMainScreen";
            _mainScreen.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            _mainScreen.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            _mainScreen.CustomMinimumSize = new Vector2(960.0f, 640.0f);
            _mainScreen.Hide();
            EditorInterface.Singleton.GetEditorMainScreen().AddChild(_mainScreen);

            AddToolMenuItem(ToolMenuTitle, Callable.From(OpenFacetWorkspace));

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

    public override bool _HasMainScreen()
    {
        return true;
    }

    public override string _GetPluginName()
    {
        return PluginName;
    }

    public override Texture2D _GetPluginIcon()
    {
        return EditorInterface.Singleton.GetBaseControl().GetThemeIcon("Node", "EditorIcons");
    }

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

    private void OpenFacetWorkspace()
    {
        FacetEditorDiagnostics.Info("Plugin", "OpenFacetWorkspace");
        EditorInterface.Singleton.SetMainScreenEditor(PluginName);
    }

    private void OpenFacetWorkspaceForDiagnostics()
    {
        OpenFacetWorkspace();
    }
}
