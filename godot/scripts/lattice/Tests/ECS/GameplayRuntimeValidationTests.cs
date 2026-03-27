using System;
using Lattice.Core;
using Lattice.ECS.Core;
using Lattice.ECS.Framework;
using Lattice.ECS.Framework.Systems;
using Lattice.ECS.Session;
using Lattice.Math;
using Lattice.Tests.Support;
using Xunit;

namespace Lattice.Tests.ECS
{
    public class GameplayRuntimeValidationTests
    {
        [Fact]
        public void CombatPipeline_SpawnsMovesDamagesKillsAndDropsLoot()
        {
            using var session = CreateCombatSession();
            session.Start();

            session.SeedDirectorState();
            session.SeedSpawner(
                new Position2D { X = 10, Y = 0 },
                new CombatSpawnerComponent
                {
                    Team = 1,
                    RemainingShots = 2,
                    BaseDamage = 2,
                    CooldownRemaining = FP.One,
                    Interval = FP.One,
                    BaseVelocityX = 2,
                    BaseVelocityY = FP.Zero,
                    ProjectileLifetime = 4
                });
            EntityRef enemy = session.SeedTarget(
                new Position2D { X = 12, Y = 0 },
                new CombatTargetComponent
                {
                    Team = 2,
                    Health = 4,
                    GoldBounty = 7
                });

            session.Update();
            Assert.Equal(1, CountProjectiles(session.PredictedFrame!));
            Assert.Equal(4, GetTargetHealth(session.PredictedFrame!, enemy));
            Assert.Equal(0, CountLoot(session.PredictedFrame!));

            session.Update();
            Assert.Equal(1, CountProjectiles(session.PredictedFrame!));
            Assert.Equal(2, GetTargetHealth(session.PredictedFrame!, enemy));
            Assert.Equal(new CombatDirectorState { TotalHits = 1, LastResolvedTick = 2 }, ReadDirectorPartial(session.PredictedFrame!));

            session.Update();
            Assert.False(session.PredictedFrame!.IsValid(enemy));
            Assert.Equal(0, CountProjectiles(session.PredictedFrame!));
            Assert.Equal(1, CountLoot(session.PredictedFrame!));

            CombatDirectorState director = session.PredictedFrame!.GetGlobal<CombatDirectorState>();
            Assert.Equal(2, director.TotalHits);
            Assert.Equal(1, director.DefeatedTargets);
            Assert.Equal(7, director.TotalGold);
            Assert.Equal(3, director.LastResolvedTick);
        }

        [Fact]
        public void CombatRollbackConsistency_LateInputResimulatesToExpectedEnemyState()
        {
            using var session = CreateCombatSession();
            session.Start();

            session.SeedDirectorState();
            session.SeedSpawner(
                new Position2D { X = 10, Y = 0 },
                new CombatSpawnerComponent
                {
                    Team = 1,
                    RemainingShots = 3,
                    BaseDamage = 2,
                    CooldownRemaining = FP.One,
                    Interval = FP.One,
                    BaseVelocityX = 2,
                    BaseVelocityY = FP.Zero,
                    ProjectileLifetime = 5
                });
            EntityRef primaryEnemy = session.SeedTarget(
                new Position2D { X = 12, Y = 0 },
                new CombatTargetComponent
                {
                    Team = 2,
                    Health = 6,
                    GoldBounty = 7
                });
            EntityRef secondaryEnemy = session.SeedTarget(
                new Position2D { X = 12, Y = 0 },
                new CombatTargetComponent
                {
                    Team = 2,
                    Health = 4,
                    GoldBounty = 11
                });

            for (int tick = 1; tick <= 4; tick++)
            {
                session.SetPlayerInput(session.LocalPlayerId, tick, new CombatInputCommand(session.LocalPlayerId, tick, FP.Zero, 0));
                session.Update();
            }

            Assert.Equal(4, GetTargetHealth(session.PredictedFrame!, secondaryEnemy));
            Assert.False(session.PredictedFrame!.IsValid(primaryEnemy));

            session.SetPlayerInput(session.LocalPlayerId, 2, new CombatInputCommand(session.LocalPlayerId, 2, FP.Zero, 2));
            Frame tickOneFrame = Assert.IsType<Frame>(session.GetHistoricalFrame(1));
            long mismatchedChecksum = unchecked((long)tickOneFrame.CalculateChecksum() + 1);

            session.VerifyFrame(1, mismatchedChecksum);

            Assert.Equal(4, session.CurrentTick);
            Assert.False(session.PredictedFrame!.IsValid(primaryEnemy));
            Assert.Equal(2, GetTargetHealth(session.PredictedFrame!, secondaryEnemy));

            CombatDirectorState director = session.PredictedFrame!.GetGlobal<CombatDirectorState>();
            Assert.Equal(3, director.TotalHits);
            Assert.Equal(1, director.DefeatedTargets);
            Assert.Equal(7, director.TotalGold);
            Assert.Equal(4, director.LastResolvedTick);
        }

