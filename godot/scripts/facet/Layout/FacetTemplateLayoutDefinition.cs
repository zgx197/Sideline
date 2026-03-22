#nullable enable

using System;
using System.Collections.Generic;

namespace Sideline.Facet.Layout
{
    /// <summary>
    /// 模板布局定义。
    /// 先实例化一个固定模板，再把动态节点描述插入指定插槽。
    /// </summary>
    public sealed class FacetTemplateLayoutDefinition
    {
        public FacetTemplateLayoutDefinition(
            string layoutId,
            string templateScenePath,
            string contentSlotKey,
            IReadOnlyList<FacetGeneratedLayoutNodeDefinition> contentNodes)
        {
            if (string.IsNullOrWhiteSpace(layoutId))
            {
                throw new ArgumentException("Template layout id cannot be empty.", nameof(layoutId));
            }

            if (string.IsNullOrWhiteSpace(templateScenePath))
            {
                throw new ArgumentException("Template scene path cannot be empty.", nameof(templateScenePath));
            }

            if (string.IsNullOrWhiteSpace(contentSlotKey))
            {
                throw new ArgumentException("Template content slot key cannot be empty.", nameof(contentSlotKey));
            }

            ArgumentNullException.ThrowIfNull(contentNodes);

            LayoutId = layoutId;
            TemplateScenePath = templateScenePath;
            ContentSlotKey = contentSlotKey;
            ContentNodes = contentNodes;
        }

        public string LayoutId { get; }

        public string TemplateScenePath { get; }

        public string ContentSlotKey { get; }

        public IReadOnlyList<FacetGeneratedLayoutNodeDefinition> ContentNodes { get; }
    }
}
