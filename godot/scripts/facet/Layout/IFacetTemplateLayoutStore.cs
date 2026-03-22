#nullable enable

using System.Collections.Generic;

namespace Sideline.Facet.Layout
{
    /// <summary>
    /// 模板布局定义仓库。
    /// </summary>
    public interface IFacetTemplateLayoutStore
    {
        bool TryGet(string layoutId, out FacetTemplateLayoutDefinition? definition);

        IReadOnlyCollection<string> GetLayoutIds();
    }
}
