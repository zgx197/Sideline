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
                .WithRuntimeFactory(options => new SpawnerIntegrationSession(options.DeltaTime, options.LocalPlayerId))
                .AddSystem(new SpawnerSystem())
                .AddSystem(new MovementSystem())
                .AddSystem(new LifetimeSystem())
                .Build();

            runner.Start();

            var session = Assert.IsType<SpawnerIntegrationSession>(runner.Runtime);
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
            AssertProjectileStates(session.PredictedFrame!, (FP._10, FP._2));

            runner.Step();

            Assert.Equal(2, CountProjectiles(session.PredictedFrame!));
            AssertProjectileStates(session.PredictedFrame!, (FP._10, FP._2), (FP._10 + FP._2, FP._1));

            runner.Step();

            Assert.Equal(1, CountProjectiles(session.PredictedFrame!));
            AssertProjectileStates(session.PredictedFrame!, (FP._10 + FP._2, FP._1));

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

        private static void AssertProjectileStates(Frame frame, params (FP X, FP Lifetime)[] expectedStates)
        {
            var positionEnumerator = frame.Query<Position2D, ProjectileTag>().GetEnumerator();
            var actualStates = new (FP X, FP Lifetime)[expectedStates.Length];
            int count = 0;

            while (positionEnumerator.MoveNext())
            {
                EntityRef projectile = positionEnumerator.Entity;
                ref Position2D position = ref positionEnumerator.Component1;
                ref Lifetime lifetime = ref frame.Get<Lifetime>(projectile);

                Assert.True(count < actualStates.Length, "Unexpected extra projectile in frame.");
                Assert.Equal(FP.Zero, position.Y);
                actualStates[count++] = (position.X, lifetime.Remaining);
            }

            Assert.Equal(expectedStates.Length, count);
            Array.Sort(actualStates, static (left, right) => left.X.CompareTo(right.X));
            Array.Sort(expectedStates, static (left, right) => left.X.CompareTo(right.X));
            Assert.Equal(expectedStates, actualStates);
        }

        private sealed class SpawnerIntegrationSession : MinimalPredictionSession
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
