// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;
using System.Runtime.CompilerServices;
using Lattice.Core;

namespace Lattice.ECS.Core
{
    internal unsafe interface IFrameOwningGroup
    {
        bool DependsOn(int typeId);

        void Reinitialize(Allocator* allocator, int entityCapacity);

        void SyncEntity(Frame frame, EntityRef entity, int changedTypeId);

        void Rebuild(Frame frame);
    }

    /// <summary>
    /// 显式注册的双组件 Owning Group。
    /// 该结构由 Frame 自动维护，适合极少数固定热点系统的高频遍历。
    /// </summary>
    public unsafe sealed class OwningGroup<T1, T2> : IFrameOwningGroup
        where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
    {
        private readonly int _typeId1;
        private readonly int _typeId2;
        private int _entityCapacity;
        private int* _entityToDenseIndex;
        private FullOwningGroup<T1, T2> _group;

        internal OwningGroup(Allocator* allocator, int entityCapacity, int typeId1, int typeId2)
        {
            _typeId1 = typeId1;
            _typeId2 = typeId2;
            Reinitialize(allocator, entityCapacity);
        }

        public int Count => _group.Count;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool NextBlock(int* blockIndex, out EntityRef* entities, out T1* data1, out T2* data2, out int count)
        {
            return _group.NextBlock(blockIndex, out entities, out data1, out data2, out count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Next(int* globalIndex, out EntityRef entity, out T1* c1, out T2* c2)
        {
            return _group.Next(globalIndex, out entity, out c1, out c2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool DependsOn(int typeId)
        {
            return typeId == _typeId1 || typeId == _typeId2;
        }

        public void Reinitialize(Allocator* allocator, int entityCapacity)
        {
            _entityCapacity = entityCapacity;
            _entityToDenseIndex = (int*)allocator->Alloc(sizeof(int) * entityCapacity);
            _group = default;
            _group.Initialize(allocator);
            ResetDenseIndexMap();
        }

        public void SyncEntity(Frame frame, EntityRef entity, int changedTypeId)
        {
            _ = changedTypeId;

            int denseIndex = GetDenseIndex(entity.Index);
            if (!ShouldInclude(frame, entity))
            {
                if (denseIndex >= 0)
                {
                    RemoveDenseIndex(entity.Index, denseIndex);
                }

                return;
            }

            T1* component1 = frame.GetPointer<T1>(entity);
            T2* component2 = frame.GetPointer<T2>(entity);
            if (component1 == null || component2 == null)
            {
                if (denseIndex >= 0)
                {
                    RemoveDenseIndex(entity.Index, denseIndex);
                }

                return;
            }

            if (denseIndex >= 0)
            {
                if (_group.GetAtLinearIndex(denseIndex, out EntityRef _, out T1* group1, out T2* group2))
                {
                    *group1 = *component1;
                    *group2 = *component2;
                }

                return;
            }

            int newIndex = _group.Count;
            _group.Add(entity, *component1, *component2);
            _entityToDenseIndex[entity.Index] = newIndex;
        }

        public void Rebuild(Frame frame)
        {
            ResetDenseIndexMap();
            while (_group.Count > 0)
            {
                _group.RemoveAtLinearIndex(_group.Count - 1, out _);
            }

            var query = frame.Query<T1, T2>();
            var enumerator = query.GetEnumerator();
            while (enumerator.MoveNext())
            {
                int denseIndex = _group.Count;
                _group.Add(enumerator.Entity, *enumerator.Component1Ptr, *enumerator.Component2Ptr);
                _entityToDenseIndex[enumerator.Entity.Index] = denseIndex;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ShouldInclude(Frame frame, EntityRef entity)
        {
            if (!frame.IsValid(entity))
            {
                return false;
            }

            return frame.HasComponentBit(entity.Index, _typeId1)
                && frame.HasComponentBit(entity.Index, _typeId2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetDenseIndex(int entityIndex)
        {
            return (uint)entityIndex < (uint)_entityCapacity ? _entityToDenseIndex[entityIndex] : -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RemoveDenseIndex(int entityIndex, int denseIndex)
        {
            if (!_group.RemoveAtLinearIndex(denseIndex, out EntityRef movedEntity))
            {
                return;
            }

            _entityToDenseIndex[entityIndex] = -1;
            if (movedEntity != EntityRef.None)
            {
                _entityToDenseIndex[movedEntity.Index] = denseIndex;
            }
        }

        private void ResetDenseIndexMap()
        {
            for (int i = 0; i < _entityCapacity; i++)
            {
                _entityToDenseIndex[i] = -1;
            }
        }
    }

    /// <summary>
    /// 显式注册的三组件 Owning Group。
    /// </summary>
    public unsafe sealed class OwningGroup<T1, T2, T3> : IFrameOwningGroup
        where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
        where T3 : unmanaged, IComponent
    {
        private readonly int _typeId1;
        private readonly int _typeId2;
        private readonly int _typeId3;
        private int _entityCapacity;
        private int* _entityToDenseIndex;
        private FullOwningGroup<T1, T2, T3> _group;

        internal OwningGroup(Allocator* allocator, int entityCapacity, int typeId1, int typeId2, int typeId3)
        {
            _typeId1 = typeId1;
            _typeId2 = typeId2;
            _typeId3 = typeId3;
            Reinitialize(allocator, entityCapacity);
        }

        public int Count => _group.Count;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool NextBlock(int* blockIndex, out EntityRef* entities, out T1* data1, out T2* data2, out T3* data3, out int count)
        {
            return _group.NextBlock(blockIndex, out entities, out data1, out data2, out data3, out count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Next(int* globalIndex, out EntityRef entity, out T1* c1, out T2* c2, out T3* c3)
        {
            return _group.Next(globalIndex, out entity, out c1, out c2, out c3);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool DependsOn(int typeId)
        {
            return typeId == _typeId1 || typeId == _typeId2 || typeId == _typeId3;
        }

        public void Reinitialize(Allocator* allocator, int entityCapacity)
        {
            _entityCapacity = entityCapacity;
            _entityToDenseIndex = (int*)allocator->Alloc(sizeof(int) * entityCapacity);
            _group = default;
            _group.Initialize(allocator);
            ResetDenseIndexMap();
        }

        public void SyncEntity(Frame frame, EntityRef entity, int changedTypeId)
        {
            _ = changedTypeId;

            int denseIndex = GetDenseIndex(entity.Index);
            if (!ShouldInclude(frame, entity))
            {
                if (denseIndex >= 0)
                {
                    RemoveDenseIndex(entity.Index, denseIndex);
                }

                return;
            }

            T1* component1 = frame.GetPointer<T1>(entity);
            T2* component2 = frame.GetPointer<T2>(entity);
            T3* component3 = frame.GetPointer<T3>(entity);
            if (component1 == null || component2 == null || component3 == null)
            {
                if (denseIndex >= 0)
                {
                    RemoveDenseIndex(entity.Index, denseIndex);
                }

                return;
            }

            if (denseIndex >= 0)
            {
                if (_group.GetAtLinearIndex(denseIndex, out EntityRef _, out T1* group1, out T2* group2, out T3* group3))
                {
                    *group1 = *component1;
                    *group2 = *component2;
                    *group3 = *component3;
                }

                return;
            }

            int newIndex = _group.Count;
            _group.Add(entity, *component1, *component2, *component3);
            _entityToDenseIndex[entity.Index] = newIndex;
        }

        public void Rebuild(Frame frame)
        {
            ResetDenseIndexMap();
            while (_group.Count > 0)
            {
                _group.RemoveAtLinearIndex(_group.Count - 1, out _);
            }

            var query = frame.Query<T1, T2, T3>();
            var enumerator = query.GetEnumerator();
            while (enumerator.MoveNext())
            {
                int denseIndex = _group.Count;
                _group.Add(
                    enumerator.Entity,
                    *enumerator.Component1Ptr,
                    *enumerator.Component2Ptr,
                    *enumerator.Component3Ptr);
                _entityToDenseIndex[enumerator.Entity.Index] = denseIndex;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ShouldInclude(Frame frame, EntityRef entity)
        {
            if (!frame.IsValid(entity))
            {
                return false;
            }

            return frame.HasComponentBit(entity.Index, _typeId1)
                && frame.HasComponentBit(entity.Index, _typeId2)
                && frame.HasComponentBit(entity.Index, _typeId3);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetDenseIndex(int entityIndex)
        {
            return (uint)entityIndex < (uint)_entityCapacity ? _entityToDenseIndex[entityIndex] : -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RemoveDenseIndex(int entityIndex, int denseIndex)
        {
            if (!_group.RemoveAtLinearIndex(denseIndex, out EntityRef movedEntity))
            {
                return;
            }

            _entityToDenseIndex[entityIndex] = -1;
            if (movedEntity != EntityRef.None)
            {
                _entityToDenseIndex[movedEntity.Index] = denseIndex;
            }
        }

        private void ResetDenseIndexMap()
        {
            for (int i = 0; i < _entityCapacity; i++)
            {
                _entityToDenseIndex[i] = -1;
            }
        }
    }
}
