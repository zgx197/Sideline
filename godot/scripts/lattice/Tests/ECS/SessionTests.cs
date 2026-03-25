using System;
using Lattice.Core;
using Lattice.ECS.Core;
using Lattice.ECS.Framework;
using Lattice.ECS.Session;
using Lattice.Math;
using Xunit;

namespace Lattice.Tests.ECS
{
    public class SessionTests
    {
        [Fact]
        public void Start_CreatesInitializedVerifiedAndPredictedFrames()
        {
            using var session = CreateSession();

            session.Start();

            Assert.True(session.IsRunning);
            Assert.NotNull(session.VerifiedFrame);
            Assert.NotNull(session.PredictedFrame);
            Assert.NotSame(session.VerifiedFrame, session.PredictedFrame);
            Assert.Equal(0, session.CurrentTick);
            Assert.Equal(1, session.VerifiedFrame!.EntityCount);
            Assert.Equal(ReadState(session.VerifiedFrame), ReadState(session.PredictedFrame!));

            Frame? historical = session.GetHistoricalFrame(0);
            Assert.NotNull(historical);
            Assert.Equal(ReadState(session.VerifiedFrame), ReadState(historical!));
        }

        [Fact]
        public void Constructor_WithRuntimeOptions_UsesConfiguredValues()
        {
            SessionRuntimeOptions options = new SessionRuntimeOptions(FP.FromRaw(FP.Raw._0_016), 5);
            using var session = new Session(options);

            Assert.Same(options, session.RuntimeOptions);
            Assert.Equal(FP.FromRaw(FP.Raw._0_016), session.DeltaTime);
            Assert.Equal(5, session.LocalPlayerId);
        }

        [Fact]
        public void Update_AdvancesTickAndPreservesClonedState()
        {
            using var session = CreateSession();
            session.Start();

            session.SetPlayerInput(session.LocalPlayerId, 1, new TestInputCommand(session.LocalPlayerId, 1, 2));
            session.SetPlayerInput(session.LocalPlayerId, 2, new TestInputCommand(session.LocalPlayerId, 2, 3));

            session.Update();
            SessionState tick1 = ReadState(session.PredictedFrame!);

            session.Update();
            SessionState tick2 = ReadState(session.PredictedFrame!);

            Assert.Equal(1, tick1.Tick);
            Assert.Equal(1, tick1.StepCount);
            Assert.Equal(2, tick1.InputSum);
            Assert.NotNull(session.PreviousFrame);
            Assert.Equal(tick1, ReadState(session.PreviousFrame!));

            Assert.Equal(2, tick2.Tick);
            Assert.Equal(2, tick2.StepCount);
            Assert.Equal(5, tick2.InputSum);
        }

        [Fact]
        public void Update_WithoutInput_AdvancesTickAndKeepsInputSumZero()
        {
            using var session = CreateSession();
            session.Start();

            session.Update();
            session.Update();

            SessionState state = ReadState(session.PredictedFrame!);
            Assert.Equal(2, state.Tick);
            Assert.Equal(2, state.StepCount);
            Assert.Equal(0, state.InputSum);
        }

        [Fact]
        public void HistoricalFrame_OutsideLiveWindow_CanBeRebuiltFromSampledSnapshots()
        {
            using var session = CreateSession();
            session.Start();

            for (int tick = 1; tick <= 32; tick++)
            {
                session.SetPlayerInput(session.LocalPlayerId, tick, new TestInputCommand(session.LocalPlayerId, tick, tick));
                session.Update();
            }

            Frame historical = Assert.IsType<Frame>(session.GetHistoricalFrame(13));
            SessionState state = ReadState(historical);

            Assert.Equal(13, state.Tick);
            Assert.Equal(13, state.StepCount);
            Assert.Equal(91, state.InputSum);
        }

        [Fact]
        public void HistoricalFrame_RepeatedOutsideLiveWindow_ReusesMaterializedScratchFrame()
        {
            using var session = CreateSession();
            session.Start();

            for (int tick = 1; tick <= 32; tick++)
            {
                session.SetPlayerInput(session.LocalPlayerId, tick, new TestInputCommand(session.LocalPlayerId, tick, tick));
                session.Update();
            }

            int baselineUpdateSystemsCalls = session.UpdateSystemsCallCount;

            Frame first = Assert.IsType<Frame>(session.GetHistoricalFrame(13));
            int afterFirstMaterializeCalls = session.UpdateSystemsCallCount;
            Frame second = Assert.IsType<Frame>(session.GetHistoricalFrame(13));

            Assert.Same(first, second);
            Assert.Equal(13, first.Tick);
            Assert.True(afterFirstMaterializeCalls > baselineUpdateSystemsCalls);
            Assert.Equal(afterFirstMaterializeCalls, session.UpdateSystemsCallCount);
        }

