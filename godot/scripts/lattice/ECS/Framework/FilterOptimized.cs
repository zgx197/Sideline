// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;
using Lattice.Core;
using Lattice.ECS.Core;

namespace Lattice.ECS.Framework
{
    /// <summary>
    /// 旧版带裁剪能力的单组件 Filter 兼容层。
    /// 新代码建议直接使用 <see cref="Frame.Query{T}()" /> 并在循环中叠加裁剪判断。
    /// </summary>
    [Obsolete("请改用 Frame.Query<T>() 并在循环中处理裁剪。FilterOptimized 仅保留为兼容层。", false)]
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public unsafe class FilterOptimized<T1> where T1 : unmanaged, IComponent
    {
        private readonly Query<T1> _query;
        private readonly CullingSystem* _culling;

        public FilterOptimized(Frame frame, CullingSystem* culling = null)
        {
            if (frame == null)
            {
                throw new ArgumentNullException(nameof(frame));
            }

            _query = frame.Query<T1>();
            _culling = culling;
        }

        public Enumerator GetEnumerator() => new Enumerator(_query.GetEnumerator(), _culling);

        public ref struct Enumerator
        {
            private Query<T1>.Enumerator _enumerator;
            private readonly CullingSystem* _culling;

            public Enumerator(Query<T1>.Enumerator enumerator, CullingSystem* culling)
            {
                _enumerator = enumerator;
                _culling = culling;
                CurrentEntity = EntityRef.None;
                CurrentPtr = null;
            }

            public EntityRef CurrentEntity;
            public T1* CurrentPtr;
            public ref T1 Component => ref *CurrentPtr;

            public bool MoveNext()
            {
                while (_enumerator.MoveNext())
                {
                    EntityRef entity = _enumerator.CurrentEntity;
                    if (_culling != null && _culling->IsCulled(entity.Index))
                    {
                        continue;
                    }

                    CurrentEntity = entity;
                    CurrentPtr = _enumerator.CurrentPtr;
                    return true;
                }

                return false;
            }
        }
    }

    /// <summary>
    /// 旧版带裁剪能力的双组件 Filter 兼容层。
    /// 新代码建议直接使用 <see cref="Frame.Query{T1,T2}()" /> 并在循环中叠加裁剪判断。
    /// </summary>
    [Obsolete("请改用 Frame.Query<T1, T2>() 并在循环中处理裁剪。FilterOptimized 仅保留为兼容层。", false)]
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public unsafe class FilterOptimized<T1, T2>
        where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
    {
        private readonly Query<T1, T2> _query;
        private readonly CullingSystem* _culling;

        public FilterOptimized(Frame frame, CullingSystem* culling = null)
        {
            if (frame == null)
            {
                throw new ArgumentNullException(nameof(frame));
            }

            _query = frame.Query<T1, T2>();
            _culling = culling;
        }

        public Enumerator GetEnumerator() => new Enumerator(_query.GetEnumerator(), _culling);

        public ref struct Enumerator
        {
            private Query<T1, T2>.Enumerator _enumerator;
            private readonly CullingSystem* _culling;

            public Enumerator(Query<T1, T2>.Enumerator enumerator, CullingSystem* culling)
            {
                _enumerator = enumerator;
                _culling = culling;
                CurrentEntity = EntityRef.None;
                CurrentPtr1 = null;
                CurrentPtr2 = null;
            }

            public EntityRef CurrentEntity;
            public T1* CurrentPtr1;
            public T2* CurrentPtr2;
            public ref T1 Component1 => ref *CurrentPtr1;
            public ref T2 Component2 => ref *CurrentPtr2;

            public bool MoveNext()
            {
                while (_enumerator.MoveNext())
                {
                    EntityRef entity = _enumerator.Entity;
                    if (_culling != null && _culling->IsCulled(entity.Index))
                    {
                        continue;
                    }

                    CurrentEntity = entity;
                    CurrentPtr1 = _enumerator.Component1Ptr;
                    CurrentPtr2 = _enumerator.Component2Ptr;
                    return true;
                }

                return false;
            }
        }
    }
}
