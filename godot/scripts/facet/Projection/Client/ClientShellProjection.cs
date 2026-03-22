#nullable enable

namespace Sideline.Facet.Projection.Client
{
    /// <summary>
    /// 客户端壳层页面状态 Projection。
    /// </summary>
    public sealed class ClientShellProjection : IViewModel
    {
        public ClientShellProjection(
            string title,
            string status,
            string primaryActionLabel,
            string mode,
            bool isPrimaryActionEnabled,
            bool showRuntimeSummary,
            bool showMetricsList)
        {
            Title = title;
            Status = status;
            PrimaryActionLabel = primaryActionLabel;
            Mode = mode;
            IsPrimaryActionEnabled = isPrimaryActionEnabled;
            ShowRuntimeSummary = showRuntimeSummary;
            ShowMetricsList = showMetricsList;
        }

        public string Title { get; }

        public string Status { get; }

        public string PrimaryActionLabel { get; }

        public string Mode { get; }

        public bool IsPrimaryActionEnabled { get; }

        public bool ShowRuntimeSummary { get; }

        public bool ShowMetricsList { get; }
    }
}
