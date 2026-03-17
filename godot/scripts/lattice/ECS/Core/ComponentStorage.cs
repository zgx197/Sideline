using System;
using System.Buffers;
using System.Collections;
using System.Runtime.CompilerServices;
using Lattice.Core;


namespace Lattice.ECS.Core
{
    /// <summary>
    /// FrameSync 风格组件存储 - Block-based 密集存储
    /// 
    /// 特性：
    /// 1. Block 分块存储（缓存友好）
    /// 2. 稀疏数组映射（O(1) 查找�?    /// 3. 版本控制（支持快�?变更检测）
    /// 4. 密集数组遍历（SIMD 友好�?    /// </summary>
    public sealed class ComponentStorage<T> : IDisposable where T : struct
    {
        #region 常量配置

        /// <summary>每个 Block 容纳的实体数（缓存行对齐�?/summary>
        public const int BlockCapacity = 64;

        /// <summary>稀疏数组扩容因�?/summary>
        private const int SparseGrowthFactor = 2;

        #endregion

        #region Block 结构

        /// <summary>
        /// 组件数据�?- 密集存储，缓存友�?        /// </summary>
        internal struct Block
        {
            /// <summary>实体引用数组（与组件一一对应，Dispose 后置 null�?/summary>
            public EntityRef[]? Entities;

            /// <summary>组件数据数组（SoA 布局，Dispose 后置 null�?/summary>
            public T[]? Components;

            /// <summary>当前已使用槽位数</summary>
            public int Count;

            /// <summary>当前 Block �?_blocks 数组中的索引</summary>
            public int BlockIndex;

            /// <summary>初始�?Block</summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Initialize(int blockIndex)
            {
                Entities = ArrayPool<EntityRef>.Shared.Rent(BlockCapacity);
                Components = ArrayPool<T>.Shared.Rent(BlockCapacity);
                Count = 0;
                BlockIndex = blockIndex;

                // 清空实体数组（避免脏数据�?                Array.Clear(Entities, 0, BlockCapacity);
            }

            /// <summary>归还数组到池</summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Dispose()
            {
                if (Entities != null)
                {
                    ArrayPool<EntityRef>.Shared.Return(Entities, clearArray: true);
                    Entities = null;
                }
                if (Components != null)
                {
                    ArrayPool<T>.Shared.Return(Components, clearArray: true);
                    Components = null;
                }
                Count = 0;
            }

            /// <summary>添加组件�?Block</summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int Add(EntityRef EntityRef, in T component)
            {
                int index = Count++;
                Entities![index] = EntityRef;
                Components![index] = component;
                return index;
            }

            /// <summary>删除 Block 内指定索引的元素（与末尾交换�?/summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void RemoveAt(int index, out EntityRef movedEntity, out int movedNewIndex)
            {
                int lastIndex = --Count;

                if (index != lastIndex)
                {
                    // 与末尾元素交换（保持密集�?                    Entities![index] = Entities[lastIndex];
                    Components![index] = Components[lastIndex];

                    movedEntity = Entities[lastIndex];
                    movedNewIndex = index;
                }
                else
                {
                    movedEntity = EntityRef.None;
                    movedNewIndex = -1;
                }

                // 清空末尾
                Entities![lastIndex] = EntityRef.None;
                Components![lastIndex] = default;
            }

            /// <summary>获取组件引用（允许修改）</summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ref T GetComponent(int index) => ref Components![index];

            /// <summary>获取实体</summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public EntityRef GetEntity(int index) => Entities![index];
        }

        #endregion

        #region 字段

        // Block 管理
        private Block[] _blocks;
        private int _blockCount;

        // 稀疏映射：Entity.Index �?(BlockIndex, ElementIndex)
        // 使用两个并行数组，避�?struct 装箱
        private int[] _sparseBlockIndex;   // -1 表示不存�?        private int[] _sparseElementIndex; // �?Block 内的索引

        // 版本控制（用于快�?变更检测）
        private int[] _versions;
        private int _currentVersion;  // 当前全局版本�?
        // 存在性标记（快速检查）
        private BitArray _exists;

