#nullable enable

using System.Threading;
using System.Threading.Tasks;

namespace Sideline.Facet.Projection
{
    /// <summary>
    /// Projection 刷新器接口。
    /// </summary>
    public interface IProjectionUpdater
    {
        /// <summary>
        /// 当前刷新器负责的投影主键。
        /// </summary>
        ProjectionKey Key { get; }

        /// <summary>
        /// 刷新投影数据。
        /// </summary>
        ValueTask RefreshAsync(CancellationToken cancellationToken = default);
    }
}
