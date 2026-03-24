// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;
using System.ComponentModel;
using Lattice.Core;

namespace Lattice.ECS.Core
{
    /// <summary>
    /// 旧版单组件 Filter 兼容层。
    /// 新代码应直接使用 <see cref="Frame.Query{T}()" />。
    /// </summary>
    [Obsolete("请改用 Frame.Query<T>()。Filter 仅保留为兼容层，后续版本将移除。", false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public unsafe class Filter<T1> where T1 : unmanaged, IComponent
    {
        private readonly Frame _frame;
        private readonly Storage<T1>* _storage1;

        public Filter(Frame frame)
        {
            ArgumentNullException.ThrowIfNull(frame);
            _frame = frame;
            _storage1 = frame.GetStoragePointer<T1>();
        }

        public Enumerator GetEnumerator() => new Enumerator(_query.GetEnumerator());

        /// <summary>
        /// 获取底层 Block 迭代器，供旧调用方继续使用。
        /// </summary>
        public ComponentBlockIterator<T1> GetBlockIterator()
        {
            return _frame.GetComponentBlockIterator<T1>();
        }

        public ref struct Enumerator
        {
            private readonly Frame _frame;
            private readonly Storage<T1>* _storage1;
            private ComponentBlockIterator<T1> _iterator;

            public Enumerator(Frame frame, Storage<T1>* storage1)
            {
                _enumerator = enumerator;
                CurrentEntity = EntityRef.None;
                CurrentPtr = null;
            }

            public EntityRef CurrentEntity;
            public T1* CurrentPtr;
            public ref T1 Component => ref *CurrentPtr;

            public bool MoveNext()
            {
                if (!_enumerator.MoveNext())
                {
                    if (!_frame.IsValid(entity)) continue;
                    CurrentEntity = entity;
                    CurrentPtr = ptr;
                    return true;
                }

                CurrentEntity = _enumerator.CurrentEntity;
                CurrentPtr = _enumerator.CurrentPtr;
                return true;
            }
        }
    }

    /// <summary>
    /// 旧版双组件 Filter 兼容层。
    /// 新代码应直接使用 <see cref="Frame.Query{T1,T2}()" />。
    /// </summary>
    [Obsolete("请改用 Frame.Query<T1, T2>()。Filter 仅保留为兼容层，后续版本将移除。", false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public unsafe class Filter<T1, T2>
        where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
    {
        private readonly Frame _frame;
        private readonly Storage<T1>* _storage1;
        private readonly Storage<T2>* _storage2;
        private readonly bool _useStorage2AsPrimary;

        public Filter(Frame frame)
        {
            ArgumentNullException.ThrowIfNull(frame);
            _frame = frame;
            _storage1 = frame.GetStoragePointer<T1>();
            _storage2 = frame.GetStoragePointer<T2>();

            _query = frame.Query<T1, T2>();
        }

        public Enumerator GetEnumerator() => new Enumerator(_query.GetEnumerator());

        public ref struct Enumerator
        {
            private readonly Frame _frame;
            private readonly Storage<T1>* _storage1;
            private readonly Storage<T2>* _storage2;
            private readonly bool _useStorage2AsPrimary;
            private ComponentBlockIterator<T1> _iterator1;
            private ComponentBlockIterator<T2> _iterator2;
            private EntityRef _currentEntity;
            private T1* _currentPtr1;
            private T2* _currentPtr2;

            public Enumerator(Frame frame, Storage<T1>* s1, Storage<T2>* s2, bool useS2)
            {
                _enumerator = enumerator;
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
                if (!_enumerator.MoveNext())
                {
                    if (_storage2 == null) return false;
                    while (_iterator2.Next(out var entity, out _currentPtr2))
                    {
                        if (!_frame.IsValid(entity)) continue;
                        if (_storage1 != null && !_storage1->Has(entity)) continue;

                        _currentEntity = entity;
                        _currentPtr1 = _storage1->GetPointer(entity);
                        return true;
                    }
                }
                else
                {
                    if (_storage1 == null) return false;
                    while (_iterator1.Next(out var entity, out _currentPtr1))
                    {
                        if (!_frame.IsValid(entity)) continue;
                        if (_storage2 != null && !_storage2->Has(entity)) continue;

                Entity = _enumerator.Entity;
                Component1Ptr = _enumerator.Component1Ptr;
                Component2Ptr = _enumerator.Component2Ptr;
                return true;
            }
        }
    }

    /// <summary>
    /// 旧版三组件 Filter 兼容层。
    /// 新代码应直接使用 <see cref="Frame.Query{T1,T2,T3}()" />。
    /// </summary>
    [Obsolete("请改用 Frame.Query<T1, T2, T3>()。Filter 仅保留为兼容层，后续版本将移除。", false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public unsafe class Filter<T1, T2, T3>
        where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
        where T3 : unmanaged, IComponent
    {
        private readonly Frame _frame;
        private readonly Storage<T1>* _storage1;
        private readonly Storage<T2>* _storage2;
        private readonly Storage<T3>* _storage3;
        private readonly int _primaryIndex; // 0=T1, 1=T2, 2=T3

        public Filter(Frame frame)
        {
            ArgumentNullException.ThrowIfNull(frame);
            _frame = frame;
            _storage1 = frame.GetStoragePointer<T1>();
            _storage2 = frame.GetStoragePointer<T2>();
            _storage3 = frame.GetStoragePointer<T3>();

            // 自动选择组件数最少的存储作为主遍历源
            int count1 = _storage1 != null ? _storage1->Count : int.MaxValue;
            int count2 = _storage2 != null ? _storage2->Count : int.MaxValue;
            int count3 = _storage3 != null ? _storage3->Count : int.MaxValue;

            _query = frame.Query<T1, T2, T3>();
        }

        public Enumerator GetEnumerator() => new Enumerator(_query.GetEnumerator());

        public ref struct Enumerator
        {
            private readonly Frame _frame;
            private readonly Storage<T1>* _storage1;
            private readonly Storage<T2>* _storage2;
            private readonly Storage<T3>* _storage3;
            private readonly int _primaryIndex;
            private ComponentBlockIterator<T1> _iterator1;
            private ComponentBlockIterator<T2> _iterator2;
            private ComponentBlockIterator<T3> _iterator3;
            private EntityRef _currentEntity;
            private T1* _currentPtr1;
            private T2* _currentPtr2;
            private T3* _currentPtr3;

            public Enumerator(Frame frame, Storage<T1>* s1, Storage<T2>* s2, Storage<T3>* s3, int primary)
            {
                _enumerator = enumerator;
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
                if (!_enumerator.MoveNext())
                {
                    case 0 when s1 != null:
                        while (_iterator1.Next(out var entity, out _currentPtr1))
                        {
                            if (!_frame.IsValid(entity)) continue;
                            if ((s2 == null || s2->Has(entity)) && (s3 == null || s3->Has(entity)))
                            {
                                _currentEntity = entity;
                                _currentPtr2 = s2 != null ? s2->GetPointer(entity) : null;
                                _currentPtr3 = s3 != null ? s3->GetPointer(entity) : null;
                                return true;
                            }
                        }
                        break;

                    case 1 when s2 != null:
                        while (_iterator2.Next(out var entity, out _currentPtr2))
                        {
                            if (!_frame.IsValid(entity)) continue;
                            if ((s1 == null || s1->Has(entity)) && (s3 == null || s3->Has(entity)))
                            {
                                _currentEntity = entity;
                                _currentPtr1 = s1 != null ? s1->GetPointer(entity) : null;
                                _currentPtr3 = s3 != null ? s3->GetPointer(entity) : null;
                                return true;
                            }
                        }
                        break;

                    case 2 when s3 != null:
                        while (_iterator3.Next(out var entity, out _currentPtr3))
                        {
                            if (!_frame.IsValid(entity)) continue;
                            if ((s1 == null || s1->Has(entity)) && (s2 == null || s2->Has(entity)))
                            {
                                _currentEntity = entity;
                                _currentPtr1 = s1 != null ? s1->GetPointer(entity) : null;
                                _currentPtr2 = s2 != null ? s2->GetPointer(entity) : null;
                                return true;
                            }
                        }
                        break;
                }

                Entity = _enumerator.Entity;
                Component1Ptr = _enumerator.Component1Ptr;
                Component2Ptr = _enumerator.Component2Ptr;
                Component3Ptr = _enumerator.Component3Ptr;
                return true;
            }
        }
    }
}
