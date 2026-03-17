// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;
using System.Runtime.CompilerServices;
using Lattice.Core;

namespace Lattice.ECS.Core
{
    /// <summary>
    /// 组件过滤器 - FrameSync 风格的高效查询
    /// 
    /// 特性：
    /// 1. 自动选择组件数最少的类型作为主遍历源
    /// 2. 支持任意数量的组件组合
    /// 3. 零分配遍历
    /// </summary>
    public unsafe class Filter<T1> where T1 : unmanaged
    {
        private readonly Frame _frame;
        private readonly Storage<T1>* _storage1;

        public Filter(Frame frame)
        {
            _frame = frame;
            _storage1 = (Storage<T1>*)frame.GetStorage<T1>(ComponentTypeId<T1>.Id);
        }

        public Enumerator GetEnumerator() => new Enumerator(_frame, _storage1);

        public struct Enumerator
        {
            private readonly Frame _frame;
            private readonly Storage<T1>* _storage1;
            private int _index;

            public Enumerator(Frame frame, Storage<T1>* storage1)
            {
                _frame = frame;
                _storage1 = storage1;
                _index = -1;
            }

            public bool MoveNext()
            {
                if (_storage1 == null) return false;
                while (++_index < _storage1->Count)
                {
                    var entity = _storage1->DenseEntities[_index];
                    if (_frame.IsValid(entity)) return true;
                }
                return false;
            }

            public EntityRef Current => _storage1->DenseEntities[_index];
            public ref T1 Component => ref _storage1->DenseComponents[_index];
        }
    }

