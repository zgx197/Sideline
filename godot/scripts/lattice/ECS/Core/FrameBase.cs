// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Lattice.Core;
using Lattice.Math;

namespace Lattice.ECS.Core
{
    /// <summary>
    /// Frame 基类 - FrameSync 风格集中式 ECS 管理
    /// 
    /// 核心设计：
    /// 1. 实体数据集中存储（EntityInfo* _info）
    /// 2. 组件位图集中管理（ulong* _componentMasks）
    /// 3. 组件存储数组（ComponentDataBuffer* _buffers）
    /// 4. 双层检查：先查位图（极快），再查稀疏数组
    /// </summary>
    public unsafe class FrameBase : IDisposable
    {
        #region 常量

        public const int ComponentStartIndex = 1; // 索引0保留
        public const int CulledWordBitSize = 64;
        public const int ComponentMaskMaxBlockCount = 8; // 512位

        #endregion

        #region 字段

        // 实体管理
        private int _usedCount;
        private int _freeCount;
        private int _freeHead;
        private int _capacity;
        private int _capacityActual;

        /// <summary>实体信息数组 [Index] → EntityInfo</summary>
        internal EntityInfo* _info;

        /// <summary>裁剪标记位图</summary>
        internal long* _culled;

        /// <summary>组件位图数组 [EntityIndex * BlockCount + Block] → 64位掩码</summary>
        internal ulong* _componentMasks;

        /// <summary>每个实体的位图块数（512组件 = 8块）</summary>
        internal int _componentMaskBlockCount;

        /// <summary>位图索引移位（log2(BlockCount)）</summary>
        internal int _componentMaskIndexShift;

        /// <summary>组件存储缓冲区数组</summary>
        internal ComponentDataBuffer* _buffers;

        /// <summary>缓冲区数量（最大组件类型数）</summary>
        internal int _bufferCount;

        #endregion

        #region 属性

        /// <summary>活跃实体数量</summary>
        public int EntityCount => _usedCount;

        /// <summary>实体容量</summary>
        public int Capacity => _capacity;

        /// <summary>DeltaTime</summary>
        public FP DeltaTime { get; set; }

        /// <summary>当前帧号</summary>
        public int FrameNumber { get; set; }

        #endregion

        #region 构造与销毁

        public FrameBase(int maxComponentTypes = 512, int initialEntityCapacity = 1024)
        {
            _componentMaskBlockCount = (maxComponentTypes + 63) / 64;
            _componentMaskIndexShift = (int)System.Math.Log2(_componentMaskBlockCount);

            EnsureEntityCapacity(initialEntityCapacity);
            EnsureComponentCapacity(maxComponentTypes);
        }

        public void Dispose()
        {
            if (_buffers != null)
            {
                for (int i = 0; i < _bufferCount; i++)
                {
                    if (_buffers[i].Blocks != null)
                        _buffers[i].Free();
                }
                Marshal.FreeHGlobal((IntPtr)_buffers);
                _buffers = null;
            }

            if (_info != null)
            {
                Marshal.FreeHGlobal((IntPtr)_info);
                _info = null;
            }

            if (_componentMasks != null)
            {
                Marshal.FreeHGlobal((IntPtr)_componentMasks);
                _componentMasks = null;
            }

            if (_culled != null)
            {
                Marshal.FreeHGlobal((IntPtr)_culled);
                _culled = null;
            }
        }

        #endregion

        #region 实体管理

        /// <summary>
        /// 创建新实体
        /// </summary>
        public EntityRef CreateEntity()
        {
            int index;
            int version;

            if (_freeCount > 0)
            {
                // 复用空闲槽位
                index = _freeHead;
                _freeHead = _info[index].Ref.Index; // 链表下一个
                _freeCount--;

                version = _info[index].Ref.Version & EntityInfo.VersionMask;
                version++; // 版本递增
                version |= EntityInfo.ActiveBit; // 标记为活跃
            }
            else
            {
                // 新槽位
                if (_usedCount >= _capacity)
                    EnsureEntityCapacity(_capacity * 2);

                index = _usedCount++;
                version = 1 | EntityInfo.ActiveBit;
            }

            _info[index].Ref = new EntityRef(index, version);
            _info[index].Flags = EntityFlags.None;

            // 清空组件位图
            ulong* mask = GetComponentMask(index);
            for (int i = 0; i < _componentMaskBlockCount; i++)
                mask[i] = 0;

            return _info[index].Ref;
        }

        /// <summary>
        /// 销毁实体（延迟执行，实际在 Commit 时清理）
        /// </summary>
        public void DestroyEntity(EntityRef entityRef)
        {
            if (!Exists(entityRef)) return;

            // 标记为待销毁（实际清理在 CommitCommands 时执行）
            _info[entityRef.Index].Flags |= EntityFlags.Destroyed;
        }

