#nullable enable

using System;
using Godot;
using Sideline.Facet.UI;

namespace Sideline.Facet.Layout
{
    /// <summary>
    /// 统一布局加载结果。
    /// </summary>
    public sealed class UILayoutResult
    {
        public UILayoutResult(Control rootNode, UINodeRegistry nodeRegistry, UINodeResolver nodeResolver)
        {
            ArgumentNullException.ThrowIfNull(rootNode);
            ArgumentNullException.ThrowIfNull(nodeRegistry);
            ArgumentNullException.ThrowIfNull(nodeResolver);

            RootNode = rootNode;
            NodeRegistry = nodeRegistry;
            NodeResolver = nodeResolver;
        }

        /// <summary>
        /// 页面根节点。
        /// </summary>
        public Control RootNode { get; }

        /// <summary>
        /// 当前页面的节点注册表。
        /// </summary>
        public UINodeRegistry NodeRegistry { get; }

        /// <summary>
        /// 当前页面的节点解析器。
        /// </summary>
        public UINodeResolver NodeResolver { get; }
    }
}