// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;
using System.Collections.Generic;
using Lattice.ECS.Core;
using Lattice.ECS.Framework;
using Lattice.Math;
using Xunit;

namespace Lattice.Tests.ECS
{
    public sealed class SystemSchedulerTests
    {
        [Fact]
        public void InitializeAndUpdate_RunSystemsInRegistrationOrder()
        {
            var events = new List<string>();
            using var frame = new Frame(8);
            var scheduler = new SystemScheduler();

            scheduler.Add(new RecordingSystem("A", events));
            scheduler.Add(new RecordingSystem("B", events));

            scheduler.Initialize(frame);
            scheduler.Update(frame, FP.One);

            Assert.Equal(
                new[]
                {
                    "A:init",
                    "B:init",
                    "A:enabled",
                    "B:enabled",
                    "A:update",
                    "B:update"
                },
                events);
        }

        [Fact]
        public void GroupUpdate_UsesDepthFirstRegistrationOrder()
        {
            var events = new List<string>();
            using var frame = new Frame(8);
            var scheduler = new SystemScheduler();

            var group = new SystemGroup(
                "Simulation",
                new RecordingSystem("Movement", events),
                new RecordingSystem("Lifetime", events));

            scheduler.Add(group);
            scheduler.Add(new RecordingSystem("Cleanup", events));

            scheduler.Initialize(frame);
            scheduler.Update(frame, FP.One);

            Assert.Equal(
                new[]
                {
                    "Movement:init",
                    "Lifetime:init",
                    "Cleanup:init",
                    "Movement:enabled",
                    "Lifetime:enabled",
                    "Cleanup:enabled",
                    "Movement:update",
                    "Lifetime:update",
                    "Cleanup:update"
                },
                events);
        }

        [Fact]
        public void DisabledByDefaultSystem_IsInitializedButSkippedUntilEnabled()
        {
            var events = new List<string>();
            using var frame = new Frame(8);
            var scheduler = new SystemScheduler();
            var disabled = new DisabledRecordingSystem("Disabled", events);

            scheduler.Add(disabled);
            scheduler.Initialize(frame);
            scheduler.Update(frame, FP.One);

            Assert.Equal(new[] { "Disabled:init" }, events);
            Assert.False(scheduler.IsEnabled(disabled));

            scheduler.Enable(disabled);
            scheduler.Update(frame, FP.One);

            Assert.Equal(
                new[]
                {
                    "Disabled:init",
                    "Disabled:enabled",
                    "Disabled:update"
                },
                events);
            Assert.True(scheduler.IsEnabled(disabled));
        }

        [Fact]
        public void DisableGroup_DisablesAllChildrenInHierarchy()
        {
            var events = new List<string>();
            using var frame = new Frame(8);
            var scheduler = new SystemScheduler();

            var child = new RecordingSystem("Child", events);
            var group = new SystemGroup("Group", child);

            scheduler.Add(group);
            scheduler.Initialize(frame);

            scheduler.Disable(group);
            scheduler.Update(frame, FP.One);

            Assert.Equal(
                new[]
                {
                    "Child:init",
                    "Child:enabled",
                    "Child:disabled"
                },
                events);

            scheduler.Enable(group);
            scheduler.Update(frame, FP.One);

            Assert.Equal(
                new[]
                {
                    "Child:init",
                    "Child:enabled",
                    "Child:disabled",
                    "Child:enabled",
                    "Child:update"
                },
                events);
        }

        [Fact]
        public void AddAfterInitialize_InitializesImmediatelyAndParticipatesInSubsequentUpdates()
        {
            var events = new List<string>();
            using var frame = new Frame(8);
            var scheduler = new SystemScheduler();

            scheduler.Add(new RecordingSystem("Existing", events));
            scheduler.Initialize(frame);

            scheduler.Add(new RecordingSystem("Late", events));
            scheduler.Update(frame, FP.One);

            Assert.Equal(
                new[]
                {
                    "Existing:init",
                    "Existing:enabled",
                    "Late:init",
                    "Late:enabled",
                    "Existing:update",
                    "Late:update"
                },
                events);
        }

