// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;
using Lattice.Core;
using Lattice.ECS.Core;
using Lattice.ECS.Core.Parallel;
using Lattice.ECS.Framework;
using Lattice.ECS.Serialization;
using Lattice.Math;
using Xunit;
using CoreBitStream = Lattice.ECS.Core.BitStream;

namespace Lattice.Tests.ECS
{
    public unsafe class ComponentIntegrationTests
    {
        private static int _onAddedCount;
        private static int _onRemovedCount;
        private static EntityRef _lastLifecycleEntity;
        private static int _lastLifecycleValue;
        private static int _queryValueAccumulator;
        private static int _queryPairAccumulator;
        private static int _queryQuadAccumulator;
        private static int _packedSerializeCount;

        private struct MetadataComponent : IComponent
        {
            public int Value;
        }

        private struct LifecycleComponent : IComponent
        {
            public int Value;
        }

        private struct ReusedSlotComponent : IComponent
        {
            public int Value;
        }

        private struct DeferredRemovalComponent : IComponent
        {
            public int Value;
        }

        private struct SerializedComponent : IComponent
        {
            public int Value;
            public EntityRef Owner;
        }

        private struct PredictionExcludedComponent : IComponent
        {
            public int Value;
        }

        private struct CommandBufferComponent : IComponent
        {
            public int Value;
        }

        private struct CheckpointExcludedComponent : IComponent
        {
            public int Value;
        }

        private struct RawSnapshotComponent : IComponent
        {
            public int Value;
            public int Bonus;
        }

        private unsafe struct LargeAutoBlockComponent : IComponent
        {
            public fixed long Values[8];
        }

        private struct QueryPrimaryComponent : IComponent
        {
            public int Value;
        }

        private struct QuerySecondaryComponent : IComponent
        {
            public int Value;
        }

        private struct QueryTertiaryComponent : IComponent
        {
            public int Value;
        }

        private struct QueryQuaternaryComponent : IComponent
        {
            public int Value;
        }

        private struct GlobalStateComponent : IComponent
        {
            public int Value;
        }

        private struct GlobalAuxiliaryComponent : IComponent
        {
            public int Value;
        }

        private struct DenseIndexComponent : IComponent
        {
            public int Value;
        }

        private struct PackedCountedSerializedComponent : IComponent
        {
            public int Value;
        }

        [Fact]
        public void ComponentRegistry_StoresMetadataLikeFrameSync()
        {
            var builder = ComponentRegistry.CreateBuilder();
            builder.Add<MetadataComponent>(flags: ComponentFlags.Singleton | ComponentFlags.HideInDump);
            builder.Finish();

            var sample = new MetadataComponent { Value = 1 };
            Assert.Equal(1, sample.Value);

            int typeId = ComponentTypeId<MetadataComponent>.Id;
            ComponentTypeInfo info = ComponentRegistry.GetTypeInfo(typeId);

            Assert.Equal(typeof(MetadataComponent), info.Type);
            Assert.Equal(nameof(MetadataComponent), info.Name);
            Assert.Equal(typeId, info.Id);
            Assert.Equal(sizeof(MetadataComponent), info.Size);
            Assert.Equal(ComponentFlags.Singleton | ComponentFlags.HideInDump, info.Flags);
            Assert.Equal(typeId, ComponentRegistry.GetComponentIndex(typeof(MetadataComponent)));
            Assert.Equal(typeId, ComponentRegistry.GetComponentIndex(nameof(MetadataComponent)));
        }

        [Fact]
        public void ComponentLifecycleCallbacks_AreTriggeredOnAddAndDestroy()
        {
            ResetLifecycleState();

            var builder = ComponentRegistry.CreateBuilder();
            builder.Add<LifecycleComponent>(null, OnLifecycleAdded, OnLifecycleRemoved);
            builder.Finish();

            using var frame = new Frame(4);
            EntityRef entity = frame.CreateEntity();

            frame.Add(entity, new LifecycleComponent { Value = 42 });
            frame.DestroyEntity(entity);

            Assert.Equal(1, _onAddedCount);
            Assert.Equal(1, _onRemovedCount);
            Assert.Equal(entity, _lastLifecycleEntity);
            Assert.Equal(42, _lastLifecycleValue);
        }

        [Fact]
        public void DestroyEntity_RemovesComponentsAndInvalidatesStaleHandles()
        {
            ComponentRegistry.Register<ReusedSlotComponent>();

            using var frame = new Frame(4);
            EntityRef original = frame.CreateEntity();
            frame.Add(original, new ReusedSlotComponent { Value = 10 });

            frame.DestroyEntity(original);

            Assert.False(frame.IsValid(original));
            Assert.False(frame.Has<ReusedSlotComponent>(original));
            Assert.False(frame.TryGet<ReusedSlotComponent>(original, out _));
            Assert.True(frame.GetPointer<ReusedSlotComponent>(original) == null);
            Assert.Throws<ArgumentException>(() => ReadComponentValue(frame, original));

            EntityRef reused = frame.CreateEntity();
            Assert.Equal(original.Index, reused.Index);
            Assert.False(frame.Has<ReusedSlotComponent>(reused));

            frame.Add(reused, new ReusedSlotComponent { Value = 20 });

            Assert.True(frame.Has<ReusedSlotComponent>(reused));
            Assert.Equal(20, frame.Get<ReusedSlotComponent>(reused).Value);
        }

