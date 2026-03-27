using System;

namespace Lattice.ECS.Session
{
    /// <summary>
    /// Session 运行时工厂。
    /// 用于根据稳定公开的运行时参数创建具体运行模式实例。
    /// </summary>
    public delegate SessionRuntime SessionRuntimeFactory(SessionRuntimeOptions runtimeOptions);

    /// <summary>
    /// Session 运行时上下文配置器。
    /// 用于在运行时实例创建后，为其上下文挂载共享对象。
    /// </summary>
    public delegate void SessionRuntimeContextConfigurator(SessionRuntimeContext context);

    /// <summary>
    /// SessionRunner 的生命周期状态。
    /// </summary>
    public enum SessionRunnerState
    {
        /// <summary>
        /// 已创建运行时，但尚未启动。
        /// </summary>
        Created = 0,

        /// <summary>
        /// 正在运行。
        /// </summary>
        Running = 1,

        /// <summary>
        /// 已停止，可再次启动。
        /// </summary>
        Stopped = 2,

        /// <summary>
        /// 生命周期执行失败，当前 runner 进入故障态。
        /// 该状态保留最后一次失败信息，供宿主显式诊断或恢复。
        /// </summary>
        Faulted = 3,

        /// <summary>
        /// 已释放，不可继续使用。
        /// </summary>
        Disposed = 4
    }
}
