using System;
using System.Buffers;
using System.Collections;
using System.Runtime.CompilerServices;
using Lattice.Core;


namespace Lattice.ECS.Core
{
    /// <summary>
    /// FrameSync 椋庢牸缁勪欢瀛樺偍 - Block-based 瀵嗛泦瀛樺偍
    /// 
    /// 鐗规€э細
    /// 1. Block 鍒嗗潡瀛樺偍锛堢紦瀛樺弸濂斤級
    /// 2. 绋€鐤忔暟缁勬槧灏勶紙O(1) 鏌ユ壘锟?    /// 3. 鐗堟湰鎺у埗锛堟敮鎸佸揩锟?鍙樻洿妫€娴嬶級
    /// 4. 瀵嗛泦鏁扮粍閬嶅巻锛圫IMD 鍙嬪ソ锟?    /// </summary>
    public sealed class ComponentStorage<T> : IComponentStorage, IDisposable where T : struct
    {
        #region 甯搁噺閰嶇疆

        /// <summary>姣忎釜 Block 瀹圭撼鐨勫疄浣撴暟锛堢紦瀛樿瀵归綈锟?/summary>
        public const int BlockCapacity = 64;

        /// <summary>绋€鐤忔暟缁勬墿瀹瑰洜锟?/summary>
        private const int SparseGrowthFactor = 2;

        #endregion

        #region Block 缁撴瀯

        /// <summary>
        /// 缁勪欢鏁版嵁锟?- 瀵嗛泦瀛樺偍锛岀紦瀛樺弸锟?        /// </summary>
        internal struct Block
        {
            /// <summary>瀹炰綋寮曠敤鏁扮粍锛堜笌缁勪欢涓€涓€瀵瑰簲锛孌ispose 鍚庣疆 null锟?/summary>
            public EntityRef[]? Entities;

            /// <summary>缁勪欢鏁版嵁鏁扮粍锛圫oA 甯冨眬锛孌ispose 鍚庣疆 null锟?/summary>
            public T[]? Components;

            /// <summary>褰撳墠宸蹭娇鐢ㄦЫ浣嶆暟</summary>
            public int Count;

            /// <summary>褰撳墠 Block 锟?_blocks 鏁扮粍涓殑绱㈠紩</summary>
            public int BlockIndex;

            /// <summary>鍒濆锟?Block</summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Initialize(int blockIndex)
            {
                Entities = ArrayPool<EntityRef>.Shared.Rent(BlockCapacity);
                Components = ArrayPool<T>.Shared.Rent(BlockCapacity);
                Count = 0;
                BlockIndex = blockIndex;

                // 娓呯┖瀹炰綋鏁扮粍锛堥伩鍏嶈剰鏁版嵁锟?                Array.Clear(Entities, 0, BlockCapacity);
            }

            /// <summary>褰掕繕鏁扮粍鍒版睜</summary>
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

            /// <summary>娣诲姞缁勪欢锟?Block</summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int Add(EntityRef EntityRef, in T component)
            {
                int index = Count++;
                Entities![index] = EntityRef;
                Components![index] = component;
                return index;
            }

            /// <summary>鍒犻櫎 Block 鍐呮寚瀹氱储寮曠殑鍏冪礌锛堜笌鏈熬浜ゆ崲锟?/summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void RemoveAt(int index, out EntityRef movedEntity, out int movedNewIndex)
            {
                int lastIndex = --Count;

                if (index != lastIndex)
                {
                    // 涓庢湯灏惧厓绱犱氦鎹紙淇濇寔瀵嗛泦锟?                    Entities![index] = Entities[lastIndex];
                    Components![index] = Components[lastIndex];

                    movedEntity = Entities[lastIndex];
                    movedNewIndex = index;
                }
                else
                {
                    movedEntity = EntityRef.None;
                    movedNewIndex = -1;
                }

                // 娓呯┖鏈熬
                Entities![lastIndex] = EntityRef.None;
                Components![lastIndex] = default;
            }

            /// <summary>鑾峰彇缁勪欢寮曠敤锛堝厑璁镐慨鏀癸級</summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ref T GetComponent(int index) => ref Components![index];

            /// <summary>鑾峰彇瀹炰綋</summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public EntityRef GetEntity(int index) => Entities![index];
        }

        #endregion

