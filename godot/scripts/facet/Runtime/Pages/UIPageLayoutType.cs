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
    }
}