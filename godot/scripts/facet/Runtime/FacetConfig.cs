#nullable enable

namespace Sideline.Facet.Runtime
{
    /// <summary>
    /// Facet 运行时配置。
    /// </summary>
    public sealed class FacetConfig
    {
        /// <summary>
        /// 默认配置。
        /// </summary>
        public static FacetConfig Default { get; } = new();

        /// <summary>
        /// 是否启用调试日志。
        /// </summary>
        public bool EnableDebugLogging { get; init; } = true;

        /// <summary>
        /// 是否启用 Lua 热重载。
        /// </summary>
        public bool EnableHotReload { get; init; } = true;

        /// <summary>
        /// 默认是否启用页面缓存。
        /// </summary>
        public bool EnablePageCacheByDefault { get; init; } = true;

        /// <summary>
        /// 默认页面缓存上限。
        /// </summary>
        public int DefaultPageCacheCapacity { get; init; } = 8;

        /// <summary>
        /// 最小日志级别。
        /// </summary>
        public FacetLogLevel MinimumLogLevel
        {
            get
            {
                return EnableDebugLogging ? FacetLogLevel.Debug : FacetLogLevel.Info;
            }
        }
    }
}