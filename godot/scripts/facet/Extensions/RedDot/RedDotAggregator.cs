#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace Sideline.Facet.Extensions.RedDot
{
    /// <summary>
    /// 红点树聚合器。
    /// 负责把原始叶子条目展开为包含父路径的聚合快照。
    /// </summary>
    public static class RedDotAggregator
    {
        public static Dictionary<string, RedDotNodeSnapshot> Build(IReadOnlyList<RedDotStateEntry> entries)
        {
            ArgumentNullException.ThrowIfNull(entries);

            Dictionary<string, bool> selfStates = new(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, HashSet<string>> children = new(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, HashSet<string>> sources = new(StringComparer.OrdinalIgnoreCase);
            HashSet<string> knownPaths = new(StringComparer.OrdinalIgnoreCase);

            foreach (RedDotStateEntry entry in entries)
            {
                if (string.IsNullOrWhiteSpace(entry.Path))
                {
                    continue;
                }

                string[] segments = SplitSegments(entry.Path);
                if (segments.Length == 0)
                {
                    continue;
                }

                string? parentPath = null;
                for (int index = 0; index < segments.Length; index++)
                {
                    string currentPath = string.Join('.', segments, 0, index + 1);
                    knownPaths.Add(currentPath);

                    if (parentPath != null)
                    {
                        GetOrCreateSet(children, parentPath).Add(currentPath);
                    }

                    parentPath = currentPath;
                }

                string normalizedPath = string.Join('.', segments);
                if (entry.HasRedDot)
                {
                    selfStates[normalizedPath] = true;
                }
                else if (!selfStates.ContainsKey(normalizedPath))
                {
                    selfStates[normalizedPath] = false;
                }

                GetOrCreateSet(sources, normalizedPath).Add(entry.SourceId ?? string.Empty);
            }

            Dictionary<string, RedDotNodeSnapshot> snapshots = new(StringComparer.OrdinalIgnoreCase);
            foreach (string path in knownPaths.OrderByDescending(GetDepth).ThenBy(static path => path, StringComparer.OrdinalIgnoreCase))
            {
                children.TryGetValue(path, out HashSet<string>? directChildren);
                bool selfHasRedDot = selfStates.TryGetValue(path, out bool selfValue) && selfValue;
                int activeChildCount = 0;

                if (directChildren != null)
                {
                    foreach (string childPath in directChildren)
                    {
                        if (snapshots.TryGetValue(childPath, out RedDotNodeSnapshot? childSnapshot) && childSnapshot.HasRedDot)
                        {
                            activeChildCount++;
                        }
                    }
                }

                bool hasRedDot = selfHasRedDot || activeChildCount > 0;
                int sourceCount = sources.TryGetValue(path, out HashSet<string>? sourceSet)
                    ? sourceSet.Count
                    : 0;

                snapshots[path] = new RedDotNodeSnapshot(
                    path,
                    selfHasRedDot,
                    hasRedDot,
                    directChildren?.Count ?? 0,
                    activeChildCount,
                    sourceCount);
            }

            return snapshots;
        }

        public static string NormalizePath(string path)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);

            string[] segments = SplitSegments(path);
            if (segments.Length == 0)
            {
                throw new ArgumentException("Facet red dot path is empty.", nameof(path));
            }

            return string.Join('.', segments);
        }

        private static string[] SplitSegments(string path)
        {
            return path
                .Split(new[] { '.', '/', '\\' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(static segment => segment.Trim())
                .Where(static segment => segment.Length > 0)
                .ToArray();
        }

        private static int GetDepth(string path)
        {
            return path.Count(static character => character == '.') + 1;
        }

        private static HashSet<string> GetOrCreateSet(Dictionary<string, HashSet<string>> map, string key)
        {
            if (map.TryGetValue(key, out HashSet<string>? set))
            {
                return set;
            }

            set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            map[key] = set;
            return set;
        }
    }
}
