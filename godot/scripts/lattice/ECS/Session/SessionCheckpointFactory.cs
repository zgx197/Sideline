using System;
using Lattice.ECS.Core;

namespace Lattice.ECS.Session
{
    /// <summary>
    /// Session checkpoint 的捕获与恢复辅助。
    /// </summary>
    internal static class SessionCheckpointFactory
    {
        public static SessionCheckpoint Capture(
            int tick,
            Frame? verifiedFrame,
            Frame? predictedFrame,
            SessionRuntimeInputBoundary inputBoundary,
            SessionRuntimeDataBoundary dataBoundary)
        {
            ArgumentNullException.ThrowIfNull(inputBoundary);
            ArgumentNullException.ThrowIfNull(dataBoundary);

            PackedFrameSnapshot? verifiedSnapshot = verifiedFrame?.CapturePackedSnapshot(ComponentSerializationMode.Checkpoint);
            PackedFrameSnapshot? predictedSnapshot = predictedFrame?.CapturePackedSnapshot(ComponentSerializationMode.Checkpoint);
            ComponentSchemaManifest componentSchema = ResolveComponentSchema(verifiedSnapshot, predictedSnapshot);

            return new SessionCheckpoint
            {
                Tick = tick,
                VerifiedSnapshot = verifiedSnapshot,
                PredictedSnapshot = predictedSnapshot,
                Protocol = new SessionCheckpointProtocol(
                    SessionCheckpointProtocol.CurrentVersion,
                    dataBoundary.CheckpointStorageKind,
                    SessionInputProtocol.CreateStamp(inputBoundary),
                    PackedFrameSnapshot.CurrentFormatVersion,
                    componentSchema)
            };
        }

        public static SessionCheckpointFrames RestoreFrames(
            SessionCheckpoint checkpoint,
            SessionRuntimeInputBoundary inputBoundary,
            SessionRuntimeDataBoundary dataBoundary,
            Func<PackedFrameSnapshot, ComponentSerializationMode, Frame> restoreFrame)
        {
            ArgumentNullException.ThrowIfNull(checkpoint);
            ArgumentNullException.ThrowIfNull(inputBoundary);
            ArgumentNullException.ThrowIfNull(dataBoundary);
            ArgumentNullException.ThrowIfNull(restoreFrame);

            ValidateCheckpointProtocol(checkpoint, inputBoundary, dataBoundary);

            Frame? verifiedFrame = checkpoint.VerifiedSnapshot != null
                ? restoreFrame(checkpoint.VerifiedSnapshot, ComponentSerializationMode.Checkpoint)
                : null;
            Frame? predictedFrame = checkpoint.PredictedSnapshot != null
                ? restoreFrame(checkpoint.PredictedSnapshot, ComponentSerializationMode.Checkpoint)
                : null;

            return new SessionCheckpointFrames(verifiedFrame, predictedFrame);
        }

        private static ComponentSchemaManifest ResolveComponentSchema(PackedFrameSnapshot? verifiedSnapshot, PackedFrameSnapshot? predictedSnapshot)
        {
            if (verifiedSnapshot?.SchemaManifest.IsSpecified == true)
            {
                if (predictedSnapshot?.SchemaManifest.IsSpecified == true &&
                    !verifiedSnapshot.SchemaManifest.Equals(predictedSnapshot.SchemaManifest))
                {
                    throw new InvalidOperationException("Verified and predicted checkpoint snapshots produced different component schema manifests.");
                }

                return verifiedSnapshot.SchemaManifest;
            }

            if (predictedSnapshot?.SchemaManifest.IsSpecified == true)
            {
                return predictedSnapshot.SchemaManifest;
            }

            return ComponentSchemaManifest.Unspecified(ComponentSerializationMode.Checkpoint);
        }

        private static void ValidateCheckpointProtocol(
            SessionCheckpoint checkpoint,
            SessionRuntimeInputBoundary inputBoundary,
            SessionRuntimeDataBoundary dataBoundary)
        {
            if (checkpoint.Protocol.Version != SessionCheckpointProtocol.CurrentVersion)
            {
                throw new InvalidOperationException(
                    $"Checkpoint protocol version {checkpoint.Protocol.Version} is not compatible with current runtime protocol version {SessionCheckpointProtocol.CurrentVersion}.");
            }

            if (checkpoint.Protocol.CheckpointStorageKind != dataBoundary.CheckpointStorageKind)
            {
                throw new InvalidOperationException(
                    $"Checkpoint storage kind {checkpoint.Protocol.CheckpointStorageKind} is not compatible with current runtime storage kind {dataBoundary.CheckpointStorageKind}.");
            }

            if (checkpoint.Protocol.PackedSnapshotFormatVersion != PackedFrameSnapshot.CurrentFormatVersion)
            {
                throw new InvalidOperationException(
                    $"Checkpoint packed snapshot format version {checkpoint.Protocol.PackedSnapshotFormatVersion} is not compatible with current runtime format version {PackedFrameSnapshot.CurrentFormatVersion}.");
            }

            SessionInputProtocol.EnsureCompatible(
                checkpoint.Protocol.InputContract,
                SessionInputProtocol.CreateStamp(inputBoundary),
                nameof(SessionCheckpoint));

            if (checkpoint.VerifiedSnapshot?.SchemaManifest.IsSpecified == true &&
                checkpoint.Protocol.ComponentSchema.IsSpecified &&
                !checkpoint.VerifiedSnapshot.SchemaManifest.Equals(checkpoint.Protocol.ComponentSchema))
            {
                throw new InvalidOperationException("Checkpoint protocol schema manifest does not match its verified snapshot manifest.");
            }

            if (checkpoint.PredictedSnapshot?.SchemaManifest.IsSpecified == true &&
                checkpoint.Protocol.ComponentSchema.IsSpecified &&
                !checkpoint.PredictedSnapshot.SchemaManifest.Equals(checkpoint.Protocol.ComponentSchema))
            {
                throw new InvalidOperationException("Checkpoint protocol schema manifest does not match its predicted snapshot manifest.");
            }
        }
    }

    internal readonly record struct SessionCheckpointFrames(Frame? VerifiedFrame, Frame? PredictedFrame);
}
