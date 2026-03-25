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
        public void Update_ExecutesSystemsInRegistrationOrder()
        {
            using var frame = new Frame(8);
            var events = new List<string>();
            var scheduler = new SystemScheduler();

            scheduler.Add(new RecordingSystem("A", events));
            scheduler.Add(new RecordingSystem("B", events));
            scheduler.Add(new RecordingSystem("C", events));

            scheduler.Initialize(frame);
            scheduler.Update(frame, FP.Zero);

            Assert.Equal(
                new[]
                {
                    "init:A",
                    "init:B",
                    "init:C",
                    "update:A",
                    "update:B",
                    "update:C"
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
        public void Remove_PreventsFurtherUpdates()
        {
            using var frame = new Frame(8);
            var events = new List<string>();
            var scheduler = new SystemScheduler();
            var first = new RecordingSystem("A", events);
            var second = new RecordingSystem("B", events);

            scheduler.Add(first);
            scheduler.Add(second);
            scheduler.Initialize(frame);
            scheduler.Update(frame, FP.Zero);

            bool removed = scheduler.Remove(first);
            scheduler.Update(frame, FP.Zero);

            Assert.True(removed);
            Assert.Equal(
                new[]
                {
                    "init:A",
                    "init:B",
                    "update:A",
                    "update:B",
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
        public void Update_BeforeInitialize_Throws()
        {
            using var frame = new Frame(8);
            var scheduler = new SystemScheduler();
            scheduler.Add(new CountingSystem());

            Assert.Throws<InvalidOperationException>(() => scheduler.Update(frame, FP.Zero));
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

            public RecordingSystem(string name, List<string> events)
            {
                _name = name;
                _events = events;
            }

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
    }
}
