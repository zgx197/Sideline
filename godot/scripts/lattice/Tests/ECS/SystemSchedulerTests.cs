using System;
using System.Collections.Generic;
using Lattice.ECS.Core;
using Lattice.ECS.Framework;
using Lattice.Math;
using Xunit;

namespace Lattice.Tests.ECS
{
    public class SystemSchedulerTests
    {
        [Fact]
        public void Update_ExecutesSystemsByPhaseThenRegistrationOrder()
        {
            using var frame = new Frame(8);
            var events = new List<string>();
            var scheduler = new SystemScheduler();

            scheduler.Add(new RecordingSystem("resolve", events, SystemPhase.Resolve));
            scheduler.Add(new RecordingSystem("simulation", events, SystemPhase.Simulation));
            scheduler.Add(new RecordingSystem("input", events, SystemPhase.Input));
            scheduler.Add(new RecordingSystem("cleanup", events, SystemPhase.Cleanup));
            scheduler.Add(new RecordingSystem("pre", events, SystemPhase.PreSimulation));

            scheduler.Initialize(frame);
            scheduler.Update(frame, FP.Zero);

            Assert.Equal(
                new[]
                {
                    "init:input",
                    "init:pre",
                    "init:simulation",
                    "init:resolve",
                    "init:cleanup",
                    "update:input",
                    "update:pre",
                    "update:simulation",
                    "update:resolve",
                    "update:cleanup"
                },
                events);
        }

        [Fact]
        public void Boundary_ExposesFlatPhasedOrderedContract()
        {
            var scheduler = new SystemScheduler();

            SystemSchedulerBoundary boundary = scheduler.Boundary;

            Assert.Equal(SystemSchedulerKind.FlatPhasedOrdered, boundary.SchedulerKind);
            Assert.True(boundary.Supports(SystemSchedulerCapability.OrderedExecution));
            Assert.True(boundary.Supports(SystemSchedulerCapability.ExplicitLifecycle));
            Assert.True(boundary.Supports(SystemSchedulerCapability.StaticRegistrationBeforeInitialize));
            Assert.True(boundary.Supports(SystemSchedulerCapability.PhasedExecution));
            Assert.True(boundary.Supports(SystemSchedulerCapability.AuthoringContractValidation));
            Assert.True(boundary.ExplicitlyDoesNotSupport(UnsupportedSystemSchedulerCapability.RuntimeMutation));
            Assert.True(boundary.ExplicitlyDoesNotSupport(UnsupportedSystemSchedulerCapability.EnableDisable));
            Assert.True(boundary.ExplicitlyDoesNotSupport(UnsupportedSystemSchedulerCapability.DependencyOrdering));
            Assert.True(boundary.ExplicitlyDoesNotSupport(UnsupportedSystemSchedulerCapability.HierarchicalGrouping));
            Assert.True(boundary.ExplicitlyDoesNotSupport(UnsupportedSystemSchedulerCapability.ThreadedExecution));
        }

        [Fact]
        public void Update_PreservesRegistrationOrderWithinSamePhase()
        {
            using var frame = new Frame(8);
            var events = new List<string>();
            var scheduler = new SystemScheduler();

            scheduler.Add(new RecordingSystem("sim-a", events, SystemPhase.Simulation));
            scheduler.Add(new RecordingSystem("sim-b", events, SystemPhase.Simulation));
            scheduler.Add(new RecordingSystem("resolve-a", events, SystemPhase.Resolve));
            scheduler.Add(new RecordingSystem("resolve-b", events, SystemPhase.Resolve));

            scheduler.Initialize(frame);
            scheduler.Update(frame, FP.Zero);

            Assert.Equal(
                new[]
                {
                    "init:sim-a",
                    "init:sim-b",
                    "init:resolve-a",
                    "init:resolve-b",
                    "update:sim-a",
                    "update:sim-b",
                    "update:resolve-a",
                    "update:resolve-b"
                },
                events);
        }

        [Fact]
        public void Initialize_AndShutdown_AreIdempotent()
        {
            using var frame = new Frame(8);
            var system = new CountingSystem();
            var scheduler = new SystemScheduler();

            scheduler.Add(system);

            scheduler.Initialize(frame);
            scheduler.Initialize(frame);
            scheduler.Shutdown(frame);
            scheduler.Shutdown(frame);
            scheduler.Initialize(frame);

            Assert.Equal(2, system.InitCount);
            Assert.Equal(1, system.DestroyCount);
        }

        [Fact]
        public void Lifecycle_UsesSamePhaseOrderForShutdown()
        {
            using var frame = new Frame(8);
            var events = new List<string>();
            var scheduler = new SystemScheduler();

            scheduler.Add(new RecordingSystem("resolve", events, SystemPhase.Resolve));
            scheduler.Add(new RecordingSystem("input", events, SystemPhase.Input));
            scheduler.Add(new RecordingSystem("simulation", events, SystemPhase.Simulation));

            scheduler.Initialize(frame);
            scheduler.Update(frame, FP.Zero);
            scheduler.Shutdown(frame);

            Assert.Equal(
                new[]
                {
                    "init:input",
                    "init:simulation",
                    "init:resolve",
                    "update:input",
                    "update:simulation",
                    "update:resolve",
                    "destroy:input",
                    "destroy:simulation",
                    "destroy:resolve"
                },
                events);
        }

