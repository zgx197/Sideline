using System;
using Lattice.ECS.Session;
using Lattice.Math;
using Xunit;

namespace Lattice.Tests.ECS
{
    public class SessionRunnerTests
    {
        [Fact]
        public void Start_Step_Stop_DrivesSessionLifecycle()
        {
            using var session = new MinimalPredictionSession(FP.One);
            using var runner = new SessionRunner(session);

            Assert.Equal(SessionRunnerState.Created, runner.State);
            Assert.Equal(SessionRunnerShutdownCause.None, runner.LastShutdownCause);
            Assert.Equal(SessionRunnerResetReason.None, runner.LastResetReason);
            Assert.Null(runner.LastFailure);
            runner.Start();
            Assert.Equal(SessionRunnerState.Running, runner.State);
            runner.Step();
            runner.Step(2);
            runner.Stop();

            Assert.False(runner.IsRunning);
            Assert.Equal(SessionRunnerState.Stopped, runner.State);
            Assert.Equal(SessionRunnerShutdownCause.HostStopRequested, runner.LastShutdownCause);
            Assert.Equal(3, session.CurrentTick);
        }

        [Fact]
        public void Start_AndStop_AreIdempotent()
        {
            using var session = new MinimalPredictionSession(FP.One);
            using var runner = new SessionRunner(session);

            runner.Start();
            runner.Start();

            Assert.True(runner.IsRunning);

            runner.Stop();
            runner.Stop();

            Assert.False(runner.IsRunning);
        }

        [Fact]
        public void Step_WhenRunnerNotStarted_Throws()
        {
            using var session = new MinimalPredictionSession(FP.One);
            using var runner = new SessionRunner(session);

            Assert.Throws<InvalidOperationException>(() => runner.Step());
        }

        [Fact]
        public void Stop_WhenRunnerNotStarted_IsNoOp()
        {
            using var session = new MinimalPredictionSession(FP.One);
            using var runner = new SessionRunner(session);

            runner.Stop();

            Assert.False(runner.IsRunning);
            Assert.Equal(SessionRunnerState.Created, runner.State);
            Assert.False(session.IsRunning);
            Assert.Equal(SessionRunnerShutdownCause.None, runner.LastShutdownCause);
        }

        [Fact]
        public void Step_WithZeroCount_IsNoOp()
        {
            using var session = new MinimalPredictionSession(FP.One);
            using var runner = new SessionRunner(session);

            runner.Start();
            runner.Step(0);

            Assert.Equal(0, session.CurrentTick);
            Assert.True(runner.IsRunning);
        }

        [Fact]
        public void Step_WithNegativeCount_Throws()
        {
            using var session = new MinimalPredictionSession(FP.One);
            using var runner = new SessionRunner(session);

            Assert.Throws<ArgumentOutOfRangeException>(() => runner.Step(-1));
        }

        [Fact]
        public void Dispose_PreventsFurtherLifecycleCalls()
        {
            using var session = new MinimalPredictionSession(FP.One);
            var runner = new SessionRunner(session);

            runner.Start();
            runner.Dispose();

            Assert.False(runner.IsRunning);
            Assert.Equal(SessionRunnerState.Disposed, runner.State);
            Assert.Throws<ObjectDisposedException>(() => runner.Start());
            Assert.Throws<ObjectDisposedException>(() => runner.Step());
            Assert.Throws<ObjectDisposedException>(() => runner.Stop());
        }

        [Fact]
        public void Dispose_CanBeCalledTwice()
        {
            using var session = new MinimalPredictionSession(FP.One);
            var runner = new SessionRunner(session);

            runner.Dispose();
            runner.Dispose();

            Assert.False(runner.IsRunning);
            Assert.Equal(SessionRunnerState.Disposed, runner.State);
        }

        [Fact]
        public void DefinitionBasedRunner_ExposesBoundaryMetadata()
        {
            SessionRunnerDefinition definition = new SessionRunnerBuilder()
                .WithRunnerName("Battle")
                .WithRuntimeOptions(new SessionRuntimeOptions(FP.One, 4))
                .BuildDefinition();

            using var runner = definition.CreateRunner();

            Assert.Equal("Battle", runner.Name);
            Assert.Same(definition, runner.Definition);
            Assert.Equal("Battle", runner.Context.RunnerName);
            Assert.Same(runner.Runtime, runner.Context.Runtime);
            Assert.Equal(SessionRuntimeKind.MinimalPrediction, runner.RuntimeKind);
            Assert.Equal(4, runner.RuntimeOptions.LocalPlayerId);
            Assert.False(runner.IsRunning);
            Assert.Equal(SessionRunnerShutdownCause.None, runner.LastShutdownCause);
            Assert.Equal(SessionRunnerResetReason.None, runner.LastResetReason);
            Assert.Null(runner.LastFailure);
        }