        // 统计
        private int _count;           // 活跃组件�?        private int _capacity;        // 总容量（BlockCount * BlockCapacity�?
        #endregion

        #region 属�?
        /// <summary>当前存储的组件数�?/summary>
        public int Count => _count;

        /// <summary>总容�?/summary>
        public int Capacity => _capacity;

        /// <summary>当前版本号（每次修改递增�?/summary>
        public int Version => _currentVersion;

        /// <summary>是否存在指定实体的组�?/summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Has(EntityRef EntityRef)
        {
            uint index = (uint)EntityRef.Index;
            if (index >= (uint)_sparseBlockIndex.Length) return false;
            return _sparseBlockIndex[EntityRef.Index] >= 0;
        }

        #endregion

        #region 构造函�?
        public ComponentStorage(int initialCapacity = 256)
        {
            // 计算初始 Block �?            int initialBlocks = System.Math.Max(1, (initialCapacity + BlockCapacity - 1) / BlockCapacity);

            _blocks = new Block[initialBlocks];
            _blockCount = 0;

            // 稀疏数组初始大�?            int sparseSize = 256;
            _sparseBlockIndex = new int[sparseSize];
            _sparseElementIndex = new int[sparseSize];
            Array.Fill(_sparseBlockIndex, -1);  // -1 表示不存�?
            _versions = new int[sparseSize];
            _exists = new BitArray(sparseSize);

            _count = 0;
            _capacity = 0;
            _currentVersion = 1;
        }

        #endregion

        #region 核心操作

        /// <summary>
        /// 添加组件
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(EntityRef EntityRef, in T component)
        {
            // 确保稀疏数组足够大
            EnsureSparseCapacity(EntityRef.Index + 1);

            // 检查是否已存在
            if (_sparseBlockIndex[EntityRef.Index] >= 0)
            {
                throw new InvalidOperationException(
                    $"EntityRef {EntityRef} already has component {typeof(T).Name}");
            }

            // 获取或创�?Block
            int blockIndex = AcquireBlock();
            ref Block block = ref _blocks[blockIndex];

            // 添加�?Block
            int elementIndex = block.Add(EntityRef, component);

            // 更新稀疏映�?            _sparseBlockIndex[EntityRef.Index] = blockIndex;
            _sparseElementIndex[EntityRef.Index] = elementIndex;
            _exists[EntityRef.Index] = true;
            _versions[EntityRef.Index] = ++_currentVersion;

            _count++;
        }

        /// <summary>
        /// 删除组件（与末尾交换保持密集�?        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Remove(EntityRef EntityRef)
        {
            if ((uint)EntityRef.Index >= (uint)_sparseBlockIndex.Length)
                return false;

            int blockIndex = _sparseBlockIndex[EntityRef.Index];
            if (blockIndex < 0)
                return false;  // 不存�?
            int elementIndex = _sparseElementIndex[EntityRef.Index];
            ref Block block = ref _blocks[blockIndex];

            // �?Block 删除（交换）
            block.RemoveAt(elementIndex, out EntityRef movedEntity, out int movedNewIndex);

            // 如果删除的不是最后一个，更新被移动实体的稀疏映�?            if (movedNewIndex >= 0)
            {
                _sparseElementIndex[movedEntity.Index] = movedNewIndex;
            }

            // 清除当前实体的映�?            _sparseBlockIndex[EntityRef.Index] = -1;
            _sparseElementIndex[EntityRef.Index] = -1;
            _exists[EntityRef.Index] = false;
            _versions[EntityRef.Index] = ++_currentVersion;

            _count--;

            // 检查是否需要回�?Block
            if (block.Count == 0)
            {
                ReleaseBlock(blockIndex);
            }

            return true;
        }

        /// <summary>
        /// 获取组件引用（可修改�?        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T Get(EntityRef EntityRef)
        {
            int blockIndex = GetBlockIndex(EntityRef);
            if (blockIndex < 0)
                throw new KeyNotFoundException(
                    $"EntityRef {EntityRef} does not have component {typeof(T).Name}");

            int elementIndex = _sparseElementIndex[EntityRef.Index];
            return ref _blocks[blockIndex].GetComponent(elementIndex);
        }

