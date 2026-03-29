#nullable enable

using System;
using System.Collections.Generic;
using Sideline.Facet.Lua;

namespace Sideline.Facet.Runtime
{
    /// <summary>
    /// 椤甸潰杩愯鏃朵笂涓嬫枃銆?    /// 鐢ㄤ簬鎶婇〉闈㈠畾涔夈€佽繍琛屾椂鏈嶅姟銆佽妭鐐硅В鏋愬櫒銆佺粦瀹氫綔鐢ㄥ煙銆丩ua 妗ユ帴銆佹帶鍒跺櫒鐘舵€佽涓庡綋鍓嶅弬鏁扮粺涓€浜ょ粰椤甸潰鐢熷懡鍛ㄦ湡浣跨敤銆?    /// </summary>
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

        public void ClearRuntimeReferences()
        {
            Resolver = null;
            Bindings = null;
            Lua = null;
            Arguments = new Dictionary<string, object?>();
            _controllerState.Clear();
        }
    }
}