#nullable enable

using System;
using System.Collections.Generic;

namespace Sideline.Facet.Runtime
{
    /// <summary>
    /// 页面返回栈中的单条路由记录。
    /// </summary>
    public sealed class UIRouteEntry
    {
        public UIRouteEntry(string pageId, IReadOnlyDictionary<string, object?>? arguments)
        {
            if (string.IsNullOrWhiteSpace(pageId))
            {
                throw new ArgumentException("PageId cannot be empty.", nameof(pageId));
            }

            PageId = pageId;
            Arguments = CloneArguments(arguments);
        }

        public string PageId { get; }

        public IReadOnlyDictionary<string, object?> Arguments { get; }

        private static IReadOnlyDictionary<string, object?> CloneArguments(IReadOnlyDictionary<string, object?>? arguments)
        {
            if (arguments == null || arguments.Count == 0)
            {
                return new Dictionary<string, object?>();
            }

            Dictionary<string, object?> snapshot = new(arguments.Count, StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, object?> pair in arguments)
            {
                snapshot[pair.Key] = pair.Value;
            }

            return snapshot;
        }
    }
}