        [Fact]
        public void GlobalState_CheckpointRestoreAndReplay_RemainsConsistent()
        {
            using var session = CreateCombatSession();
            session.Start();

            session.SeedDirectorState();
            session.SeedSpawner(
                new Position2D { X = 10, Y = 0 },
                new CombatSpawnerComponent
                {
                    Team = 1,
                    RemainingShots = 2,
                    BaseDamage = 2,
                    CooldownRemaining = FP.One,
                    Interval = FP.One,
                    BaseVelocityX = 2,
                    BaseVelocityY = FP.Zero,
                    ProjectileLifetime = 5
                });
            EntityRef enemy = session.SeedTarget(
                new Position2D { X = 12, Y = 0 },
                new CombatTargetComponent
                {
                    Team = 2,
                    Health = 6,
                    GoldBounty = 7
                });

            SessionCheckpoint checkpoint = session.CreateCheckpoint();

            session.SetPlayerInput(session.LocalPlayerId, 1, new CombatInputCommand(session.LocalPlayerId, 1, FP.Zero, 0));
            session.Update();
            session.SetPlayerInput(session.LocalPlayerId, 2, new CombatInputCommand(session.LocalPlayerId, 2, FP.Zero, 3));
            session.Update();
            session.SetPlayerInput(session.LocalPlayerId, 3, new CombatInputCommand(session.LocalPlayerId, 3, FP.Zero, 0));
            session.Update();

            CombatDirectorState boostedDirector = session.PredictedFrame!.GetGlobal<CombatDirectorState>();
            Assert.False(session.PredictedFrame!.IsValid(enemy));
            Assert.Equal(1, boostedDirector.DefeatedTargets);
            Assert.Equal(7, boostedDirector.TotalGold);

            session.RestoreFromCheckpoint(checkpoint);
            session.SetPlayerInput(session.LocalPlayerId, 1, new CombatInputCommand(session.LocalPlayerId, 1, FP.Zero, 0));
            session.Update();
            session.SetPlayerInput(session.LocalPlayerId, 2, new CombatInputCommand(session.LocalPlayerId, 2, FP.Zero, 0));
            session.Update();
            session.SetPlayerInput(session.LocalPlayerId, 3, new CombatInputCommand(session.LocalPlayerId, 3, FP.Zero, 0));
            session.Update();

            CombatDirectorState restoredDirector = session.PredictedFrame!.GetGlobal<CombatDirectorState>();
            Assert.True(session.PredictedFrame!.IsValid(enemy));
            Assert.Equal(2, GetTargetHealth(session.PredictedFrame!, enemy));
            Assert.Equal(2, restoredDirector.TotalHits);
            Assert.Equal(0, restoredDirector.DefeatedTargets);
            Assert.Equal(0, restoredDirector.TotalGold);
            Assert.Equal(3, restoredDirector.LastResolvedTick);
        }

