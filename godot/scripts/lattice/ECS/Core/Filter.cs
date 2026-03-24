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
        private readonly Query<T1> _query;

        public Filter(Frame frame)
        {
            ArgumentNullException.ThrowIfNull(frame);
            _frame = frame;
            _query = frame.Query<T1>();
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
            private Query<T1>.Enumerator _enumerator;

            public Enumerator(Query<T1>.Enumerator enumerator)
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
                    return false;
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
        private readonly Query<T1, T2> _query;

        public Filter(Frame frame)
        {
            ArgumentNullException.ThrowIfNull(frame);
            _query = frame.Query<T1, T2>();
        }

        public Enumerator GetEnumerator() => new Enumerator(_query.GetEnumerator());

        public ref struct Enumerator
        {
            private Query<T1, T2>.Enumerator _enumerator;

            public Enumerator(Query<T1, T2>.Enumerator enumerator)
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
                    return false;
                }

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
        private readonly Query<T1, T2, T3> _query;

        public Filter(Frame frame)
        {
            ArgumentNullException.ThrowIfNull(frame);
            _query = frame.Query<T1, T2, T3>();
        }

        public Enumerator GetEnumerator() => new Enumerator(_query.GetEnumerator());

        public ref struct Enumerator
        {
            private Query<T1, T2, T3>.Enumerator _enumerator;

            public Enumerator(Query<T1, T2, T3>.Enumerator enumerator)
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
                    return false;
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
