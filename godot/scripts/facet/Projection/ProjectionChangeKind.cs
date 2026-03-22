#nullable enable

namespace Sideline.Facet.Projection
{
    /// <summary>
    /// Projection 变更类型。
    /// 当前只区分写入更新与移除两种基础事件。
    /// </summary>
    public enum ProjectionChangeKind
    {
        /// <summary>
        /// 表示某个 Projection 被设置或更新。
        /// </summary>
        Set = 0,

        /// <summary>
        /// 表示某个 Projection 被移除。
        /// </summary>
        Removed = 1,
    }
}
