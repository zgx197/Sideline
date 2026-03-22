#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using Sideline.Facet.Application;
using Sideline.Facet.Application.Diagnostics;
using Sideline.Facet.Runtime;

namespace Sideline.Facet.Projection.Diagnostics
{
    /// <summary>
    /// 运行时探针 Projection 刷新器。
    /// </summary>
    public sealed class RuntimeProbeProjectionUpdater : IProjectionUpdater
    {
        private readonly FacetRuntimeContext _context;

        public RuntimeProbeProjectionUpdater(FacetRuntimeContext context)
        {
            _context = context;
        }

        public ProjectionKey Key => FacetProjectionKeys.RuntimeProbe;

        public async ValueTask RefreshAsync(CancellationToken cancellationToken = default)
        {
            AppResult<FacetRuntimeProbeStatusSnapshot> result = await _context.QueryBus.QueryAsync(new FacetRuntimeProbeStatusQuery(), cancellationToken);
            if (!result.IsSuccess || result.Value == null)
            {
                throw new InvalidOperationException($"Runtime probe projection refresh failed: {result.ErrorCode} {result.ErrorMessage}");
            }

            FacetRuntimeProbeStatusSnapshot status = result.Value;
            FacetRuntimeProbeSnapshot? snapshot = status.LastSnapshot;
            FacetRuntimeProbeProjection projection = new(
                sessionId: snapshot?.SessionId ?? string.Empty,
                hasSnapshot: status.HasSnapshot,
                recordedCount: status.RecordedCount,
                hotReloadEnabled: snapshot?.HotReloadEnabled ?? false,
                pageCacheEnabled: snapshot?.PageCacheEnabled ?? false,
                pageCacheCapacity: snapshot?.PageCacheCapacity ?? 0,
                updatedAtUtc: snapshot?.CapturedAtUtc ?? DateTimeOffset.UtcNow);

            _context.ProjectionStore.Set(Key, projection, "RuntimeProbeProjectionUpdater.Refresh");
        }
    }
}