        [Fact]
        public void DisableByType_DisablesAllMatchingSystemsUntilEnabledAgain()
        {
            var events = new List<string>();
            using var frame = new Frame(8);
            var scheduler = new SystemScheduler();

            scheduler.Add(new TaggedRecordingSystem("A", events));
            scheduler.Add(new TaggedRecordingSystem("B", events));

            scheduler.Initialize(frame);
            scheduler.Disable<TaggedRecordingSystem>();
            scheduler.Update(frame, FP.One);

            Assert.Equal(
                new[]
                {
                    "A:init",
                    "B:init",
                    "A:enabled",
                    "B:enabled",
                    "B:disabled",
                    "A:disabled"
                },
                events);
            Assert.False(scheduler.IsEnabled<TaggedRecordingSystem>());

            scheduler.Enable<TaggedRecordingSystem>();
            scheduler.Update(frame, FP.One);

            Assert.Equal(
                new[]
                {
                    "A:init",
                    "B:init",
                    "A:enabled",
                    "B:enabled",
                    "B:disabled",
                    "A:disabled",
                    "A:enabled",
                    "B:enabled",
                    "A:update",
                    "B:update"
                },
                events);
            Assert.True(scheduler.IsEnabled<TaggedRecordingSystem>());
        }

        [Fact]
        public void Dispose_CallsSystemsInReverseRegistrationOrder()
        {
            var events = new List<string>();
            using var frame = new Frame(8);
            var scheduler = new SystemScheduler();

            scheduler.Add(new RecordingSystem("A", events));
            scheduler.Add(new RecordingSystem("B", events));

            scheduler.Initialize(frame);
            scheduler.Dispose(frame);

            Assert.Equal(
                new[]
                {
                    "A:init",
                    "B:init",
                    "A:enabled",
                    "B:enabled",
                    "B:disabled",
                    "B:dispose",
                    "A:disabled",
                    "A:dispose"
                },
                events);
        }

        [Fact]
        public void UpdateBeforeInitialize_ThrowsInvalidOperationException()
        {
            using var frame = new Frame(8);
            var scheduler = new SystemScheduler();

            scheduler.Add(new RecordingSystem("A", new List<string>()));

            Assert.Throws<InvalidOperationException>(() => scheduler.Update(frame, FP.One));
        }

        [Fact]
        public void InitializeTwice_ThrowsInvalidOperationException()
        {
            using var frame = new Frame(8);
            var scheduler = new SystemScheduler();

            scheduler.Add(new RecordingSystem("A", new List<string>()));
            scheduler.Initialize(frame);

            Assert.Throws<InvalidOperationException>(() => scheduler.Initialize(frame));
        }

        [Fact]
        public void DisposeBeforeInitialize_ThrowsInvalidOperationException()
        {
            using var frame = new Frame(8);
            var scheduler = new SystemScheduler();

            scheduler.Add(new RecordingSystem("A", new List<string>()));

            Assert.Throws<InvalidOperationException>(() => scheduler.Dispose(frame));
        }

        [Fact]
        public void MutationDuringUpdate_ThrowsInvalidOperationException()
        {
            using var frame = new Frame(8);
            var scheduler = new SystemScheduler();
            var mutator = new MutatingSystem(scheduler);

            scheduler.Add(mutator);
            scheduler.Initialize(frame);

            Assert.Throws<InvalidOperationException>(() => scheduler.Update(frame, FP.One));
        }

        [Fact]
        public void GetNodes_ExportsStableTreeAndExecutionOrder()
        {
            using var frame = new Frame(8);
            var scheduler = new SystemScheduler();

            var movement = new RecordingSystem(
                "Movement",
                new List<string>(),
                new SystemMetadata(
                    Order: -10,
                    Kind: SystemExecutionKind.HotPath,
                    Category: "Simulation",
                    DebugCategory: "Simulation/Movement",
                    AllowRuntimeToggle: true));
            var lifetime = new RecordingSystem("Lifetime", new List<string>());
            var group = new SystemGroup("Simulation", movement, lifetime);
            var cleanup = new RecordingSystem(
                "Cleanup",
                new List<string>(),
                new SystemMetadata(
                    Order: 20,
                    Kind: SystemExecutionKind.MainThread,
                    Category: "Cleanup",
                    DebugCategory: "Cleanup/Final",
                    AllowRuntimeToggle: false));

            scheduler.Add(group);
            scheduler.Add(cleanup);
            scheduler.Initialize(frame);

            SystemNodeInfo[] nodes = scheduler.GetNodes();
            Assert.Equal(4, nodes.Length);
            Assert.Equal("Simulation", nodes[0].Name);
            Assert.True(nodes[0].IsGroup);
            Assert.True(nodes[0].IsRoot);
            Assert.True(nodes[0].Active);
            Assert.Equal(SystemExecutionKind.Group, nodes[0].Kind);

            Assert.Equal("Movement", nodes[1].Name);
            Assert.Equal("Simulation", nodes[1].ParentName);
            Assert.Equal(1, nodes[1].Depth);
            Assert.True(nodes[1].Active);
            Assert.Equal(-10, nodes[1].Order);
            Assert.Equal(SystemExecutionKind.HotPath, nodes[1].Kind);
            Assert.Equal("Simulation", nodes[1].Category);
            Assert.Equal("Simulation/Movement", nodes[1].DebugCategory);
            Assert.True(nodes[1].AllowRuntimeToggle);

            Assert.Equal("Cleanup", nodes[3].Name);
            Assert.True(nodes[3].IsRoot);
            Assert.Equal(0, nodes[3].Depth);
            Assert.Equal(20, nodes[3].Order);
            Assert.False(nodes[3].AllowRuntimeToggle);

            SystemNodeInfo[] execution = scheduler.GetExecutionOrder();
            Assert.Equal(new[] { "Movement", "Lifetime", "Cleanup" }, Array.ConvertAll(execution, static x => x.Name));

            Assert.True(scheduler.TryGetNode(cleanup, out SystemNodeInfo cleanupNode));
            Assert.Equal("Cleanup", cleanupNode.Name);

            SystemNodeInfo[] named = scheduler.GetNodes("Movement");
            Assert.Single(named);
            Assert.Equal("Movement", named[0].Name);

            SystemNodeInfo[] typed = scheduler.GetNodes<RecordingSystem>();
            Assert.Equal(3, typed.Length);
        }

