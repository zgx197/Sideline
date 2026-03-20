#nullable enable

using System;
using System.Collections.Generic;
using Godot;
using Sideline.Facet.Runtime;
using Sideline.Facet.UI;

namespace Sideline.Facet.Layout
{
    /// <summary>
    /// 基于 Godot 场景节点的布局提供者。
    /// 当前同时支持 ExistingNode 与 PackedScene 两种页面布局来源。
    /// </summary>
    public sealed class SceneLayoutProvider : IUILayoutProvider
    {
        private readonly IFacetLogger? _logger;

        public SceneLayoutProvider(IFacetLogger? logger = null)
        {
            _logger = logger;
        }

        /// <inheritdoc />
        public string Name => nameof(SceneLayoutProvider);

        /// <inheritdoc />
        public bool CanLoad(UIPageDefinition definition)
        {
            ArgumentNullException.ThrowIfNull(definition);
            return definition.LayoutType == UIPageLayoutType.ExistingNode || definition.LayoutType == UIPageLayoutType.PackedScene;
        }

        /// <inheritdoc />
        public UILayoutResult Load(UIPageDefinition definition, Node mountRoot)
        {
            ArgumentNullException.ThrowIfNull(definition);
            ArgumentNullException.ThrowIfNull(mountRoot);

            Control rootNode = definition.LayoutType switch
            {
                UIPageLayoutType.ExistingNode => LoadExistingNode(definition, mountRoot),
                UIPageLayoutType.PackedScene => LoadPackedScene(definition, mountRoot),
                _ => throw new InvalidOperationException($"Unsupported layout type for SceneLayoutProvider: {definition.LayoutType}"),
            };

            UINodeRegistry nodeRegistry = UINodeRegistry.CreateFromSubtree(rootNode);
            UINodeResolver nodeResolver = new(nodeRegistry);

            _logger?.Info(
                "UI.Layout",
                "SceneLayoutProvider 已构建页面布局与节点注册表。",
                new Dictionary<string, object?>
                {
                    ["pageId"] = definition.PageId,
                    ["layoutType"] = definition.LayoutType.ToString(),
                    ["layoutPath"] = definition.LayoutPath,
                    ["registeredNodes"] = nodeRegistry.Count,
                });

            return new UILayoutResult(rootNode, nodeRegistry, nodeResolver);
        }

        private static Control LoadExistingNode(UIPageDefinition definition, Node mountRoot)
        {
            Control? pageRoot = mountRoot.GetNodeOrNull<Control>(definition.LayoutPath);
            if (pageRoot == null)
            {
                throw new InvalidOperationException(
                    $"Facet existing page node not found. pageId={definition.PageId}, layoutPath={definition.LayoutPath}");
            }

            return pageRoot;
        }

        private static Control LoadPackedScene(UIPageDefinition definition, Node mountRoot)
        {
            PackedScene? packedScene = ResourceLoader.Load<PackedScene>(definition.LayoutPath);
            if (packedScene == null)
            {
                throw new InvalidOperationException(
                    $"Facet packed scene not found. pageId={definition.PageId}, layoutPath={definition.LayoutPath}");
            }

            Control pageRoot = packedScene.Instantiate<Control>();
            mountRoot.AddChild(pageRoot);
            return pageRoot;
        }
    }
}