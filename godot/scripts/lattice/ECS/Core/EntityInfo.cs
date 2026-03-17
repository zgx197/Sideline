// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System.Runtime.InteropServices;
using Lattice.Core;

namespace Lattice.ECS.Core
{
    /// <summary>
    /// 实体信息 - FrameSync 风格
    /// 
    /// 每个实体在 Frame 中对应一个 EntityInfo
    /// 包含实体引用和标志位
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 12)]
    public struct EntityInfo
    {
        public const int ActiveBit = int.MinValue;
        public const int VersionMask = int.MaxValue;

        /// <summary>实体引用（8字节）</summary>
        [FieldOffset(0)]
        public EntityRef Ref;

        /// <summary>实体标志（4字节）</summary>
        [FieldOffset(8)]
        public EntityFlags Flags;

        /// <summary>是否活跃</summary>
        public bool IsActive => (Ref.Version & ActiveBit) != 0;

        /// <summary>获取版本号（不含活跃标志）</summary>
        public int Version => Ref.Version & VersionMask;
    }

    /// <summary>
    /// 实体标志
    /// </summary>
    public enum EntityFlags : int
    {
        None = 0,
        /// <summary>不可被裁剪</summary>
        NotCullable = 1 << 0,
        /// <summary>已销毁（待清理）</summary>
        Destroyed = 1 << 1,
        /// <summary>原型实体</summary>
        IsPrototype = 1 << 2,
    }
}