        /// <summary>
        /// 立即销毁实体（不安全，谨慎使用）
        /// </summary>
        internal void DestroyEntityImmediate(EntityRef entityRef)
        {
            if (!Exists(entityRef)) return;

            int index = entityRef.Index;

            // 移除所有组件
            ulong* mask = GetComponentMask(index);
            for (int i = ComponentStartIndex; i < _bufferCount; i++)
            {
                int block = i / 64;
                int bit = i % 64;
                if ((mask[block] & (1UL << bit)) != 0)
                {
                    _buffers[i].Remove(entityRef);
                }
            }

            // 清空位图
            for (int i = 0; i < _componentMaskBlockCount; i++)
                mask[i] = 0;

            // 加入空闲链表
            int version = (_info[index].Ref.Version + 1) & EntityInfo.VersionMask;
            _info[index].Ref = new EntityRef(_freeHead, version);
            _freeHead = index;
            _freeCount++;
            _usedCount--;
        }

        /// <summary>
        /// 检查实体是否存在且有效
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Exists(EntityRef entityRef)
        {
            if ((uint)entityRef.Index >= (uint)_capacity) return false;
            return _info[entityRef.Index].Ref.Version == entityRef.Version;
        }

        /// <summary>
        /// 检查实体是否活跃
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsAlive(EntityRef entityRef)
        {
            if ((uint)entityRef.Index >= (uint)_capacity) return false;
            ref var info = ref _info[entityRef.Index];
            return info.Ref.Version == entityRef.Version && info.IsActive;
        }

        #endregion

        #region 组件访问（零开销抽象）

        /// <summary>
        /// 获取组件指针（极快路径：先查位图，再查稀疏数组）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T* GetPointer<T>(EntityRef entityRef) where T : unmanaged, IComponent
        {
            CheckExistsAndThrow(entityRef);
            return (T*)_buffers[ComponentTypeId<T>.Id].GetDataPointer(entityRef);
        }

        /// <summary>
        /// 尝试获取组件指针（最快路径，内联优化）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetPointer<T>(EntityRef entityRef, out T* value) where T : unmanaged, IComponent
        {
            int index = ComponentTypeId<T>.Id;

            // 第一层：边界和版本检查
            if ((uint)entityRef.Index >= (uint)_capacity ||
                _info[entityRef.Index].Ref.Version != entityRef.Version)
            {
                value = null;
                return false;
            }

            // 第二层：位图检查（极快，缓存友好）
            ulong* mask = GetComponentMask(entityRef.Index);
            int block = index / 64;
            int bit = index % 64;

            if ((mask[block] & (1UL << bit)) == 0)
            {
                value = null;
                return false;
            }

            // 第三层：直接获取指针（调用方已验证存在）
            value = (T*)_buffers[index].GetDataPointerFastUnsafe(entityRef);
            return true;
        }

        /// <summary>
        /// 检查是否有组件
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Has<T>(EntityRef entityRef) where T : unmanaged, IComponent
        {
            if ((uint)entityRef.Index >= (uint)_capacity) return false;
            if (_info[entityRef.Index].Ref.Version != entityRef.Version) return false;

            int index = ComponentTypeId<T>.Id;
            ulong* mask = GetComponentMask(entityRef.Index);
            return (mask[index / 64] & (1UL << (index % 64))) != 0;
        }

        /// <summary>
        /// 检查是否有组件（按 ID）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Has(EntityRef entityRef, int componentId)
        {
            if ((uint)entityRef.Index >= (uint)_capacity) return false;
            if (_info[entityRef.Index].Ref.Version != entityRef.Version) return false;

            ulong* mask = GetComponentMask(entityRef.Index);
            return (mask[componentId / 64] & (1UL << (componentId % 64))) != 0;
        }

        /// <summary>
        /// 添加组件
        /// </summary>
        public T* Add<T>(EntityRef entityRef, T component) where T : unmanaged, IComponent
        {
            CheckExistsAndThrow(entityRef);

            int index = ComponentTypeId<T>.Id;
            var buffer = &_buffers[index];

            // 确保稀疏数组容量
            if (entityRef.Index >= buffer->SparseCapacity)
                buffer->ChangeEntityCapacity(System.Math.Max(entityRef.Index + 1, _capacity));

            // 设置位图
            ulong* mask = GetComponentMask(entityRef.Index);
            mask[index / 64] |= (1UL << (index % 64));

            // 添加组件数据
            return buffer->Set(entityRef, component);
        }

