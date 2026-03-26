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
                .WithRunnerName("Gameplay")
                .WithDeltaTime(FP.One)
                .AddSystem(system)
                .Build();

            Assert.Equal("Gameplay", runner.Name);
            runner.Start();
            runner.Step(2);

            Assert.True(runner.IsRunning);
            Assert.Equal(SessionRunnerState.Running, runner.State);
            Assert.Equal(1, system.InitCount);
            Assert.Equal(2, system.UpdateCount);
        }

        [Fact]
        public void Build_UsesCustomRuntimeFactory()
        {
            using var runner = new SessionRunnerBuilder()
                .WithDeltaTime(FP.FromRaw(FP.Raw._0_016))
                .WithLocalPlayerId(7)
                .WithRuntimeFactory(options => new CustomRuntime(options.DeltaTime, options.LocalPlayerId))
                .Build();

            var runtime = Assert.IsType<CustomRuntime>(runner.Runtime);
            Assert.Equal(FP.FromRaw(FP.Raw._0_016), runtime.DeltaTime);
            Assert.Equal(7, runtime.LocalPlayerId);
        }

        [Fact]
        public void Build_UsesRuntimeFactory_WithRuntimeOptionsPayload()
        {
            SessionRuntimeOptions? capturedOptions = null;

            using var runner = new SessionRunnerBuilder()
                .WithRuntimeFactory(options =>
                {
                    capturedOptions = options;
                    return new CustomRuntime(options.DeltaTime, options.LocalPlayerId);
                })
                .WithRuntimeOptions(new SessionRuntimeOptions(FP.FromRaw(FP.Raw._0_016), 11))
                .Build();

            Assert.NotNull(capturedOptions);
            Assert.Equal(FP.FromRaw(FP.Raw._0_016), capturedOptions!.DeltaTime);
            Assert.Equal(11, capturedOptions.LocalPlayerId);
            Assert.IsType<CustomRuntime>(runner.Runtime);
        }

        [Fact]
        public void Build_CanCreateOfficialLocalAuthoritativeRuntime()
        {
            using var runner = new SessionRunnerBuilder()
                .WithRunnerName("Authority")
                .WithRuntimeOptions(new SessionRuntimeOptions(FP.One, 13))
                .ConfigureContext(context => context.SetShared(new SharedCounter(context.RuntimeOptions.LocalPlayerId)))
                .WithRuntimeFactory(options => new LocalAuthoritativeSession(options))
                .Build();

            LocalAuthoritativeSession runtime = Assert.IsType<LocalAuthoritativeSession>(runner.Runtime);
            Assert.Equal(SessionRuntimeKind.LocalAuthoritative, runner.RuntimeKind);
            Assert.Equal("Authority", runner.Name);
            Assert.Equal("Authority", runner.Context.RunnerName);
            Assert.Equal(13, runtime.LocalPlayerId);
            Assert.Equal(13, runner.Context.GetRequiredShared<SharedCounter>().Value);
        }

        [Fact]
        public void Build_UsesRuntimeOptions_WhenProvided()
        {
            SessionRuntimeOptions options = new SessionRuntimeOptions(FP.FromRaw(FP.Raw._0_016), 9);

            using var runner = new SessionRunnerBuilder()
                .WithRuntimeOptions(options)
                .Build();

            Assert.Equal(options, runner.Runtime.RuntimeOptions);
            Assert.Equal(FP.FromRaw(FP.Raw._0_016), runner.Runtime.DeltaTime);
            Assert.Equal(9, runner.Runtime.LocalPlayerId);
        }

        [Fact]
        public void Build_WithoutOverrides_UsesDefaultRuntimeOptions()
        {
            using var runner = new SessionRunnerBuilder().Build();

            Assert.NotNull(runner.Definition);
            Assert.IsType<MinimalPredictionSession>(runner.Runtime);
            Assert.Equal(SessionRuntimeOptions.Default.DeltaTime, runner.Runtime.DeltaTime);
            Assert.Equal(SessionRuntimeOptions.Default.LocalPlayerId, runner.Runtime.LocalPlayerId);
            Assert.Same(runner.Runtime.RuntimeBoundary, runner.Runtime.RuntimeBoundary);
        }

        [Fact]
        public void BuildDefinition_CapturesImmutableRunnerConfiguration()
        {
            var builder = new SessionRunnerBuilder()
                .WithRunnerName("BattleLoop")
                .WithDeltaTime(FP.FromRaw(FP.Raw._0_016))
                .WithLocalPlayerId(5);

            SessionRunnerDefinition definition = builder.BuildDefinition();

            builder
                .WithRunnerName("Changed")
                .WithDeltaTime(FP.One)
                .WithLocalPlayerId(9);

            Assert.Equal("BattleLoop", definition.RunnerName);
            Assert.Equal(FP.FromRaw(FP.Raw._0_016), definition.RuntimeOptions.DeltaTime);
            Assert.Equal(5, definition.RuntimeOptions.LocalPlayerId);
        }

        [Fact]
        public void BuildDefinition_CapturesLifecycleHooksImmutably()
        {
            string observed = string.Empty;
            var builder = new SessionRunnerBuilder()
                .ConfigureLifecycle(hooks => hooks.RuntimeCreated = (_, _) => observed = "first");

            SessionRunnerDefinition definition = builder.BuildDefinition();

            builder.ConfigureLifecycle(hooks => hooks.RuntimeCreated = (_, _) => observed = "second");

            using var runner = definition.CreateRunner();

            Assert.Equal("first", observed);
            Assert.Equal("Default", runner.Name);
        }

        [Fact]
        public void WithRuntimeOptions_Null_Throws()
        {
            var builder = new SessionRunnerBuilder();

            Assert.Throws<ArgumentNullException>(() => builder.WithRuntimeOptions(null!));
        }

        [Fact]
        public void WithRunnerName_Invalid_Throws()
        {
            var builder = new SessionRunnerBuilder();

            Assert.Throws<ArgumentException>(() => builder.WithRunnerName(""));
            Assert.Throws<ArgumentException>(() => builder.WithRunnerName("   "));
        }

        [Fact]
        public void AddSystem_Null_Throws()
        {
            var builder = new SessionRunnerBuilder();

            Assert.Throws<ArgumentNullException>(() => builder.AddSystem(null!));
        }

        [Fact]
        public void WithRuntimeFactory_Null_Throws()
        {
            var builder = new SessionRunnerBuilder();

            Assert.Throws<ArgumentNullException>(() => builder.WithRuntimeFactory(null!));
        }

        [Fact]
        public void ConfigureLifecycle_Null_Throws()
        {
            var builder = new SessionRunnerBuilder();

            Assert.Throws<ArgumentNullException>(() => builder.ConfigureLifecycle(null!));
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
            Assert.Equal(1, runner.Runtime.CurrentTick);
            Assert.Equal(2, system.InitCount);
            Assert.Equal(3, system.UpdateCount);
        }

        [Fact]
        public void BuildRuntime_CanBeCalledMultipleTimes_AndReturnsIndependentSessions()
        {
            var builder = new SessionRunnerBuilder()
                .WithDeltaTime(FP.FromRaw(FP.Raw._0_016))
                .WithLocalPlayerId(3);

            using SessionRuntime first = builder.BuildRuntime();
            using SessionRuntime second = builder.BuildRuntime();

            Assert.NotSame(first, second);
            Assert.Equal(first.DeltaTime, second.DeltaTime);
            Assert.Equal(first.LocalPlayerId, second.LocalPlayerId);

            first.Start();
            first.Update();

            Assert.Equal(1, first.CurrentTick);
            Assert.Equal(0, second.CurrentTick);
            Assert.False(second.IsRunning);
        }

        [Fact]
        public void BuildRuntime_FromDefinition_CanBeCalledMultipleTimes()
        {
            SessionRunnerDefinition definition = new SessionRunnerBuilder()
                .WithRuntimeOptions(new SessionRuntimeOptions(FP.One, 2))
                .BuildDefinition();

            using SessionRuntime first = definition.BuildRuntime();
            using SessionRuntime second = definition.BuildRuntime();

            Assert.NotSame(first, second);
            Assert.Equal(2, first.LocalPlayerId);
            Assert.Equal(2, second.LocalPlayerId);
        }

        [Fact]
        public void BuildDefinition_ContextConfigurator_IsAppliedToEachRuntime()
        {
            SessionRunnerDefinition definition = new SessionRunnerBuilder()
                .WithRunnerName("Contextual")
                .ConfigureContext(context => context.SetShared(new SharedCounter(context.RuntimeOptions.LocalPlayerId)))
                .WithRuntimeOptions(new SessionRuntimeOptions(FP.One, 12))
                .BuildDefinition();

            using SessionRuntime first = definition.BuildRuntime();
            using SessionRuntime second = definition.BuildRuntime();

            Assert.Equal("Contextual", first.Context.RunnerName);
            Assert.Equal(12, first.Context.GetRequiredShared<SharedCounter>().Value);
            Assert.Equal(12, second.Context.GetRequiredShared<SharedCounter>().Value);
            Assert.NotSame(first.Context.GetRequiredShared<SharedCounter>(), second.Context.GetRequiredShared<SharedCounter>());
        }

        [Fact]
        public void BuildRuntime_WhenFactoryReturnsMismatchedStableOptions_Throws()
        {
            SessionRunnerDefinition definition = new SessionRunnerBuilder()
                .WithRuntimeOptions(new SessionRuntimeOptions(FP.One, 2))
                .WithRuntimeFactory(_ => new CustomRuntime(FP.FromRaw(FP.Raw._0_016), 99))
                .BuildDefinition();

            Assert.Throws<InvalidOperationException>(() => definition.BuildRuntime());
        }

        [Fact]
        public void Session_CompatibilityAlias_StillRepresentsMinimalPredictionRuntime()
        {
#pragma warning disable CS0618
            using Session session = new Session(FP.One);
#pragma warning restore CS0618

            Assert.IsAssignableFrom<MinimalPredictionSession>(session);
            Assert.IsAssignableFrom<SessionRuntime>(session);
            Assert.Equal(SessionRuntimeKind.MinimalPrediction, session.RuntimeKind);
        }

        [Fact]
        public void SessionRuntimeOptions_WithMethods_ReturnNewInstancesAndPreserveOtherValues()
        {
            SessionRuntimeOptions source = new SessionRuntimeOptions(FP.One, 7);

            SessionRuntimeOptions changedDeltaTime = source.WithDeltaTime(FP.FromRaw(FP.Raw._0_016));
            SessionRuntimeOptions changedPlayerId = source.WithLocalPlayerId(3);

            Assert.NotSame(source, changedDeltaTime);
            Assert.NotSame(source, changedPlayerId);
            Assert.Equal(7, changedDeltaTime.LocalPlayerId);
            Assert.Equal(FP.One, changedPlayerId.DeltaTime);
            Assert.Equal(FP.FromRaw(FP.Raw._0_016), changedDeltaTime.DeltaTime);
            Assert.Equal(3, changedPlayerId.LocalPlayerId);
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

        private sealed class CustomRuntime : MinimalPredictionSession
        {
            public CustomRuntime(FP deltaTime, int localPlayerId)
                : base(deltaTime, localPlayerId)
            {
            }
        }

        private sealed class SharedCounter : ISessionRuntimeSharedService
        {
            public SharedCounter(int value)
            {
                Value = value;
            }

            public int Value { get; }
        }
    }
}
