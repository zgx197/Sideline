#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sideline.Facet.Runtime;

namespace Sideline.Facet.Application
{
    /// <summary>
    /// Facet 默认本地查询总线实现。
    /// 负责在宿主内部分发查询处理器，并统一返回 AppResult。
    /// </summary>
    public sealed class LocalQueryBus : IQueryBus
    {
        /// <summary>
        /// 查询处理器表。
        /// Key 为查询类型，Value 为统一包装后的运行时委托。
        /// </summary>
        private readonly Dictionary<Type, Func<object, CancellationToken, ValueTask<object>>> _queryHandlers = new();

        /// <summary>
        /// 可选日志器，用于记录处理器注册与缺失告警。
        /// </summary>
        private readonly IFacetLogger? _logger;

        /// <summary>
        /// 创建本地查询总线。
        /// </summary>
        public LocalQueryBus(IFacetLogger? logger = null)
        {
            _logger = logger;
        }

        /// <summary>
        /// 注册一个查询处理器。
        /// </summary>
        public void RegisterHandler<TQuery, TResult>(Func<TQuery, CancellationToken, ValueTask<AppResult<TResult>>> handler)
            where TQuery : class, IQuery<TResult>
        {
            ArgumentNullException.ThrowIfNull(handler);
            _queryHandlers[typeof(TQuery)] = async (query, cancellationToken) => await handler((TQuery)query, cancellationToken);
            _logger?.Debug("Application.QueryBus", $"Registered query handler: {typeof(TQuery).FullName}");
        }

        /// <summary>
        /// 执行一次查询。
        /// 若查询未注册处理器，则返回统一失败结果。
        /// </summary>
        public async ValueTask<AppResult<TResult>> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(query);

            if (_queryHandlers.TryGetValue(query.GetType(), out Func<object, CancellationToken, ValueTask<object>>? handler))
            {
                return (AppResult<TResult>)await handler(query, cancellationToken);
            }

            _logger?.Warning("Application.QueryBus", $"No handler registered for query: {query.GetType().FullName}");
            return AppResult<TResult>.Fail("query.handler_missing", $"No handler registered for query: {query.GetType().FullName}");
        }
    }
}
