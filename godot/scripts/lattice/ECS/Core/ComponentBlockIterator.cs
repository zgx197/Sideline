// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

// 姝ゆ枃浠朵娇鐢?unsafe 浠ｇ爜杩涜楂樻€ц兘杩唬
// 鎵€鏈夋寚閽堟搷浣滃湪 DEBUG 妯″紡涓嬫湁杈圭晫妫€鏌?

using System;
using System.Runtime.CompilerServices;
using Lattice.Core;

namespace Lattice.ECS.Core
{
    /// <summary>
    /// 缁勪欢鍧楄凯浠ｅ櫒 - 鎵归噺閬嶅巻缁勪欢锛屾渶澶у寲缂撳瓨鍛戒腑鐜?
    /// 
    /// ============================================================
    /// 涓轰粈涔堥渶瑕?Block Iterator锛?
    /// ============================================================
    /// 
    /// 浼犵粺杩唬鏂瑰紡鐨勯棶棰橈細
    ///   foreach (var entity in filter) { ... }
    ///   - 姣忔杩唬閮借妫€鏌ョ増鏈彿
    ///   - 姣忔閮借璁＄畻 Block/Offset
    ///   - 缂撳瓨鏈懡涓巼楂橈紙璺冲埌涓嬩竴涓疄浣擄級
    /// 
    /// Block Iterator 鐨勪紭鍔匡細
    ///   while (iterator.NextBlock(out entities, out comps, out count)) {
    ///       for (int i = 0; i &lt; count; i++) { ... }
    ///   }
    ///   - 涓€娆¤幏鍙?128 涓粍浠?
    ///   - 鍐呭眰寰幆鏃犲垎鏀€佹棤鍑芥暟璋冪敤
    ///   - 缂撳瓨鍛戒腑鐜囨帴杩?100%
    /// 
    /// 鎬ц兘瀵规瘮锛堢悊璁猴級锛?
    /// - 浼犵粺杩唬锛殈50-100 CPU 鍛ㄦ湡/瀹炰綋
    /// - Block 杩唬锛殈5-10 CPU 鍛ㄦ湡/瀹炰綋锛堝唴灞傚惊鐜級
    /// 
    /// ============================================================
    /// 鏋舵瀯璁捐鍐崇瓥
    /// ============================================================
    /// 
    /// Q: 涓轰粈涔堟彁渚涗袱绉嶈凯浠ｆā寮忥紵
    /// A:
    ///   1. NextBlock锛氭壒閲忓鐞嗭紝閫傚悎 SIMD锛堜竴娆″鐞?128 涓級
    ///   2. Next锛氶€愪釜澶勭悊锛岄€傚悎澶嶆潅閫昏緫锛堟瘡涓疄浣撲笉鍚屾搷浣滐級
    /// 
    /// Q: 涓轰粈涔堣烦杩囩储寮?0锛?
    /// A:
    ///   1. 涓?FrameSync 淇濇寔涓€鑷达紙绱㈠紩 0 淇濈暀涓烘棤鏁堝€硷級
    ///   2. 绠€鍖栧垹闄ら€昏緫锛氱敤 0 浣滀负 TOMBSTONE
    ///   3. 閬垮厤绌哄紩鐢ㄦ鏌ワ細entity.Index == 0 鐩存帴杩斿洖鏃犳晥
    /// 
    /// Q: 涓轰粈涔堥渶瑕佺増鏈彿妫€娴嬶紵
    /// A:
    ///   1. C# IEnumerator 妯″紡锛氭鏌ラ泦鍚堜慨鏀?
    ///   2. 璋冭瘯鍙嬪ソ锛氬揩閫熷け璐ワ紝缁欏嚭娓呮櫚閿欒
    ///   3. 鎬ц兘寮€閿€灏忥細鍙湪 DEBUG 妯″紡妫€鏌?
    /// 
    /// ============================================================
    /// 棰勫彇浼樺寲 (PrefetchedBlockIterator)
    /// ============================================================
    /// 
    /// 闂锛氬鐞嗗綋鍓?Block 鏃讹紝涓嬩竴涓?Block 涓嶅湪缂撳瓨涓?
    /// 瑙ｅ喅锛氬湪 CPU 澶勭悊褰撳墠鏁版嵁鏃讹紝寮傛鍔犺浇涓嬩竴涓?Block
    /// 
    /// 纭欢棰勫彇 vs 杞欢棰勫彇锛?
    /// - 纭欢棰勫彇锛氳嚜鍔ㄦ娴嬮『搴忚闂ā寮忥紝浣嗗欢杩熻緝楂?
    /// - 杞欢棰勫彇锛氱▼搴忓憳鏄庣‘鎸囩ず锛屾彁鍓?100+ 鍛ㄦ湡寮€濮嬪姞杞?
    /// 
    /// 棰勫彇璺濈锛?
    /// - 澶繎锛氭暟鎹繕娌＄敤瀹屽氨鍔犺浇锛屾氮璐瑰甫瀹?
    /// - 澶繙锛氭暟鎹鍏朵粬缂撳瓨琛岄┍閫?
    /// - 缁忛獙鍊硷細2 涓?Block锛?56 涓粍浠讹紝绾?4-8KB锛?
    /// 
    /// 浣跨敤鍦烘櫙锛?
    /// - 澶у閲忓瓨鍌紙> 1000 涓粍浠讹級
    /// - 椤哄簭閬嶅巻锛堥殢鏈鸿闂棤鏁堬級
    /// - 鍐呭瓨甯﹀鍏呰冻锛堥潪澶氱嚎绋嬬珵浜夛級
    /// </summary>
    public unsafe struct ComponentBlockIterator<T> where T : unmanaged
    {
        private readonly Storage<T>* _storage;
        private readonly int _version;
        private readonly int _blockCapacity;