        /// <summary>
        /// 尝试获取组件
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGet(EntityRef EntityRef, out T component)
        {
            int blockIndex = GetBlockIndex(EntityRef);
            if (blockIndex < 0)
            {
                component = default;
                return false;
            }

            int elementIndex = _sparseElementIndex[EntityRef.Index];
            component = _blocks[blockIndex].GetComponent(elementIndex);
            return true;
        }

        /// <summary>
        /// 尝试获取组件引用（高性能�?        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetRef(EntityRef EntityRef, out Ref<T> component)
        {
            int blockIndex = GetBlockIndex(EntityRef);
            if (blockIndex < 0)
            {
                component = default;
                return false;
            }

            int elementIndex = _sparseElementIndex[EntityRef.Index];
            component = new Ref<T>(ref _blocks[blockIndex].GetComponent(elementIndex));
            return true;
        }

        #endregion

        #region 遍历支持

        /// <summary>
        /// 组件迭代器回调委�?        /// </summary>
        public delegate void ComponentIteratorCallback(EntityRef EntityRef, ref T component);

        /// <summary>
        /// Span 遍历回调委托
        /// </summary>
        public delegate void ForEachSpanCallback(ReadOnlySpan<EntityRef> entities, Span<T> components);

        /// <summary>
        /// 遍历所有组件（回调方式，零分配�?        /// </summary>
        public void ForEach(ComponentIteratorCallback callback)
        {
            for (int i = 0; i < _blockCount; i++)
            {
                ref Block block = ref _blocks[i];
                if (block.Count == 0) continue;

                for (int j = 0; j < block.Count; j++)
                {
                    callback(block.Entities![j], ref block.Components![j]);
                }
            }
        }

        /// <summary>
        /// 遍历所有组件（Span 方式�?        /// </summary>
        public void ForEachSpan(ForEachSpanCallback callback)
        {
            for (int i = 0; i < _blockCount; i++)
            {
                ref Block block = ref _blocks[i];
                if (block.Count == 0) continue;

                var entities = block.Entities.AsSpan(0, block.Count);
                var components = block.Components.AsSpan(0, block.Count);

                callback(entities, components);
            }
        }

        /// <summary>
        /// 获取所有组件到 Span
        /// </summary>
        public int GetAllComponents(Span<T> buffer)
        {
            int count = 0;
            for (int i = 0; i < _blockCount; i++)
            {
                ref Block block = ref _blocks[i];
                if (block.Count == 0) continue;

                var span = block.Components.AsSpan(0, block.Count);
                span.CopyTo(buffer.Slice(count));
                count += block.Count;
            }
            return count;
        }

        /// <summary>
        /// 获取所有实体到 Span
        /// </summary>
        public int GetAllEntities(Span<EntityRef> buffer)
        {
            int count = 0;
            for (int i = 0; i < _blockCount; i++)
            {
                ref Block block = ref _blocks[i];
                if (block.Count == 0) continue;

                var span = block.Entities.AsSpan(0, block.Count);
                span.CopyTo(buffer.Slice(count));
                count += block.Count;
            }
            return count;
        }

        /// <summary>
        /// 获取组件枚举器（支持 foreach�?        /// </summary>
        public ComponentEnumerator GetEnumerator() => new ComponentEnumerator(this);

        #endregion

        #region 版本控制

        /// <summary>
        /// 获取实体的组件版本号
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetVersion(EntityRef EntityRef)
        {
            if ((uint)EntityRef.Index >= (uint)_versions.Length)
                return 0;
            return _versions[EntityRef.Index];
        }

        /// <summary>
        /// 标记组件为已修改
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MarkChanged(EntityRef EntityRef)
        {
            if ((uint)EntityRef.Index < (uint)_versions.Length && _exists[EntityRef.Index])
            {
                _versions[EntityRef.Index] = ++_currentVersion;
            }
        }

        #endregion

