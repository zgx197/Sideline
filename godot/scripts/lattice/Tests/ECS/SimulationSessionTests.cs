// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using Lattice.Core;
using Lattice.ECS.Core;
using Lattice.ECS.Framework;
using Lattice.ECS.Session;
using Lattice.Math;
using Xunit;

namespace Lattice.Tests.ECS
{
    public sealed class SimulationSessionTests
    {
        static SimulationSessionTests()
        {
            ComponentRegistry.EnsureRegistered<Position>();
            ComponentRegistry.EnsureRegistered<Velocity>();
        }

        [Fact]
        public void FrameSnapshot_RestoreRecoversChecksumAndEntities()
        {
            using var frame = new Frame(16);

            EntityRef mover = frame.CreateEntity();
            frame.Add(mover, new Position { Value = FP.One });
            frame.Add(mover, new Velocity { Value = FP._2 });

            EntityRef target = frame.CreateEntity();
            frame.Add(target, new Position { Value = FP._10 });

            FrameSnapshot snapshot = frame.CreateSnapshot();
            ulong checksumBefore = frame.CalculateChecksum();

            frame.Get<Position>(mover).Value = FP._10 - FP.One;
            frame.Remove<Velocity>(mover);
            frame.DestroyEntity(target);
            frame.CreateEntity();

            frame.RestoreFromSnapshot(snapshot);

            Assert.Equal(checksumBefore, frame.CalculateChecksum());
            Assert.Equal(snapshot.Checksum, frame.CalculateChecksum());
            Assert.True(frame.IsValid(mover));
            Assert.True(frame.IsValid(target));
            Assert.True(frame.Has<Velocity>(mover));
            Assert.Equal(FP.One, frame.Get<Position>(mover).Value);
            Assert.Equal(FP._10, frame.Get<Position>(target).Value);

            using Frame clone = frame.Clone();
            Assert.Equal(frame.CalculateChecksum(), clone.CalculateChecksum());
            Assert.Equal(frame.Get<Position>(mover).Value, clone.Get<Position>(mover).Value);
        }

        [Fact]
        public void SimulationSession_AdvancesVerifiedAndPredictedFrames()
        {
            var holder = new EntityHolder();
            using var session = CreateSession(holder);

            session.SetVerifiedInput(1, new MoveInput { Velocity = FP.One });
            session.SetVerifiedInput(2, new MoveInput { Velocity = FP._2 });
            session.AdvanceVerifiedTo(2);

            Assert.Equal(FP._3, session.VerifiedFrame!.Get<Position>(holder.Entity).Value);

            session.SetPredictedInput(3, new MoveInput { Velocity = FP._3 });
            session.SetPredictedInput(4, new MoveInput { Velocity = FP._4 });
            session.AdvancePredictedTo(4);

            Assert.Equal(FP._10, session.PredictedFrame!.Get<Position>(holder.Entity).Value);
        }

        [Fact]
        public void SimulationSession_RebuildsPredictionFromLatestVerifiedFrame()
        {
            var holder = new EntityHolder();
            using var session = CreateSession(holder);

            session.SetPredictedInput(1, new MoveInput { Velocity = FP.One });
            session.SetPredictedInput(2, new MoveInput { Velocity = FP.One });
            session.SetPredictedInput(3, new MoveInput { Velocity = FP.One });
            session.AdvancePredictedTo(3);

            Assert.Equal(FP._3, session.PredictedFrame!.Get<Position>(holder.Entity).Value);

            session.SetVerifiedInput(1, new MoveInput { Velocity = FP.One });
            session.SetVerifiedInput(2, new MoveInput { Velocity = FP._5 });
            session.AdvanceVerifiedTo(2);
            session.AdvancePredictedTo(3);

            Assert.Equal(FP._5 + FP.One, session.VerifiedFrame!.Get<Position>(holder.Entity).Value);
            Assert.Equal(FP._5 + FP._2, session.PredictedFrame!.Get<Position>(holder.Entity).Value);
        }

        [Fact]
        public void SimulationSession_SameInputsProduceSameChecksums()
        {
            ulong verified1;
            ulong predicted1;
            RunSession(out verified1, out predicted1);

            ulong verified2;
            ulong predicted2;
            RunSession(out verified2, out predicted2);

            Assert.Equal(verified1, verified2);
            Assert.Equal(predicted1, predicted2);
        }

        private static SimulationSession<MoveInput> CreateSession(EntityHolder holder)
        {
            var session = new SimulationSession<MoveInput>(
                new SimulationSessionOptions
                {
                    DeltaTime = FP.One,
                    MaxEntities = 16,
                    InputCapacity = 32,
                    CommandBufferCapacity = 256
                },
                (frame, input) =>
                {
                    frame.Get<Velocity>(holder.Entity).Value = input.Velocity;
                });

            session.Add(new MovementSystem());
            session.Start(frame =>
            {
                holder.Entity = frame.CreateEntity();
                frame.Add(holder.Entity, new Position { Value = FP.Zero });
                frame.Add(holder.Entity, new Velocity { Value = FP.Zero });
            });

            return session;
        }

        private static void RunSession(out ulong verifiedChecksum, out ulong predictedChecksum)
        {
            var holder = new EntityHolder();
            using var session = CreateSession(holder);

            session.SetVerifiedInput(1, new MoveInput { Velocity = FP.One });
            session.SetVerifiedInput(2, new MoveInput { Velocity = FP._2 });
            session.SetVerifiedInput(3, new MoveInput { Velocity = FP._3 });
            session.AdvanceVerifiedTo(3);

            session.SetPredictedInput(4, new MoveInput { Velocity = FP._4 });
            session.SetPredictedInput(5, new MoveInput { Velocity = FP._5 });
            session.AdvancePredictedTo(5);

            verifiedChecksum = session.CalculateVerifiedChecksum();
            predictedChecksum = session.CalculatePredictedChecksum();
        }

        private sealed class EntityHolder
        {
            public EntityRef Entity;
        }

        private struct Position
        {
            public FP Value;
        }

        private struct Velocity
        {
            public FP Value;
        }

        private struct MoveInput
        {
            public FP Velocity;
        }

        private sealed class MovementSystem : SystemBase
        {
            public override void OnUpdate(Frame frame, FP deltaTime)
            {
                var filter = frame.Filter<Position, Velocity>();
                var enumerator = filter.GetEnumerator();

                while (enumerator.MoveNext())
                {
                    enumerator.Component1.Value += enumerator.Component2.Value * deltaTime;
                }
            }
        }
    }
}
