#nullable enable

namespace Sideline.Facet.Extensions.RedDot
{
    /// <summary>
    /// 红点原始条目。
    /// 由 Provider 输出，再由 RedDotAggregator 聚合为完整红点树快照。
    /// </summary>
    public sealed record RedDotStateEntry(
        string Path,
        bool HasRedDot,
        string SourceId);
}
