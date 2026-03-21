#nullable enable

using System;
using System.Collections.Generic;
using Godot;
using Sideline.Facet.Runtime;

namespace Sideline.Facet.UI
{
    /// <summary>
    /// 节点解析器。
    /// 对外暴露稳定 Node Key，而不是直接暴露 Godot 路径。
    /// </summary>
    public sealed class UINodeResolver : IUIObjectResolver
    {
        private readonly UINodeRegistry _registry;

        public UINodeResolver(UINodeRegistry registry)
        {
            ArgumentNullException.ThrowIfNull(registry);
            _registry = registry;
        }

        /// <summary>
        /// 当前已注册节点数量。
        /// </summary>
        public int Count => _registry.Count;

        /// <inheritdoc />
        public IReadOnlyCollection<string> GetRegisteredKeys()
        {
            return _registry.Keys;
        }

        /// <inheritdoc />
        public bool TryResolve(string key, out object? value)
        {
            bool found = _registry.TryGet(key, out Node? node);
            value = node;
            return found;
        }

        /// <inheritdoc />
        public object GetRequired(string key)
        {
            return _registry.GetRequired(key);
        }

        /// <summary>
        /// 获取必须存在的指定类型节点。
        /// </summary>
        public TNode GetRequired<TNode>(string key) where TNode : Node
        {
            if (_registry.TryGet(key, out TNode? node) && node != null)
            {
                return node;
            }

            throw new InvalidOperationException($"Facet node key not registered with expected type: {key} -> {typeof(TNode).FullName}");
        }

        /// <summary>
        /// 尝试获取指定类型节点。
        /// </summary>
        public bool TryGet<TNode>(string key, out TNode? node) where TNode : Node
        {
            return _registry.TryGet(key, out node);
        }

        /// <summary>
        /// 基于某个子树根节点创建局部解析器。
        /// 局部解析器只暴露该区域内部的稳定节点键。
        /// </summary>
        public UINodeResolver CreateSubtreeResolver(string rootKey)
        {
            Node rootNode = GetRequired<Node>(rootKey);
            UINodeRegistry subtreeRegistry = UINodeRegistry.CreateFromSubtree(rootNode);
            return new UINodeResolver(subtreeRegistry);
        }

        IUIObjectResolver IUIObjectResolver.CreateSubtreeResolver(string rootKey)
        {
            return CreateSubtreeResolver(rootKey);
        }
    }
}
