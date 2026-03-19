using System.Collections.Generic;
using Godot;

/// <summary>
/// 主场景控制器：管理窗口模式切换和面板显示。
/// </summary>
public partial class Main : Node
{
    private WindowManager _windowManager = null!;
    private Control _idlePanel = null!;
    private Control _dungeonPanel = null!;

    public override void _Ready()
    {
        _windowManager = GetNode<WindowManager>("WindowManager");
        _idlePanel = GetNode<Control>("CanvasLayer/IdlePanel");
        _dungeonPanel = GetNode<Control>("CanvasLayer/DungeonPanel");

        _windowManager.ModeChanged += OnModeChanged;
        _idlePanel.Connect("SwitchToDungeonRequested", Callable.From(OnSwitchToDungeon));
        _dungeonPanel.Connect("SwitchToIdleRequested", Callable.From(OnSwitchToIdle));

        ShowPanel(WindowManager.GameMode.Idle);

        ClientLog.Info(
            "Main",
            "Sideline Phase 0 启动完成",
            new Dictionary<string, object?>
            {
                ["scenePath"] = GetPath().ToString(),
                ["currentMode"] = WindowManager.GameMode.Idle.ToString(),
                ["idlePanelVisible"] = _idlePanel.Visible,
                ["dungeonPanelVisible"] = _dungeonPanel.Visible,
            });
    }

    private void OnModeChanged(int mode)
    {
        ShowPanel((WindowManager.GameMode)mode);
    }

    private void OnSwitchToDungeon()
    {
        _windowManager.ToggleMode();
    }

    private void OnSwitchToIdle()
    {
        _windowManager.ToggleMode();
    }

    private void ShowPanel(WindowManager.GameMode mode)
    {
        _idlePanel.Visible = mode == WindowManager.GameMode.Idle;
        _dungeonPanel.Visible = mode == WindowManager.GameMode.Dungeon;
    }
}