        [Fact]
        public void DefinitionBasedRunner_CanHostLocalAuthoritativeRuntime()
        {
            SessionRunnerDefinition definition = new SessionRunnerBuilder()
                .WithRunnerName("Authority")
                .WithRuntimeFactory(options => new LocalAuthoritativeSession(options))
                .WithRuntimeOptions(new SessionRuntimeOptions(FP.One, 6))
                .BuildDefinition();

            using var runner = definition.CreateRunner();

            Assert.Equal("Authority", runner.Name);
            Assert.Equal("Authority", runner.Context.RunnerName);
            Assert.Equal(SessionRuntimeKind.LocalAuthoritative, runner.RuntimeKind);
            Assert.True(runner.RuntimeBoundary.Supports(SessionRuntimeCapability.LocalPredictionStep));
            Assert.True(runner.RuntimeBoundary.Supports(SessionRuntimeCapability.CheckpointRestore));
            Assert.False(runner.RuntimeBoundary.Supports(SessionRuntimeCapability.PredictionVerification));
            Assert.False(runner.RuntimeBoundary.Supports(SessionRuntimeCapability.LocalRewind));
        }

        [Fact]
        public void ResetRuntime_RecreatesRuntimeFromDefinition()
        {
            SessionRunnerDefinition definition = new SessionRunnerBuilder()
                .WithRunnerName("Resettable")
                .BuildDefinition();

            using var runner = definition.CreateRunner();
            SessionRuntime initialRuntime = runner.Runtime;

            runner.Start();
            runner.Step();
            runner.Stop();
            runner.ResetRuntime();

            Assert.NotSame(initialRuntime, runner.Runtime);
            Assert.NotSame(initialRuntime.Context, runner.Context);
            Assert.Equal(SessionRunnerState.Created, runner.State);
            Assert.Equal(SessionRunnerShutdownCause.RuntimeReset, runner.LastShutdownCause);
            Assert.Equal(SessionRunnerResetReason.HostRequested, runner.LastResetReason);
            Assert.Equal(0, runner.Runtime.CurrentTick);
            Assert.False(runner.IsRunning);
        }

        [Fact]
        public void ResetRuntime_ReappliesContextConfiguration()
        {
            SessionRunnerDefinition definition = new SessionRunnerBuilder()
                .WithRunnerName("Resettable")
                .ConfigureContext(context => context.SetShared(new ResetMarker(Guid.NewGuid())))
                .BuildDefinition();

            using var runner = definition.CreateRunner();
            ResetMarker before = runner.Context.GetRequiredShared<ResetMarker>();

            runner.ResetRuntime();

            ResetMarker after = runner.Context.GetRequiredShared<ResetMarker>();
            Assert.NotEqual(before.Id, after.Id);
            Assert.Equal("Resettable", runner.Context.RunnerName);
        }

        [Fact]
        public void ConfigureLifecycle_InvokesHooksAroundRuntimeCreationStartStopAndReset()
        {
            var events = new System.Collections.Generic.List<string>();
            int runtimeGeneration = 0;

            SessionRunnerDefinition definition = new SessionRunnerBuilder()
                .WithRunnerName("Hosted")
                .ConfigureLifecycle(hooks =>
                {
                    hooks.RuntimeCreated = (runner, runtime) =>
                    {
                        runtime.Context.SetShared(new HostMarker(++runtimeGeneration));
                        events.Add($"created:{runner.Name}:{runtime.Context.GetRequiredShared<HostMarker>().Generation}");
                    };
                    hooks.BeforeStart = runner => events.Add($"before-start:{runner.State}");
                    hooks.AfterStart = runner => events.Add($"after-start:{runner.State}");
                    hooks.BeforeStop = (_, cause) => events.Add($"before-stop:{cause}");
                    hooks.AfterStop = (_, cause) => events.Add($"after-stop:{cause}");
                    hooks.BeforeReset = (_, reason) => events.Add($"before-reset:{reason}");
                    hooks.AfterReset = (runner, _) => events.Add($"after-reset:{runner.Context.GetRequiredShared<HostMarker>().Generation}");
                })
                .BuildDefinition();

            using var runner = definition.CreateRunner();

            Assert.Equal(1, runner.Context.GetRequiredShared<HostMarker>().Generation);

            runner.Start();
            runner.ResetRuntime();

            Assert.Equal(SessionRunnerState.Created, runner.State);
            Assert.Equal(SessionRunnerShutdownCause.RuntimeReset, runner.LastShutdownCause);
            Assert.Equal(SessionRunnerResetReason.HostRequested, runner.LastResetReason);
            Assert.Equal(2, runner.Context.GetRequiredShared<HostMarker>().Generation);
            Assert.Equal(
                new[]
                {
                    "created:Hosted:1",
                    "before-start:Created",
                    "after-start:Running",
                    "before-reset:HostRequested",
                    "before-stop:RuntimeReset",
                    "after-stop:RuntimeReset",
                    "created:Hosted:2",
                    "after-reset:2"
                },
                events.ToArray());
        }

