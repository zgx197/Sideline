using System;
using Lattice.Core;
using Lattice.ECS.Core;
using Lattice.ECS.Framework;
using Lattice.ECS.Session;
using Lattice.Math;
using Xunit;

namespace Lattice.Tests.ECS
{
    public class SessionRunnerBuilderTests
    {
        [Fact]
        public void Build_CreatesRunnableSessionWithConfiguredSystems()
        {
            var system = new CountingSystem();

            using var runner = new SessionRunnerBuilder()
                .WithDeltaTime(FP.One)
                .AddSystem(system)
                .Build();

            runner.Start();
            runner.Step(2);

            Assert.True(runner.IsRunning);
            Assert.Equal(1, system.InitCount);
            Assert.Equal(2, system.UpdateCount);
        }

        [Fact]
        public void Build_UsesCustomSessionFactory()
        {
            using var runner = new SessionRunnerBuilder()
                .WithDeltaTime(FP.FromRaw(FP.Raw._0_016))
                .WithLocalPlayerId(7)
                .WithSessionFactory((deltaTime, localPlayerId) => new CustomSession(deltaTime, localPlayerId))
                .Build();

            var session = Assert.IsType<CustomSession>(runner.Session);
            Assert.Equal(FP.FromRaw(FP.Raw._0_016), session.DeltaTime);
            Assert.Equal(7, session.LocalPlayerId);
        }

        [Fact]
        public void Build_UsesRuntimeOptions_WhenProvided()
        {
            SessionRuntimeOptions options = new SessionRuntimeOptions(FP.FromRaw(FP.Raw._0_016), 9);

            using var runner = new SessionRunnerBuilder()
                .WithRuntimeOptions(options)
                .Build();

            Assert.Equal(options, runner.Session.RuntimeOptions);
            Assert.Equal(FP.FromRaw(FP.Raw._0_016), runner.Session.DeltaTime);
            Assert.Equal(9, runner.Session.LocalPlayerId);
        }

        [Fact]
        public void WithRuntimeOptions_Null_Throws()
        {
            var builder = new SessionRunnerBuilder();

            Assert.Throws<ArgumentNullException>(() => builder.WithRuntimeOptions(null!));
        }

        [Fact]
        public void AddSystem_Null_Throws()
        {
            var builder = new SessionRunnerBuilder();

            Assert.Throws<ArgumentNullException>(() => builder.AddSystem(null!));
        }

        [Fact]
        public void WithSessionFactory_Null_Throws()
        {
            var builder = new SessionRunnerBuilder();

            Assert.Throws<ArgumentNullException>(() => builder.WithSessionFactory(null!));
        }

        [Fact]
        public void Build_RunnerCanRestartSessionLifecycle()
        {
            var system = new CountingSystem();

            using var runner = new SessionRunnerBuilder()
                .WithDeltaTime(FP.One)
                .AddSystem(system)
                .Build();

            runner.Start();
            runner.Step(2);
            runner.Stop();

            runner.Start();
            runner.Step();

            Assert.True(runner.IsRunning);
            Assert.Equal(1, runner.Session.CurrentTick);
            Assert.Equal(2, system.InitCount);
            Assert.Equal(3, system.UpdateCount);
        }

        private sealed class CountingSystem : ISystem
        {
            public int InitCount { get; private set; }

            public int UpdateCount { get; private set; }

            public void OnInit(Frame frame)
            {
                InitCount++;
            }

            public void OnUpdate(Frame frame, FP deltaTime)
            {
                UpdateCount++;
            }

            public void OnDestroy(Frame frame)
            {
            }
        }

        private sealed class CustomSession : Session
        {
            public CustomSession(FP deltaTime, int localPlayerId)
                : base(deltaTime, localPlayerId)
            {
            }
        }
    }
}