        [Fact]
        public void DeferredRemoval_ComponentCanBeRecoveredWithinSameFrame()
        {
            ComponentRegistry.Register<DeferredRemovalComponent>(
                ComponentFlags.None,
                ComponentCallbacks.Empty,
                StorageFlags.DeferredRemoval);

            using var frame = new Frame(4);
            EntityRef entity = frame.CreateEntity();

            frame.Add(entity, new DeferredRemovalComponent { Value = 1 });
            frame.Remove<DeferredRemovalComponent>(entity);

            Assert.False(frame.Has<DeferredRemovalComponent>(entity));
            Assert.Equal(1, frame.PendingDeferredRemovalCount);

            frame.Set(entity, new DeferredRemovalComponent { Value = 99 });

            Assert.True(frame.Has<DeferredRemovalComponent>(entity));
            Assert.Equal(0, frame.PendingDeferredRemovalCount);
            Assert.Equal(99, frame.Get<DeferredRemovalComponent>(entity).Value);

            frame.CommitDeferredRemovals();

            Assert.True(frame.Has<DeferredRemovalComponent>(entity));
            Assert.Equal(99, frame.Get<DeferredRemovalComponent>(entity).Value);
        }

        [Fact]
        public void ComponentRegistry_SerializeComponent_UsesCallbacksAndFlags()
        {
            var builder = ComponentRegistry.CreateBuilder();
            builder.Add<SerializedComponent>(SerializeSerializedComponent);
            builder.Add<PredictionExcludedComponent>(
                flags: ComponentFlags.ExcludeFromPrediction,
                serialize: SerializePredictionExcludedComponent);
            builder.Finish();

            var writeSerializer = new FrameSerializer(new CoreBitStream(), writing: true);
            var source = new SerializedComponent
            {
                Value = 123,
                Owner = new EntityRef(7, 3)
            };

            SerializedComponent* sourcePtr = &source;
            {
                bool serialized = ComponentRegistry.SerializeComponent(
                    ComponentTypeId<SerializedComponent>.Id,
                    sourcePtr,
                    writeSerializer);

                Assert.True(serialized);
            }

            var readSerializer = new FrameSerializer(new CoreBitStream(writeSerializer.Stream.ToArray()), writing: false);
            var roundTrip = default(SerializedComponent);
            SerializedComponent* roundTripPtr = &roundTrip;
            {
                bool serialized = ComponentRegistry.SerializeComponent(
                    ComponentTypeId<SerializedComponent>.Id,
                    roundTripPtr,
                    readSerializer);

                Assert.True(serialized);
            }

            Assert.Equal(source.Value, roundTrip.Value);
            Assert.Equal(source.Owner, roundTrip.Owner);

            var excludedSerializer = new FrameSerializer(new CoreBitStream(), writing: true);
            var excluded = new PredictionExcludedComponent { Value = 5 };

            PredictionExcludedComponent* excludedPtr = &excluded;
            {
                bool serialized = ComponentRegistry.SerializeComponent(
                    ComponentTypeId<PredictionExcludedComponent>.Id,
                    excludedPtr,
                    excludedSerializer,
                    ComponentSerializationMode.Prediction);

                Assert.False(serialized);
            }
        }

        [Fact]
        public void CapturePackedSnapshot_CustomSerializedComponent_DoesNotSerializeTwiceDuringStorageCount()
        {
            _packedSerializeCount = 0;

            var builder = ComponentRegistry.CreateBuilder();
            builder.Add<PackedCountedSerializedComponent>(SerializePackedCountedComponent);
            builder.Finish();

            using var frame = new Frame(4);
            EntityRef first = frame.CreateEntity();
            EntityRef second = frame.CreateEntity();

            frame.Add(first, new PackedCountedSerializedComponent { Value = 10 });
            frame.Add(second, new PackedCountedSerializedComponent { Value = 20 });

            PackedFrameSnapshot snapshot = frame.CapturePackedSnapshot(ComponentSerializationMode.Checkpoint);

            Assert.NotNull(snapshot);
            Assert.Equal(2, _packedSerializeCount);
        }

        [Fact]
        public void PackedSnapshot_CustomSerializedComponent_RoundTripsThroughFixedPayloadLayout()
        {
            var builder = ComponentRegistry.CreateBuilder();
            builder.Add<PackedCountedSerializedComponent>(SerializePackedCountedComponent);
            builder.Finish();

            using var frame = new Frame(4);
            EntityRef first = frame.CreateEntity();
            EntityRef second = frame.CreateEntity();

            frame.Add(first, new PackedCountedSerializedComponent { Value = 10 });
            frame.Add(second, new PackedCountedSerializedComponent { Value = 20 });

            PackedFrameSnapshot snapshot = frame.CapturePackedSnapshot(ComponentSerializationMode.Checkpoint);

            using var restored = new Frame(4);
            restored.RestoreFromPackedSnapshot(snapshot, ComponentSerializationMode.Checkpoint);

            Assert.True(restored.Has<PackedCountedSerializedComponent>(first));
            Assert.True(restored.Has<PackedCountedSerializedComponent>(second));
            Assert.Equal(10, restored.Get<PackedCountedSerializedComponent>(first).Value);
            Assert.Equal(20, restored.Get<PackedCountedSerializedComponent>(second).Value);
            Assert.Equal(frame.CalculateChecksum(ComponentSerializationMode.Checkpoint), restored.CalculateChecksum(ComponentSerializationMode.Checkpoint));
            Assert.True(snapshot.Data.Length >= snapshot.Length);
        }