        [Fact]
        public void HistoricalFrame_WhenAdvanceFrameIsOverridden_FallsBackToCloneReplayPath()
        {
            using var session = CreateAdvanceTrackingSession();
            session.Start();

            for (int tick = 1; tick <= 32; tick++)
            {
                session.SetPlayerInput(session.LocalPlayerId, tick, new TestInputCommand(session.LocalPlayerId, tick, tick));
                session.Update();
            }

            int baselineAdvanceFrameCalls = session.AdvanceFrameCallCount;

            Frame historical = Assert.IsType<Frame>(session.GetHistoricalFrame(13));
            SessionState state = ReadState(historical);

            Assert.True(session.AdvanceFrameCallCount > baselineAdvanceFrameCalls);
            Assert.Equal(13, state.Tick);
            Assert.Equal(13, state.StepCount);
            Assert.Equal(91, state.InputSum);
        }

        [Fact]
        public void CloneState_CopiesRuntimeStateWithoutSharingMutableStorage()
        {
            using var session = CreateSession();
            session.Start();

            session.SetPlayerInput(session.LocalPlayerId, 1, new TestInputCommand(session.LocalPlayerId, 1, 2));
            session.Update();

            using Frame clone = session.PredictedFrame!.CloneState();
            Assert.Equal(ReadState(session.PredictedFrame!), ReadState(clone));

            var cloneEnumerator = clone.Query<TestInputSumComponent>().GetEnumerator();
            Assert.True(cloneEnumerator.MoveNext());
            cloneEnumerator.Component.Value += 10;

            Assert.Equal(12, ReadState(clone).InputSum);
            Assert.Equal(2, ReadState(session.PredictedFrame!).InputSum);
        }

        [Fact]
        public void CalculateChecksum_MatchesFrameSnapshotChecksum()
        {
            using var session = CreateSession();
            session.Start();

            session.SetPlayerInput(session.LocalPlayerId, 1, new TestInputCommand(session.LocalPlayerId, 1, 2));
            session.Update();

            Frame frame = session.PredictedFrame!;
#pragma warning disable CS0618
            FrameSnapshot snapshot = frame.CreateSnapshot(ComponentSerializationMode.Checkpoint);
#pragma warning restore CS0618

            Assert.Equal(snapshot.Checksum, frame.CalculateChecksum(ComponentSerializationMode.Checkpoint));
        }

        [Fact]
        public void Rewind_CanRestoreFromMaterializedHistoricalSnapshot()
        {
            using var session = CreateSession();
            session.Start();

            for (int tick = 1; tick <= 24; tick++)
            {
                session.SetPlayerInput(session.LocalPlayerId, tick, new TestInputCommand(session.LocalPlayerId, tick, tick));
                session.Update();
            }

            session.Rewind(11);

            SessionState state = ReadState(session.PredictedFrame!);
            Assert.Equal(13, session.CurrentTick);
            Assert.Equal(13, state.Tick);
            Assert.Equal(13, state.StepCount);
            Assert.Equal(91, state.InputSum);
        }

        [Fact]
        public void HistoryWindow_EvictsOldestTickWhenCapacityExceeded()
        {
            using var session = CreateSession();
            session.Start();

            for (int i = 0; i < Session.HistorySize; i++)
            {
                session.Update();
            }

            Assert.Null(session.GetHistoricalFrame(0));
            Assert.NotNull(session.GetHistoricalFrame(1));
            Assert.NotNull(session.GetHistoricalFrame(Session.HistorySize));
        }