        #region 瀛楁

        // Block 绠＄悊
        private Block[] _blocks;
        private int _blockCount;

        // 绋€鐤忔槧灏勶細Entity.Index 锟?(BlockIndex, ElementIndex)
        // 浣跨敤涓や釜骞惰鏁扮粍锛岄伩锟?struct 瑁呯
        private int[] _sparseBlockIndex;   // -1 琛ㄧず涓嶅瓨锟?        private int[] _sparseElementIndex; // 锟?Block 鍐呯殑绱㈠紩

        // 鐗堟湰鎺у埗锛堢敤浜庡揩锟?鍙樻洿妫€娴嬶級
        private int[] _versions;
        private int _currentVersion;  // 褰撳墠鍏ㄥ眬鐗堟湰锟?
        // 瀛樺湪鎬ф爣璁帮紙蹇€熸鏌ワ級
        private BitArray _exists;

        // 缁熻
        private int _count;           // 娲昏穬缁勪欢锟?        private int _capacity;        // 鎬诲閲忥紙BlockCount * BlockCapacity锟?
        // 结构版本（用于检测遍历期间的结构变更）
        private int _structuralVersion;
        #endregion

        #region 灞烇拷?
        /// <summary>褰撳墠瀛樺偍鐨勭粍浠舵暟锟?/summary>
        public int Count => _count;

        /// <summary>鎬诲锟?/summary>
        public int Capacity => _capacity;

        /// <summary>褰撳墠鐗堟湰鍙凤紙姣忔淇敼閫掑锟?/summary>
        public int Version => _currentVersion;

        /// <summary>结构版本号（结构变更时递增）</summary>
        public int StructuralVersion => _structuralVersion;

        /// <summary>
        /// 验证结构版本是否匹配（用于检测遍历期间的结构变更）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ValidateStructuralVersion(int expectedVersion)
        {
            if (_structuralVersion != expectedVersion)
            {
                throw new InvalidOperationException(
                    $"Collection was modified during enumeration. Expected structural version {expectedVersion}, but found {_structuralVersion}.");
            }
        }

        /// <summary>鏄惁瀛樺湪鎸囧畾瀹炰綋鐨勭粍锟?/summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Has(EntityRef EntityRef)
        {
            uint index = (uint)EntityRef.Index;
            if (index >= (uint)_sparseBlockIndex.Length) return false;
            return _sparseBlockIndex[EntityRef.Index] >= 0;
        }

        #endregion

        #region 鏋勯€犲嚱锟?
        public ComponentStorage(int initialCapacity = 256)
        {
            // 璁＄畻鍒濆 Block 锟?            int initialBlocks = System.Math.Max(1, (initialCapacity + BlockCapacity - 1) / BlockCapacity);

            _blocks = new Block[initialBlocks];
            _blockCount = 0;

            // 绋€鐤忔暟缁勫垵濮嬪ぇ锟?            int sparseSize = 256;
            _sparseBlockIndex = new int[sparseSize];
            _sparseElementIndex = new int[sparseSize];
            Array.Fill(_sparseBlockIndex, -1);  // -1 琛ㄧず涓嶅瓨锟?
            _versions = new int[sparseSize];
            _exists = new BitArray(sparseSize);

            _count = 0;
            _capacity = 0;
            _currentVersion = 1;`r`n            _structuralVersion = 1;
        }

        #endregion

        #region 鏍稿績鎿嶄綔

        /// <summary>
        /// 娣诲姞缁勪欢
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(EntityRef EntityRef, in T component)
        {
            // 纭繚绋€鐤忔暟缁勮冻澶熷ぇ
            EnsureSparseCapacity(EntityRef.Index + 1);

            // 妫€鏌ユ槸鍚﹀凡瀛樺湪
            if (_sparseBlockIndex[EntityRef.Index] >= 0)
            {
                throw new InvalidOperationException(
                    $"EntityRef {EntityRef} already has component {typeof(T).Name}");
            }

            // 鑾峰彇鎴栧垱锟?Block
            int blockIndex = AcquireBlock();
            ref Block block = ref _blocks[blockIndex];

            // 娣诲姞锟?Block
            int elementIndex = block.Add(EntityRef, component);

            // 鏇存柊绋€鐤忔槧锟?            _sparseBlockIndex[EntityRef.Index] = blockIndex;
            _sparseElementIndex[EntityRef.Index] = elementIndex;
            _exists[EntityRef.Index] = true;
            _versions[EntityRef.Index] = ++_currentVersion;

            _count++;
        }