        [Fact]
        public void FrameSnapshot_RestoresStateAndRespectsCheckpointFlags()
        {
            ComponentRegistry.Register<CommandBufferComponent>();
            ComponentRegistry.Register<CheckpointExcludedComponent>(
                ComponentFlags.ExcludeFromCheckpoints,
                ComponentCallbacks.Empty);

            using var frame = new Frame(8);
            frame.Tick = 12;
            frame.DeltaTime = FP.FromRaw(FP.Raw._0_50);

            EntityRef entity = frame.CreateEntity();
            frame.Add(entity, new CommandBufferComponent { Value = 77 });
            frame.Add(entity, new CheckpointExcludedComponent { Value = 31 });

#pragma warning disable CS0618
            FrameSnapshot checkpointSnapshot = frame.CreateSnapshot(ComponentSerializationMode.Checkpoint);

            using var restored = new Frame(8);
            restored.RestoreFromSnapshot(checkpointSnapshot, ComponentSerializationMode.Checkpoint);
#pragma warning restore CS0618

            Assert.Equal(frame.Tick, restored.Tick);
            Assert.Equal(frame.DeltaTime, restored.DeltaTime);
            Assert.True(restored.Has<CommandBufferComponent>(entity));
            Assert.Equal(77, restored.Get<CommandBufferComponent>(entity).Value);
            Assert.False(restored.Has<CheckpointExcludedComponent>(entity));
            Assert.Equal(checkpointSnapshot.Checksum, restored.CalculateChecksum(ComponentSerializationMode.Checkpoint));
        }

        [Fact]
        public void FrameSnapshot_UsesRawDenseStoragePath_ForPlainUnmanagedComponents()
        {
            ComponentRegistry.Register<RawSnapshotComponent>();

            using var frame = new Frame(8);
            EntityRef first = frame.CreateEntity();
            EntityRef second = frame.CreateEntity();

            frame.Add(first, new RawSnapshotComponent { Value = 10, Bonus = 20 });
            frame.Add(second, new RawSnapshotComponent { Value = 30, Bonus = 40 });

#pragma warning disable CS0618
            FrameSnapshot snapshot = frame.CreateSnapshot();
            ComponentStorageSnapshot storageSnapshot = Assert.Single(snapshot.ComponentStorages);

            Assert.Equal(ComponentTypeId<RawSnapshotComponent>.Id, storageSnapshot.TypeId);
            Assert.Equal(ComponentSnapshotDataKind.RawDense, storageSnapshot.Kind);
            Assert.Equal(2, storageSnapshot.DenseEntities.Length);
            Assert.Equal(sizeof(RawSnapshotComponent) * 2, storageSnapshot.DenseData.Length);

            using var restored = new Frame(8);
            restored.RestoreFromSnapshot(snapshot);
#pragma warning restore CS0618

            Assert.True(restored.Has<RawSnapshotComponent>(first));
            Assert.True(restored.Has<RawSnapshotComponent>(second));
            Assert.Equal(10, restored.Get<RawSnapshotComponent>(first).Value);
            Assert.Equal(20, restored.Get<RawSnapshotComponent>(first).Bonus);
            Assert.Equal(30, restored.Get<RawSnapshotComponent>(second).Value);
            Assert.Equal(40, restored.Get<RawSnapshotComponent>(second).Bonus);
        }

        [Fact]
        public void CommandBuffer_Playback_ResolvesTemporaryEntitiesAndCommitsDeferredRemovals()
        {
            ComponentRegistry.Register<CommandBufferComponent>();
            ComponentRegistry.Register<DeferredRemovalComponent>(
                ComponentFlags.None,
                ComponentCallbacks.Empty,
                StorageFlags.DeferredRemoval);

            using var frame = new Frame(8);
            CommandBuffer commandBuffer = default;
            commandBuffer.Initialize();

            EntityRef tempEntity = commandBuffer.CreateEntity();
            commandBuffer.AddComponent(tempEntity, new CommandBufferComponent { Value = 10 });
            commandBuffer.AddComponent(tempEntity, new DeferredRemovalComponent { Value = 5 });
            commandBuffer.SetComponent(tempEntity, new CommandBufferComponent { Value = 25 });
            commandBuffer.RemoveComponent<DeferredRemovalComponent>(tempEntity);
            commandBuffer.Playback(frame);

            Assert.Equal(1, frame.EntityCount);

            EntityRef actualEntity = new EntityRef(0, 1);
            Assert.True(frame.Has<CommandBufferComponent>(actualEntity));
            Assert.Equal(25, frame.Get<CommandBufferComponent>(actualEntity).Value);
            Assert.False(frame.Has<DeferredRemovalComponent>(actualEntity));
            Assert.Equal(0, frame.PendingDeferredRemovalCount);

            commandBuffer.Dispose();
        }

