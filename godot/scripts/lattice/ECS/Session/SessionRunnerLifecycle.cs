using System;

namespace Lattice.ECS.Session
{
    /// <summary>
    /// SessionRunner 的停止原因。
    /// 用于把宿主主动停止、重置停止和失败停止区分为正式生命周期语义。
    /// </summary>
    public enum SessionRunnerShutdownCause
    {
        /// <summary>
        /// 尚未发生正式停止。
        /// </summary>
        None = 0,

        /// <summary>
        /// 宿主显式调用 Stop()。
        /// </summary>
        HostStopRequested = 1,

        /// <summary>
        /// 因重建 runtime 而停止旧实例。
        /// </summary>
        RuntimeReset = 2,

        /// <summary>
        /// 因释放 runner 而停止。
        /// </summary>
        RunnerDisposed = 3,

        /// <summary>
        /// 启动阶段失败后进入关闭收敛。
        /// </summary>
        StartupFailure = 4,

        /// <summary>
        /// 固定帧推进失败后进入关闭收敛。
        /// </summary>
        StepFailure = 5
    }

    /// <summary>
    /// SessionRunner 的 runtime 重建原因。
    /// </summary>
    public enum SessionRunnerResetReason
    {
        /// <summary>
        /// 尚未发生正式重建。
        /// </summary>
        None = 0,

        /// <summary>
        /// 宿主显式调用 ResetRuntime()。
        /// </summary>
        HostRequested = 1
    }

    /// <summary>
    /// SessionRunner 的失败类别。
    /// </summary>
    public enum SessionRunnerFailureKind
    {
        /// <summary>
        /// 启动阶段失败。
        /// </summary>
        Start = 0,

        /// <summary>
        /// 固定帧推进失败。
        /// </summary>
        Step = 1,

        /// <summary>
        /// 停止阶段失败。
        /// </summary>
        Stop = 2,

        /// <summary>
        /// 重建阶段失败。
        /// </summary>
        Reset = 3,

        /// <summary>
        /// 生命周期钩子失败。
        /// </summary>
        LifecycleHook = 4,

        /// <summary>
        /// 释放阶段失败。
        /// </summary>
        Dispose = 5
    }

    /// <summary>
    /// SessionRunner 的最后一次失败快照。
    /// </summary>
    public sealed class SessionRunnerFailureInfo
    {
        public SessionRunnerFailureInfo(SessionRunnerFailureKind kind, string operationName, Exception exception)
        {
            if (string.IsNullOrWhiteSpace(operationName))
            {
                throw new ArgumentException("Operation name cannot be null or whitespace.", nameof(operationName));
            }

            Kind = kind;
            OperationName = operationName;
            Exception = exception ?? throw new ArgumentNullException(nameof(exception));
        }

        /// <summary>
        /// 失败类别。
        /// </summary>
        public SessionRunnerFailureKind Kind { get; }

        /// <summary>
        /// 失败所在操作名。
        /// </summary>
        public string OperationName { get; }

        /// <summary>
        /// 原始异常。
        /// </summary>
        public Exception Exception { get; }
    }

    /// <summary>
    /// SessionRunner 的宿主生命周期钩子集合。
    /// 仅用于补齐纯 C# 宿主边界，不引入额外异步或平台外壳。
    /// </summary>
    public sealed class SessionRunnerLifecycleHooks
    {
        /// <summary>
        /// 新 runtime 创建完成后调用。
        /// 此时 Context configurator 与系统装配已完成，适合挂接宿主级共享服务。
        /// </summary>
        public Action<SessionRunner, SessionRuntime>? RuntimeCreated { get; set; }

        /// <summary>
        /// 启动前调用。
        /// </summary>
        public Action<SessionRunner>? BeforeStart { get; set; }

        /// <summary>
        /// 启动成功后调用。
        /// </summary>
        public Action<SessionRunner>? AfterStart { get; set; }

        /// <summary>
        /// 停止前调用。
        /// </summary>
        public Action<SessionRunner, SessionRunnerShutdownCause>? BeforeStop { get; set; }

        /// <summary>
        /// 停止成功后调用。
        /// </summary>
        public Action<SessionRunner, SessionRunnerShutdownCause>? AfterStop { get; set; }

        /// <summary>
        /// 重建前调用。
        /// </summary>
        public Action<SessionRunner, SessionRunnerResetReason>? BeforeReset { get; set; }

        /// <summary>
        /// 重建成功后调用。
        /// </summary>
        public Action<SessionRunner, SessionRunnerResetReason>? AfterReset { get; set; }

        /// <summary>
        /// 生命周期失败后调用。
        /// </summary>
        public Action<SessionRunner, SessionRunnerFailureInfo>? Failure { get; set; }

        internal SessionRunnerLifecycleHooks Clone()
        {
            return new SessionRunnerLifecycleHooks
            {
                RuntimeCreated = RuntimeCreated,
                BeforeStart = BeforeStart,
                AfterStart = AfterStart,
                BeforeStop = BeforeStop,
                AfterStop = AfterStop,
                BeforeReset = BeforeReset,
                AfterReset = AfterReset,
                Failure = Failure
            };
        }
    }
}
