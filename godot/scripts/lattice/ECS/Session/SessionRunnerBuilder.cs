using System;
using System.Collections.Generic;
using System.ComponentModel;
using Lattice.ECS.Framework;
using Lattice.Math;

namespace Lattice.ECS.Session
{
    /// <summary>
    /// 会话运行器构建器。
    /// 为代码侧提供最小但正式的运行时装配入口。
    /// 当前构建目标是 `SessionRuntime`，默认实例为 `MinimalPredictionSession`。
    /// </summary>
    public sealed class SessionRunnerBuilder
    {
        private readonly List<ISystem> _systems = new();
        private readonly List<SessionRuntimeContextConfigurator> _contextConfigurators = new();
        private readonly SessionRunnerLifecycleHooks _lifecycleHooks = new();
        private SessionRuntimeFactory? _runtimeFactory;
        private SessionRuntimeOptions _runtimeOptions = SessionRuntimeOptions.Default;
        private string _runnerName = "Default";

        /// <summary>
        /// 设置运行器名称。
        /// </summary>
        public SessionRunnerBuilder WithRunnerName(string runnerName)
        {
            if (string.IsNullOrWhiteSpace(runnerName))
            {
                throw new ArgumentException("Runner name cannot be null or whitespace.", nameof(runnerName));
            }

            _runnerName = runnerName;
            return this;
        }

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
        /// 设置自定义运行时工厂。
        /// </summary>
        public SessionRunnerBuilder WithRuntimeFactory(SessionRuntimeFactory runtimeFactory)
        {
            _runtimeFactory = runtimeFactory ?? throw new ArgumentNullException(nameof(runtimeFactory));
            return this;
        }

        /// <summary>
        /// 设置自定义运行时工厂。
        /// 该重载保留旧的 `(deltaTime, localPlayerId)` 形式，内部会适配到 `SessionRuntimeOptions` 工厂。
        /// </summary>
        [Obsolete("请改用 WithRuntimeFactory(SessionRuntimeFactory)。WithSessionFactory 仅保留为兼容入口。", false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public SessionRunnerBuilder WithSessionFactory(Func<FP, int, SessionRuntime> sessionFactory)
        {
            ArgumentNullException.ThrowIfNull(sessionFactory);
            return WithRuntimeFactory(options => sessionFactory(options.DeltaTime, options.LocalPlayerId));
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
        /// 配置运行时上下文。
        /// 用于挂载显式声明的 runtime shared service，
        /// 而不是继续侵入 `Frame` / `SessionRuntime` 或把 Context 当成万能对象包。
        /// </summary>
        public SessionRunnerBuilder ConfigureContext(SessionRuntimeContextConfigurator configurator)
        {
            ArgumentNullException.ThrowIfNull(configurator);
            _contextConfigurators.Add(configurator);
            return this;
        }

        /// <summary>
        /// 配置宿主生命周期钩子。
        /// 用于在 runtime 创建、启动、停止、重建与失败收敛时补齐明确的宿主责任边界。
        /// </summary>
        public SessionRunnerBuilder ConfigureLifecycle(Action<SessionRunnerLifecycleHooks> configurator)
        {
            ArgumentNullException.ThrowIfNull(configurator);
            configurator(_lifecycleHooks);
            return this;
        }

        /// <summary>
        /// 构建不可变运行定义。
        /// </summary>
        public SessionRunnerDefinition BuildDefinition()
        {
            SessionRuntimeFactory runtimeFactory = _runtimeFactory ?? (options => new MinimalPredictionSession(options));
            return new SessionRunnerDefinition(_runnerName, _runtimeOptions, runtimeFactory, _systems, _contextConfigurators, _lifecycleHooks);
        }

        /// <summary>
        /// 构建运行时实例。
        /// </summary>
        public SessionRuntime BuildRuntime()
        {
            return BuildDefinition().BuildRuntime();
        }

        /// <summary>
        /// 构建运行时实例。
        /// 保留 `BuildSession()` 名称作为兼容入口。
        /// </summary>
        [Obsolete("请改用 BuildRuntime()。BuildSession 仅保留为兼容入口。", false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public SessionRuntime BuildSession()
        {
            return BuildRuntime();
        }

        /// <summary>
        /// 构建运行器。
        /// </summary>
        public SessionRunner Build()
        {
            return BuildDefinition().CreateRunner();
        }
    }
}