        private int _currentBlock;
        private int _currentOffset;
        private int _remaining;
        private int _startGlobalIndex;

        /// <summary>
        /// 鍒涘缓瀹屾暣杩唬鍣?
        /// </summary>
        internal ComponentBlockIterator(Storage<T>* storage)
        {
            _storage = storage;
            _version = storage->Version;
            _blockCapacity = storage->BlockItemCapacity;
            _currentBlock = 0;
            _currentOffset = 1; // 璺宠繃绱㈠紩0
            _remaining = storage->Count;
            _startGlobalIndex = 1;
        }

        /// <summary>
        /// 鍒涘缓鑼冨洿杩唬鍣?
        /// </summary>
        internal ComponentBlockIterator(Storage<T>* storage, int offset, int count)
        {
            _storage = storage;
            _version = storage->Version;
            _blockCapacity = storage->BlockItemCapacity;
            _startGlobalIndex = offset + 1;

            // 璁＄畻璧峰浣嶇疆
            int clampedOffset = System.Math.Min(_startGlobalIndex, storage->Count);
            int clampedCount = System.Math.Max(0, System.Math.Min(count, storage->Count - clampedOffset));

            _currentBlock = clampedOffset / _blockCapacity;
            _currentOffset = clampedOffset % _blockCapacity;
            _remaining = clampedCount;
        }

        /// <summary>
        /// 閲嶇疆杩唬鍣?
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
            ValidateVersion();
            _currentBlock = 0;
            _currentOffset = 1;
            _remaining = _storage->Count;
        }

        /// <summary>
        /// 鑾峰彇涓嬩竴涓?Block 鐨勬暟鎹?
        /// </summary>
        /// <param name="entities">瀹炰綋寮曠敤鏁扮粍鎸囬拡</param>
        /// <param name="components">缁勪欢鏁版嵁鏁扮粍鎸囬拡</param>
        /// <param name="count">姝?Block 涓殑鏈夋晥椤规暟</param>
        /// <returns>鏄惁杩樻湁鏇村鏁版嵁</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool NextBlock(out EntityRef* entities, out T* components, out int count)
        {
            ValidateVersion();

            while (_currentBlock < _storage->BlockCount && _remaining > 0)
            {
                int itemsInBlock = _blockCapacity - _currentOffset;
                if (itemsInBlock > 0)
                {
                    count = System.Math.Min(_remaining, itemsInBlock);
                    entities = _storage->GetBlockEntityRefs(_currentBlock) + _currentOffset;
                    components = _storage->GetBlockData(_currentBlock) + _currentOffset;

                    _remaining -= count;
                    _currentOffset += count;
                    return true;
                }

                _currentBlock++;
                _currentOffset = 0;
            }

            entities = default;
            components = default;
            count = 0;
            return false;
        }

