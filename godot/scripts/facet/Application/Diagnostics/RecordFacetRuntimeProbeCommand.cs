#nullable enable

using System;

namespace Sideline.Facet.Application.Diagnostics
{
    /// <summary>
    /// 记录 Facet 运行时探针快照的命令。
    /// </summary>
    public sealed class RecordFacetRuntimeProbeCommand : ICommand
    {
        public RecordFacetRuntimeProbeCommand(FacetRuntimeProbeSnapshot snapshot)
        {
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        }

        /// <summary>
        /// 待记录的探针快照。
        /// </summary>
        public FacetRuntimeProbeSnapshot Snapshot { get; }
    }
}
