#nullable enable

using System;

namespace Sideline.Facet.Runtime
{
    /// <summary>
    /// Facet 页面定义：描述一个 pageId 对应的布局来源、控制器与层级元数据。
    /// </summary>
    public sealed class UIPageDefinition
    {
        public UIPageDefinition(
            string pageId,
            UIPageLayoutType layoutType,
            string layoutPath,
            string layer,
            UIPageCachePolicy cachePolicy,
            string? controllerScript = null)
        {
            if (string.IsNullOrWhiteSpace(pageId))
            {
                throw new ArgumentException("PageId cannot be empty.", nameof(pageId));
            }

            if (string.IsNullOrWhiteSpace(layoutPath))
            {
                throw new ArgumentException("LayoutPath cannot be empty.", nameof(layoutPath));
            }

            if (string.IsNullOrWhiteSpace(layer))
            {
                throw new ArgumentException("Layer cannot be empty.", nameof(layer));
            }

            PageId = pageId;
            LayoutType = layoutType;
            LayoutPath = layoutPath;
            Layer = layer;
            CachePolicy = cachePolicy;
            ControllerScript = controllerScript;
        }

        public string PageId { get; }

        public UIPageLayoutType LayoutType { get; }

        public string LayoutPath { get; }

        public string Layer { get; }

        public UIPageCachePolicy CachePolicy { get; }

        public string? ControllerScript { get; }
    }
}