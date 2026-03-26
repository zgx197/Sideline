using Lattice.Math;

namespace Lattice.ECS.Session
{
    /// <summary>
    /// 纯 C#、单线程、固定顺序的本地权威运行时。
    /// 该模式用于承载只前推的本地 authoritative 模拟链路：
    /// 保留固定帧推进与显式 checkpoint，但不支持预测验证与 rewind。
    /// </summary>
    public sealed class LocalAuthoritativeSession : SessionRuntime
    {
        private static readonly SessionRuntimeBoundary LocalAuthoritativeBoundary = new(
            SessionRuntimeKind.LocalAuthoritative,
            SessionRuntimeCapability.LocalPredictionStep |
            SessionRuntimeCapability.CheckpointRestore,
            UnsupportedSessionRuntimeCapability.ThreadedScheduling |
            UnsupportedSessionRuntimeCapability.ResourceConfiguredBoot |
            UnsupportedSessionRuntimeCapability.FullNetworkSession |
            UnsupportedSessionRuntimeCapability.PlayerMappingAndTransport);

        public LocalAuthoritativeSession(FP deltaTime, int localPlayerId = 0)
            : base(deltaTime, localPlayerId)
        {
        }

        public LocalAuthoritativeSession(SessionRuntimeOptions runtimeOptions)
            : base(runtimeOptions)
        {
        }

        /// <summary>
        /// 当前模式的正式运行边界。
        /// </summary>
        public override SessionRuntimeBoundary RuntimeBoundary => LocalAuthoritativeBoundary;
    }
}