        /// <summary>
        /// 移除组件
        /// </summary>
        public void Remove<T>(EntityRef entityRef) where T : unmanaged, IComponent
        {
            CheckExistsAndThrow(entityRef);

            int index = ComponentTypeId<T>.Id;

            // 清除位图
            ulong* mask = GetComponentMask(entityRef.Index);
            mask[index / 64] &= ~(1UL << (index % 64));

            // 移除数据
            _buffers[index].Remove(entityRef);
        }

        #endregion

        #region 批量操作

        /// <summary>
        /// 获取组件块迭代器（批量处理）
        /// </summary>
        public ComponentBlockIterator<T> GetComponentBlockIterator<T>() where T : unmanaged, IComponent
        {
            // FrameBase 是引用类型，this 本身就是指针
            // 获取 this 的指针（FrameBase 是类，this 是托管引用）
            var handle = GCHandle.Alloc(this, GCHandleType.Pinned);
            try
            {
                return new ComponentBlockIterator<T>((FrameBase*)handle.AddrOfPinnedObject(), &_buffers[ComponentTypeId<T>.Id]);
            }
            finally
            {
                handle.Free();
            }
        }

        // Query 方法在后续实现

        #endregion

        #region 内部辅助

        /// <summary>
        /// 获取实体的组件位图指针
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ulong* GetComponentMask(int entityIndex)
        {
            return _componentMasks + (entityIndex << _componentMaskIndexShift);
        }

        /// <summary>
        /// 获取组件数据缓冲区指针
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ComponentDataBuffer* GetBuffer(int componentId)
        {
            return &_buffers[componentId];
        }

        /// <summary>
        /// 确保实体容量
        /// </summary>
        private void EnsureEntityCapacity(int newCapacity)
        {
            if (newCapacity <= _capacityActual) return;

            int newActual = System.Math.Max(newCapacity, _capacityActual * 2);
            if (newActual < 64) newActual = 64;

            // 重新分配实体信息
            var newInfo = (EntityInfo*)Marshal.AllocHGlobal(sizeof(EntityInfo) * newActual);
            if (_info != null)
            {
                Unsafe.CopyBlock(newInfo, _info, (uint)(sizeof(EntityInfo) * _capacity));
                Marshal.FreeHGlobal((IntPtr)_info);
            }
            Unsafe.InitBlock(newInfo + _capacity, 0, (uint)(sizeof(EntityInfo) * (newActual - _capacity)));
            _info = newInfo;

            // 重新分配组件位图
            int maskSize = newActual * _componentMaskBlockCount;
            var newMasks = (ulong*)Marshal.AllocHGlobal(sizeof(ulong) * maskSize);
            if (_componentMasks != null)
            {
                int oldMaskSize = _capacity * _componentMaskBlockCount;
                Unsafe.CopyBlock(newMasks, _componentMasks, (uint)(sizeof(ulong) * oldMaskSize));
                Marshal.FreeHGlobal((IntPtr)_componentMasks);
            }
            Unsafe.InitBlock(newMasks + (_capacity * _componentMaskBlockCount), 0,
                (uint)(sizeof(ulong) * (maskSize - _capacity * _componentMaskBlockCount)));
            _componentMasks = newMasks;

            // 重新分配裁剪标记
            int culledSize = (newActual + 63) / 64;
            var newCulled = (long*)Marshal.AllocHGlobal(sizeof(long) * culledSize);
            if (_culled != null)
            {
                int oldCulledSize = (_capacity + 63) / 64;
                Unsafe.CopyBlock(newCulled, _culled, (uint)(sizeof(long) * oldCulledSize));
                Marshal.FreeHGlobal((IntPtr)_culled);
            }
            _culled = newCulled;

            _capacity = newCapacity;
            _capacityActual = newActual;
        }

        /// <summary>
        /// 确保组件缓冲区容量
        /// </summary>
        private void EnsureComponentCapacity(int maxTypes)
        {
            if (_buffers != null && maxTypes <= _bufferCount) return;

            int newCount = System.Math.Max(maxTypes, _bufferCount * 2);
            if (newCount < 64) newCount = 64;

            var newBuffers = (ComponentDataBuffer*)Marshal.AllocHGlobal(sizeof(ComponentDataBuffer) * newCount);
            if (_buffers != null)
            {
                Unsafe.CopyBlock(newBuffers, _buffers, (uint)(sizeof(ComponentDataBuffer) * _bufferCount));
                Marshal.FreeHGlobal((IntPtr)_buffers);
            }
            Unsafe.InitBlock(newBuffers + _bufferCount, 0,
                (uint)(sizeof(ComponentDataBuffer) * (newCount - _bufferCount)));

            _buffers = newBuffers;
            _bufferCount = newCount;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CheckExistsAndThrow(EntityRef entityRef)
        {
            if (!Exists(entityRef))
                throw new ArgumentException($"Entity {entityRef} does not exist", nameof(entityRef));
        }

        #endregion
    }
}