        [Fact]
        public void Remove_BeforeInitialize_PreventsFutureLifecycle()
        {
            using var frame = new Frame(8);
            var events = new List<string>();
            var scheduler = new SystemScheduler();
            var first = new RecordingSystem("A", events);
            var second = new RecordingSystem("B", events);

            scheduler.Add(first);
            scheduler.Add(second);

            bool removed = scheduler.Remove(first);

            scheduler.Initialize(frame);
            scheduler.Update(frame, FP.Zero);

            Assert.True(removed);
            Assert.Equal(
                new[]
                {
                    "init:B",
                    "update:B"
                },
                events);
        }

        [Fact]
        public void Add_RejectsNullAndDuplicateInstance()
        {
            var scheduler = new SystemScheduler();
            var system = new CountingSystem();

            Assert.Throws<ArgumentNullException>(() => scheduler.Add(null!));

            scheduler.Add(system);

            Assert.Throws<InvalidOperationException>(() => scheduler.Add(system));
        }

        [Fact]
        public void Add_RejectsWritableGlobalSystemsOutsideAllowedPhases()
        {
            var scheduler = new SystemScheduler();
            var system = new ContractSystem(
                SystemPhase.Simulation,
                new SystemAuthoringContract(
                    SystemFrameAccess.ReadWrite,
                    SystemGlobalAccess.ReadWrite));

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => scheduler.Add(system));

