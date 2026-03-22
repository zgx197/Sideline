#nullable enable

using System;
using System.Collections.Generic;
using Godot;
using Sideline.Facet.Layout;

namespace Sideline.Facet.Runtime
{
    /// <summary>
    /// 页面加载器：通过布局提供者把页面定义转换成统一布局结果。
    /// </summary>
    public sealed class UIPageLoader
    {
        private readonly IReadOnlyList<IUILayoutProvider> _layoutProviders;
        private readonly IFacetLogger? _logger;

        public UIPageLoader(IReadOnlyList<IUILayoutProvider> layoutProviders, IFacetLogger? logger = null)
        {
            ArgumentNullException.ThrowIfNull(layoutProviders);
            _layoutProviders = layoutProviders;
            _logger = logger;
        }

        public UILayoutResult Load(UIPageDefinition definition, Node mountRoot)
        {
            ArgumentNullException.ThrowIfNull(definition);
            ArgumentNullException.ThrowIfNull(mountRoot);

            IUILayoutProvider provider = GetRequiredProvider(definition);
            UILayoutResult result = provider.Load(definition, mountRoot);

            _logger?.Info(
                "UI.Page",
                "页面布局加载完成。",
                new Dictionary<string, object?>
                {
                    ["pageId"] = definition.PageId,
                    ["layoutType"] = definition.LayoutType.ToString(),
                    ["layoutPath"] = definition.LayoutPath,
                    ["layer"] = definition.Layer,
                    ["layoutProvider"] = provider.Name,
                    ["nodePath"] = result.RootNode.GetPath().ToString(),
                    ["registeredNodes"] = result.NodeRegistry.Count,
                });

            return result;
        }

        private IUILayoutProvider GetRequiredProvider(UIPageDefinition definition)
        {
            foreach (IUILayoutProvider provider in _layoutProviders)
            {
                if (provider.CanLoad(definition))
                {
                    return provider;
                }
            }

            throw new InvalidOperationException(
                $"Facet layout provider not found. pageId={definition.PageId}, layoutType={definition.LayoutType}");
        }
    }
}