        [Fact]
        public void Frame_DeferredStructuralChanges_QueueUntilCommit_AndBlockManualDeferredCommit()
        {
            ComponentRegistry.Register<CommandBufferComponent>();

            using var frame = new Frame(8);

            frame.BeginDeferredStructuralChanges();

            EntityRef tempEntity = frame.CreateEntity();
            frame.Add(tempEntity, new CommandBufferComponent { Value = 41 });

            Assert.True(frame.HasPendingStructuralChanges);
            Assert.Equal(0, frame.EntityCount);
            Assert.Throws<InvalidOperationException>(() => frame.CommitDeferredRemovals());

            frame.CommitStructuralChanges();

            EntityRef actualEntity = new EntityRef(0, 1);
            Assert.False(frame.HasPendingStructuralChanges);
            Assert.Equal(1, frame.EntityCount);
            Assert.True(frame.Has<CommandBufferComponent>(actualEntity));
            Assert.Equal(41, frame.Get<CommandBufferComponent>(actualEntity).Value);
        }

        [Fact]
        public void Storage_DefaultBlockCapacity_AdaptsToComponentSize()
        {
            var smallStorage = new Storage<RawSnapshotComponent>();
            smallStorage.Initialize(512);

            var largeStorage = new Storage<LargeAutoBlockComponent>();
            largeStorage.Initialize(512);

            Assert.Equal(256, smallStorage.BlockItemCapacity);
            Assert.Equal(32, largeStorage.BlockItemCapacity);
            Assert.True(largeStorage.BlockItemCapacity < smallStorage.BlockItemCapacity);

            smallStorage.Dispose();
            largeStorage.Dispose();
        }

        [Fact]
        public void Storage_NonDeferredMode_DoesNotAllocatePendingMetadata()
        {
            var storage = new Storage<QueryPrimaryComponent>();
            storage.Initialize(128);

            Assert.False(storage.HasEntryStateTracking);
            Assert.False(storage.HasPendingQueueTracking);

            storage.Dispose();
        }

        [Fact]
        public void Storage_DeferredMode_AllocatesPendingMetadata()
        {
            var storage = new Storage<QuerySecondaryComponent>();
            storage.Initialize(128, StorageFlags.DeferredRemoval);

            Assert.True(storage.HasEntryStateTracking);
            Assert.True(storage.HasPendingQueueTracking);

            storage.Dispose();
        }

        [Fact]
        public void Query_ForEachAndEnumerator_OnlyReturnActiveMatches()
        {
            ComponentRegistry.Register<QueryPrimaryComponent>();
            ComponentRegistry.Register<QuerySecondaryComponent>(
                ComponentFlags.None,
                ComponentCallbacks.Empty,
                StorageFlags.DeferredRemoval);

            using var frame = new Frame(16);

            EntityRef first = frame.CreateEntity();
            EntityRef second = frame.CreateEntity();
            EntityRef third = frame.CreateEntity();

            frame.Add(first, new QueryPrimaryComponent { Value = 10 });
            frame.Add(first, new QuerySecondaryComponent { Value = 100 });

            frame.Add(second, new QueryPrimaryComponent { Value = 20 });
            frame.Add(second, new QuerySecondaryComponent { Value = 200 });

            frame.Add(third, new QueryPrimaryComponent { Value = 30 });

            frame.Remove<QuerySecondaryComponent>(second);

            int enumeratedCount = 0;
            int enumeratedSum = 0;
            var query = frame.Query<QueryPrimaryComponent, QuerySecondaryComponent>();
            var enumerator = query.GetEnumerator();
            while (enumerator.MoveNext())
            {
                enumeratedCount++;
                enumeratedSum += enumerator.Component1.Value + enumerator.Component2.Value;
            }

            Assert.Equal(1, enumeratedCount);
            Assert.Equal(110, enumeratedSum);

            _queryPairAccumulator = 0;
            query.ForEach(&AccumulateQueryPair);
            Assert.Equal(110, _queryPairAccumulator);
        }

        [Fact]
        public void Query_PairEnumerator_CoversSecondaryPrimaryBranch()
        {
            ComponentRegistry.Register<QueryPrimaryComponent>();
            ComponentRegistry.Register<QuerySecondaryComponent>(
                ComponentFlags.None,
                ComponentCallbacks.Empty,
                StorageFlags.DeferredRemoval);

            using var frame = new Frame(16);
            var entities = new EntityRef[6];

            for (int i = 0; i < 6; i++)
            {
                EntityRef entity = frame.CreateEntity();
                entities[i] = entity;
                frame.Add(entity, new QueryPrimaryComponent { Value = 10 + i });

                if (i >= 4)
                {
                    frame.Add(entity, new QuerySecondaryComponent { Value = 100 + i });
                }
            }

            EntityRef removedMatch = entities[5];
            frame.Remove<QuerySecondaryComponent>(removedMatch);

            int count = 0;
            int sum = 0;
            var enumerator = frame.Query<QueryPrimaryComponent, QuerySecondaryComponent>().GetEnumerator();
            while (enumerator.MoveNext())
            {
                count++;
                sum += enumerator.Component1.Value + enumerator.Component2.Value;
            }

            Assert.Equal(1, count);
            Assert.Equal(118, sum);
        }

