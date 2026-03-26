// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System.Runtime.CompilerServices;
using Lattice.Math;

namespace Lattice.ECS.Session
{
    /// <summary>
    /// 最小模拟会话配置。
    /// </summary>
    public sealed class SimulationSessionOptions
    {
        /// <summary>初始 tick。</summary>
        public int InitialTick { get; set; }

        /// <summary>固定时间步长。</summary>
        public FP DeltaTime
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set;
        } = FP.One;

        /// <summary>实体容量。</summary>
        public int MaxEntities { get; set; } = 65536;

        /// <summary>输入缓冲容量。</summary>
        public int InputCapacity { get; set; } = 64;

        /// <summary>命令缓冲初始容量。</summary>
        public int CommandBufferCapacity { get; set; } = 4096;
    }
}
