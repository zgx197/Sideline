// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Lattice.ECS.Core
{
    /// <summary>
    /// 实体注册表，对齐 FrameSync 设计
    /// 使用稀疏集（Sparse Set）实现高效的创建、销毁和存在性检查
    /// </summary>
    public unsafe class EntityRegistry : IDisposable
    {
        /// <summary>
        /// 实体槽位结构
        /// </summary>
        private struct EntitySlot
        {
            /// <summary>实体版本（用于验证实体有效性）</summary>
            public int Version;

            /// <summary>下一个空闲槽位的索引（仅当槽位空闲时有效）</summary>
            public int NextFree;

            /// <summary>此槽位是否正在被使用</summary>
            public bool IsAlive;
        }

        // 实体槽位数组（稀疏集）
        private EntitySlot* _slots;
        private int _slotCapacity;
        private int _slotCount;

        // 密集数组：存储活跃实体的索引（用于快速遍历）
        private int* _dense;
        private int _denseCount;
        private int _denseCapacity;

        // 空闲链表头
        private int _freeHead;

        /// <summary>当前活跃实体数量</summary>
        public int Count => _denseCount;

        /// <summary>总槽位数量（包含已销毁的）</summary>
        public int Capacity => _slotCount;

        /// <summary>当前容量（可用于验证边界）</summary>
        internal int SlotCapacity => _slotCapacity;

        public EntityRegistry(int initialCapacity = 1024)
        {
            _slotCapacity = global::System.Math.Max(16, initialCapacity);
            _denseCapacity = _slotCapacity;
            _freeHead = -1;

            _slots = (EntitySlot*)NativeMemory.AlignedAlloc(
                (nuint)(_slotCapacity * sizeof(EntitySlot)), 8);

            _dense = (int*)NativeMemory.AlignedAlloc(
                (nuint)(_denseCapacity * sizeof(int)), 8);

            // 初始化所有槽位
            for (int i = 0; i < _slotCapacity; i++)
            {
                _slots[i].Version = 0;
                _slots[i].NextFree = -1;
                _slots[i].IsAlive = false;
            }
        }

        /// <summary>
        /// 创建新实体
        /// </summary>
        public Entity Create()
        {
            int index;
            int version;

            if (_freeHead >= 0)
            {
                // 复用空闲槽位
                index = _freeHead;
                ref var slot = ref _slots[index];

                // 验证槽位确实已死亡
                if (slot.IsAlive)
                    throw new InvalidOperationException("Entity slot in free list is marked as alive");

                version = slot.Version;
                _freeHead = slot.NextFree;

                slot.IsAlive = true;

                // 添加到密集数组
                EnsureDenseCapacity();
                _dense[_denseCount] = index;
                _denseCount++;
            }
            else
            {
                // 分配新槽位
                EnsureSlotCapacity();
                index = _slotCount;
                version = 1;  // 初始版本为 1（0 表示无效）

                ref var slot = ref _slots[index];
                slot.Version = version;
                slot.IsAlive = true;
                slot.NextFree = -1;

                _slotCount++;

                // 添加到密集数组
                EnsureDenseCapacity();
                _dense[_denseCount] = index;
                _denseCount++;
            }

            return new Entity(index, version);
        }

        /// <summary>
        /// 销毁实体
        /// </summary>
        public void Destroy(Entity entity)
        {
            int index = entity.Index;

            if (index < 0 || index >= _slotCount)
                return;

            ref var slot = ref _slots[index];

            // 验证版本
            if (slot.Version != entity.Version || !slot.IsAlive)
                return;

            // 标记为死亡并增加版本
            slot.IsAlive = false;
            int newVersion = slot.Version + 1;
            if (newVersion == 0) newVersion = 1;  // 版本 0 保留为无效
            slot.Version = newVersion;

            // 加入空闲链表
            slot.NextFree = _freeHead;
            _freeHead = index;

            // 从密集数组中移除（与最后一个元素交换）
            for (int i = 0; i < _denseCount; i++)
            {
                if (_dense[i] == index)
                {
                    _dense[i] = _dense[_denseCount - 1];
                    _denseCount--;
                    break;
                }
            }
        }

        /// <summary>
        /// 检查实体是否存在且有效
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Exists(Entity entity)
        {
            int index = entity.Index;

            if (index < 0 || index >= _slotCount)
                return false;

            ref var slot = ref _slots[index];
            return slot.IsAlive && slot.Version == entity.Version;
        }

        /// <summary>
        /// 获取指定索引的实体（如果有效）
        /// </summary>
        public bool TryGetEntity(int index, out Entity entity)
        {
            if (index < 0 || index >= _slotCount)
            {
                entity = Entity.None;
                return false;
            }

            ref var slot = ref _slots[index];
            if (!slot.IsAlive)
            {
                entity = Entity.None;
                return false;
            }

            entity = new Entity(index, slot.Version);
            return true;
        }

        /// <summary>
        /// 获取所有活跃实体（密集数组遍历）
        /// </summary>
        public Span<int> GetAliveEntityIndices()
        {
            return new Span<int>(_dense, _denseCount);
        }

        /// <summary>
        /// 确保槽位容量足够
        /// </summary>
        private void EnsureSlotCapacity()
        {
            if (_slotCount < _slotCapacity) return;

            int newCapacity = _slotCapacity * 2;
            var newSlots = (EntitySlot*)NativeMemory.AlignedAlloc(
                (nuint)(newCapacity * sizeof(EntitySlot)), 8);

            // 复制旧数据
            Buffer.MemoryCopy(_slots, newSlots,
                _slotCount * sizeof(EntitySlot),
                _slotCount * sizeof(EntitySlot));

            // 初始化新槽位
            for (int i = _slotCapacity; i < newCapacity; i++)
            {
                newSlots[i].Version = 0;
                newSlots[i].NextFree = -1;
                newSlots[i].IsAlive = false;
            }

            NativeMemory.AlignedFree(_slots);
            _slots = newSlots;
            _slotCapacity = newCapacity;
        }

        /// <summary>
        /// 确保密集数组容量足够
        /// </summary>
        private void EnsureDenseCapacity()
        {
            if (_denseCount < _denseCapacity) return;

            int newCapacity = _denseCapacity * 2;
            var newDense = (int*)NativeMemory.AlignedAlloc(
                (nuint)(newCapacity * sizeof(int)), 8);

            Buffer.MemoryCopy(_dense, newDense,
                _denseCount * sizeof(int),
                _denseCount * sizeof(int));

            NativeMemory.AlignedFree(_dense);
            _dense = newDense;
            _denseCapacity = newCapacity;
        }

        /// <summary>
        /// 清除所有实体
        /// </summary>
        public void Clear()
        {
            // 重置所有槽位
            for (int i = 0; i < _slotCount; i++)
            {
                _slots[i].IsAlive = false;
                _slots[i].NextFree = -1;
                // 保留版本号，使旧引用失效
            }

            _freeHead = -1;
            _denseCount = 0;
        }

        /// <summary>
        /// 复制到另一个注册表
        /// </summary>
        public void CopyTo(EntityRegistry other)
        {
            // 确保目标容量足够
            while (other._slotCapacity < _slotCount)
            {
                other.EnsureSlotCapacity();
            }

            // 复制槽位数据
            Buffer.MemoryCopy(_slots, other._slots,
                _slotCount * sizeof(EntitySlot),
                _slotCount * sizeof(EntitySlot));

            other._slotCount = _slotCount;
            other._freeHead = _freeHead;

            // 复制密集数组
            while (other._denseCapacity < _denseCount)
            {
                other.EnsureDenseCapacity();
            }

            Buffer.MemoryCopy(_dense, other._dense,
                _denseCount * sizeof(int),
                _denseCount * sizeof(int));

            other._denseCount = _denseCount;
        }

        /// <summary>
        /// 从另一个注册表复制
        /// </summary>
        public void CopyFrom(EntityRegistry other)
        {
            other.CopyTo(this);
        }

        /// <summary>
        /// 释放所有资源
        /// </summary>
        public void Dispose()
        {
            if (_slots != null)
            {
                NativeMemory.AlignedFree(_slots);
                _slots = null;
            }

            if (_dense != null)
            {
                NativeMemory.AlignedFree(_dense);
                _dense = null;
            }

            _slotCapacity = 0;
            _slotCount = 0;
            _denseCapacity = 0;
            _denseCount = 0;
            _freeHead = -1;
        }
    }
}