        /// <summary>
        /// 鍒犻櫎缁勪欢锛堜笌鏈熬浜ゆ崲淇濇寔瀵嗛泦锟?        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Remove(EntityRef EntityRef)
        {
            if ((uint)EntityRef.Index >= (uint)_sparseBlockIndex.Length)
                return false;

            int blockIndex = _sparseBlockIndex[EntityRef.Index];
            if (blockIndex < 0)
                return false;  // 涓嶅瓨锟?
            int elementIndex = _sparseElementIndex[EntityRef.Index];
            ref Block block = ref _blocks[blockIndex];

            // 锟?Block 鍒犻櫎锛堜氦鎹級
            block.RemoveAt(elementIndex, out EntityRef movedEntity, out int movedNewIndex);

            // 濡傛灉鍒犻櫎鐨勪笉鏄渶鍚庝竴涓紝鏇存柊琚Щ鍔ㄥ疄浣撶殑绋€鐤忔槧锟?            if (movedNewIndex >= 0)
            {
                _sparseElementIndex[movedEntity.Index] = movedNewIndex;
            }

            // 娓呴櫎褰撳墠瀹炰綋鐨勬槧锟?            _sparseBlockIndex[EntityRef.Index] = -1;
            _sparseElementIndex[EntityRef.Index] = -1;
            _exists[EntityRef.Index] = false;
            _versions[EntityRef.Index] = ++_currentVersion;

            _count--;

            // 妫€鏌ユ槸鍚﹂渶瑕佸洖锟?Block
            if (block.Count == 0)
            {
                ReleaseBlock(blockIndex);
            }

            return true;
        }

        /// <summary>
        /// 鑾峰彇缁勪欢寮曠敤锛堝彲淇敼锟?        /// </summary>
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
        /// 灏濊瘯鑾峰彇缁勪欢
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
        /// 灏濊瘯鑾峰彇缁勪欢寮曠敤锛堥珮鎬ц兘锟?        /// </summary>
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

        #region 閬嶅巻鏀寔

        /// <summary>
        /// 缁勪欢杩唬鍣ㄥ洖璋冨锟?        /// </summary>
        public delegate void ComponentIteratorCallback(EntityRef EntityRef, ref T component);

        /// <summary>
        /// Span 閬嶅巻鍥炶皟濮旀墭
        /// </summary>
        public delegate void ForEachSpanCallback(ReadOnlySpan<EntityRef> entities, Span<T> components);

        /// <summary>
        /// 閬嶅巻鎵€鏈夌粍浠讹紙鍥炶皟鏂瑰紡锛岄浂鍒嗛厤锟?        /// </summary>
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
        /// 閬嶅巻鎵€鏈夌粍浠讹紙Span 鏂瑰紡锟?        /// </summary>
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
        /// 鑾峰彇鎵€鏈夌粍浠跺埌 Span
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
        /// 鑾峰彇鎵€鏈夊疄浣撳埌 Span
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
        /// 鑾峰彇缁勪欢鏋氫妇鍣紙鏀寔 foreach锟?        /// </summary>
        public ComponentEnumerator GetEnumerator() => new ComponentEnumerator(this);

        #endregion

        #region 鐗堟湰鎺у埗

        /// <summary>
        /// 鑾峰彇瀹炰綋鐨勭粍浠剁増鏈彿
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetVersion(EntityRef EntityRef)
        {
            if ((uint)EntityRef.Index >= (uint)_versions.Length)
                return 0;
            return _versions[EntityRef.Index];
        }

        /// <summary>
        /// 鏍囪缁勪欢涓哄凡淇敼
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

        #region 杈呭姪鏂规硶

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

            // 鏂版墿瀹圭殑閮ㄥ垎鍒濆鍖栦负 -1锛堜笉瀛樺湪锟?            for (int i = _sparseBlockIndex.Length / SparseGrowthFactor; i < newSize; i++)
            {
                _sparseBlockIndex[i] = -1;
            }

            // BitArray 闇€瑕侀噸鏂板垱锟?            var newExists = new BitArray(newSize);
            for (int i = 0; i < _exists.Length; i++)
                newExists[i] = _exists[i];
            _exists = newExists;
        }

