#nullable enable

using System;
using System.Collections.Generic;
using Godot;

namespace Sideline.Facet.UI
{
    /// <summary>
    /// 节点注册表。
    /// 负责维护稳定的 Node Key 到 Godot 节点实例之间的映射关系。
    /// </summary>
    public sealed class UINodeRegistry
    {
        private readonly Dictionary<string, Node> _nodes = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 当前已注册节点数量。
        /// </summary>
        public int Count => _nodes.Count;

        /// <summary>
        /// 获取全部已注册节点键。
        /// </summary>
        public IReadOnlyCollection<string> Keys => _nodes.Keys;

        /// <summary>
        /// 注册节点。
        /// 若键已存在，则保留首次注册结果，避免后续重复节点覆盖稳定键。
        /// </summary>
        public void Register(string key, Node node)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ArgumentNullException.ThrowIfNull(node);

            if (_nodes.ContainsKey(key))
            {
                return;
            }

            _nodes[key] = node;
        }

        /// <summary>
        /// 检查指定键是否已注册。
        /// </summary>
        public bool Contains(string key)
        {
            return _nodes.ContainsKey(key);
        }

        /// <summary>
        /// 尝试按键获取节点。
        /// </summary>
        public bool TryGet(string key, out Node? node)
        {
            return _nodes.TryGetValue(key, out node);
        }

        /// <summary>
        /// 尝试按键获取指定类型的节点。
        /// </summary>
        public bool TryGet<TNode>(string key, out TNode? node) where TNode : Node
        {
            if (_nodes.TryGetValue(key, out Node? rawNode) && rawNode is TNode typedNode)
            {
                node = typedNode;
                return true;
            }

            node = null;
            return false;
        }

        /// <summary>
        /// 获取必须存在的节点。
        /// </summary>
        public Node GetRequired(string key)
        {
            if (TryGet(key, out Node? node) && node != null)
            {
                return node;
            }

            throw new InvalidOperationException($"Facet node key not registered: {key}");
        }

        /// <summary>
        /// 基于指定根节点创建一份新的节点注册表。
        /// 可用于整页、组件子树和复杂列表项模板子树。
        /// </summary>
        public static UINodeRegistry CreateFromSubtree(Node rootNode)
        {
            ArgumentNullException.ThrowIfNull(rootNode);

            UINodeRegistry registry = new();
            RegisterNodeRecursive(rootNode, rootNode, registry);
            return registry;
        }

        private static void RegisterNodeRecursive(Node rootNode, Node currentNode, UINodeRegistry registry)
        {
            string currentName = currentNode.Name.ToString();
            registry.Register(currentName, currentNode);

            string relativePath = rootNode == currentNode
                ? "."
                : rootNode.GetPathTo(currentNode).ToString();
            registry.Register($"path:{relativePath}", currentNode);

            if (currentNode.HasMeta("facet_node_key"))
            {
                Variant metaValue = currentNode.GetMeta("facet_node_key");
                string customKey = metaValue.ToString();
                if (!string.IsNullOrWhiteSpace(customKey))
                {
                    registry.Register(customKey, currentNode);
                }
            }

            foreach (Node child in currentNode.GetChildren())
            {
                RegisterNodeRecursive(rootNode, child, registry);
            }
        }
    }
}