// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;
using System.Runtime.CompilerServices;

namespace Lattice.ECS.Core
{
    /// <summary>
    /// 组件查询结果项（单组件）
    /// </summary>
    public unsafe readonly struct QueryItem<T> where T : unmanaged
    {
        public readonly Entity Entity;
        public readonly T* Component;

        public QueryItem(Entity entity, T* component)
        {
            Entity = entity;
            Component = component;
        }

        /// <summary>解引用组件值（谨慎使用，会复制）</summary>
        public T Value => *Component;

        /// <summary>通过指针修改组件值</summary>
        public ref T ValueRef => ref *Component;
    }

    /// <summary>
    /// 组件查询结果项（2 组件）
    /// </summary>
    public unsafe readonly struct QueryItem<T1, T2>
        where T1 : unmanaged
        where T2 : unmanaged
    {
        public readonly Entity Entity;
        public readonly T1* Component1;
        public readonly T2* Component2;

        public QueryItem(Entity entity, T1* c1, T2* c2)
        {
            Entity = entity;
            Component1 = c1;
            Component2 = c2;
        }
    }

    /// <summary>
    /// 组件查询结果项（3 组件）
    /// </summary>
    public unsafe readonly struct QueryItem<T1, T2, T3>
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
    {
        public readonly Entity Entity;
        public readonly T1* Component1;
        public readonly T2* Component2;
        public readonly T3* Component3;

        public QueryItem(Entity entity, T1* c1, T2* c2, T3* c3)
        {
            Entity = entity;
            Component1 = c1;
            Component2 = c2;
            Component3 = c3;
        }
    }

