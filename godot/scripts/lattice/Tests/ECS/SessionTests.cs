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
            using var session = new MinimalPredictionSession(options);

            Assert.Same(options, session.RuntimeOptions);
            Assert.Equal(FP.FromRaw(FP.Raw._0_016), session.DeltaTime);
            Assert.Equal(5, session.LocalPlayerId);
        }

        [Fact]
        public void RuntimeBoundary_ExposesMinimalPredictionContract()
        {
            using SessionRuntime session = new MinimalPredictionSession(FP.One);

            SessionRuntimeBoundary boundary = session.RuntimeBoundary;

            Assert.Equal(SessionRuntimeKind.MinimalPrediction, boundary.RuntimeKind);
            Assert.True(boundary.Supports(SessionRuntimeCapability.LocalPredictionStep));
            Assert.True(boundary.Supports(SessionRuntimeCapability.PredictionVerification));
            Assert.True(boundary.Supports(SessionRuntimeCapability.LocalRewind));
            Assert.True(boundary.Supports(SessionRuntimeCapability.CheckpointRestore));
            Assert.True(boundary.ExplicitlyDoesNotSupport(UnsupportedSessionRuntimeCapability.ThreadedScheduling));
            Assert.True(boundary.ExplicitlyDoesNotSupport(UnsupportedSessionRuntimeCapability.ResourceConfiguredBoot));
            Assert.True(boundary.ExplicitlyDoesNotSupport(UnsupportedSessionRuntimeCapability.FullNetworkSession));
            Assert.True(boundary.ExplicitlyDoesNotSupport(UnsupportedSessionRuntimeCapability.PlayerMappingAndTransport));
        }

        [Fact]
        public void RuntimeBoundary_ExposesLocalAuthoritativeContract()
        {
            using SessionRuntime session = new LocalAuthoritativeSession(FP.One);

            SessionRuntimeBoundary boundary = session.RuntimeBoundary;

            Assert.Equal(SessionRuntimeKind.LocalAuthoritative, boundary.RuntimeKind);
            Assert.True(boundary.Supports(SessionRuntimeCapability.LocalPredictionStep));
            Assert.True(boundary.Supports(SessionRuntimeCapability.CheckpointRestore));
            Assert.False(boundary.Supports(SessionRuntimeCapability.PredictionVerification));
            Assert.False(boundary.Supports(SessionRuntimeCapability.LocalRewind));
            Assert.True(boundary.ExplicitlyDoesNotSupport(UnsupportedSessionRuntimeCapability.ThreadedScheduling));
            Assert.True(boundary.ExplicitlyDoesNotSupport(UnsupportedSessionRuntimeCapability.ResourceConfiguredBoot));
            Assert.True(boundary.ExplicitlyDoesNotSupport(UnsupportedSessionRuntimeCapability.FullNetworkSession));
            Assert.True(boundary.ExplicitlyDoesNotSupport(UnsupportedSessionRuntimeCapability.PlayerMappingAndTransport));
        }

        [Fact]
        public void DataBoundary_ExposesCurrentInputHistoryContract()
        {
            using SessionRuntime session = new MinimalPredictionSession(FP.One);

            SessionRuntimeDataBoundary boundary = session.DataBoundary;

            Assert.Equal(SessionInputStorageKind.PlayerTickFixedWindow, boundary.InputStorageKind);
            Assert.Equal(SessionHistoryStorageKind.BoundedLiveFramesWithSampledSnapshots, boundary.HistoryStorageKind);
            Assert.Equal(SessionCheckpointStorageKind.PackedSnapshot, boundary.CheckpointStorageKind);
            Assert.True(boundary.Supports(SessionRuntimeDataCapability.PlayerTickInputLookup));
            Assert.True(boundary.Supports(SessionRuntimeDataCapability.BoundedInputRetention));
            Assert.True(boundary.Supports(SessionRuntimeDataCapability.TickAddressableHistory));
            Assert.True(boundary.Supports(SessionRuntimeDataCapability.SampledHistoryMaterialization));
            Assert.True(boundary.Supports(SessionRuntimeDataCapability.ExplicitCheckpointRestore));
            Assert.True(boundary.Supports(SessionRuntimeDataCapability.PackedCheckpointStorage));
            Assert.True(boundary.ExplicitlyDoesNotSupport(UnsupportedSessionRuntimeDataCapability.ConfigurableInputRetention));
            Assert.True(boundary.ExplicitlyDoesNotSupport(UnsupportedSessionRuntimeDataCapability.ConfigurableHistoryRetention));
            Assert.True(boundary.ExplicitlyDoesNotSupport(UnsupportedSessionRuntimeDataCapability.ConfigurableSnapshotSampling));
            Assert.True(boundary.ExplicitlyDoesNotSupport(UnsupportedSessionRuntimeDataCapability.PluggableHistoryStore));
            Assert.True(boundary.ExplicitlyDoesNotSupport(UnsupportedSessionRuntimeDataCapability.AlternativeCheckpointFormats));
            Assert.True(boundary.ExplicitlyDoesNotSupport(UnsupportedSessionRuntimeDataCapability.UnboundedPerTickRetention));
        }

        [Fact]
        public void InputBoundary_ExposesFormalInputContract()
        {
            using SessionRuntime session = new MinimalPredictionSession(FP.One);

            SessionRuntimeInputBoundary boundary = session.InputBoundary;

            Assert.Equal(SessionInputContractKind.TickScopedPlayerInputSet, boundary.ContractKind);
            Assert.Equal(SessionMissingInputPolicy.OmitMissingPlayers, boundary.MissingInputPolicy);
            Assert.Equal(SessionInputWritePolicy.LatestWriteWins, boundary.WritePolicy);
            Assert.Equal(SessionInputOrder.PlayerIdAscending, boundary.PlayerOrder);
            Assert.True(boundary.Supports(SessionRuntimeInputCapability.TickScopedInputAggregation));
            Assert.True(boundary.Supports(SessionRuntimeInputCapability.SparsePlayerInput));
            Assert.True(boundary.Supports(SessionRuntimeInputCapability.StablePlayerOrdering));
            Assert.True(boundary.Supports(SessionRuntimeInputCapability.LatestWriteWins));
            Assert.True(boundary.Supports(SessionRuntimeInputCapability.TransportDecoupledInputModel));
            Assert.True(boundary.ExplicitlyDoesNotSupport(UnsupportedSessionRuntimeInputCapability.ImplicitPreviousInputCarryForward));
            Assert.True(boundary.ExplicitlyDoesNotSupport(UnsupportedSessionRuntimeInputCapability.BuiltInDefaultInputSynthesis));
            Assert.True(boundary.ExplicitlyDoesNotSupport(UnsupportedSessionRuntimeInputCapability.ConfigurableInputAggregation));
            Assert.True(boundary.ExplicitlyDoesNotSupport(UnsupportedSessionRuntimeInputCapability.ConfigurableMissingInputPolicy));
            Assert.True(boundary.ExplicitlyDoesNotSupport(UnsupportedSessionRuntimeInputCapability.BuiltInTransportSerialization));
        }

        [Fact]
        public void TickPipeline_ExposesPhasedStructuralCommitContract()
        {
            using SessionRuntime session = new MinimalPredictionSession(FP.One);

            SessionTickPipelineBoundary boundary = session.TickPipeline;

            Assert.Equal(SessionTickPipelineKind.PhasedStructuralCommit, boundary.PipelineKind);
            Assert.True(boundary.Supports(SessionTickPipelineCapability.ExplicitStageExposure));
            Assert.True(boundary.Supports(SessionTickPipelineCapability.DeferredStructuralChanges));
            Assert.True(boundary.Supports(SessionTickPipelineCapability.StructuralCommitStage));
            Assert.True(boundary.Supports(SessionTickPipelineCapability.CleanupAndHistoryStages));
            Assert.True(boundary.Supports(SessionTickPipelineCapability.ImmediateComponentMutationDuringSimulation));
            Assert.True(boundary.ExplicitlyDoesNotSupport(UnsupportedSessionTickPipelineCapability.ImmediateStructuralVisibilityDuringSimulation));
            Assert.True(boundary.ExplicitlyDoesNotSupport(UnsupportedSessionTickPipelineCapability.RuntimeReorderedStages));
            Assert.True(boundary.ExplicitlyDoesNotSupport(UnsupportedSessionTickPipelineCapability.PerSystemStructuralCommit));
            Assert.Equal(SessionTickStage.Idle, session.CurrentTickStage);
        }

        [Fact]
        public void RuntimeContext_ExposesStandaloneRuntimeMetadata()
        {
            using SessionRuntime session = new MinimalPredictionSession(new SessionRuntimeOptions(FP.One, 6));

            Assert.Same(session, session.Context.Runtime);
            Assert.Equal(nameof(MinimalPredictionSession), session.Context.RunnerName);
            Assert.Equal(6, session.Context.RuntimeOptions.LocalPlayerId);
            Assert.Equal(SessionRuntimeKind.MinimalPrediction, session.Context.RuntimeKind);
            Assert.Same(session.RuntimeBoundary, session.Context.RuntimeBoundary);
            Assert.Equal(SessionRuntimeContextKind.TypedRuntimeSharedServices, session.Context.Boundary.ContextKind);
            Assert.True(session.Context.Boundary.Supports(SessionRuntimeContextCapability.TypedSharedLookup));
            Assert.True(session.Context.Boundary.Supports(SessionRuntimeContextCapability.ExplicitRuntimeSharedServiceRegistration));
            Assert.True(session.Context.Boundary.Supports(SessionRuntimeContextCapability.OwnedSharedObjectLifetime));
            Assert.True(session.Context.Boundary.Supports(SessionRuntimeContextCapability.RuntimeMetadataExposure));
            Assert.True(session.Context.Boundary.ExplicitlyDoesNotSupport(UnsupportedSessionRuntimeContextCapability.ArbitraryObjectBag));
            Assert.True(session.Context.Boundary.ExplicitlyDoesNotSupport(UnsupportedSessionRuntimeContextCapability.RollbackStateStorage));
            Assert.True(session.Context.Boundary.ExplicitlyDoesNotSupport(UnsupportedSessionRuntimeContextCapability.GameplayStateExchange));
            Assert.True(session.Context.Boundary.ExplicitlyDoesNotSupport(UnsupportedSessionRuntimeContextCapability.StringKeyedServiceLocator));
        }

        [Fact]
        public void RuntimeContext_ExposesStandaloneRuntimeMetadata_ForLocalAuthoritativeRuntime()
        {
            using SessionRuntime session = new LocalAuthoritativeSession(new SessionRuntimeOptions(FP.One, 8));

            Assert.Same(session, session.Context.Runtime);
            Assert.Equal(nameof(LocalAuthoritativeSession), session.Context.RunnerName);
            Assert.Equal(8, session.Context.RuntimeOptions.LocalPlayerId);
            Assert.Equal(SessionRuntimeKind.LocalAuthoritative, session.Context.RuntimeKind);
            Assert.Same(session.RuntimeBoundary, session.Context.RuntimeBoundary);
        }

        [Fact]
        public void Update_UsesFormalTickStages_AndReturnsToIdle()
        {
            using var session = new TickStageTrackingSession(FP.One);
            session.RegisterSystem(new NoOpSystem());
            session.Start();

            session.Update();

            Assert.Equal(SessionTickStage.InputApply, session.ApplyInputsStage);
            Assert.Equal(SessionTickStage.Simulation, session.UpdateSystemsStage);
            Assert.Equal(SessionTickStage.Cleanup, session.CleanupStage);
            Assert.Equal(SessionTickStage.HistoryCapture, session.HistoryCaptureStage);
            Assert.Equal(SessionTickStage.Cleanup, session.FrameUpdateStage);
            Assert.Equal(SessionTickStage.Idle, session.CurrentTickStage);
        }

        [Fact]
        public void StructuralChanges_DuringSimulation_AreCommittedAfterSimulation()
        {
            var spawnSystem = new DeferredSpawnSystem();
            var probeSystem = new DeferredVisibilityProbeSystem();

            using var session = new MinimalPredictionSession(FP.One);
            session.RegisterSystem(spawnSystem);
            session.RegisterSystem(probeSystem);
            session.Start();

            int frameUpdateVisibleCount = -1;
            session.OnFrameUpdate += (frame, _) => frameUpdateVisibleCount = CountDeferredSpawned(frame);

            SeedDeferredSpawnController(session, remainingCount: 1);

            session.Update();

            Assert.Equal(0, probeSystem.LastVisibleCount);
            Assert.Equal(1, frameUpdateVisibleCount);
            Assert.Equal(1, CountDeferredSpawned(session.PredictedFrame!));

            session.Update();

            Assert.Equal(1, probeSystem.LastVisibleCount);
            Assert.Equal(1, CountDeferredSpawned(session.PredictedFrame!));
        }

        [Fact]
        public void RollbackTo_ReusesStructuralCommitPipelineDuringResimulate()
        {
            using var session = new MinimalPredictionSession(FP.One);
            session.RegisterSystem(new DeferredSpawnSystem());
            session.RegisterSystem(new DeferredVisibilityProbeSystem());
            session.Start();

            SeedDeferredSpawnController(session, remainingCount: 2);

            session.Update();
            session.Update();

            int countBeforeRollback = CountDeferredSpawned(session.PredictedFrame!);
            Assert.Equal(2, countBeforeRollback);

            session.RollbackTo(1);

            Assert.Equal(SessionTickStage.Idle, session.CurrentTickStage);
            Assert.Equal(2, CountDeferredSpawned(session.PredictedFrame!));
        }

        [Fact]
        public void RuntimeContext_CanStoreAndRetrieveSharedObjects()
        {
            using SessionRuntime session = new MinimalPredictionSession(FP.One);
            var shared = new TestSharedState(7);

            session.Context.SetShared(shared);

            Assert.True(session.Context.TryGetShared<TestSharedState>(out TestSharedState? read));
            Assert.Same(shared, read);
            Assert.Same(shared, session.Context.GetRequiredShared<TestSharedState>());
            Assert.Equal(1, session.Context.SharedObjectCount);

            Assert.True(session.Context.RemoveShared<TestSharedState>());
            Assert.False(session.Context.TryGetShared<TestSharedState>(out _));
            Assert.Equal(0, session.Context.SharedObjectCount);
        }

        [Fact]
        public void RuntimeContext_RejectsUnmarkedSharedObjects()
        {
            using SessionRuntime session = new MinimalPredictionSession(FP.One);

            Assert.Throws<ArgumentException>(() => session.Context.SetShared(new InvalidSharedState(7)));
        }

        [Fact]
        public void RuntimeContext_RejectsRollbackStateCarriers_EvenWhenMarkedAsSharedServices()
        {
            using SessionRuntime session = new MinimalPredictionSession(FP.One);

            Assert.Throws<ArgumentException>(() => session.Context.SetShared(new SharedFrame(4)));
            Assert.Throws<ArgumentException>(() => session.Context.SetShared(new SharedCheckpoint()));
            Assert.Throws<ArgumentException>(() => session.Context.SetShared(new SharedRuntime(FP.One)));
        }

        [Fact]
        public void RuntimeContext_DisposesOwnedSharedObjects_WhenRuntimeIsDisposed()
        {
            var session = new MinimalPredictionSession(FP.One);
            var shared = new DisposableSharedState();
            session.Context.SetShared(shared, disposeWithContext: true);

            session.Dispose();

            Assert.True(shared.IsDisposed);
        }

        [Fact]
        public void SessionRuntime_DirectDerivedType_CanReuseSharedLifecycle()
        {
            using var session = new DerivedRuntimeSession(FP.One);
            session.RegisterSystem(new BootstrapSystem());

            session.Start();
            session.Update();

            Assert.True(session.IsRunning);
            Assert.Equal(1, session.CurrentTick);
            Assert.Equal(SessionRuntimeKind.MinimalPrediction, session.RuntimeKind);
        }

        [Fact]
        public void LocalAuthoritativeRuntime_CapabilityGuard_RejectsPredictionApis_AndKeepsCheckpointRestore()
        {
            using var session = new LocalAuthoritativeSession(FP.One);
            session.RegisterSystem(new BootstrapSystem());
            session.Start();
            session.Update();

            Assert.Equal(1, session.CurrentTick);
            SessionCheckpoint checkpoint = session.CreateCheckpoint();
            session.Update();
            Assert.Equal(2, session.CurrentTick);
            session.RestoreFromCheckpoint(checkpoint);
            Assert.Equal(1, session.CurrentTick);
            Assert.Throws<NotSupportedException>(() => session.VerifyFrame(0, 0));
            Assert.Throws<NotSupportedException>(() => session.RollbackTo(0));
            Assert.Throws<NotSupportedException>(() => session.Rewind(1));
        }

        [Fact]
        public void SetPlayerInput_MismatchedInputIdentity_Throws()
        {
            using var session = CreateSession();

            Assert.Throws<ArgumentException>(() => session.SetPlayerInput(1, 2, new TestInputCommand(3, 2, 5)));
            Assert.Throws<ArgumentException>(() => session.SetPlayerInput(1, 2, new TestInputCommand(1, 4, 5)));
        }

        [Fact]
        public void ApplyInputSet_UsesSparseSortedLatestWriteWinsContract()
        {
            using var session = CreateInputSetTrackingSession();
            session.Start();

            session.SetPlayerInput(5, 1, new TestInputCommand(5, 1, 50));
            session.SetPlayerInput(2, 1, new TestInputCommand(2, 1, 20));
            session.SetPlayerInput(2, 1, new TestInputCommand(2, 1, 22));

            session.Update();

            Assert.Equal(SessionTickStage.InputApply, session.ApplyInputSetStage);
            Assert.Equal(1, session.LastInputTick);
            Assert.Equal(new[] { 2, 5 }, session.LastPlayerIds);
            Assert.Equal(new[] { 22, 50 }, session.LastValues);
            Assert.True(session.MissingPlayerWasOmitted);
            Assert.Equal(72, ReadState(session.PredictedFrame!).InputSum);
        }

        [Fact]
        public void VerifyFrame_WhenChecksumMatches_UpdatesVerifiedFrameOnly()
        {
            using var session = CreateSession();
            session.Start();

            session.SetPlayerInput(session.LocalPlayerId, 1, new TestInputCommand(session.LocalPlayerId, 1, 2));
            session.Update();

            SessionState predictedBeforeVerify = ReadState(session.PredictedFrame!);
            Frame historical = Assert.IsType<Frame>(session.GetHistoricalFrame(1));
            long checksum = (long)historical.CalculateChecksum(ComponentSerializationMode.Checkpoint);

            session.VerifyFrame(1, checksum);

            Assert.Equal(predictedBeforeVerify, ReadState(session.PredictedFrame!));
            Assert.Equal(ReadState(historical), ReadState(session.VerifiedFrame!));
            Assert.Equal(1, session.CurrentTick);
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

            for (int i = 0; i < SessionRuntimeDataDefaults.HistorySize; i++)
            {
                session.Update();
            }

            Assert.Null(session.GetHistoricalFrame(0));
            Assert.NotNull(session.GetHistoricalFrame(1));
            Assert.NotNull(session.GetHistoricalFrame(SessionRuntimeDataDefaults.HistorySize));
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
        public void CreateCheckpoint_CapturesFormalProtocolMetadata()
        {
            using var session = CreateSession();
            session.Start();

            session.SetPlayerInput(session.LocalPlayerId, 1, new TestInputCommand(session.LocalPlayerId, 1, 5));
            session.Update();

            SessionCheckpoint checkpoint = session.CreateCheckpoint();

            Assert.Equal(SessionCheckpointProtocol.CurrentVersion, checkpoint.Protocol.Version);
            Assert.Equal(SessionCheckpointStorageKind.PackedSnapshot, checkpoint.Protocol.CheckpointStorageKind);
            Assert.Equal(SessionInputProtocol.CurrentContractVersion, checkpoint.Protocol.InputContract.ContractVersion);
            Assert.Equal(session.InputBoundary.ContractKind, checkpoint.Protocol.InputContract.ContractKind);
            Assert.Equal(PackedFrameSnapshot.CurrentFormatVersion, checkpoint.Protocol.PackedSnapshotFormatVersion);
            Assert.True(checkpoint.Protocol.ComponentSchema.IsSpecified);
            Assert.Equal(ComponentSerializationMode.Checkpoint, checkpoint.Protocol.ComponentSchema.SerializationMode);
            Assert.Equal(checkpoint.Protocol.ComponentSchema, checkpoint.VerifiedSnapshot!.SchemaManifest);
            Assert.Equal(checkpoint.Protocol.ComponentSchema, checkpoint.PredictedSnapshot!.SchemaManifest);
        }

        [Fact]
        public void RestoreFromCheckpoint_WhenInputContractVersionMismatches_Throws()
        {
            using var session = CreateSession();
            session.Start();
            session.Update();

            SessionCheckpoint checkpoint = session.CreateCheckpoint();
            SessionInputContractStamp inputContract = checkpoint.Protocol.InputContract;
            checkpoint.Protocol = new SessionCheckpointProtocol(
                checkpoint.Protocol.Version,
                checkpoint.Protocol.CheckpointStorageKind,
                new SessionInputContractStamp(
                    inputContract.ContractVersion + 1,
                    inputContract.ContractKind,
                    inputContract.MissingInputPolicy,
                    inputContract.WritePolicy,
                    inputContract.PlayerOrder),
                checkpoint.Protocol.PackedSnapshotFormatVersion,
                checkpoint.Protocol.ComponentSchema);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => session.RestoreFromCheckpoint(checkpoint));
            Assert.Contains("Input contract protocol mismatch", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void RestoreFromCheckpoint_WhenPackedSnapshotFormatVersionMismatches_Throws()
        {
            using var session = CreateSession();
            session.Start();
            session.Update();

            SessionCheckpoint checkpoint = session.CreateCheckpoint();
            checkpoint.Protocol = new SessionCheckpointProtocol(
                checkpoint.Protocol.Version,
                checkpoint.Protocol.CheckpointStorageKind,
                checkpoint.Protocol.InputContract,
                checkpoint.Protocol.PackedSnapshotFormatVersion + 1,
                checkpoint.Protocol.ComponentSchema);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => session.RestoreFromCheckpoint(checkpoint));
            Assert.Contains("packed snapshot format version", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void RestoreFromPackedSnapshot_WhenSchemaFingerprintMismatches_Throws()
        {
            using var session = CreateSession();
            session.Start();
            session.SetPlayerInput(session.LocalPlayerId, 1, new TestInputCommand(session.LocalPlayerId, 1, 3));
            session.Update();

            PackedFrameSnapshot snapshot = session.PredictedFrame!.CapturePackedSnapshot(ComponentSerializationMode.Checkpoint);
            ComponentSchemaManifest schema = snapshot.SchemaManifest;
            var incompatibleSnapshot = new PackedFrameSnapshot(
                snapshot.Tick,
                snapshot.EntityCapacity,
                snapshot.Data,
                snapshot.Length,
                snapshot.FormatVersion,
                new ComponentSchemaManifest(
                    schema.SerializationMode,
                    schema.Fingerprint + 1,
                    schema.SerializedComponentCount,
                    schema.Version));

            using var restored = new Frame(snapshot.EntityCapacity);
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => restored.RestoreFromPackedSnapshot(incompatibleSnapshot, ComponentSerializationMode.Checkpoint));

            Assert.Contains("component schema fingerprint", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void VersionedInputPayloadCodec_RequiresExactFormatMatch()
        {
            var expectedCodec = new TestInputPayloadCodec(new SessionInputPayloadFormat("test-input", 1));
            var actualCodec = new TestInputPayloadCodec(new SessionInputPayloadFormat("test-input", 2));

            SessionInputProtocol.EnsurePayloadCompatible(expectedCodec.PayloadFormat, expectedCodec.PayloadFormat, "self");

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => SessionInputProtocol.EnsurePayloadCompatible(expectedCodec.PayloadFormat, actualCodec.PayloadFormat, "payload replay"));

            Assert.Contains("payload replay", exception.Message, StringComparison.Ordinal);
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
        public void PublicApis_BeforeStart_ExposeDefinedNonRunningBehavior()
        {
            using var session = CreateSession();
            var system = new NoOpSystem();
            var input = new TestInputCommand(0, 1, 9);

            session.RegisterSystem(system);
            session.UnregisterSystem(system);

            session.SetPlayerInput(0, 1, input);

            Assert.Same(input, session.GetPlayerInput(0, 1));
            Assert.Null(session.GetHistoricalFrame(0));
            Assert.False(session.IsRunning);

            Assert.Throws<InvalidOperationException>(() => session.Update());
            Assert.Throws<InvalidOperationException>(() => session.VerifyFrame(0, 0));
            Assert.Throws<InvalidOperationException>(() => session.RollbackTo(0));
            Assert.Throws<InvalidOperationException>(() => session.Rewind(1));
            Assert.Throws<InvalidOperationException>(() => session.CreateCheckpoint());
        }

        [Fact]
        public void PublicApis_AfterStop_PreserveDefinedNonRunningBehavior()
        {
            using var session = CreateSession();
            session.Start();

            session.SetPlayerInput(session.LocalPlayerId, 1, new TestInputCommand(session.LocalPlayerId, 1, 3));
            session.SetPlayerInput(session.LocalPlayerId, 2, new TestInputCommand(session.LocalPlayerId, 2, 4));
            session.Update();
            session.Update();

            SessionState expectedBeforeStop = ReadState(session.PredictedFrame!);
            SessionCheckpoint checkpoint = session.CreateCheckpoint();
            Frame historicalBeforeStop = Assert.IsType<Frame>(session.GetHistoricalFrame(1));

            session.Stop();

            Assert.False(session.IsRunning);
            Assert.Equal(expectedBeforeStop, ReadState(session.PredictedFrame!));
            Assert.Equal(new SessionState(1, 1, 3), ReadState(historicalBeforeStop));
            Assert.Equal(4, ((TestInputCommand)session.GetPlayerInput(session.LocalPlayerId, 2)!).Value);

            Assert.Throws<InvalidOperationException>(() => session.Update());
            Assert.Throws<InvalidOperationException>(() => session.VerifyFrame(0, 0));
            Assert.Throws<InvalidOperationException>(() => session.RollbackTo(0));
            Assert.Throws<InvalidOperationException>(() => session.Rewind(1));

            session.RestoreFromCheckpoint(checkpoint);

            Assert.Equal(expectedBeforeStop, ReadState(session.PredictedFrame!));
            Assert.Equal(2, session.CurrentTick);
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
        public void VerifyFrame_WithNegativeTick_Throws()
        {
            using var session = CreateSession();
            session.Start();

            Assert.Throws<ArgumentOutOfRangeException>(() => session.VerifyFrame(-1, 0));
        }

        [Fact]
        public void VerifyFrame_WithFutureTick_Throws()
        {
            using var session = CreateSession();
            session.Start();
            session.Update();

            Assert.Throws<ArgumentOutOfRangeException>(() => session.VerifyFrame(2, 0));
        }

        [Fact]
        public void RollbackTo_WithNegativeTick_Throws()
        {
            using var session = CreateSession();
            session.Start();

            Assert.Throws<ArgumentOutOfRangeException>(() => session.RollbackTo(-1));
        }

        [Fact]
        public void RollbackTo_WithFutureTick_Throws()
        {
            using var session = CreateSession();
            session.Start();
            session.Update();

            Assert.Throws<ArgumentOutOfRangeException>(() => session.RollbackTo(2));
        }

        [Fact]
        public void Rewind_WhenNotRunning_Throws()
        {
            using var session = CreateSession();

            Assert.Throws<InvalidOperationException>(() => session.Rewind(1));
        }

        [Fact]
        public void Rewind_WithNegativeFrameCount_Throws()
        {
            using var session = CreateSession();
            session.Start();

            Assert.Throws<ArgumentOutOfRangeException>(() => session.Rewind(-1));
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
        public void RestoreFromCheckpoint_AfterStop_DoesNotRestartSession()
        {
            using var session = CreateSession();
            session.Start();
            session.SetPlayerInput(session.LocalPlayerId, 1, new TestInputCommand(session.LocalPlayerId, 1, 5));
            session.Update();

            SessionCheckpoint checkpoint = session.CreateCheckpoint();
            session.Stop();

            session.RestoreFromCheckpoint(checkpoint);

            Assert.False(session.IsRunning);
            Assert.Equal(1, session.CurrentTick);
            Assert.Equal(new SessionState(1, 1, 5), ReadState(session.PredictedFrame!));
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
        public void HistoricalFrame_SparseInputsAcrossWindow_RollbackRemainsStable()
        {
            using var session = CreateSession();
            session.Start();

            var sparseInputs = new (int Tick, int Value)[]
            {
                (2, 5),
                (7, 7),
                (9, 11),
                (20, 13),
                (30, 17)
            };

            int expectedOriginalSum = 0;
            for (int tick = 1; tick <= 32; tick++)
            {
                int value = 0;
                for (int i = 0; i < sparseInputs.Length; i++)
                {
                    if (sparseInputs[i].Tick == tick)
                    {
                        value = sparseInputs[i].Value;
                        break;
                    }
                }

                expectedOriginalSum += value;
                if (value != 0)
                {
                    session.SetPlayerInput(session.LocalPlayerId, tick, new TestInputCommand(session.LocalPlayerId, tick, value));
                }

                session.Update();
            }

            Assert.Equal(expectedOriginalSum, ReadState(session.PredictedFrame!).InputSum);

            SessionState warm13 = ReadState(Assert.IsType<Frame>(session.GetHistoricalFrame(13)));
            SessionState warm14 = ReadState(Assert.IsType<Frame>(session.GetHistoricalFrame(14)));
            Assert.Equal(new SessionState(13, 13, 23), warm13);
            Assert.Equal(new SessionState(14, 14, 23), warm14);

            session.SetPlayerInput(session.LocalPlayerId, 20, new TestInputCommand(session.LocalPlayerId, 20, 100));
            session.RollbackTo(13);

            SessionState replayed = ReadState(session.PredictedFrame!);

            Assert.Equal(32, session.CurrentTick);
            Assert.Equal(32, replayed.Tick);
            Assert.Equal(32, replayed.StepCount);
            Assert.Equal(140, replayed.InputSum);
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

        private static InputSetTrackingSession CreateInputSetTrackingSession()
        {
            var session = new InputSetTrackingSession(FP.FromRaw(FP.Raw._0_016));
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

        private static int CountDeferredSpawned(Frame frame)
        {
            int count = 0;
            var enumerator = frame.Query<DeferredSpawnedTag>().GetEnumerator();
            while (enumerator.MoveNext())
            {
                count++;
            }

            return count;
        }

        private static void SeedDeferredSpawnController(MinimalPredictionSession session, int remainingCount)
        {
            Assert.NotNull(session.VerifiedFrame);
            Assert.NotNull(session.PredictedFrame);

            EntityRef verifiedController = session.VerifiedFrame!.CreateEntity();
            session.VerifiedFrame.Add(verifiedController, new DeferredSpawnController { RemainingCount = remainingCount });

            EntityRef predictedController = session.PredictedFrame!.CreateEntity();
            session.PredictedFrame.Add(predictedController, new DeferredSpawnController { RemainingCount = remainingCount });

            Assert.Equal(verifiedController, predictedController);
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

        private struct DeferredSpawnController : IComponent
        {
            public int RemainingCount;
        }

        private struct DeferredSpawnedTag : IComponent
        {
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

        private sealed class TestSession : MinimalPredictionSession
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

            protected override void ApplyInputSet(Frame frame, in SessionInputSet inputSet)
            {
                if (!inputSet.TryGetPlayerInput(LocalPlayerId, out TestInputCommand? input))
                {
                    return;
                }

                Assert.NotNull(input);

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

        private sealed class TickStageTrackingSession : MinimalPredictionSession
        {
            public TickStageTrackingSession(FP deltaTime)
                : base(deltaTime)
            {
                OnFrameUpdate += (_, _) => FrameUpdateStage = CurrentTickStage;
            }

            public SessionTickStage ApplyInputsStage { get; private set; }

            public SessionTickStage UpdateSystemsStage { get; private set; }

            public SessionTickStage CleanupStage { get; private set; }

            public SessionTickStage HistoryCaptureStage { get; private set; }

            public SessionTickStage FrameUpdateStage { get; private set; }

            protected override void ApplyInputSet(Frame frame, in SessionInputSet inputSet)
            {
                ApplyInputsStage = CurrentTickStage;
            }

            protected override void UpdateSystems(Frame frame, FP deltaTime)
            {
                UpdateSystemsStage = CurrentTickStage;
                base.UpdateSystems(frame, deltaTime);
            }

            protected override void CleanupFrame(Frame frame)
            {
                CleanupStage = CurrentTickStage;
            }

            protected override void CaptureHistory(Frame frame)
            {
                HistoryCaptureStage = CurrentTickStage;
                base.CaptureHistory(frame);
            }
        }

        private sealed class AdvanceTrackingSession : MinimalPredictionSession
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

            protected override void ApplyInputSet(Frame frame, in SessionInputSet inputSet)
            {
                if (!inputSet.TryGetPlayerInput(LocalPlayerId, out TestInputCommand? input))
                {
                    return;
                }

                Assert.NotNull(input);

                var enumerator = frame.Query<TestInputSumComponent>().GetEnumerator();
                while (enumerator.MoveNext())
                {
                    enumerator.Component.Value += input.Value;
                }
            }
        }

        private sealed class DerivedRuntimeSession : SessionRuntime
        {
            private static readonly SessionRuntimeBoundary Boundary = new(
                SessionRuntimeKind.MinimalPrediction,
                SessionRuntimeCapability.LocalPredictionStep |
                SessionRuntimeCapability.PredictionVerification |
                SessionRuntimeCapability.LocalRewind |
                SessionRuntimeCapability.CheckpointRestore,
                UnsupportedSessionRuntimeCapability.ThreadedScheduling |
                UnsupportedSessionRuntimeCapability.ResourceConfiguredBoot |
                UnsupportedSessionRuntimeCapability.FullNetworkSession |
                UnsupportedSessionRuntimeCapability.PlayerMappingAndTransport);

            public DerivedRuntimeSession(FP deltaTime)
                : base(deltaTime)
            {
            }

            public override SessionRuntimeBoundary RuntimeBoundary => Boundary;
        }

        private sealed class InputSetTrackingSession : MinimalPredictionSession
        {
            public InputSetTrackingSession(FP deltaTime)
                : base(deltaTime)
            {
            }

            public SessionTickStage ApplyInputSetStage { get; private set; }

            public int LastInputTick { get; private set; }

            public List<int> LastPlayerIds { get; } = new();

            public List<int> LastValues { get; } = new();

            public bool MissingPlayerWasOmitted { get; private set; }

            protected override void ApplyInputSet(Frame frame, in SessionInputSet inputSet)
            {
                ApplyInputSetStage = CurrentTickStage;
                LastInputTick = inputSet.Tick;
                LastPlayerIds.Clear();
                LastValues.Clear();

                var enumerator = inputSet.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    Assert.True(enumerator.Current.TryGet(out TestInputCommand? input));
                    Assert.NotNull(input);
                    LastPlayerIds.Add(input.PlayerId);
                    LastValues.Add(input.Value);
                }

                MissingPlayerWasOmitted = !inputSet.TryGetPlayerInput<TestInputCommand>(3, out _);

                var inputSumEnumerator = frame.Query<TestInputSumComponent>().GetEnumerator();
                while (inputSumEnumerator.MoveNext())
                {
                    for (int i = 0; i < LastValues.Count; i++)
                    {
                        inputSumEnumerator.Component.Value += LastValues[i];
                    }
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

        private sealed class DeferredSpawnSystem : ISystem
        {
            public SystemAuthoringContract Contract => new(
                SystemFrameAccess.ReadWrite,
                SystemGlobalAccess.None,
                SystemStructuralChangeAccess.Deferred);

            public void OnInit(Frame frame)
            {
            }

            public void OnUpdate(Frame frame, FP deltaTime)
            {
                var enumerator = frame.Query<DeferredSpawnController>().GetEnumerator();
                while (enumerator.MoveNext())
                {
                    if (enumerator.Component.RemainingCount <= 0)
                    {
                        continue;
                    }

                    enumerator.Component.RemainingCount--;
                    EntityRef entity = frame.CreateEntity();
                    frame.Add(entity, new DeferredSpawnedTag());
                }
            }

            public void OnDestroy(Frame frame)
            {
            }
        }

        private sealed class DeferredVisibilityProbeSystem : ISystem
        {
            public int LastVisibleCount { get; private set; }

            public void OnInit(Frame frame)
            {
            }

            public void OnUpdate(Frame frame, FP deltaTime)
            {
                LastVisibleCount = CountDeferredSpawned(frame);
            }

            public void OnDestroy(Frame frame)
            {
            }
        }

        private sealed class TestSharedState : ISessionRuntimeSharedService
        {
            public TestSharedState(int value)
            {
                Value = value;
            }

            public int Value { get; }
        }

        private sealed class InvalidSharedState
        {
            public InvalidSharedState(int value)
            {
                Value = value;
            }

            public int Value { get; }
        }

        private sealed unsafe class SharedFrame : Frame, ISessionRuntimeSharedService
        {
            public SharedFrame(int entityCapacity)
                : base(entityCapacity)
            {
            }
        }

        private sealed class SharedCheckpoint : SessionCheckpoint, ISessionRuntimeSharedService
        {
        }

        private sealed class SharedRuntime : MinimalPredictionSession, ISessionRuntimeSharedService
        {
            public SharedRuntime(FP deltaTime)
                : base(deltaTime)
            {
            }
        }

        private sealed class DisposableSharedState : IDisposable, ISessionRuntimeSharedService
        {
            public bool IsDisposed { get; private set; }

            public void Dispose()
            {
                IsDisposed = true;
            }
        }

        private sealed class TestInputCommand : IPlayerInput
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
        }

        private sealed class TestInputPayloadCodec : IVersionedInputPayloadCodec<TestInputCommand>
        {
            public TestInputPayloadCodec(SessionInputPayloadFormat payloadFormat)
            {
                PayloadFormat = payloadFormat;
            }

            public SessionInputPayloadFormat PayloadFormat { get; }

            public byte[] Serialize(TestInputCommand input)
            {
                ArgumentNullException.ThrowIfNull(input);
                return BitConverter.GetBytes(input.Value);
            }

            public TestInputCommand Deserialize(int playerId, int tick, byte[] payload)
            {
                ArgumentNullException.ThrowIfNull(payload);
                int value = payload.Length >= sizeof(int) ? BitConverter.ToInt32(payload, 0) : 0;
                return new TestInputCommand(playerId, tick, value);
            }
        }
    }
}