        private int AcquireBlock()
        {
            // 妫€鏌ユ槸鍚︽湁鏈垵濮嬪寲锟?Block
            if (_blockCount < _blocks.Length)
            {
                int index = _blockCount++;
                _blocks[index].Initialize(index);
                _capacity += BlockCapacity;
                return index;
            }

            // 鎵╁ Block 鏁扮粍
            Array.Resize(ref _blocks, _blocks.Length * 2);
            int newIndex = _blockCount++;
            _blocks[newIndex].Initialize(newIndex);
            _capacity += BlockCapacity;
            return newIndex;
        }

        private void ReleaseBlock(int blockIndex)
        {
            // 鏆傛椂涓嶇湡姝ｉ噴鏀撅紝鍙仛鏍囪
            // 鍚庣画鍙互缁存姢绌洪棽閾捐〃
            _blocks[blockIndex].Dispose();
            _capacity -= BlockCapacity;
        }

        #endregion

        #region 杩唬锟?
        /// <summary>
        /// 杩唬鍣ㄥ綋鍓嶉」缁撴瀯锛堥伩鍏嶄娇锟?ValueTuple 锟?Ref of T锟?        /// </summary>
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
        /// ref struct 迭代器 - 支持 foreach（带结构版本验证）
        /// </summary>
        public ref struct ComponentEnumerator
        {
            private readonly ComponentStorage<T> _storage;
            private readonly int _initialStructuralVersion;
            private int _blockIndex;
            private int _elementIndex;
            private EntityRef _currentEntity;
            private Ref<T> _currentComponent;

            public ComponentEnumerator(ComponentStorage<T> storage)
            {
                _storage = storage;
                _initialStructuralVersion = storage._structuralVersion;
                _blockIndex = 0;
                _elementIndex = -1;
                _currentEntity = EntityRef.None;
                _currentComponent = default;
            }

            public bool MoveNext()
            {
                // 验证结构版本（检测遍历期间的结构变更）
                if (_storage._structuralVersion != _initialStructuralVersion)
                {
                    throw new InvalidOperationException(
                        "Collection was modified during enumeration. " +
                        "Do not add or remove components while iterating.");
                }

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

        #region IComponentStorage 鎺ュ彛瀹炵幇

        /// <summary>
        /// 闈炴硾鍨嬪垹闄ゆ帴鍙ｅ疄鐜?
        /// </summary>
        bool IComponentStorage.Remove(EntityRef entity) => Remove(entity);

        /// <summary>
        /// 妫€鏌ユ槸鍚﹀寘鍚寚瀹氬疄浣撶殑缁勪欢
        /// </summary>
        bool IComponentStorage.Contains(EntityRef entity) => GetBlockIndex(entity) >= 0;

        /// <summary>
        /// 褰撳墠瀛樺偍鐨勭粍浠舵暟閲?
        /// </summary>
        int IComponentStorage.Count => _count;

        /// <summary>
        /// 娓呯┖鎵€鏈夌粍浠?
        /// </summary>
        void IComponentStorage.Clear()
        {
            for (int i = 0; i < _blockCount; i++)
            {
                _blocks[i].Dispose();
            }
            _blockCount = 0;
            _count = 0;

            // 閲嶇疆绋€鐤忔暟缁?
            for (int i = 0; i < _sparseBlockIndex.Length; i++)
            {
                _sparseBlockIndex[i] = -1;
                _sparseElementIndex[i] = -1;
            }
            _exists.SetAll(false);
            _currentVersion++;
        }

        /// <summary>
        /// 缁勪欢绫诲瀷ID
        /// </summary>
        int IComponentStorage.TypeId => ComponentTypeId<T>.Id;

        /// <summary>
        /// 缁勪欢绫诲瀷鍚嶇О
        /// </summary>
        string IComponentStorage.TypeName => typeof(T).Name;

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
    /// 鐢ㄤ簬杩斿洖 ref 鐨勫寘瑁呯粨锟?    /// </summary>
    public readonly ref struct Ref<T>
    {
        private readonly ref T _value;
        public Ref(ref T value) => _value = ref value;
        public ref T Value => ref _value;

        public static implicit operator T(Ref<T> r) => r._value;
    }
}
