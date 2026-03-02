using Godot;

/// <summary>
/// 刷宝模式面板：显示地下城状态等
/// Phase 0 原型仅显示占位信息和模式切换按钮
/// </summary>
public partial class DungeonPanel : PanelContainer
{
    private Label _titleLabel;
    private Label _statusLabel;
    private Button _switchButton;

    [Signal]
    public delegate void SwitchToIdleRequestedEventHandler();

    public override void _Ready()
    {
        _titleLabel = GetNode<Label>("%TitleLabel");
        _statusLabel = GetNode<Label>("%StatusLabel");
        _switchButton = GetNode<Button>("%SwitchButton");

        _switchButton.Pressed += OnSwitchPressed;

        _titleLabel.Text = "Sideline · 地下城";
        _statusLabel.Text = "准备进入地下城...（Phase 0 占位）";
    }

    private void OnSwitchPressed()
    {
        EmitSignal(SignalName.SwitchToIdleRequested);
    }
}
