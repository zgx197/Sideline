#nullable enable

using System;
using System.Collections.Generic;
using MoonSharp.Interpreter;
using Sideline.Facet.Application;
using Sideline.Facet.Application.Diagnostics;
using Sideline.Facet.Runtime;

namespace Sideline.Facet.Lua
{
    /// <summary>
    /// 提供给 Lua 页面控制器的受限 API 桥接。
    /// 只暴露节点解析、Binding 刷新、命令查询、页面路由、状态袋和红点占位能力。
    /// </summary>
    public sealed class LuaApiBridge
    {
        private const string LuaRootComponentId = "__lua";

        private readonly UIContext _context;
        private readonly ILuaRedDotBridge _redDotBridge;
        private readonly Dictionary<string, LuaBindingScopeBridge> _componentBindings = new(StringComparer.OrdinalIgnoreCase);
        private LuaBindingScopeBridge? _pageBindings;
        private IUIComponentBindingScope? _luaRootScope;

        public LuaApiBridge(UIContext context, ILuaRedDotBridge redDotBridge)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(redDotBridge);

            _context = context;
            _redDotBridge = redDotBridge;
        }

        public string PageId => _context.PageId;

        public string Layer => _context.Layer;

        public string? ControllerScript => _context.Definition.ControllerScript;

        [MoonSharpHidden]
        public IReadOnlyDictionary<string, object?> Arguments => _context.Arguments;

        [MoonSharpHidden]
        public IUIObjectResolver? Resolver => _context.Resolver;

        [MoonSharpHidden]
        public IUIBindingScope? Bindings => _context.Bindings;

        public bool HasResolver => Resolver != null;

        public bool HasBindings => Bindings != null;

        public bool CanGoBack
        {
            get
            {
                if (!TryGetNavigator(out IUIPageNavigator? navigator) || navigator == null)
                {
                    return false;
                }

                return navigator.CanGoBack;
            }
        }

        public int BackStackDepth
        {
            get
            {
                if (!TryGetNavigator(out IUIPageNavigator? navigator) || navigator == null)
                {
                    return 0;
                }

                return navigator.BackStackDepth;
            }
        }

        public bool SupportsRedDot => _redDotBridge.IsAvailable;

        [MoonSharpHidden]
        public bool TryResolve<TObject>(string key, out TObject? value) where TObject : class
        {
            value = null;
            if (Resolver == null || !Resolver.TryResolve(key, out object? rawValue))
            {
                return false;
            }

            value = rawValue as TObject;
            return value != null;
        }

        public bool HasNode(string key)
        {
            return Resolver?.TryResolve(key, out object? _) == true;
        }

