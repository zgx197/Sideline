// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using Lattice.ECS.Serialization;

namespace Lattice.ECS.Core
{
    /// <summary>
    /// 组件集合（512-bit 位图），对齐 FrameSync 设计
    /// 
    /// 跨平台说明：
    /// - x86/x64: 支持 Vector512(AVX-512)、Vector256(AVX2)、Vector128(SSE2)
    /// - ARM64: 支持 Vector128(NEON)，Vector256/512 使用软件模拟
    /// - WASM: 支持 Vector128(SIMD128)
    /// - 所有 SIMD 路径都有软件回退，保证行为一致性
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = SIZE)]
    public unsafe struct ComponentSet : IEquatable<ComponentSet>
    {
        /// <summary>最大组件类型数量</summary>
        public const int MAX_COMPONENTS = 512;

        /// <summary>块数量（512 / 64 = 8）</summary>
        public const int BLOCK_COUNT = 8;

        /// <summary>总字节大小</summary>
        public const int SIZE = 64;

        /// <summary>
        /// 位集合数据（8 个 ulong = 512 位）
        /// 注意：fixed buffer 保证内存连续，便于 SIMD 加载
        /// </summary>
        [FieldOffset(0)]
        public fixed ulong Set[8];

        /// <summary>空集合</summary>
        public static ComponentSet Empty => new();

        #region 属性

        /// <summary>检查是否为空集</summary>
        public bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                // 注意：不使用 SIMD 检查 IsEmpty，因为分支预测失败的开销
                // 可能超过 SIMD 收益，且手动检查对跨平台更友好
                if (Set[0] != 0 || Set[1] != 0 || Set[2] != 0 || Set[3] != 0)
                    return false;
                return Set[4] == 0 && Set[5] == 0 && Set[6] == 0 && Set[7] == 0;
            }
        }

        /// <summary>
        /// 获取集合的"有效块数"（用于优化比较）
        /// 返回最高非零块的索引 + 1
        /// </summary>
        internal int Rank
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                // 从高位向低位检查，快速确定有效范围
                if (Set[7] != 0) return 8;
                if (Set[6] != 0) return 7;
                if (Set[5] != 0) return 6;
                if (Set[4] != 0) return 5;
                if (Set[3] != 0) return 4;
                if (Set[2] != 0) return 3;
                if (Set[1] != 0) return 2;
                return Set[0] != 0 ? 1 : 0;
            }
        }

        /// <summary>
        /// 将 fixed buffer 转换为 Span（.NET 8 优化）
        /// </summary>
        public Span<ulong> AsSpan()
        {
            ref ulong start = ref Set[0];
            return MemoryMarshal.CreateSpan(ref start, 8);
        }

        /// <summary>
        /// 将 fixed buffer 转换为 ReadOnlySpan
        /// </summary>
        public ReadOnlySpan<ulong> AsReadOnlySpan()
        {
            ref ulong start = ref Set[0];
            return MemoryMarshal.CreateReadOnlySpan(ref start, 8);
        }

        #endregion

        #region 核心操作

        /// <summary>
        /// 检查是否包含指定索引的组件类型
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsSet(int index)
        {
            // 使用无符号比较同时检查下界和上界
            if ((uint)index >= (uint)MAX_COMPONENTS)
                return false;
            return (Set[index / 64] & (1UL << (index % 64))) != 0;
        }

        /// <summary>
        /// 添加组件（修改当前集合）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UnionWith(int index) => AddInternal(index);

        /// <summary>
        /// 泛型添加组件（修改当前集合）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add<T>() where T : unmanaged, IComponent
        {
            Add(ComponentTypeId<T>.Id);
        }

        /// <summary>
        /// 泛型移除组件（修改当前集合）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove<T>() where T : unmanaged, IComponent
        {
            Remove(ComponentTypeId<T>.Id);
        }

        /// <summary>
        /// 添加组件（修改当前集合，返回 void）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(int index) => AddInternal(index);

        /// <summary>
        /// 移除组件（修改当前集合，返回 void）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove(int index) => RemoveInternal(index);

        /// <summary>
        /// 函数式 API：添加组件并返回新集合
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ComponentSet With(int index)
        {
            ComponentSet result = this;
            result.AddInternal(index);
            return result;
        }

        /// <summary>
        /// 函数式 API：移除组件并返回新集合
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ComponentSet Without(int index)
        {
            ComponentSet result = this;
            result.RemoveInternal(index);
            return result;
        }

        /// <summary>
        /// 内部添加（修改当前集合）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AddInternal(int index)
        {
#if DEBUG
            if ((uint)index >= (uint)MAX_COMPONENTS)
                ThrowIndexOutOfRange(index);
#endif
            // 跨平台：使用 ref 保证原地修改
            ref ulong reference = ref Set[index / 64];
            reference |= (1UL << (index % 64));
        }

        /// <summary>
        /// 内部移除（修改当前集合）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RemoveInternal(int index)
        {
#if DEBUG
            if ((uint)index >= (uint)MAX_COMPONENTS)
                ThrowIndexOutOfRange(index);
#endif
            ref ulong reference = ref Set[index / 64];
            reference &= ~(1UL << (index % 64));
        }

