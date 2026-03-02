using Godot;

/// <summary>
/// 主场景控制器：管理窗口模式切换和面板显示
/// </summary>
public partial class Main : Node
{
    private WindowManager _windowManager;
    private Control _idlePanel;
    private Control _dungeonPanel;

    public override void _Ready()
    {
        _windowManager = GetNode<WindowManager>("WindowManager");
        _idlePanel = GetNode<Control>("CanvasLayer/IdlePanel");
        _dungeonPanel = GetNode<Control>("CanvasLayer/DungeonPanel");

        // 监听窗口模式变化
        _windowManager.ModeChanged += OnModeChanged;

        // 监听面板切换请求
        _idlePanel.Connect("SwitchToDungeonRequested", Callable.From(OnSwitchToDungeon));
        _dungeonPanel.Connect("SwitchToIdleRequested", Callable.From(OnSwitchToIdle));

        // 初始状态：挂机模式
        ShowPanel(WindowManager.GameMode.Idle);

        GD.Print("[Main] Sideline Phase 0 启动完成");
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
