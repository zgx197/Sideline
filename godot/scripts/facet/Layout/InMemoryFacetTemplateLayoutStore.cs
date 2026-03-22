#nullable enable

using System;
using System.Collections.Generic;

namespace Sideline.Facet.Layout
{
    /// <summary>
    /// 基于内存字典的模板布局定义仓库。
    /// </summary>
    public sealed class InMemoryFacetTemplateLayoutStore : IFacetTemplateLayoutStore
    {
        private readonly Dictionary<string, FacetTemplateLayoutDefinition> _definitions;

        public InMemoryFacetTemplateLayoutStore(IReadOnlyDictionary<string, FacetTemplateLayoutDefinition> definitions)
        {
            ArgumentNullException.ThrowIfNull(definitions);

            _definitions = new Dictionary<string, FacetTemplateLayoutDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, FacetTemplateLayoutDefinition> entry in definitions)
            {
                _definitions[entry.Key] = entry.Value;
            }
        }

        public bool TryGet(string layoutId, out FacetTemplateLayoutDefinition? definition)
        {
            return _definitions.TryGetValue(layoutId, out definition);
        }

        public IReadOnlyCollection<string> GetLayoutIds()
        {
            return _definitions.Keys;
        }
    }
}
