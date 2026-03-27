using System;
using System.Collections.Generic;
using Lattice.ECS.Framework;

namespace Lattice.ECS.Session
{
    /// <summary>
    /// SessionRunner 的不可变运行定义。
    /// 承载稳定公开的启动参数、运行时工厂以及系统装配信息。
    /// </summary>
    public sealed class SessionRunnerDefinition
    {
        private readonly SessionRuntimeFactory _runtimeFactory;
        private readonly ISystem[] _systems;
        private readonly SessionRuntimeContextConfigurator[] _contextConfigurators;
        private readonly SessionRunnerLifecycleHooks _lifecycleHooks;

        internal SessionRunnerDefinition(
            string runnerName,
            SessionRuntimeOptions runtimeOptions,
            SessionRuntimeFactory runtimeFactory,
            IReadOnlyList<ISystem> systems,
            IReadOnlyList<SessionRuntimeContextConfigurator> contextConfigurators,
            SessionRunnerLifecycleHooks lifecycleHooks)
        {
            if (string.IsNullOrWhiteSpace(runnerName))
            {
                throw new ArgumentException("Runner name cannot be null or whitespace.", nameof(runnerName));
            }

            RunnerName = runnerName;
            RuntimeOptions = runtimeOptions ?? throw new ArgumentNullException(nameof(runtimeOptions));
            _runtimeFactory = runtimeFactory ?? throw new ArgumentNullException(nameof(runtimeFactory));
            ArgumentNullException.ThrowIfNull(systems);
            ArgumentNullException.ThrowIfNull(contextConfigurators);

            _systems = new ISystem[systems.Count];
            if (_systems.Length > 0)
            {
                for (int i = 0; i < _systems.Length; i++)
                {
                    _systems[i] = systems[i] ?? throw new ArgumentNullException(nameof(systems), "System registration cannot contain null.");
                }
            }

            _contextConfigurators = new SessionRuntimeContextConfigurator[contextConfigurators.Count];
            for (int i = 0; i < _contextConfigurators.Length; i++)
            {
                _contextConfigurators[i] = contextConfigurators[i]
                    ?? throw new ArgumentNullException(nameof(contextConfigurators), "Context configurator cannot be null.");
            }

            _lifecycleHooks = (lifecycleHooks ?? throw new ArgumentNullException(nameof(lifecycleHooks))).Clone();
        }

        /// <summary>
        /// 运行器名称。
        /// 用于标识一个稳定的运行入口，而不是具体某一帧状态对象。
        /// </summary>
        public string RunnerName { get; }

        /// <summary>
        /// 稳定公开的运行时参数。
        /// </summary>
        public SessionRuntimeOptions RuntimeOptions { get; }

        /// <summary>
        /// 当前定义中注册的系统数量。
        /// </summary>
        public int SystemCount => _systems.Length;

        /// <summary>
        /// 当前定义中注册的上下文配置器数量。
        /// </summary>
        public int ContextConfiguratorCount => _contextConfigurators.Length;

        /// <summary>
        /// 按定义创建一个新的运行时实例并完成系统装配。
        /// </summary>
        public SessionRuntime BuildRuntime()
        {
            SessionRuntime runtime = _runtimeFactory(RuntimeOptions)
                ?? throw new InvalidOperationException("Session runtime factory returned null.");

            if (runtime.DeltaTime != RuntimeOptions.DeltaTime ||
                runtime.LocalPlayerId != RuntimeOptions.LocalPlayerId)
            {
                throw new InvalidOperationException(
                    "Session runtime factory returned a runtime whose stable public parameters do not match SessionRunnerDefinition.RuntimeOptions.");
            }

            runtime.BindContextRunnerName(RunnerName);

            for (int i = 0; i < _contextConfigurators.Length; i++)
            {
                _contextConfigurators[i](runtime.Context);
            }

            for (int i = 0; i < _systems.Length; i++)
            {
                runtime.RegisterSystem(_systems[i]);
            }

            return runtime;
        }

        /// <summary>
        /// 按定义创建运行器。
        /// </summary>
        public SessionRunner CreateRunner()
        {
            return new SessionRunner(this);
        }

        internal SessionRunnerLifecycleHooks LifecycleHooks => _lifecycleHooks;
    }
}
