#nullable enable

using System;

namespace Sideline.Facet.Layout
{
    /// <summary>
    /// 自动生成布局定义。
    /// 一个 layoutId 对应一棵根节点描述树。
    /// </summary>
    public sealed class FacetGeneratedLayoutDefinition
    {
        public FacetGeneratedLayoutDefinition(string layoutId, FacetGeneratedLayoutNodeDefinition root)
        {
            if (string.IsNullOrWhiteSpace(layoutId))
            {
                throw new ArgumentException("Generated layout id cannot be empty.", nameof(layoutId));
            }

            ArgumentNullException.ThrowIfNull(root);
            LayoutId = layoutId;
            Root = root;
        }

        public string LayoutId { get; }

        public FacetGeneratedLayoutNodeDefinition Root { get; }
    }
}
