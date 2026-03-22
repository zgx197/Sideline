#nullable enable

using System;
using System.Collections.Generic;

namespace Sideline.Facet.Runtime
{
    /// <summary>
    /// 基于内存列表的页面定义来源。
    /// </summary>
    public sealed class InMemoryPageDefinitionSource : IPageDefinitionSource
    {
        private readonly IReadOnlyList<UIPageDefinition> _definitions;

        public InMemoryPageDefinitionSource(IReadOnlyList<UIPageDefinition> definitions)
        {
            ArgumentNullException.ThrowIfNull(definitions);
            _definitions = definitions;
        }

        public IReadOnlyList<UIPageDefinition> LoadDefinitions()
        {
            return _definitions;
        }
    }
}