        [Fact]
        public void ExecutionOrder_UsesMetadataOrderBeforeRegistrationOrder()
        {
            using var frame = new Frame(8);
            var scheduler = new SystemScheduler();
            var events = new List<string>();

            scheduler.Add(new RecordingSystem("Late", events, new SystemMetadata(10, SystemExecutionKind.MainThread, "Sim", null, true)));
            scheduler.Add(new RecordingSystem("First", events, new SystemMetadata(-20, SystemExecutionKind.MainThread, "Sim", null, true)));
            scheduler.Add(new RecordingSystem("TieA", events, new SystemMetadata(10, SystemExecutionKind.MainThread, "Sim", null, true)));

            scheduler.Initialize(frame);
            scheduler.Update(frame, FP.One);

            Assert.Equal(
                new[] { "First", "Late", "TieA" },
                Array.ConvertAll(scheduler.GetExecutionOrder(), static x => x.Name));
        }

        [Fact]
        public void GroupChildren_UseMetadataOrderBeforeRegistrationOrder()
        {
            using var frame = new Frame(8);
            var scheduler = new SystemScheduler();
            var events = new List<string>();

            var group = new SystemGroup(
                "Simulation",
                new RecordingSystem("B", events, new SystemMetadata(10, SystemExecutionKind.MainThread, "Sim", null, true)),
                new RecordingSystem("A", events, new SystemMetadata(-10, SystemExecutionKind.MainThread, "Sim", null, true)),
                new RecordingSystem("C", events, new SystemMetadata(10, SystemExecutionKind.MainThread, "Sim", null, true)));

            scheduler.Add(group);
            scheduler.Initialize(frame);
            scheduler.Update(frame, FP.One);

            Assert.Equal(
                new[] { "A", "B", "C" },
                Array.ConvertAll(scheduler.GetExecutionOrder(), static x => x.Name));
        }

        [Fact]
        public void RuntimeToggleDisabledSystem_RejectsEnableAndDisableAfterInitialize()
        {
            using var frame = new Frame(8);
            var scheduler = new SystemScheduler();
            var system = new DisabledRecordingSystem(
                "Locked",
                new List<string>(),
                new SystemMetadata(
                    Order: 0,
                    Kind: SystemExecutionKind.MainThread,
                    Category: "Locked",
                    DebugCategory: "Locked",
                    AllowRuntimeToggle: false));

            scheduler.Add(system);
            scheduler.Initialize(frame);

            Assert.Throws<InvalidOperationException>(() => scheduler.Enable(system));
            Assert.Throws<InvalidOperationException>(() => scheduler.Enable<DisabledRecordingSystem>());
        }

