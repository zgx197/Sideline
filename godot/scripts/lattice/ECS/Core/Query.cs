// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;
using System.Runtime.CompilerServices;
using Lattice.Core;

namespace Lattice.ECS.Core
{
    /// <summary>
    /// 单组件查询。
    /// </summary>
    public readonly unsafe struct Query<T1> where T1 : unmanaged, IComponent
    {
        private readonly Storage<T1>* _storage1;

        internal Query(Frame frame)
        {
            ArgumentNullException.ThrowIfNull(frame);
            _storage1 = frame.GetStoragePointer<T1>();
        }

        public int Count => _storage1 != null ? _storage1->UsedCount : 0;

        public Enumerator GetEnumerator() => new Enumerator(_storage1);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ForEach(delegate* managed<EntityRef, T1*, void> action)
        {
            if (_storage1 == null || action == null)
            {
                return;
            }

            var iterator = new ActiveComponentIterator<T1>(_storage1);
            while (iterator.Next(out EntityRef entity, out T1* component))
            {
                action(entity, component);
            }
        }

        public unsafe ref struct Enumerator
        {
            private readonly Storage<T1>* _storage1;
            private ActiveComponentIterator<T1> _iterator;

            internal Enumerator(Storage<T1>* storage1)
            {
                _storage1 = storage1;
                _iterator = storage1 != null ? new ActiveComponentIterator<T1>(storage1) : default;
                CurrentEntity = EntityRef.None;
                CurrentPtr = null;
            }

            public EntityRef CurrentEntity { get; private set; }

            public T1* CurrentPtr { get; private set; }

            public ref T1 Component => ref *CurrentPtr;

            public bool MoveNext()
            {
                if (_storage1 == null)
                {
                    return false;
                }

                while (_iterator.Next(out EntityRef entity, out T1* ptr))
                {
                    CurrentEntity = entity;
                    CurrentPtr = ptr;
                    return true;
                }

                return false;
            }
        }
    }

    /// <summary>
    /// 双组件查询。
    /// </summary>
    public readonly unsafe struct Query<T1, T2>
        where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
    {
        private readonly Frame _frame;
        private readonly Storage<T1>* _storage1;
        private readonly Storage<T2>* _storage2;
        private readonly bool _useStorage2AsPrimary;
        private readonly int _typeId1;
        private readonly int _typeId2;

        internal Query(Frame frame)
        {
            _frame = frame ?? throw new ArgumentNullException(nameof(frame));
            _storage1 = frame.GetStoragePointer<T1>();
            _storage2 = frame.GetStoragePointer<T2>();
            _typeId1 = ComponentTypeId<T1>.Id;
            _typeId2 = ComponentTypeId<T2>.Id;

            int count1 = _storage1 != null ? _storage1->UsedCount : int.MaxValue;
            int count2 = _storage2 != null ? _storage2->UsedCount : int.MaxValue;
            _useStorage2AsPrimary = count2 < count1;
        }

        public int Count
        {
            get
            {
                if (_storage1 == null || _storage2 == null)
                {
                    return 0;
                }

                return _useStorage2AsPrimary ? _storage2->UsedCount : _storage1->UsedCount;
            }
        }

        public Enumerator GetEnumerator() => new Enumerator(_frame, _storage1, _storage2, _useStorage2AsPrimary, _typeId1, _typeId2);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ForEach(delegate* managed<EntityRef, T1*, T2*, void> action)
        {
            if (_storage1 == null || _storage2 == null || action == null)
            {
                return;
            }

            var enumerator = GetEnumerator();
            while (enumerator.MoveNext())
            {
                action(enumerator.Entity, enumerator.Component1Ptr, enumerator.Component2Ptr);
            }
        }

        public unsafe ref struct Enumerator
        {
            private readonly Frame _frame;
            private readonly Storage<T1>* _storage1;
            private readonly Storage<T2>* _storage2;
            private readonly bool _useStorage2AsPrimary;
            private readonly int _typeId1;
            private readonly int _typeId2;
            private ActiveComponentIterator<T1> _iterator1;
            private ActiveComponentIterator<T2> _iterator2;

            internal Enumerator(Frame frame, Storage<T1>* storage1, Storage<T2>* storage2, bool useStorage2AsPrimary, int typeId1, int typeId2)
            {
                _frame = frame;
                _storage1 = storage1;
                _storage2 = storage2;
                _useStorage2AsPrimary = useStorage2AsPrimary;
                _typeId1 = typeId1;
                _typeId2 = typeId2;
                _iterator1 = storage1 != null ? new ActiveComponentIterator<T1>(storage1) : default;
                _iterator2 = storage2 != null ? new ActiveComponentIterator<T2>(storage2) : default;
                Entity = EntityRef.None;
                Component1Ptr = null;
                Component2Ptr = null;
            }

            public EntityRef Entity { get; private set; }

            public T1* Component1Ptr { get; private set; }

            public T2* Component2Ptr { get; private set; }

            public ref T1 Component1 => ref *Component1Ptr;

            public ref T2 Component2 => ref *Component2Ptr;

            public bool MoveNext()
            {
                if (_storage1 == null || _storage2 == null)
                {
                    return false;
                }

                if (_useStorage2AsPrimary)
                {
                    while (_iterator2.Next(out EntityRef entity, out T2* ptr2))
                    {
                        if (!_frame.HasComponentBit(entity.Index, _typeId1))
                        {
                            continue;
                        }

                        T1* ptr1 = _storage1->GetPointerAssumePresent(entity);
                        if (ptr1 == null)
                        {
                            continue;
                        }

                        Entity = entity;
                        Component1Ptr = ptr1;
                        Component2Ptr = ptr2;
                        return true;
                    }

                    return false;
                }

                while (_iterator1.Next(out EntityRef entity, out T1* ptr1))
                {
                    if (!_frame.HasComponentBit(entity.Index, _typeId2))
                    {
                        continue;
                    }

                    T2* ptr2 = _storage2->GetPointerAssumePresent(entity);
                    if (ptr2 == null)
                    {
                        continue;
                    }

                    Entity = entity;
                    Component1Ptr = ptr1;
                    Component2Ptr = ptr2;
                    return true;
                }

                return false;
            }
        }
    }

