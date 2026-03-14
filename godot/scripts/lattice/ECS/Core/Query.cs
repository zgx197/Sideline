using System;
using System.Runtime.CompilerServices;
using Lattice.Core;

namespace Lattice.ECS.Core
{
    // ECS 查询系统 - 支持多组件联合查询
    // 性能特点：
    // - ref struct 迭代器（零分配）
    // - 密集数组遍历（缓存友好）
    // - 位图快速过滤（ComponentSet）

    #region 单类型查询

    /// <summary>
    /// 单类型组件查询
    /// </summary>
    public readonly ref struct Query<T> where T : struct
    {
        private readonly Frame _frame;
        private readonly int _typeId;
        private readonly ComponentStorage<T> _storage;

        public Query(Frame frame)
        {
            _frame = frame;
            _typeId = frame.GetTypeId<T>();
            frame.TryGetStorage(out _storage);
        }

        /// <summary>
        /// 遍历所有匹配的实体和组件
        /// </summary>
        public Enumerator GetEnumerator() => new Enumerator(_frame, _storage);

        /// <summary>
        /// ref struct 迭代器
        /// </summary>
        public ref struct Enumerator
        {
            private readonly Frame _frame;
            private readonly ComponentStorage<T> _storage;
            private ComponentStorage<T>.ComponentEnumerator _storageEnumerator;
            private QueryItem<T> _current;

            public Enumerator(Frame frame, ComponentStorage<T> storage)
            {
                _frame = frame;
                _storage = storage;
                _storageEnumerator = storage != null ? storage.GetEnumerator() : default;
                _current = default;
            }

            public bool MoveNext()
            {
                if (_storage == null) return false;

                while (_storageEnumerator.MoveNext())
                {
                    var item = _storageEnumerator.Current;
                    if (_frame.IsValid(item.Entity))
                    {
                        _current = new QueryItem<T>(item.Entity, new Ref<T>(ref _storage.Get(item.Entity)));
                        return true;
                    }
                }
                return false;
            }

            public QueryItem<T> Current => _current;
        }
    }

    #endregion

    #region 双类型查询

    /// <summary>
    /// 双类型组件查询
    /// </summary>
    public readonly ref struct Query<T1, T2>
        where T1 : struct
        where T2 : struct
    {
        private readonly Frame _frame;
        private readonly int _typeId1;
        private readonly int _typeId2;
        private readonly ComponentStorage<T1> _storage1;
        private readonly ComponentSet _requiredSet;

        public Query(Frame frame)
        {
            _frame = frame;
            _typeId1 = frame.GetTypeId<T1>();
            _typeId2 = frame.GetTypeId<T2>();
            frame.TryGetStorage(out _storage1);

            // 构建所需的组件集合
            _requiredSet = ComponentSet.Empty;
            _requiredSet.Add(_typeId1);
            _requiredSet.Add(_typeId2);
        }

        /// <summary>
        /// 遍历所有同时包含两种组件的实体
        /// </summary>
        public Enumerator GetEnumerator() => new Enumerator(_frame, _storage1, _typeId2, _requiredSet);

        /// <summary>
        /// ref struct 迭代器
        /// </summary>
        public ref struct Enumerator
        {
            private readonly Frame _frame;
            private readonly ComponentStorage<T1> _storage1;
            private readonly int _typeId2;
            private readonly ComponentSet _requiredSet;
            private ComponentStorage<T1>.ComponentEnumerator _enumerator1;
            private QueryItem<T1, T2> _current;

            public Enumerator(Frame frame, ComponentStorage<T1> storage1, int typeId2, ComponentSet requiredSet)
            {
                _frame = frame;
                _storage1 = storage1;
                _typeId2 = typeId2;
                _requiredSet = requiredSet;
                _enumerator1 = storage1 != null ? storage1.GetEnumerator() : default;
                _current = default;
            }

            public bool MoveNext()
            {
                if (_storage1 == null) return false;

                while (_enumerator1.MoveNext())
                {
                    var item1 = _enumerator1.Current;
                    var entity = item1.Entity;

                    // 快速检查：实体是否有所有需要的组件
                    if (!_frame.MatchesQuery(entity, _requiredSet))
                        continue;

                    // 获取第二个组件
                    if (_frame.TryGetComponent<T2>(entity, out var component2))
                    {
                        _current = new QueryItem<T1, T2>(entity, new Ref<T1>(ref _storage1.Get(entity)), component2);
                        return true;
                    }
                }
                return false;
            }

            public QueryItem<T1, T2> Current => _current;
        }
    }

