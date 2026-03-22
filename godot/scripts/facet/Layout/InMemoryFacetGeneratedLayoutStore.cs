#nullable enable

using System;
using System.Collections.Generic;

namespace Sideline.Facet.Layout
{
    /// <summary>
    /// 基于内存字典的自动布局定义仓库。
    /// </summary>
    public sealed class InMemoryFacetGeneratedLayoutStore : IFacetGeneratedLayoutStore
    {
        private readonly Dictionary<string, FacetGeneratedLayoutDefinition> _definitions;

        public InMemoryFacetGeneratedLayoutStore(IReadOnlyDictionary<string, FacetGeneratedLayoutDefinition> definitions)
        {
            ArgumentNullException.ThrowIfNull(definitions);

            _definitions = new Dictionary<string, FacetGeneratedLayoutDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, FacetGeneratedLayoutDefinition> entry in definitions)
            {
                _definitions[entry.Key] = entry.Value;
            }
        }

        public bool TryGet(string layoutId, out FacetGeneratedLayoutDefinition? definition)
        {
            return _definitions.TryGetValue(layoutId, out definition);
        }

        public IReadOnlyCollection<string> GetLayoutIds()
        {
            return _definitions.Keys;
        }
    }
}
