#nullable enable

namespace Sideline.Facet.Extensions.RedDot
{
    /// <summary>
    /// 红点树某一路径的聚合快照。
    /// </summary>
    public sealed record RedDotNodeSnapshot(
        string Path,
        bool SelfHasRedDot,
        bool HasRedDot,
        int DirectChildCount,
        int ActiveChildCount,
        int SourceCount);
}
