#nullable enable

using System;
using System.Collections.Generic;

namespace Sideline.Facet.Layout
{
    /// <summary>
    /// 自动布局节点描述。
    /// 通过稳定 key、有限节点类型和少量样式参数，表达一棵可运行时构建的 Control 树。
    /// </summary>
    public sealed class FacetGeneratedLayoutNodeDefinition
    {
        public FacetGeneratedLayoutNodeDefinition(
            string key,
            FacetLayoutNodeType nodeType,
            string? text = null,
            string? name = null,
            int minimumWidth = 0,
            int minimumHeight = 0,
            int sizeFlagsHorizontal = 0,
            int sizeFlagsVertical = 0,
            bool visible = true,
            int? horizontalAlignment = null,
            bool wrapText = false,
            int marginLeft = 0,
            int marginTop = 0,
            int marginRight = 0,
            int marginBottom = 0,
            int separation = 0,
            IReadOnlyList<FacetGeneratedLayoutNodeDefinition>? children = null)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Layout node key cannot be empty.", nameof(key));
            }

            Key = key;
            NodeType = nodeType;
            Text = text;
            Name = name;
            MinimumWidth = minimumWidth;
            MinimumHeight = minimumHeight;
            SizeFlagsHorizontal = sizeFlagsHorizontal;
            SizeFlagsVertical = sizeFlagsVertical;
            Visible = visible;
            HorizontalAlignment = horizontalAlignment;
            WrapText = wrapText;
            MarginLeft = marginLeft;
            MarginTop = marginTop;
            MarginRight = marginRight;
            MarginBottom = marginBottom;
            Separation = separation;
            Children = children ?? Array.Empty<FacetGeneratedLayoutNodeDefinition>();
        }

        public string Key { get; }

        public FacetLayoutNodeType NodeType { get; }

        public string? Text { get; }

        public string? Name { get; }

        public int MinimumWidth { get; }

        public int MinimumHeight { get; }

        public int SizeFlagsHorizontal { get; }

        public int SizeFlagsVertical { get; }

        public bool Visible { get; }

        public int? HorizontalAlignment { get; }

        public bool WrapText { get; }

        public int MarginLeft { get; }

        public int MarginTop { get; }

        public int MarginRight { get; }

        public int MarginBottom { get; }

        public int Separation { get; }

        public IReadOnlyList<FacetGeneratedLayoutNodeDefinition> Children { get; }
    }
}
