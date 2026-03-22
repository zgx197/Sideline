#nullable enable

namespace Sideline.Facet.Layout
{
    /// <summary>
    /// 阶段 11 动态布局节点类型。
    /// 仅暴露 Facet 当前需要的常用 Control 子类，避免把完整 Godot 节点树直接泄露给描述对象。
    /// </summary>
    public enum FacetLayoutNodeType
    {
        PanelContainer = 0,
        MarginContainer = 1,
        VBoxContainer = 2,
        HBoxContainer = 3,
        ScrollContainer = 4,
        Label = 5,
        Button = 6,
        HSeparator = 7,
    }
}