        /// <summary>
        /// 绉诲姩鍒颁笅涓€涓疄浣擄紙閫愪釜杩唬锛?
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Next(out EntityRef entity, out T* component)
        {
            ValidateVersion();

            if (_remaining > 0)
            {
                // 纭繚褰撳墠 offset 鍦ㄦ湁鏁堣寖鍥村唴
                if (_currentOffset >= _blockCapacity)
                {
                    _currentBlock++;
                    _currentOffset = 0;
                }

                while (_currentBlock < _storage->BlockCount)
                {
                    int blockItems = _storage->GetBlockItemCount(_currentBlock);
                    if (_currentOffset < blockItems)
                    {
                        entity = _storage->GetBlockEntityRefs(_currentBlock)[_currentOffset];
                        component = &_storage->GetBlockData(_currentBlock)[_currentOffset];
                        _currentOffset++;
                        _remaining--;
                        return true;
                    }

                    _currentBlock++;
                    _currentOffset = 0;
                }
            }

            entity = EntityRef.None;
            component = null;
            return false;
        }

        /// <summary>
        /// 楠岃瘉瀛樺偍鏈淇敼锛堥槻姝㈣凯浠ｄ腑澧炲垹缁勪欢锛?
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ValidateVersion()
        {
#if DEBUG
            if (_storage->Version != _version)
            {
                throw new InvalidOperationException(
                    $"Cannot modify Storage<{typeof(T).Name}> while iterating over it. " +
                    "Use a command buffer or defer modifications.");
            }
#endif
        }
    }

    /// <summary>
    /// 澧炲己鐗堝潡杩唬鍣?- 甯﹂鍙栦紭鍖?
    /// </summary>
    public unsafe struct PrefetchedBlockIterator<T> where T : unmanaged
    {
        private readonly Storage<T>* _storage;
        private readonly int _version;
        private readonly int _blockCapacity;
        private readonly int _prefetchDistance;

        private int _currentBlock;
        private int _currentOffset;
        private int _remaining;

        public PrefetchedBlockIterator(Storage<T>* storage, int prefetchDistance = 2)
        {
            _storage = storage;
            _version = storage->Version;
            _blockCapacity = storage->BlockItemCapacity;
            _prefetchDistance = prefetchDistance;
            _currentBlock = 0;
            _currentOffset = 1;
            _remaining = storage->Count;

            // 棰勫彇鍓嶅嚑涓潡
            PrefetchUpcoming();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool NextBlock(out EntityRef* entities, out T* components, out int count)
        {
            ValidateVersion();

            while (_currentBlock < _storage->BlockCount && _remaining > 0)
            {
                int itemsInBlock = _blockCapacity - _currentOffset;
                if (itemsInBlock > 0)
                {
                    count = System.Math.Min(_remaining, itemsInBlock);
                    entities = _storage->GetBlockEntityRefs(_currentBlock) + _currentOffset;
                    components = _storage->GetBlockData(_currentBlock) + _currentOffset;

                    _remaining -= count;
                    _currentOffset += count;

                    // 棰勫彇鍚庣画鍧?
                    PrefetchUpcoming();

                    return true;
                }

                _currentBlock++;
                _currentOffset = 0;
            }

            entities = default;
            components = default;
            count = 0;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void PrefetchUpcoming()
        {
            for (int i = 1; i <= _prefetchDistance; i++)
            {
                int prefetchBlock = _currentBlock + i;
                if (prefetchBlock >= _storage->BlockCount) break;

                // 棰勫彇瀹炰綋寮曠敤鍜屾暟鎹?
                SIMDUtils.PrefetchL2(_storage->GetBlockEntityRefs(prefetchBlock));
                SIMDUtils.PrefetchL2(_storage->GetBlockData(prefetchBlock));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ValidateVersion()
        {
#if DEBUG
            if (_storage->Version != _version)
            {
                throw new InvalidOperationException(
                    $"Cannot modify Storage<{typeof(T).Name}> while iterating over it.");
            }
#endif
        }
    }
}
