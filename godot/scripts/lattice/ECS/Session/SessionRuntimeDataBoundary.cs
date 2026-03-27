using System;

namespace Lattice.ECS.Session
{
    /// <summary>
    /// 当前运行时的输入存取策略类型。
    /// </summary>
    public enum SessionInputStorageKind
    {
        /// <summary>
        /// 按 `(playerId, tick)` 访问的固定窗口输入存储。
        /// </summary>
        PlayerTickFixedWindow = 0
    }

    /// <summary>
    /// 当前运行时的历史帧策略类型。
    /// </summary>
    public enum SessionHistoryStorageKind
    {
        /// <summary>
        /// 保留有限 live frame，并通过 sampled snapshot 按需重建更早历史。
        /// </summary>
        BoundedLiveFramesWithSampledSnapshots = 0
    }

    /// <summary>
    /// 当前运行时的 checkpoint 存储策略类型。
    /// </summary>
    public enum SessionCheckpointStorageKind
    {
        /// <summary>
        /// 使用 packed frame snapshot 作为 checkpoint 主格式。
        /// </summary>
        PackedSnapshot = 0
    }

    /// <summary>
    /// 当前运行时公开承诺的数据策略能力。
    /// </summary>
    [Flags]
    public enum SessionRuntimeDataCapability
    {
        None = 0,
        PlayerTickInputLookup = 1 << 0,
        BoundedInputRetention = 1 << 1,
        TickAddressableHistory = 1 << 2,
        SampledHistoryMaterialization = 1 << 3,
        ExplicitCheckpointRestore = 1 << 4,
        PackedCheckpointStorage = 1 << 5
    }

    /// <summary>
    /// 当前运行时明确不承诺的数据策略能力。
    /// </summary>
    [Flags]
    public enum UnsupportedSessionRuntimeDataCapability
    {
        None = 0,
        ConfigurableInputRetention = 1 << 0,
        ConfigurableHistoryRetention = 1 << 1,
        ConfigurableSnapshotSampling = 1 << 2,
        PluggableHistoryStore = 1 << 3,
        AlternativeCheckpointFormats = 1 << 4,
        UnboundedPerTickRetention = 1 << 5
    }

    /// <summary>
    /// Session 运行时的数据策略边界描述。
    /// 用于把“输入 / 历史 / checkpoint 的稳定公开契约”和“内部 sizing 策略”分开。
    /// </summary>
    public sealed class SessionRuntimeDataBoundary
    {
        public SessionRuntimeDataBoundary(
            SessionInputStorageKind inputStorageKind,
            SessionHistoryStorageKind historyStorageKind,
            SessionCheckpointStorageKind checkpointStorageKind,
            SessionRuntimeDataCapability supportedCapabilities,
            UnsupportedSessionRuntimeDataCapability unsupportedCapabilities)
        {
            InputStorageKind = inputStorageKind;
            HistoryStorageKind = historyStorageKind;
            CheckpointStorageKind = checkpointStorageKind;
            SupportedCapabilities = supportedCapabilities;
            UnsupportedCapabilities = unsupportedCapabilities;
        }

        public SessionInputStorageKind InputStorageKind { get; }

        public SessionHistoryStorageKind HistoryStorageKind { get; }

        public SessionCheckpointStorageKind CheckpointStorageKind { get; }

        public SessionRuntimeDataCapability SupportedCapabilities { get; }

        public UnsupportedSessionRuntimeDataCapability UnsupportedCapabilities { get; }

        public bool Supports(SessionRuntimeDataCapability capability)
        {
            return (SupportedCapabilities & capability) == capability;
        }

        public bool ExplicitlyDoesNotSupport(UnsupportedSessionRuntimeDataCapability capability)
        {
            return (UnsupportedCapabilities & capability) == capability;
        }
    }

    /// <summary>
    /// 当前运行时内部使用的输入 / 历史策略默认 sizing。
    /// 这些值用于实现调优，不属于稳定公开 API。
    /// </summary>
    internal static class SessionRuntimeDataDefaults
    {
        internal const int MaxPredictionFrames = 8;
        internal const int HistorySize = 128;
        internal const int LiveHistorySize = MaxPredictionFrames + 4;
        internal const int HistorySnapshotInterval = 4;
        internal const int MaterializedHistoryCacheSize = 4;
        internal const int RecycledFrameLimit = LiveHistorySize + 4;
    }
}
