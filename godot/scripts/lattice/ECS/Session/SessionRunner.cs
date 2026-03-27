using System;
using System.ComponentModel;

namespace Lattice.ECS.Session
{
    /// <summary>
    /// 会话运行器。
    /// 为纯 C# 环境提供统一的 Start / Step / Stop 驱动入口。
    /// 当前面向 `SessionRuntime` 工作，而不是绑定某一个具体运行模式。
    /// </summary>
    public sealed class SessionRunner : IDisposable
    {
        private bool _disposed;
        private readonly bool _ownsRuntime;
        private readonly SessionRunnerLifecycleHooks _lifecycleHooks;

        /// <summary>
        /// 当前运行定义。
        /// 由 `SessionRunnerBuilder` 构建的 runner 一定具备定义；手动传入 runtime 的兼容入口则没有。
        /// </summary>
        public SessionRunnerDefinition? Definition { get; }

        /// <summary>
        /// 当前绑定的运行时实例。
        /// </summary>
        public SessionRuntime Runtime { get; private set; }

        /// <summary>
        /// 兼容入口：当前运行时实例。
        /// </summary>
        [Obsolete("请改用 Runtime。Session 仅保留为兼容属性名。", false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public SessionRuntime Session => Runtime;

        /// <summary>
        /// 当前运行时上下文。
        /// </summary>
        public SessionRuntimeContext Context => Runtime.Context;

        /// <summary>
        /// 运行器名称。
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 运行器当前是否处于运行状态。
        /// </summary>
        public bool IsRunning => State == SessionRunnerState.Running;

        /// <summary>
        /// 当前运行器状态。
        /// </summary>
        public SessionRunnerState State { get; private set; }

        /// <summary>
        /// 最近一次停止原因。
        /// </summary>
        public SessionRunnerShutdownCause LastShutdownCause { get; private set; }

        /// <summary>
        /// 最近一次 runtime 重建原因。
        /// </summary>
        public SessionRunnerResetReason LastResetReason { get; private set; }

        /// <summary>
        /// 最近一次生命周期失败信息。
        /// </summary>
        public SessionRunnerFailureInfo? LastFailure { get; private set; }

        /// <summary>
        /// 当前运行时的稳定公开参数。
        /// </summary>
        public SessionRuntimeOptions RuntimeOptions => Runtime.RuntimeOptions;

        /// <summary>
        /// 当前运行时类型。
        /// </summary>
        public SessionRuntimeKind RuntimeKind => Runtime.RuntimeKind;

        /// <summary>
        /// 当前运行时边界。
        /// </summary>
        public SessionRuntimeBoundary RuntimeBoundary => Runtime.RuntimeBoundary;

        public SessionRunner(SessionRuntime session)
        {
            Runtime = session ?? throw new ArgumentNullException(nameof(session));
            Name = session.GetType().Name;
            session.BindContextRunnerName(Name);
            State = session.IsRunning ? SessionRunnerState.Running : SessionRunnerState.Created;
            _ownsRuntime = false;
            _lifecycleHooks = new SessionRunnerLifecycleHooks();
        }

        public SessionRunner(SessionRunnerDefinition definition)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Name = definition.RunnerName;
            _lifecycleHooks = definition.LifecycleHooks.Clone();
            Runtime = definition.BuildRuntime();

            try
            {
                InvokeRuntimeCreated(Runtime);
            }
            catch
            {
                Runtime.Dispose();
                throw;
            }

            State = SessionRunnerState.Created;
            _ownsRuntime = true;
        }

        /// <summary>
        /// 启动运行器。
        /// 该方法是幂等的。
        /// </summary>
        public void Start()
        {
            ThrowIfDisposed();

            if (Runtime.IsRunning)
            {
                State = SessionRunnerState.Running;
                return;
            }

            try
            {
                InvokeLifecycleHook(_lifecycleHooks.BeforeStart, "BeforeStart");
                Runtime.Start();
                State = SessionRunnerState.Running;
                InvokeLifecycleHook(_lifecycleHooks.AfterStart, "AfterStart");
            }
            catch (Exception ex)
            {
                HandleFailure(ResolveFailureKind(SessionRunnerFailureKind.Start, ex), ResolveOperationName("Start", ex), ex, SessionRunnerShutdownCause.StartupFailure, stopRuntimeIfNeeded: true);
                throw;
            }
        }

        /// <summary>
        /// 推进一步固定帧。
        /// </summary>
        public void Step()
        {
            ThrowIfDisposed();

            if (!Runtime.IsRunning)
            {
                throw new InvalidOperationException("SessionRunner must be started before stepping.");
            }

            try
            {
                Runtime.Update();
            }
            catch (Exception ex)
            {
                HandleFailure(ResolveFailureKind(SessionRunnerFailureKind.Step, ex), ResolveOperationName("Step", ex), ex, SessionRunnerShutdownCause.StepFailure, stopRuntimeIfNeeded: true);
                throw;
            }
        }

        /// <summary>
        /// 推进多个固定帧。
        /// </summary>
        public void Step(int stepCount)
        {
            ThrowIfDisposed();

            if (stepCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(stepCount));
            }

            for (int i = 0; i < stepCount; i++)
            {
                Step();
            }
        }

