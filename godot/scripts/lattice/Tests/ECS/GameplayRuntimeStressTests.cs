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
    public class GameplayRuntimeStressTests
    {
        [Fact]
        public void DeferredStructuralChanges_ComposeAcrossPhasesAndBecomeVisibleAfterCommit()
        {
            using var session = CreateDeferredSession();
            session.Start();

            session.SeedDeferredProbe();
            session.SeedActivationCandidate();
            session.SeedRemovalCandidate();
            session.SeedDestroyCandidate();

            session.Update();

            DeferredLifecycleProbeComponent tickOneProbe = ReadDeferredProbe(session.PredictedFrame!);
            Assert.Equal(0, tickOneProbe.SimulationSpawnedVisible);
            Assert.Equal(0, tickOneProbe.ResolveSpawnedVisible);
            Assert.Equal(0, tickOneProbe.CleanupSpawnedVisible);
            Assert.Equal(0, tickOneProbe.SimulationActivatedVisible);
            Assert.Equal(0, tickOneProbe.ResolveActivatedVisible);
            Assert.Equal(0, tickOneProbe.CleanupActivatedVisible);
            Assert.Equal(1, tickOneProbe.SimulationTransientVisible);
            Assert.Equal(1, tickOneProbe.ResolveTransientVisible);
            Assert.Equal(1, tickOneProbe.CleanupTransientVisible);
            Assert.Equal(1, tickOneProbe.SimulationDestroyVisible);
            Assert.Equal(1, tickOneProbe.ResolveDestroyVisible);
            Assert.Equal(1, tickOneProbe.CleanupDestroyVisible);

            Assert.Equal(1, CountEntities<DeferredSpawnedTag>(session.PredictedFrame!));
            Assert.Equal(1, CountEntities<DeferredActivatedTag>(session.PredictedFrame!));
            Assert.Equal(0, CountEntities<DeferredTransientTag>(session.PredictedFrame!));
            Assert.Equal(0, CountEntities<DeferredDestroyCandidateTag>(session.PredictedFrame!));

            session.Update();

            DeferredLifecycleProbeComponent tickTwoProbe = ReadDeferredProbe(session.PredictedFrame!);
            Assert.Equal(1, tickTwoProbe.SimulationSpawnedVisible);
            Assert.Equal(1, tickTwoProbe.ResolveSpawnedVisible);
            Assert.Equal(1, tickTwoProbe.CleanupSpawnedVisible);
            Assert.Equal(1, tickTwoProbe.SimulationActivatedVisible);
            Assert.Equal(1, tickTwoProbe.ResolveActivatedVisible);
            Assert.Equal(1, tickTwoProbe.CleanupActivatedVisible);
            Assert.Equal(0, tickTwoProbe.SimulationTransientVisible);
            Assert.Equal(0, tickTwoProbe.ResolveTransientVisible);
            Assert.Equal(0, tickTwoProbe.CleanupTransientVisible);
            Assert.Equal(0, tickTwoProbe.SimulationDestroyVisible);
            Assert.Equal(0, tickTwoProbe.ResolveDestroyVisible);
            Assert.Equal(0, tickTwoProbe.CleanupDestroyVisible);

            Assert.Equal(1, CountEntities<DeferredSpawnedTag>(session.PredictedFrame!));
            Assert.Equal(1, CountEntities<DeferredActivatedTag>(session.PredictedFrame!));
            Assert.Equal(0, CountEntities<DeferredTransientTag>(session.PredictedFrame!));
            Assert.Equal(0, CountEntities<DeferredDestroyCandidateTag>(session.PredictedFrame!));
        }

        [Fact]
        public void CheckpointChain_RestoreReplayAndNestedCheckpointRemainConsistent()
        {
            using var session = CreateCombatSession();
            session.Start();

            CombatScenario scenario = SeedCombatScenario(session);
            SessionCheckpoint baselineCheckpoint = session.CreateCheckpoint();

            RunCombatTicksAndVerify(session, 1, 4, BuildCheckpointChainInput);
            SessionCheckpoint checkpointOne = session.CreateCheckpoint();
            CombatFrameSnapshot checkpointOneSnapshot = CaptureCombatSnapshot(session.PredictedFrame!, scenario.Targets);

            RunCombatTicksAndVerify(session, 5, 7, BuildCheckpointChainInput);
            SessionCheckpoint checkpointTwo = session.CreateCheckpoint();
            CombatFrameSnapshot checkpointTwoSnapshot = CaptureCombatSnapshot(session.PredictedFrame!, scenario.Targets);

            RunCombatTicksAndVerify(session, 8, 9, BuildCheckpointChainInput);
            CombatFrameSnapshot finalSnapshot = CaptureCombatSnapshot(session.PredictedFrame!, scenario.Targets);

            session.RestoreFromCheckpoint(checkpointOne);
            RunCombatTicksAndVerify(session, 5, 7, BuildCheckpointChainInput);
            Assert.Equal(checkpointTwoSnapshot, CaptureCombatSnapshot(session.PredictedFrame!, scenario.Targets));

            SessionCheckpoint rebuiltCheckpointTwo = session.CreateCheckpoint();

            RunCombatTicksAndVerify(session, 8, 9, BuildCheckpointChainInput);
            Assert.Equal(finalSnapshot, CaptureCombatSnapshot(session.PredictedFrame!, scenario.Targets));

            session.RestoreFromCheckpoint(checkpointTwo);
            Assert.Equal(checkpointTwoSnapshot, CaptureCombatSnapshot(session.PredictedFrame!, scenario.Targets));

            session.RestoreFromCheckpoint(baselineCheckpoint);
            RunCombatTicksAndVerify(session, 1, 4, BuildCheckpointChainInput);
            Assert.Equal(checkpointOneSnapshot, CaptureCombatSnapshot(session.PredictedFrame!, scenario.Targets));

            session.RestoreFromCheckpoint(rebuiltCheckpointTwo);
            Assert.Equal(checkpointTwoSnapshot, CaptureCombatSnapshot(session.PredictedFrame!, scenario.Targets));
        }

        [Fact]
        public void HistoryAnchors_WarmColdAndRollbackReadsRemainConsistent()
        {
            using var session = CreateCombatSession();
            session.Start();

            CombatScenario scenario = SeedCombatScenario(session);

            RunCombatTicks(session, 1, 32, BuildHistoryInput);

            CombatFrameSnapshot warm11 = CaptureCombatSnapshot(Assert.IsType<Frame>(session.GetHistoricalFrame(11)), scenario.Targets);
            CombatFrameSnapshot warm14 = CaptureCombatSnapshot(Assert.IsType<Frame>(session.GetHistoricalFrame(14)), scenario.Targets);
            CombatFrameSnapshot warm21 = CaptureCombatSnapshot(Assert.IsType<Frame>(session.GetHistoricalFrame(21)), scenario.Targets);
            CombatFrameSnapshot warm24 = CaptureCombatSnapshot(Assert.IsType<Frame>(session.GetHistoricalFrame(24)), scenario.Targets);

            session.ClearHistoricalMaterializeCache();

            CombatFrameSnapshot cold24 = CaptureCombatSnapshot(Assert.IsType<Frame>(session.GetHistoricalFrame(24)), scenario.Targets);
            CombatFrameSnapshot cold21 = CaptureCombatSnapshot(Assert.IsType<Frame>(session.GetHistoricalFrame(21)), scenario.Targets);
            CombatFrameSnapshot cold14 = CaptureCombatSnapshot(Assert.IsType<Frame>(session.GetHistoricalFrame(14)), scenario.Targets);
            CombatFrameSnapshot cold11 = CaptureCombatSnapshot(Assert.IsType<Frame>(session.GetHistoricalFrame(11)), scenario.Targets);

            Assert.Equal(warm11, cold11);
            Assert.Equal(warm14, cold14);
            Assert.Equal(warm21, cold21);
            Assert.Equal(warm24, cold24);

            Frame tickFifteenFrame = Assert.IsType<Frame>(session.GetHistoricalFrame(15));
            long mismatchedChecksum = unchecked((long)tickFifteenFrame.CalculateChecksum() + 1);

            session.SetPlayerInput(session.LocalPlayerId, 16, new CombatInputCommand(session.LocalPlayerId, 16, FP._2, 6));
            session.VerifyFrame(15, mismatchedChecksum);

            CombatFrameSnapshot replayWarm11 = CaptureCombatSnapshot(Assert.IsType<Frame>(session.GetHistoricalFrame(11)), scenario.Targets);
            CombatFrameSnapshot replayWarm14 = CaptureCombatSnapshot(Assert.IsType<Frame>(session.GetHistoricalFrame(14)), scenario.Targets);
            CombatFrameSnapshot replayWarm21 = CaptureCombatSnapshot(Assert.IsType<Frame>(session.GetHistoricalFrame(21)), scenario.Targets);
            CombatFrameSnapshot replayWarm24 = CaptureCombatSnapshot(Assert.IsType<Frame>(session.GetHistoricalFrame(24)), scenario.Targets);

            Assert.Equal(warm11, replayWarm11);
            Assert.Equal(warm14, replayWarm14);
            Assert.NotEqual(warm21, replayWarm21);
            Assert.NotEqual(warm24, replayWarm24);

            session.ClearHistoricalMaterializeCache();

            CombatFrameSnapshot replayCold24 = CaptureCombatSnapshot(Assert.IsType<Frame>(session.GetHistoricalFrame(24)), scenario.Targets);
            CombatFrameSnapshot replayCold21 = CaptureCombatSnapshot(Assert.IsType<Frame>(session.GetHistoricalFrame(21)), scenario.Targets);

            Assert.Equal(replayWarm21, replayCold21);
            Assert.Equal(replayWarm24, replayCold24);
        }

        private static DeferredStructuralChangeSession CreateDeferredSession()
        {
            GameplayStressRegistry.EnsureRegistered();

            var session = new DeferredStructuralChangeSession(FP.One);
            session.RegisterSystem(new DeferredLifecycleMutationSystem());
            session.RegisterSystem(new DeferredLifecycleSimulationProbeSystem());
            session.RegisterSystem(new DeferredLifecycleResolveProbeSystem());
            session.RegisterSystem(new DeferredLifecycleCleanupProbeSystem());
            return session;
        }

        private static CombatStressSession CreateCombatSession()
        {
            GameplayStressRegistry.EnsureRegistered();

            var session = new CombatStressSession(FP.One);
            session.RegisterSystem(new CombatSpawnerSystem());
            session.RegisterSystem(new MovementSystem());
            session.RegisterSystem(new LifetimeSystem());
            session.RegisterSystem(new CombatProjectileDamageSystem());
            return session;
        }

        private static CombatScenario SeedCombatScenario(CombatStressSession session)
        {
            session.SeedDirectorState();
            session.SeedSpawner(
                new Position2D { X = 10, Y = 0 },
                new CombatSpawnerComponent
                {
                    Team = 1,
                    RemainingShots = 48,
                    BaseDamage = 2,
                    CooldownRemaining = FP.One,
                    Interval = FP.One,
                    BaseVelocityX = 2,
                    BaseVelocityY = FP.Zero,
                    ProjectileLifetime = 6
                });
            session.SeedSpawner(
                new Position2D { X = 9, Y = 1 },
                new CombatSpawnerComponent
                {
                    Team = 1,
                    RemainingShots = 48,
                    BaseDamage = 1,
                    CooldownRemaining = FP.One,
                    Interval = FP.One,
                    BaseVelocityX = 3,
                    BaseVelocityY = FP.Zero,
                    ProjectileLifetime = 6
                });

            return new CombatScenario(
                new[]
                {
                    session.SeedTarget(
                        new Position2D { X = 12, Y = 0 },
                        new CombatTargetComponent
                        {
                            Team = 2,
                            Health = 18,
                            GoldBounty = 7
                        }),
                    session.SeedTarget(
                        new Position2D { X = 15, Y = 1 },
                        new CombatTargetComponent
                        {
                            Team = 2,
                            Health = 24,
                            GoldBounty = 11
                        }),
                    session.SeedTarget(
                        new Position2D { X = 20, Y = 0 },
                        new CombatTargetComponent
                        {
                            Team = 2,
                            Health = 80,
                            GoldBounty = 13
                        }),
                    session.SeedTarget(
                        new Position2D { X = 22, Y = 1 },
                        new CombatTargetComponent
                        {
                            Team = 2,
                            Health = 84,
                            GoldBounty = 17
                        })
                });
        }

        private static void RunCombatTicks(
            CombatStressSession session,
            int fromTick,
            int toTick,
            Func<int, CombatInputCommand> buildInput)
        {
            for (int tick = fromTick; tick <= toTick; tick++)
            {
                CombatInputCommand input = buildInput(tick);
                session.SetPlayerInput(session.LocalPlayerId, tick, input);
                session.Update();
            }
        }

        private static void RunCombatTicksAndVerify(
            CombatStressSession session,
            int fromTick,
            int toTick,
            Func<int, CombatInputCommand> buildInput)
        {
            for (int tick = fromTick; tick <= toTick; tick++)
            {
                CombatInputCommand input = buildInput(tick);
                session.SetPlayerInput(session.LocalPlayerId, tick, input);
                session.Update();

                Frame historical = Assert.IsType<Frame>(session.GetHistoricalFrame(tick));
                long checksum = (long)historical.CalculateChecksum(ComponentSerializationMode.Checkpoint);
                session.VerifyFrame(tick, checksum);
            }
        }

        private static CombatInputCommand BuildCheckpointChainInput(int tick)
        {
            return tick switch
            {
                1 => new CombatInputCommand(0, tick, FP.Zero, 0),
                2 => new CombatInputCommand(0, tick, FP.Zero, 1),
                3 => new CombatInputCommand(0, tick, FP.One, 0),
                4 => new CombatInputCommand(0, tick, FP.Zero, 2),
                5 => new CombatInputCommand(0, tick, FP.One, 1),
                6 => new CombatInputCommand(0, tick, FP.Zero, 0),
                7 => new CombatInputCommand(0, tick, FP.One, 2),
                8 => new CombatInputCommand(0, tick, FP.Zero, 1),
                _ => new CombatInputCommand(0, tick, FP.One, 0)
            };
        }

        private static CombatInputCommand BuildHistoryInput(int tick)
        {
            FP velocityBonus = (tick & 1) == 0 ? FP.Zero : FP.One;
            int damageBonus = tick & 0x3;
            return new CombatInputCommand(0, tick, velocityBonus, damageBonus);
        }

        private static DeferredLifecycleProbeComponent ReadDeferredProbe(Frame frame)
        {
            var enumerator = frame.Query<DeferredLifecycleProbeComponent>().GetEnumerator();
            Assert.True(enumerator.MoveNext());
            return enumerator.Component;
        }

        private static int CountEntities<T>(Frame frame)
            where T : unmanaged, IComponent
        {
            int count = 0;
            var enumerator = frame.Query<T>().GetEnumerator();
            while (enumerator.MoveNext())
            {
                count++;
            }

            return count;
        }

        private static CombatFrameSnapshot CaptureCombatSnapshot(Frame frame, EntityRef[] targets)
        {
            CombatDirectorState director = frame.GetGlobal<CombatDirectorState>();
            long targetSignature = 17;
            for (int i = 0; i < targets.Length; i++)
            {
                bool isValid = frame.IsValid(targets[i]);
                targetSignature = unchecked(targetSignature * 31 + (isValid ? 1 : 0));
                targetSignature = unchecked(targetSignature * 31 + (isValid ? frame.Get<CombatTargetComponent>(targets[i]).Health : -1));
            }

            return new CombatFrameSnapshot(
                frame.Tick,
                frame.CalculateChecksum(),
                CountEntities<ProjectileTag>(frame),
                CountEntities<CombatLootComponent>(frame),
                director.TotalHits,
                director.DefeatedTargets,
                director.TotalGold,
                director.LastResolvedTick,
                targetSignature);
        }

        private readonly record struct CombatScenario(EntityRef[] Targets);

        private readonly record struct CombatFrameSnapshot(
            int Tick,
            ulong Checksum,
            int ProjectileCount,
            int LootCount,
            int TotalHits,
            int DefeatedTargets,
            int TotalGold,
            int LastResolvedTick,
            long TargetSignature);

        private abstract class MirroredGameplaySessionBase : MinimalPredictionSession
        {
            protected MirroredGameplaySessionBase(FP deltaTime)
                : base(deltaTime)
            {
            }

            protected EntityRef MirrorEntity(Action<Frame, EntityRef> configure)
            {
                if (VerifiedFrame == null || PredictedFrame == null)
                {
                    throw new InvalidOperationException("Session must be started before seeding mirrored gameplay state.");
                }

                EntityRef verified = VerifiedFrame.CreateEntity();
                configure(VerifiedFrame, verified);

                EntityRef predicted = PredictedFrame.CreateEntity();
                configure(PredictedFrame, predicted);

                Assert.Equal(verified, predicted);
                return verified;
            }

            protected void MirrorGlobal<T>(in T component)
                where T : unmanaged, IComponent
            {
                if (VerifiedFrame == null || PredictedFrame == null)
                {
                    throw new InvalidOperationException("Session must be started before seeding mirrored global state.");
                }

                VerifiedFrame.SetGlobal(component);
                PredictedFrame.SetGlobal(component);
            }
        }

        private sealed class CombatStressSession : MirroredGameplaySessionBase
        {
            public CombatStressSession(FP deltaTime)
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

            public void ClearHistoricalMaterializeCache()
            {
                InvalidateHistoricalMaterializeCache();
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

        private sealed class DeferredStructuralChangeSession : MirroredGameplaySessionBase
        {
            public DeferredStructuralChangeSession(FP deltaTime)
                : base(deltaTime)
            {
            }

            public void SeedDeferredProbe()
            {
                MirrorEntity(
                    (frame, entity) =>
                    {
                        frame.Add(entity, new DeferredLifecycleController { PendingOperations = 1 });
                        frame.Add(entity, new DeferredLifecycleProbeComponent());
                    });
            }

            public void SeedActivationCandidate()
            {
                MirrorEntity(
                    (frame, entity) =>
                    {
                        frame.Add(entity, new DeferredLifecycleSubjectTag());
                        frame.Add(entity, new DeferredActivationCandidateTag());
                    });
            }

            public void SeedRemovalCandidate()
            {
                MirrorEntity(
                    (frame, entity) =>
                    {
                        frame.Add(entity, new DeferredLifecycleSubjectTag());
                        frame.Add(entity, new DeferredRemovalCandidateTag());
                        frame.Add(entity, new DeferredTransientTag());
                    });
            }

            public void SeedDestroyCandidate()
            {
                MirrorEntity(
                    (frame, entity) =>
                    {
                        frame.Add(entity, new DeferredLifecycleSubjectTag());
                        frame.Add(entity, new DeferredDestroyCandidateTag());
                    });
            }

            protected override void ApplyInputSet(Frame frame, in SessionInputSet inputSet)
            {
            }
        }

        private struct DeferredLifecycleController : IComponent
        {
            public int PendingOperations;
        }

        private struct DeferredLifecycleProbeComponent : IComponent
        {
            public int SimulationSpawnedVisible;
            public int ResolveSpawnedVisible;
            public int CleanupSpawnedVisible;
            public int SimulationActivatedVisible;
            public int ResolveActivatedVisible;
            public int CleanupActivatedVisible;
            public int SimulationTransientVisible;
            public int ResolveTransientVisible;
            public int CleanupTransientVisible;
            public int SimulationDestroyVisible;
            public int ResolveDestroyVisible;
            public int CleanupDestroyVisible;
        }

        private struct DeferredActivationCandidateTag : IComponent
        {
        }

        private struct DeferredLifecycleSubjectTag : IComponent
        {
        }

        private struct DeferredActivatedTag : IComponent
        {
        }

        private struct DeferredRemovalCandidateTag : IComponent
        {
        }

        private struct DeferredTransientTag : IComponent
        {
        }

        private struct DeferredDestroyCandidateTag : IComponent
        {
        }

        private struct DeferredSpawnedTag : IComponent
        {
        }

        private sealed class DeferredLifecycleMutationSystem : ISystem
        {
            public SystemPhase Phase => SystemPhase.PreSimulation;

            public SystemAuthoringContract Contract => new(
                SystemFrameAccess.ReadWrite,
                SystemGlobalAccess.None,
                SystemStructuralChangeAccess.Deferred);

            public void OnInit(Frame frame)
            {
            }

            public void OnUpdate(Frame frame, FP deltaTime)
            {
                bool shouldExecute = false;
                var controllerEnumerator = frame.Query<DeferredLifecycleController>().GetEnumerator();
                while (controllerEnumerator.MoveNext())
                {
                    if (controllerEnumerator.Component.PendingOperations <= 0)
                    {
                        continue;
                    }

                    controllerEnumerator.Component.PendingOperations--;
                    shouldExecute = true;
                }

                if (!shouldExecute)
                {
                    return;
                }

                EntityRef spawned = frame.CreateEntity();
                frame.Add(spawned, new DeferredSpawnedTag());

                var activationEnumerator = frame.Query<DeferredActivationCandidateTag, DeferredLifecycleSubjectTag>().GetEnumerator();
                while (activationEnumerator.MoveNext())
                {
                    frame.Add(activationEnumerator.Entity, new DeferredActivatedTag());
                }

                var removalEnumerator = frame.Query<DeferredRemovalCandidateTag, DeferredLifecycleSubjectTag>().GetEnumerator();
                while (removalEnumerator.MoveNext())
                {
                    frame.Remove<DeferredTransientTag>(removalEnumerator.Entity);
                }

                var destroyEnumerator = frame.Query<DeferredDestroyCandidateTag, DeferredLifecycleSubjectTag>().GetEnumerator();
                while (destroyEnumerator.MoveNext())
                {
                    frame.DestroyEntity(destroyEnumerator.Entity);
                }
            }

            public void OnDestroy(Frame frame)
            {
            }
        }

        private sealed class DeferredLifecycleSimulationProbeSystem : ISystem
        {
            public void OnInit(Frame frame)
            {
            }

            public void OnUpdate(Frame frame, FP deltaTime)
            {
                var probeEnumerator = frame.Query<DeferredLifecycleProbeComponent>().GetEnumerator();
                while (probeEnumerator.MoveNext())
                {
                    probeEnumerator.Component.SimulationSpawnedVisible = CountEntities<DeferredSpawnedTag>(frame);
                    probeEnumerator.Component.SimulationActivatedVisible = CountEntities<DeferredActivatedTag>(frame);
                    probeEnumerator.Component.SimulationTransientVisible = CountEntities<DeferredTransientTag>(frame);
                    probeEnumerator.Component.SimulationDestroyVisible = CountEntities<DeferredDestroyCandidateTag>(frame);
                }
            }

            public void OnDestroy(Frame frame)
            {
            }
        }

        private sealed class DeferredLifecycleResolveProbeSystem : ISystem
        {
            public SystemPhase Phase => SystemPhase.Resolve;

            public void OnInit(Frame frame)
            {
            }

            public void OnUpdate(Frame frame, FP deltaTime)
            {
                var probeEnumerator = frame.Query<DeferredLifecycleProbeComponent>().GetEnumerator();
                while (probeEnumerator.MoveNext())
                {
                    probeEnumerator.Component.ResolveSpawnedVisible = CountEntities<DeferredSpawnedTag>(frame);
                    probeEnumerator.Component.ResolveActivatedVisible = CountEntities<DeferredActivatedTag>(frame);
                    probeEnumerator.Component.ResolveTransientVisible = CountEntities<DeferredTransientTag>(frame);
                    probeEnumerator.Component.ResolveDestroyVisible = CountEntities<DeferredDestroyCandidateTag>(frame);
                }
            }

            public void OnDestroy(Frame frame)
            {
            }
        }

        private sealed class DeferredLifecycleCleanupProbeSystem : ISystem
        {
            public SystemPhase Phase => SystemPhase.Cleanup;

            public void OnInit(Frame frame)
            {
            }

            public void OnUpdate(Frame frame, FP deltaTime)
            {
                var probeEnumerator = frame.Query<DeferredLifecycleProbeComponent>().GetEnumerator();
                while (probeEnumerator.MoveNext())
                {
                    probeEnumerator.Component.CleanupSpawnedVisible = CountEntities<DeferredSpawnedTag>(frame);
                    probeEnumerator.Component.CleanupActivatedVisible = CountEntities<DeferredActivatedTag>(frame);
                    probeEnumerator.Component.CleanupTransientVisible = CountEntities<DeferredTransientTag>(frame);
                    probeEnumerator.Component.CleanupDestroyVisible = CountEntities<DeferredDestroyCandidateTag>(frame);
                }
            }

            public void OnDestroy(Frame frame)
            {
            }
        }

        private static class GameplayStressRegistry
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

                    GameplayValidationRegistry.EnsureRegistered();
                    ComponentRegistry.Register<DeferredLifecycleController>();
                    ComponentRegistry.Register<DeferredLifecycleProbeComponent>();
                    ComponentRegistry.Register<DeferredActivationCandidateTag>();
                    ComponentRegistry.Register<DeferredLifecycleSubjectTag>();
                    ComponentRegistry.Register<DeferredActivatedTag>();
                    ComponentRegistry.Register<DeferredRemovalCandidateTag>();
                    ComponentRegistry.Register<DeferredTransientTag>();
                    ComponentRegistry.Register<DeferredDestroyCandidateTag>();
                    ComponentRegistry.Register<DeferredSpawnedTag>();

                    _registered = true;
                }
            }
        }
    }
}