        [Fact]
        public void Query_TripleEnumerator_CoversThirdStoragePrimaryBranch()
        {
            ComponentRegistry.Register<QueryPrimaryComponent>();
            ComponentRegistry.Register<QuerySecondaryComponent>(
                ComponentFlags.None,
                ComponentCallbacks.Empty,
                StorageFlags.DeferredRemoval);
            ComponentRegistry.Register<QueryTertiaryComponent>();

            using var frame = new Frame(16);

            for (int i = 0; i < 6; i++)
            {
                EntityRef entity = frame.CreateEntity();
                frame.Add(entity, new QueryPrimaryComponent { Value = 10 + i });

                if (i != 2)
                {
                    frame.Add(entity, new QuerySecondaryComponent { Value = 100 + i });
                }

                if (i >= 4)
                {
                    frame.Add(entity, new QueryTertiaryComponent { Value = 1000 + i });
                }
            }

            int count = 0;
            int sum = 0;
            var enumerator = frame.Query<QueryPrimaryComponent, QuerySecondaryComponent, QueryTertiaryComponent>().GetEnumerator();
            while (enumerator.MoveNext())
            {
                count++;
                sum += enumerator.Component1.Value + enumerator.Component2.Value + enumerator.Component3.Value;
            }

            Assert.Equal(2, count);
            Assert.Equal(2247, sum);
        }

        [Fact]
        public void Query_FourComponentEnumerator_AndForEach_CoverFormalFourWayApi()
        {
            ComponentRegistry.Register<QueryPrimaryComponent>();
            ComponentRegistry.Register<QuerySecondaryComponent>(
                ComponentFlags.None,
                ComponentCallbacks.Empty,
                StorageFlags.DeferredRemoval);
            ComponentRegistry.Register<QueryTertiaryComponent>();
            ComponentRegistry.Register<QueryQuaternaryComponent>();

            using var frame = new Frame(16);
            var entities = new EntityRef[6];

            for (int i = 0; i < entities.Length; i++)
            {
                EntityRef entity = frame.CreateEntity();
                entities[i] = entity;
                frame.Add(entity, new QueryPrimaryComponent { Value = 10 + i });
                frame.Add(entity, new QuerySecondaryComponent { Value = 100 + i });

                if (i >= 2)
                {
                    frame.Add(entity, new QueryTertiaryComponent { Value = 1000 + i });
                }

                if (i >= 4)
                {
                    frame.Add(entity, new QueryQuaternaryComponent { Value = 10000 + i });
                }
            }

            frame.Remove<QuerySecondaryComponent>(entities[5]);

            int count = 0;
            int sum = 0;
            var enumerator = frame.Query<QueryPrimaryComponent, QuerySecondaryComponent, QueryTertiaryComponent, QueryQuaternaryComponent>().GetEnumerator();
            while (enumerator.MoveNext())
            {
                count++;
                sum += enumerator.Component1.Value +
                    enumerator.Component2.Value +
                    enumerator.Component3.Value +
                    enumerator.Component4.Value;
            }

            Assert.Equal(1, count);
            Assert.Equal(11126, sum);

            _queryQuadAccumulator = 0;
            frame.Query<QueryPrimaryComponent, QuerySecondaryComponent, QueryTertiaryComponent, QueryQuaternaryComponent>()
                .ForEach(&AccumulateQueryQuad);
            Assert.Equal(11126, _queryQuadAccumulator);
        }

        [Fact]
        public void Query_SingleComponentFunctionPointer_ScansActiveEntries()
        {
            ComponentRegistry.Register<QueryPrimaryComponent>();

            using var frame = new Frame(8);

            EntityRef first = frame.CreateEntity();
            EntityRef second = frame.CreateEntity();

            frame.Add(first, new QueryPrimaryComponent { Value = 7 });
            frame.Add(second, new QueryPrimaryComponent { Value = 11 });

            _queryValueAccumulator = 0;
            frame.Query<QueryPrimaryComponent>().ForEach(&AccumulateQueryValue);

            Assert.Equal(18, _queryValueAccumulator);
        }

        [Fact]
        public void FrameReadOnly_Query_ForwardsToStronglyTypedQueryApi()
        {
            ComponentRegistry.Register<QueryPrimaryComponent>();
            ComponentRegistry.Register<QuerySecondaryComponent>(
                ComponentFlags.None,
                ComponentCallbacks.Empty,
                StorageFlags.DeferredRemoval);

            using var frame = new Frame(8);
            EntityRef entity = frame.CreateEntity();
            frame.Add(entity, new QueryPrimaryComponent { Value = 3 });
            frame.Add(entity, new QuerySecondaryComponent { Value = 9 });

            var readOnly = new FrameReadOnly(frame);
            var query = readOnly.Query<QueryPrimaryComponent, QuerySecondaryComponent>();
            var enumerator = query.GetEnumerator();

            Assert.True(enumerator.MoveNext());
            Assert.Equal(entity, enumerator.Entity);
            Assert.Equal(3, enumerator.Component1.Value);
            Assert.Equal(9, enumerator.Component2.Value);
            Assert.False(enumerator.MoveNext());
        }

