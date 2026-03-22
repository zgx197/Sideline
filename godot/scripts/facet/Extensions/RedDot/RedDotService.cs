#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Sideline.Facet.Runtime;

namespace Sideline.Facet.Extensions.RedDot
{
    /// <summary>
    /// 红点树运行时服务。
    /// 负责聚合多来源条目、提供查询能力，并向页面广播路径级变更。
    /// </summary>
    public sealed class RedDotService : IRedDotService, IDisposable
    {
        private readonly IFacetLogger? _logger;
        private readonly Dictionary<string, ProviderRegistration> _providers = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Dictionary<long, Action<RedDotChange>>> _subscriptions = new(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, RedDotNodeSnapshot> _snapshots = new(StringComparer.OrdinalIgnoreCase);
        private long _nextSubscriptionId;
        private bool _disposed;

        public RedDotService(IFacetLogger? logger = null)
        {
            _logger = logger;
        }

        public bool IsAvailable => _providers.Count > 0;

        public int RegisteredPathCount => _snapshots.Count;

        public void RegisterProvider(IRedDotProvider provider)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ArgumentNullException.ThrowIfNull(provider);

            if (_providers.ContainsKey(provider.ProviderId))
            {
                throw new InvalidOperationException($"Facet red dot provider already registered: {provider.ProviderId}");
            }

            void HandleProviderChanged()
            {
                Rebuild($"provider.changed:{provider.ProviderId}");
            }

            provider.Changed += HandleProviderChanged;
            _providers[provider.ProviderId] = new ProviderRegistration(provider, HandleProviderChanged);

            _logger?.Info(
                "RedDot.Service",
                "红点 Provider 已注册。",
                new Dictionary<string, object?>
                {
                    ["providerId"] = provider.ProviderId,
                    ["providerCount"] = _providers.Count,
                });

            Rebuild($"provider.register:{provider.ProviderId}");
        }

        public bool TryGetState(string path, out bool hasRedDot)
        {
            string normalizedPath = RedDotAggregator.NormalizePath(path);
            if (_snapshots.TryGetValue(normalizedPath, out RedDotNodeSnapshot? snapshot))
            {
                hasRedDot = snapshot.HasRedDot;
                return true;
            }

            hasRedDot = false;
            return false;
        }

        public bool GetStateOrDefault(string path, bool fallback = false)
        {
            return TryGetState(path, out bool hasRedDot)
                ? hasRedDot
                : fallback;
        }

        public bool TryGetSnapshot(string path, out RedDotNodeSnapshot snapshot)
        {
            string normalizedPath = RedDotAggregator.NormalizePath(path);
            return _snapshots.TryGetValue(normalizedPath, out snapshot!);
        }

        public IReadOnlyList<string> GetRegisteredPaths()
        {
            return _snapshots.Keys
                .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public IDisposable Subscribe(string path, Action<RedDotChange> listener)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            ArgumentNullException.ThrowIfNull(listener);

            string normalizedPath = RedDotAggregator.NormalizePath(path);
            if (!_subscriptions.TryGetValue(normalizedPath, out Dictionary<long, Action<RedDotChange>>? listeners))
            {
                listeners = new Dictionary<long, Action<RedDotChange>>();
                _subscriptions[normalizedPath] = listeners;
            }

            long subscriptionId = Interlocked.Increment(ref _nextSubscriptionId);
            listeners[subscriptionId] = listener;
            return new Subscription(this, normalizedPath, subscriptionId);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            foreach (ProviderRegistration registration in _providers.Values)
            {
                registration.Provider.Changed -= registration.Handler;
                registration.Provider.Dispose();
            }

            _providers.Clear();
            _subscriptions.Clear();
            _snapshots.Clear();
            _disposed = true;
        }

        private void Rebuild(string source)
        {
            List<RedDotStateEntry> entries = new();
            foreach (ProviderRegistration registration in _providers.Values)
            {
                IReadOnlyList<RedDotStateEntry> providerEntries = registration.Provider.GetEntries();
                if (providerEntries.Count > 0)
                {
                    entries.AddRange(providerEntries);
                }
            }

            Dictionary<string, RedDotNodeSnapshot> nextSnapshots = RedDotAggregator.Build(entries);
            Dictionary<string, RedDotNodeSnapshot> previousSnapshots = _snapshots;
            _snapshots = nextSnapshots;

            int changedPathCount = PublishChanges(previousSnapshots, nextSnapshots, source);
            int activePathCount = nextSnapshots.Values.Count(static snapshot => snapshot.HasRedDot);

            _logger?.Info(
                "RedDot.Aggregator",
                "红点树已完成聚合刷新。",
                new Dictionary<string, object?>
                {
                    ["source"] = source,
                    ["providerCount"] = _providers.Count,
                    ["entryCount"] = entries.Count,
                    ["registeredPathCount"] = nextSnapshots.Count,
                    ["activePathCount"] = activePathCount,
                    ["changedPathCount"] = changedPathCount,
                    ["activePathsPreview"] = string.Join(", ", nextSnapshots.Values.Where(static snapshot => snapshot.HasRedDot).Take(6).Select(static snapshot => snapshot.Path)),
                });
        }

        private int PublishChanges(
            IReadOnlyDictionary<string, RedDotNodeSnapshot> previousSnapshots,
            IReadOnlyDictionary<string, RedDotNodeSnapshot> nextSnapshots,
            string source)
        {
            HashSet<string> changedPaths = new(StringComparer.OrdinalIgnoreCase);

            foreach (string path in previousSnapshots.Keys)
            {
                if (!nextSnapshots.TryGetValue(path, out RedDotNodeSnapshot? nextSnapshot) ||
                    !Equals(previousSnapshots[path], nextSnapshot))
                {
                    changedPaths.Add(path);
                }
            }

            foreach (string path in nextSnapshots.Keys)
            {
                if (!previousSnapshots.TryGetValue(path, out RedDotNodeSnapshot? previousSnapshot) ||
                    !Equals(previousSnapshot, nextSnapshots[path]))
                {
                    changedPaths.Add(path);
                }
            }

            foreach (string path in changedPaths)
            {
                previousSnapshots.TryGetValue(path, out RedDotNodeSnapshot? previousSnapshot);
                nextSnapshots.TryGetValue(path, out RedDotNodeSnapshot? currentSnapshot);
                Publish(new RedDotChange(path, previousSnapshot, currentSnapshot, source));
            }

            return changedPaths.Count;
        }

        private void Publish(RedDotChange change)
        {
            if (!_subscriptions.TryGetValue(change.Path, out Dictionary<long, Action<RedDotChange>>? listeners))
            {
                return;
            }

            foreach (Action<RedDotChange> listener in listeners.Values.ToArray())
            {
                listener(change);
            }
        }

        private void Unsubscribe(string path, long subscriptionId)
        {
            if (!_subscriptions.TryGetValue(path, out Dictionary<long, Action<RedDotChange>>? listeners))
            {
                return;
            }

            listeners.Remove(subscriptionId);
            if (listeners.Count == 0)
            {
                _subscriptions.Remove(path);
            }
        }

        private sealed record ProviderRegistration(
            IRedDotProvider Provider,
            Action Handler);

        private sealed class Subscription : IDisposable
        {
            private readonly RedDotService _service;
            private readonly string _path;
            private readonly long _subscriptionId;
            private bool _disposed;

            public Subscription(RedDotService service, string path, long subscriptionId)
            {
                _service = service;
                _path = path;
                _subscriptionId = subscriptionId;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _service.Unsubscribe(_path, _subscriptionId);
                _disposed = true;
            }
        }
    }
}
