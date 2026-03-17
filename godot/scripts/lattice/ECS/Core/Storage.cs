// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;
using System.Runtime.CompilerServices;
using Lattice.Core;

namespace Lattice.ECS.Core
{
    /// <summary>
    /// 高性能组件存储 - Sparse Set 实现（FrameSync 风格）
    /// 
    /// 设计目标：
    /// 1. O(1) 随机访问，最少内存访问次数
    /// 2. 顺序遍历缓存友好
    /// 3. 零 GC 压力
    /// 4. 极简内存布局
    /// </summary>
    public unsafe struct Storage<T> where T : unmanaged
    {
        #region 常量

        /// <summary>稀疏数组 Tombstone 标记</summary>
        public const int TOMBSTONE = -1;

        #endregion

        #region 字段

        /// <summary>稀疏数组：Entity.Index -> DenseIndex（或 TOMBSTONE）</summary>
        private int* _sparse;

        /// <summary>密集数组：实体引用（与组件一一对应）</summary>
        private EntityRef* _denseEntities;

        /// <summary>密集数组：组件数据</summary>
        private T* _denseComponents;

        /// <summary>密集数组当前元素数</summary>
        private int _denseCount;

        /// <summary>稀疏数组容量（最大实体数）</summary>
        private int _sparseCapacity;

        /// <summary>密集数组容量</summary>
        private int _denseCapacity;

        #endregion

        #region 属性

        /// <summary>当前组件数量</summary>
        public int Count => _denseCount;

        /// <summary>是否为空</summary>
        public bool IsEmpty => _denseCount == 0;

        /// <summary>密集数组实体指针（用于遍历）</summary>
        public EntityRef* DenseEntities => _denseEntities;

        /// <summary>密集数组组件指针（用于遍历）</summary>
        public T* DenseComponents => _denseComponents;

        #endregion

        #region 生命周期

        /// <summary>
        /// 初始化存储
        /// </summary>
        public void Initialize(int maxEntities, int initialCapacity = 64)
        {
            _sparseCapacity = maxEntities;
            _denseCapacity = System.Math.Max(initialCapacity, 64);
            _denseCount = 0;

            // 分配非托管内存
            _sparse = (int*)System.Runtime.InteropServices.Marshal.AllocHGlobal(
                sizeof(int) * maxEntities).ToPointer();
            _denseEntities = (EntityRef*)System.Runtime.InteropServices.Marshal.AllocHGlobal(
                sizeof(EntityRef) * _denseCapacity).ToPointer();
            _denseComponents = (T*)System.Runtime.InteropServices.Marshal.AllocHGlobal(
                sizeof(T) * _denseCapacity).ToPointer();

            // 初始化稀疏数组为 TOMBSTONE
            for (int i = 0; i < maxEntities; i++)
                _sparse[i] = TOMBSTONE;
        }

        /// <summary>
        /// 释放内存
        /// </summary>
        public void Dispose()
        {
            if (_sparse != null)
            {
                System.Runtime.InteropServices.Marshal.FreeHGlobal((IntPtr)_sparse);
                _sparse = null;
            }
            if (_denseEntities != null)
            {
                System.Runtime.InteropServices.Marshal.FreeHGlobal((IntPtr)_denseEntities);
                _denseEntities = null;
            }
            if (_denseComponents != null)
            {
                System.Runtime.InteropServices.Marshal.FreeHGlobal((IntPtr)_denseComponents);
                _denseComponents = null;
            }
            _denseCount = 0;
        }

        #endregion

        #region 核心操作

        /// <summary>
        /// 添加组件 - O(1) 均摊
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(EntityRef entity, in T component)
        {
            int index = entity.Index;

#if DEBUG
            if ((uint)index >= (uint)_sparseCapacity)
                throw new ArgumentOutOfRangeException(nameof(entity), "Entity index out of range");
            if (_sparse[index] != TOMBSTONE)
                throw new InvalidOperationException($"Component already exists for entity {entity}");
#endif

            // 确保密集数组容量
            if (_denseCount >= _denseCapacity)
                GrowDense();

            // 添加到密集数组末尾
            int denseIndex = _denseCount++;
            _denseEntities[denseIndex] = entity;
            _denseComponents[denseIndex] = component;

            // 更新稀疏数组
            _sparse[index] = denseIndex;
        }