    /// <summary>
    /// 双组件过滤器
    /// </summary>
    public unsafe class Filter<T1, T2>
        where T1 : unmanaged
        where T2 : unmanaged
    {
        private readonly Frame _frame;
        private readonly Storage<T1>* _storage1;
        private readonly Storage<T2>* _storage2;
        private readonly bool _useStorage2AsPrimary;

        public Filter(Frame frame)
        {
            _frame = frame;
            _storage1 = (Storage<T1>*)frame.GetStorage<T1>(ComponentTypeId<T1>.Id);
            _storage2 = (Storage<T2>*)frame.GetStorage<T2>(ComponentTypeId<T2>.Id);

            // 自动选择组件数最少的存储作为主遍历源
            int count1 = _storage1 != null ? _storage1->Count : int.MaxValue;
            int count2 = _storage2 != null ? _storage2->Count : int.MaxValue;
            _useStorage2AsPrimary = count2 < count1;
        }

        public Enumerator GetEnumerator() => new Enumerator(_frame, _storage1, _storage2, _useStorage2AsPrimary);

        public struct Enumerator
        {
            private readonly Frame _frame;
            private readonly Storage<T1>* _storage1;
            private readonly Storage<T2>* _storage2;
            private readonly bool _useStorage2AsPrimary;
            private int _index;
            private EntityRef _currentEntity;

            public Enumerator(Frame frame, Storage<T1>* s1, Storage<T2>* s2, bool useS2)
            {
                _frame = frame;
                _storage1 = s1;
                _storage2 = s2;
                _useStorage2AsPrimary = useS2;
                _index = -1;
                _currentEntity = EntityRef.None;
            }

            public bool MoveNext()
            {
                if (_useStorage2AsPrimary)
                {
                    if (_storage2 == null) return false;
                    while (++_index < _storage2->Count)
                    {
                        var entity = _storage2->DenseEntities[_index];
                        if (!_frame.IsValid(entity)) continue;
                        if (_storage1 != null && _storage1->Has(entity))
                        {
                            _currentEntity = entity;
                            return true;
                        }
                    }
                }
                else
                {
                    if (_storage1 == null) return false;
                    while (++_index < _storage1->Count)
                    {
                        var entity = _storage1->DenseEntities[_index];
                        if (!_frame.IsValid(entity)) continue;
                        if (_storage2 != null && _storage2->Has(entity))
                        {
                            _currentEntity = entity;
                            return true;
                        }
                    }
                }
                return false;
            }

            public EntityRef Entity => _currentEntity;
            public ref T1 Component1 => ref _storage1->Get(_currentEntity);
            public ref T2 Component2 => ref _storage2->Get(_currentEntity);
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
        private readonly Frame _frame;
        private readonly Storage<T1>* _storage1;
        private readonly Storage<T2>* _storage2;
        private readonly Storage<T3>* _storage3;
        private readonly int _primaryIndex; // 0=T1, 1=T2, 2=T3

        public Filter(Frame frame)
        {
            _frame = frame;
            _storage1 = (Storage<T1>*)frame.GetStorage<T1>(ComponentTypeId<T1>.Id);
            _storage2 = (Storage<T2>*)frame.GetStorage<T2>(ComponentTypeId<T2>.Id);
            _storage3 = (Storage<T3>*)frame.GetStorage<T3>(ComponentTypeId<T3>.Id);

            // 自动选择组件数最少的存储作为主遍历源
            int count1 = _storage1 != null ? _storage1->Count : int.MaxValue;
            int count2 = _storage2 != null ? _storage2->Count : int.MaxValue;
            int count3 = _storage3 != null ? _storage3->Count : int.MaxValue;

            if (count1 <= count2 && count1 <= count3) _primaryIndex = 0;
            else if (count2 <= count1 && count2 <= count3) _primaryIndex = 1;
            else _primaryIndex = 2;
        }

        public Enumerator GetEnumerator() => new Enumerator(_frame, _storage1, _storage2, _storage3, _primaryIndex);

        public struct Enumerator
        {
            private readonly Frame _frame;
            private readonly Storage<T1>* _storage1;
            private readonly Storage<T2>* _storage2;
            private readonly Storage<T3>* _storage3;
            private readonly int _primaryIndex;
            private int _index;
            private EntityRef _currentEntity;

            public Enumerator(Frame frame, Storage<T1>* s1, Storage<T2>* s2, Storage<T3>* s3, int primary)
            {
                _frame = frame;
                _storage1 = s1;
                _storage2 = s2;
                _storage3 = s3;
                _primaryIndex = primary;
                _index = -1;
                _currentEntity = EntityRef.None;
            }

            public bool MoveNext()
            {
                // 根据主索引选择存储
                if (_primaryIndex == 0)
                {
                    if (_storage1 == null) return false;
                    while (++_index < _storage1->Count)
                    {
                        var entity = _storage1->DenseEntities[_index];
                        if (!_frame.IsValid(entity)) continue;
                        if ((_storage2 == null || _storage2->Has(entity)) &&
                            (_storage3 == null || _storage3->Has(entity)))
                        {
                            _currentEntity = entity;
                            return true;
                        }
                    }
                }
                else if (_primaryIndex == 1)
                {
                    if (_storage2 == null) return false;
                    while (++_index < _storage2->Count)
                    {
                        var entity = _storage2->DenseEntities[_index];
                        if (!_frame.IsValid(entity)) continue;
                        if ((_storage1 == null || _storage1->Has(entity)) &&
                            (_storage3 == null || _storage3->Has(entity)))
                        {
                            _currentEntity = entity;
                            return true;
                        }
                    }
                }
                else
                {
                    if (_storage3 == null) return false;
                    while (++_index < _storage3->Count)
                    {
                        var entity = _storage3->DenseEntities[_index];
                        if (!_frame.IsValid(entity)) continue;
                        if ((_storage1 == null || _storage1->Has(entity)) &&
                            (_storage2 == null || _storage2->Has(entity)))
                        {
                            _currentEntity = entity;
                            return true;
                        }
                    }
                }
                return false;
            }

            private bool HasSecondaryComponents(EntityRef entity)
            {
                if (_primaryIndex != 0 && (_storage1 == null || !_storage1->Has(entity))) return false;
                if (_primaryIndex != 1 && (_storage2 == null || !_storage2->Has(entity))) return false;
                if (_primaryIndex != 2 && (_storage3 == null || !_storage3->Has(entity))) return false;
                return true;
            }

            public EntityRef Entity => _currentEntity;
            public ref T1 Component1 => ref _storage1->Get(_currentEntity);
            public ref T2 Component2 => ref _storage2->Get(_currentEntity);
            public ref T3 Component3 => ref _storage3->Get(_currentEntity);
        }
    }
}
