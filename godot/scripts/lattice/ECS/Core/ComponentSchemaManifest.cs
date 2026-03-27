using System;

namespace Lattice.ECS.Core
{
    /// <summary>
    /// 当前快照 / checkpoint 实际写出的组件 schema 清单摘要。
    /// 用于把“这一份序列化负载依赖的组件协议是什么”显式固化到代码层，
    /// 避免未来格式演进时只能靠经验判断兼容性。
    /// </summary>
    public readonly struct ComponentSchemaManifest : IEquatable<ComponentSchemaManifest>
    {
        public const int CurrentVersion = 1;

        public ComponentSchemaManifest(
            ComponentSerializationMode serializationMode,
            ulong fingerprint,
            int serializedComponentCount,
            int version = CurrentVersion)
        {
            if (version < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(version), "Component schema manifest version cannot be negative.");
            }

            if (serializedComponentCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(serializedComponentCount), "Serialized component count cannot be negative.");
            }

            SerializationMode = serializationMode;
            Fingerprint = fingerprint;
            SerializedComponentCount = serializedComponentCount;
            Version = version;
        }

        /// <summary>
        /// 当前 manifest 是否已经明确指定。
        /// 旧兼容入口构造的快照可能不包含 schema 摘要，此时不强制执行 schema 校验。
        /// </summary>
        public bool IsSpecified => Version > 0;

        public int Version { get; }

        public ComponentSerializationMode SerializationMode { get; }

        public ulong Fingerprint { get; }

        public int SerializedComponentCount { get; }

        public static ComponentSchemaManifest Unspecified(ComponentSerializationMode serializationMode)
        {
            return new ComponentSchemaManifest(serializationMode, 0, 0, version: 0);
        }

        public bool Equals(ComponentSchemaManifest other)
        {
            return Version == other.Version &&
                   SerializationMode == other.SerializationMode &&
                   Fingerprint == other.Fingerprint &&
                   SerializedComponentCount == other.SerializedComponentCount;
        }

        public override bool Equals(object? obj)
        {
            return obj is ComponentSchemaManifest other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Version, (int)SerializationMode, Fingerprint, SerializedComponentCount);
        }
    }
}
