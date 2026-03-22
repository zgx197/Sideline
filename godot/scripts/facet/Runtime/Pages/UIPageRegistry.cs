#nullable enable

using System;
using System.Collections.Generic;

namespace Sideline.Facet.Runtime
{
    /// <summary>
    /// 页面注册表：负责管理 pageId 与页面定义之间的映射关系。
    /// </summary>
    public sealed class UIPageRegistry
    {
        private readonly Dictionary<string, UIPageDefinition> _definitions = new(StringComparer.OrdinalIgnoreCase);

        public int Count => _definitions.Count;

        public IReadOnlyCollection<UIPageDefinition> GetAll()
        {
            return _definitions.Values;
        }

        public int RegisterRange(IPageDefinitionSource source)
        {
            ArgumentNullException.ThrowIfNull(source);

            int registeredCount = 0;
            foreach (UIPageDefinition definition in source.LoadDefinitions())
            {
                Register(definition);
                registeredCount++;
            }

            return registeredCount;
        }

        public void Register(UIPageDefinition definition)
        {
            ArgumentNullException.ThrowIfNull(definition);
            _definitions[definition.PageId] = definition;
        }

        public bool Contains(string pageId)
        {
            return _definitions.ContainsKey(pageId);
        }

        public bool TryGet(string pageId, out UIPageDefinition? definition)
        {
            return _definitions.TryGetValue(pageId, out definition);
        }

        public UIPageDefinition GetRequired(string pageId)
        {
            if (TryGet(pageId, out UIPageDefinition? definition) && definition != null)
            {
                return definition;
            }

            throw new InvalidOperationException($"Facet page definition not registered: {pageId}");
        }
    }
}