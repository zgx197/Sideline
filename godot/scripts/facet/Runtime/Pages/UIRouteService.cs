#nullable enable

using System.Collections.Generic;

namespace Sideline.Facet.Runtime
{
    /// <summary>
    /// 最小页面路由服务。
    /// 当前阶段只负责维护返回栈，不承担复杂路径匹配或多层路由分发。
    /// </summary>
    public sealed class UIRouteService
    {
        private readonly Stack<UIRouteEntry> _backStack = new();

        public int Count => _backStack.Count;

        public bool CanGoBack => _backStack.Count > 0;

        public void Push(string pageId, IReadOnlyDictionary<string, object?>? arguments)
        {
            _backStack.Push(new UIRouteEntry(pageId, arguments));
        }

        public bool TryPop(out UIRouteEntry? entry)
        {
            if (_backStack.Count == 0)
            {
                entry = null;
                return false;
            }

            entry = _backStack.Pop();
            return true;
        }

        public void Clear()
        {
            _backStack.Clear();
        }
    }
}