        [Fact]
        public void MultiPhaseOrdering_PreservesStableVisibilityAcrossPhases()
        {
            using var session = CreatePhaseSession();
            session.Start();
            session.SeedPhaseProbe();
            session.SeedPhaseActor(new Position2D { X = 0, Y = 0 });

            session.SetPlayerInput(session.LocalPlayerId, 1, new PhaseInputCommand(session.LocalPlayerId, 1, 3));
            session.Update();

            PhaseOrderProbeComponent probe = ReadPhaseProbe(session.PredictedFrame!);
            Assert.Equal(1234, probe.Order);
            Assert.Equal(3, probe.AppliedVelocityX);
            Assert.Equal(3, probe.SimulationObservedX);
            Assert.Equal(3, probe.ResolveObservedX);
            Assert.Equal(3, probe.CleanupObservedX);
            Assert.Equal(1, probe.CleanupPassCount);
        }

        private static CombatValidationSession CreateCombatSession()
        {
            GameplayValidationRegistry.EnsureRegistered();

            var session = new CombatValidationSession(FP.One);
            session.RegisterSystem(new CombatSpawnerSystem());
            session.RegisterSystem(new MovementSystem());
            session.RegisterSystem(new LifetimeSystem());
            session.RegisterSystem(new CombatProjectileDamageSystem());
            return session;
        }

        private static PhaseValidationSession CreatePhaseSession()
        {
            GameplayValidationRegistry.EnsureRegistered();

            var session = new PhaseValidationSession(FP.One);
            session.RegisterSystem(new PhasePreSimulationSystem());
            session.RegisterSystem(new MovementSystem());
            session.RegisterSystem(new PhaseSimulationAuditSystem());
            session.RegisterSystem(new PhaseResolveAuditSystem());
            session.RegisterSystem(new PhaseCleanupAuditSystem());
            return session;
        }

        private static CombatDirectorState ReadDirectorPartial(Frame frame)
        {
            CombatDirectorState director = frame.GetGlobal<CombatDirectorState>();
            return new CombatDirectorState
            {
                TotalHits = director.TotalHits,
                LastResolvedTick = director.LastResolvedTick
            };
        }

        private static int CountProjectiles(Frame frame)
        {
            int count = 0;
            var enumerator = frame.Query<ProjectileTag>().GetEnumerator();
            while (enumerator.MoveNext())
            {
                count++;
            }

            return count;
        }

        private static int CountLoot(Frame frame)
        {
            int count = 0;
            var enumerator = frame.Query<CombatLootComponent>().GetEnumerator();
            while (enumerator.MoveNext())
            {
                count++;
            }

            return count;
        }

        private static int GetTargetHealth(Frame frame, EntityRef entity)
        {
            return frame.Get<CombatTargetComponent>(entity).Health;
        }

        private static PhaseOrderProbeComponent ReadPhaseProbe(Frame frame)
        {
            var enumerator = frame.Query<PhaseOrderProbeComponent>().GetEnumerator();
            Assert.True(enumerator.MoveNext());
            return enumerator.Component;
        }

        private abstract class MirroredTestSessionBase : MinimalPredictionSession
        {
            protected MirroredTestSessionBase(FP deltaTime)
                : base(deltaTime)
            {
            }

            protected EntityRef MirrorEntity(Action<Frame, EntityRef> configure)
            {
                if (VerifiedFrame == null || PredictedFrame == null)
                {
                    throw new InvalidOperationException("Session must be started before seeding gameplay state.");
                }

                EntityRef verified = VerifiedFrame.CreateEntity();
                configure(VerifiedFrame, verified);

                EntityRef predicted = PredictedFrame.CreateEntity();
                configure(PredictedFrame, predicted);

                if (verified != predicted)
                {
                    throw new InvalidOperationException("Mirrored entity IDs diverged between verified and predicted frames.");
                }

                return verified;
            }