        [Fact]
        public void CreateCheckpoint_AndRestoreFromCheckpoint_RecoverStateAndAllowContinue()
        {
            using var session = CreateSession();
            session.Start();

            session.SetPlayerInput(session.LocalPlayerId, 1, new TestInputCommand(session.LocalPlayerId, 1, 1));
            session.SetPlayerInput(session.LocalPlayerId, 2, new TestInputCommand(session.LocalPlayerId, 2, 2));
            session.Update();
            session.Update();

            SessionCheckpoint checkpoint = session.CreateCheckpoint();
            SessionState checkpointState = ReadState(session.PredictedFrame!);
            using Frame restoredVerified = RestoreFrame(checkpoint.VerifiedSnapshot!);
            SessionState verifiedCheckpointState = ReadState(restoredVerified);

            session.SetPlayerInput(session.LocalPlayerId, 3, new TestInputCommand(session.LocalPlayerId, 3, 4));
            session.Update();

            Assert.Equal(3, session.CurrentTick);
            Assert.Equal(7, ReadState(session.PredictedFrame!).InputSum);

            session.RestoreFromCheckpoint(checkpoint);

            Assert.Equal(2, session.CurrentTick);
            Assert.Equal(checkpointState, ReadState(session.PredictedFrame!));
            Assert.Equal(verifiedCheckpointState, ReadState(session.VerifiedFrame!));

            session.Update();

            Assert.Equal(3, session.CurrentTick);
            Assert.Equal(7, ReadState(session.PredictedFrame!).InputSum);
        }

        [Fact]
        public void VerifyFrame_WhenChecksumMismatch_RollsBackAndResimulatesToCurrentTick()
        {
            using var session = CreateSession();
            session.Start();

            session.SetPlayerInput(session.LocalPlayerId, 1, new TestInputCommand(session.LocalPlayerId, 1, 1));
            session.SetPlayerInput(session.LocalPlayerId, 2, new TestInputCommand(session.LocalPlayerId, 2, 2));
            session.SetPlayerInput(session.LocalPlayerId, 3, new TestInputCommand(session.LocalPlayerId, 3, 3));

            session.Update();
            session.Update();
            session.Update();

            int rollbackFrom = -1;
            int rollbackTo = -1;
            session.OnRollback += (fromTick, toTick) =>
            {
                rollbackFrom = fromTick;
                rollbackTo = toTick;
            };

            session.SetPlayerInput(session.LocalPlayerId, 2, new TestInputCommand(session.LocalPlayerId, 2, 20));

            Frame historical = Assert.IsType<Frame>(session.GetHistoricalFrame(1));
            long mismatchedChecksum = unchecked((long)historical.CalculateChecksum() + 1);

            session.VerifyFrame(1, mismatchedChecksum);

            SessionState state = ReadState(session.PredictedFrame!);
            Assert.Equal(3, session.CurrentTick);
            Assert.Equal(3, state.Tick);
            Assert.Equal(3, state.StepCount);
            Assert.Equal(24, state.InputSum);
            Assert.Equal(3, rollbackFrom);
            Assert.Equal(1, rollbackTo);
            Assert.False(session.IsRollingBack);
        }

        [Fact]
        public void RestoreFromCheckpoint_CanBeAppliedTwice()
        {
            using var session = CreateSession();
            session.Start();

            session.SetPlayerInput(session.LocalPlayerId, 1, new TestInputCommand(session.LocalPlayerId, 1, 3));
            session.SetPlayerInput(session.LocalPlayerId, 2, new TestInputCommand(session.LocalPlayerId, 2, 4));
            session.Update();
            session.Update();

            SessionCheckpoint checkpoint = session.CreateCheckpoint();
            SessionState expected = ReadState(session.PredictedFrame!);

            session.Update();
            session.RestoreFromCheckpoint(checkpoint);
            session.RestoreFromCheckpoint(checkpoint);

            Assert.Equal(expected, ReadState(session.PredictedFrame!));
            Assert.Equal(2, session.CurrentTick);
        }

        [Fact]
        public void RollbackTo_CanBeAppliedTwice()
        {
            using var session = CreateSession();
            session.Start();

            session.SetPlayerInput(session.LocalPlayerId, 1, new TestInputCommand(session.LocalPlayerId, 1, 1));
            session.SetPlayerInput(session.LocalPlayerId, 2, new TestInputCommand(session.LocalPlayerId, 2, 2));
            session.SetPlayerInput(session.LocalPlayerId, 3, new TestInputCommand(session.LocalPlayerId, 3, 3));

            session.Update();
            session.Update();
            session.Update();

            session.SetPlayerInput(session.LocalPlayerId, 2, new TestInputCommand(session.LocalPlayerId, 2, 20));
            session.RollbackTo(1);
            Assert.Equal(24, ReadState(session.PredictedFrame!).InputSum);

            session.SetPlayerInput(session.LocalPlayerId, 2, new TestInputCommand(session.LocalPlayerId, 2, 30));
            session.RollbackTo(1);
            Assert.Equal(34, ReadState(session.PredictedFrame!).InputSum);
        }

