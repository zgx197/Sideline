using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Lattice.ECS.Core
{
    /// <summary>
    /// 组件集合位图 - FrameSync 风格
    /// 
    /// 使用 512 位位图表示实体拥有的组件类型。
    /// 支持高效的集合操作（交集、并集、子集判断）。
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ComponentSet : IEquatable<ComponentSet>
    {
        #region 常量

        /// <summary>最大支持的组件类型数</summary>
        public const int MaxComponents = 512;

        /// <summary>每个 ulong 块包含的位数</summary>
        public const int BitsPerBlock = 64;

        /// <summary>需要的 ulong 块数</summary>
        public const int BlockCount = 8;  // 512 / 64 = 8

        #endregion

        #region 字段

        /// <summary>
        /// 位图数据 - 8 个 ulong 覆盖 512 位（FrameSync 风格命名）
        /// </summary>
        [FieldOffset(0)]
        public unsafe fixed ulong Set[BlockCount];

        #endregion

        #region 构造函数

        /// <summary>
        /// 创建空的组件集合
        /// </summary>
        public static ComponentSet Empty => default;

        /// <summary>
        /// 创建包含单个组件的集合
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ComponentSet Create(int componentIndex)
        {
            var set = new ComponentSet();
            set.Add(componentIndex);
            return set;
        }

        /// <summary>
        /// 创建包含两个组件的集合
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ComponentSet Create(int index0, int index1)
        {
            var set = new ComponentSet();
            set.Add(index0);
            set.Add(index1);
            return set;
        }

        /// <summary>
        /// 创建包含三个组件的集合
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ComponentSet Create(int index0, int index1, int index2)
        {
            var set = new ComponentSet();
            set.Add(index0);
            set.Add(index1);
            set.Add(index2);
            return set;
        }

        /// <summary>
        /// 创建包含四个组件的集合
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ComponentSet Create(int index0, int index1, int index2, int index3)
        {
            var set = new ComponentSet();
            set.Add(index0);
            set.Add(index1);
            set.Add(index2);
            set.Add(index3);
            return set;
        }

        /// <summary>
        /// 创建包含五个组件的集合
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ComponentSet Create(int index0, int index1, int index2, int index3, int index4)
        {
            var set = new ComponentSet();
            set.Add(index0);
            set.Add(index1);
            set.Add(index2);
            set.Add(index3);
            set.Add(index4);
            return set;
        }

        #endregion

        #region 基本操作

        /// <summary>
        /// 检查是否包含指定组件（FrameSync 命名风格）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe bool IsSet(int index)
        {
#if DEBUG
            if ((uint)index >= MaxComponents)
                throw new ArgumentOutOfRangeException(nameof(index), $"Component index {index} out of range [0, {MaxComponents})");
#endif
            int blockIndex = index >> 6;      // index / 64
            int bitIndex = index & 0x3F;      // index % 64
            return (Set[blockIndex] & (1UL << bitIndex)) != 0;
        }

        /// <summary>
        /// 检查是否包含指定组件类型（泛型版本，零开销）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsSet<T>() where T : unmanaged, IComponent
        {
            return Contains(ComponentTypeId<T>.Id);
        }

        /// <summary>
        /// 添加组件类型到集合（泛型版本，零开销）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add<T>() where T : unmanaged, IComponent
        {
            Add(ComponentTypeId<T>.Id);
        }

        /// <summary>
        /// 从集合移除组件类型（泛型版本，零开销）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove<T>() where T : unmanaged, IComponent
        {
            Remove(ComponentTypeId<T>.Id);
        }

        /// <summary>
        /// 创建包含指定组件类型的集合（泛型版本）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ComponentSet Create<T0>() where T0 : unmanaged, IComponent
        {
            ComponentSet set = default;
            set.Add<T0>();
            return set;
        }

        /// <summary>
        /// 创建包含指定组件类型的集合（2个类型）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ComponentSet Create<T0, T1>()
            where T0 : unmanaged, IComponent
            where T1 : unmanaged, IComponent
        {
            ComponentSet set = default;
            set.Add<T0>();
            set.Add<T1>();
            return set;
        }

        /// <summary>
        /// 创建包含指定组件类型的集合（3个类型）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ComponentSet Create<T0, T1, T2>()
            where T0 : unmanaged, IComponent
            where T1 : unmanaged, IComponent
            where T2 : unmanaged, IComponent
        {
            ComponentSet set = default;
            set.Add<T0>();
            set.Add<T1>();
            set.Add<T2>();
            return set;
        }

        /// <summary>
        /// 创建包含指定组件类型的集合（4个类型）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ComponentSet Create<T0, T1, T2, T3>()
            where T0 : unmanaged, IComponent
            where T1 : unmanaged, IComponent
            where T2 : unmanaged, IComponent
            where T3 : unmanaged, IComponent
        {
            ComponentSet set = default;
            set.Add<T0>();
            set.Add<T1>();
            set.Add<T2>();
            set.Add<T3>();
            return set;
        }

        /// <summary>
        /// 添加组件到集合
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void Add(int index)
        {
#if DEBUG
            if ((uint)index >= MaxComponents)
                throw new ArgumentOutOfRangeException(nameof(index), $"Component index {index} out of range [0, {MaxComponents})");
#endif
            int blockIndex = index >> 6;
            int bitIndex = index & 0x3F;
            Set[blockIndex] |= (1UL << bitIndex);
        }

        /// <summary>
        /// 从集合移除组件
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void Remove(int index)
        {
#if DEBUG
            if ((uint)index >= MaxComponents)
                throw new ArgumentOutOfRangeException(nameof(index), $"Component index {index} out of range [0, {MaxComponents})");
#endif
            int blockIndex = index >> 6;
            int bitIndex = index & 0x3F;
            Set[blockIndex] &= ~(1UL << bitIndex);
        }

        /// <summary>
        /// 切换组件存在性
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void Toggle(int index)
        {
#if DEBUG
            if ((uint)index >= MaxComponents)
                throw new ArgumentOutOfRangeException(nameof(index), $"Component index {index} out of range [0, {MaxComponents})");
#endif
            int blockIndex = index >> 6;
            int bitIndex = index & 0x3F;
            Set[blockIndex] ^= (1UL << bitIndex);
        }

        /// <summary>
        /// 清空集合
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void Clear()
        {
            Set[0] = 0;
            Set[1] = 0;
            Set[2] = 0;
            Set[3] = 0;
            Set[4] = 0;
            Set[5] = 0;
            Set[6] = 0;
            Set[7] = 0;
        }

        /// <summary>
        /// 是否为空集合
        /// </summary>
        public unsafe bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return Set[0] == 0 && Set[1] == 0 && Set[2] == 0 && Set[3] == 0
                    && Set[4] == 0 && Set[5] == 0 && Set[6] == 0 && Set[7] == 0;
            }
        }

        /// <summary>
        /// 集合中组件数量
        /// </summary>
        public unsafe int Count
        {
            get
            {
                int count = 0;
                count += System.Numerics.BitOperations.PopCount(Set[0]);
                count += System.Numerics.BitOperations.PopCount(Set[1]);
                count += System.Numerics.BitOperations.PopCount(Set[2]);
                count += System.Numerics.BitOperations.PopCount(Set[3]);
                count += System.Numerics.BitOperations.PopCount(Set[4]);
                count += System.Numerics.BitOperations.PopCount(Set[5]);
                count += System.Numerics.BitOperations.PopCount(Set[6]);
                count += System.Numerics.BitOperations.PopCount(Set[7]);
                return count;
            }
        }

        #endregion

        #region 集合运算

        /// <summary>
        /// 检查是否包含另一个集合的所有组件（子集判断）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe bool IsSupersetOf(in ComponentSet other)
        {
            return (Set[0] & other.Set[0]) == other.Set[0]
                && (Set[1] & other.Set[1]) == other.Set[1]
                && (Set[2] & other.Set[2]) == other.Set[2]
                && (Set[3] & other.Set[3]) == other.Set[3]
                && (Set[4] & other.Set[4]) == other.Set[4]
                && (Set[5] & other.Set[5]) == other.Set[5]
                && (Set[6] & other.Set[6]) == other.Set[6]
                && (Set[7] & other.Set[7]) == other.Set[7];
        }

        /// <summary>
        /// 检查是否是另一个集合的子集
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsSubsetOf(in ComponentSet other) => other.IsSupersetOf(this);

        #region 别名方法（向后兼容）

        /// <summary>
        /// 检查是否包含指定组件（Contains 别名）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe bool Contains(int index) => IsSet(index);

        /// <summary>
        /// 检查是否包含指定组件类型（Contains 别名）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains<T>() where T : unmanaged, IComponent => IsSet<T>();

        #endregion

        /// <summary>
        /// 检查两个集合是否有交集
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe bool Overlaps(in ComponentSet other)
        {
            return (Set[0] & other.Set[0]) != 0
                || (Set[1] & other.Set[1]) != 0
                || (Set[2] & other.Set[2]) != 0
                || (Set[3] & other.Set[3]) != 0
                || (Set[4] & other.Set[4]) != 0
                || (Set[5] & other.Set[5]) != 0
                || (Set[6] & other.Set[6]) != 0
                || (Set[7] & other.Set[7]) != 0;
        }

        /// <summary>
        /// 并集：添加另一个集合的所有组件
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void UnionWith(in ComponentSet other)
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
        /// 交集：只保留两个集合共有的组件
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void IntersectWith(in ComponentSet other)
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
        /// 差集：移除另一个集合中存在的组件
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void ExceptWith(in ComponentSet other)
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

        /// <summary>
        /// 对称差集：保留只存在于一个集合中的组件
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void SymmetricExceptWith(in ComponentSet other)
        {
            Set[0] ^= other.Set[0];
            Set[1] ^= other.Set[1];
            Set[2] ^= other.Set[2];
            Set[3] ^= other.Set[3];
            Set[4] ^= other.Set[4];
            Set[5] ^= other.Set[5];
            Set[6] ^= other.Set[6];
            Set[7] ^= other.Set[7];
        }

        #endregion

        #region 与子集的互操作（FrameSync 风格）

        /// <summary>
        /// 检查是否包含 ComponentSet64 的所有组件
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe bool IsSupersetOf(in ComponentSet64 other)
        {
            return (Set[0] & other.Set) == other.Set;
        }

        /// <summary>
        /// 检查是否包含 ComponentSet256 的所有组件
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe bool IsSupersetOf(in ComponentSet256 other)
        {
            return (Set[0] & other.Set[0]) == other.Set[0]
                && (Set[1] & other.Set[1]) == other.Set[1]
                && (Set[2] & other.Set[2]) == other.Set[2]
                && (Set[3] & other.Set[3]) == other.Set[3];
        }

        /// <summary>
        /// 添加 ComponentSet64 的所有组件
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void UnionWith(in ComponentSet64 other)
        {
            Set[0] |= other.Set;
        }

        /// <summary>
        /// 添加 ComponentSet256 的所有组件
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void UnionWith(in ComponentSet256 other)
        {
            Set[0] |= other.Set[0];
            Set[1] |= other.Set[1];
            Set[2] |= other.Set[2];
            Set[3] |= other.Set[3];
        }

        /// <summary>
        /// 只保留与 ComponentSet64 共有的组件
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void IntersectWith(in ComponentSet64 other)
        {
            Set[0] &= other.Set;
            Set[1] = 0;
            Set[2] = 0;
            Set[3] = 0;
            Set[4] = 0;
            Set[5] = 0;
            Set[6] = 0;
            Set[7] = 0;
        }

        /// <summary>
        /// 只保留与 ComponentSet256 共有的组件
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void IntersectWith(in ComponentSet256 other)
        {
            Set[0] &= other.Set[0];
            Set[1] &= other.Set[1];
            Set[2] &= other.Set[2];
            Set[3] &= other.Set[3];
            Set[4] = 0;
            Set[5] = 0;
            Set[6] = 0;
            Set[7] = 0;
        }

        #endregion

        #region 静态集合运算（返回新集合）

        /// <summary>
        /// 计算并集（返回新集合）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ComponentSet Union(ComponentSet a, ComponentSet b)
        {
            a.UnionWith(b);
            return a;
        }

        /// <summary>
        /// 计算交集（返回新集合）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ComponentSet Intersection(ComponentSet a, ComponentSet b)
        {
            a.IntersectWith(b);
            return a;
        }

        /// <summary>
        /// 计算差集（返回新集合）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ComponentSet Difference(ComponentSet a, ComponentSet b)
        {
            a.ExceptWith(b);
            return a;
        }

        #endregion

        #region 相等性比较

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe bool Equals(ComponentSet other)
        {
            return Set[0] == other.Set[0]
                && Set[1] == other.Set[1]
                && Set[2] == other.Set[2]
                && Set[3] == other.Set[3]
                && Set[4] == other.Set[4]
                && Set[5] == other.Set[5]
                && Set[6] == other.Set[6]
                && Set[7] == other.Set[7];
        }

        public override bool Equals(object? obj) => obj is ComponentSet other && Equals(other);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(ComponentSet left, ComponentSet right) => left.Equals(right);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(ComponentSet left, ComponentSet right) => !left.Equals(right);

        public unsafe override int GetHashCode()
        {
            // 组合所有块的哈希
            int hash = (int)Set[0];
            hash = System.HashCode.Combine(hash, (int)(Set[0] >> 32));
            hash = System.HashCode.Combine(hash, (int)Set[1]);
            hash = System.HashCode.Combine(hash, (int)(Set[1] >> 32));
            hash = System.HashCode.Combine(hash, (int)Set[2]);
            hash = System.HashCode.Combine(hash, (int)(Set[2] >> 32));
            hash = System.HashCode.Combine(hash, (int)Set[3]);
            hash = System.HashCode.Combine(hash, (int)(Set[3] >> 32));
            return hash;
        }

        #endregion

        #region 序列化支持

        /// <summary>
        /// 将集合写入 Span（用于快照序列化）
        /// </summary>
        public unsafe void WriteTo(Span<ulong> destination)
        {
            if (destination.Length < BlockCount)
                throw new ArgumentException("Destination span too small", nameof(destination));

            destination[0] = Set[0];
            destination[1] = Set[1];
            destination[2] = Set[2];
            destination[3] = Set[3];
            destination[4] = Set[4];
            destination[5] = Set[5];
            destination[6] = Set[6];
            destination[7] = Set[7];
        }

        /// <summary>
        /// 从 Span 读取集合（用于快照反序列化）
        /// </summary>
        public unsafe void ReadFrom(ReadOnlySpan<ulong> source)
        {
            if (source.Length < BlockCount)
                throw new ArgumentException("Source span too small", nameof(source));

            Set[0] = source[0];
            Set[1] = source[1];
            Set[2] = source[2];
            Set[3] = source[3];
            Set[4] = source[4];
            Set[5] = source[5];
            Set[6] = source[6];
            Set[7] = source[7];
        }

        #endregion

        #region 调试支持

        public unsafe override string ToString()
        {
            if (IsEmpty) return "ComponentSet{}";

            var sb = new System.Text.StringBuilder();
            sb.Append("ComponentSet{");

            bool first = true;
            for (int i = 0; i < MaxComponents; i++)
            {
                if (Contains(i))
                {
                    if (!first) sb.Append(", ");
                    sb.Append(i);
                    first = false;
                }
            }

            sb.Append('}');
            return sb.ToString();
        }

        #endregion
    }
}
