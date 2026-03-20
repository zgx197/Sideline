#nullable enable

namespace Sideline.Facet.Application.Diagnostics
{
    /// <summary>
    /// Facet 运行时探针记录状态快照。
    /// </summary>
    public sealed class FacetRuntimeProbeStatusSnapshot
    {
        public FacetRuntimeProbeStatusSnapshot(bool hasSnapshot, int recordedCount, FacetRuntimeProbeSnapshot? lastSnapshot)
        {
            HasSnapshot = hasSnapshot;
            RecordedCount = recordedCount;
            LastSnapshot = lastSnapshot;
        }

        /// <summary>
        /// 是否已有探针记录。
        /// </summary>
        public bool HasSnapshot { get; }

        /// <summary>
        /// 当前累计记录次数。
        /// </summary>
        public int RecordedCount { get; }

        /// <summary>
        /// 最近一次探针快照。
        /// </summary>
        public FacetRuntimeProbeSnapshot? LastSnapshot { get; }
    }
}