        [Fact]
        public void FrameReadOnly_Query4_AndGlobalState_ForwardToFormalApis()
        {
            ComponentRegistry.Register<QueryPrimaryComponent>();
            ComponentRegistry.Register<QuerySecondaryComponent>(
                ComponentFlags.None,
                ComponentCallbacks.Empty,
                StorageFlags.DeferredRemoval);
            ComponentRegistry.Register<QueryTertiaryComponent>();
            ComponentRegistry.Register<QueryQuaternaryComponent>();
            ComponentRegistry.Register<GlobalStateComponent>(ComponentFlags.Singleton, ComponentCallbacks.Empty);

            using var frame = new Frame(8);
            EntityRef entity = frame.CreateEntity();
            frame.Add(entity, new QueryPrimaryComponent { Value = 3 });
            frame.Add(entity, new QuerySecondaryComponent { Value = 9 });
            frame.Add(entity, new QueryTertiaryComponent { Value = 27 });
            frame.Add(entity, new QueryQuaternaryComponent { Value = 81 });
            EntityRef globalEntity = frame.SetGlobal(new GlobalStateComponent { Value = 144 });

            var readOnly = new FrameReadOnly(frame);

            Assert.True(readOnly.HasGlobal<GlobalStateComponent>());
            Assert.True(readOnly.TryGetGlobal(out GlobalStateComponent global));
            Assert.Equal(144, global.Value);
            Assert.Equal(globalEntity, readOnly.GetGlobalEntity<GlobalStateComponent>());
            Assert.True(readOnly.TryGetGlobalEntity<GlobalStateComponent>(out EntityRef resolvedGlobalEntity));
            Assert.Equal(globalEntity, resolvedGlobalEntity);

            var enumerator = readOnly.Query<QueryPrimaryComponent, QuerySecondaryComponent, QueryTertiaryComponent, QueryQuaternaryComponent>().GetEnumerator();

            Assert.True(enumerator.MoveNext());
            Assert.Equal(entity, enumerator.Entity);
            Assert.Equal(3, enumerator.Component1.Value);
            Assert.Equal(9, enumerator.Component2.Value);
            Assert.Equal(27, enumerator.Component3.Value);
            Assert.Equal(81, enumerator.Component4.Value);
            Assert.False(enumerator.MoveNext());
        }

        [Fact]
        public void Frame_GlobalStateApi_ProvidesUnifiedSingletonAccess()
        {
            ComponentRegistry.Register<GlobalStateComponent>(ComponentFlags.Singleton, ComponentCallbacks.Empty);

            using var frame = new Frame(8);

            EntityRef firstEntity = frame.SetGlobal(new GlobalStateComponent { Value = 7 });

            Assert.True(frame.HasGlobal<GlobalStateComponent>());
            Assert.Equal(firstEntity, frame.GetGlobalEntity<GlobalStateComponent>());
            Assert.True(frame.TryGetGlobalEntity<GlobalStateComponent>(out EntityRef resolvedEntity));
            Assert.Equal(firstEntity, resolvedEntity);
            Assert.True(frame.TryGetGlobal(out GlobalStateComponent state));
            Assert.Equal(7, state.Value);
            Assert.Equal(7, frame.GetGlobal<GlobalStateComponent>().Value);

            EntityRef updatedEntity = frame.SetGlobal(new GlobalStateComponent { Value = 11 });

            Assert.Equal(firstEntity, updatedEntity);
            Assert.Equal(11, frame.GetGlobal<GlobalStateComponent>().Value);

            Assert.True(frame.RemoveGlobal<GlobalStateComponent>());
            Assert.False(frame.HasGlobal<GlobalStateComponent>());
            Assert.False(frame.TryGetGlobal<GlobalStateComponent>(out _));
            Assert.False(frame.TryGetGlobalEntity<GlobalStateComponent>(out _));
            Assert.False(frame.IsValid(firstEntity));
        }

        [Fact]
        public void Frame_GlobalStateApi_RejectsNonSingletonComponents()
        {
            ComponentRegistry.Register<GlobalAuxiliaryComponent>();

            using var frame = new Frame(8);

            Assert.Throws<InvalidOperationException>(() => frame.HasGlobal<GlobalAuxiliaryComponent>());
            Assert.Throws<InvalidOperationException>(() => frame.SetGlobal(new GlobalAuxiliaryComponent { Value = 1 }));
            Assert.Throws<InvalidOperationException>(() => frame.TryGetGlobal<GlobalAuxiliaryComponent>(out _));
            Assert.Throws<InvalidOperationException>(() => frame.RemoveGlobal<GlobalAuxiliaryComponent>());
        }

        [Fact]
        public void Frame_GlobalStateApi_RemovesOnlySingletonComponent_WhenCarrierHasOtherComponents()
        {
            ComponentRegistry.Register<GlobalStateComponent>(ComponentFlags.Singleton, ComponentCallbacks.Empty);
            ComponentRegistry.Register<GlobalAuxiliaryComponent>();

            using var frame = new Frame(8);
            EntityRef globalEntity = frame.SetGlobal(new GlobalStateComponent { Value = 5 });
            frame.Add(globalEntity, new GlobalAuxiliaryComponent { Value = 9 });

            Assert.True(frame.RemoveGlobal<GlobalStateComponent>());
            Assert.True(frame.IsValid(globalEntity));
            Assert.False(frame.HasGlobal<GlobalStateComponent>());
            Assert.True(frame.Has<GlobalAuxiliaryComponent>(globalEntity));
            Assert.Equal(9, frame.Get<GlobalAuxiliaryComponent>(globalEntity).Value);
        }

