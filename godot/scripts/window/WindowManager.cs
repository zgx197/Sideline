using Godot;

/// <summary>
/// 窗口管理器：负责无边框窗口的拖拽、置顶、模式切换
/// 挂机模式：小窗口(400x300)，无边框，始终置顶，可拖拽
/// 刷宝模式：大窗口(1280x720)，有边框，非置顶
/// </summary>
public partial class WindowManager : Node
{
    /// <summary>
    /// 游戏模式
    /// </summary>
    public enum GameMode
    {
        Idle,    // 挂机模式（小窗口）
        Dungeon  // 刷宝模式（大窗口）
    }

    // 挂机模式窗口尺寸
    private static readonly Vector2I IdleWindowSize = new(400, 300);
    // 刷宝模式窗口尺寸
    private static readonly Vector2I DungeonWindowSize = new(1280, 720);

    [Signal]
    public delegate void ModeChangedEventHandler(int mode);

    public GameMode CurrentMode { get; private set; } = GameMode.Idle;

    // 拖拽状态
    private bool _isDragging;
    private Vector2I _dragOffset;

    public override void _Ready()
    {
        // 启动时进入挂机模式
        ApplyIdleMode();
    }

    public override void _Input(InputEvent @event)
    {
        // ESC 键：切换模式
        if (@event.IsActionPressed("ui_toggle_mode"))
        {
            ToggleMode();
            GetViewport().SetInputAsHandled();
            return;
        }

        // 挂机模式下支持拖拽窗口
        if (CurrentMode == GameMode.Idle)
        {
            HandleDrag(@event);
        }
    }

    /// <summary>
    /// 切换游戏模式
    /// </summary>
    public void ToggleMode()
    {
        if (CurrentMode == GameMode.Idle)
        {
            ApplyDungeonMode();
        }
        else
        {
            ApplyIdleMode();
        }
    }

    /// <summary>
    /// 切换到挂机模式：无边框、小窗口、置顶、可拖拽
    /// </summary>
    private void ApplyIdleMode()
    {
        CurrentMode = GameMode.Idle;

        var window = GetWindow();

        // 先取消全屏
        window.Mode = Godot.Window.ModeEnum.Windowed;

        // 无边框
        window.Borderless = true;
        // 始终置顶
        window.AlwaysOnTop = true;
        // 设置尺寸
        window.Size = IdleWindowSize;
        // 居中到屏幕右下角
        var screenSize = DisplayServer.ScreenGetSize();
        window.Position = new Vector2I(
            screenSize.X - IdleWindowSize.X - 50,
            screenSize.Y - IdleWindowSize.Y - 100
        );

        EmitSignal(SignalName.ModeChanged, (int)CurrentMode);

        GD.Print("[WindowManager] 切换到挂机模式");
    }

    /// <summary>
    /// 切换到刷宝模式：有边框、大窗口、非置顶
    /// </summary>
    private void ApplyDungeonMode()
    {
        CurrentMode = GameMode.Dungeon;
        _isDragging = false;

        var window = GetWindow();

        // 有边框
        window.Borderless = false;
        // 取消置顶
        window.AlwaysOnTop = false;
        // 设置尺寸
        window.Size = DungeonWindowSize;
        // 居中
        var screenSize = DisplayServer.ScreenGetSize();
        window.Position = new Vector2I(
            (screenSize.X - DungeonWindowSize.X) / 2,
            (screenSize.Y - DungeonWindowSize.Y) / 2
        );

        EmitSignal(SignalName.ModeChanged, (int)CurrentMode);

        GD.Print("[WindowManager] 切换到刷宝模式");
    }

    /// <summary>
    /// 处理挂机模式下的窗口拖拽
    /// </summary>
    private void HandleDrag(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton)
        {
            if (mouseButton.ButtonIndex == MouseButton.Left)
            {
                if (mouseButton.Pressed)
                {
                    _isDragging = true;
                    _dragOffset = DisplayServer.MouseGetPosition() - GetWindow().Position;
                }
                else
                {
                    _isDragging = false;
                }
            }
        }
        else if (@event is InputEventMouseMotion && _isDragging)
        {
            GetWindow().Position = DisplayServer.MouseGetPosition() - _dragOffset;
        }
    }
}
