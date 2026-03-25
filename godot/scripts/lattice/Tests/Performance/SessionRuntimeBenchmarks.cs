// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Lattice.Core;
using Lattice.ECS.Core;
using Lattice.ECS.Framework;
using Lattice.ECS.Serialization;
using Lattice.ECS.Session;
using Lattice.Math;

namespace Lattice.Tests.Performance
{
    /// <summary>
    /// Session 运行时基准定义。
    /// 目标是为 Update / Checkpoint / Restore / Rollback / Historical Rebuild / Checksum 提供可重复的基线。
    /// </summary>
    [MemoryDiagnoser]
    [SimpleJob(RuntimeMoniker.Net80, launchCount: 1, warmupCount: 2, iterationCount: 5, invocationCount: 1)]
    public unsafe class SessionRuntimeBenchmarks
    {
        private const int BaselineTick = 48;
        private const int InputSeedTickCount = 96;
        private const int UpdateBatchSize = 64;
        private const int CheckpointBatchSize = 128;
        private const int RestoreCheckpointBatchSize = 32;
        private const int RollbackBatchSize = 4;
        private const int ChecksumBatchSize = 256;
        private const int HistoricalRebuildBatchSize = 32;
        private const int HistoricalColdMaterializeBatchSize = 32;
        private const int HistoricalSequentialReadBatchSize = 32;
        private const int HistoricalCrossAnchorReadBatchSize = 32;
        private const int RollbackTick = 24;
        private const int HistoricalRebuildTick = 13;
        private const int HistoricalSequentialTickStart = 13;
        private const int HistoricalCrossAnchorTickStart = 11;

        private static readonly object RegistrationSync = new();
        private static bool _componentsRegistered;

        private BenchmarkSession? _session;
        private SessionCheckpoint? _baselineCheckpoint;
        private long _mismatchedRollbackChecksum;

        [Params(64, 256, 1024)]
        public int EntityCount { get; set; }

        [Params(BenchmarkPayloadProfile.RawDense, BenchmarkPayloadProfile.MixedSerialized)]
        public BenchmarkPayloadProfile PayloadProfile { get; set; }

        [GlobalSetup]
        public void GlobalSetup()
        {
            EnsureComponentsRegistered();

            _session = new BenchmarkSession(FP.One, PayloadProfile);
            _session.RegisterSystem(new BenchmarkBootstrapSystem(EntityCount, PayloadProfile));
            _session.RegisterSystem(new BenchmarkSimulationSystem(PayloadProfile));
            _session.Start();

            SeedInputs(_session, InputSeedTickCount);
            AdvanceSession(_session, BaselineTick);

            _baselineCheckpoint = _session.CreateCheckpoint();

            Frame rollbackFrame = _session.GetHistoricalFrame(RollbackTick)
                ?? throw new InvalidOperationException($"Historical frame {RollbackTick} is missing.");
            _mismatchedRollbackChecksum = unchecked((long)rollbackFrame.CalculateChecksum() + 1);
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            _session?.Dispose();
            _session = null;
            _baselineCheckpoint = null;
        }

        [IterationSetup]
        public void IterationSetup()
        {
            GetSession().RestoreFromCheckpoint(GetBaselineCheckpoint());
        }

        [Benchmark(Baseline = true, Description = "Update x64", OperationsPerInvoke = UpdateBatchSize)]
        public int Session_Update_64Ticks()
        {
            BenchmarkSession session = GetSession();

            for (int i = 0; i < UpdateBatchSize; i++)
            {
                session.Update();
            }

            return session.CurrentTick;
        }

        [Benchmark(Description = "Create Checkpoint x128", OperationsPerInvoke = CheckpointBatchSize)]
        public int Session_CreateCheckpoint()
        {
            BenchmarkSession session = GetSession();
            int tickSum = 0;

            for (int i = 0; i < CheckpointBatchSize; i++)
            {
                SessionCheckpoint checkpoint = session.CreateCheckpoint();
                tickSum += checkpoint.Tick;
            }

            return tickSum;
        }

