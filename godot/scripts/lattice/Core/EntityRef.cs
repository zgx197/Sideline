// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Lattice.ECS.Core;

namespace Lattice.Core
{
    /// <summary>
    /// 实体引用 - FrameSync 风格（对齐 EntityRef）
    /// 
    /// 用于标识 ECS 中的唯一实体。
    /// 实现了稀疏集合的 ECS 模型（类似 enTT）。
    /// 
    /// 内存布局：8 字节（Index + Version）
    /// - Index: int (4 bytes) - 实体在存储中的索引
    /// - Version: int (4 bytes) - 版本号，用于检测过期引用
    /// - Raw: ulong (8 bytes) - 与 Index/Version 重叠，用于快速比较
    /// 
    /// 设计参考：Bevy ECS, FrameSync (Quantum), Entitas
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 8)]
    public readonly struct EntityRef : IEquatable<EntityRef>
    {
        /// <summary>
        /// 实体结构体的字节大小（8字节）
        /// </summary>
        public const int Size = 8;

        /// <summary>
        /// 实体在内部数组中的索引位置
        /// </summary>
        [FieldOffset(0)]
        public readonly int Index;

        /// <summary>
        /// 版本号：槽位复用时递增，用于检测失效引用
        /// 最高位同时用作活跃标志位（参见 EntityRegistry.ActiveBit）
        /// </summary>
        [FieldOffset(4)]
        public readonly int Version;

        /// <summary>
        /// 原始64位值，用于快速比较和哈希计算
        /// </summary>
        [FieldOffset(0)]
        public readonly ulong Raw;

        /// <summary>
        /// 表示无效实体的静态实例（Index=0, Version=0）
        /// </summary>
        public static readonly EntityRef None = default;

        /// <summary>
        /// 无效实体引用（None 的别名，与 FrameSync 命名一致）
        /// </summary>
        public static readonly EntityRef Invalid = default;

        /// <summary>
        /// 判断此实体标识符是否有效（非零）
        /// </summary>
        public bool IsValid => Raw != 0;

        /// <summary>
        /// 创建一个新的实体引用
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public EntityRef(int index, int version)
        {
            Index = index;
            Version = version;
            Raw = (ulong)(uint)index | ((ulong)(uint)version << 32);
        }

        /// <summary>
        /// 从原始值创建实体引用（用于序列化）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public EntityRef(ulong raw)
        {
            Raw = raw;
            Index = (int)(raw & 0xFFFFFFFF);
            Version = (int)(raw >> 32);
        }

        #region 相等性比较

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(EntityRef a, EntityRef b)
        {
            return a.Raw == b.Raw;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(EntityRef a, EntityRef b)
        {
            return a.Raw != b.Raw;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(EntityRef other)
        {
            return Raw == other.Raw;
        }

        public override bool Equals(object? obj)
        {
            return obj is EntityRef other && Equals(other);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            // FrameSync 风格哈希计算
            uint a = (uint)Version;
            uint b = (uint)Index;
            return (int)(a + b * 59209);
        }

        #endregion

        #region 转换

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong ToRaw()
        {
            return Raw;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EntityRef FromRaw(ulong raw)
        {
            return new EntityRef(raw);
        }

        #endregion

        #region 字符串表示

        public override string ToString()
        {
            return $"E.{Index:D5}.{Version:D3}";
        }

        public static bool TryParse(string str, out EntityRef result)
        {
            result = None;
            if (string.IsNullOrEmpty(str) || str.Length < 3)
                return false;

            if (!str.StartsWith("E.", StringComparison.Ordinal))
                return false;

            var parts = str[2..].Split('.');
            if (parts.Length != 2)
                return false;

            if (int.TryParse(parts[0], out int index) &&
                int.TryParse(parts[1], out int version))
            {
                result = new EntityRef(index, version);
                return true;
            }

            return false;
        }

        #endregion

        #region 序列化支持

        // 序列化方法在 FrameSerializer 中实现

        #endregion
    }
}