        [Fact]
        public void RegisterOwningGroup_PairBundle_RebuildsAndTracksMutations()
        {
            ComponentRegistry.Register<QueryPrimaryComponent>();
            ComponentRegistry.Register<QuerySecondaryComponent>(
                ComponentFlags.None,
                ComponentCallbacks.Empty,
                StorageFlags.DeferredRemoval);

            using var frame = new Frame(8);
            EntityRef first = frame.CreateEntity();
            EntityRef second = frame.CreateEntity();

            frame.Add(first, new QueryPrimaryComponent { Value = 10 });
            frame.Add(first, new QuerySecondaryComponent { Value = 20 });
            frame.Add(second, new QueryPrimaryComponent { Value = 30 });

            OwningGroup<QueryPrimaryComponent, QuerySecondaryComponent> group =
                frame.RegisterOwningGroup<QueryPrimaryComponent, QuerySecondaryComponent>();

            Assert.Equal(1, group.Count);
            Assert.Equal(30, SumOwningPairValues(group));

            frame.Add(second, new QuerySecondaryComponent { Value = 40 });

            Assert.Equal(2, group.Count);
            Assert.Equal(100, SumOwningPairValues(group));

            frame.Set(first, new QueryPrimaryComponent { Value = 99 });

            Assert.Equal(189, SumOwningPairValues(group));

            frame.Remove<QuerySecondaryComponent>(first);

            Assert.Equal(1, group.Count);
            Assert.Equal(70, SumOwningPairValues(group));
            Assert.Same(group, frame.GetOwningGroup<QueryPrimaryComponent, QuerySecondaryComponent>());
        }

        [Fact]
        public void RegisterOwningGroup_TripleBundle_TracksQualificationAndValueRefresh()
        {
            ComponentRegistry.Register<QueryPrimaryComponent>();
            ComponentRegistry.Register<QuerySecondaryComponent>(
                ComponentFlags.None,
                ComponentCallbacks.Empty,
                StorageFlags.DeferredRemoval);
            ComponentRegistry.Register<QueryTertiaryComponent>();

            using var frame = new Frame(8);
            EntityRef entity = frame.CreateEntity();

            frame.Add(entity, new QueryPrimaryComponent { Value = 5 });
            frame.Add(entity, new QuerySecondaryComponent { Value = 7 });

            OwningGroup<QueryPrimaryComponent, QuerySecondaryComponent, QueryTertiaryComponent> group =
                frame.RegisterOwningGroup<QueryPrimaryComponent, QuerySecondaryComponent, QueryTertiaryComponent>();

            Assert.Equal(0, group.Count);

            frame.Add(entity, new QueryTertiaryComponent { Value = 11 });

            Assert.Equal(1, group.Count);
            Assert.Equal(23, SumOwningTripleValues(group));

            frame.Set(entity, new QueryTertiaryComponent { Value = 100 });

            Assert.Equal(112, SumOwningTripleValues(group));

            frame.Remove<QuerySecondaryComponent>(entity);

            Assert.Equal(0, group.Count);
            Assert.True(frame.TryGetOwningGroup<QueryPrimaryComponent, QuerySecondaryComponent, QueryTertiaryComponent>(out var registered));
            Assert.Same(group, registered);
        }

        [Fact]
        public void RestoreFromSnapshot_RebuildsRegisteredOwningGroups()
        {
            ComponentRegistry.Register<QueryPrimaryComponent>();
            ComponentRegistry.Register<QuerySecondaryComponent>(
                ComponentFlags.None,
                ComponentCallbacks.Empty,
                StorageFlags.DeferredRemoval);

            using var frame = new Frame(8);
            EntityRef first = frame.CreateEntity();
            frame.Add(first, new QueryPrimaryComponent { Value = 10 });
            frame.Add(first, new QuerySecondaryComponent { Value = 20 });

            OwningGroup<QueryPrimaryComponent, QuerySecondaryComponent> group =
                frame.RegisterOwningGroup<QueryPrimaryComponent, QuerySecondaryComponent>();

#pragma warning disable CS0618
            FrameSnapshot snapshot = frame.CreateSnapshot();
#pragma warning restore CS0618

            EntityRef second = frame.CreateEntity();
            frame.Add(second, new QueryPrimaryComponent { Value = 30 });
            frame.Add(second, new QuerySecondaryComponent { Value = 40 });
            Assert.Equal(100, SumOwningPairValues(group));

#pragma warning disable CS0618
            frame.RestoreFromSnapshot(snapshot);
#pragma warning restore CS0618

            Assert.Same(group, frame.GetOwningGroup<QueryPrimaryComponent, QuerySecondaryComponent>());
            Assert.Equal(1, group.Count);
            Assert.Equal(30, SumOwningPairValues(group));
        }

        [Fact]
        public void OwningGroup_ReusedEntitySlot_DropsStaleDenseIndexAndAcceptsReplacement()
        {
            ComponentRegistry.Register<QueryPrimaryComponent>();
            ComponentRegistry.Register<QuerySecondaryComponent>(
                ComponentFlags.None,
                ComponentCallbacks.Empty,
                StorageFlags.DeferredRemoval);

            using var frame = new Frame(8);
            EntityRef original = frame.CreateEntity();
            frame.Add(original, new QueryPrimaryComponent { Value = 10 });
            frame.Add(original, new QuerySecondaryComponent { Value = 20 });

            OwningGroup<QueryPrimaryComponent, QuerySecondaryComponent> group =
                frame.RegisterOwningGroup<QueryPrimaryComponent, QuerySecondaryComponent>();

            Assert.Equal(1, group.Count);
            Assert.Equal(30, SumOwningPairValues(group));

            frame.DestroyEntity(original);

            Assert.Equal(0, group.Count);

            EntityRef reused = frame.CreateEntity();
            Assert.Equal(original.Index, reused.Index);
            Assert.NotEqual(original.Version, reused.Version);

            frame.Add(reused, new QueryPrimaryComponent { Value = 70 });
            frame.Add(reused, new QuerySecondaryComponent { Value = 80 });

            Assert.Equal(1, group.Count);
            Assert.Equal(150, SumOwningPairValues(group));
        }