        [Benchmark(Description = "Restore Checkpoint x32", OperationsPerInvoke = RestoreCheckpointBatchSize)]
        public int Session_RestoreCheckpoint()
        {
            BenchmarkSession session = GetSession();
            SessionCheckpoint checkpoint = GetBaselineCheckpoint();
            int tickSum = 0;

            for (int i = 0; i < RestoreCheckpointBatchSize; i++)
            {
                session.RestoreFromCheckpoint(checkpoint);
                tickSum += session.CurrentTick;
            }

            return tickSum;
        }

        [Benchmark(Description = "Rollback + Resimulate x4", OperationsPerInvoke = RollbackBatchSize)]
        public int Session_Rollback_Resimulate()
        {
            BenchmarkSession session = GetSession();
            int tickSum = 0;

            for (int i = 0; i < RollbackBatchSize; i++)
            {
                session.VerifyFrame(RollbackTick, _mismatchedRollbackChecksum);
                tickSum += session.CurrentTick;
            }

            return tickSum;
        }

        [Benchmark(Description = "Frame Checksum x256", OperationsPerInvoke = ChecksumBatchSize)]
        public ulong Session_Frame_Checksum()
        {
            Frame frame = GetSession().PredictedFrame!;
            ulong checksum = 0;

            for (int i = 0; i < ChecksumBatchSize; i++)
            {
                checksum ^= frame.CalculateChecksum();
            }

            return checksum;
        }

        [Benchmark(Description = "Historical Rebuild From Sampled Snapshot x32", OperationsPerInvoke = HistoricalRebuildBatchSize)]
        public int Session_Historical_Rebuild_FromSampledSnapshot()
        {
            BenchmarkSession session = GetSession();
            int tickSum = 0;

            for (int i = 0; i < HistoricalRebuildBatchSize; i++)
            {
                Frame historicalFrame = session.GetHistoricalFrame(HistoricalRebuildTick)
                    ?? throw new InvalidOperationException($"Historical frame {HistoricalRebuildTick} is missing.");
                tickSum += historicalFrame.Tick;
            }

            return tickSum;
        }

        [Benchmark(Description = "Historical Cold Materialize x32", OperationsPerInvoke = HistoricalColdMaterializeBatchSize)]
        public int Session_Historical_ColdMaterialize_FromSampledSnapshot()
        {
            BenchmarkSession session = GetSession();
            int tickSum = 0;

            for (int i = 0; i < HistoricalColdMaterializeBatchSize; i++)
            {
                session.ClearHistoricalMaterializeCache();

                Frame historicalFrame = session.GetHistoricalFrame(HistoricalRebuildTick)
                    ?? throw new InvalidOperationException($"Historical frame {HistoricalRebuildTick} is missing.");
                tickSum += historicalFrame.Tick;
            }

            return tickSum;
        }

        [Benchmark(Description = "Historical Sequential Read 13-15 x32", OperationsPerInvoke = HistoricalSequentialReadBatchSize)]
        public int Session_Historical_SequentialRead_13_15()
        {
            BenchmarkSession session = GetSession();
            int tickSum = 0;

            for (int i = 0; i < HistoricalSequentialReadBatchSize; i++)
            {
                session.ClearHistoricalMaterializeCache();

                for (int tick = HistoricalSequentialTickStart; tick <= HistoricalSequentialTickStart + 2; tick++)
                {
                    Frame historicalFrame = session.GetHistoricalFrame(tick)
                        ?? throw new InvalidOperationException($"Historical frame {tick} is missing.");
                    tickSum += historicalFrame.Tick;
                }
            }

            return tickSum;
        }