        /// <summary>
        /// 停止运行器。
        /// 该方法是幂等的。
        /// </summary>
        public void Stop()
        {
            ThrowIfDisposed();
            StopCore(SessionRunnerShutdownCause.HostStopRequested, failureKind: SessionRunnerFailureKind.Stop, operationName: "Stop");
        }

        /// <summary>
        /// 依据当前定义重建一个新的运行时实例。
        /// 仅适用于由 `SessionRunnerDefinition` 创建的 runner。
        /// </summary>
        public void ResetRuntime()
        {
            ThrowIfDisposed();

            if (Definition == null)
            {
                throw new InvalidOperationException("ResetRuntime is only available for runners created from SessionRunnerDefinition.");
            }

            SessionRunnerResetReason reason = SessionRunnerResetReason.HostRequested;

            try
            {
                InvokeLifecycleHook(_lifecycleHooks.BeforeReset, reason, "BeforeReset");

                if (Runtime.IsRunning)
                {
                    StopCore(SessionRunnerShutdownCause.RuntimeReset, failureKind: SessionRunnerFailureKind.Stop, operationName: "ResetRuntime.Stop");
                }
                else
                {
                    LastShutdownCause = SessionRunnerShutdownCause.RuntimeReset;
                }

                SessionRuntime previousRuntime = Runtime;
                SessionRuntime nextRuntime = Definition.BuildRuntime();

                try
                {
                    InvokeRuntimeCreated(nextRuntime);
                }
                catch
                {
                    nextRuntime.Dispose();
                    throw;
                }

                Runtime = nextRuntime;
                previousRuntime.Dispose();
                State = SessionRunnerState.Created;
                LastResetReason = reason;
                InvokeLifecycleHook(_lifecycleHooks.AfterReset, reason, "AfterReset");
            }
            catch (Exception ex)
            {
                HandleFailure(ResolveFailureKind(SessionRunnerFailureKind.Reset, ex), ResolveOperationName("ResetRuntime", ex), ex, SessionRunnerShutdownCause.None, stopRuntimeIfNeeded: false);
                throw;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                if (Runtime.IsRunning)
                {
                    StopCore(SessionRunnerShutdownCause.RunnerDisposed, failureKind: SessionRunnerFailureKind.Dispose, operationName: "Dispose.Stop");
                }

                if (_ownsRuntime)
                {
                    Runtime.Dispose();
                }
            }
            catch (Exception ex)
            {
                HandleFailure(ResolveFailureKind(SessionRunnerFailureKind.Dispose, ex), ResolveOperationName("Dispose", ex), ex, SessionRunnerShutdownCause.RunnerDisposed, stopRuntimeIfNeeded: false);
                throw;
            }

            State = SessionRunnerState.Disposed;
            _disposed = true;
        }

