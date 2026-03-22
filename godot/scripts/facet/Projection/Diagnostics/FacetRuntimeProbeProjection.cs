#nullable enable

using System;

namespace Sideline.Facet.Projection.Diagnostics
{
    /// <summary>
    /// Facet 运行时探针的 Projection 样例。
    /// 用于把宿主配置和诊断快照转换成页面可直接消费的只读视图模型。
    /// </summary>
    public sealed class FacetRuntimeProbeProjection : IViewModel
    {
        /// <summary>
        /// 创建运行时探针 Projection。
        /// </summary>
        public FacetRuntimeProbeProjection(
            string sessionId,
            bool hasSnapshot,
            int recordedCount,
            bool hotReloadEnabled,
            bool pageCacheEnabled,
            int pageCacheCapacity,
            DateTimeOffset updatedAtUtc)
        {
            SessionId = sessionId;
            HasSnapshot = hasSnapshot;
            RecordedCount = recordedCount;
            HotReloadEnabled = hotReloadEnabled;
            PageCacheEnabled = pageCacheEnabled;
            PageCacheCapacity = pageCacheCapacity;
            UpdatedAtUtc = updatedAtUtc;
        }

        /// <summary>
        /// 当前宿主会话标识。
        /// </summary>
        public string SessionId { get; }

        /// <summary>
        /// 是否已有探针快照被记录。
        /// </summary>
        public bool HasSnapshot { get; }

        /// <summary>
        /// 已记录的探针条数。
        /// </summary>
        public int RecordedCount { get; }

        /// <summary>
        /// 宿主是否启用了热更新能力。
        /// </summary>
        public bool HotReloadEnabled { get; }

        /// <summary>
        /// 是否启用了页面缓存。
        /// </summary>
        public bool PageCacheEnabled { get; }

        /// <summary>
        /// 页面缓存容量。
        /// </summary>
        public int PageCacheCapacity { get; }

        /// <summary>
        /// Projection 最后更新时间（UTC）。
        /// </summary>
        public DateTimeOffset UpdatedAtUtc { get; }
    }
}
