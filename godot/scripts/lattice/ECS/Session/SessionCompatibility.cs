using System;
using System.ComponentModel;
using Lattice.Math;

namespace Lattice.ECS.Session
{
    /// <summary>
    /// `MinimalPredictionSession` 的兼容入口。
    /// 当前仍保留 `Session` 名称，避免现有调用面一次性迁移。
    /// </summary>
    [Obsolete("请优先改用 MinimalPredictionSession 或 SessionRunnerBuilder。Session 仅保留为兼容入口。", false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class Session : MinimalPredictionSession
    {
        public Session(FP deltaTime, int localPlayerId = 0)
            : base(deltaTime, localPlayerId)
        {
        }

        public Session(SessionRuntimeOptions runtimeOptions)
            : base(runtimeOptions)
        {
        }
    }
}
