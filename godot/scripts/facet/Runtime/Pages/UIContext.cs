#nullable enable

using System;
using System.Collections.Generic;
using Sideline.Facet.Lua;

namespace Sideline.Facet.Runtime
{
    /// <summary>
    /// 页面运行时上下文。
    /// 用于把页面定义、运行时服务、节点解析器、绑定作用域、Lua 桥接、控制器状态袋与当前参数统一交给页面生命周期使用。
    /// </summary>
    public sealed class UIContext
    {
        private readonly Dictionary<string, object?> _controllerState = new(StringComparer.OrdinalIgnoreCase);

        public UIContext(UIPageDefinition definition, FacetRuntimeContext runtimeContext)
        {
            ArgumentNullException.ThrowIfNull(definition);
            ArgumentNullException.ThrowIfNull(runtimeContext);

            Definition = definition;
            RuntimeContext = runtimeContext;
            Arguments = new Dictionary<string, object?>();
        }

        public UIPageDefinition Definition { get; }

        public string PageId => Definition.PageId;

        public string Layer => Definition.Layer;

        public FacetRuntimeContext RuntimeContext { get; }

        public FacetServices Services => RuntimeContext.Services;

        public IFacetLogger Logger => RuntimeContext.Logger;

        public IUIObjectResolver? Resolver { get; private set; }

        public IUIBindingScope? Bindings { get; private set; }

        public LuaApiBridge? Lua { get; private set; }

        public IReadOnlyDictionary<string, object?> Arguments { get; private set; }

        public IReadOnlyDictionary<string, object?> ControllerState => _controllerState;

        public void AttachResolver(IUIObjectResolver resolver)
        {
            ArgumentNullException.ThrowIfNull(resolver);
            Resolver = resolver;
        }

        public void AttachBindings(IUIBindingScope bindings)
        {
            ArgumentNullException.ThrowIfNull(bindings);
            Bindings = bindings;
        }

        public void AttachLua(LuaApiBridge lua)
        {
            ArgumentNullException.ThrowIfNull(lua);
            Lua = lua;
        }

        public void UpdateArguments(IReadOnlyDictionary<string, object?>? arguments)
        {
            if (arguments == null || arguments.Count == 0)
            {
                Arguments = new Dictionary<string, object?>();
                return;
            }

            Dictionary<string, object?> snapshot = new(arguments.Count, StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, object?> pair in arguments)
            {
                snapshot[pair.Key] = pair.Value;
            }

            Arguments = snapshot;
        }

        public void SetControllerState(string key, object? value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            _controllerState[key] = value;
        }

        public bool TryGetControllerState(string key, out object? value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            return _controllerState.TryGetValue(key, out value);
        }

        public void RemoveControllerState(string key)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            _controllerState.Remove(key);
        }
    }
}