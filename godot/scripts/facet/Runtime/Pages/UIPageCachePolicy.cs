#nullable enable

namespace Sideline.Facet.Runtime
{
    /// <summary>
    /// 页面缓存策略。
    /// </summary>
    public enum UIPageCachePolicy
    {
        /// <summary>
        /// 页面关闭后继续保留实例，后续重复打开时直接复用。
        /// </summary>
        Reuse = 0,

        /// <summary>
        /// 页面关闭后不保证保留实例。
        /// </summary>
        NoCache = 1,
    }
}