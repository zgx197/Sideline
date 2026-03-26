// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;
using Lattice.Core;
using Lattice.ECS.Core;
using Lattice.ECS.Framework;
using Lattice.Math;
using Xunit;

namespace Lattice.Tests.ECS
{
    public sealed class SystemIntegrationTests
    {
        static SystemIntegrationTests()
        {
            ComponentRegistry.EnsureRegistered<Position>();
            ComponentRegistry.EnsureRegistered<Velocity>();
            ComponentRegistry.EnsureRegistered<Lifetime>();
        }

        [Fact]
        public void FilterSystem_UpdatesOnlyMatchingEntities()
        {
            using var frame = new Frame(16);
            var scheduler = new SystemScheduler();

            EntityRef moving = frame.CreateEntity();
            frame.Add(moving, new Position { Value = FP.One });
            frame.Add(moving, new Velocity { Value = FP.One });

            EntityRef staticEntity = frame.CreateEntity();
            frame.Add(staticEntity, new Position { Value = FP._10 });

            scheduler.Add(new MovementFilterSystem());
            scheduler.Initialize(frame);
            scheduler.Update(frame, FP.One);

            Assert.Equal(FP._2, frame.Get<Position>(moving).Value);
            Assert.Equal(FP._10, frame.Get<Position>(staticEntity).Value);
        }

        [Fact]
        public void CommandBuffer_PlaybackSupportsCreateSetAndRemoveForTemporaryEntity()
        {
            using var frame = new Frame(16);
            CommandBuffer commandBuffer = default;
            commandBuffer.Initialize(frame);

            EntityRef tempEntity = commandBuffer.CreateEntity();
            commandBuffer.AddComponent(tempEntity, new Position { Value = FP.One });
            commandBuffer.SetComponent(tempEntity, new Position { Value = FP._3 });
            commandBuffer.AddComponent(tempEntity, new Velocity { Value = FP._2 });

            commandBuffer.Playback(frame);

            EntityRef resolved = GetSingleEntityWith<Position>(frame);
            Assert.True(frame.Has<Velocity>(resolved));
            Assert.Equal(FP._3, frame.Get<Position>(resolved).Value);

            commandBuffer.SetComponent(resolved, new Position { Value = FP._5 });
            commandBuffer.RemoveComponent<Velocity>(resolved);
            commandBuffer.Playback(frame);

            Assert.Equal(FP._5, frame.Get<Position>(resolved).Value);
            Assert.False(frame.Has<Velocity>(resolved));

            commandBuffer.Dispose();
        }

        [Fact]
        public void CommandBuffer_PlaybackPreservesEntityVersionForReusedSlot()
        {
            using var frame = new Frame(16);
            CommandBuffer commandBuffer = default;
            commandBuffer.Initialize(frame);

            EntityRef first = frame.CreateEntity();
            frame.DestroyEntity(first);

            EntityRef reused = frame.CreateEntity();
            Assert.Equal(first.Index, reused.Index);
            Assert.NotEqual(first.Version, reused.Version);

            commandBuffer.AddComponent(reused, new Position { Value = FP.FromRaw(FP.Raw._7) });
            commandBuffer.Playback(frame);

            Assert.True(frame.Has<Position>(reused));
            Assert.Equal(FP.FromRaw(FP.Raw._7), frame.Get<Position>(reused).Value);

            commandBuffer.Dispose();
        }

        [Fact]
        public void DeferredStructuralChanges_AreAppliedByPlaybackAfterSystemUpdate()
        {
            using var frame = new Frame(16);
            var host = new CommandBufferHost();
            host.Buffer.Initialize(frame);
            var scheduler = new SystemScheduler();

            EntityRef entity = frame.CreateEntity();
            frame.Add(entity, new Position { Value = FP.One });
            frame.Add(entity, new Velocity { Value = FP.One });
            frame.Add(entity, new Lifetime { Remaining = FP.One });

            scheduler.Add(new MovementFilterSystem());
            scheduler.Add(new LifetimeCleanupSystem(host));

            scheduler.Initialize(frame);
            scheduler.Update(frame, FP.One);

            Assert.True(frame.IsValid(entity));
            Assert.Equal(FP._2, frame.Get<Position>(entity).Value);
            Assert.False(host.Buffer.IsEmpty);

            host.Buffer.Playback(frame);

            Assert.False(frame.IsValid(entity));
            host.Buffer.Dispose();
        }

        private static unsafe EntityRef GetSingleEntityWith<T>(Frame frame) where T : unmanaged, IComponent
        {
            var iterator = frame.GetComponentBlockIterator<T>();
            Assert.True(iterator.Next(out EntityRef entity, out T* _));
            Assert.False(iterator.Next(out _, out _));
            return entity;
        }

        private struct Position : IComponent
        {
            public FP Value;
        }

        private struct Velocity : IComponent
        {
            public FP Value;
        }

        private struct Lifetime : IComponent
        {
            public FP Remaining;
        }

        private sealed class MovementFilterSystem : SystemBase
        {
            public override unsafe void OnUpdate(Frame frame, FP deltaTime)
            {
                var enumerator = frame.Query<Position, Velocity>().GetEnumerator();

                while (enumerator.MoveNext())
                {
                    enumerator.Component1.Value += enumerator.Component2.Value * deltaTime;
                }
            }
        }

        private sealed class LifetimeCleanupSystem : SystemBase
        {
            private readonly CommandBufferHost _host;

            public LifetimeCleanupSystem(CommandBufferHost host)
            {
                _host = host;
            }

            public override unsafe void OnUpdate(Frame frame, FP deltaTime)
            {
                var iterator = frame.GetComponentBlockIterator<Lifetime>();
                while (iterator.Next(out EntityRef entity, out Lifetime* lifetime))
                {
                    lifetime->Remaining -= deltaTime;
                    if (lifetime->Remaining <= FP.Zero)
                    {
                        _host.Buffer.DestroyEntity(entity);
                    }
                }
            }
        }

        private sealed class CommandBufferHost
        {
            public CommandBuffer Buffer;
        }
    }
}
