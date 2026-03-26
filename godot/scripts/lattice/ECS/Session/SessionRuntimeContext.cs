using System;
using System.Collections.Generic;
using Lattice.ECS.Core;

namespace Lattice.ECS.Session
{
    /// <summary>
    /// 很薄的 Session 运行时上下文。
    /// 只承载显式声明的 runtime shared service 与稳定公开的运行元信息，
    /// 避免继续侵入 `Frame` 或退化成万能对象包。
    /// </summary>
    public sealed class SessionRuntimeContext : IDisposable
    {
        private static readonly Type? LegacySnapshotType =
            typeof(Frame).Assembly.GetType("Lattice.ECS.Core." + "Frame" + "Snapshot", throwOnError: false);

        private static readonly SessionRuntimeContextBoundary DefaultBoundary = new(
            SessionRuntimeContextKind.TypedRuntimeSharedServices,
            SessionRuntimeContextCapability.TypedSharedLookup |
            SessionRuntimeContextCapability.ExplicitRuntimeSharedServiceRegistration |
            SessionRuntimeContextCapability.OwnedSharedObjectLifetime |
            SessionRuntimeContextCapability.RuntimeMetadataExposure,
            UnsupportedSessionRuntimeContextCapability.ArbitraryObjectBag |
            UnsupportedSessionRuntimeContextCapability.RollbackStateStorage |
            UnsupportedSessionRuntimeContextCapability.GameplayStateExchange |
            UnsupportedSessionRuntimeContextCapability.StringKeyedServiceLocator);

        private readonly SessionRuntime _runtime;
        private readonly Dictionary<Type, object> _sharedObjects = new();
        private readonly HashSet<object> _ownedObjects = new(ReferenceEqualityComparer.Instance);
        private bool _disposed;

        internal SessionRuntimeContext(SessionRuntime runtime, string runnerName)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));

            if (string.IsNullOrWhiteSpace(runnerName))
            {
                throw new ArgumentException("Runner name cannot be null or whitespace.", nameof(runnerName));
            }

            RunnerName = runnerName;
        }

        /// <summary>
        /// 当前运行时上下文边界。
        /// </summary>
        public SessionRuntimeContextBoundary Boundary => DefaultBoundary;

        /// <summary>
        /// 当前上下文所属的运行时实例。
        /// </summary>
        public SessionRuntime Runtime => _runtime;

        /// <summary>
        /// 当前运行器名称。
        /// 对于未接入 runner 的独立 runtime，默认使用 runtime 类型名。
        /// </summary>
        public string RunnerName { get; private set; }

        /// <summary>
        /// 稳定公开的运行时参数。
        /// </summary>
        public SessionRuntimeOptions RuntimeOptions => _runtime.RuntimeOptions;

        /// <summary>
        /// 当前运行时类型。
        /// </summary>
        public SessionRuntimeKind RuntimeKind => _runtime.RuntimeKind;

        /// <summary>
        /// 当前运行时边界。
        /// </summary>
        public SessionRuntimeBoundary RuntimeBoundary => _runtime.RuntimeBoundary;

        /// <summary>
        /// 当前已注册的共享对象数量。
        /// </summary>
        public int SharedObjectCount => _sharedObjects.Count;

        /// <summary>
        /// 写入或替换一个按类型索引的共享对象。
        /// 只有显式声明为 runtime shared service 的类型才允许注册。
        /// </summary>
        public void SetShared<T>(T instance, bool disposeWithContext = false) where T : class
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(instance);
            ValidateSharedRegistration(instance);

            Type type = typeof(T);
            if (_sharedObjects.TryGetValue(type, out object? existing))
            {
                if (ReferenceEquals(existing, instance))
                {
                    if (disposeWithContext)
                    {
                        _ownedObjects.Add(instance);
                    }

                    return;
                }

                if (_ownedObjects.Remove(existing) && existing is IDisposable disposableExisting)
                {
                    disposableExisting.Dispose();
                }
            }

            _sharedObjects[type] = instance;
            if (disposeWithContext)
            {
                _ownedObjects.Add(instance);
            }
        }

        /// <summary>
        /// 尝试获取一个按类型索引的共享对象。
        /// </summary>
        public bool TryGetShared<T>(out T? instance) where T : class
        {
            ThrowIfDisposed();

            if (_sharedObjects.TryGetValue(typeof(T), out object? value))
            {
                instance = (T)value;
                return true;
            }

            instance = null;
            return false;
        }

        /// <summary>
        /// 获取一个必须存在的共享对象。
        /// </summary>
        public T GetRequiredShared<T>() where T : class
        {
            ThrowIfDisposed();

            if (TryGetShared<T>(out T? instance))
            {
                return instance!;
            }

            throw new InvalidOperationException($"Shared object of type {typeof(T).Name} is not registered.");
        }

        /// <summary>
        /// 移除一个按类型索引的共享对象。
        /// </summary>
        public bool RemoveShared<T>() where T : class
        {
            ThrowIfDisposed();

            Type type = typeof(T);
            if (!_sharedObjects.Remove(type, out object? existing))
            {
                return false;
            }

            if (_ownedObjects.Remove(existing) && existing is IDisposable disposable)
            {
                disposable.Dispose();
            }

            return true;
        }

        internal void BindRunnerName(string runnerName)
        {
            ThrowIfDisposed();

            if (string.IsNullOrWhiteSpace(runnerName))
            {
                throw new ArgumentException("Runner name cannot be null or whitespace.", nameof(runnerName));
            }

            RunnerName = runnerName;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            foreach (object owned in _ownedObjects)
            {
                if (owned is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }

            _ownedObjects.Clear();
            _sharedObjects.Clear();
            _disposed = true;
        }

        private static void ValidateSharedRegistration(object instance)
        {
            Type instanceType = instance.GetType();

            if (!typeof(ISessionRuntimeSharedService).IsAssignableFrom(instanceType))
            {
                throw new ArgumentException(
                    $"Shared object type {instanceType.Name} must implement {nameof(ISessionRuntimeSharedService)} before it can be registered in {nameof(SessionRuntimeContext)}.",
                    nameof(instance));
            }

            if (IsDisallowedSharedType(instanceType))
            {
                throw new ArgumentException(
                    $"Shared object type {instanceType.Name} cannot be registered in {nameof(SessionRuntimeContext)} because rollback state and snapshot carriers must stay outside the runtime service context.",
                    nameof(instance));
            }
        }

        private static bool IsDisallowedSharedType(Type instanceType)
        {
            return typeof(Frame).IsAssignableFrom(instanceType) ||
                   (LegacySnapshotType != null && LegacySnapshotType.IsAssignableFrom(instanceType)) ||
                   typeof(PackedFrameSnapshot).IsAssignableFrom(instanceType) ||
                   typeof(SessionCheckpoint).IsAssignableFrom(instanceType) ||
                   typeof(SessionRuntime).IsAssignableFrom(instanceType) ||
                   typeof(SessionRuntimeContext).IsAssignableFrom(instanceType);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SessionRuntimeContext));
            }
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static ReferenceEqualityComparer Instance { get; } = new();

            public new bool Equals(object? x, object? y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(object obj)
            {
                return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}
