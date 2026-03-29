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

    private static readonly Vector2I MinimumIdleWindowSize = new(560, 420);
    private static readonly Vector2I MaximumIdleWindowSize = new(900, 640);
    private static readonly Vector2I MinimumDungeonWindowSize = new(1600, 900);

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
        Vector2I screenSize = DisplayServer.ScreenGetSize();
        Vector2I idleWindowSize = GetIdleWindowSize(screenSize);
        window.Size = idleWindowSize;

        window.Position = new Vector2I(
            screenSize.X - idleWindowSize.X - 50,
            screenSize.Y - idleWindowSize.Y - 100);

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
        Vector2I screenSize = DisplayServer.ScreenGetSize();
        Vector2I dungeonWindowSize = GetDungeonWindowSize(screenSize);
        window.Size = dungeonWindowSize;

        window.Position = new Vector2I(
            (screenSize.X - dungeonWindowSize.X) / 2,
            (screenSize.Y - dungeonWindowSize.Y) / 2);

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

    private static Vector2I GetIdleWindowSize(Vector2I screenSize)
    {
        int width = Mathf.Clamp(screenSize.X / 5, MinimumIdleWindowSize.X, MaximumIdleWindowSize.X);
        int height = Mathf.Clamp(screenSize.Y / 4, MinimumIdleWindowSize.Y, MaximumIdleWindowSize.Y);
        return new Vector2I(width, height);
    }

    private static Vector2I GetDungeonWindowSize(Vector2I screenSize)
    {
        int maxWidth = Mathf.Max(MinimumDungeonWindowSize.X, screenSize.X - 120);
        int maxHeight = Mathf.Max(MinimumDungeonWindowSize.Y, screenSize.Y - 120);
        int width = Mathf.Clamp(screenSize.X * 3 / 5, MinimumDungeonWindowSize.X, maxWidth);
        int height = Mathf.Clamp(screenSize.Y * 3 / 5, MinimumDungeonWindowSize.Y, maxHeight);
        return new Vector2I(width, height);
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
