#nullable enable

using System;
using System.Collections.Generic;

namespace Sideline.Facet.Projection.Diagnostics
{
    /// <summary>
    /// Facet 运行时指标列表 Projection。
    /// </summary>
    public sealed class FacetRuntimeMetricListProjection : IViewModel
    {
        public FacetRuntimeMetricListProjection(string title, IReadOnlyList<FacetRuntimeMetricItem> items, DateTimeOffset updatedAtUtc)
        {
            Title = title;
            Items = items;
            UpdatedAtUtc = updatedAtUtc;
        }

        public string Title { get; }

        public IReadOnlyList<FacetRuntimeMetricItem> Items { get; }

        public DateTimeOffset UpdatedAtUtc { get; }
    }
}