        [Fact]
        public void VerifyFrame_WhenNotRunning_Throws()
        {
            using var session = CreateSession();

            Assert.Throws<InvalidOperationException>(() => session.VerifyFrame(0, 0));
        }

        [Fact]
        public void RollbackTo_WhenNotRunning_Throws()
        {
            using var session = CreateSession();

            Assert.Throws<InvalidOperationException>(() => session.RollbackTo(0));
        }

        [Fact]
        public void Rewind_WhenNotRunning_Throws()
        {
            using var session = CreateSession();

            Assert.Throws<InvalidOperationException>(() => session.Rewind(1));
        }

        [Fact]
        public void RuntimeApis_AfterStop_ThrowForRunningOnlyOperations()
        {
            using var session = CreateSession();
            session.Start();
            session.Update();
            session.Stop();

            Assert.False(session.IsRunning);
            Assert.Throws<InvalidOperationException>(() => session.Update());
            Assert.Throws<InvalidOperationException>(() => session.VerifyFrame(0, 0));
            Assert.Throws<InvalidOperationException>(() => session.RollbackTo(0));
            Assert.Throws<InvalidOperationException>(() => session.Rewind(1));
        }

        [Fact]
        public void PublicApis_AfterDispose_ThrowObjectDisposed()
        {
            var session = CreateSession();
            session.Start();
            session.Update();
            SessionCheckpoint checkpoint = session.CreateCheckpoint();
            var system = new NoOpSystem();

            session.Dispose();

            Assert.Throws<ObjectDisposedException>(() => session.Start());
            Assert.Throws<ObjectDisposedException>(() => session.Stop());
            Assert.Throws<ObjectDisposedException>(() => session.Update());
            Assert.Throws<ObjectDisposedException>(() => session.VerifyFrame(0, 0));
            Assert.Throws<ObjectDisposedException>(() => session.RollbackTo(0));
            Assert.Throws<ObjectDisposedException>(() => session.Rewind(1));
            Assert.Throws<ObjectDisposedException>(() => session.CreateCheckpoint());
            Assert.Throws<ObjectDisposedException>(() => session.RestoreFromCheckpoint(checkpoint));
            Assert.Throws<ObjectDisposedException>(() => session.SetPlayerInput(0, 1, new TestInputCommand(0, 1, 1)));
            Assert.Throws<ObjectDisposedException>(() => session.GetPlayerInput(0, 1));
            Assert.Throws<ObjectDisposedException>(() => session.GetHistoricalFrame(0));
            Assert.Throws<ObjectDisposedException>(() => session.RegisterSystem(system));
            Assert.Throws<ObjectDisposedException>(() => session.UnregisterSystem(system));
        }

        [Fact]
        public void SetPlayerInput_Null_Throws()
        {
            using var session = CreateSession();

            Assert.Throws<ArgumentNullException>(() => session.SetPlayerInput(0, 1, null!));
        }

        [Fact]
        public void RestoreFromCheckpoint_Null_Throws()
        {
            using var session = CreateSession();

            Assert.Throws<ArgumentNullException>(() => session.RestoreFromCheckpoint(null!));
        }

        [Fact]
        public void HistoricalFrame_Update_InvalidatesMaterializedCache()
        {
            using var session = CreateSession();
            session.Start();

            for (int tick = 1; tick <= 32; tick++)
            {
                session.SetPlayerInput(session.LocalPlayerId, tick, new TestInputCommand(session.LocalPlayerId, tick, tick));
                session.Update();
            }

            Frame first = Assert.IsType<Frame>(session.GetHistoricalFrame(13));
            int afterFirstMaterializeCalls = session.UpdateSystemsCallCount;

            session.SetPlayerInput(session.LocalPlayerId, 33, new TestInputCommand(session.LocalPlayerId, 33, 33));
            session.Update();

            int afterLiveUpdateCalls = session.UpdateSystemsCallCount;
            Frame second = Assert.IsType<Frame>(session.GetHistoricalFrame(13));

            Assert.NotSame(first, second);
            Assert.True(session.UpdateSystemsCallCount > afterLiveUpdateCalls);
            Assert.True(afterLiveUpdateCalls >= afterFirstMaterializeCalls);
            Assert.Equal(ReadState(first), ReadState(second));
        }

