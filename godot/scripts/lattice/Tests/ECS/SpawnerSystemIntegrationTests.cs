using System;
using Lattice.Core;
using Lattice.ECS.Core;
using Lattice.ECS.Framework.Systems;
using Lattice.ECS.Session;
using Lattice.Math;
using Xunit;

namespace Lattice.Tests.ECS
{
    public class SpawnerSystemIntegrationTests
    {
        [Fact]
        public void SpawnerChain_SpawnsMovesAndExpiresProjectiles()
        {
            using var runner = new SessionRunnerBuilder()
                .WithDeltaTime(FP.One)
                .WithSessionFactory((deltaTime, localPlayerId) => new SpawnerIntegrationSession(deltaTime, localPlayerId))
                .AddSystem(new SpawnerSystem())
                .AddSystem(new MovementSystem())
                .AddSystem(new LifetimeSystem())
                .Build();

            runner.Start();

            var session = Assert.IsType<SpawnerIntegrationSession>(runner.Session);
            session.SpawnSpawner(
                new Position2D { X = FP.FromRaw(FP.Raw._10), Y = FP.Zero },
                new Spawner
                {
                    RemainingCount = 2,
                    CooldownRemaining = FP.One,
                    Interval = FP.One,
                    SpawnVelocityX = FP._2,
                    SpawnVelocityY = FP.Zero,
                    SpawnLifetime = FP._2
                });

            runner.Step();

            Assert.Equal(1, CountProjectiles(session.PredictedFrame!));
            AssertProjectileState(session.PredictedFrame!, expectedX: FP._10 + FP._2, expectedLifetime: FP._1);

            runner.Step();

            Assert.Equal(1, CountProjectiles(session.PredictedFrame!));
            AssertProjectileState(session.PredictedFrame!, expectedX: FP._10 + FP._2, expectedLifetime: FP._1);

            runner.Step();

            Assert.Equal(0, CountProjectiles(session.PredictedFrame!));
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

        private static void AssertProjectileState(Frame frame, FP expectedX, FP expectedLifetime)
        {
            var positionEnumerator = frame.Query<Position2D, ProjectileTag>().GetEnumerator();
            Assert.True(positionEnumerator.MoveNext());

            EntityRef projectile = positionEnumerator.Entity;
            ref Position2D position = ref positionEnumerator.Component1;
            ref Lifetime lifetime = ref frame.Get<Lifetime>(projectile);

            Assert.Equal(expectedX, position.X);
            Assert.Equal(FP.Zero, position.Y);
            Assert.Equal(expectedLifetime, lifetime.Remaining);
            Assert.False(positionEnumerator.MoveNext());
        }

        private sealed class SpawnerIntegrationSession : Session
        {
            public SpawnerIntegrationSession(FP deltaTime, int localPlayerId)
                : base(deltaTime, localPlayerId)
            {
            }

            public EntityRef SpawnSpawner(Position2D position, Spawner spawner)
            {
                if (VerifiedFrame == null || PredictedFrame == null)
                {
                    throw new InvalidOperationException("Session must be started before spawning entities.");
                }

                EntityRef verifiedEntity = VerifiedFrame.CreateEntity();
                VerifiedFrame.Add(verifiedEntity, position);
                VerifiedFrame.Add(verifiedEntity, spawner);

                EntityRef predictedEntity = PredictedFrame.CreateEntity();
                PredictedFrame.Add(predictedEntity, position);
                PredictedFrame.Add(predictedEntity, spawner);

                Assert.Equal(verifiedEntity, predictedEntity);
                return verifiedEntity;
            }
        }
    }
}