            protected void MirrorGlobal<T>(in T component)
                where T : unmanaged, IComponent
            {
                if (VerifiedFrame == null || PredictedFrame == null)
                {
                    throw new InvalidOperationException("Session must be started before seeding global state.");
                }

                VerifiedFrame.SetGlobal(component);
                PredictedFrame.SetGlobal(component);
            }
        }

        private sealed class CombatValidationSession : MirroredTestSessionBase
        {
            public CombatValidationSession(FP deltaTime)
                : base(deltaTime)
            {
            }

            public void SeedDirectorState()
            {
                MirrorGlobal(new CombatDirectorState());
                MirrorGlobal(new CombatInputState());
            }

            public EntityRef SeedSpawner(Position2D position, CombatSpawnerComponent spawner)
            {
                return MirrorEntity(
                    (frame, entity) =>
                    {
                        frame.Add(entity, position);
                        frame.Add(entity, spawner);
                    });
            }

            public EntityRef SeedTarget(Position2D position, CombatTargetComponent target)
            {
                return MirrorEntity(
                    (frame, entity) =>
                    {
                        frame.Add(entity, position);
                        frame.Add(entity, target);
                    });
            }

            protected override void ApplyInputSet(Frame frame, in SessionInputSet inputSet)
            {
                var state = new CombatInputState
                {
                    Tick = inputSet.Tick,
                    DamageBonus = 0,
                    VelocityXBonus = FP.Zero
                };

                if (inputSet.TryGetPlayerInput(LocalPlayerId, out CombatInputCommand? input))
                {
                    ArgumentNullException.ThrowIfNull(input);
                    state.DamageBonus = input.DamageBonus;
                    state.VelocityXBonus = input.VelocityXBonus;
                }

                frame.SetGlobal(state);
            }
        }

        private sealed class CombatInputCommand : IPlayerInput
        {
            public CombatInputCommand(int playerId, int tick, FP velocityXBonus, int damageBonus)
            {
                PlayerId = playerId;
                Tick = tick;
                VelocityXBonus = velocityXBonus;
                DamageBonus = damageBonus;
            }

            public int PlayerId { get; }

            public int Tick { get; }

            public FP VelocityXBonus { get; }

            public int DamageBonus { get; }
        }

        private sealed class PhaseValidationSession : MirroredTestSessionBase
        {
            public PhaseValidationSession(FP deltaTime)
                : base(deltaTime)
            {
            }

            public void SeedPhaseProbe()
            {
                MirrorEntity(
                    (frame, entity) =>
                    {
                        frame.Add(entity, new PhaseOrderProbeComponent());
                        frame.Add(entity, new PhaseInputStateComponent());
                    });
            }

            public void SeedPhaseActor(Position2D position)
            {
                MirrorEntity(
                    (frame, entity) =>
                    {
                        frame.Add(entity, position);
                        frame.Add(entity, new Velocity2D());
                        frame.Add(entity, new PhaseActorTag());
                    });
            }

            protected override void ApplyInputSet(Frame frame, in SessionInputSet inputSet)
            {
                int velocityX = 0;
                if (inputSet.TryGetPlayerInput(LocalPlayerId, out PhaseInputCommand? input))
                {
                    ArgumentNullException.ThrowIfNull(input);
                    velocityX = input.VelocityX;
                }

                var probeEnumerator = frame.Query<PhaseInputStateComponent>().GetEnumerator();
                while (probeEnumerator.MoveNext())
                {
                    probeEnumerator.Component.PendingVelocityX = velocityX;
                    probeEnumerator.Component.LastAppliedTick = inputSet.Tick;
                }
            }
        }

        private sealed class PhaseInputCommand : IPlayerInput
        {
            public PhaseInputCommand(int playerId, int tick, int velocityX)
            {
                PlayerId = playerId;
                Tick = tick;
                VelocityX = velocityX;
            }

            public int PlayerId { get; }

            public int Tick { get; }

            public int VelocityX { get; }
        }

