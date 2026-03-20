#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sideline.Facet.Application;
using Sideline.Facet.Application.Diagnostics;
using Sideline.Facet.Runtime;

namespace Sideline.Facet.Projection.Diagnostics
{
    /// <summary>
    /// 运行时指标列表 Projection 刷新器。
    /// </summary>
    public sealed class RuntimeMetricsProjectionUpdater : IProjectionUpdater
    {
        private readonly FacetRuntimeContext _context;

        public RuntimeMetricsProjectionUpdater(FacetRuntimeContext context)
        {
            _context = context;
        }

        public ProjectionKey Key => FacetProjectionKeys.RuntimeMetrics;

        public async ValueTask RefreshAsync(CancellationToken cancellationToken = default)
        {
            AppResult<FacetRuntimeProbeStatusSnapshot> result = await _context.QueryBus.QueryAsync(new FacetRuntimeProbeStatusQuery(), cancellationToken);
            if (!result.IsSuccess || result.Value == null)
            {
                throw new InvalidOperationException($"Runtime metrics projection refresh failed: {result.ErrorCode} {result.ErrorMessage}");
            }

            FacetRuntimeProbeStatusSnapshot status = result.Value;
            FacetRuntimeProbeSnapshot? snapshot = status.LastSnapshot;
            string shortSessionId = string.IsNullOrWhiteSpace(snapshot?.SessionId)
                ? "<none>"
                : snapshot!.SessionId.Length <= 8
                    ? snapshot.SessionId
                    : snapshot.SessionId[..8];

            List<FacetRuntimeMetricItem> items = new()
            {
                new FacetRuntimeMetricItem("session", "Session", shortSessionId),
                new FacetRuntimeMetricItem("records", "Records", status.RecordedCount.ToString()),
                new FacetRuntimeMetricItem("hot_reload", "Reload", snapshot?.HotReloadEnabled == true ? "On" : "Off"),
                new FacetRuntimeMetricItem("page_cache", "Cache", snapshot?.PageCacheEnabled == true ? $"On ({snapshot.PageCacheCapacity})" : "Off"),
            };

            FacetRuntimeMetricListProjection projection = new(
                title: "运行时指标 / Runtime Metrics",
                items: items,
                updatedAtUtc: snapshot?.CapturedAtUtc ?? DateTimeOffset.UtcNow);

            _context.ProjectionStore.Set(Key, projection, "RuntimeMetricsProjectionUpdater.Refresh");
        }
    }
}
