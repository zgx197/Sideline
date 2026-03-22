#nullable enable

namespace Sideline.Facet.Runtime
{
    /// <summary>
    /// 页面布局来源类型。
    /// </summary>
    public enum UIPageLayoutType
    {
        /// <summary>
        /// 页面节点已经存在于当前场景中，运行时只负责按路径解析。
        /// </summary>
        ExistingNode = 0,

        /// <summary>
        /// 页面通过 PackedScene 在运行时实例化。
        /// </summary>
        PackedScene = 1,

        /// <summary>
        /// 页面先加载固定模板，再把动态描述内容插入模板插槽。
        /// </summary>
        Template = 2,

        /// <summary>
        /// 页面完全由布局描述对象在运行时生成。
        /// </summary>
        Generated = 3,
    }
}
