using System;
using Lattice.Core;
using Lattice.ECS.Core;
using Lattice.ECS.Framework;
using Lattice.ECS.Framework.Systems;
using Lattice.Math;

namespace Lattice.Tests.Support
{
    internal static class GameplayValidationRegistry
    {
        private static readonly object SyncRoot = new();
        private static bool _registered;

        public static void EnsureRegistered()
        {
            if (_registered)
            {
                return;
            }

            lock (SyncRoot)
            {
                if (_registered)
                {
                    return;
                }

                ComponentRegistry.Register<Position2D>();
                ComponentRegistry.Register<Velocity2D>();
                ComponentRegistry.Register<Lifetime>();
                ComponentRegistry.Register<ProjectileTag>();
                ComponentRegistry.Register<CombatSpawnerComponent>();
                ComponentRegistry.Register<CombatProjectileComponent>();
                ComponentRegistry.Register<CombatTargetComponent>();
                ComponentRegistry.Register<CombatLootComponent>();
                ComponentRegistry.Register<CombatInputState>(ComponentFlags.Singleton, ComponentCallbacks.Empty);
                ComponentRegistry.Register<CombatDirectorState>(ComponentFlags.Singleton, ComponentCallbacks.Empty);
                ComponentRegistry.Register<PhaseOrderProbeComponent>();
                ComponentRegistry.Register<PhaseInputStateComponent>();
                ComponentRegistry.Register<PhaseActorTag>();

                _registered = true;
            }
        }
    }

    internal struct CombatSpawnerComponent : IComponent
    {
        public int Team;
        public int RemainingShots;
        public int BaseDamage;
        public FP CooldownRemaining;
        public FP Interval;
        public FP BaseVelocityX;
        public FP BaseVelocityY;
        public FP ProjectileLifetime;
    }

    internal struct CombatProjectileComponent : IComponent
    {
        public int Team;
        public int Damage;
    }

    internal struct CombatTargetComponent : IComponent
    {
        public int Team;
        public int Health;
        public int GoldBounty;
    }

    internal struct CombatLootComponent : IComponent
    {
        public int Gold;
    }

    internal struct CombatInputState : IComponent
    {
        public int Tick;
        public int DamageBonus;
        public FP VelocityXBonus;
    }

    internal struct CombatDirectorState : IComponent
    {
        public int SpawnedProjectiles;
        public int TotalHits;
        public int DefeatedTargets;
        public int TotalGold;
        public int LastResolvedTick;
    }

    internal struct PhaseOrderProbeComponent : IComponent
    {
        public int Order;
        public int AppliedVelocityX;
        public int SimulationObservedX;
        public int ResolveObservedX;
        public int CleanupObservedX;
        public int CleanupPassCount;
    }

    internal struct PhaseInputStateComponent : IComponent
    {
        public int PendingVelocityX;
        public int LastAppliedTick;
    }

    internal struct PhaseActorTag : IComponent
    {
    }

    internal sealed class CombatSpawnerSystem : ISystem
    {
        public SystemPhase Phase => SystemPhase.PreSimulation;

        public SystemAuthoringContract Contract => new(
            SystemFrameAccess.ReadWrite,
            SystemGlobalAccess.ReadOnly,
            SystemStructuralChangeAccess.Deferred);

        public void OnInit(Frame frame)
        {
        }

        public void OnUpdate(Frame frame, FP deltaTime)
        {
            var query = frame.Query<Position2D, CombatSpawnerComponent>();
            int capacity = query.Count;
            if (capacity <= 0)
            {
                return;
            }

            CombatInputState input = frame.TryGetGlobal<CombatInputState>(out CombatInputState currentInput)
                ? currentInput
                : default;

            Span<CombatSpawnCommand> commands = capacity <= 64
                ? stackalloc CombatSpawnCommand[capacity]
                : new CombatSpawnCommand[capacity];

            int commandCount = 0;
            var enumerator = query.GetEnumerator();
            while (enumerator.MoveNext())
            {
                ref Position2D position = ref enumerator.Component1;
                ref CombatSpawnerComponent spawner = ref enumerator.Component2;

                if (spawner.RemainingShots <= 0)
                {
                    continue;
                }

                spawner.CooldownRemaining -= deltaTime;
                if (spawner.CooldownRemaining > FP.Zero)
                {
                    continue;
                }

                commands[commandCount++] = new CombatSpawnCommand
                {
                    Position = position,
                    Velocity = new Velocity2D
                    {
                        X = spawner.BaseVelocityX + input.VelocityXBonus,
                        Y = spawner.BaseVelocityY
                    },
                    Lifetime = new Lifetime
                    {
                        Remaining = spawner.ProjectileLifetime
                    },
                    Projectile = new CombatProjectileComponent
                    {
                        Team = spawner.Team,
                        Damage = spawner.BaseDamage + input.DamageBonus
                    }
                };

                spawner.RemainingShots--;
                spawner.CooldownRemaining += spawner.Interval;
            }

            for (int i = 0; i < commandCount; i++)
            {
                EntityRef projectile = frame.CreateEntity();
                frame.Add(projectile, commands[i].Position);
                frame.Add(projectile, commands[i].Velocity);
                frame.Add(projectile, commands[i].Lifetime);
                frame.Add(projectile, commands[i].Projectile);
                frame.Add(projectile, new ProjectileTag());
            }
        }

