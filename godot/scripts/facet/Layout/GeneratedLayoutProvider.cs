#nullable enable

using System;
using System.Collections.Generic;
using Godot;
using Sideline.Facet.Runtime;
using Sideline.Facet.UI;

namespace Sideline.Facet.Layout
{
    /// <summary>
    /// 自动生成布局提供者。
    /// 从配置化描述对象直接构建 Control 树，并复用统一节点注册协议。
    /// </summary>
    public sealed class GeneratedLayoutProvider : IUILayoutProvider
    {
        private readonly IFacetGeneratedLayoutStore _layoutStore;
        private readonly FacetDynamicNodeFactory _nodeFactory;
        private readonly IFacetLogger? _logger;

        public GeneratedLayoutProvider(
            IFacetGeneratedLayoutStore layoutStore,
            FacetDynamicNodeFactory nodeFactory,
            IFacetLogger? logger = null)
        {
            _layoutStore = layoutStore ?? throw new ArgumentNullException(nameof(layoutStore));
            _nodeFactory = nodeFactory ?? throw new ArgumentNullException(nameof(nodeFactory));
            _logger = logger;
        }

        public string Name => nameof(GeneratedLayoutProvider);

        public bool CanLoad(UIPageDefinition definition)
        {
            ArgumentNullException.ThrowIfNull(definition);
            return definition.LayoutType == UIPageLayoutType.Generated;
        }

        public UILayoutResult Load(UIPageDefinition definition, Node mountRoot)
        {
            ArgumentNullException.ThrowIfNull(definition);
            ArgumentNullException.ThrowIfNull(mountRoot);

            if (!_layoutStore.TryGet(definition.LayoutPath, out FacetGeneratedLayoutDefinition? layoutDefinition) ||
                layoutDefinition == null)
            {
                throw new InvalidOperationException(
                    $"Facet generated layout definition not found. pageId={definition.PageId}, layoutPath={definition.LayoutPath}");
            }

            Control rootNode = _nodeFactory.CreateTree(layoutDefinition.Root);
            rootNode.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            rootNode.GrowHorizontal = Control.GrowDirection.Both;
            rootNode.GrowVertical = Control.GrowDirection.Both;
            mountRoot.AddChild(rootNode);

            UINodeRegistry nodeRegistry = UINodeRegistry.CreateFromSubtree(rootNode);
            UINodeResolver nodeResolver = new(nodeRegistry);

            _logger?.Info(
                "UI.Layout",
                "GeneratedLayoutProvider 已构建动态页面布局。",
                new Dictionary<string, object?>
                {
                    ["pageId"] = definition.PageId,
                    ["layoutType"] = definition.LayoutType.ToString(),
                    ["layoutPath"] = definition.LayoutPath,
                    ["rootKey"] = layoutDefinition.Root.Key,
                    ["registeredNodes"] = nodeRegistry.Count,
                });

            return new UILayoutResult(rootNode, nodeRegistry, nodeResolver, ownsRootNode: true);
        }
    }
}
