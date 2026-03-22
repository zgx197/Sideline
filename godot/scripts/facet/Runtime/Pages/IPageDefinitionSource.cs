#nullable enable

using System.Collections.Generic;

namespace Sideline.Facet.Runtime
{
    /// <summary>
    /// 页面定义来源。
    /// 阶段 4 先支持内存定义，后续再扩展到 JSON 或其他配置文件。
    /// </summary>
    public interface IPageDefinitionSource
    {
        IReadOnlyList<UIPageDefinition> LoadDefinitions();
    }
}