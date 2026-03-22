#nullable enable

using Sideline.Facet.Projection;

namespace Sideline.Facet.Projection.Diagnostics
{
    /// <summary>
    /// Facet 内置 Projection 主键集合。
    /// </summary>
    public static class FacetProjectionKeys
    {
        /// <summary>
        /// 运行时探针 Projection 主键。
        /// </summary>
        public static ProjectionKey RuntimeProbe { get; } = ProjectionKey.From("diagnostics.runtime_probe");

        /// <summary>
        /// 运行时指标列表 Projection 主键。
        /// </summary>
        public static ProjectionKey RuntimeMetrics { get; } = ProjectionKey.From("diagnostics.runtime_metrics");

        /// <summary>
        /// 客户端壳层页面状态 Projection 主键。
        /// </summary>
        public static ProjectionKey ClientShell { get; } = ProjectionKey.From("client.shell");
    }
}
