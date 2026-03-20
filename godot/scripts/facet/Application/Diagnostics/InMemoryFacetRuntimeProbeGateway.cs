#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Sideline.Facet.Application.Diagnostics
{
    /// <summary>
    /// Facet 运行时探针状态的本地内存网关实现。
    /// </summary>
    public sealed class InMemoryFacetRuntimeProbeGateway : IFacetRuntimeProbeGateway
    {
        private FacetRuntimeProbeSnapshot? _lastSnapshot;
        private int _recordedCount;

        public ValueTask RecordAsync(FacetRuntimeProbeSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            cancellationToken.ThrowIfCancellationRequested();

            _lastSnapshot = snapshot;
            _recordedCount++;
            return ValueTask.CompletedTask;
        }

        public ValueTask<FacetRuntimeProbeStatusSnapshot> GetStatusAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            FacetRuntimeProbeStatusSnapshot status = new(
                hasSnapshot: _lastSnapshot != null,
                recordedCount: _recordedCount,
                lastSnapshot: _lastSnapshot);

            return ValueTask.FromResult(status);
        }
    }
}
