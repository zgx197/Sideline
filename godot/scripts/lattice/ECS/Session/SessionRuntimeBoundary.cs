using System;

namespace Lattice.ECS.Session
{
    /// <summary>
    /// 当前 Session 的正式运行时类型。
    /// </summary>
    public enum SessionRuntimeKind
    {
        /// <summary>
        /// 纯 C#、单线程、固定顺序、可回滚的最小预测运行时。
        /// </summary>
        MinimalPrediction = 0,

        /// <summary>
        /// 纯 C#、单线程、固定顺序的本地权威运行时。
        /// 该模式保留固定帧推进与显式 checkpoint，但不承诺预测验证与 rewind 玩法入口。
        /// </summary>
        LocalAuthoritative = 1
    }

    /// <summary>
    /// 当前 Session 公开承诺的运行能力。
    /// </summary>
    [Flags]
    public enum SessionRuntimeCapability
    {
        None = 0,

        /// <summary>
        /// 支持本地固定帧推进。
        /// </summary>
        LocalPredictionStep = 1 << 0,

        /// <summary>
        /// 支持最小验证与回滚修正。
        /// </summary>
        PredictionVerification = 1 << 1,

        /// <summary>
        /// 支持本地 rewind 玩法入口。
        /// </summary>
        LocalRewind = 1 << 2,

        /// <summary>
        /// 支持显式 checkpoint 保存与恢复。
        /// </summary>
        CheckpointRestore = 1 << 3
    }

    /// <summary>
    /// 当前 Session 未作为正式能力承诺的运行面。
    /// </summary>
    [Flags]
    public enum UnsupportedSessionRuntimeCapability
    {
        None = 0,

        /// <summary>
        /// 不支持多线程系统调度。
        /// </summary>
        ThreadedScheduling = 1 << 0,

        /// <summary>
        /// 不支持资源化 / 配置化多模式启动。
        /// </summary>
        ResourceConfiguredBoot = 1 << 1,

        /// <summary>
        /// 不支持完整网络会话管理。
        /// </summary>
        FullNetworkSession = 1 << 2,

        /// <summary>
        /// 不支持玩家映射、房间态与传输层。
        /// </summary>
        PlayerMappingAndTransport = 1 << 3
    }

    /// <summary>
    /// Session 运行边界描述。
    /// 用于把“当前正式支持什么、不支持什么”显式暴露在代码层，而不只停留在文档。
    /// </summary>
    public sealed class SessionRuntimeBoundary
    {
        public SessionRuntimeBoundary(
            SessionRuntimeKind runtimeKind,
            SessionRuntimeCapability supportedCapabilities,
            UnsupportedSessionRuntimeCapability unsupportedCapabilities)
        {
            RuntimeKind = runtimeKind;
            SupportedCapabilities = supportedCapabilities;
            UnsupportedCapabilities = unsupportedCapabilities;
        }

        /// <summary>
        /// 当前运行时类型。
        /// </summary>
        public SessionRuntimeKind RuntimeKind { get; }

        /// <summary>
        /// 当前正式支持的运行能力。
        /// </summary>
        public SessionRuntimeCapability SupportedCapabilities { get; }

        /// <summary>
        /// 当前明确不承诺的运行能力。
        /// </summary>
        public UnsupportedSessionRuntimeCapability UnsupportedCapabilities { get; }

        public bool Supports(SessionRuntimeCapability capability)
        {
            return (SupportedCapabilities & capability) == capability;
        }

        public bool ExplicitlyDoesNotSupport(UnsupportedSessionRuntimeCapability capability)
        {
            return (UnsupportedCapabilities & capability) == capability;
        }
    }
}
