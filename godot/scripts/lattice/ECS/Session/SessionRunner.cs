using System;

namespace Lattice.ECS.Session
{
    /// <summary>
    /// 会话运行器。
    /// 为纯 C# 环境提供统一的 Start / Step / Stop 驱动入口。
    /// </summary>
    public sealed class SessionRunner : IDisposable
    {
        private bool _disposed;

        /// <summary>
        /// 当前绑定的会话。
        /// </summary>
        public Session Session { get; }

        /// <summary>
        /// 运行器当前是否处于运行状态。
        /// </summary>
        public bool IsRunning => !_disposed && Session.IsRunning;

        public SessionRunner(Session session)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
        }

        /// <summary>
        /// 启动运行器。
        /// 该方法是幂等的。
        /// </summary>
        public void Start()
        {
            ThrowIfDisposed();

            if (Session.IsRunning)
            {
                return;
            }

            Session.Start();
        }

        /// <summary>
        /// 推进一步固定帧。
        /// </summary>
        public void Step()
        {
            ThrowIfDisposed();

            if (!Session.IsRunning)
            {
                throw new InvalidOperationException("SessionRunner must be started before stepping.");
            }

            Session.Update();
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

            if (!Session.IsRunning)
            {
                return;
            }

            Session.Stop();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            if (Session.IsRunning)
            {
                Session.Stop();
            }

            _disposed = true;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SessionRunner));
            }
        }
    }
}