        private void StopCore(SessionRunnerShutdownCause cause, SessionRunnerFailureKind failureKind, string operationName)
        {
            if (!Runtime.IsRunning)
            {
                if (cause == SessionRunnerShutdownCause.HostStopRequested &&
                    (State == SessionRunnerState.Created || State == SessionRunnerState.Stopped))
                {
                    return;
                }

                if (cause != SessionRunnerShutdownCause.None)
                {
                    LastShutdownCause = cause;
                }

                return;
            }

            try
            {
                InvokeLifecycleHook(_lifecycleHooks.BeforeStop, cause, "BeforeStop");
                Runtime.Stop();
                State = SessionRunnerState.Stopped;
                LastShutdownCause = cause;
                InvokeLifecycleHook(_lifecycleHooks.AfterStop, cause, "AfterStop");
            }
            catch (Exception ex)
            {
                HandleFailure(ResolveFailureKind(failureKind, ex), ResolveOperationName(operationName, ex), ex, cause, stopRuntimeIfNeeded: true);
                throw;
            }
        }

        private void HandleFailure(
            SessionRunnerFailureKind kind,
            string operationName,
            Exception exception,
            SessionRunnerShutdownCause shutdownCause,
            bool stopRuntimeIfNeeded)
        {
            LastFailure = new SessionRunnerFailureInfo(kind, operationName, exception);
            State = SessionRunnerState.Faulted;

            if (shutdownCause != SessionRunnerShutdownCause.None)
            {
                LastShutdownCause = shutdownCause;
            }

            if (stopRuntimeIfNeeded && Runtime.IsRunning)
            {
                TryStopAfterFailure(shutdownCause);
            }

            TryInvokeFailureHook(LastFailure);
        }

        private void TryStopAfterFailure(SessionRunnerShutdownCause cause)
        {
            try
            {
                InvokeLifecycleHook(_lifecycleHooks.BeforeStop, cause, "BeforeStop");
            }
            catch
            {
            }

            try
            {
                Runtime.Stop();
            }
            catch
            {
            }

            try
            {
                InvokeLifecycleHook(_lifecycleHooks.AfterStop, cause, "AfterStop");
            }
            catch
            {
            }
        }

        private void InvokeRuntimeCreated(SessionRuntime runtime)
        {
            if (_lifecycleHooks.RuntimeCreated == null)
            {
                return;
            }

            try
            {
                _lifecycleHooks.RuntimeCreated(this, runtime);
            }
            catch (Exception ex)
            {
                throw new SessionRunnerLifecycleHookException("RuntimeCreated", ex);
            }
        }

        private void InvokeLifecycleHook(Action<SessionRunner>? hook, string hookName)
        {
            if (hook == null)
            {
                return;
            }

            try
            {
                hook(this);
            }
            catch (Exception ex)
            {
                throw new SessionRunnerLifecycleHookException(hookName, ex);
            }
        }

        private void InvokeLifecycleHook<TArg>(Action<SessionRunner, TArg>? hook, TArg argument, string hookName)
        {
            if (hook == null)
            {
                return;
            }

            try
            {
                hook(this, argument);
            }
            catch (Exception ex)
            {
                throw new SessionRunnerLifecycleHookException(hookName, ex);
            }
        }

        private void TryInvokeFailureHook(SessionRunnerFailureInfo failure)
        {
            if (_lifecycleHooks.Failure == null)
            {
                return;
            }

            try
            {
                _lifecycleHooks.Failure(this, failure);
            }
            catch
            {
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SessionRunner));
            }
        }

        private static SessionRunnerFailureKind ResolveFailureKind(SessionRunnerFailureKind defaultKind, Exception exception)
        {
            return exception is SessionRunnerLifecycleHookException
                ? SessionRunnerFailureKind.LifecycleHook
                : defaultKind;
        }

        private static string ResolveOperationName(string defaultOperationName, Exception exception)
        {
            return exception is SessionRunnerLifecycleHookException hookException
                ? hookException.HookName
                : defaultOperationName;
        }

        private sealed class SessionRunnerLifecycleHookException : InvalidOperationException
        {
            public SessionRunnerLifecycleHookException(string hookName, Exception innerException)
                : base($"SessionRunner lifecycle hook {hookName} failed.", innerException)
            {
                HookName = hookName;
            }

            public string HookName { get; }
        }
    }
}