        private sealed class PhasePreSimulationSystem : ISystem
        {
            public SystemPhase Phase => SystemPhase.PreSimulation;

            public void OnInit(Frame frame)
            {
            }

            public void OnUpdate(Frame frame, FP deltaTime)
            {
                int velocityX = 0;
                var inputEnumerator = frame.Query<PhaseInputStateComponent>().GetEnumerator();
                while (inputEnumerator.MoveNext())
                {
                    velocityX = inputEnumerator.Component.PendingVelocityX;
                }

                var actorEnumerator = frame.Query<Velocity2D, PhaseActorTag>().GetEnumerator();
                while (actorEnumerator.MoveNext())
                {
                    actorEnumerator.Component1.X = velocityX;
                    actorEnumerator.Component1.Y = FP.Zero;
                }

                var probeEnumerator = frame.Query<PhaseOrderProbeComponent>().GetEnumerator();
                while (probeEnumerator.MoveNext())
                {
                    probeEnumerator.Component.Order = probeEnumerator.Component.Order * 10 + 1;
                    probeEnumerator.Component.AppliedVelocityX = velocityX;
                }
            }

            public void OnDestroy(Frame frame)
            {
            }
        }

        private sealed class PhaseSimulationAuditSystem : ISystem
        {
            public void OnInit(Frame frame)
            {
            }

            public void OnUpdate(Frame frame, FP deltaTime)
            {
                int positionX = 0;
                var actorEnumerator = frame.Query<Position2D, PhaseActorTag>().GetEnumerator();
                while (actorEnumerator.MoveNext())
                {
                    positionX = (int)actorEnumerator.Component1.X;
                }

                var probeEnumerator = frame.Query<PhaseOrderProbeComponent>().GetEnumerator();
                while (probeEnumerator.MoveNext())
                {
                    probeEnumerator.Component.Order = probeEnumerator.Component.Order * 10 + 2;
                    probeEnumerator.Component.SimulationObservedX = positionX;
                }
            }

            public void OnDestroy(Frame frame)
            {
            }
        }

        private sealed class PhaseResolveAuditSystem : ISystem
        {
            public SystemPhase Phase => SystemPhase.Resolve;

            public void OnInit(Frame frame)
            {
            }

            public void OnUpdate(Frame frame, FP deltaTime)
            {
                int positionX = 0;
                var actorEnumerator = frame.Query<Position2D, PhaseActorTag>().GetEnumerator();
                while (actorEnumerator.MoveNext())
                {
                    positionX = (int)actorEnumerator.Component1.X;
                }

                var probeEnumerator = frame.Query<PhaseOrderProbeComponent>().GetEnumerator();
                while (probeEnumerator.MoveNext())
                {
                    probeEnumerator.Component.Order = probeEnumerator.Component.Order * 10 + 3;
                    probeEnumerator.Component.ResolveObservedX = positionX;
                }
            }

            public void OnDestroy(Frame frame)
            {
            }
        }

        private sealed class PhaseCleanupAuditSystem : ISystem
        {
            public SystemPhase Phase => SystemPhase.Cleanup;

            public void OnInit(Frame frame)
            {
            }

            public void OnUpdate(Frame frame, FP deltaTime)
            {
                int positionX = 0;
                var actorEnumerator = frame.Query<Position2D, PhaseActorTag>().GetEnumerator();
                while (actorEnumerator.MoveNext())
                {
                    positionX = (int)actorEnumerator.Component1.X;
                }

                var probeEnumerator = frame.Query<PhaseOrderProbeComponent>().GetEnumerator();
                while (probeEnumerator.MoveNext())
                {
                    probeEnumerator.Component.Order = probeEnumerator.Component.Order * 10 + 4;
                    probeEnumerator.Component.CleanupObservedX = positionX;
                    probeEnumerator.Component.CleanupPassCount++;
                }
            }

            public void OnDestroy(Frame frame)
            {
            }
        }
    }
}
