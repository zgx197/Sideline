// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Lattice.ECS.Core
{
    /// <summary>
    /// 实体引用，对齐 FrameSync 的 EntityRef 设计
    /// 使用 64-bit 结构：32-bit 索引 + 32-bit 版本
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 8)]
    public readonly struct Entity : IEquatable<Entity>, IComparable<Entity>
    {
        /// <summary>无效实体</summary>
        public static readonly Entity None = new(-1, 0);

        /// <summary>实体索引（低 32 位）</summary>
        [FieldOffset(0)]
        public readonly int Index;

        /// <summary>实体版本（高 32 位）</summary>
        [FieldOffset(4)]
        public readonly int Version;

        /// <summary>原始 64-bit 值（用于快速比较）</summary>
        [FieldOffset(0)]
        public readonly long Raw;

        /// <summary>
        /// 创建实体引用
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Entity(int index, int version)
        {
            Raw = 0;  // 必须初始化所有字段
            Index = index;
            Version = version;
        }

        /// <summary>
        /// 从原始值创建（内部使用）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Entity(long raw)
        {
            Index = 0;
            Version = 0;
            Raw = raw;
        }

        /// <summary>是否为有效实体</summary>
        public bool IsValid
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Index >= 0;
        }

        #region 运算符重载

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(Entity a, Entity b) => a.Raw == b.Raw;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(Entity a, Entity b) => a.Raw != b.Raw;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator <(Entity a, Entity b) => a.Raw < b.Raw;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator >(Entity a, Entity b) => a.Raw > b.Raw;

        #endregion

        #region 接口实现

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(Entity other) => Raw == other.Raw;

        public override bool Equals(object? obj) => obj is Entity other && Equals(other);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode() => Raw.GetHashCode();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CompareTo(Entity other) => Raw.CompareTo(other.Raw);

        public override string ToString() => $"Entity(Index={Index}, Version={Version})";

        #endregion
    }
}
