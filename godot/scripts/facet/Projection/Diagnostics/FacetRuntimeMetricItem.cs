#nullable enable

namespace Sideline.Facet.Projection.Diagnostics
{
    /// <summary>
    /// Facet 运行时指标条目。
    /// </summary>
    public sealed class FacetRuntimeMetricItem
    {
        public FacetRuntimeMetricItem(string key, string label, string value)
        {
            Key = key;
            Label = label;
            Value = value;
        }

        public string Key { get; }

        public string Label { get; }

        public string Value { get; }
    }
}
