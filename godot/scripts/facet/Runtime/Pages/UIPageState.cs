#nullable enable

namespace Sideline.Facet.Runtime
{
    /// <summary>
    /// 页面统一生命周期状态。
    /// </summary>
    public enum UIPageState
    {
        /// <summary>
        /// 页面运行时对象已创建，但尚未初始化。
        /// </summary>
        Created = 0,

        /// <summary>
        /// 页面已完成首次初始化。
        /// </summary>
        Initialized = 1,

        /// <summary>
        /// 页面当前处于显示状态。
        /// </summary>
        Shown = 2,

        /// <summary>
        /// 页面当前处于隐藏状态，可被缓存恢复。
        /// </summary>
        Hidden = 3,

        /// <summary>
        /// 页面已销毁，不可再恢复。
        /// </summary>
        Disposed = 4,
    }
}