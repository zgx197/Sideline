using System;
using System.Collections.Generic;
using Lattice.ECS.Framework;
using Lattice.Math;

namespace Lattice.ECS.Session
{
    /// <summary>
    /// 会话运行器构建器。
    /// 为代码侧提供最小但正式的运行时装配入口。
    /// </summary>
    public sealed class SessionRunnerBuilder
    {
        private readonly List<ISystem> _systems = new();
        private Func<FP, int, Session>? _sessionFactory;
        private SessionRuntimeOptions _runtimeOptions = SessionRuntimeOptions.Default;

        /// <summary>
        /// 设置固定时间步长。
        /// </summary>
        public SessionRunnerBuilder WithDeltaTime(FP deltaTime)
        {
            _runtimeOptions = _runtimeOptions.WithDeltaTime(deltaTime);
            return this;
        }

        /// <summary>
        /// 设置本地玩家 ID。
        /// </summary>
        public SessionRunnerBuilder WithLocalPlayerId(int localPlayerId)
        {
            _runtimeOptions = _runtimeOptions.WithLocalPlayerId(localPlayerId);
            return this;
        }

        /// <summary>
        /// 设置运行时基础配置。
        /// 当前仅承载稳定公开的基础参数，不提前暴露历史窗口和采样策略等内部实现细节。
        /// </summary>
        public SessionRunnerBuilder WithRuntimeOptions(SessionRuntimeOptions runtimeOptions)
        {
            _runtimeOptions = runtimeOptions ?? throw new ArgumentNullException(nameof(runtimeOptions));
            return this;
        }

        /// <summary>
        /// 设置自定义会话工厂。
        /// </summary>
        public SessionRunnerBuilder WithSessionFactory(Func<FP, int, Session> sessionFactory)
        {
            _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
            return this;
        }

        /// <summary>
        /// 注册系统。
        /// </summary>
        public SessionRunnerBuilder AddSystem(ISystem system)
        {
            ArgumentNullException.ThrowIfNull(system);
            _systems.Add(system);
            return this;
        }

        /// <summary>
        /// 构建会话。
        /// </summary>
        public Session BuildSession()
        {
            Session session = _sessionFactory != null
                ? _sessionFactory(_runtimeOptions.DeltaTime, _runtimeOptions.LocalPlayerId)
                : new Session(_runtimeOptions);

            for (int i = 0; i < _systems.Count; i++)
            {
                session.RegisterSystem(_systems[i]);
            }

            return session;
        }

        /// <summary>
        /// 构建运行器。
        /// </summary>
        public SessionRunner Build()
        {
            return new SessionRunner(BuildSession());
        }
    }
}
