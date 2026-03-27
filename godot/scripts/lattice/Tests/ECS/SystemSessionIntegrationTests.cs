using System;
using Lattice.Core;
using Lattice.ECS.Core;
using Lattice.ECS.Framework.Systems;
using Lattice.ECS.Session;
using Lattice.Math;
using Xunit;

namespace Lattice.Tests.ECS
{
    public class SystemSessionIntegrationTests
    {
        [Fact]
        public void MovementAndLifetimeSystems_RunThroughCheckpointRestoreLoop()
        {
            using var session = CreateSession();
            session.Start();

            EntityRef actor = session.SpawnActor(
                new Position2D { X = FP.Zero, Y = FP.Zero },
                new Velocity2D { X = FP.Zero, Y = FP.Zero },
                new Lifetime { Remaining = FP.FromRaw(FP.Raw._4) });

            session.SetPlayerInput(session.LocalPlayerId, 1, new MoveInputCommand(session.LocalPlayerId, 1, FP._1, FP.Zero));
            session.SetPlayerInput(session.LocalPlayerId, 2, new MoveInputCommand(session.LocalPlayerId, 2, FP._2, FP.Zero));
            session.SetPlayerInput(session.LocalPlayerId, 3, new MoveInputCommand(session.LocalPlayerId, 3, FP._3, FP.Zero));

            session.Update();
            AssertActorState(session.PredictedFrame!, actor, expectedX: FP._1, expectedLifetime: FP._3);

            session.Update();
            AssertActorState(session.PredictedFrame!, actor, expectedX: FP._3, expectedLifetime: FP._2);

            SessionCheckpoint checkpoint = session.CreateCheckpoint();

            session.Update();
            AssertActorState(session.PredictedFrame!, actor, expectedX: FP.FromRaw(FP.Raw._6), expectedLifetime: FP._1);

            session.RestoreFromCheckpoint(checkpoint);
            AssertActorState(session.PredictedFrame!, actor, expectedX: FP._3, expectedLifetime: FP._2);

            session.Update();
            AssertActorState(session.PredictedFrame!, actor, expectedX: FP.FromRaw(FP.Raw._6), expectedLifetime: FP._1);
        }

        [Fact]
        public void MovementAndLifetimeSystems_RollbackResimulatesAndExpiresEntity()
        {
            using var session = CreateSession();
            session.Start();

            EntityRef actor = session.SpawnActor(
                new Position2D { X = FP.Zero, Y = FP.Zero },
                new Velocity2D { X = FP.Zero, Y = FP.Zero },
                new Lifetime { Remaining = FP.FromRaw(FP.Raw._4) });

            session.SetPlayerInput(session.LocalPlayerId, 1, new MoveInputCommand(session.LocalPlayerId, 1, FP._1, FP.Zero));
            session.SetPlayerInput(session.LocalPlayerId, 2, new MoveInputCommand(session.LocalPlayerId, 2, FP._2, FP.Zero));
            session.SetPlayerInput(session.LocalPlayerId, 3, new MoveInputCommand(session.LocalPlayerId, 3, FP._3, FP.Zero));
            session.SetPlayerInput(session.LocalPlayerId, 4, new MoveInputCommand(session.LocalPlayerId, 4, FP.Zero, FP.Zero));

            session.Update();
            session.Update();
            session.Update();

            session.SetPlayerInput(session.LocalPlayerId, 2, new MoveInputCommand(session.LocalPlayerId, 2, FP._5, FP.Zero));

            Frame tickOneFrame = Assert.IsType<Frame>(session.GetHistoricalFrame(1));
            long mismatchedChecksum = unchecked((long)tickOneFrame.CalculateChecksum() + 1);

            session.VerifyFrame(1, mismatchedChecksum);

            Assert.Equal(3, session.CurrentTick);
            AssertActorState(session.PredictedFrame!, actor, expectedX: FP.FromRaw(FP.Raw._9), expectedLifetime: FP._1);

            session.Update();

            Assert.False(session.PredictedFrame!.IsValid(actor));
            Assert.Null(session.GetHistoricalFrame(99));
        }

        private static IntegrationSession CreateSession()
        {
            var session = new IntegrationSession(FP.One);
            session.RegisterSystem(new MovementSystem());
            session.RegisterSystem(new LifetimeSystem());
            return session;
        }

        private static void AssertActorState(Frame frame, EntityRef actor, FP expectedX, FP expectedLifetime)
        {
            Assert.True(frame.IsValid(actor));

            ref Position2D position = ref frame.Get<Position2D>(actor);
            ref Lifetime lifetime = ref frame.Get<Lifetime>(actor);

            Assert.Equal(expectedX, position.X);
            Assert.Equal(FP.Zero, position.Y);
            Assert.Equal(expectedLifetime, lifetime.Remaining);
        }

        private sealed class IntegrationSession : MinimalPredictionSession
        {
            public IntegrationSession(FP deltaTime)
                : base(deltaTime)
            {
            }

            public EntityRef SpawnActor(Position2D position, Velocity2D velocity, Lifetime lifetime)
            {
                if (VerifiedFrame == null || PredictedFrame == null)
                {
                    throw new InvalidOperationException("Session must be started before spawning actors.");
                }

                EntityRef verifiedEntity = VerifiedFrame.CreateEntity();
                VerifiedFrame.Add(verifiedEntity, position);
                VerifiedFrame.Add(verifiedEntity, velocity);
                VerifiedFrame.Add(verifiedEntity, lifetime);

                EntityRef predictedEntity = PredictedFrame.CreateEntity();
                PredictedFrame.Add(predictedEntity, position);
                PredictedFrame.Add(predictedEntity, velocity);
                PredictedFrame.Add(predictedEntity, lifetime);

                Assert.Equal(verifiedEntity, predictedEntity);
                return verifiedEntity;
            }

            protected override void ApplyInputSet(Frame frame, in SessionInputSet inputSet)
            {
                if (!inputSet.TryGetPlayerInput(LocalPlayerId, out MoveInputCommand? input))
                {
                    return;
                }

                Assert.NotNull(input);

                var enumerator = frame.Query<Velocity2D>().GetEnumerator();
                while (enumerator.MoveNext())
                {
                    enumerator.Component.X = input.VelocityX;
                    enumerator.Component.Y = input.VelocityY;
                }
            }
        }

        private sealed class MoveInputCommand : IPlayerInput
        {
            public MoveInputCommand()
            {
            }

            public MoveInputCommand(int playerId, int tick, FP velocityX, FP velocityY)
            {
                PlayerId = playerId;
                Tick = tick;
                VelocityX = velocityX;
                VelocityY = velocityY;
            }

            public int PlayerId { get; private set; }

            public int Tick { get; private set; }

            public FP VelocityX { get; private set; }

            public FP VelocityY { get; private set; }
        }
    }
}