        /// <summary>
        /// 删除组件 - O(1)，与末尾交换保持密集
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove(EntityRef entity)
        {
            int index = entity.Index;
            int denseIndex = _sparse[index];

#if DEBUG
            if ((uint)index >= (uint)_sparseCapacity || denseIndex == TOMBSTONE)
                throw new InvalidOperationException($"Component not found for entity {entity}");
#endif

            int lastIndex = --_denseCount;

            // 如果不是最后一个，与末尾交换
            if (denseIndex != lastIndex)
            {
                EntityRef lastEntity = _denseEntities[lastIndex];
                _denseEntities[denseIndex] = lastEntity;
                _denseComponents[denseIndex] = _denseComponents[lastIndex];

                // 更新被移动实体的稀疏索引
                _sparse[lastEntity.Index] = denseIndex;
            }

            // 标记为已删除
            _sparse[index] = TOMBSTONE;
        }

        /// <summary>
        /// 获取组件引用 - O(1)，2 次内存访问
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T Get(EntityRef entity)
        {
            int denseIndex = _sparse[entity.Index];
#if DEBUG
            if ((uint)entity.Index >= (uint)_sparseCapacity || denseIndex == TOMBSTONE)
                throw new InvalidOperationException($"Component not found for entity {entity}");
#endif
            return ref _denseComponents[denseIndex];
        }

        /// <summary>
        /// 获取组件指针 - O(1)，2 次内存访问
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T* GetPointer(EntityRef entity)
        {
            int denseIndex = _sparse[entity.Index];
            return denseIndex != TOMBSTONE ? &_denseComponents[denseIndex] : null;
        }

        /// <summary>
        /// 尝试获取组件 - O(1)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGet(EntityRef entity, out T component)
        {
            int index = entity.Index;
            if ((uint)index < (uint)_sparseCapacity)
            {
                int denseIndex = _sparse[index];
                if (denseIndex != TOMBSTONE)
                {
                    component = _denseComponents[denseIndex];
                    return true;
                }
            }
            component = default;
            return false;
        }

        /// <summary>
        /// 检查是否存在 - O(1)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Has(EntityRef entity)
        {
            uint index = (uint)entity.Index;
            return index < (uint)_sparseCapacity && _sparse[index] != TOMBSTONE;
        }

        #endregion

        #region 遍历支持

        /// <summary>
        /// 获取所有实体到 Span（零拷贝）
        /// </summary>
        public ReadOnlySpan<EntityRef> GetEntities()
        {
            return new ReadOnlySpan<EntityRef>(_denseEntities, _denseCount);
        }

        /// <summary>
        /// 获取所有组件到 Span（零拷贝）
        /// </summary>
        public Span<T> GetComponents()
        {
            return new Span<T>(_denseComponents, _denseCount);
        }

        /// <summary>
        /// 遍历所有组件（高性能指针遍历）
        /// </summary>
        public void ForEach(delegate*<EntityRef, T*, void> action)
        {
            for (int i = 0; i < _denseCount; i++)
                action(_denseEntities[i], &_denseComponents[i]);
        }

        #endregion

        #region 内部辅助

        private void GrowDense()
        {
            int newCapacity = _denseCapacity * 2;

            var newEntities = (EntityRef*)System.Runtime.InteropServices.Marshal.AllocHGlobal(
                sizeof(EntityRef) * newCapacity).ToPointer();
            var newComponents = (T*)System.Runtime.InteropServices.Marshal.AllocHGlobal(
                sizeof(T) * newCapacity).ToPointer();

            // 复制旧数据
            System.Buffer.MemoryCopy(_denseEntities, newEntities,
                sizeof(EntityRef) * newCapacity, sizeof(EntityRef) * _denseCount);
            System.Buffer.MemoryCopy(_denseComponents, newComponents,
                sizeof(T) * newCapacity, sizeof(T) * _denseCount);

            // 释放旧内存
            System.Runtime.InteropServices.Marshal.FreeHGlobal((IntPtr)_denseEntities);
            System.Runtime.InteropServices.Marshal.FreeHGlobal((IntPtr)_denseComponents);

            _denseEntities = newEntities;
            _denseComponents = newComponents;
            _denseCapacity = newCapacity;
        }

        #endregion
    }
}
