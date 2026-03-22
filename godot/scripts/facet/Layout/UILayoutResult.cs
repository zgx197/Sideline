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
        public UILayoutResult(Control rootNode, UINodeRegistry nodeRegistry, UINodeResolver nodeResolver, bool ownsRootNode)
        {
            ArgumentNullException.ThrowIfNull(rootNode);
            ArgumentNullException.ThrowIfNull(nodeRegistry);
            ArgumentNullException.ThrowIfNull(nodeResolver);

            RootNode = rootNode;
            NodeRegistry = nodeRegistry;
            NodeResolver = nodeResolver;
            OwnsRootNode = ownsRootNode;
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

        /// <summary>
        /// 当前布局结果是否拥有根节点生命周期。
        /// ExistingNode 这类外部现有节点不应由运行时释放；运行时创建的新根节点则应在销毁时释放。
        /// </summary>
        public bool OwnsRootNode { get; }
    }
}
