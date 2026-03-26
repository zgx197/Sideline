using System;
using Lattice.ECS.Core;

namespace Lattice.ECS.Session
{
    /// <summary>
    /// 输入 payload 兼容规则。
    /// 当前第一轮正式协议采用“显式 codec 标识 + 显式版本号 + 精确匹配”策略，
    /// 避免 payload 格式继续依赖隐式约定演进。
    /// </summary>
    public enum SessionPayloadCompatibilityMode
    {
        ExactMatchRequired = 0
    }

    /// <summary>
    /// 输入 payload codec 的版本描述。
    /// </summary>
    public readonly struct SessionInputPayloadFormat : IEquatable<SessionInputPayloadFormat>
    {
        public SessionInputPayloadFormat(
            string codecId,
            int version,
            SessionPayloadCompatibilityMode compatibilityMode = SessionPayloadCompatibilityMode.ExactMatchRequired)
        {
            if (string.IsNullOrWhiteSpace(codecId))
            {
                throw new ArgumentException("Codec id cannot be null or whitespace.", nameof(codecId));
            }

            if (version <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(version), "Input payload format version must be greater than zero.");
            }

            CodecId = codecId;
            Version = version;
            CompatibilityMode = compatibilityMode;
        }

        public string CodecId { get; }

        public int Version { get; }

        public SessionPayloadCompatibilityMode CompatibilityMode { get; }

        public bool Equals(SessionInputPayloadFormat other)
        {
            return string.Equals(CodecId, other.CodecId, StringComparison.Ordinal) &&
                   Version == other.Version &&
                   CompatibilityMode == other.CompatibilityMode;
        }

        public override bool Equals(object? obj)
        {
            return obj is SessionInputPayloadFormat other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(CodecId, Version, (int)CompatibilityMode);
        }
    }

    /// <summary>
    /// 带显式 payload 版本声明的输入 codec。
    /// 当输入需要接入网络、回放或文件载荷时，应显式声明 codec 身份与版本，而不是只暴露裸序列化函数。
    /// </summary>
    public interface IVersionedInputPayloadCodec<TInput> : IInputPayloadCodec<TInput>
        where TInput : IPlayerInput
    {
        SessionInputPayloadFormat PayloadFormat { get; }
    }

    /// <summary>
    /// 当前运行时输入契约的正式 stamp。
    /// 用于把输入模型、缺失语义、重复写入语义与协议版本一起收口。
    /// </summary>
    public readonly struct SessionInputContractStamp : IEquatable<SessionInputContractStamp>
    {
        public SessionInputContractStamp(
            int contractVersion,
            SessionInputContractKind contractKind,
            SessionMissingInputPolicy missingInputPolicy,
            SessionInputWritePolicy writePolicy,
            SessionInputOrder playerOrder)
        {
            if (contractVersion <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(contractVersion), "Input contract version must be greater than zero.");
            }

            ContractVersion = contractVersion;
            ContractKind = contractKind;
            MissingInputPolicy = missingInputPolicy;
            WritePolicy = writePolicy;
            PlayerOrder = playerOrder;
        }

        public int ContractVersion { get; }

        public SessionInputContractKind ContractKind { get; }

        public SessionMissingInputPolicy MissingInputPolicy { get; }

        public SessionInputWritePolicy WritePolicy { get; }

        public SessionInputOrder PlayerOrder { get; }

        public bool Equals(SessionInputContractStamp other)
        {
            return ContractVersion == other.ContractVersion &&
                   ContractKind == other.ContractKind &&
                   MissingInputPolicy == other.MissingInputPolicy &&
                   WritePolicy == other.WritePolicy &&
                   PlayerOrder == other.PlayerOrder;
        }

        public override bool Equals(object? obj)
        {
            return obj is SessionInputContractStamp other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ContractVersion, (int)ContractKind, (int)MissingInputPolicy, (int)WritePolicy, (int)PlayerOrder);
        }
    }

    /// <summary>
    /// Session 输入协议治理入口。
    /// </summary>
    public static class SessionInputProtocol
    {
        public const int CurrentContractVersion = 1;

        public static SessionInputContractStamp CreateStamp(SessionRuntimeInputBoundary boundary)
        {
            ArgumentNullException.ThrowIfNull(boundary);
            return new SessionInputContractStamp(
                CurrentContractVersion,
                boundary.ContractKind,
                boundary.MissingInputPolicy,
                boundary.WritePolicy,
                boundary.PlayerOrder);
        }

        public static void EnsureCompatible(SessionInputContractStamp expected, SessionInputContractStamp actual, string context)
        {
            if (!expected.Equals(actual))
            {
                throw new InvalidOperationException(
                    $"Input contract protocol mismatch while restoring {context}. " +
                    $"ExpectedVersion={expected.ContractVersion}, ActualVersion={actual.ContractVersion}, " +
                    $"ExpectedKind={expected.ContractKind}, ActualKind={actual.ContractKind}.");
            }
        }

        public static void EnsurePayloadCompatible(SessionInputPayloadFormat expected, SessionInputPayloadFormat actual, string context)
        {
            if (!expected.Equals(actual))
            {
                throw new InvalidOperationException(
                    $"Input payload codec mismatch for {context}. " +
                    $"Expected={expected.CodecId}@{expected.Version}, Actual={actual.CodecId}@{actual.Version}.");
            }
        }
    }

    /// <summary>
    /// 当前 checkpoint 的正式协议 stamp。
    /// 第一轮治理采用精确匹配策略：
    /// - 输入契约必须一致
    /// - checkpoint 存储类型必须一致
    /// - packed snapshot 格式版本必须一致
    /// - 组件 schema 摘要必须一致
    /// </summary>
    public readonly struct SessionCheckpointProtocol : IEquatable<SessionCheckpointProtocol>
    {
        public const int CurrentVersion = 1;

        public SessionCheckpointProtocol(
            int version,
            SessionCheckpointStorageKind checkpointStorageKind,
            SessionInputContractStamp inputContract,
            int packedSnapshotFormatVersion,
            ComponentSchemaManifest componentSchema)
        {
            if (version <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(version), "Checkpoint protocol version must be greater than zero.");
            }

            if (packedSnapshotFormatVersion <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(packedSnapshotFormatVersion), "Packed snapshot format version must be greater than zero.");
            }

            Version = version;
            CheckpointStorageKind = checkpointStorageKind;
            InputContract = inputContract;
            PackedSnapshotFormatVersion = packedSnapshotFormatVersion;
            ComponentSchema = componentSchema;
        }

        public int Version { get; }

        public SessionCheckpointStorageKind CheckpointStorageKind { get; }

        public SessionInputContractStamp InputContract { get; }

        public int PackedSnapshotFormatVersion { get; }

        public ComponentSchemaManifest ComponentSchema { get; }

        public bool Equals(SessionCheckpointProtocol other)
        {
            return Version == other.Version &&
                   CheckpointStorageKind == other.CheckpointStorageKind &&
                   InputContract.Equals(other.InputContract) &&
                   PackedSnapshotFormatVersion == other.PackedSnapshotFormatVersion &&
                   ComponentSchema.Equals(other.ComponentSchema);
        }

        public override bool Equals(object? obj)
        {
            return obj is SessionCheckpointProtocol other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Version, (int)CheckpointStorageKind, InputContract, PackedSnapshotFormatVersion, ComponentSchema);
        }
    }
}