            Assert.Contains("global", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Update_RejectsStructuralChangesWithoutExplicitContract()
        {
            using var frame = new Frame(8);
            var scheduler = new SystemScheduler();
            scheduler.Add(new StructuralMutationSystem(SystemAuthoringContract.Default));
            scheduler.Initialize(frame);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => scheduler.Update(frame, FP.Zero));

            Assert.Contains(nameof(Frame.CreateEntity), exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Update_AllowsDeclaredStructuralChanges()
        {
            using var frame = new Frame(8);
            var scheduler = new SystemScheduler();
            scheduler.Add(new StructuralMutationSystem(new SystemAuthoringContract(
                SystemFrameAccess.ReadWrite,
                SystemGlobalAccess.None,
                SystemStructuralChangeAccess.Deferred)));
            scheduler.Initialize(frame);

            scheduler.Update(frame, FP.Zero);

            Assert.Equal(1, frame.EntityCount);
        }

        [Fact]
        public void Update_RejectsGlobalReadsWithoutExplicitContract()
        {
            ComponentRegistry.Register<SchedulerGlobalStateComponent>(ComponentFlags.Singleton, ComponentCallbacks.Empty);

            using var frame = new Frame(8);
            frame.SetGlobal(new SchedulerGlobalStateComponent { Value = 7 });

            var scheduler = new SystemScheduler();
            scheduler.Add(new GlobalProbeSystem(
                new SystemAuthoringContract(SystemFrameAccess.ReadWrite),
                static currentFrame => currentFrame.TryGetGlobal(out SchedulerGlobalStateComponent _)));
            scheduler.Initialize(frame);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => scheduler.Update(frame, FP.Zero));

            Assert.Contains(nameof(Frame.TryGetGlobal), exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Update_AllowsReadOnlyGlobalAccessThroughTryGetGlobal()
        {
            ComponentRegistry.Register<SchedulerGlobalStateComponent>(ComponentFlags.Singleton, ComponentCallbacks.Empty);

            using var frame = new Frame(8);
            frame.SetGlobal(new SchedulerGlobalStateComponent { Value = 11 });

            int observedValue = 0;
            var scheduler = new SystemScheduler();
            scheduler.Add(new GlobalProbeSystem(
                new SystemAuthoringContract(
                    SystemFrameAccess.ReadOnly,
                    SystemGlobalAccess.ReadOnly),
                currentFrame =>
                {
                    Assert.True(currentFrame.TryGetGlobal(out SchedulerGlobalStateComponent state));
                    observedValue = state.Value;
                }));
            scheduler.Initialize(frame);

            scheduler.Update(frame, FP.Zero);

            Assert.Equal(11, observedValue);
        }

        [Fact]
        public void Update_RejectsGlobalWritesWhenContractIsReadOnly()
        {
            ComponentRegistry.Register<SchedulerGlobalStateComponent>(ComponentFlags.Singleton, ComponentCallbacks.Empty);

            using var frame = new Frame(8);
            frame.SetGlobal(new SchedulerGlobalStateComponent { Value = 3 });

            var scheduler = new SystemScheduler();
            scheduler.Add(new GlobalProbeSystem(
                new SystemAuthoringContract(
                    SystemFrameAccess.ReadWrite,
                    SystemGlobalAccess.ReadOnly),
                currentFrame => currentFrame.SetGlobal(new SchedulerGlobalStateComponent { Value = 9 })));
            scheduler.Initialize(frame);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => scheduler.Update(frame, FP.Zero));

            Assert.Contains(nameof(Frame.SetGlobal), exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ISystem_DefaultPhase_IsSimulation()
        {
            ISystem system = new CountingSystem();

            Assert.Equal(SystemPhase.Simulation, system.Phase);
        }

        [Fact]
        public void ISystem_DefaultContract_DoesNotAllowGlobalsOrStructuralChanges()
        {
            ISystem system = new CountingSystem();

            Assert.Equal(SystemAuthoringContract.Default, system.Contract);
            Assert.False(system.Contract.AllowsGlobalReads);
            Assert.False(system.Contract.AllowsGlobalWrites);
            Assert.False(system.Contract.AllowsStructuralChanges);
        }

        [Fact]
        public void Update_BeforeInitialize_Throws()
        {
            using var frame = new Frame(8);
            var scheduler = new SystemScheduler();
            scheduler.Add(new CountingSystem());

            Assert.Throws<InvalidOperationException>(() => scheduler.Update(frame, FP.Zero));
        }

        [Fact]
        public void Add_WhenInitialized_RequiresShutdownFirst()
        {
            using var frame = new Frame(8);
            var scheduler = new SystemScheduler();
            scheduler.Add(new CountingSystem());
            scheduler.Initialize(frame);

            Assert.Throws<InvalidOperationException>(() => scheduler.Add(new CountingSystem()));
        }

        [Fact]
        public void Remove_WhenInitialized_RequiresShutdownFirst()
        {
            using var frame = new Frame(8);
            var scheduler = new SystemScheduler();
            var system = new CountingSystem();
            scheduler.Add(system);
            scheduler.Initialize(frame);

            Assert.Throws<InvalidOperationException>(() => scheduler.Remove(system));
        }

        [Fact]
        public void Clear_WhenInitialized_RequiresShutdownFirst()
        {
            using var frame = new Frame(8);
            var scheduler = new SystemScheduler();
            var system = new CountingSystem();

            scheduler.Add(system);
            scheduler.Initialize(frame);

            Assert.Throws<InvalidOperationException>(() => scheduler.Clear());

            scheduler.Shutdown(frame);
            scheduler.Clear();

            Assert.Equal(0, scheduler.Count);
            Assert.Equal(1, system.InitCount);
            Assert.Equal(1, system.DestroyCount);
        }

        private sealed class RecordingSystem : ISystem
        {
            private readonly string _name;
            private readonly List<string> _events;
            private readonly SystemPhase _phase;

            public RecordingSystem(string name, List<string> events, SystemPhase phase = SystemPhase.Simulation)
            {
                _name = name;
                _events = events;
                _phase = phase;
            }

            public SystemPhase Phase => _phase;

            public void OnInit(Frame frame)
            {
                _events.Add($"init:{_name}");
            }

            public void OnUpdate(Frame frame, FP deltaTime)
            {
                _events.Add($"update:{_name}");
            }

            public void OnDestroy(Frame frame)
            {
                _events.Add($"destroy:{_name}");
            }
        }

        private sealed class CountingSystem : ISystem
        {
            public int InitCount { get; private set; }

            public int UpdateCount { get; private set; }

            public int DestroyCount { get; private set; }

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
                DestroyCount++;
            }
        }

        private sealed class ContractSystem : ISystem
        {
            private readonly SystemPhase _phase;
            private readonly SystemAuthoringContract _contract;

            public ContractSystem(SystemPhase phase, SystemAuthoringContract contract)
            {
                _phase = phase;
                _contract = contract;
            }

            public SystemPhase Phase => _phase;

            public SystemAuthoringContract Contract => _contract;

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

        private sealed class StructuralMutationSystem : ISystem
        {
            private readonly SystemAuthoringContract _contract;

            public StructuralMutationSystem(SystemAuthoringContract contract)
            {
                _contract = contract;
            }

            public SystemAuthoringContract Contract => _contract;

            public void OnInit(Frame frame)
            {
            }

            public void OnUpdate(Frame frame, FP deltaTime)
            {
                frame.CreateEntity();
            }

            public void OnDestroy(Frame frame)
            {
            }
        }

        private sealed class GlobalProbeSystem : ISystem
        {
            private readonly SystemAuthoringContract _contract;
            private readonly Action<Frame> _probe;

            public GlobalProbeSystem(SystemAuthoringContract contract, Action<Frame> probe)
            {
                _contract = contract;
                _probe = probe;
            }

            public SystemAuthoringContract Contract => _contract;

            public void OnInit(Frame frame)
            {
            }

            public void OnUpdate(Frame frame, FP deltaTime)
            {
                _probe(frame);
            }

            public void OnDestroy(Frame frame)
            {
            }
        }

        private struct SchedulerGlobalStateComponent : IComponent
        {
            public int Value;
        }
    }
}
