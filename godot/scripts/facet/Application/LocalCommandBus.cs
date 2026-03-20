#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sideline.Facet.Runtime;

namespace Sideline.Facet.Application
{
    /// <summary>
    /// Facet 默认本地命令总线实现。
    /// 负责在宿主内部分发命令处理器，并统一返回 AppResult。
    /// </summary>
    public sealed class LocalCommandBus : ICommandBus
    {
        /// <summary>
        /// 不带返回值的命令处理器表。
        /// Key 为命令类型，Value 为运行时分发包装器。
        /// </summary>
        private readonly Dictionary<Type, Func<object, CancellationToken, ValueTask<AppResult>>> _commandHandlers = new();

        /// <summary>
        /// 带返回值的命令处理器表。
        /// </summary>
        private readonly Dictionary<Type, Func<object, CancellationToken, ValueTask<object>>> _commandResultHandlers = new();

        /// <summary>
        /// 可选日志器，用于记录注册和缺失处理器等诊断信息。
        /// </summary>
        private readonly IFacetLogger? _logger;

        /// <summary>
        /// 创建本地命令总线。
        /// </summary>
        public LocalCommandBus(IFacetLogger? logger = null)
        {
            _logger = logger;
        }

        /// <summary>
        /// 注册一个不带返回值的命令处理器。
        /// </summary>
        public void RegisterHandler<TCommand>(Func<TCommand, CancellationToken, ValueTask<AppResult>> handler)
            where TCommand : class, ICommand
        {
            ArgumentNullException.ThrowIfNull(handler);
            _commandHandlers[typeof(TCommand)] = (command, cancellationToken) => handler((TCommand)command, cancellationToken);
            _logger?.Debug("Application.CommandBus", $"Registered command handler: {typeof(TCommand).FullName}");
        }

        /// <summary>
        /// 注册一个带返回值的命令处理器。
        /// </summary>
        public void RegisterHandler<TCommand, TResult>(Func<TCommand, CancellationToken, ValueTask<AppResult<TResult>>> handler)
            where TCommand : class, ICommand<TResult>
        {
            ArgumentNullException.ThrowIfNull(handler);
            _commandResultHandlers[typeof(TCommand)] = async (command, cancellationToken) => await handler((TCommand)command, cancellationToken);
            _logger?.Debug("Application.CommandBus", $"Registered command handler with result: {typeof(TCommand).FullName}");
        }

        /// <summary>
        /// 发送一个不带返回值的命令。
        /// 若找不到处理器，则返回统一的失败结果而不是抛异常。
        /// </summary>
        public ValueTask<AppResult> SendAsync(ICommand command, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(command);

            if (_commandHandlers.TryGetValue(command.GetType(), out Func<object, CancellationToken, ValueTask<AppResult>>? handler))
            {
                return handler(command, cancellationToken);
            }

            _logger?.Warning("Application.CommandBus", $"No handler registered for command: {command.GetType().FullName}");
            return ValueTask.FromResult(AppResult.Fail("command.handler_missing", $"No handler registered for command: {command.GetType().FullName}"));
        }

        /// <summary>
        /// 发送一个带返回值的命令。
        /// </summary>
        public async ValueTask<AppResult<TResult>> SendAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(command);

            if (_commandResultHandlers.TryGetValue(command.GetType(), out Func<object, CancellationToken, ValueTask<object>>? handler))
            {
                return (AppResult<TResult>)await handler(command, cancellationToken);
            }

            _logger?.Warning("Application.CommandBus", $"No handler registered for command: {command.GetType().FullName}");
            return AppResult<TResult>.Fail("command.handler_missing", $"No handler registered for command: {command.GetType().FullName}");
        }
    }
}
