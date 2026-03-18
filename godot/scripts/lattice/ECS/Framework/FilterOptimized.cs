// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;
using System.Runtime.CompilerServices;
using Lattice.Core;
using Lattice.ECS.Core;

namespace Lattice.ECS.Framework
{
    /// <summary>
    /// 优化版 Filter - 集成裁剪和位图检查
    /// 
    /// 相比基础版 Filter 的改进：
    /// 1. 预计算组件位图掩码（避免多次 Storage.Has 调用）
    /// 2. 集成 CullingSystem 检查
    /// 3. 批量位图检查（一次检查 64 个实体）
    /// </summary>
    public unsafe class FilterOptimized<T1> where T1 : unmanaged
    {
        private readonly Frame* _frame;
        private readonly Storage<T1>* _storage1;
        private readonly CullingSystem* _culling;

        public FilterOptimized(Frame* frame, CullingSystem* culling = null)
        {
            _frame = frame;
            _storage1 = frame->GetStoragePointer<T1>();
            _culling = culling;
        }

        public Enumerator GetEnumerator() => new Enumerator(_frame, _storage1, _culling);

        public ref struct Enumerator
        {
            private readonly Frame* _frame;
            private readonly Storage<T1>* _storage1;
            private readonly CullingSystem* _culling;
            private ComponentBlockIterator<T1> _iterator;

            public Enumerator(Frame* frame, Storage<T1>* storage1, CullingSystem* culling)
            {
                _frame = frame;
                _storage1 = storage1;
                _culling = culling;
                _iterator = storage1 != null ? new ComponentBlockIterator<T1>(storage1) : default;
            }

            public bool MoveNext()
            {
                if (_storage1 == null) return false;

                while (_iterator.Next(out var entity, out var ptr))
                {
                    // 裁剪检查
                    if (_culling != null && _culling->IsCulled(entity.Index))
                        continue;

                    CurrentEntity = entity;
                    CurrentPtr = ptr;
                    return true;
                }
                return false;
            }

            public EntityRef CurrentEntity;
            public T1* CurrentPtr;
            public ref T1 Component => ref *CurrentPtr;
        }
    }

    /// <summary>
    /// 双组件优化 Filter - 使用位图预检查
    /// </summary>
    public unsafe class FilterOptimized<T1, T2>
        where T1 : unmanaged
        where T2 : unmanaged
    {
        private readonly Frame* _frame;
        private readonly Storage<T1>* _storage1;
        private readonly Storage<T2>* _storage2;
        private readonly CullingSystem* _culling;
        private readonly bool _useStorage2AsPrimary;

        // 预计算的组件类型 ID（用于位图检查）
        private readonly int _typeId1;
        private readonly int _typeId2;

        public FilterOptimized(Frame* frame, CullingSystem* culling = null)
        {
            _frame = frame;
            _culling = culling;
            _typeId1 = ComponentTypeId<T1>.Id;
            _typeId2 = ComponentTypeId<T2>.Id;

            _storage1 = frame->GetStoragePointer<T1>();
            _storage2 = frame->GetStoragePointer<T2>();

            // 选择组件数最少的存储作为主遍历源
            int count1 = _storage1 != null ? _storage1->Count : int.MaxValue;
            int count2 = _storage2 != null ? _storage2->Count : int.MaxValue;
            _useStorage2AsPrimary = count2 < count1;
        }

        public Enumerator GetEnumerator()
        {
            return new Enumerator(_frame, _storage1, _storage2, _culling,
                _useStorage2AsPrimary, _typeId1, _typeId2);
        }

        public ref struct Enumerator
        {
            private readonly Frame* _frame;
            private readonly Storage<T1>* _storage1;
            private readonly Storage<T2>* _storage2;
            private readonly CullingSystem* _culling;
            private readonly bool _useStorage2AsPrimary;
            private readonly int _typeId1;
            private readonly int _typeId2;

            private ComponentBlockIterator<T1> _iterator1;
            private ComponentBlockIterator<T2> _iterator2;

            public Enumerator(Frame* frame, Storage<T1>* s1, Storage<T2>* s2,
                CullingSystem* culling, bool useS2, int typeId1, int typeId2)
            {
                _frame = frame;
                _storage1 = s1;
                _storage2 = s2;
                _culling = culling;
                _useStorage2AsPrimary = useS2;
                _typeId1 = typeId1;
                _typeId2 = typeId2;

                if (useS2 && s2 != null)
                    _iterator2 = new ComponentBlockIterator<T2>(s2);
                else if (s1 != null)
                    _iterator1 = new ComponentBlockIterator<T1>(s1);
            }

            public bool MoveNext()
            {
                if (_useStorage2AsPrimary)
                {
                    return MoveNextPrimary2();
                }
                else
                {
                    return MoveNextPrimary1();
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool MoveNextPrimary1()
            {
                if (_storage1 == null) return false;

                while (_iterator1.Next(out var entity, out var ptr1))
                {
                    // 裁剪检查
                    if (_culling != null && _culling->IsCulled(entity.Index))
                        continue;

                    // 使用位图检查替代 Storage.Has（更快）
                    if (!HasComponentBit(entity.Index, _typeId2))
                        continue;

                    CurrentEntity = entity;
                    CurrentPtr1 = ptr1;
                    CurrentPtr2 = _storage2->GetPointer(entity);
                    return true;
                }
                return false;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool MoveNextPrimary2()
            {
                if (_storage2 == null) return false;

                while (_iterator2.Next(out var entity, out var ptr2))
                {
                    // 裁剪检查
                    if (_culling != null && _culling->IsCulled(entity.Index))
                        continue;

                    // 使用位图检查替代 Storage.Has
                    if (!HasComponentBit(entity.Index, _typeId1))
                        continue;

                    CurrentEntity = entity;
                    CurrentPtr1 = _storage1->GetPointer(entity);
                    CurrentPtr2 = ptr2;
                    return true;
                }
                return false;
            }

            /// <summary>
            /// 使用位图检查组件是否存在（比 Storage.Has 更快）
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool HasComponentBit(int entityIndex, int typeId)
            {
                // 获取实体的组件位图
                ulong* mask = _frame->GetComponentMaskPointer(entityIndex);
                int block = typeId >> 6;      // / 64
                int bit = typeId & 0x3F;      // % 64
                return (mask[block] & (1UL << bit)) != 0;
            }

            public EntityRef CurrentEntity;
            public T1* CurrentPtr1;
            public T2* CurrentPtr2;

            public ref T1 Component1 => ref *CurrentPtr1;
            public ref T2 Component2 => ref *CurrentPtr2;
        }
    }
}
