#nullable enable

using System;
using System.Collections.Generic;
using Sideline.Facet.Projection;
using Sideline.Facet.Projection.Diagnostics;
using Sideline.Facet.Runtime;

namespace Sideline.Facet.Extensions.RedDot
{
    /// <summary>
    /// 閸╄桨绨?Facet 鏉╂劘顢戦弮?Projection 閻ㄥ嫮瀛╅悙?Provider閵?    /// 瑜版挸澧犻崗鍫㈡暏鏉╂劘顢戦弮鑸靛赴闁藉牅绗岄幐鍥ㄧ垼 Projection 閺嬪嫰鈧娀妯佸▓?10 閻ㄥ嫮婀＄€圭偞鐗辨笟瀣熅瀵板嫨鈧?    /// </summary>
    public sealed class FacetRuntimeRedDotProvider : IRedDotProvider
    {
        private readonly ProjectionStore _projectionStore;
        private readonly IFacetLogger? _logger;
        private readonly IDisposable _runtimeProbeSubscription;
        private readonly IDisposable _runtimeMetricsSubscription;
        private FacetRuntimeProbeProjection? _runtimeProbeProjection;
        private FacetRuntimeMetricListProjection? _runtimeMetricsProjection;
        private bool _disposed;

        public FacetRuntimeRedDotProvider(ProjectionStore projectionStore, IFacetLogger? logger = null)
        {
            ArgumentNullException.ThrowIfNull(projectionStore);

            _projectionStore = projectionStore;
            _logger = logger;
            _projectionStore.TryGet(FacetProjectionKeys.RuntimeProbe, out _runtimeProbeProjection);
            _projectionStore.TryGet(FacetProjectionKeys.RuntimeMetrics, out _runtimeMetricsProjection);
            _runtimeProbeSubscription = _projectionStore.Subscribe(FacetProjectionKeys.RuntimeProbe, OnRuntimeProbeChanged);
            _runtimeMetricsSubscription = _projectionStore.Subscribe(FacetProjectionKeys.RuntimeMetrics, OnRuntimeMetricsChanged);
        }

        public string ProviderId => "facet.runtime";

        public event Action? Changed;

        public IReadOnlyList<RedDotStateEntry> GetEntries()
        {
            List<RedDotStateEntry> entries = new()
            {
                new RedDotStateEntry("client.idle", false, ProviderId),
                new RedDotStateEntry("client.dungeon", false, ProviderId),
                new RedDotStateEntry("client.idle.runtime", _runtimeProbeProjection?.HasSnapshot == true, ProviderId),
                new RedDotStateEntry("client.idle.runtime.records", (_runtimeProbeProjection?.RecordedCount ?? 0) > 0, ProviderId),
                new RedDotStateEntry("client.idle.runtime.hot_reload", _runtimeProbeProjection?.HotReloadEnabled == true, ProviderId),
                new RedDotStateEntry("client.dungeon.metrics", (_runtimeMetricsProjection?.Items.Count ?? 0) > 0, ProviderId),
                new RedDotStateEntry("client.dungeon.metrics.list", (_runtimeMetricsProjection?.Items.Count ?? 0) > 0, ProviderId),
            };

            return entries;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _runtimeProbeSubscription.Dispose();
            _runtimeMetricsSubscription.Dispose();
            Changed = null;
            _runtimeProbeProjection = null;
            _runtimeMetricsProjection = null;
            _disposed = true;
        }

        private void OnRuntimeProbeChanged(ProjectionChange change)
        {
            _runtimeProbeProjection = change.CurrentValue as FacetRuntimeProbeProjection;

            _logger?.Debug(
                "RedDot.Provider",
                "Runtime probe red dot source updated.",
                new Dictionary<string, object?>
                {
                    ["providerId"] = ProviderId,
                    ["hasSnapshot"] = _runtimeProbeProjection?.HasSnapshot ?? false,
                    ["recordedCount"] = _runtimeProbeProjection?.RecordedCount ?? 0,
                    ["hotReloadEnabled"] = _runtimeProbeProjection?.HotReloadEnabled ?? false,
                    ["changeKind"] = change.Kind.ToString(),
                });

            Changed?.Invoke();
        }

        private void OnRuntimeMetricsChanged(ProjectionChange change)
        {
            _runtimeMetricsProjection = change.CurrentValue as FacetRuntimeMetricListProjection;

            _logger?.Debug(
                "RedDot.Provider",
                "Runtime metrics red dot source updated.",
                new Dictionary<string, object?>
                {
                    ["providerId"] = ProviderId,
                    ["metricCount"] = _runtimeMetricsProjection?.Items.Count ?? 0,
                    ["changeKind"] = change.Kind.ToString(),
                });

            Changed?.Invoke();
        }
    }
}
