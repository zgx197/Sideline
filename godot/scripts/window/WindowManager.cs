using System.Collections.Generic;
using Godot;

/// <summary>
/// 窗口管理器：负责无边框窗口的拖拽、置顶和模式切换。
/// 挂机模式为小窗口、无边框、置顶、可拖拽；刷宝模式为大窗口、有边框、不置顶。
/// </summary>
public partial class WindowManager : Node
{
    /// <summary>
    /// 游戏模式。
    /// </summary>
    public enum GameMode
    {
        Idle,
        Dungeon,
    }

    private static readonly Vector2I IdleWindowSize = new(400, 300);
    private static readonly Vector2I DungeonWindowSize = new(1280, 720);

    [Signal]
    public delegate void ModeChangedEventHandler(int mode);

    public GameMode CurrentMode { get; private set; } = GameMode.Idle;

    private bool _isDragging;
    private Vector2I _dragOffset;

    public override void _Ready()
    {
        ApplyIdleMode();
    }

    public override void _Input(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_toggle_mode"))
        {
            ToggleMode();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (CurrentMode == GameMode.Idle)
        {
            HandleDrag(@event);
        }
    }

    /// <summary>
    /// 切换游戏模式。
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
    /// 切换到挂机模式：无边框、小窗口、置顶、可拖拽。
    /// </summary>
    private void ApplyIdleMode()
    {
        CurrentMode = GameMode.Idle;

        Window window = GetWindow();
        window.Mode = Window.ModeEnum.Windowed;
        window.Borderless = true;
        window.AlwaysOnTop = true;
        window.Size = IdleWindowSize;

        Vector2I screenSize = DisplayServer.ScreenGetSize();
        window.Position = new Vector2I(
            screenSize.X - IdleWindowSize.X - 50,
            screenSize.Y - IdleWindowSize.Y - 100);

        EmitSignal("ModeChanged", (int)CurrentMode);
        LogModeChange("切换到挂机模式", window);
    }

    /// <summary>
    /// 切换到刷宝模式：有边框、大窗口、不置顶。
    /// </summary>
    private void ApplyDungeonMode()
    {
        CurrentMode = GameMode.Dungeon;
        _isDragging = false;

        Window window = GetWindow();
        window.Borderless = false;
        window.AlwaysOnTop = false;
        window.Size = DungeonWindowSize;

        Vector2I screenSize = DisplayServer.ScreenGetSize();
        window.Position = new Vector2I(
            (screenSize.X - DungeonWindowSize.X) / 2,
            (screenSize.Y - DungeonWindowSize.Y) / 2);

        EmitSignal("ModeChanged", (int)CurrentMode);
        LogModeChange("切换到刷宝模式", window);
    }

    /// <summary>
    /// 处理挂机模式下的窗口拖拽。
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

    private void LogModeChange(string message, Window window)
    {
        ClientLog.Info(
            "WindowManager",
            message,
            new Dictionary<string, object?>
            {
                ["mode"] = CurrentMode.ToString(),
                ["borderless"] = window.Borderless,
                ["alwaysOnTop"] = window.AlwaysOnTop,
                ["sizeX"] = window.Size.X,
                ["sizeY"] = window.Size.Y,
                ["positionX"] = window.Position.X,
                ["positionY"] = window.Position.Y,
            });
    }
}
