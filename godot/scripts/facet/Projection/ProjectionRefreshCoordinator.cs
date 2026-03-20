#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sideline.Facet.Runtime;

namespace Sideline.Facet.Projection
{
    /// <summary>
    /// Projection 刷新协调器：负责注册与调度 ProjectionUpdater。
    /// </summary>
    public sealed class ProjectionRefreshCoordinator
    {
        private readonly Dictionary<ProjectionKey, IProjectionUpdater> _updaters = new();
        private readonly IFacetLogger? _logger;

        public ProjectionRefreshCoordinator(IFacetLogger? logger = null)
        {
            _logger = logger;
        }

        /// <summary>
        /// 当前已注册的刷新器数量。
        /// </summary>
        public int Count => _updaters.Count;

        /// <summary>
        /// 获取当前已注册的 ProjectionKey 快照。
        /// </summary>
        public IReadOnlyList<ProjectionKey> GetRegisteredKeys()
        {
            return new List<ProjectionKey>(_updaters.Keys);
        }

        /// <summary>
        /// 注册 Projection 刷新器。
        /// </summary>
        public void Register(IProjectionUpdater updater)
        {
            ArgumentNullException.ThrowIfNull(updater);
            _updaters[updater.Key] = updater;
            _logger?.Debug("Projection.Refresh", $"Registered projection updater: {updater.Key}");
        }

        /// <summary>
        /// 检查指定 ProjectionKey 是否已注册刷新器。
        /// </summary>
        public bool Contains(ProjectionKey key)
        {
            return _updaters.ContainsKey(key);
        }

        /// <summary>
        /// 刷新指定 Projection。
        /// </summary>
        public async ValueTask<bool> RefreshAsync(ProjectionKey key, CancellationToken cancellationToken = default)
        {
            if (!_updaters.TryGetValue(key, out IProjectionUpdater? updater))
            {
                _logger?.Warning("Projection.Refresh", $"Projection updater not found: {key}");
                return false;
            }

            await updater.RefreshAsync(cancellationToken);
            _logger?.Debug("Projection.Refresh", $"Projection refreshed: {key}");
            return true;
        }

        /// <summary>
        /// 刷新全部已注册 Projection。
        /// </summary>
        public async ValueTask<int> RefreshAllAsync(CancellationToken cancellationToken = default)
        {
            int refreshedCount = 0;
            foreach (IProjectionUpdater updater in _updaters.Values)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await updater.RefreshAsync(cancellationToken);
                refreshedCount++;
            }

            _logger?.Debug("Projection.Refresh", $"Refreshed all projections. Count={refreshedCount}");
            return refreshedCount;
        }
    }
}
