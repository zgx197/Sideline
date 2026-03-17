// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;
using System.Runtime.CompilerServices;
using Lattice.Core;

namespace Lattice.ECS.Core
{
    /// <summary>
    /// 组件过滤器 - FrameSync 风格的高效查询，支持 Block 批量迭代
    /// 
    /// 性能特性：
    /// 1. 自动选择组件数最少的类型作为主遍历源
    /// 2. 支持 Block 级别的批量迭代，缓存友好
    /// 3. 零分配遍历（ref struct）
    /// </summary>
    public unsafe class Filter<T1> where T1 : unmanaged
    {
        private readonly Frame* _frame;
        private readonly Storage<T1>* _storage1;

        public Filter(Frame* frame)
        {
            _frame = frame;
            _storage1 = frame->GetStoragePointer<T1>();
        }

        public Enumerator GetEnumerator() => new Enumerator(_frame, _storage1);

        /// <summary>
        /// 获取 Block 迭代器（批量遍历）
        /// </summary>
        public ComponentBlockIterator<T1> GetBlockIterator()
        {
            if (_storage1 == null) return default;
            return new ComponentBlockIterator<T1>(_storage1);
        }

        public ref struct Enumerator
        {
            private readonly Frame* _frame;
            private readonly Storage<T1>* _storage1;
            private ComponentBlockIterator<T1> _iterator;

            public Enumerator(Frame* frame, Storage<T1>* storage1)
            {
                _frame = frame;
                _storage1 = storage1;
                _iterator = storage1 != null ? new ComponentBlockIterator<T1>(storage1) : default;
            }

            public bool MoveNext()
            {
                if (_storage1 == null) return false;
                while (_iterator.Next(out var entity, out var ptr))
                {
                    if (!_frame->IsValid(entity)) continue;
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
    /// 双组件过滤器 - 支持 Block 批量迭代
    /// </summary>
    public unsafe class Filter<T1, T2>
        where T1 : unmanaged
        where T2 : unmanaged
    {
        private readonly Frame* _frame;
        private readonly Storage<T1>* _storage1;
        private readonly Storage<T2>* _storage2;
        private readonly bool _useStorage2AsPrimary;

        public Filter(Frame* frame)
        {
            _frame = frame;
            _storage1 = frame->GetStoragePointer<T1>();
            _storage2 = frame->GetStoragePointer<T2>();

            // 自动选择组件数最少的存储作为主遍历源
            int count1 = _storage1 != null ? _storage1->Count : int.MaxValue;
            int count2 = _storage2 != null ? _storage2->Count : int.MaxValue;
            _useStorage2AsPrimary = count2 < count1;
        }

        public Enumerator GetEnumerator() => new Enumerator(_frame, _storage1, _storage2, _useStorage2AsPrimary);

        public ref struct Enumerator
        {
            private readonly Frame* _frame;
            private readonly Storage<T1>* _storage1;
            private readonly Storage<T2>* _storage2;
            private readonly bool _useStorage2AsPrimary;
            private ComponentBlockIterator<T1> _iterator1;
            private ComponentBlockIterator<T2> _iterator2;
            private EntityRef _currentEntity;
            private T1* _currentPtr1;
            private T2* _currentPtr2;

            public Enumerator(Frame* frame, Storage<T1>* s1, Storage<T2>* s2, bool useS2)
            {
                _frame = frame;
                _storage1 = s1;
                _storage2 = s2;
                _useStorage2AsPrimary = useS2;
                _currentEntity = EntityRef.None;
                _currentPtr1 = null;
                _currentPtr2 = null;

                if (useS2 && s2 != null)
                    _iterator2 = new ComponentBlockIterator<T2>(s2);
                else if (s1 != null)
                    _iterator1 = new ComponentBlockIterator<T1>(s1);
            }

            public bool MoveNext()
            {
                if (_useStorage2AsPrimary)
                {
                    if (_storage2 == null) return false;
                    while (_iterator2.Next(out var entity, out _currentPtr2))
                    {
                        if (!_frame->IsValid(entity)) continue;
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
                        if (!_frame->IsValid(entity)) continue;
                        if (_storage2 != null && !_storage2->Has(entity)) continue;

                        _currentEntity = entity;
                        _currentPtr2 = _storage2->GetPointer(entity);
                        return true;
                    }
                }
                return false;
            }

            public EntityRef Entity => _currentEntity;
            public ref T1 Component1 => ref *_currentPtr1;
            public ref T2 Component2 => ref *_currentPtr2;
        }
    }

    /// <summary>
    /// 三组件过滤器
    /// </summary>
    public unsafe class Filter<T1, T2, T3>
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
    {
        private readonly Frame* _frame;
        private readonly Storage<T1>* _storage1;
        private readonly Storage<T2>* _storage2;
        private readonly Storage<T3>* _storage3;
        private readonly int _primaryIndex; // 0=T1, 1=T2, 2=T3

        public Filter(Frame* frame)
        {
            _frame = frame;
            _storage1 = frame->GetStoragePointer<T1>();
            _storage2 = frame->GetStoragePointer<T2>();
            _storage3 = frame->GetStoragePointer<T3>();

            // 自动选择组件数最少的存储作为主遍历源
            int count1 = _storage1 != null ? _storage1->Count : int.MaxValue;
            int count2 = _storage2 != null ? _storage2->Count : int.MaxValue;
            int count3 = _storage3 != null ? _storage3->Count : int.MaxValue;

            if (count1 <= count2 && count1 <= count3) _primaryIndex = 0;
            else if (count2 <= count1 && count2 <= count3) _primaryIndex = 1;
            else _primaryIndex = 2;
        }

        public Enumerator GetEnumerator() => new Enumerator(_frame, _storage1, _storage2, _storage3, _primaryIndex);

        public ref struct Enumerator
        {
            private readonly Frame* _frame;
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

            public Enumerator(Frame* frame, Storage<T1>* s1, Storage<T2>* s2, Storage<T3>* s3, int primary)
            {
                _frame = frame;
                _storage1 = s1;
                _storage2 = s2;
                _storage3 = s3;
                _primaryIndex = primary;
                _currentEntity = EntityRef.None;
                _currentPtr1 = null;
                _currentPtr2 = null;
                _currentPtr3 = null;

                switch (primary)
                {
                    case 0 when s1 != null: _iterator1 = new ComponentBlockIterator<T1>(s1); break;
                    case 1 when s2 != null: _iterator2 = new ComponentBlockIterator<T2>(s2); break;
                    case 2 when s3 != null: _iterator3 = new ComponentBlockIterator<T3>(s3); break;
                }
            }

            public bool MoveNext()
            {
                Storage<T1>* s1 = _storage1;
                Storage<T2>* s2 = _storage2;
                Storage<T3>* s3 = _storage3;

                switch (_primaryIndex)
                {
                    case 0 when s1 != null:
                        while (_iterator1.Next(out var entity, out _currentPtr1))
                        {
                            if (!_frame->IsValid(entity)) continue;
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
                            if (!_frame->IsValid(entity)) continue;
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
                            if (!_frame->IsValid(entity)) continue;
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

                return false;
            }

            public EntityRef Entity => _currentEntity;
            public ref T1 Component1 => ref *_currentPtr1;
            public ref T2 Component2 => ref *_currentPtr2;
            public ref T3 Component3 => ref *_currentPtr3;
        }
    }
}
