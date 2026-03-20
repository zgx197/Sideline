#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Sideline.Facet.Runtime;

namespace Sideline.Facet.UI
{
    /// <summary>
    /// 页面 Binding 服务。
    /// 负责为页面运行时创建独立的绑定作用域，并统一管理绑定对象生命周期。
    /// </summary>
    public sealed class UIBindingService
    {
        private readonly IFacetLogger? _logger;

        public UIBindingService(IFacetLogger? logger = null)
        {
            _logger = logger;
        }

        /// <summary>
        /// 为指定页面创建新的绑定作用域。
        /// </summary>
        public IUIBindingScope CreateScope(string pageId, IUIObjectResolver resolver)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pageId);
            ArgumentNullException.ThrowIfNull(resolver);

            UINodeBindingScope scope = new(pageId, resolver, _logger);
            _logger?.Info(
                "UI.Binding",
                "页面 BindingScope 已创建。",
                new Dictionary<string, object?>
                {
                    ["pageId"] = pageId,
                    ["scopeId"] = scope.ScopeId,
                    ["registeredKeys"] = resolver.GetRegisteredKeys().Count,
                });
            return scope;
        }

        /// <summary>
        /// 为页面内部复合区域创建组件级 Binding 作用域。
        /// </summary>
        public IUIComponentBindingScope CreateComponentScope(string parentScopeId, string componentId, IUIObjectResolver resolver)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(parentScopeId);
            ArgumentException.ThrowIfNullOrWhiteSpace(componentId);
            ArgumentNullException.ThrowIfNull(resolver);

            UIComponentBindingScope scope = new(parentScopeId, componentId, resolver, _logger);
            _logger?.Info(
                "UI.Binding",
                "组件 BindingScope 已创建。",
                new Dictionary<string, object?>
                {
                    ["parentScopeId"] = parentScopeId,
                    ["componentId"] = componentId,
                    ["scopeId"] = scope.ScopeId,
                    ["registeredKeys"] = resolver.GetRegisteredKeys().Count,
                });
            return scope;
        }
    }

    /// <summary>
    /// 基于节点解析器的绑定作用域实现。
    /// </summary>
    public class UINodeBindingScope : IUIBindingScope
    {
        private readonly string _scopeId;
        private readonly IUIObjectResolver _resolver;
        private readonly IFacetLogger? _logger;
        private readonly List<IUINodeBinding> _bindings = new();
        private readonly List<UIBindingDescriptor> _descriptors = new();
        private readonly List<IUIComponentBindingScope> _componentScopes = new();
        private bool _disposed;
        private int _refreshCount;
        private string? _lastRefreshReason;

        public UINodeBindingScope(string scopeId, IUIObjectResolver resolver, IFacetLogger? logger)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(scopeId);
            ArgumentNullException.ThrowIfNull(resolver);

            _scopeId = scopeId;
            _resolver = resolver;
            _logger = logger;
        }

        /// <inheritdoc />
        public string ScopeId => _scopeId;

        /// <inheritdoc />
        public int Count => _bindings.Count;

        /// <inheritdoc />
        public int RefreshCount => _refreshCount;

        /// <inheritdoc />
        public void BindText(string key, Func<string?> valueFactory)
        {
            EnsureNotDisposed();
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ArgumentNullException.ThrowIfNull(valueFactory);

            object target = ResolveRequired(key);
            RegisterBinding(
                new TextBinding(key, target, valueFactory),
                new UIBindingDescriptor("Text", key, target.GetType().Name));
        }

        /// <inheritdoc />
        public void BindVisibility(string key, Func<bool> valueFactory)
        {
            EnsureNotDisposed();
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ArgumentNullException.ThrowIfNull(valueFactory);

            CanvasItem target = ResolveRequired<CanvasItem>(key);
            RegisterBinding(
                new VisibilityBinding(target, valueFactory),
                new UIBindingDescriptor("Visibility", key, target.GetType().Name));
        }

        /// <inheritdoc />
        public void BindInteractable(string key, Func<bool> valueFactory)
        {
            EnsureNotDisposed();
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ArgumentNullException.ThrowIfNull(valueFactory);

            BaseButton target = ResolveRequired<BaseButton>(key);
            RegisterBinding(
                new InteractableBinding(target, valueFactory),
                new UIBindingDescriptor("Interactable", key, target.GetType().Name));
        }

        /// <inheritdoc />
        public void BindCommand(string key, Action handler)
        {
            EnsureNotDisposed();
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ArgumentNullException.ThrowIfNull(handler);

            BaseButton target = ResolveRequired<BaseButton>(key);
            RegisterBinding(
                new CommandBinding(target, handler),
                new UIBindingDescriptor("Command", key, target.GetType().Name, handler.Method.Name));
        }

        /// <inheritdoc />
        public IUIComponentBindingScope CreateComponentScope(string componentId, IUIObjectResolver resolver)
        {
            EnsureNotDisposed();
            ArgumentException.ThrowIfNullOrWhiteSpace(componentId);
            ArgumentNullException.ThrowIfNull(resolver);

            UIComponentBindingScope scope = new(ScopeId, componentId, resolver, _logger);
            _componentScopes.Add(scope);

            _logger?.Debug(
                "UI.Binding.Component",
                "组件级 BindingScope 已注册。",
                new Dictionary<string, object?>
                {
                    ["parentScopeId"] = ScopeId,
                    ["componentId"] = componentId,
                    ["scopeId"] = scope.ScopeId,
                    ["componentScopeCount"] = _componentScopes.Count,
                });

            return scope;
        }

        /// <inheritdoc />
        public void BindList(string key, Func<IReadOnlyList<string>> itemsFactory, string separator = "\n", string? emptyText = null)
        {
            EnsureNotDisposed();
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ArgumentNullException.ThrowIfNull(itemsFactory);

            Label target = ResolveRequired<Label>(key);
            RegisterBinding(
                new ListBinding(target, itemsFactory, separator, emptyText),
                new UIBindingDescriptor("List", key, target.GetType().Name));
        }

        /// <inheritdoc />
        public IUIComplexListBinding<TItem> BindComplexList<TItem>(
            string containerKey,
            string templateKey,
            Func<IReadOnlyList<TItem>> itemsFactory,
            IUIComplexListAdapter<TItem> adapter,
            string? emptyStateKey = null)
        {
            EnsureNotDisposed();
            ArgumentException.ThrowIfNullOrWhiteSpace(containerKey);
            ArgumentException.ThrowIfNullOrWhiteSpace(templateKey);
            ArgumentNullException.ThrowIfNull(itemsFactory);
            ArgumentNullException.ThrowIfNull(adapter);

            Container container = ResolveRequired<Container>(containerKey);
            Control template = ResolveRequired<Control>(templateKey);
            CanvasItem? emptyState = string.IsNullOrWhiteSpace(emptyStateKey)
                ? null
                : ResolveRequired<CanvasItem>(emptyStateKey);

            ComplexListBinding<TItem> binding = new(
                ScopeId,
                containerKey,
                templateKey,
                container,
                template,
                emptyState,
                itemsFactory,
                adapter,
                _logger);

            RegisterBinding(
                binding,
                new UIBindingDescriptor("ComplexList", containerKey, container.GetType().Name, $"template={templateKey}"));

            return binding;
        }

        /// <inheritdoc />
        public void RefreshAll(string? reason = null)
        {
            EnsureNotDisposed();

            reason ??= "manual";
            _refreshCount++;

            foreach (IUINodeBinding binding in _bindings)
            {
                binding.Apply();
            }

            foreach (IUIComponentBindingScope componentScope in _componentScopes)
            {
                componentScope.RefreshAll(reason);
            }

            if (ShouldLogRefresh(reason))
            {
                _logger?.Debug(
                    "UI.Binding.Refresh",
                    "BindingScope 已执行刷新。",
                    new Dictionary<string, object?>
                    {
                        ["scopeId"] = ScopeId,
                        ["bindingCount"] = Count,
                        ["componentScopeCount"] = _componentScopes.Count,
                        ["refreshCount"] = _refreshCount,
                        ["reason"] = reason,
                        ["bindingKeysPreview"] = GetBindingKeysPreview(),
                    });
            }

            _lastRefreshReason = reason;
        }

        /// <inheritdoc />
        public UIBindingDiagnosticsSnapshot GetDiagnosticsSnapshot()
        {
            return new UIBindingDiagnosticsSnapshot(
                ScopeId,
                Count,
                _refreshCount,
                _lastRefreshReason,
                _descriptors.ToArray());
        }

        /// <inheritdoc />
        public void Clear()
        {
            foreach (IUINodeBinding binding in _bindings)
            {
                binding.Dispose();
            }

            _bindings.Clear();
            _descriptors.Clear();

            foreach (IUIComponentBindingScope componentScope in _componentScopes)
            {
                componentScope.Dispose();
            }

            _componentScopes.Clear();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            UIBindingDiagnosticsSnapshot snapshot = GetDiagnosticsSnapshot();
            Clear();
            _disposed = true;

            _logger?.Info(
                "UI.Binding",
                "页面 BindingScope 已释放。",
                new Dictionary<string, object?>
                {
                    ["scopeId"] = snapshot.ScopeId,
                    ["bindingCount"] = snapshot.BindingCount,
                    ["refreshCount"] = snapshot.RefreshCount,
                    ["lastRefreshReason"] = snapshot.LastRefreshReason,
                });
        }

        private void RegisterBinding(IUINodeBinding binding, UIBindingDescriptor descriptor)
        {
            _bindings.Add(binding);
            _descriptors.Add(descriptor);

            _logger?.Debug(
                "UI.Binding.Register",
                "Binding 已注册。",
                new Dictionary<string, object?>
                {
                    ["scopeId"] = ScopeId,
                    ["bindingKind"] = descriptor.Kind,
                    ["key"] = descriptor.Key,
                    ["targetType"] = descriptor.TargetType,
                    ["notes"] = descriptor.Notes,
                    ["bindingCount"] = Count,
                });
        }

        private object ResolveRequired(string key)
        {
            return _resolver.GetRequired(key);
        }

        private TNode ResolveRequired<TNode>(string key) where TNode : class
        {
            object value = _resolver.GetRequired(key);
            if (value is TNode typedValue)
            {
                return typedValue;
            }

            throw new InvalidOperationException($"Facet binding target type mismatch: {key} -> {value.GetType().FullName}, expected {typeof(TNode).FullName}");
        }

        private bool ShouldLogRefresh(string reason)
        {
            if (!string.Equals(_lastRefreshReason, reason, StringComparison.Ordinal))
            {
                return true;
            }

            return _refreshCount <= 3 || _refreshCount % 20 == 0;
        }

        private string GetBindingKeysPreview()
        {
            if (_descriptors.Count == 0)
            {
                return string.Empty;
            }

            return string.Join(", ", _descriptors.Take(5).Select(static descriptor => descriptor.Key));
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
            {
                throw new InvalidOperationException($"Facet binding scope already disposed: {ScopeId}");
            }
        }
    }

    /// <summary>
    /// 组件级 Binding 作用域实现。
    /// </summary>
    public sealed class UIComponentBindingScope : UINodeBindingScope, IUIComponentBindingScope
    {
        public UIComponentBindingScope(string parentScopeId, string componentId, IUIObjectResolver resolver, IFacetLogger? logger)
            : base($"{parentScopeId}/{componentId}", resolver, logger)
        {
            ParentScopeId = parentScopeId;
            ComponentId = componentId;
        }

        /// <inheritdoc />
        public string ComponentId { get; }

        /// <inheritdoc />
        public string ParentScopeId { get; }
    }

    /// <summary>
    /// 绑定条目内部协议。
    /// </summary>
    internal interface IUINodeBinding : IDisposable
    {
        void Apply();
    }

    /// <summary>
    /// 文本绑定。
    /// 支持 Label 与 Button 两类常见控件。
    /// </summary>
    internal sealed class TextBinding : IUINodeBinding
    {
        private readonly string _key;
        private readonly object _target;
        private readonly Func<string?> _valueFactory;

        public TextBinding(string key, object target, Func<string?> valueFactory)
        {
            _key = key;
            _target = target;
            _valueFactory = valueFactory;
        }

        public void Apply()
        {
            string text = _valueFactory() ?? string.Empty;

            switch (_target)
            {
                case Label label:
                    label.Text = text;
                    break;
                case Button button:
                    button.Text = text;
                    break;
                default:
                    throw new InvalidOperationException($"Facet text binding target is not supported: {_key} -> {_target.GetType().FullName}");
            }
        }

        public void Dispose()
        {
        }
    }

    /// <summary>
    /// 显隐绑定。
    /// </summary>
    internal sealed class VisibilityBinding : IUINodeBinding
    {
        private readonly CanvasItem _target;
        private readonly Func<bool> _valueFactory;

        public VisibilityBinding(CanvasItem target, Func<bool> valueFactory)
        {
            _target = target;
            _valueFactory = valueFactory;
        }

        public void Apply()
        {
            _target.Visible = _valueFactory();
        }

        public void Dispose()
        {
        }
    }

    /// <summary>
    /// 可交互状态绑定。
    /// 当前最小实现先面向按钮控件。
    /// </summary>
    internal sealed class InteractableBinding : IUINodeBinding
    {
        private readonly BaseButton _target;
        private readonly Func<bool> _valueFactory;

        public InteractableBinding(BaseButton target, Func<bool> valueFactory)
        {
            _target = target;
            _valueFactory = valueFactory;
        }

        public void Apply()
        {
            _target.Disabled = !_valueFactory();
        }

        public void Dispose()
        {
        }
    }

    /// <summary>
    /// 按钮命令绑定。
    /// </summary>
    internal sealed class CommandBinding : IUINodeBinding
    {
        private readonly BaseButton _target;
        private readonly Callable _callable;

        public CommandBinding(BaseButton target, Action handler)
        {
            _target = target;
            _callable = Callable.From(handler);

            if (!_target.IsConnected(BaseButton.SignalName.Pressed, _callable))
            {
                _target.Connect(BaseButton.SignalName.Pressed, _callable);
            }
        }

        public void Apply()
        {
        }

        public void Dispose()
        {
            if (GodotObject.IsInstanceValid(_target) && _target.IsConnected(BaseButton.SignalName.Pressed, _callable))
            {
                _target.Disconnect(BaseButton.SignalName.Pressed, _callable);
            }
        }
    }

    /// <summary>
    /// 简单文本列表绑定。
    /// 当前最小实现把字符串列表拼接到 Label。
    /// </summary>
    internal sealed class ListBinding : IUINodeBinding
    {
        private readonly Label _target;
        private readonly Func<IReadOnlyList<string>> _itemsFactory;
        private readonly string _separator;
        private readonly string? _emptyText;

        public ListBinding(Label target, Func<IReadOnlyList<string>> itemsFactory, string separator, string? emptyText)
        {
            _target = target;
            _itemsFactory = itemsFactory;
            _separator = separator;
            _emptyText = emptyText;
        }

        public void Apply()
        {
            IReadOnlyList<string> items = _itemsFactory();
            if (items.Count == 0)
            {
                _target.Text = _emptyText ?? string.Empty;
                return;
            }

            _target.Text = string.Join(_separator, items);
        }

        public void Dispose()
        {
        }
    }

    /// <summary>
    /// 基于模板节点的复杂列表绑定。
    /// 每个子项都会生成独立的组件级 Binding 作用域。
    /// </summary>
    internal sealed class ComplexListBinding<TItem> : IUINodeBinding, IUIComplexListBinding<TItem>
    {
        private readonly string _scopeId;
        private readonly string _containerKey;
        private readonly string _templateKey;
        private readonly Container _container;
        private readonly Control _template;
        private readonly CanvasItem? _emptyState;
        private readonly Func<IReadOnlyList<TItem>> _itemsFactory;
        private readonly IUIComplexListAdapter<TItem> _adapter;
        private readonly IFacetLogger? _logger;
        private readonly Dictionary<string, ComplexListItemRuntime> _itemRuntimes = new(StringComparer.Ordinal);
        private readonly List<string> _itemKeys = new();
        private bool _disposed;

        public ComplexListBinding(
            string scopeId,
            string containerKey,
            string templateKey,
            Container container,
            Control template,
            CanvasItem? emptyState,
            Func<IReadOnlyList<TItem>> itemsFactory,
            IUIComplexListAdapter<TItem> adapter,
            IFacetLogger? logger)
        {
            _scopeId = scopeId;
            _containerKey = containerKey;
            _templateKey = templateKey;
            _container = container;
            _template = template;
            _emptyState = emptyState;
            _itemsFactory = itemsFactory;
            _adapter = adapter;
            _logger = logger;

            _template.Visible = false;
        }

        public string Key => _containerKey;

        public int ItemCount => _itemKeys.Count;

        public IReadOnlyList<string> ItemKeys => _itemKeys;

        public void Apply()
        {
            Refresh("binding.apply");
        }

        public void Refresh(string? reason = null)
        {
            EnsureNotDisposed();

            IReadOnlyList<TItem> items = _itemsFactory();
            HashSet<string> activeKeys = new(StringComparer.Ordinal);
            _itemKeys.Clear();

            for (int index = 0; index < items.Count; index++)
            {
                TItem item = items[index];
                string itemKey = _adapter.GetItemKey(item, index);
                if (string.IsNullOrWhiteSpace(itemKey) || activeKeys.Contains(itemKey))
                {
                    itemKey = $"item_{index}_{SanitizeKey(itemKey)}";
                }

                activeKeys.Add(itemKey);
                _itemKeys.Add(itemKey);

                ComplexListItemRuntime runtime = GetOrCreateItemRuntime(itemKey, index);
                runtime.Scope.Clear();
                _adapter.BindItem(runtime.Scope, item, index);
                runtime.Scope.RefreshAll(reason ?? "complex_list.refresh");

                int templateIndex = _template.GetIndex();
                _container.MoveChild(runtime.RootNode, templateIndex + index + 1);
            }

            List<string> removedKeys = new();
            foreach (string existingKey in _itemRuntimes.Keys)
            {
                if (!activeKeys.Contains(existingKey))
                {
                    removedKeys.Add(existingKey);
                }
            }

            foreach (string removedKey in removedKeys)
            {
                RemoveItemRuntime(removedKey);
            }

            bool hasItems = items.Count > 0;
            _template.Visible = false;
            _container.Visible = hasItems;
            if (_emptyState != null)
            {
                _emptyState.Visible = !hasItems;
            }

            _logger?.Debug(
                "UI.Binding.ComplexList",
                "复杂列表 Binding 已刷新。",
                new Dictionary<string, object?>
                {
                    ["scopeId"] = _scopeId,
                    ["containerKey"] = _containerKey,
                    ["templateKey"] = _templateKey,
                    ["itemCount"] = _itemKeys.Count,
                    ["itemKeys"] = string.Join(", ", _itemKeys),
                    ["reason"] = reason,
                });
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            foreach (string itemKey in _itemRuntimes.Keys.ToArray())
            {
                RemoveItemRuntime(itemKey);
            }

            _disposed = true;
        }

        private ComplexListItemRuntime GetOrCreateItemRuntime(string itemKey, int index)
        {
            if (_itemRuntimes.TryGetValue(itemKey, out ComplexListItemRuntime? existingRuntime))
            {
                return existingRuntime;
            }

            Control itemRoot = DuplicateTemplate(itemKey, index);
            UINodeResolver itemResolver = new(UINodeRegistry.CreateFromSubtree(itemRoot));
            UIComponentBindingScope itemScope = new(_scopeId, $"{_containerKey}[{itemKey}]", itemResolver, _logger);

            ComplexListItemRuntime runtime = new(itemKey, itemRoot, itemScope);
            _itemRuntimes[itemKey] = runtime;
            return runtime;
        }

        private Control DuplicateTemplate(string itemKey, int index)
        {
            if (_template.Duplicate() is not Control itemRoot)
            {
                throw new InvalidOperationException($"Facet complex list template is not a Control: {_templateKey}");
            }

            itemRoot.Name = $"{_template.Name}_{index}_{SanitizeKey(itemKey)}";
            itemRoot.Visible = true;
            _container.AddChild(itemRoot);
            return itemRoot;
        }

        private void RemoveItemRuntime(string itemKey)
        {
            if (!_itemRuntimes.Remove(itemKey, out ComplexListItemRuntime? runtime) || runtime == null)
            {
                return;
            }

            runtime.Scope.Dispose();
            if (GodotObject.IsInstanceValid(runtime.RootNode))
            {
                runtime.RootNode.QueueFree();
            }
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
            {
                throw new InvalidOperationException($"Facet complex list binding already disposed: {_containerKey}");
            }
        }

        private static string SanitizeKey(string itemKey)
        {
            return string.IsNullOrWhiteSpace(itemKey)
                ? "empty"
                : itemKey.Replace("/", "_", StringComparison.Ordinal)
                    .Replace("\\", "_", StringComparison.Ordinal)
                    .Replace(":", "_", StringComparison.Ordinal)
                    .Replace(" ", "_", StringComparison.Ordinal);
        }

        private sealed record ComplexListItemRuntime(
            string ItemKey,
            Control RootNode,
            UIComponentBindingScope Scope);
    }
}