        public void OnDestroy(Frame frame)
        {
        }

        private struct CombatSpawnCommand
        {
            public Position2D Position;
            public Velocity2D Velocity;
            public Lifetime Lifetime;
            public CombatProjectileComponent Projectile;
        }
    }

    internal sealed class CombatProjectileDamageSystem : ISystem
    {
        public SystemPhase Phase => SystemPhase.Resolve;

        public SystemAuthoringContract Contract => new(
            SystemFrameAccess.ReadWrite,
            SystemGlobalAccess.ReadWrite,
            SystemStructuralChangeAccess.Deferred);

        public void OnInit(Frame frame)
        {
        }

        public void OnUpdate(Frame frame, FP deltaTime)
        {
            var projectileQuery = frame.Query<Position2D, CombatProjectileComponent>();
            int projectileCapacity = projectileQuery.Count;
            if (projectileCapacity <= 0)
            {
                return;
            }

            ref CombatDirectorState director = ref frame.GetGlobal<CombatDirectorState>();

            Span<EntityRef> spentProjectiles = projectileCapacity <= 64
                ? stackalloc EntityRef[projectileCapacity]
                : new EntityRef[projectileCapacity];
            Span<EntityRef> defeatedTargets = projectileCapacity <= 64
                ? stackalloc EntityRef[projectileCapacity]
                : new EntityRef[projectileCapacity];
            Span<CombatLootSpawnCommand> lootCommands = projectileCapacity <= 64
                ? stackalloc CombatLootSpawnCommand[projectileCapacity]
                : new CombatLootSpawnCommand[projectileCapacity];

            int spentCount = 0;
            int defeatedCount = 0;
            int lootCount = 0;

            var projectileEnumerator = projectileQuery.GetEnumerator();
            while (projectileEnumerator.MoveNext())
            {
                EntityRef projectileEntity = projectileEnumerator.Entity;
                ref Position2D projectilePosition = ref projectileEnumerator.Component1;
                ref CombatProjectileComponent projectile = ref projectileEnumerator.Component2;

                var targetEnumerator = frame.Query<Position2D, CombatTargetComponent>().GetEnumerator();
                while (targetEnumerator.MoveNext())
                {
                    EntityRef targetEntity = targetEnumerator.Entity;
                    ref Position2D targetPosition = ref targetEnumerator.Component1;
                    ref CombatTargetComponent target = ref targetEnumerator.Component2;

                    if (target.Team == projectile.Team ||
                        target.Health <= 0 ||
                        projectilePosition.Y != targetPosition.Y ||
                        projectilePosition.X < targetPosition.X)
                    {
                        continue;
                    }

                    target.Health -= projectile.Damage;
                    director.TotalHits++;
                    director.LastResolvedTick = frame.Tick;
                    spentProjectiles[spentCount++] = projectileEntity;

                    if (target.Health <= 0)
                    {
                        director.DefeatedTargets++;
                        director.TotalGold += target.GoldBounty;
                        defeatedTargets[defeatedCount++] = targetEntity;
                        lootCommands[lootCount++] = new CombatLootSpawnCommand
                        {
                            Position = targetPosition,
                            Loot = new CombatLootComponent
                            {
                                Gold = target.GoldBounty
                            }
                        };
                    }

                    break;
                }
            }
            for (int i = 0; i < spentCount; i++)
            {
                frame.DestroyEntity(spentProjectiles[i]);
            }

            for (int i = 0; i < defeatedCount; i++)
            {
                frame.DestroyEntity(defeatedTargets[i]);
            }

            for (int i = 0; i < lootCount; i++)
            {
                EntityRef loot = frame.CreateEntity();
                frame.Add(loot, lootCommands[i].Position);
                frame.Add(loot, lootCommands[i].Loot);
            }
        }

        public void OnDestroy(Frame frame)
        {
        }

        private struct CombatLootSpawnCommand
        {
            public Position2D Position;
            public CombatLootComponent Loot;
        }
    }
}