        [Benchmark(Description = "Historical Cross-Anchor Read 11-14 x32", OperationsPerInvoke = HistoricalCrossAnchorReadBatchSize)]
        public int Session_Historical_CrossAnchorRead_11_14()
        {
            BenchmarkSession session = GetSession();
            int tickSum = 0;

            for (int i = 0; i < HistoricalCrossAnchorReadBatchSize; i++)
            {
                session.ClearHistoricalMaterializeCache();

                for (int tick = HistoricalCrossAnchorTickStart; tick <= HistoricalCrossAnchorTickStart + 3; tick++)
                {
                    Frame historicalFrame = session.GetHistoricalFrame(tick)
                        ?? throw new InvalidOperationException($"Historical frame {tick} is missing.");
                    tickSum += historicalFrame.Tick;
                }
            }

            return tickSum;
        }

        private static void EnsureComponentsRegistered()
        {
            if (_componentsRegistered)
            {
                return;
            }

            lock (RegistrationSync)
            {
                if (_componentsRegistered)
                {
                    return;
                }

                var builder = ComponentRegistry.CreateBuilder();
                builder.Add<BenchmarkCounterComponent>();
                builder.Add<BenchmarkInputSumComponent>();
                builder.Add<BenchmarkRawPayloadComponent>();
                builder.Add<BenchmarkSerializedPayloadComponent>(SerializeBenchmarkSerializedPayloadComponent);
                builder.Finish();

                _componentsRegistered = true;
            }
        }

        private static void SeedInputs(BenchmarkSession session, int tickCount)
        {
            for (int tick = 1; tick <= tickCount; tick++)
            {
                session.SetPlayerInput(0, tick, new BenchmarkInputCommand(0, tick, tick));
            }
        }

        private static void AdvanceSession(BenchmarkSession session, int tickCount)
        {
            for (int i = 0; i < tickCount; i++)
            {
                session.Update();
            }
        }

        private BenchmarkSession GetSession()
        {
            return _session ?? throw new InvalidOperationException("Benchmark session has not been initialized.");
        }

        private SessionCheckpoint GetBaselineCheckpoint()
        {
            return _baselineCheckpoint ?? throw new InvalidOperationException("Benchmark baseline checkpoint has not been initialized.");
        }

        private static unsafe void SerializeBenchmarkSerializedPayloadComponent(void* component, IFrameSerializer serializer)
        {
            var typed = (BenchmarkSerializedPayloadComponent*)component;
            serializer.Serialize(ref typed->A);
            serializer.Serialize(ref typed->B);
            serializer.Serialize(ref typed->C);
            serializer.Serialize(ref typed->D);
        }

        public enum BenchmarkPayloadProfile
        {
            RawDense,
            MixedSerialized
        }

        private struct BenchmarkCounterComponent : IComponent
        {
            public int Value;
        }

        private struct BenchmarkInputSumComponent : IComponent
        {
            public int Value;
        }

        private struct BenchmarkRawPayloadComponent : IComponent
        {
            public int X;
            public int Y;
            public int Z;
            public int W;
        }

        private struct BenchmarkSerializedPayloadComponent : IComponent
        {
            public int A;
            public int B;
            public int C;
            public int D;
        }

        private sealed class BenchmarkBootstrapSystem : ISystem
        {
            private readonly int _entityCount;
            private readonly BenchmarkPayloadProfile _payloadProfile;

            public BenchmarkBootstrapSystem(int entityCount, BenchmarkPayloadProfile payloadProfile)
            {
                _entityCount = entityCount;
                _payloadProfile = payloadProfile;
            }

            public void OnInit(Frame frame)
            {
                for (int i = 0; i < _entityCount; i++)
                {
                    EntityRef entity = frame.CreateEntity();
                    frame.Add(entity, new BenchmarkCounterComponent { Value = i });
                    frame.Add(entity, new BenchmarkInputSumComponent { Value = i & 7 });
                    frame.Add(entity, new BenchmarkRawPayloadComponent
                    {
                        X = i,
                        Y = i + 1,
                        Z = i + 2,
                        W = i + 3
                    });

                    if (_payloadProfile == BenchmarkPayloadProfile.MixedSerialized)
                    {
                        frame.Add(entity, new BenchmarkSerializedPayloadComponent
                        {
                            A = i,
                            B = i * 2,
                            C = i * 3,
                            D = i * 4
                        });
                    }
                }
            }