        [Fact]
        public void Trace_EmitsOrderedLifecycleEventsWithAccurateSnapshots()
        {
            using var frame = new Frame(8);
            var scheduler = new SystemScheduler();
            var traces = new List<SystemSchedulerTraceEvent>();

            scheduler.Trace = traceEvent =>
            {
                Assert.True(scheduler.CurrentSystem.HasValue);
                Assert.Equal(traceEvent.Name, scheduler.CurrentSystem.Value.Name);
                traces.Add(traceEvent);
            };

            scheduler.Add(
                new RecordingSystem(
                    "Tracked",
                    new List<string>(),
                    new SystemMetadata(
                        Order: -5,
                        Kind: SystemExecutionKind.HotPath,
                        Category: "Simulation",
                        DebugCategory: "Simulation/Tracked",
                        AllowRuntimeToggle: true)));

            scheduler.Initialize(frame);
            scheduler.Update(frame, FP.One);
            scheduler.Dispose(frame);

            Assert.Equal(
                new[]
                {
                    SystemSchedulerTracePhase.InitEnter,
                    SystemSchedulerTracePhase.InitExit,
                    SystemSchedulerTracePhase.EnabledEnter,
                    SystemSchedulerTracePhase.EnabledExit,
                    SystemSchedulerTracePhase.UpdateEnter,
                    SystemSchedulerTracePhase.UpdateExit,
                    SystemSchedulerTracePhase.DisabledEnter,
                    SystemSchedulerTracePhase.DisabledExit,
                    SystemSchedulerTracePhase.DisposeEnter,
                    SystemSchedulerTracePhase.DisposeExit
                },
                Array.ConvertAll(traces.ToArray(), static x => x.Phase));

            Assert.Equal(SystemSchedulerState.Initializing, traces[0].SchedulerState);
            Assert.Equal(SystemSchedulerState.Initializing, traces[3].SchedulerState);
            Assert.Equal(SystemSchedulerState.Updating, traces[4].SchedulerState);
            Assert.Equal(SystemSchedulerState.Disposing, traces[9].SchedulerState);

            Assert.False(traces[0].Initialized);
            Assert.True(traces[1].Initialized);
            Assert.False(traces[2].Active);
            Assert.True(traces[3].Active);
            Assert.True(traces[6].Active);
            Assert.False(traces[7].Active);
            Assert.True(traces[8].Initialized);
            Assert.False(traces[9].Initialized);

            Assert.Equal("Tracked", traces[0].Name);
            Assert.Equal("RecordingSystem", traces[0].TypeName);
            Assert.Equal(-5, traces[0].Order);
            Assert.Equal(SystemExecutionKind.HotPath, traces[0].Kind);
            Assert.Equal("Simulation", traces[0].Category);
            Assert.Equal("Simulation/Tracked", traces[0].DebugCategory);
            Assert.Equal(0, traces[0].Depth);
            Assert.False(traces[0].IsGroup);
            Assert.False(scheduler.CurrentSystem.HasValue);
        }

        [Fact]
        public void ParallelReservedSystem_IsRejectedDuringRegistration()
        {
            var scheduler = new SystemScheduler();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => scheduler.Add(new ParallelReservedRecordingSystem()));

            Assert.Contains("ParallelReserved", exception.Message, StringComparison.Ordinal);
        }

        private class RecordingSystem : SystemBase
        {
            private readonly string _name;
            private readonly List<string> _events;
            private readonly SystemMetadata _metadata;

            public RecordingSystem(string name, List<string> events, SystemMetadata? metadata = null)
            {
                _name = name;
                _events = events;
                _metadata = metadata ?? SystemMetadata.Default;
            }

            public override string Name => _name;

            public override SystemMetadata Metadata => _metadata;

            public override void OnInit(Frame frame)
            {
                _events.Add($"{_name}:init");
            }

            public override void OnEnabled(Frame frame)
            {
                _events.Add($"{_name}:enabled");
            }

            public override void OnDisabled(Frame frame)
            {
                _events.Add($"{_name}:disabled");
            }

            public override void OnUpdate(Frame frame, FP deltaTime)
            {
                _events.Add($"{_name}:update");
            }

            public override void OnDispose(Frame frame)
            {
                _events.Add($"{_name}:dispose");
            }
        }

        private sealed class DisabledRecordingSystem : RecordingSystem
        {
            public DisabledRecordingSystem(string name, List<string> events, SystemMetadata? metadata = null)
                : base(name, events, metadata)
            {
            }

            public override bool EnabledByDefault => false;
        }

        private sealed class TaggedRecordingSystem : RecordingSystem
        {
            public TaggedRecordingSystem(string name, List<string> events)
                : base(name, events)
            {
            }
        }

        private sealed class MutatingSystem : SystemBase
        {
            private readonly SystemScheduler _scheduler;

            public MutatingSystem(SystemScheduler scheduler)
            {
                _scheduler = scheduler;
            }

            public override void OnUpdate(Frame frame, FP deltaTime)
            {
                _scheduler.Disable(this);
            }
        }

        private sealed class ParallelReservedRecordingSystem : SystemBase
        {
            public override SystemMetadata Metadata => new(
                Order: 0,
                Kind: SystemExecutionKind.ParallelReserved,
                Category: "Parallel",
                DebugCategory: "Parallel/Reserved",
                AllowRuntimeToggle: true);

            public override void OnUpdate(Frame frame, FP deltaTime)
            {
            }
        }
    }
}
