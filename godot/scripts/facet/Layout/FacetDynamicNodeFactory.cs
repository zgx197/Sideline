#nullable enable

using System;
using Godot;

namespace Sideline.Facet.Layout
{
    /// <summary>
    /// 动态布局节点工厂。
    /// 负责把受限描述对象构造成 Godot Control 树，并写入稳定节点键元数据。
    /// </summary>
    public sealed class FacetDynamicNodeFactory
    {
        public Control CreateTree(FacetGeneratedLayoutNodeDefinition definition)
        {
            ArgumentNullException.ThrowIfNull(definition);

            Control root = CreateControl(definition);
            foreach (FacetGeneratedLayoutNodeDefinition childDefinition in definition.Children)
            {
                root.AddChild(CreateTree(childDefinition));
            }

            return root;
        }

        private static Control CreateControl(FacetGeneratedLayoutNodeDefinition definition)
        {
            Control control = definition.NodeType switch
            {
                FacetLayoutNodeType.PanelContainer => new PanelContainer(),
                FacetLayoutNodeType.MarginContainer => new MarginContainer(),
                FacetLayoutNodeType.VBoxContainer => new VBoxContainer(),
                FacetLayoutNodeType.HBoxContainer => new HBoxContainer(),
                FacetLayoutNodeType.ScrollContainer => new ScrollContainer(),
                FacetLayoutNodeType.Label => new Label(),
                FacetLayoutNodeType.Button => new Button(),
                FacetLayoutNodeType.HSeparator => new HSeparator(),
                _ => throw new InvalidOperationException($"Unsupported dynamic layout node type: {definition.NodeType}"),
            };

            control.Name = string.IsNullOrWhiteSpace(definition.Name)
                ? CreateNodeName(definition.Key)
                : definition.Name;
            control.Visible = definition.Visible;
            control.SetMeta("facet_node_key", definition.Key);

            if (definition.MinimumWidth > 0 || definition.MinimumHeight > 0)
            {
                control.CustomMinimumSize = new Vector2(definition.MinimumWidth, definition.MinimumHeight);
            }

            if (definition.SizeFlagsHorizontal != 0)
            {
                control.SizeFlagsHorizontal = (Control.SizeFlags)definition.SizeFlagsHorizontal;
            }

            if (definition.SizeFlagsVertical != 0)
            {
                control.SizeFlagsVertical = (Control.SizeFlags)definition.SizeFlagsVertical;
            }

            ApplyTextProperties(control, definition);
            ApplyContainerProperties(control, definition);
            return control;
        }

        private static void ApplyTextProperties(Control control, FacetGeneratedLayoutNodeDefinition definition)
        {
            switch (control)
            {
                case Label label:
                    label.Text = definition.Text ?? string.Empty;
                    if (definition.HorizontalAlignment.HasValue)
                    {
                        label.HorizontalAlignment = (HorizontalAlignment)definition.HorizontalAlignment.Value;
                    }

                    if (definition.WrapText)
                    {
                        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                    }

                    break;
                case Button button:
                    button.Text = definition.Text ?? string.Empty;
                    break;
            }
        }

        private static void ApplyContainerProperties(Control control, FacetGeneratedLayoutNodeDefinition definition)
        {
            switch (control)
            {
                case MarginContainer marginContainer:
                    marginContainer.AddThemeConstantOverride("margin_left", definition.MarginLeft);
                    marginContainer.AddThemeConstantOverride("margin_top", definition.MarginTop);
                    marginContainer.AddThemeConstantOverride("margin_right", definition.MarginRight);
                    marginContainer.AddThemeConstantOverride("margin_bottom", definition.MarginBottom);
                    break;
                case VBoxContainer vBoxContainer when definition.Separation != 0:
                    vBoxContainer.AddThemeConstantOverride("separation", definition.Separation);
                    break;
                case HBoxContainer hBoxContainer when definition.Separation != 0:
                    hBoxContainer.AddThemeConstantOverride("separation", definition.Separation);
                    break;
            }
        }

        private static string CreateNodeName(string key)
        {
            return key
                .Replace(".", "_", StringComparison.Ordinal)
                .Replace("/", "_", StringComparison.Ordinal)
                .Replace(":", "_", StringComparison.Ordinal)
                .Replace("-", "_", StringComparison.Ordinal);
        }
    }
}