            public void OnUpdate(Frame frame, FP deltaTime)
            {
            }

            public void OnDestroy(Frame frame)
            {
            }
        }

        private sealed class BenchmarkSimulationSystem : ISystem
        {
            private readonly BenchmarkPayloadProfile _payloadProfile;

            public BenchmarkSimulationSystem(BenchmarkPayloadProfile payloadProfile)
            {
                _payloadProfile = payloadProfile;
            }

            public void OnInit(Frame frame)
            {
            }

            public void OnUpdate(Frame frame, FP deltaTime)
            {
                var counterEnumerator = frame.Query<BenchmarkCounterComponent>().GetEnumerator();
                while (counterEnumerator.MoveNext())
                {
                    counterEnumerator.Component.Value += 1;
                }

                var rawEnumerator = frame.Query<BenchmarkRawPayloadComponent>().GetEnumerator();
                while (rawEnumerator.MoveNext())
                {
                    rawEnumerator.Component.X += 1;
                    rawEnumerator.Component.Y += rawEnumerator.Component.X;
                    rawEnumerator.Component.Z ^= rawEnumerator.Component.Y;
                    rawEnumerator.Component.W -= 1;
                }

                if (_payloadProfile != BenchmarkPayloadProfile.MixedSerialized)
                {
                    return;
                }

                var serializedEnumerator = frame.Query<BenchmarkSerializedPayloadComponent>().GetEnumerator();
                while (serializedEnumerator.MoveNext())
                {
                    serializedEnumerator.Component.A += 1;
                    serializedEnumerator.Component.B += serializedEnumerator.Component.A;
                    serializedEnumerator.Component.C ^= serializedEnumerator.Component.B;
                    serializedEnumerator.Component.D -= 1;
                }
            }

            public void OnDestroy(Frame frame)
            {
            }
        }

        private sealed class BenchmarkSession : Session
        {
            private readonly BenchmarkPayloadProfile _payloadProfile;

            public BenchmarkSession(FP deltaTime, BenchmarkPayloadProfile payloadProfile)
                : base(deltaTime)
            {
                _payloadProfile = payloadProfile;
            }

            protected override void ApplyInputs(Frame frame)
            {
                if (GetPlayerInput(LocalPlayerId, frame.Tick) is not BenchmarkInputCommand input)
                {
                    return;
                }

                var inputEnumerator = frame.Query<BenchmarkInputSumComponent>().GetEnumerator();
                while (inputEnumerator.MoveNext())
                {
                    inputEnumerator.Component.Value += input.Value;
                }

                if (_payloadProfile != BenchmarkPayloadProfile.MixedSerialized)
                {
                    return;
                }

                var serializedEnumerator = frame.Query<BenchmarkSerializedPayloadComponent>().GetEnumerator();
                while (serializedEnumerator.MoveNext())
                {
                    serializedEnumerator.Component.D += input.Value;
                }
            }

            public void ClearHistoricalMaterializeCache()
            {
                InvalidateHistoricalMaterializeCache();
            }
        }

        private sealed class BenchmarkInputCommand : IInputCommand
        {
            public BenchmarkInputCommand(int playerId, int tick, int value)
            {
                PlayerId = playerId;
                Tick = tick;
                Value = value;
            }

            public int PlayerId { get; }

            public int Tick { get; }

            public int Value { get; }

            public byte[] Serialize()
            {
                return BitConverter.GetBytes(Value);
            }

            public void Deserialize(byte[] data)
            {
            }
        }
    }
}
