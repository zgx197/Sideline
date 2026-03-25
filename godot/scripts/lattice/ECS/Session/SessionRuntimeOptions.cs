using System;
using Lattice.Math;

namespace Lattice.ECS.Session
{
    /// <summary>
    /// Session 运行时配置。
    /// 该对象只承载当前已经稳定公开的基础参数，不提前暴露历史窗口和采样策略等内部实现细节。
    /// </summary>
    public sealed class SessionRuntimeOptions
    {
        /// <summary>
        /// 默认运行配置。
        /// </summary>
        public static SessionRuntimeOptions Default { get; } = new(FP.One, 0);

        public SessionRuntimeOptions(FP deltaTime, int localPlayerId = 0)
        {
            DeltaTime = deltaTime;
            LocalPlayerId = localPlayerId;
        }

        /// <summary>
        /// 固定时间步长。
        /// </summary>
        public FP DeltaTime { get; }

        /// <summary>
        /// 本地玩家 ID。
        /// 主要用于本地输入读取和最小预测链路。
        /// </summary>
        public int LocalPlayerId { get; }

        /// <summary>
        /// 创建一个仅替换固定时间步长的新配置。
        /// </summary>
        public SessionRuntimeOptions WithDeltaTime(FP deltaTime)
        {
            return new SessionRuntimeOptions(deltaTime, LocalPlayerId);
        }

        /// <summary>
        /// 创建一个仅替换本地玩家 ID 的新配置。
        /// </summary>
        public SessionRuntimeOptions WithLocalPlayerId(int localPlayerId)
        {
            return new SessionRuntimeOptions(DeltaTime, localPlayerId);
        }
    }
}