    /// <summary>
    /// 单组件查询，完全对齐 FrameSync 设计
    /// 使用 ComponentFilterState 进行路径优化
    /// </summary>
    public unsafe readonly ref struct Query<T> where T : unmanaged, IComponent
    {
        private readonly Frame _frame;
        private readonly ComponentStorage<T> _storage;
        private readonly ComponentSet _without;
        private readonly ComponentSet _any;

        public Query(Frame frame, ComponentSet without = default, ComponentSet any = default)
        {
            _frame = frame;
            _storage = frame.GetStorage<T>();
            _without = without;
            _any = any;
        }

        /// <summary>
        /// 排除有指定组件的实体
        /// </summary>
        public Query<T> Without<TWithout>() where TWithout : unmanaged, IComponent
        {
            var without = _without;
            without.Add<TWithout>();
            return new Query<T>(_frame, without, _any);
        }

        /// <summary>
        /// 只包含有任意指定组件的实体
        /// </summary>
        public Query<T> WithAny<TAny>() where TAny : unmanaged, IComponent
        {
            var any = _any;
            any.Add<TAny>();
            return new Query<T>(_frame, _without, any);
        }

        /// <summary>
        /// 获取枚举器
        /// </summary>
        public Enumerator GetEnumerator()
        {
            return new Enumerator(_frame, _storage, _without, _any);
        }

        /// <summary>
        /// ref struct 枚举器，对齐 FrameSync ComponentFilterState
        /// </summary>
        public unsafe ref struct Enumerator
        {
            private readonly Frame _frame;
            private readonly ComponentStorage<T> _storage;
            private readonly ComponentSet _with;
            private readonly ComponentSet _without;
            private readonly ComponentSet _any;

            // 路径优化标志
            private readonly bool _anySuccess;
            private readonly bool _withoutSuccess;
            private readonly bool _use256Path;
            private readonly bool _useFullComponentSet;

            // 块迭代状态
            private int _blockIndex;
            private int _blockCount;
            private byte* _blockData;
            private Entity* _blockEntities;
            private int _elementIndex;

            // 当前结果
            private QueryItem<T> _current;

            internal Enumerator(Frame frame, ComponentStorage<T> storage, ComponentSet without, ComponentSet any)
            {
                _frame = frame;
                _storage = storage;
                _with = ComponentSet.Create<T>();
                _without = without;
                _any = any;

                // 优化标志
                _anySuccess = any.IsEmpty;
                _withoutSuccess = without.IsEmpty;
                _use256Path = _with.Rank <= 4 && any.Rank <= 4 && without.Rank <= 4;
                _useFullComponentSet = true;  // 假设总是使用完整 512-bit

                // 块迭代初始化
                _blockIndex = -1;
                _blockCount = 0;
                _blockData = null;
                _blockEntities = null;
                _elementIndex = 0;
                _current = default;
            }

            public QueryItem<T> Current => _current;

            public bool MoveNext()
            {
                // 使用 256 路径优化
                if (_use256Path)
                {
                    return MoveNext256();
                }
                return MoveNext512();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool MoveNext256()
            {
                while (true)
                {
                    // 需要加载新块
                    if (_blockData == null)
                    {
                        _blockIndex++;
                        if (!_storage.TryGetBlock(_blockIndex, out _blockData, out _blockEntities, out _blockCount))
                        {
                            return false;  // 没有更多块
                        }
                        _elementIndex = 0;
                    }

                    // 遍历当前块
                    while (_elementIndex < _blockCount)
                    {
                        Entity entity = _blockEntities[_elementIndex];
                        _elementIndex++;

                        // 验证实体有效性
                        if ((uint)entity.Index >= (uint)_frame.Entities.Capacity)
                            continue;
                        if (!_frame.Entities.Exists(entity))
                            continue;

                        // 检查 Culling（被裁剪的实体跳过）
                        if (_frame.IsCulled(entity))
                            continue;

                        // 获取组件位图（256-bit 路径）
                        ComponentSet256 mask = _frame.GetComponentSet256(entity);
                        ulong* mset = (ulong*)&mask;

                        // 检查 With 条件（必需组件）
                        if ((mset[0] & _with.Set[0]) != _with.Set[0] ||
                            (mset[1] & _with.Set[1]) != _with.Set[1] ||
                            (mset[2] & _with.Set[2]) != _with.Set[2] ||
                            (mset[3] & _with.Set[3]) != _with.Set[3])
                            continue;

                        // 检查 Any 条件（任意组件）
                        if (!_anySuccess)
                        {
                            if ((mset[0] & _any.Set[0]) == 0 &&
                                (mset[1] & _any.Set[1]) == 0 &&
                                (mset[2] & _any.Set[2]) == 0 &&
                                (mset[3] & _any.Set[3]) == 0)
                                continue;
                        }

                        // 检查 Without 条件（排除组件）
                        if (!_withoutSuccess)
                        {
                            if ((mset[0] & _without.Set[0]) != 0 ||
                                (mset[1] & _without.Set[1]) != 0 ||
                                (mset[2] & _without.Set[2]) != 0 ||
                                (mset[3] & _without.Set[3]) != 0)
                                continue;
                        }

                        // 获取组件指针
                        T* component = (T*)(_blockData + (_elementIndex - 1) * sizeof(T));
                        _current = new QueryItem<T>(entity, component);
                        return true;
                    }

                    // 当前块遍历完毕，标记为需要新块
                    _blockData = null;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool MoveNext512()
            {
                while (true)
                {
                    if (_blockData == null)
                    {
                        _blockIndex++;
                        if (!_storage.TryGetBlock(_blockIndex, out _blockData, out _blockEntities, out _blockCount))
                        {
                            return false;
                        }
                        _elementIndex = 0;
                    }

                    while (_elementIndex < _blockCount)
                    {
                        Entity entity = _blockEntities[_elementIndex];
                        _elementIndex++;

                        if ((uint)entity.Index >= (uint)_frame.Entities.Capacity)
                            continue;
                        if (!_frame.Entities.Exists(entity))
                            continue;

                        // 检查 Culling（被裁剪的实体跳过）
                        if (_frame.IsCulled(entity))
                            continue;

                        // 获取完整 512-bit 组件位图
                        ComponentSet mask = _frame.GetComponentSet(entity);

                        // 检查 With 条件
                        if (!mask.IsSupersetOf(_with))
                            continue;

                        // 检查 Any 条件
                        if (!_anySuccess && !mask.Overlaps(_any))
                            continue;

                        // 检查 Without 条件
                        if (!_withoutSuccess && mask.Overlaps(_without))
                            continue;

                        T* component = (T*)(_blockData + (_elementIndex - 1) * sizeof(T));
                        _current = new QueryItem<T>(entity, component);
                        return true;
                    }

                    _blockData = null;
                }
            }
        }
    }

    /// <summary>
    /// 双组件查询
    /// </summary>
    public unsafe readonly ref struct Query<T1, T2>
        where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
    {
        private readonly Frame _frame;
        private readonly ComponentStorage<T1> _storage1;
        private readonly ComponentStorage<T2> _storage2;
        private readonly ComponentSet _without;
        private readonly ComponentSet _any;

        public Query(Frame frame, ComponentSet without = default, ComponentSet any = default)
        {
            _frame = frame;
            _storage1 = frame.GetStorage<T1>();
            _storage2 = frame.GetStorage<T2>();
            _without = without;
            _any = any;
        }

        public Query<T1, T2> Without<TWithout>() where TWithout : unmanaged, IComponent
        {
            var without = _without;
            without.Add<TWithout>();
            return new Query<T1, T2>(_frame, without, _any);
        }

        public Query<T1, T2> WithAny<TAny>() where TAny : unmanaged, IComponent
        {
            var any = _any;
            any.Add<TAny>();
            return new Query<T1, T2>(_frame, _without, any);
        }

        public Enumerator GetEnumerator()
        {
            return new Enumerator(_frame, _storage1, _storage2, _without, _any);
        }

        public unsafe ref struct Enumerator
        {
            private readonly Frame _frame;
            private readonly ComponentStorage<T1> _storage1;
            private readonly ComponentStorage<T2> _storage2;
            private readonly ComponentSet _with;
            private readonly ComponentSet _without;
            private readonly ComponentSet _any;

            private readonly bool _anySuccess;
            private readonly bool _withoutSuccess;
            private readonly bool _use256Path;

            private int _blockIndex;
            private int _blockCount;
            private byte* _blockData1;
            private Entity* _blockEntities;
            private int _elementIndex;

            private QueryItem<T1, T2> _current;

            internal Enumerator(Frame frame, ComponentStorage<T1> s1, ComponentStorage<T2> s2,
                ComponentSet without, ComponentSet any)
            {
                _frame = frame;
                _storage1 = s1;
                _storage2 = s2;
                _with = ComponentSet.Create<T1, T2>();
                _without = without;
                _any = any;

                _anySuccess = any.IsEmpty;
                _withoutSuccess = without.IsEmpty;
                _use256Path = _with.Rank <= 4 && any.Rank <= 4 && without.Rank <= 4;

                _blockIndex = -1;
                _blockCount = 0;
                _blockData1 = null;
                _blockEntities = null;
                _elementIndex = 0;
                _current = default;
            }

            public QueryItem<T1, T2> Current => _current;

            public bool MoveNext()
            {
                if (_use256Path)
                    return MoveNext256();
                return MoveNext512();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool MoveNext256()
            {
                while (true)
                {
                    if (_blockData1 == null)
                    {
                        _blockIndex++;
                        if (!_storage1.TryGetBlock(_blockIndex, out _blockData1, out _blockEntities, out _blockCount))
                            return false;
                        _elementIndex = 0;
                    }

                    while (_elementIndex < _blockCount)
                    {
                        Entity entity = _blockEntities[_elementIndex];
                        _elementIndex++;

                        if ((uint)entity.Index >= (uint)_frame.Entities.Capacity)
                            continue;
                        if (!_frame.Entities.Exists(entity))
                            continue;

                        // 检查第二个组件是否存在
                        T2* c2;
                        if (!_storage2.TryGetPointer(entity, out c2))
                            continue;

                        ComponentSet256 mask = _frame.GetComponentSet256(entity);
                        ulong* mset = (ulong*)&mask;

                        // With 检查
                        if ((mset[0] & _with.Set[0]) != _with.Set[0] ||
                            (mset[1] & _with.Set[1]) != _with.Set[1] ||
                            (mset[2] & _with.Set[2]) != _with.Set[2] ||
                            (mset[3] & _with.Set[3]) != _with.Set[3])
                            continue;

                        // Any 检查
                        if (!_anySuccess)
                        {
                            if ((mset[0] & _any.Set[0]) == 0 &&
                                (mset[1] & _any.Set[1]) == 0 &&
                                (mset[2] & _any.Set[2]) == 0 &&
                                (mset[3] & _any.Set[3]) == 0)
                                continue;
                        }

                        // Without 检查
                        if (!_withoutSuccess)
                        {
                            if ((mset[0] & _without.Set[0]) != 0 ||
                                (mset[1] & _without.Set[1]) != 0 ||
                                (mset[2] & _without.Set[2]) != 0 ||
                                (mset[3] & _without.Set[3]) != 0)
                                continue;
                        }

                        T1* c1 = (T1*)(_blockData1 + (_elementIndex - 1) * sizeof(T1));
                        _current = new QueryItem<T1, T2>(entity, c1, c2);
                        return true;
                    }

                    _blockData1 = null;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool MoveNext512()
            {
                while (true)
                {
                    if (_blockData1 == null)
                    {
                        _blockIndex++;
                        if (!_storage1.TryGetBlock(_blockIndex, out _blockData1, out _blockEntities, out _blockCount))
                            return false;
                        _elementIndex = 0;
                    }

                    while (_elementIndex < _blockCount)
                    {
                        Entity entity = _blockEntities[_elementIndex];
                        _elementIndex++;

                        if ((uint)entity.Index >= (uint)_frame.Entities.Capacity)
                            continue;
                        if (!_frame.Entities.Exists(entity))
                            continue;

                        T2* c2;
                        if (!_storage2.TryGetPointer(entity, out c2))
                            continue;

                        ComponentSet mask = _frame.GetComponentSet(entity);

                        if (!mask.IsSupersetOf(_with))
                            continue;
                        if (!_anySuccess && !mask.Overlaps(_any))
                            continue;
                        if (!_withoutSuccess && mask.Overlaps(_without))
                            continue;

                        T1* c1 = (T1*)(_blockData1 + (_elementIndex - 1) * sizeof(T1));
                        _current = new QueryItem<T1, T2>(entity, c1, c2);
                        return true;
                    }

                    _blockData1 = null;
                }
            }
        }
    }

    /// <summary>
    /// 三组件查询
    /// </summary>
    public unsafe readonly ref struct Query<T1, T2, T3>
        where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
        where T3 : unmanaged, IComponent
    {
        private readonly Frame _frame;
        private readonly ComponentStorage<T1> _storage1;
        private readonly ComponentStorage<T2> _storage2;
        private readonly ComponentStorage<T3> _storage3;
        private readonly ComponentSet _without;
        private readonly ComponentSet _any;

        public Query(Frame frame, ComponentSet without = default, ComponentSet any = default)
        {
            _frame = frame;
            _storage1 = frame.GetStorage<T1>();
            _storage2 = frame.GetStorage<T2>();
            _storage3 = frame.GetStorage<T3>();
            _without = without;
            _any = any;
        }

        public Query<T1, T2, T3> Without<TWithout>() where TWithout : unmanaged, IComponent
        {
            var without = _without;
            without.Add<TWithout>();
            return new Query<T1, T2, T3>(_frame, without, _any);
        }

        public Query<T1, T2, T3> WithAny<TAny>() where TAny : unmanaged, IComponent
        {
            var any = _any;
            any.Add<TAny>();
            return new Query<T1, T2, T3>(_frame, _without, any);
        }

        public Enumerator GetEnumerator()
        {
            return new Enumerator(_frame, _storage1, _storage2, _storage3, _without, _any);
        }

        public unsafe ref struct Enumerator
        {
            private readonly Frame _frame;
            private readonly ComponentStorage<T1> _storage1;
            private readonly ComponentStorage<T2> _storage2;
            private readonly ComponentStorage<T3> _storage3;
            private readonly ComponentSet _with;
            private readonly ComponentSet _without;
            private readonly ComponentSet _any;

            private readonly bool _anySuccess;
            private readonly bool _withoutSuccess;
            private readonly bool _use256Path;

            private int _blockIndex;
            private int _blockCount;
            private byte* _blockData1;
            private Entity* _blockEntities;
            private int _elementIndex;

            private QueryItem<T1, T2, T3> _current;

            internal Enumerator(Frame frame, ComponentStorage<T1> s1, ComponentStorage<T2> s2,
                ComponentStorage<T3> s3, ComponentSet without, ComponentSet any)
            {
                _frame = frame;
                _storage1 = s1;
                _storage2 = s2;
                _storage3 = s3;
                _with = ComponentSet.Create<T1, T2, T3>();
                _without = without;
                _any = any;

                _anySuccess = any.IsEmpty;
                _withoutSuccess = without.IsEmpty;
                _use256Path = _with.Rank <= 4 && any.Rank <= 4 && without.Rank <= 4;

                _blockIndex = -1;
                _blockCount = 0;
                _blockData1 = null;
                _blockEntities = null;
                _elementIndex = 0;
                _current = default;
            }

            public QueryItem<T1, T2, T3> Current => _current;

            public bool MoveNext()
            {
                if (_use256Path)
                    return MoveNext256();
                return MoveNext512();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool MoveNext256()
            {
                while (true)
                {
                    if (_blockData1 == null)
                    {
                        _blockIndex++;
                        if (!_storage1.TryGetBlock(_blockIndex, out _blockData1, out _blockEntities, out _blockCount))
                            return false;
                        _elementIndex = 0;
                    }

                    while (_elementIndex < _blockCount)
                    {
                        Entity entity = _blockEntities[_elementIndex];
                        _elementIndex++;

                        if ((uint)entity.Index >= (uint)_frame.Entities.Capacity)
                            continue;
                        if (!_frame.Entities.Exists(entity))
                            continue;

                        T2* c2;
                        T3* c3;
                        if (!_storage2.TryGetPointer(entity, out c2))
                            continue;
                        if (!_storage3.TryGetPointer(entity, out c3))
                            continue;

                        ComponentSet256 mask = _frame.GetComponentSet256(entity);
                        ulong* mset = (ulong*)&mask;

                        if ((mset[0] & _with.Set[0]) != _with.Set[0] ||
                            (mset[1] & _with.Set[1]) != _with.Set[1] ||
                            (mset[2] & _with.Set[2]) != _with.Set[2] ||
                            (mset[3] & _with.Set[3]) != _with.Set[3])
                            continue;

                        if (!_anySuccess)
                        {
                            if ((mset[0] & _any.Set[0]) == 0 &&
                                (mset[1] & _any.Set[1]) == 0 &&
                                (mset[2] & _any.Set[2]) == 0 &&
                                (mset[3] & _any.Set[3]) == 0)
                                continue;
                        }

                        if (!_withoutSuccess)
                        {
                            if ((mset[0] & _without.Set[0]) != 0 ||
                                (mset[1] & _without.Set[1]) != 0 ||
                                (mset[2] & _without.Set[2]) != 0 ||
                                (mset[3] & _without.Set[3]) != 0)
                                continue;
                        }

                        T1* c1 = (T1*)(_blockData1 + (_elementIndex - 1) * sizeof(T1));
                        _current = new QueryItem<T1, T2, T3>(entity, c1, c2, c3);
                        return true;
                    }

                    _blockData1 = null;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool MoveNext512()
            {
                while (true)
                {
                    if (_blockData1 == null)
                    {
                        _blockIndex++;
                        if (!_storage1.TryGetBlock(_blockIndex, out _blockData1, out _blockEntities, out _blockCount))
                            return false;
                        _elementIndex = 0;
                    }

                    while (_elementIndex < _blockCount)
                    {
                        Entity entity = _blockEntities[_elementIndex];
                        _elementIndex++;

                        if ((uint)entity.Index >= (uint)_frame.Entities.Capacity)
                            continue;
                        if (!_frame.Entities.Exists(entity))
                            continue;

                        T2* c2;
                        T3* c3;
                        if (!_storage2.TryGetPointer(entity, out c2))
                            continue;
                        if (!_storage3.TryGetPointer(entity, out c3))
                            continue;

                        ComponentSet mask = _frame.GetComponentSet(entity);

                        if (!mask.IsSupersetOf(_with))
                            continue;
                        if (!_anySuccess && !mask.Overlaps(_any))
                            continue;
                        if (!_withoutSuccess && mask.Overlaps(_without))
                            continue;

                        T1* c1 = (T1*)(_blockData1 + (_elementIndex - 1) * sizeof(T1));
                        _current = new QueryItem<T1, T2, T3>(entity, c1, c2, c3);
                        return true;
                    }

                    _blockData1 = null;
                }
            }
        }
    }
}
