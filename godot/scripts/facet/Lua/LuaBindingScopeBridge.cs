#nullable enable

using System;
using System.Collections.Generic;
using Sideline.Facet.Runtime;

namespace Sideline.Facet.Lua
{
    /// <summary>
    /// 提供给 Lua 的受限 Binding 作用域桥接。
    /// Lua 不直接接触底层 Binding 接口，而是通过状态袋驱动受限绑定注册。
    /// </summary>
    public sealed class LuaBindingScopeBridge
    {
        private readonly LuaApiBridge _api;
        private readonly IUIBindingScope _scope;
        private readonly IUIObjectResolver? _resolver;
        private readonly HashSet<string> _registeredBindings = new(StringComparer.OrdinalIgnoreCase);

        public LuaBindingScopeBridge(LuaApiBridge api, IUIBindingScope scope, IUIObjectResolver? resolver)
        {
            ArgumentNullException.ThrowIfNull(api);
            ArgumentNullException.ThrowIfNull(scope);

            _api = api;
            _scope = scope;
            _resolver = resolver;
        }

        public string ScopeId => _scope.ScopeId;

        public int Count => _scope.Count;

        public int RefreshCount => _scope.RefreshCount;

        public UIBindingDiagnosticsSnapshot GetDiagnosticsSnapshot()
        {
            return _scope.GetDiagnosticsSnapshot();
        }

        public bool HasNode(string key)
        {
            return _resolver?.TryResolve(key, out object? _) == true;
        }

        public void BindStateText(string key, string stateKey, string fallback = "")
        {
            RegisterOnce(
                $"text|{key}|{stateKey}",
                () => _scope.BindText(key, () => _api.GetStateString(stateKey, fallback)));
        }

        public void BindStateVisibility(string key, string stateKey, bool fallback = false)
        {
            RegisterOnce(
                $"visibility|{key}|{stateKey}",
                () => _scope.BindVisibility(key, () => _api.GetStateBoolean(stateKey, fallback)));
        }

        public void BindStateInteractable(string key, string stateKey, bool fallback = false)
        {
            RegisterOnce(
                $"interactable|{key}|{stateKey}",
                () => _scope.BindInteractable(key, () => _api.GetStateBoolean(stateKey, fallback)));
        }

        public void BindStateList(string key, string stateKey, string separator = "\n", string emptyText = "")
        {
            RegisterOnce(
                $"list|{key}|{stateKey}",
                () => _scope.BindList(key, () => _api.GetStateStrings(stateKey), separator, emptyText));
        }

        public void BindStateStructuredList(
            string containerKey,
            string templateKey,
            string stateKey,
            string primaryTextKey,
            string secondaryTextKey,
            string tertiaryTextKey,
            string? emptyStateKey = null)
        {
            RegisterOnce(
                $"structured-list|{containerKey}|{templateKey}|{stateKey}|{primaryTextKey}|{secondaryTextKey}|{tertiaryTextKey}|{emptyStateKey}",
                () => _scope.BindComplexList(
                    containerKey,
                    templateKey,
                    () => _api.GetStructuredListState(stateKey),
                    new LuaStructuredListAdapter(primaryTextKey, secondaryTextKey, tertiaryTextKey),
                    emptyStateKey));
        }

        public void Refresh(string reason = "lua.bindings.refresh")
        {
            _scope.RefreshAll(reason);
        }

        internal void ResetRegistrations()
        {
            _registeredBindings.Clear();
        }

        private void RegisterOnce(string registrationKey, Action registerAction)
        {
            if (_registeredBindings.Contains(registrationKey))
            {
                return;
            }

            registerAction();
            _registeredBindings.Add(registrationKey);
        }

        /// <summary>
        /// 结构化列表项。
        /// 由 Lua 或 C# 写入状态袋，再由复杂列表 Binding 统一消费。
        /// </summary>
        public sealed class LuaStructuredListItem
        {
            public LuaStructuredListItem(string key, string primaryText, string secondaryText, string tertiaryText)
            {
                Key = string.IsNullOrWhiteSpace(key) ? "item" : key;
                PrimaryText = primaryText ?? string.Empty;
                SecondaryText = secondaryText ?? string.Empty;
                TertiaryText = tertiaryText ?? string.Empty;
            }

            public string Key { get; }

            public string PrimaryText { get; }

            public string SecondaryText { get; }

            public string TertiaryText { get; }
        }

        private sealed class LuaStructuredListAdapter : IUIComplexListAdapter<LuaStructuredListItem>
        {
            private readonly string _primaryTextKey;
            private readonly string _secondaryTextKey;
            private readonly string _tertiaryTextKey;

            public LuaStructuredListAdapter(string primaryTextKey, string secondaryTextKey, string tertiaryTextKey)
            {
                _primaryTextKey = primaryTextKey;
                _secondaryTextKey = secondaryTextKey;
                _tertiaryTextKey = tertiaryTextKey;
            }

            public string GetItemKey(LuaStructuredListItem item, int index)
            {
                return item.Key;
            }

            public void BindItem(IUIComponentBindingScope itemScope, LuaStructuredListItem item, int index)
            {
                itemScope.BindText(_primaryTextKey, () => item.PrimaryText);
                itemScope.BindText(_secondaryTextKey, () => item.SecondaryText);
                itemScope.BindText(_tertiaryTextKey, () => item.TertiaryText);
            }
        }
    }
}
