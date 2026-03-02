using Godot;

/// <summary>
/// 挂机模式面板：显示资源收集、养成状态等
/// Phase 0 原型仅显示基本信息和模式切换按钮
/// </summary>
public partial class IdlePanel : PanelContainer
{
    private Label _titleLabel;
    private Label _statusLabel;
    private Label _resourceLabel;
    private Button _switchButton;
    private Button _closeButton;

    // 模拟资源收集
    private int _gold;
    private double _timer;

    [Signal]
    public delegate void SwitchToDungeonRequestedEventHandler();

    public override void _Ready()
    {
        _titleLabel = GetNode<Label>("%TitleLabel");
        _statusLabel = GetNode<Label>("%StatusLabel");
        _resourceLabel = GetNode<Label>("%ResourceLabel");
        _switchButton = GetNode<Button>("%SwitchButton");
        _closeButton = GetNode<Button>("%CloseButton");

        _switchButton.Pressed += OnSwitchPressed;
        _closeButton.Pressed += OnClosePressed;

        UpdateDisplay();
    }

    public override void _Process(double delta)
    {
        // 模拟挂机资源收集：每秒 +1 金币
        _timer += delta;
        if (_timer >= 1.0)
        {
            _timer -= 1.0;
            _gold++;
            UpdateDisplay();
        }
    }

    private void UpdateDisplay()
    {
        _titleLabel.Text = "Sideline · 挂机中";
        _statusLabel.Text = "正在自动收集资源...";
        _resourceLabel.Text = $"金币: {_gold}";
    }

    private void OnSwitchPressed()
    {
        EmitSignal(SignalName.SwitchToDungeonRequested);
    }

    private void OnClosePressed()
    {
        GetTree().Quit();
    }
}