#if DEBUG
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowIndexOutOfRange(int index)
        {
            throw new ArgumentOutOfRangeException(nameof(index), $"组件索引 {index} 超出范围 [0, {MAX_COMPONENTS})");
        }
#endif

        /// <summary>
        /// 泛型检查组件
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsSet<T>() where T : unmanaged, IComponent
        {
            return IsSet(ComponentTypeId<T>.Id);
        }

        /// <summary>
        /// 检查是否包含组件类型（旧 API 兼容）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(int index) => IsSet(index);

        /// <summary>
        /// 检查是否包含组件类型（旧 API 兼容）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains<T>() where T : unmanaged, IComponent => IsSet<T>();

        #endregion

        #region 集合关系运算（跨平台安全）

        /// <summary>
        /// 检查当前集合是否为 other 的子集
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsSubsetOf(in ComponentSet other)
        {
            // 跨平台：使用 64 位比较而非 SIMD，确保所有平台行为一致
            // SIMD 优化在此场景收益有限，且避免跨平台对齐问题
            return (Set[0] & other.Set[0]) == Set[0] &&
                   (Set[1] & other.Set[1]) == Set[1] &&
                   (Set[2] & other.Set[2]) == Set[2] &&
                   (Set[3] & other.Set[3]) == Set[3] &&
                   (Set[4] & other.Set[4]) == Set[4] &&
                   (Set[5] & other.Set[5]) == Set[5] &&
                   (Set[6] & other.Set[6]) == Set[6] &&
                   (Set[7] & other.Set[7]) == Set[7];
        }

        /// <summary>
        /// 检查当前集合是否为 other 的超集
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsSupersetOf(in ComponentSet other)
        {
            return (Set[0] & other.Set[0]) == other.Set[0] &&
                   (Set[1] & other.Set[1]) == other.Set[1] &&
                   (Set[2] & other.Set[2]) == other.Set[2] &&
                   (Set[3] & other.Set[3]) == other.Set[3] &&
                   (Set[4] & other.Set[4]) == other.Set[4] &&
                   (Set[5] & other.Set[5]) == other.Set[5] &&
                   (Set[6] & other.Set[6]) == other.Set[6] &&
                   (Set[7] & other.Set[7]) == other.Set[7];
        }

        /// <summary>
        /// 检查是否与 other 有交集（同 Overlaps）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Intersects(in ComponentSet other) => Overlaps(other);

        /// <summary>
        /// 检查是否与 other 有交集
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Overlaps(in ComponentSet other)
        {
            return (Set[0] & other.Set[0]) != 0 ||
                   (Set[1] & other.Set[1]) != 0 ||
                   (Set[2] & other.Set[2]) != 0 ||
                   (Set[3] & other.Set[3]) != 0 ||
                   (Set[4] & other.Set[4]) != 0 ||
                   (Set[5] & other.Set[5]) != 0 ||
                   (Set[6] & other.Set[6]) != 0 ||
                   (Set[7] & other.Set[7]) != 0;
        }

        #endregion

        #region 集合运算（跨平台安全）

        /// <summary>
        /// 并集（修改当前集合）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UnionWith(in ComponentSet other)
        {
            Set[0] |= other.Set[0];
            Set[1] |= other.Set[1];
            Set[2] |= other.Set[2];
            Set[3] |= other.Set[3];
            Set[4] |= other.Set[4];
            Set[5] |= other.Set[5];
            Set[6] |= other.Set[6];
            Set[7] |= other.Set[7];
        }

        /// <summary>
        /// 交集（修改当前集合）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void IntersectWith(in ComponentSet other)
        {
            Set[0] &= other.Set[0];
            Set[1] &= other.Set[1];
            Set[2] &= other.Set[2];
            Set[3] &= other.Set[3];
            Set[4] &= other.Set[4];
            Set[5] &= other.Set[5];
            Set[6] &= other.Set[6];
            Set[7] &= other.Set[7];
        }

        /// <summary>
        /// 差集（修改当前集合）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove(in ComponentSet other)
        {
            Set[0] &= ~other.Set[0];
            Set[1] &= ~other.Set[1];
            Set[2] &= ~other.Set[2];
            Set[3] &= ~other.Set[3];
            Set[4] &= ~other.Set[4];
            Set[5] &= ~other.Set[5];
            Set[6] &= ~other.Set[6];
            Set[7] &= ~other.Set[7];
        }

        #endregion

        #region 静态创建方法

        public static ComponentSet Create<T>() where T : unmanaged, IComponent
        {
            ComponentSet set = default;
            set.Add<T>();
            return set;
        }

        public static ComponentSet Create<T1, T2>()
            where T1 : unmanaged, IComponent where T2 : unmanaged, IComponent
        {
            ComponentSet set = default;
            set.Add<T1>();
            set.Add<T2>();
            return set;
        }

        public static ComponentSet Create<T1, T2, T3>()
            where T1 : unmanaged, IComponent where T2 : unmanaged, IComponent where T3 : unmanaged, IComponent
        {
            ComponentSet set = default;
            set.Add<T1>();
            set.Add<T2>();
            set.Add<T3>();
            return set;
        }

        public static ComponentSet Create<T1, T2, T3, T4>()
            where T1 : unmanaged, IComponent where T2 : unmanaged, IComponent where T3 : unmanaged, IComponent where T4 : unmanaged, IComponent
        {
            ComponentSet set = default;
            set.Add<T1>();
            set.Add<T2>();
            set.Add<T3>();
            set.Add<T4>();
            return set;
        }

        public static ComponentSet Create<T1, T2, T3, T4, T5>()
            where T1 : unmanaged, IComponent where T2 : unmanaged, IComponent where T3 : unmanaged, IComponent where T4 : unmanaged, IComponent where T5 : unmanaged, IComponent
        {
            ComponentSet set = default;
            set.Add<T1>();
            set.Add<T2>();
            set.Add<T3>();
            set.Add<T4>();
            set.Add<T5>();
            return set;
        }

        public static ComponentSet Create<T1, T2, T3, T4, T5, T6>()
            where T1 : unmanaged, IComponent where T2 : unmanaged, IComponent where T3 : unmanaged, IComponent
            where T4 : unmanaged, IComponent where T5 : unmanaged, IComponent where T6 : unmanaged, IComponent
        {
            ComponentSet set = default;
            set.Add<T1>();
            set.Add<T2>();
            set.Add<T3>();
            set.Add<T4>();
            set.Add<T5>();
            set.Add<T6>();
            return set;
        }

        public static ComponentSet Create<T1, T2, T3, T4, T5, T6, T7>()
            where T1 : unmanaged, IComponent where T2 : unmanaged, IComponent where T3 : unmanaged, IComponent
            where T4 : unmanaged, IComponent where T5 : unmanaged, IComponent where T6 : unmanaged, IComponent where T7 : unmanaged, IComponent
        {
            ComponentSet set = default;
            set.Add<T1>();
            set.Add<T2>();
            set.Add<T3>();
            set.Add<T4>();
            set.Add<T5>();
            set.Add<T6>();
            set.Add<T7>();
            return set;
        }

        public static ComponentSet Create<T1, T2, T3, T4, T5, T6, T7, T8>()
            where T1 : unmanaged, IComponent where T2 : unmanaged, IComponent where T3 : unmanaged, IComponent where T4 : unmanaged, IComponent
            where T5 : unmanaged, IComponent where T6 : unmanaged, IComponent where T7 : unmanaged, IComponent where T8 : unmanaged, IComponent
        {
            ComponentSet set = default;
            set.Add<T1>();
            set.Add<T2>();
            set.Add<T3>();
            set.Add<T4>();
            set.Add<T5>();
            set.Add<T6>();
            set.Add<T7>();
            set.Add<T8>();
            return set;
        }

        #endregion

        #region 功能方法（返回新集合，函数式风格）

        /// <summary>
        /// 并集（返回新集合，不修改原集合）
        /// </summary>
        public ComponentSet Union(in ComponentSet other)
        {
            ComponentSet result = this;
            result.UnionWith(other);
            return result;
        }

        /// <summary>
        /// 交集（返回新集合，不修改原集合）
        /// </summary>
        public ComponentSet Intersection(in ComponentSet other)
        {
            ComponentSet result = this;
            result.IntersectWith(other);
            return result;
        }

        /// <summary>
        /// 差集（返回新集合，不修改原集合）
        /// </summary>
        public ComponentSet Difference(in ComponentSet other)
        {
            ComponentSet result = this;
            result.Remove(other);
            return result;
        }

        /// <summary>
        /// 取反（返回新集合，不修改原集合）
        /// </summary>
        public ComponentSet Inverse()
        {
            ComponentSet result = default;
            result.Set[0] = ~Set[0];
            result.Set[1] = ~Set[1];
            result.Set[2] = ~Set[2];
            result.Set[3] = ~Set[3];
            result.Set[4] = ~Set[4];
            result.Set[5] = ~Set[5];
            result.Set[6] = ~Set[6];
            result.Set[7] = ~Set[7];
            return result;
        }

        #endregion

        #region 接口实现（跨平台安全）

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(in ComponentSet other)
        {
            return Set[0] == other.Set[0] && Set[1] == other.Set[1] && Set[2] == other.Set[2] && Set[3] == other.Set[3] &&
                   Set[4] == other.Set[4] && Set[5] == other.Set[5] && Set[6] == other.Set[6] && Set[7] == other.Set[7];
        }

        bool IEquatable<ComponentSet>.Equals(ComponentSet other) => Equals(other);

        public override bool Equals(object? obj) => obj is ComponentSet other && Equals(other);

        /// <summary>
        /// 使用 HashCodeUtils 计算哈希码（对齐 FrameSync）
        /// 跨平台：算法与平台无关
        /// </summary>
        public override int GetHashCode()
        {
            int hash = HashCodeUtils.UInt64HashCode(Set[0]);
            hash = HashCodeUtils.CombineHashCodes(hash, HashCodeUtils.UInt64HashCode(Set[1]));
            hash = HashCodeUtils.CombineHashCodes(hash, HashCodeUtils.UInt64HashCode(Set[2]));
            hash = HashCodeUtils.CombineHashCodes(hash, HashCodeUtils.UInt64HashCode(Set[3]));
            hash = HashCodeUtils.CombineHashCodes(hash, HashCodeUtils.UInt64HashCode(Set[4]));
            hash = HashCodeUtils.CombineHashCodes(hash, HashCodeUtils.UInt64HashCode(Set[5]));
            hash = HashCodeUtils.CombineHashCodes(hash, HashCodeUtils.UInt64HashCode(Set[6]));
            return HashCodeUtils.CombineHashCodes(hash, HashCodeUtils.UInt64HashCode(Set[7]));
        }

        public override string ToString()
        {
            return $"ComponentSet(Count={CountBits()})";
        }

        /// <summary>
        /// 统计置位数量（跨平台安全）
        /// 使用 BitOperations.PopCount，它在 .NET 8 中已针对各平台优化
        /// </summary>
        public int CountBits()
        {
            // BitOperations.PopCount 是跨平台的，使用硬件指令或软件回退
            return BitOperations.PopCount(Set[0]) +
                   BitOperations.PopCount(Set[1]) +
                   BitOperations.PopCount(Set[2]) +
                   BitOperations.PopCount(Set[3]) +
                   BitOperations.PopCount(Set[4]) +
                   BitOperations.PopCount(Set[5]) +
                   BitOperations.PopCount(Set[6]) +
                   BitOperations.PopCount(Set[7]);
        }

        #endregion

        #region 运算符重载

        public static ComponentSet operator |(in ComponentSet left, in ComponentSet right) => left.Union(right);
        public static ComponentSet operator &(in ComponentSet left, in ComponentSet right) => left.Intersection(right);
        public static ComponentSet operator -(in ComponentSet left, in ComponentSet right) => left.Difference(right);
        public static ComponentSet operator ~(in ComponentSet set) => set.Inverse();
        public static bool operator ==(in ComponentSet left, in ComponentSet right) => left.Equals(right);
        public static bool operator !=(in ComponentSet left, in ComponentSet right) => !left.Equals(right);

        #endregion

        #region 序列化（跨平台安全）

        /// <summary>
        /// 序列化（对齐 FrameSync）
        /// </summary>
        public unsafe static void Serialize(void* ptr, IFrameSerializer serializer)
        {
            var setPtr = (ComponentSet*)ptr;
            serializer.Stream.Serialize(ref setPtr->Set[0]);
            serializer.Stream.Serialize(ref setPtr->Set[1]);
            serializer.Stream.Serialize(ref setPtr->Set[2]);
            serializer.Stream.Serialize(ref setPtr->Set[3]);
            serializer.Stream.Serialize(ref setPtr->Set[4]);
            serializer.Stream.Serialize(ref setPtr->Set[5]);
            serializer.Stream.Serialize(ref setPtr->Set[6]);
            serializer.Stream.Serialize(ref setPtr->Set[7]);
        }

        /// <summary>
        /// 批量序列化（跨平台安全）
        /// 注意：MemoryMarshal 在不同平台可能有不同行为，这里显式逐字节复制
        /// </summary>
        public static void SerializeBatch(ReadOnlySpan<ComponentSet> sets, Span<byte> buffer)
        {
            if (buffer.Length < sets.Length * SIZE)
                throw new ArgumentException("Buffer too small", nameof(buffer));

            // 使用 MemoryMarshal 但要注意平台字节序
            // ComponentSet 内部是 ulong，如果序列化用于网络传输，需要考虑字节序
            var sourceSpan = MemoryMarshal.AsBytes(sets);
            sourceSpan.CopyTo(buffer);
        }

        /// <summary>
        /// 从 Span 创建 ComponentSet（跨平台安全）
        /// </summary>
        public static ComponentSet FromSpan(ReadOnlySpan<byte> data)
        {
            if (data.Length < SIZE)
                throw new ArgumentException("Data too small", nameof(data));

            return Unsafe.ReadUnaligned<ComponentSet>(ref MemoryMarshal.GetReference(data));
        }

        /// <summary>
        /// 转换为小端字节序（网络/跨平台传输用）
        /// </summary>
        public byte[] ToLittleEndianBytes()
        {
            var bytes = new byte[SIZE];
            var span = AsSpan();
            for (int i = 0; i < 8; i++)
            {
                var value = span[i];
                // 显式转小端
                bytes[i * 8 + 0] = (byte)(value);
                bytes[i * 8 + 1] = (byte)(value >> 8);
                bytes[i * 8 + 2] = (byte)(value >> 16);
                bytes[i * 8 + 3] = (byte)(value >> 24);
                bytes[i * 8 + 4] = (byte)(value >> 32);
                bytes[i * 8 + 5] = (byte)(value >> 40);
                bytes[i * 8 + 6] = (byte)(value >> 48);
                bytes[i * 8 + 7] = (byte)(value >> 56);
            }
            return bytes;
        }

        /// <summary>
        /// 从小端字节序创建（网络/跨平台传输用）
        /// </summary>
        public static ComponentSet FromLittleEndianBytes(ReadOnlySpan<byte> data)
        {
            if (data.Length < SIZE)
                throw new ArgumentException("Data too small", nameof(data));

            ComponentSet result = default;
            for (int i = 0; i < 8; i++)
            {
                result.Set[i] =
                    (ulong)data[i * 8 + 0] |
                    ((ulong)data[i * 8 + 1] << 8) |
                    ((ulong)data[i * 8 + 2] << 16) |
                    ((ulong)data[i * 8 + 3] << 24) |
                    ((ulong)data[i * 8 + 4] << 32) |
                    ((ulong)data[i * 8 + 5] << 40) |
                    ((ulong)data[i * 8 + 6] << 48) |
                    ((ulong)data[i * 8 + 7] << 56);
            }
            return result;
        }

        #endregion

        #region 平台特性检测

        /// <summary>
        /// 获取当前平台 SIMD 支持信息（调试/诊断用）
        /// </summary>
        public static string GetPlatformInfo()
        {
            return $"Vector512: {Vector512.IsHardwareAccelerated}, " +
                   $"Vector256: {Vector256.IsHardwareAccelerated}, " +
                   $"Vector128: {Vector128.IsHardwareAccelerated}, " +
                   $"BitOperations.PopCount: {BitOperations.PopCount(0xFFFF) == 16}";
        }

        #endregion
    }
}