        #region 辅助方法

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetBlockIndex(EntityRef EntityRef)
        {
            uint index = (uint)EntityRef.Index;
            if (index >= (uint)_sparseBlockIndex.Length)
                return -1;
            return _sparseBlockIndex[EntityRef.Index];
        }

        private void EnsureSparseCapacity(int required)
        {
            if (required <= _sparseBlockIndex.Length)
                return;

            int newSize = System.Math.Max(required, _sparseBlockIndex.Length * SparseGrowthFactor);

            Array.Resize(ref _sparseBlockIndex, newSize);
            Array.Resize(ref _sparseElementIndex, newSize);
            Array.Resize(ref _versions, newSize);

            // 新扩容的部分初始化为 -1（不存在�?            for (int i = _sparseBlockIndex.Length / SparseGrowthFactor; i < newSize; i++)
            {
                _sparseBlockIndex[i] = -1;
            }

            // BitArray 需要重新创�?            var newExists = new BitArray(newSize);
            for (int i = 0; i < _exists.Length; i++)
                newExists[i] = _exists[i];
            _exists = newExists;
        }

        private int AcquireBlock()
        {
            // 检查是否有未初始化�?Block
            if (_blockCount < _blocks.Length)
            {
                int index = _blockCount++;
                _blocks[index].Initialize(index);
                _capacity += BlockCapacity;
                return index;
            }

            // 扩容 Block 数组
            Array.Resize(ref _blocks, _blocks.Length * 2);
            int newIndex = _blockCount++;
            _blocks[newIndex].Initialize(newIndex);
            _capacity += BlockCapacity;
            return newIndex;
        }

        private void ReleaseBlock(int blockIndex)
        {
            // 暂时不真正释放，只做标记
            // 后续可以维护空闲链表
            _blocks[blockIndex].Dispose();
            _capacity -= BlockCapacity;
        }

        #endregion

        #region 迭代�?
        /// <summary>
        /// 迭代器当前项结构（避免使�?ValueTuple �?Ref of T�?        /// </summary>
        public readonly ref struct ComponentEnumeratorItem
        {
            public readonly EntityRef EntityRef;
            private readonly Ref<T> _component;

            public ComponentEnumeratorItem(EntityRef EntityRef, Ref<T> component)
            {
                EntityRef = EntityRef;
                _component = component;
            }

            public ref T Component => ref _component.Value;
        }

        /// <summary>
        /// ref struct 迭代�?- 支持 foreach
        /// </summary>
        public ref struct ComponentEnumerator
        {
            private readonly ComponentStorage<T> _storage;
            private int _blockIndex;
            private int _elementIndex;
            private EntityRef _currentEntity;
            private Ref<T> _currentComponent;

            public ComponentEnumerator(ComponentStorage<T> storage)
            {
                _storage = storage;
                _blockIndex = 0;
                _elementIndex = -1;
                _currentEntity = EntityRef.None;
                _currentComponent = default;
            }

            public bool MoveNext()
            {
                while (_blockIndex < _storage._blockCount)
                {
                    ref var block = ref _storage._blocks[_blockIndex];

                    _elementIndex++;
                    if (_elementIndex < block.Count)
                    {
                        _currentEntity = block.Entities![_elementIndex];
                        _currentComponent = new Ref<T>(ref block.Components![_elementIndex]);
                        return true;
                    }

                    _blockIndex++;
                    _elementIndex = -1;
                }
                return false;
            }

            public readonly ComponentEnumeratorItem Current =>
                new ComponentEnumeratorItem(_currentEntity, _currentComponent);
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            for (int i = 0; i < _blockCount; i++)
            {
                _blocks[i].Dispose();
            }
            _blocks = null!;
            _blockCount = 0;
            _count = 0;
        }

        #endregion
    }

    /// <summary>
    /// 用于返回 ref 的包装结�?    /// </summary>
    public readonly ref struct Ref<T>
    {
        private readonly ref T _value;
        public Ref(ref T value) => _value = ref value;
        public ref T Value => ref _value;

        public static implicit operator T(Ref<T> r) => r._value;
    }
}
