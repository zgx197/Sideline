using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Lattice.Core
{
    /// <summary>
    /// 实体标识符：使用代际索引（Generational Index）实现
    /// 
    /// 内存布局（8 字节）：
    /// [0-3]   Index: int    - 实体在数组中的索引位置
    /// [4-7]   Version: int  - 版本号（最高位为活跃标志）
    /// [0-7]   Raw: ulong    - 原始值（用于快速比较）
    /// 
    /// 设计参考：Bevy ECS, FrameSync (Quantum), Entitas
    /// 
    /// .NET 8 优化：
    /// - StructLayout.Explicit 精确控制内存布局
    /// - AggressiveInlining 强制内联关键方法
    /// - readonly 修饰符确保不可变性
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 8)]
    public readonly struct Entity : IEquatable<Entity>
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
        public static readonly Entity None = default;

        /// <summary>
        /// 判断此实体标识符是否有效（非零）
        /// </summary>
        public bool IsValid => Raw != 0;

        /// <summary>
        /// 创建一个新的实体标识符
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Entity(int index, int version)
        {
            Index = index;
            Version = version;
            Raw = (ulong)(uint)index | ((ulong)(uint)version << 32);
        }

        /// <summary>
        /// 从原始值创建实体标识符（用于序列化）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Entity(ulong raw)
        {
            Raw = raw;
            Index = (int)(raw & 0xFFFFFFFF);
            Version = (int)(raw >> 32);
        }

        /// <summary>
        /// 相等性比较：比较原始64位值
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(Entity other) => Raw == other.Raw;

        /// <summary>
        /// 对象相等性比较
        /// </summary>
        public override bool Equals(object? obj) => obj is Entity other && Equals(other);

        /// <summary>
        /// 获取哈希码：混合高低32位（FNV-1a 风格）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            // FNV-1a 风格哈希混合
            uint a = (uint)Version;
            uint b = (uint)Index;
            return (int)(a ^ (b * 16777619));
        }

        /// <summary>
        /// 相等运算符
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(Entity left, Entity right) => left.Raw == right.Raw;

        /// <summary>
        /// 不等运算符
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(Entity left, Entity right) => left.Raw != right.Raw;

        /// <summary>
        /// 返回格式化的字符串表示：E.{Index}.{Version}
        /// 活跃实体的 Version 显示为数值，非活跃显示为 "-"
        /// </summary>
        public override string ToString()
        {
            bool isActive = (Version & EntityRegistry.ActiveBit) != 0;
            int gen = Version & EntityRegistry.VersionMask;
            return $"E.{Index:D5}.{gen:D3}{(isActive ? "" : "-")}";
        }

        /// <summary>
        /// 尝试从字符串解析 Entity（用于调试和序列化）
        /// </summary>
        public static bool TryParse(ReadOnlySpan<char> str, out Entity result)
        {
            result = None;
            
            if (str.Length < 3 || str[0] != 'E' || str[1] != '.')
                return false;

            // 解析 Index
            int indexEnd = str.Slice(2).IndexOf('.');
            if (indexEnd < 0) return false;
            
            if (!int.TryParse(str.Slice(2, indexEnd), out int index))
                return false;

            // 解析 Version
            var versionSpan = str.Slice(2 + indexEnd + 1);
            int versionLen = versionSpan.Length;
            
            // 检查是否有非活跃标记
            bool isActive = true;
            if (versionLen > 0 && versionSpan[versionLen - 1] == '-')
            {
                isActive = false;
                versionLen--;
            }

            if (!int.TryParse(versionSpan.Slice(0, versionLen), out int version))
                return false;

            if (!isActive) version &= EntityRegistry.VersionMask;
            else version |= EntityRegistry.ActiveBit;

            result = new Entity(index, version);
            return true;
        }
        
        /// <summary>
        /// 尝试从字符串解析（字符串重载）
        /// </summary>
        public static bool TryParse(string? str, out Entity result)
        {
            if (str == null)
            {
                result = None;
                return false;
            }
            return TryParse(str.AsSpan(), out result);
        }
    }
}
