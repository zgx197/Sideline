#nullable enable

using System.Threading;
using System.Threading.Tasks;

namespace Sideline.Facet.Application
{
    /// <summary>
    /// Facet 查询总线接口。
    /// </summary>
    public interface IQueryBus
    {
        /// <summary>
        /// 发送查询请求。
        /// </summary>
        ValueTask<AppResult<TResult>> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default);
    }
}