        [Fact]
        public void Storage_DenseLinearIndex_MapsAcrossReservedZeroSlotAndBlockBoundary()
        {
            var storage = new Storage<DenseIndexComponent>();
            storage.Initialize(512);

            for (int i = 0; i < 300; i++)
            {
                storage.Add(new EntityRef(i, 1), new DenseIndexComponent { Value = i * 10 });
            }

            storage.GetDenseEntryByLinearIndex(0, out EntityRef firstEntity, out DenseIndexComponent* firstComponent);
            storage.GetDenseEntryByLinearIndex(254, out EntityRef lastInFirstBlockEntity, out DenseIndexComponent* lastInFirstBlockComponent);
            storage.GetDenseEntryByLinearIndex(255, out EntityRef firstInSecondBlockEntity, out DenseIndexComponent* firstInSecondBlockComponent);
            storage.GetDenseEntryByLinearIndex(299, out EntityRef lastEntity, out DenseIndexComponent* lastComponent);

            Assert.Equal(new EntityRef(0, 1), firstEntity);
            Assert.Equal(0, firstComponent->Value);
            Assert.Equal(new EntityRef(254, 1), lastInFirstBlockEntity);
            Assert.Equal(2540, lastInFirstBlockComponent->Value);
            Assert.Equal(new EntityRef(255, 1), firstInSecondBlockEntity);
            Assert.Equal(2550, firstInSecondBlockComponent->Value);
            Assert.Equal(new EntityRef(299, 1), lastEntity);
            Assert.Equal(2990, lastComponent->Value);

            storage.Dispose();
        }

        private static void ResetLifecycleState()
        {
            _onAddedCount = 0;
            _onRemovedCount = 0;
            _lastLifecycleEntity = EntityRef.None;
            _lastLifecycleValue = 0;
        }

        private static int ReadComponentValue(Frame frame, EntityRef entity)
        {
            return frame.Get<ReusedSlotComponent>(entity).Value;
        }

        private static void OnLifecycleAdded(EntityRef entity, void* component, Frame frame)
        {
            _onAddedCount++;
            _lastLifecycleEntity = entity;
            _lastLifecycleValue = ((LifecycleComponent*)component)->Value;
        }

        private static void OnLifecycleRemoved(EntityRef entity, void* component, Frame frame)
        {
            _onRemovedCount++;
            _lastLifecycleEntity = entity;
            _lastLifecycleValue = ((LifecycleComponent*)component)->Value;
        }

        private static void SerializeSerializedComponent(void* component, IFrameSerializer serializer)
        {
            var typed = (SerializedComponent*)component;
            serializer.Serialize(ref typed->Value);
            serializer.Serialize(ref typed->Owner);
        }

        private static void SerializePredictionExcludedComponent(void* component, IFrameSerializer serializer)
        {
            var typed = (PredictionExcludedComponent*)component;
            serializer.Serialize(ref typed->Value);
        }

        private static void SerializePackedCountedComponent(void* component, IFrameSerializer serializer)
        {
            _packedSerializeCount++;

            var typed = (PackedCountedSerializedComponent*)component;
            serializer.Serialize(ref typed->Value);
        }

        private static void AccumulateQueryValue(EntityRef entity, QueryPrimaryComponent* component)
        {
            _queryValueAccumulator += component->Value;
        }

        private static void AccumulateQueryPair(EntityRef entity, QueryPrimaryComponent* primary, QuerySecondaryComponent* secondary)
        {
            _queryPairAccumulator += primary->Value + secondary->Value;
        }

        private static void AccumulateQueryQuad(
            EntityRef entity,
            QueryPrimaryComponent* primary,
            QuerySecondaryComponent* secondary,
            QueryTertiaryComponent* tertiary,
            QueryQuaternaryComponent* quaternary)
        {
            _queryQuadAccumulator += primary->Value + secondary->Value + tertiary->Value + quaternary->Value;
        }

        private static int SumOwningPairValues(OwningGroup<QueryPrimaryComponent, QuerySecondaryComponent> group)
        {
            int sum = 0;
            int index = 0;
            while (group.Next(&index, out EntityRef _, out QueryPrimaryComponent* primary, out QuerySecondaryComponent* secondary))
            {
                sum += primary->Value + secondary->Value;
            }

            return sum;
        }

        private static int SumOwningTripleValues(OwningGroup<QueryPrimaryComponent, QuerySecondaryComponent, QueryTertiaryComponent> group)
        {
            int sum = 0;
            int index = 0;
            while (group.Next(
                &index,
                out EntityRef _,
                out QueryPrimaryComponent* primary,
                out QuerySecondaryComponent* secondary,
                out QueryTertiaryComponent* tertiary))
            {
                sum += primary->Value + secondary->Value + tertiary->Value;
            }

            return sum;
        }
    }
}
