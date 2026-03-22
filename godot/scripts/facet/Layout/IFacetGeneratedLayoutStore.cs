#nullable enable

using System.Collections.Generic;

namespace Sideline.Facet.Layout
{
    /// <summary>
    /// 自动布局定义仓库。
    /// 当前阶段先用内存字典承载，后续可替换为外部文件或工具生成产物。
    /// </summary>
    public interface IFacetGeneratedLayoutStore
    {
        bool TryGet(string layoutId, out FacetGeneratedLayoutDefinition? definition);

        IReadOnlyCollection<string> GetLayoutIds();
    }
}
