using Lattice.Math;

namespace Lattice.ECS.Session
{
    /// <summary>
    /// 纯 C#、单线程、固定顺序、可回滚的最小预测运行时。
    /// 这是当前主干正式承诺的 Session 运行模式。
    /// </summary>
    public class MinimalPredictionSession : SessionRuntime
    {
        private static readonly SessionRuntimeBoundary MinimalPredictionBoundary = new(
            SessionRuntimeKind.MinimalPrediction,
            SessionRuntimeCapability.LocalPredictionStep |
            SessionRuntimeCapability.PredictionVerification |
            SessionRuntimeCapability.LocalRewind |
            SessionRuntimeCapability.CheckpointRestore,
            UnsupportedSessionRuntimeCapability.ThreadedScheduling |
            UnsupportedSessionRuntimeCapability.ResourceConfiguredBoot |
            UnsupportedSessionRuntimeCapability.FullNetworkSession |
            UnsupportedSessionRuntimeCapability.PlayerMappingAndTransport);

        public MinimalPredictionSession(FP deltaTime, int localPlayerId = 0)
            : base(deltaTime, localPlayerId)
        {
        }

        public MinimalPredictionSession(SessionRuntimeOptions runtimeOptions)
            : base(runtimeOptions)
        {
        }

        /// <summary>
        /// 当前模式的正式运行边界。
        /// </summary>
        public override SessionRuntimeBoundary RuntimeBoundary => MinimalPredictionBoundary;
    }
}
