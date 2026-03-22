#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace Sideline.Facet.Extensions.RedDot
{
    /// <summary>
    /// 页面内红点测试 Provider。
    /// 通过显式按钮触发，便于快速验证聚合、Binding 和页面挂载链路。
    /// </summary>
    public sealed class ManualRedDotProvider : IRedDotProvider
    {
        private readonly Dictionary<string, bool> _states = new(StringComparer.OrdinalIgnoreCase)
        {
            [FacetRedDotLabPaths.IdleManual] = false,
            [FacetRedDotLabPaths.DungeonManual] = false,
        };

        public string ProviderId => "facet.manual_lab";

        public event Action? Changed;

        public IReadOnlyList<RedDotStateEntry> GetEntries()
        {
            return _states
                .OrderBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(pair => new RedDotStateEntry(pair.Key, pair.Value, ProviderId))
                .ToArray();
        }

        public bool IsActive(string path)
        {
            string normalizedPath = RedDotAggregator.NormalizePath(path);
            return _states.TryGetValue(normalizedPath, out bool value) && value;
        }

        public void Toggle(string path)
        {
            string normalizedPath = EnsurePath(path);
            _states[normalizedPath] = !_states[normalizedPath];
            Changed?.Invoke();
        }

        public void ClearAll()
        {
            bool changed = false;
            foreach (string path in _states.Keys.ToArray())
            {
                if (!_states[path])
                {
                    continue;
                }

                _states[path] = false;
                changed = true;
            }

            if (changed)
            {
                Changed?.Invoke();
            }
        }

        public void Dispose()
        {
        }

        private string EnsurePath(string path)
        {
            string normalizedPath = RedDotAggregator.NormalizePath(path);
            if (!_states.ContainsKey(normalizedPath))
            {
                _states[normalizedPath] = false;
            }

            return normalizedPath;
        }
    }
}