        [Fact]
        public void Start_WhenRuntimeStartThrows_CapturesFailureAndTransitionsToFaulted()
        {
            var failures = new System.Collections.Generic.List<SessionRunnerFailureInfo>();

            using var runner = new SessionRunnerBuilder()
                .WithRuntimeFactory(_ => new StartFailureRuntime(FP.One))
                .ConfigureLifecycle(hooks => hooks.Failure = (_, failure) => failures.Add(failure))
                .Build();

            InvalidOperationException exception = Assert.ThrowsAny<InvalidOperationException>(() => runner.Start());

            Assert.Equal("Start failed after entering runtime.", exception.Message);
            Assert.Equal(SessionRunnerState.Faulted, runner.State);
            Assert.False(runner.IsRunning);
            Assert.False(runner.Runtime.IsRunning);
            Assert.Equal(SessionRunnerShutdownCause.StartupFailure, runner.LastShutdownCause);
            Assert.NotNull(runner.LastFailure);
            Assert.Equal(SessionRunnerFailureKind.Start, runner.LastFailure!.Kind);
            Assert.Equal("Start", runner.LastFailure.OperationName);
            Assert.Single(failures);
            Assert.Same(runner.LastFailure, failures[0]);
        }

        [Fact]
        public void Step_WhenRuntimeUpdateThrows_CapturesFailureAndTransitionsToFaulted()
        {
            var failures = new System.Collections.Generic.List<SessionRunnerFailureInfo>();

            using var runner = new SessionRunnerBuilder()
                .WithRuntimeFactory(_ => new UpdateFailureRuntime(FP.One))
                .ConfigureLifecycle(hooks => hooks.Failure = (_, failure) => failures.Add(failure))
                .Build();

            runner.Start();
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => runner.Step());

            Assert.Equal("Step failed inside runtime update.", exception.Message);
            Assert.Equal(SessionRunnerState.Faulted, runner.State);
            Assert.False(runner.IsRunning);
            Assert.False(runner.Runtime.IsRunning);
            Assert.Equal(SessionRunnerShutdownCause.StepFailure, runner.LastShutdownCause);
            Assert.NotNull(runner.LastFailure);
            Assert.Equal(SessionRunnerFailureKind.Step, runner.LastFailure!.Kind);
            Assert.Equal("Step", runner.LastFailure.OperationName);
            Assert.Single(failures);
            Assert.Same(runner.LastFailure, failures[0]);
        }

        [Fact]
        public void Start_WhenLifecycleHookThrows_CapturesLifecycleFailure()
        {
            using var runner = new SessionRunnerBuilder()
                .ConfigureLifecycle(hooks => hooks.BeforeStart = _ => throw new InvalidOperationException("Hook exploded."))
                .Build();

            InvalidOperationException exception = Assert.ThrowsAny<InvalidOperationException>(() => runner.Start());

            Assert.Contains("BeforeStart", exception.Message, StringComparison.Ordinal);
            Assert.Equal(SessionRunnerState.Faulted, runner.State);
            Assert.False(runner.Runtime.IsRunning);
            Assert.NotNull(runner.LastFailure);
            Assert.Equal(SessionRunnerFailureKind.LifecycleHook, runner.LastFailure!.Kind);
            Assert.Equal("BeforeStart", runner.LastFailure.OperationName);
            Assert.Equal(SessionRunnerShutdownCause.StartupFailure, runner.LastShutdownCause);
        }

        [Fact]
        public void Dispose_DisposesOwnedContextSharedObjects_WhenRunnerOwnsRuntime()
        {
            var shared = new DisposableMarker();
            SessionRunnerDefinition definition = new SessionRunnerBuilder()
                .ConfigureContext(context => context.SetShared(shared, disposeWithContext: true))
                .BuildDefinition();

            var runner = definition.CreateRunner();
            runner.Dispose();

            Assert.True(shared.IsDisposed);
        }

        [Fact]
        public void ResetRuntime_OnAdHocRunner_Throws()
        {
            using var session = new MinimalPredictionSession(FP.One);
            using var runner = new SessionRunner(session);

            Assert.Throws<InvalidOperationException>(() => runner.ResetRuntime());
        }

        private sealed class ResetMarker : ISessionRuntimeSharedService
        {
            public ResetMarker(Guid id)
            {
                Id = id;
            }

            public Guid Id { get; }
        }

        private sealed class DisposableMarker : IDisposable, ISessionRuntimeSharedService
        {
            public bool IsDisposed { get; private set; }

            public void Dispose()
            {
                IsDisposed = true;
            }
        }

        private sealed class HostMarker : ISessionRuntimeSharedService
        {
            public HostMarker(int generation)
            {
                Generation = generation;
            }

            public int Generation { get; }
        }

        private sealed class StartFailureRuntime : MinimalPredictionSession
        {
            public StartFailureRuntime(FP deltaTime)
                : base(deltaTime)
            {
            }

            public override void Start()
            {
                base.Start();
                throw new InvalidOperationException("Start failed after entering runtime.");
            }
        }

        private sealed class UpdateFailureRuntime : MinimalPredictionSession
        {
            public UpdateFailureRuntime(FP deltaTime)
                : base(deltaTime)
            {
            }

            public override void Update()
            {
                throw new InvalidOperationException("Step failed inside runtime update.");
            }
        }
    }
}