    /// <summary>
    /// 三组件查询。
    /// </summary>
    public readonly unsafe struct Query<T1, T2, T3>
        where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
        where T3 : unmanaged, IComponent
    {
        private readonly Frame _frame;
        private readonly Storage<T1>* _storage1;
        private readonly Storage<T2>* _storage2;
        private readonly Storage<T3>* _storage3;
        private readonly int _primaryIndex;
        private readonly int _typeId1;
        private readonly int _typeId2;
        private readonly int _typeId3;

        internal Query(Frame frame)
        {
            _frame = frame ?? throw new ArgumentNullException(nameof(frame));
            _storage1 = frame.GetStoragePointer<T1>();
            _storage2 = frame.GetStoragePointer<T2>();
            _storage3 = frame.GetStoragePointer<T3>();
            _typeId1 = ComponentTypeId<T1>.Id;
            _typeId2 = ComponentTypeId<T2>.Id;
            _typeId3 = ComponentTypeId<T3>.Id;

            int count1 = _storage1 != null ? _storage1->UsedCount : int.MaxValue;
            int count2 = _storage2 != null ? _storage2->UsedCount : int.MaxValue;
            int count3 = _storage3 != null ? _storage3->UsedCount : int.MaxValue;

            if (count1 <= count2 && count1 <= count3)
            {
                _primaryIndex = 0;
            }
            else if (count2 <= count1 && count2 <= count3)
            {
                _primaryIndex = 1;
            }
            else
            {
                _primaryIndex = 2;
            }
        }

        public Enumerator GetEnumerator() => new Enumerator(_frame, _storage1, _storage2, _storage3, _primaryIndex, _typeId1, _typeId2, _typeId3);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ForEach(delegate* managed<EntityRef, T1*, T2*, T3*, void> action)
        {
            if (_storage1 == null || _storage2 == null || _storage3 == null || action == null)
            {
                return;
            }

            var enumerator = GetEnumerator();
            while (enumerator.MoveNext())
            {
                action(enumerator.Entity, enumerator.Component1Ptr, enumerator.Component2Ptr, enumerator.Component3Ptr);
            }
        }

        public unsafe ref struct Enumerator
        {
            private readonly Frame _frame;
            private readonly Storage<T1>* _storage1;
            private readonly Storage<T2>* _storage2;
            private readonly Storage<T3>* _storage3;
            private readonly int _primaryIndex;
            private readonly int _typeId1;
            private readonly int _typeId2;
            private readonly int _typeId3;
            private ActiveComponentIterator<T1> _iterator1;
            private ActiveComponentIterator<T2> _iterator2;
            private ActiveComponentIterator<T3> _iterator3;

            internal Enumerator(
                Frame frame,
                Storage<T1>* storage1,
                Storage<T2>* storage2,
                Storage<T3>* storage3,
                int primaryIndex,
                int typeId1,
                int typeId2,
                int typeId3)
            {
                _frame = frame;
                _storage1 = storage1;
                _storage2 = storage2;
                _storage3 = storage3;
                _primaryIndex = primaryIndex;
                _typeId1 = typeId1;
                _typeId2 = typeId2;
                _typeId3 = typeId3;
                _iterator1 = storage1 != null ? new ActiveComponentIterator<T1>(storage1) : default;
                _iterator2 = storage2 != null ? new ActiveComponentIterator<T2>(storage2) : default;
                _iterator3 = storage3 != null ? new ActiveComponentIterator<T3>(storage3) : default;
                Entity = EntityRef.None;
                Component1Ptr = null;
                Component2Ptr = null;
                Component3Ptr = null;
            }

            public EntityRef Entity { get; private set; }

            public T1* Component1Ptr { get; private set; }

            public T2* Component2Ptr { get; private set; }

            public T3* Component3Ptr { get; private set; }

            public ref T1 Component1 => ref *Component1Ptr;

            public ref T2 Component2 => ref *Component2Ptr;

            public ref T3 Component3 => ref *Component3Ptr;

            public bool MoveNext()
            {
                if (_storage1 == null || _storage2 == null || _storage3 == null)
                {
                    return false;
                }

                switch (_primaryIndex)
                {
                    case 0:
                        return MovePrimary1();
                    case 1:
                        return MovePrimary2();
                    default:
                        return MovePrimary3();
                }
            }

            private bool MovePrimary1()
            {
                while (_iterator1.Next(out EntityRef entity, out T1* ptr1))
                {
                    if (!_frame.HasComponentBit(entity.Index, _typeId2) ||
                        !_frame.HasComponentBit(entity.Index, _typeId3))
                    {
                        continue;
                    }

                    T2* ptr2 = _storage2->GetPointerAssumePresent(entity);
                    T3* ptr3 = _storage3->GetPointerAssumePresent(entity);
                    if (ptr2 == null || ptr3 == null)
                    {
                        continue;
                    }

                    Entity = entity;
                    Component1Ptr = ptr1;
                    Component2Ptr = ptr2;
                    Component3Ptr = ptr3;
                    return true;
                }

                return false;
            }

            private bool MovePrimary2()
            {
                while (_iterator2.Next(out EntityRef entity, out T2* ptr2))
                {
                    if (!_frame.HasComponentBit(entity.Index, _typeId1) ||
                        !_frame.HasComponentBit(entity.Index, _typeId3))
                    {
                        continue;
                    }

                    T1* ptr1 = _storage1->GetPointerAssumePresent(entity);
                    T3* ptr3 = _storage3->GetPointerAssumePresent(entity);
                    if (ptr1 == null || ptr3 == null)
                    {
                        continue;
                    }

                    Entity = entity;
                    Component1Ptr = ptr1;
                    Component2Ptr = ptr2;
                    Component3Ptr = ptr3;
                    return true;
                }

                return false;
            }

            private bool MovePrimary3()
            {
                while (_iterator3.Next(out EntityRef entity, out T3* ptr3))
                {
                    if (!_frame.HasComponentBit(entity.Index, _typeId1) ||
                        !_frame.HasComponentBit(entity.Index, _typeId2))
                    {
                        continue;
                    }

                    T1* ptr1 = _storage1->GetPointerAssumePresent(entity);
                    T2* ptr2 = _storage2->GetPointerAssumePresent(entity);
                    if (ptr1 == null || ptr2 == null)
                    {
                        continue;
                    }

                    Entity = entity;
                    Component1Ptr = ptr1;
                    Component2Ptr = ptr2;
                    Component3Ptr = ptr3;
                    return true;
                }

                return false;
            }
        }
    }
}
