#nullable enable

using System;
using System.Collections.Generic;
using Godot;
using Sideline.Facet.Runtime;
using Sideline.Facet.UI;

namespace Sideline.Facet.Layout
{
    /// <summary>
    /// 模板布局提供者。
    /// 先加载模板场景，再把受限动态节点插入指定插槽，实现“固定壳体 + 动态内容”的混合布局。
    /// </summary>
    public sealed class TemplateLayoutProvider : IUILayoutProvider
    {
        private readonly IFacetTemplateLayoutStore _layoutStore;
        private readonly FacetDynamicNodeFactory _nodeFactory;
        private readonly IFacetLogger? _logger;

        public TemplateLayoutProvider(
            IFacetTemplateLayoutStore layoutStore,
            FacetDynamicNodeFactory nodeFactory,
            IFacetLogger? logger = null)
        {
            _layoutStore = layoutStore ?? throw new ArgumentNullException(nameof(layoutStore));
            _nodeFactory = nodeFactory ?? throw new ArgumentNullException(nameof(nodeFactory));
            _logger = logger;
        }

        public string Name => nameof(TemplateLayoutProvider);

        public bool CanLoad(UIPageDefinition definition)
        {
            ArgumentNullException.ThrowIfNull(definition);
            return definition.LayoutType == UIPageLayoutType.Template;
        }

        public UILayoutResult Load(UIPageDefinition definition, Node mountRoot)
        {
            ArgumentNullException.ThrowIfNull(definition);
            ArgumentNullException.ThrowIfNull(mountRoot);

            if (!_layoutStore.TryGet(definition.LayoutPath, out FacetTemplateLayoutDefinition? layoutDefinition) ||
                layoutDefinition == null)
            {
                throw new InvalidOperationException(
                    $"Facet template layout definition not found. pageId={definition.PageId}, layoutPath={definition.LayoutPath}");
            }

            PackedScene? packedScene = ResourceLoader.Load<PackedScene>(layoutDefinition.TemplateScenePath);
            if (packedScene == null)
            {
                throw new InvalidOperationException(
                    $"Facet template scene not found. pageId={definition.PageId}, templateScenePath={layoutDefinition.TemplateScenePath}");
            }

            Control rootNode = packedScene.Instantiate<Control>();
            rootNode.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            rootNode.GrowHorizontal = Control.GrowDirection.Both;
            rootNode.GrowVertical = Control.GrowDirection.Both;
            mountRoot.AddChild(rootNode);

            UINodeRegistry templateRegistry = UINodeRegistry.CreateFromSubtree(rootNode);
            UINodeResolver templateResolver = new(templateRegistry);
            Control slotNode = templateResolver.GetRequired<Control>(layoutDefinition.ContentSlotKey);
            InjectContent(slotNode, layoutDefinition.ContentNodes);

            UINodeRegistry finalRegistry = UINodeRegistry.CreateFromSubtree(rootNode);
            UINodeResolver finalResolver = new(finalRegistry);

            _logger?.Info(
                "UI.Layout",
                "TemplateLayoutProvider 已完成模板布局构建。",
                new Dictionary<string, object?>
                {
                    ["pageId"] = definition.PageId,
                    ["layoutType"] = definition.LayoutType.ToString(),
                    ["layoutPath"] = definition.LayoutPath,
                    ["templateScenePath"] = layoutDefinition.TemplateScenePath,
                    ["contentSlotKey"] = layoutDefinition.ContentSlotKey,
                    ["contentNodeCount"] = layoutDefinition.ContentNodes.Count,
                    ["registeredNodes"] = finalRegistry.Count,
                });

            return new UILayoutResult(rootNode, finalRegistry, finalResolver, ownsRootNode: true);
        }

        private void InjectContent(Control slotNode, IReadOnlyList<FacetGeneratedLayoutNodeDefinition> contentNodes)
        {
            foreach (FacetGeneratedLayoutNodeDefinition nodeDefinition in contentNodes)
            {
                Control contentRoot = _nodeFactory.CreateTree(nodeDefinition);
                slotNode.AddChild(contentRoot);
            }
        }
    }
}
