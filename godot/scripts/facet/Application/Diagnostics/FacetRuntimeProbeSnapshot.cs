#nullable enable

using System;

namespace Sideline.Facet.Application.Diagnostics
{
    /// <summary>
    /// Facet 运行时应用边界自检结果快照。
    /// </summary>
    public sealed class FacetRuntimeProbeSnapshot
    {
        public FacetRuntimeProbeSnapshot(
            string sessionId,
            bool hotReloadEnabled,
            bool pageCacheEnabled,
            int pageCacheCapacity,
            bool structuredLoggingEnabled,
            string structuredLogPath,
            bool commandBusRegistered,
            bool queryBusRegistered,
            DateTimeOffset capturedAtUtc)
        {
            SessionId = sessionId;
            HotReloadEnabled = hotReloadEnabled;
            PageCacheEnabled = pageCacheEnabled;
            PageCacheCapacity = pageCacheCapacity;
            StructuredLoggingEnabled = structuredLoggingEnabled;
            StructuredLogPath = structuredLogPath;
            CommandBusRegistered = commandBusRegistered;
            QueryBusRegistered = queryBusRegistered;
            CapturedAtUtc = capturedAtUtc;
        }

        /// <summary>
        /// 当前日志会话标识。
        /// </summary>
        public string SessionId { get; }

        /// <summary>
        /// 是否启用热更新。
        /// </summary>
        public bool HotReloadEnabled { get; }

        /// <summary>
        /// 是否启用页面缓存。
        /// </summary>
        public bool PageCacheEnabled { get; }

        /// <summary>
        /// 页面缓存容量。
        /// </summary>
        public int PageCacheCapacity { get; }

        /// <summary>
        /// 是否启用结构化日志。
        /// </summary>
        public bool StructuredLoggingEnabled { get; }

        /// <summary>
        /// 结构化日志路径。
        /// </summary>
        public string StructuredLogPath { get; }

        /// <summary>
        /// 命令总线是否已注册。
        /// </summary>
        public bool CommandBusRegistered { get; }

        /// <summary>
        /// 查询总线是否已注册。
        /// </summary>
        public bool QueryBusRegistered { get; }

        /// <summary>
        /// 快照采集时间（UTC）。
        /// </summary>
        public DateTimeOffset CapturedAtUtc { get; }
    }
}