    #endregion

    #region 三类型查询

    /// <summary>
    /// 三类型组件查询
    /// </summary>
    public readonly ref struct Query<T1, T2, T3>
        where T1 : struct
        where T2 : struct
        where T3 : struct
    {
        private readonly Frame _frame;
        private readonly int _typeId1;
        private readonly int _typeId2;
        private readonly int _typeId3;
        private readonly ComponentStorage<T1> _storage1;
        private readonly ComponentSet _requiredSet;

        public Query(Frame frame)
        {
            _frame = frame;
            _typeId1 = frame.GetTypeId<T1>();
            _typeId2 = frame.GetTypeId<T2>();
            _typeId3 = frame.GetTypeId<T3>();
            frame.TryGetStorage(out _storage1);

            // 构建所需的组件集合
            _requiredSet = ComponentSet.Empty;
            _requiredSet.Add(_typeId1);
            _requiredSet.Add(_typeId2);
            _requiredSet.Add(_typeId3);
        }

        /// <summary>
        /// 遍历所有同时包含三种组件的实体
        /// </summary>
        public Enumerator GetEnumerator() => new Enumerator(_frame, _storage1, _typeId2, _typeId3, _requiredSet);

        /// <summary>
        /// ref struct 迭代器
        /// </summary>
        public ref struct Enumerator
        {
            private readonly Frame _frame;
            private readonly ComponentStorage<T1> _storage1;
            private readonly int _typeId2;
            private readonly int _typeId3;
            private readonly ComponentSet _requiredSet;
            private ComponentStorage<T1>.ComponentEnumerator _enumerator1;
            private QueryItem<T1, T2, T3> _current;

            public Enumerator(Frame frame, ComponentStorage<T1> storage1, int typeId2, int typeId3, ComponentSet requiredSet)
            {
                _frame = frame;
                _storage1 = storage1;
                _typeId2 = typeId2;
                _typeId3 = typeId3;
                _requiredSet = requiredSet;
                _enumerator1 = storage1 != null ? storage1.GetEnumerator() : default;
                _current = default;
            }

            public bool MoveNext()
            {
                if (_storage1 == null) return false;

                while (_enumerator1.MoveNext())
                {
                    var item1 = _enumerator1.Current;
                    var entity = item1.Entity;

                    // 快速检查：实体是否有所有需要的组件
                    if (!_frame.MatchesQuery(entity, _requiredSet))
                        continue;

                    // 获取第二、三个组件
                    if (_frame.TryGetComponent<T2>(entity, out var component2) &&
                        _frame.TryGetComponent<T3>(entity, out var component3))
                    {
                        _current = new QueryItem<T1, T2, T3>(entity, new Ref<T1>(ref _storage1.Get(entity)), component2, component3);
                        return true;
                    }
                }
                return false;
            }

            public QueryItem<T1, T2, T3> Current => _current;
        }
    }

    #endregion

    #region 查询结果项

    /// <summary>
    /// 单类型查询结果项
    /// </summary>
    public readonly ref struct QueryItem<T> where T : struct
    {
        public readonly Entity Entity;
        private readonly Ref<T> _component;

        public QueryItem(Entity entity, Ref<T> component)
        {
            Entity = entity;
            _component = component;
        }

        public ref T Component => ref _component.Value;
    }

    /// <summary>
    /// 双类型查询结果项
    /// </summary>
    public readonly ref struct QueryItem<T1, T2>
        where T1 : struct
        where T2 : struct
    {
        public readonly Entity Entity;
        private readonly Ref<T1> _component1;
        private readonly T2 _component2;

        public QueryItem(Entity entity, Ref<T1> component1, T2 component2)
        {
            Entity = entity;
            _component1 = component1;
            _component2 = component2;
        }

        public ref T1 Component1 => ref _component1.Value;
        public T2 Component2 => _component2;
    }

    /// <summary>
    /// 三类型查询结果项
    /// </summary>
    public readonly ref struct QueryItem<T1, T2, T3>
        where T1 : struct
        where T2 : struct
        where T3 : struct
    {
        public readonly Entity Entity;
        private readonly Ref<T1> _component1;
        private readonly T2 _component2;
        private readonly T3 _component3;

        public QueryItem(Entity entity, Ref<T1> component1, T2 component2, T3 component3)
        {
            Entity = entity;
            _component1 = component1;
            _component2 = component2;
            _component3 = component3;
        }

        public ref T1 Component1 => ref _component1.Value;
        public T2 Component2 => _component2;
        public T3 Component3 => _component3;
    }

    #endregion

}