        public bool HasArgument(string key)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            return Arguments.ContainsKey(key);
        }

        public string GetArgumentString(string key, string fallback = "")
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            if (Arguments.TryGetValue(key, out object? value) && value is string text)
            {
                return text;
            }

            return fallback;
        }

        public double GetArgumentNumber(string key, double fallback = 0)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            if (Arguments.TryGetValue(key, out object? value))
            {
                return value switch
                {
                    double doubleValue => doubleValue,
                    float floatValue => floatValue,
                    int intValue => intValue,
                    long longValue => longValue,
                    _ => fallback,
                };
            }

            return fallback;
        }

        public bool GetArgumentBoolean(string key, bool fallback = false)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            if (Arguments.TryGetValue(key, out object? value) && value is bool booleanValue)
            {
                return booleanValue;
            }

            return fallback;
        }

        public void RefreshBindings(string reason)
        {
            Bindings?.RefreshAll(reason);
        }

        public LuaBindingScopeBridge? GetPageBindings()
        {
            if (Bindings == null || Resolver == null)
            {
                return null;
            }

            EnsureLuaRootScope();
            if (_luaRootScope == null)
            {
                return null;
            }

            _pageBindings ??= new LuaBindingScopeBridge(this, _luaRootScope, Resolver);
            return _pageBindings;
        }

        public UIBindingDiagnosticsSnapshot? GetLuaRootBindingDiagnostics()
        {
            return _luaRootScope?.GetDiagnosticsSnapshot();
        }

        public LuaBindingScopeBridge? GetComponentBindings(string componentId, string rootKey)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(componentId);
            ArgumentException.ThrowIfNullOrWhiteSpace(rootKey);

            if (Bindings == null || Resolver == null)
            {
                return null;
            }

            EnsureLuaRootScope();
            if (_luaRootScope == null)
            {
                return null;
            }

            string cacheKey = $"{componentId}@{rootKey}";
            if (_componentBindings.TryGetValue(cacheKey, out LuaBindingScopeBridge? cachedBindings))
            {
                return cachedBindings;
            }

            IUIObjectResolver subtreeResolver = Resolver.CreateSubtreeResolver(rootKey);
            IUIComponentBindingScope componentScope = _luaRootScope.CreateComponentScope(componentId, subtreeResolver);
            LuaBindingScopeBridge bridge = new(this, componentScope, subtreeResolver);
            _componentBindings[cacheKey] = bridge;
            return bridge;
        }

        public UIBindingDiagnosticsSnapshot? GetLuaComponentBindingDiagnostics(string componentId, string rootKey)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(componentId);
            ArgumentException.ThrowIfNullOrWhiteSpace(rootKey);

            string cacheKey = $"{componentId}@{rootKey}";
            return _componentBindings.TryGetValue(cacheKey, out LuaBindingScopeBridge? bridge)
                ? bridge.GetDiagnosticsSnapshot()
                : null;
        }

        public bool TryOpenPage(string pageId, IReadOnlyDictionary<string, object?>? arguments = null, bool pushHistory = true)
        {
            if (!TryGetNavigator(out IUIPageNavigator? navigator) || navigator == null)
            {
                return false;
            }

            navigator.Open(pageId, arguments, pushHistory);
            return true;
        }

        public bool TryGoBack()
        {
            return TryGetNavigator(out IUIPageNavigator? navigator) && navigator != null && navigator.GoBack();
        }

        [MoonSharpHidden]
        public AppResult Send(ICommand command)
        {
            ArgumentNullException.ThrowIfNull(command);

            try
            {
                return _context.RuntimeContext.CommandBus
                    .SendAsync(command)
                    .GetAwaiter()
                    .GetResult();
            }
            catch (Exception exception)
            {
                return AppResult.Fail("lua.command.exception", exception.Message);
            }
        }

        [MoonSharpHidden]
        public AppResult<TResult> Send<TResult>(ICommand<TResult> command)
        {
            ArgumentNullException.ThrowIfNull(command);

            try
            {
                return _context.RuntimeContext.CommandBus
                    .SendAsync(command)
                    .GetAwaiter()
                    .GetResult();
            }
            catch (Exception exception)
            {
                return AppResult<TResult>.Fail("lua.command.exception", exception.Message);
            }
        }

        [MoonSharpHidden]
        public AppResult<TResult> Query<TResult>(IQuery<TResult> query)
        {
            ArgumentNullException.ThrowIfNull(query);

            try
            {
                return _context.RuntimeContext.QueryBus
                    .QueryAsync(query)
                    .GetAwaiter()
                    .GetResult();
            }
            catch (Exception exception)
            {
                return AppResult<TResult>.Fail("lua.query.exception", exception.Message);
            }
        }

        public bool TryGetRedDot(string path, out bool hasRedDot)
        {
            return _redDotBridge.TryGetState(path, out hasRedDot);
        }

        public bool HasRuntimeProbeSnapshot()
        {
            AppResult<FacetRuntimeProbeStatusSnapshot> result = QueryStatusSnapshot();
            return result.IsSuccess && result.Value?.HasSnapshot == true;
        }

        public int GetRuntimeProbeRecordedCount(int fallback = 0)
        {
            AppResult<FacetRuntimeProbeStatusSnapshot> result = QueryStatusSnapshot();
            return result.IsSuccess && result.Value != null
                ? result.Value.RecordedCount
                : fallback;
        }

        public string GetRuntimeProbeSessionId(string fallback = "")
        {
            AppResult<FacetRuntimeProbeSnapshot> result = QuerySnapshot();
            return result.IsSuccess && result.Value != null
                ? result.Value.SessionId
                : fallback;
        }

        public bool GetRuntimeProbeHotReloadEnabled(bool fallback = false)
        {
            AppResult<FacetRuntimeProbeSnapshot> result = QuerySnapshot();
            return result.IsSuccess && result.Value != null
                ? result.Value.HotReloadEnabled
                : fallback;
        }

        public bool GetRuntimeProbePageCacheEnabled(bool fallback = false)
        {
            AppResult<FacetRuntimeProbeSnapshot> result = QuerySnapshot();
            return result.IsSuccess && result.Value != null
                ? result.Value.PageCacheEnabled
                : fallback;
        }

        public int GetRuntimeProbePageCacheCapacity(int fallback = 0)
        {
            AppResult<FacetRuntimeProbeSnapshot> result = QuerySnapshot();
            return result.IsSuccess && result.Value != null
                ? result.Value.PageCacheCapacity
                : fallback;
        }

        public void SetStateString(string key, string value)
        {
            _context.SetControllerState(key, value);
        }

        public string GetStateString(string key, string fallback = "")
        {
            if (_context.TryGetControllerState(key, out object? value) && value is string text)
            {
                return text;
            }

            return fallback;
        }

        public void SetStateNumber(string key, double value)
        {
            _context.SetControllerState(key, value);
        }

        public double GetStateNumber(string key, double fallback = 0)
        {
            if (_context.TryGetControllerState(key, out object? value))
            {
                return value switch
                {
                    double doubleValue => doubleValue,
                    float floatValue => floatValue,
                    int intValue => intValue,
                    long longValue => longValue,
                    _ => fallback,
                };
            }

            return fallback;
        }

        public void SetStateBoolean(string key, bool value)
        {
            _context.SetControllerState(key, value);
        }

        public bool GetStateBoolean(string key, bool fallback = false)
        {
            if (_context.TryGetControllerState(key, out object? value) && value is bool booleanValue)
            {
                return booleanValue;
            }

            return fallback;
        }

        public void ClearState(string key)
        {
            _context.RemoveControllerState(key);
        }

        public void SetStateStrings(string key, Table values)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ArgumentNullException.ThrowIfNull(values);

            List<string> snapshot = new();
            foreach (DynValue itemValue in values.Values)
            {
                if (itemValue.Type == DataType.String)
                {
                    snapshot.Add(itemValue.String ?? string.Empty);
                    continue;
                }

                if (itemValue.Type == DataType.Number)
                {
                    snapshot.Add(itemValue.Number.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    continue;
                }

                if (itemValue.Type == DataType.Boolean)
                {
                    snapshot.Add(itemValue.Boolean ? "true" : "false");
                }
            }

            _context.SetControllerState(key, snapshot.ToArray());
        }

        public void SetStructuredListState(string key, Table items)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ArgumentNullException.ThrowIfNull(items);

            List<LuaBindingScopeBridge.LuaStructuredListItem> snapshot = new();
            int index = 0;
            foreach (DynValue itemValue in items.Values)
            {
                if (itemValue.Type != DataType.Table || itemValue.Table == null)
                {
                    continue;
                }

                snapshot.Add(CreateStructuredListItem(itemValue.Table, index));
                index++;
            }

            _context.SetControllerState(key, snapshot.ToArray());
        }

        public void SetStructuredListState(string key, IReadOnlyList<LuaBindingScopeBridge.LuaStructuredListItem> items)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ArgumentNullException.ThrowIfNull(items);

            LuaBindingScopeBridge.LuaStructuredListItem[] snapshot = new LuaBindingScopeBridge.LuaStructuredListItem[items.Count];
            for (int index = 0; index < items.Count; index++)
            {
                snapshot[index] = items[index];
            }

            _context.SetControllerState(key, snapshot);
        }

        public void LogInfoText(string message)
        {
            LogInfo(message, null);
        }

        public void LogWarningText(string message)
        {
            LogWarning(message, null);
        }

        public void LogErrorText(string message)
        {
            LogError(message, null);
        }

        public void LogInfo(string message, IReadOnlyDictionary<string, object?>? payload = null)
        {
            _context.Logger.Info("Lua.Controller", message, CreatePayload(payload));
        }

        public void LogWarning(string message, IReadOnlyDictionary<string, object?>? payload = null)
        {
            _context.Logger.Warning("Lua.Controller", message, CreatePayload(payload));
        }

        public void LogError(string message, IReadOnlyDictionary<string, object?>? payload = null)
        {
            _context.Logger.Error("Lua.Controller", message, CreatePayload(payload));
        }

        public int GetStructuredListStateCount(string key)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            return GetStructuredListState(key).Count;
        }

        [MoonSharpHidden]
        public void PrepareForControllerReload()
        {
            _pageBindings?.ResetRegistrations();
            foreach (LuaBindingScopeBridge bridge in _componentBindings.Values)
            {
                bridge.ResetRegistrations();
            }

            _componentBindings.Clear();
            _luaRootScope?.Clear();
        }

        internal IReadOnlyList<string> GetStateStrings(string key)
        {
            if (_context.TryGetControllerState(key, out object? value) &&
                value is IReadOnlyList<string> list)
            {
                return list;
            }

            return Array.Empty<string>();
        }

        internal IReadOnlyList<LuaBindingScopeBridge.LuaStructuredListItem> GetStructuredListState(string key)
        {
            if (_context.TryGetControllerState(key, out object? value) &&
                value is IReadOnlyList<LuaBindingScopeBridge.LuaStructuredListItem> list)
            {
                return list;
            }

            return Array.Empty<LuaBindingScopeBridge.LuaStructuredListItem>();
        }

        private bool TryGetNavigator(out IUIPageNavigator? navigator)
        {
            return _context.Services.TryGet(out navigator);
        }

        private void EnsureLuaRootScope()
        {
            if (_luaRootScope != null || Bindings == null || Resolver == null)
            {
                return;
            }

            _luaRootScope = Bindings.CreateComponentScope(LuaRootComponentId, Resolver);
        }

        private AppResult<FacetRuntimeProbeSnapshot> QuerySnapshot()
        {
            try
            {
                return _context.RuntimeContext.QueryBus
                    .QueryAsync(new FacetRuntimeProbeQuery())
                    .GetAwaiter()
                    .GetResult();
            }
            catch (Exception exception)
            {
                return AppResult<FacetRuntimeProbeSnapshot>.Fail("lua.query.exception", exception.Message);
            }
        }

        private AppResult<FacetRuntimeProbeStatusSnapshot> QueryStatusSnapshot()
        {
            try
            {
                return _context.RuntimeContext.QueryBus
                    .QueryAsync(new FacetRuntimeProbeStatusQuery())
                    .GetAwaiter()
                    .GetResult();
            }
            catch (Exception exception)
            {
                return AppResult<FacetRuntimeProbeStatusSnapshot>.Fail("lua.query.exception", exception.Message);
            }
        }

        private Dictionary<string, object?> CreatePayload(IReadOnlyDictionary<string, object?>? payload)
        {
            Dictionary<string, object?> snapshot = new(StringComparer.OrdinalIgnoreCase)
            {
                ["pageId"] = PageId,
                ["layer"] = Layer,
                ["controllerScript"] = ControllerScript,
                ["argumentCount"] = Arguments.Count,
                ["controllerStateCount"] = _context.ControllerState.Count,
                ["hasResolver"] = HasResolver,
                ["hasBindings"] = HasBindings,
                ["componentBindingCount"] = _componentBindings.Count,
                ["canGoBack"] = CanGoBack,
                ["backStackDepth"] = BackStackDepth,
                ["supportsRedDot"] = SupportsRedDot,
            };

            if (payload == null)
            {
                return snapshot;
            }

            foreach (KeyValuePair<string, object?> pair in payload)
            {
                snapshot[pair.Key] = pair.Value;
            }

            return snapshot;
        }

        private static LuaBindingScopeBridge.LuaStructuredListItem CreateStructuredListItem(Table table, int index)
        {
            string key = GetTableString(table, "key", $"item_{index}");
            string primaryText = GetTableString(table, "primaryText", string.Empty);
            string secondaryText = GetTableString(table, "secondaryText", string.Empty);
            string tertiaryText = GetTableString(table, "tertiaryText", string.Empty);
            return new LuaBindingScopeBridge.LuaStructuredListItem(key, primaryText, secondaryText, tertiaryText);
        }

        private static string GetTableString(Table table, string fieldName, string fallback)
        {
            DynValue value = table.Get(fieldName);
            return value.Type switch
            {
                DataType.String => value.String ?? fallback,
                DataType.Number => value.Number.ToString(System.Globalization.CultureInfo.InvariantCulture),
                DataType.Boolean => value.Boolean ? "true" : "false",
                _ => fallback,
            };
        }
    }
}