        [Fact]
        public void HistoricalFrame_WarmCrossAnchorRead_MatchesColdRead()
        {
            using var session = CreateSession();
            session.Start();

            for (int tick = 1; tick <= 32; tick++)
            {
                session.SetPlayerInput(session.LocalPlayerId, tick, new TestInputCommand(session.LocalPlayerId, tick, tick));
                session.Update();
            }

            SessionState warm13 = ReadState(Assert.IsType<Frame>(session.GetHistoricalFrame(13)));
            SessionState warm14 = ReadState(Assert.IsType<Frame>(session.GetHistoricalFrame(14)));
            SessionState warm11 = ReadState(Assert.IsType<Frame>(session.GetHistoricalFrame(11)));

            session.ClearHistoricalMaterializeCache();

            SessionState cold11 = ReadState(Assert.IsType<Frame>(session.GetHistoricalFrame(11)));
            SessionState cold14 = ReadState(Assert.IsType<Frame>(session.GetHistoricalFrame(14)));

            Assert.Equal(new SessionState(13, 13, 91), warm13);
            Assert.Equal(new SessionState(14, 14, 105), warm14);
            Assert.Equal(warm11, cold11);
            Assert.Equal(warm14, cold14);
        }

        [Fact]
        public void HistoricalFrame_RollbackAfterWarmRead_RemainsCorrect()
        {
            using var session = CreateSession();
            session.Start();

            for (int tick = 1; tick <= 32; tick++)
            {
                session.SetPlayerInput(session.LocalPlayerId, tick, new TestInputCommand(session.LocalPlayerId, tick, tick));
                session.Update();
            }

            Assert.NotNull(session.GetHistoricalFrame(13));
            Assert.NotNull(session.GetHistoricalFrame(14));

            session.SetPlayerInput(session.LocalPlayerId, 20, new TestInputCommand(session.LocalPlayerId, 20, 100));
            session.RollbackTo(13);

            SessionState state = ReadState(session.PredictedFrame!);

            Assert.Equal(32, session.CurrentTick);
            Assert.Equal(32, state.Tick);
            Assert.Equal(32, state.StepCount);
            Assert.Equal(608, state.InputSum);
        }

        [Fact]
        public void InputBuffer_EvictsExpiredTicksWithinCapacityWindow()
        {
            var buffer = new InputBuffer(2);
            var first = new TestInputCommand(0, 1, 10);
            var second = new TestInputCommand(0, 2, 20);
            var third = new TestInputCommand(0, 3, 30);

            buffer.SetInput(1, first);
            buffer.SetInput(2, second);
            buffer.SetInput(3, third);

            Assert.Null(buffer.GetInput(1));
            Assert.Same(second, buffer.GetInput(2));
            Assert.Same(third, buffer.GetInput(3));
        }

        [Fact]
        public void InputBuffer_AcceptsOutOfOrderWritesWithinWindow()
        {
            var buffer = new InputBuffer(4);
            var tick4 = new TestInputCommand(0, 4, 40);
            var tick2 = new TestInputCommand(0, 2, 20);

            buffer.SetInput(4, tick4);
            buffer.SetInput(2, tick2);

            Assert.Same(tick2, buffer.GetInput(2));
            Assert.Same(tick4, buffer.GetInput(4));
        }

        [Fact]
        public void CreateCheckpoint_BeforeStart_Throws()
        {
            using var session = CreateSession();

            Assert.Throws<InvalidOperationException>(() => session.CreateCheckpoint());
        }

        [Fact]
        public void Update_WhenNotRunning_Throws()
        {
            using var session = CreateSession();

            Assert.Throws<InvalidOperationException>(() => session.Update());
        }

        [Fact]
        public void RegisterSystem_WhenRunning_ThrowsToKeepFrameStateConsistent()
        {
            using var session = CreateSession();
            session.Start();

            Assert.Throws<InvalidOperationException>(() => session.RegisterSystem(new NoOpSystem()));
        }

        [Fact]
        public void UnregisterSystem_WhenRunning_ThrowsToKeepFrameStateConsistent()
        {
            using var session = CreateSession();
            var system = new NoOpSystem();
            session.RegisterSystem(system);
            session.Start();

            Assert.Throws<InvalidOperationException>(() => session.UnregisterSystem(system));
        }

