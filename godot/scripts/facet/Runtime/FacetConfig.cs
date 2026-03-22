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
        /// 是否启用结构化日志。
        /// </summary>
        public bool EnableStructuredLogging { get; init; } = true;

        /// <summary>
        /// 结构化日志输出路径。
        /// </summary>
        public string StructuredLogPath { get; init; } = "user://logs/facet-structured.jsonl";

        /// <summary>
        /// 内存缓冲中保留的最近日志上限。
        /// </summary>
        public int StructuredLogBufferCapacity { get; init; } = 256;

        /// <summary>
        /// 结构化日志历史会话保留数量。
        /// </summary>
        public int StructuredLogHistoryLimit { get; init; } = 10;

        /// <summary>
        /// 是否启用纯文本镜像日志。
        /// 该日志独立于 Godot 自身的 godot.log，便于运行时直接调试查看。
        /// </summary>
        public bool EnableConsoleMirrorLogging { get; init; } = true;

        /// <summary>
        /// 纯文本镜像日志输出路径。
        /// </summary>
        public string ConsoleMirrorLogPath { get; init; } = "user://logs/facet-console.log";

        /// <summary>
        /// 纯文本镜像日志历史会话保留数量。
        /// </summary>
        public int ConsoleMirrorLogHistoryLimit { get; init; } = 10;

        /// <summary>
        /// 是否启用 Lua 热重载。
        /// </summary>
        public bool EnableHotReload { get; init; } = true;

        /// <summary>
        /// Lua 热重载轮询间隔，单位秒。
        /// </summary>
        public double HotReloadPollIntervalSeconds { get; init; } = 0.5d;

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