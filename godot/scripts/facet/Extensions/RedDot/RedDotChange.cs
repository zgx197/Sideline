#nullable enable

namespace Sideline.Facet.Extensions.RedDot
{
    /// <summary>
    /// 红点路径变更事件。
    /// </summary>
    public sealed record RedDotChange(
        string Path,
        RedDotNodeSnapshot? Previous,
        RedDotNodeSnapshot? Current,
        string Source);
}
