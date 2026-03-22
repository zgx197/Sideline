#nullable enable

using System.Threading;
using System.Threading.Tasks;

namespace Sideline.Facet.Application
{
    /// <summary>
    /// Facet 命令总线接口。
    /// </summary>
    public interface ICommandBus
    {
        /// <summary>
        /// 发送无返回值命令。
        /// </summary>
        ValueTask<AppResult> SendAsync(ICommand command, CancellationToken cancellationToken = default);

        /// <summary>
        /// 发送带返回值命令。
        /// </summary>
        ValueTask<AppResult<TResult>> SendAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default);
    }
}
