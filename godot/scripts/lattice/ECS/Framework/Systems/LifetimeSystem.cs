using System;
using Lattice.Core;
using Lattice.ECS.Core;
using Lattice.Math;

namespace Lattice.ECS.Framework.Systems
{
    /// <summary>
    /// 最小样例生命周期系统。
    /// </summary>
    public sealed class LifetimeSystem : ISystem
    {
        public SystemAuthoringContract Contract => new(
            SystemFrameAccess.ReadWrite,
            SystemGlobalAccess.None,
            SystemStructuralChangeAccess.Deferred);

        public void OnInit(Frame frame)
        {
        }

        public void OnUpdate(Frame frame, FP deltaTime)
        {
            var query = frame.Query<Lifetime>();
            int capacity = query.Count;
            if (capacity <= 0)
            {
                return;
            }

            Span<EntityRef> expiredEntities = capacity <= 64
                ? stackalloc EntityRef[capacity]
                : new EntityRef[capacity];

            int expiredCount = 0;
            var enumerator = query.GetEnumerator();
            while (enumerator.MoveNext())
            {
                enumerator.Component.Remaining -= deltaTime;
                if (enumerator.Component.Remaining <= FP.Zero)
                {
                    expiredEntities[expiredCount++] = enumerator.CurrentEntity;
                }
            }

            for (int i = 0; i < expiredCount; i++)
            {
                frame.DestroyEntity(expiredEntities[i]);
            }
        }

        public void OnDestroy(Frame frame)
        {
        }
    }
}