        private static Frame RestoreFrame(PackedFrameSnapshot snapshot)
        {
            var frame = new Frame(snapshot.EntityCapacity);
            frame.RestoreFromPackedSnapshot(snapshot, ComponentSerializationMode.Checkpoint);
            return frame;
        }

        private static TestSession CreateSession()
        {
            var session = new TestSession(FP.FromRaw(FP.Raw._0_016));
            session.RegisterSystem(new BootstrapSystem());
            return session;
        }

        private static AdvanceTrackingSession CreateAdvanceTrackingSession()
        {
            var session = new AdvanceTrackingSession(FP.FromRaw(FP.Raw._0_016));
            session.RegisterSystem(new BootstrapSystem());
            return session;
        }

        private static SessionState ReadState(Frame frame)
        {
            var counterEnumerator = frame.Query<TestCounterComponent>().GetEnumerator();
            Assert.True(counterEnumerator.MoveNext());

            var inputEnumerator = frame.Query<TestInputSumComponent>().GetEnumerator();
            Assert.True(inputEnumerator.MoveNext());

            return new SessionState(frame.Tick, counterEnumerator.Component.Value, inputEnumerator.Component.Value);
        }

        private readonly record struct SessionState(int Tick, int StepCount, int InputSum);

        private struct TestCounterComponent : IComponent
        {
            public int Value;
        }

        private struct TestInputSumComponent : IComponent
        {
            public int Value;
        }

        private sealed class BootstrapSystem : ISystem
        {
            public void OnInit(Frame frame)
            {
                EntityRef entity = frame.CreateEntity();
                frame.Add(entity, new TestCounterComponent());
                frame.Add(entity, new TestInputSumComponent());
            }

            public void OnUpdate(Frame frame, FP deltaTime)
            {
                var enumerator = frame.Query<TestCounterComponent>().GetEnumerator();
                while (enumerator.MoveNext())
                {
                    enumerator.Component.Value += 1;
                }
            }

            public void OnDestroy(Frame frame)
            {
            }
        }

        private sealed class TestSession : Session
        {
            public TestSession(FP deltaTime)
                : base(deltaTime)
            {
            }

            public int UpdateSystemsCallCount { get; private set; }

            public void ClearHistoricalMaterializeCache()
            {
                InvalidateHistoricalMaterializeCache();
            }

            protected override void ApplyInputs(Frame frame)
            {
                if (GetPlayerInput(LocalPlayerId, frame.Tick) is not TestInputCommand input)
                {
                    return;
                }

                var enumerator = frame.Query<TestInputSumComponent>().GetEnumerator();
                while (enumerator.MoveNext())
                {
                    enumerator.Component.Value += input.Value;
                }
            }

            protected override void UpdateSystems(Frame frame, FP deltaTime)
            {
                UpdateSystemsCallCount++;
                base.UpdateSystems(frame, deltaTime);
            }
        }

        private sealed class AdvanceTrackingSession : Session
        {
            public AdvanceTrackingSession(FP deltaTime)
                : base(deltaTime)
            {
            }

            public int AdvanceFrameCallCount { get; private set; }

            protected override Frame AdvanceFrame(Frame currentFrame)
            {
                AdvanceFrameCallCount++;
                return base.AdvanceFrame(currentFrame);
            }

            protected override void ApplyInputs(Frame frame)
            {
                if (GetPlayerInput(LocalPlayerId, frame.Tick) is not TestInputCommand input)
                {
                    return;
                }

                var enumerator = frame.Query<TestInputSumComponent>().GetEnumerator();
                while (enumerator.MoveNext())
                {
                    enumerator.Component.Value += input.Value;
                }
            }
        }

        private sealed class NoOpSystem : ISystem
        {
            public void OnInit(Frame frame)
            {
            }

            public void OnUpdate(Frame frame, FP deltaTime)
            {
            }

            public void OnDestroy(Frame frame)
            {
            }
        }

        private sealed class TestInputCommand : IInputCommand
        {
            public TestInputCommand()
            {
            }

            public TestInputCommand(int playerId, int tick, int value)
            {
                PlayerId = playerId;
                Tick = tick;
                Value = value;
            }

            public int PlayerId { get; private set; }

            public int Tick { get; private set; }

            public int Value { get; private set; }

            public byte[] Serialize()
            {
                return BitConverter.GetBytes(Value);
            }

            public void Deserialize(byte[] data)
            {
                Value = BitConverter.ToInt32(data, 0);
            }
        }
    }
}
