#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;

namespace Sideline.Facet.Projection
{
    /// <summary>
    /// Facet 最小 Projection 存储。
    /// </summary>
    public sealed class ProjectionStore
    {
        private readonly Dictionary<ProjectionKey, object> _values = new();
        private readonly Dictionary<ProjectionKey, Dictionary<long, Action<ProjectionChange>>> _subscriptions = new();
        private long _nextSubscriptionId;

        /// <summary>
        /// 当前已缓存的 Projection 数量。
        /// </summary>
        public int Count => _values.Count;

        /// <summary>
        /// 按主键设置 Projection。
        /// </summary>
        public ProjectionChange Set<TViewModel>(ProjectionKey key, TViewModel viewModel, string? source = null)
            where TViewModel : class, IViewModel
        {
            ArgumentNullException.ThrowIfNull(viewModel);

            _values.TryGetValue(key, out object? previousValue);
            _values[key] = viewModel;

            ProjectionChange change = new(
                key,
                ProjectionChangeKind.Set,
                previousValue,
                viewModel,
                source,
                DateTimeOffset.UtcNow);

            Publish(change);
            return change;
        }

        /// <summary>
        /// 按主键移除 Projection。
        /// </summary>
        public ProjectionChange Remove(ProjectionKey key, string? source = null)
        {
            _values.TryGetValue(key, out object? previousValue);
            _values.Remove(key);

            ProjectionChange change = new(
                key,
                ProjectionChangeKind.Removed,
                previousValue,
                null,
                source,
                DateTimeOffset.UtcNow);

            Publish(change);
            return change;
        }

        /// <summary>
        /// 尝试获取 Projection。
        /// </summary>
        public bool TryGet<TViewModel>(ProjectionKey key, out TViewModel? viewModel)
            where TViewModel : class, IViewModel
        {
            if (_values.TryGetValue(key, out object? value) && value is TViewModel typedValue)
            {
                viewModel = typedValue;
                return true;
            }

            viewModel = null;
            return false;
        }

        /// <summary>
        /// 获取必须存在的 Projection。
        /// </summary>
        public TViewModel GetRequired<TViewModel>(ProjectionKey key)
            where TViewModel : class, IViewModel
        {
            if (TryGet<TViewModel>(key, out TViewModel? viewModel) && viewModel != null)
            {
                return viewModel;
            }

            throw new InvalidOperationException($"Projection not found: {key}");
        }

        /// <summary>
        /// 检查 Projection 是否存在。
        /// </summary>
        public bool Contains(ProjectionKey key)
        {
            return _values.ContainsKey(key);
        }

        /// <summary>
        /// 获取当前全部 Projection 主键快照。
        /// 供运行时诊断与工具面板读取，不暴露内部字典引用。
        /// </summary>
        public IReadOnlyList<ProjectionKey> GetKeysSnapshot()
        {
            List<ProjectionKey> keys = new(_values.Keys);
            keys.Sort(static (left, right) => string.Compare(left.Value, right.Value, StringComparison.OrdinalIgnoreCase));
            return keys;
        }

        /// <summary>
        /// 订阅指定 ProjectionKey 的变更。
        /// </summary>
        public IDisposable Subscribe(ProjectionKey key, Action<ProjectionChange> listener)
        {
            ArgumentNullException.ThrowIfNull(listener);

            if (!_subscriptions.TryGetValue(key, out Dictionary<long, Action<ProjectionChange>>? listeners))
            {
                listeners = new Dictionary<long, Action<ProjectionChange>>();
                _subscriptions[key] = listeners;
            }

            long subscriptionId = Interlocked.Increment(ref _nextSubscriptionId);
            listeners[subscriptionId] = listener;
            return new Subscription(this, key, subscriptionId);
        }

        private void Publish(ProjectionChange change)
        {
            if (!_subscriptions.TryGetValue(change.Key, out Dictionary<long, Action<ProjectionChange>>? listeners))
            {
                return;
            }

            foreach (Action<ProjectionChange> listener in listeners.Values)
            {
                listener(change);
            }
        }

        private void Unsubscribe(ProjectionKey key, long subscriptionId)
        {
            if (!_subscriptions.TryGetValue(key, out Dictionary<long, Action<ProjectionChange>>? listeners))
            {
                return;
            }

            listeners.Remove(subscriptionId);
            if (listeners.Count == 0)
            {
                _subscriptions.Remove(key);
            }
        }

        private sealed class Subscription : IDisposable
        {
            private readonly ProjectionStore _store;
            private readonly ProjectionKey _key;
            private readonly long _subscriptionId;
            private bool _disposed;

            public Subscription(ProjectionStore store, ProjectionKey key, long subscriptionId)
            {
                _store = store;
                _key = key;
                _subscriptionId = subscriptionId;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _store.Unsubscribe(_key, _subscriptionId);
                _disposed = true;
            }
        }
    }
}
