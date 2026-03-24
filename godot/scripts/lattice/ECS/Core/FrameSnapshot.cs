// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;
using Lattice.Math;

namespace Lattice.ECS.Core
{
    /// <summary>
    /// 帧快照。
    /// </summary>
    public sealed class FrameSnapshot
    {
        public FrameSnapshot(int tick, FP deltaTime, int entityCapacity, byte[] data, ulong checksum)
        {
            ArgumentNullException.ThrowIfNull(data);

            Tick = tick;
            DeltaTime = deltaTime;
            EntityCapacity = entityCapacity;
            Data = data;
            Checksum = checksum;
        }

        /// <summary>快照对应的 tick。</summary>
        public int Tick { get; }

        /// <summary>快照记录的固定时间步长。</summary>
        public FP DeltaTime { get; }

        /// <summary>快照对应的实体容量。</summary>
        public int EntityCapacity { get; }

        /// <summary>快照原始数据。</summary>
        public byte[] Data { get; }

        /// <summary>快照字节长度。</summary>
        public int DataLength => Data.Length;

        /// <summary>快照校验和。</summary>
        public ulong Checksum { get; }
    }
}
