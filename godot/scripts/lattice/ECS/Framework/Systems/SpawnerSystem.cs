using System;
using Lattice.Core;
using Lattice.ECS.Core;
using Lattice.Math;

namespace Lattice.ECS.Framework.Systems
{
    /// <summary>
    /// 最小样例生成系统。
    /// 用于验证“实体在系统更新中创建，再参与后续系统处理”的链路。
    /// </summary>
    public sealed class SpawnerSystem : ISystem
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
            var query = frame.Query<Position2D, Spawner>();
            int capacity = query.Count;
            if (capacity <= 0)
            {
                return;
            }

            Span<SpawnCommand> commands = capacity <= 32
                ? stackalloc SpawnCommand[capacity]
                : new SpawnCommand[capacity];

            int commandCount = 0;
            var enumerator = query.GetEnumerator();
            while (enumerator.MoveNext())
            {
                ref Position2D position = ref enumerator.Component1;
                ref Spawner spawner = ref enumerator.Component2;

                if (spawner.RemainingCount <= 0)
                {
                    continue;
                }

                spawner.CooldownRemaining -= deltaTime;
                if (spawner.CooldownRemaining > FP.Zero)
                {
                    continue;
                }

                commands[commandCount++] = new SpawnCommand
                {
                    Position = position,
                    Velocity = new Velocity2D
                    {
                        X = spawner.SpawnVelocityX,
                        Y = spawner.SpawnVelocityY
                    },
                    Lifetime = new Lifetime
                    {
                        Remaining = spawner.SpawnLifetime
                    }
                };

                spawner.RemainingCount--;
                spawner.CooldownRemaining += spawner.Interval;
            }

            for (int i = 0; i < commandCount; i++)
            {
                EntityRef entity = frame.CreateEntity();
                frame.Add(entity, commands[i].Position);
                frame.Add(entity, commands[i].Velocity);
                frame.Add(entity, commands[i].Lifetime);
                frame.Add(entity, new ProjectileTag());
            }
        }

        public void OnDestroy(Frame frame)
        {
        }

        private struct SpawnCommand
        {
            public Position2D Position;
            public Velocity2D Velocity;
            public Lifetime Lifetime;
        }
    }
}
