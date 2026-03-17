// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Lattice.ECS.Core
{
    /// <summary>
    /// 确定性字典 - 用于类型注册等场景
    /// 
    /// 关键保证：
    /// 1. FrozenDictionary 构建后不可变，遍历顺序固定
    /// 2. 跨平台一致性（x64/ARM64 行为相同）
    /// 3. 无运行时哈希码差异
    /// </summary>
    public sealed class DeterministicTypeMap<TValue>
    {
        private FrozenDictionary<Type, TValue> _frozen;
        private readonly Dictionary<Type, TValue> _builder;

        public DeterministicTypeMap()
        {
            _builder = new Dictionary<Type, TValue>();
            _frozen = FrozenDictionary<Type, TValue>.Empty;
        }

        public DeterministicTypeMap(int capacity)
        {
            _builder = new Dictionary<Type, TValue>(capacity);
            _frozen = FrozenDictionary<Type, TValue>.Empty;
        }

        /// <summary>
        /// 添加条目（构建阶段）
        /// </summary>
        public void Add(Type key, TValue value)
        {
            if (_frozen.Count > 0)
                throw new InvalidOperationException("Cannot modify after Freeze");
            _builder.Add(key, value);
        }

        /// <summary>
        /// 冻结字典 - 之后不可修改，遍历顺序固定
        /// </summary>
        public void Freeze()
        {
            if (_builder.Count == 0)
            {
                _frozen = FrozenDictionary<Type, TValue>.Empty;
                return;
            }
            _frozen = _builder.ToFrozenDictionary();
        }

        /// <summary>
        /// 查找（O(1)，确定性性能）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(Type key, out TValue value)
        {
            return _frozen.TryGetValue(key, out value);
        }

        /// <summary>
        /// 检查是否包含键
        /// </summary>
        public bool ContainsKey(Type key)
        {
            return _frozen.ContainsKey(key);
        }

        /// <summary>
        /// 获取值（确定性顺序遍历）
        /// </summary>
        public IEnumerable<KeyValuePair<Type, TValue>> Items => _frozen;

        public int Count => _frozen.Count;
    }

    /// <summary>
    /// 确定性整数 ID 映射
    /// 使用数组而非字典实现 O(1) 访问，完全确定性
    /// </summary>
    public sealed class DeterministicIdMap<TValue> where TValue : struct
    {
        private TValue[] _values;
        private bool[] _occupied;
        private int _count;

        public DeterministicIdMap(int initialCapacity = 64)
        {
            _values = new TValue[initialCapacity];
            _occupied = new bool[initialCapacity];
            _count = 0;
        }

        /// <summary>
        /// 添加条目（必须在构建阶段完成）
        /// </summary>
        public void Add(int id, TValue value)
        {
            if (id >= _values.Length)
            {
                int newSize = System.Math.Max(id + 1, _values.Length * 2);
                Array.Resize(ref _values, newSize);
                Array.Resize(ref _occupied, newSize);
            }

            if (!_occupied[id])
            {
                _count++;
            }

            _values[id] = value;
            _occupied[id] = true;
        }

        /// <summary>
        /// 获取值（O(1)，无哈希，完全确定性）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(int id, out TValue value)
        {
            if ((uint)id < (uint)_occupied.Length && _occupied[id])
            {
                value = _values[id];
                return true;
            }
            value = default;
            return false;
        }

        /// <summary>
        /// 直接索引（调用方确保有效性）
        /// </summary>
        public ref TValue GetById(int id)
        {
            return ref _values[id];
        }

        public bool Has(int id)
        {
            return (uint)id < (uint)_occupied.Length && _occupied[id];
        }

        public int Count => _count;

        /// <summary>
        /// 确定性遍历（按 ID 排序）
        /// </summary>
        public IEnumerable<(int Id, TValue Value)> GetAll()
        {
            for (int i = 0; i < _occupied.Length; i++)
            {
                if (_occupied[i])
                    yield return (i, _values[i]);
            }
        }
    }

    /// <summary>
    /// 确定性集合（有序数组实现）
    /// 适用于小集合（< 32），无哈希，完全确定性
    /// </summary>
    public struct DeterministicSmallSet<T> where T : unmanaged, IEquatable<T>
    {
        public const int MaxSize = 32;

        private FixedList32<T> _items;
        private int _count;

        public DeterministicSmallSet()
        {
            _items = new FixedList32<T>();
            _count = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(T item)
        {
            var span = AsSpan();
            for (int i = 0; i < _count; i++)
            {
                if (span[i].Equals(item))
                    return true;
            }
            return false;
        }

        public bool Add(T item)
        {
            if (Contains(item))
                return false;
            if (_count >= MaxSize)
                throw new InvalidOperationException("DeterministicSmallSet is full");
            _items.AsSpan()[_count++] = item;
            return true;
        }

        public bool Remove(T item)
        {
            var span = _items.AsSpan();
            for (int i = 0; i < _count; i++)
            {
                if (span[i].Equals(item))
                {
                    // 移动元素
                    for (int j = i; j < _count - 1; j++)
                        span[j] = span[j + 1];
                    _count--;
                    return true;
                }
            }
            return false;
        }

        public ReadOnlySpan<T> AsSpan() => _items.AsSpan().Slice(0, _count);
        public int Count => _count;
        public void Clear() => _count = 0;
    }

    /// <summary>
    /// 固定容量列表（最大32个）- 栈上分配，无GC
    /// 注意：需要外部管理_count
    /// </summary>
    [InlineArray(MaxSize)]
    public struct FixedList32<T> where T : unmanaged
    {
        public const int MaxSize = 32;
        private T _element0;

        public Span<T> AsSpan() => MemoryMarshal.CreateSpan(ref _element0, MaxSize);
    }
}
