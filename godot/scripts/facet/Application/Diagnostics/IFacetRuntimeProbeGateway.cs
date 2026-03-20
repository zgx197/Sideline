#nullable enable

using System.Threading;
using System.Threading.Tasks;

namespace Sideline.Facet.Application.Diagnostics
{
    /// <summary>
    /// Facet 运行时探针状态数据源。
    /// </summary>
    public interface IFacetRuntimeProbeGateway : IGateway
    {
        /// <summary>
        /// 记录一次运行时探针快照。
        /// </summary>
        ValueTask RecordAsync(FacetRuntimeProbeSnapshot snapshot, CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取最近一次记录的探针状态。
        /// </summary>
        ValueTask<FacetRuntimeProbeStatusSnapshot> GetStatusAsync(CancellationToken cancellationToken = default);
    }
}
