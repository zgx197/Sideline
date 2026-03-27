using System;
using System.Collections.Generic;
using Lattice.ECS.Core;

namespace Lattice.ECS.Session
{
    /// <summary>
    /// SessionRuntime 的历史帧、采样快照、物化缓存与帧池管理。
    /// 负责保留策略与缓存复用，不直接决定运行时公开语义。
    /// </summary>
    internal sealed class SessionFrameHistory
    {
        private readonly Dictionary<int, Frame> _historyByTick;
        private readonly Queue<int> _historyOrder;
        private readonly Dictionary<int, PackedFrameSnapshot> _historySnapshotsByTick;
        private readonly List<int> _historySnapshotTicks;
        private readonly Stack<Frame> _recycledFrames;
        private readonly Dictionary<int, Frame> _materializedHistoryByTick;
        private readonly List<int> _materializedHistoryOrder;
        private Frame? _historicalScratchFrame;
        private int _highestHistoryTick = -1;

        public SessionFrameHistory()
        {
            _historyByTick = new Dictionary<int, Frame>(SessionRuntimeDataDefaults.HistorySize);
            _historyOrder = new Queue<int>(SessionRuntimeDataDefaults.HistorySize);
            _historySnapshotsByTick = new Dictionary<int, PackedFrameSnapshot>(
                SessionRuntimeDataDefaults.HistorySize / SessionRuntimeDataDefaults.HistorySnapshotInterval + 2);
            _historySnapshotTicks = new List<int>(
                SessionRuntimeDataDefaults.HistorySize / SessionRuntimeDataDefaults.HistorySnapshotInterval + 2);
            _recycledFrames = new Stack<Frame>(SessionRuntimeDataDefaults.RecycledFrameLimit);
            _materializedHistoryByTick = new Dictionary<int, Frame>(SessionRuntimeDataDefaults.MaterializedHistoryCacheSize);
            _materializedHistoryOrder = new List<int>(SessionRuntimeDataDefaults.MaterializedHistoryCacheSize);
        }

        public int HighestHistoryTick => _highestHistoryTick;

        public bool TryGetLiveFrame(int tick, out Frame? frame)
        {
            return _historyByTick.TryGetValue(tick, out frame);
        }

        public bool TryGetMaterializedFrame(int tick, out Frame? frame)
        {
            if (_materializedHistoryByTick.TryGetValue(tick, out frame))
            {
                TouchMaterializedHistoryTick(tick);
                return true;
            }

            return false;
        }

        public bool TryGetScratchFrame(int tick, out Frame? frame)
        {
            if (_historicalScratchFrame != null && _historicalScratchFrame.Tick == tick)
            {
                frame = _historicalScratchFrame;
                return true;
            }

            frame = null;
            return false;
        }

        public bool IsTickWithinRetentionWindow(int tick)
        {
            if (tick < 0 || _highestHistoryTick < 0)
            {
                return false;
            }

            if (tick > _highestHistoryTick)
            {
                return false;
            }

            int minTick = _highestHistoryTick - SessionRuntimeDataDefaults.HistorySize + 1;
            return tick >= minTick;
        }

        public bool TryFindClosestLiveFrame(int tick, out int baseTick, out Frame? frame)
        {
            baseTick = -1;
            frame = null;

            foreach ((int historicalTick, Frame historicalFrame) in _historyByTick)
            {
                if (historicalTick <= tick && historicalTick > baseTick)
                {
                    baseTick = historicalTick;
                    frame = historicalFrame;
                }
            }

            return frame != null;
        }

        public bool TryFindClosestMaterializedFrame(int tick, out int baseTick, out Frame? frame)
        {
            baseTick = -1;
            frame = null;

            for (int i = 0; i < _materializedHistoryOrder.Count; i++)
            {
                int candidateTick = _materializedHistoryOrder[i];
                if (candidateTick <= tick && candidateTick > baseTick)
                {
                    baseTick = candidateTick;
                }
            }

            if (baseTick < 0 || !_materializedHistoryByTick.TryGetValue(baseTick, out frame))
            {
                baseTick = -1;
                frame = null;
                return false;
            }

            TouchMaterializedHistoryTick(baseTick);
            return true;
        }

        public bool TryGetScratchAsBaseFrame(int tick, out int baseTick, out Frame? frame)
        {
            if (_historicalScratchFrame != null && _historicalScratchFrame.Tick <= tick)
            {
                baseTick = _historicalScratchFrame.Tick;
                frame = _historicalScratchFrame;
                return true;
            }

            baseTick = -1;
            frame = null;
            return false;
        }

        public bool TryFindClosestSnapshotTick(int tick, out int snapshotTick)
        {
            snapshotTick = -1;
            if (_historySnapshotTicks.Count == 0)
            {
                return false;
            }

            int index = _historySnapshotTicks.BinarySearch(tick);
            if (index < 0)
            {
                index = ~index - 1;
            }

            if ((uint)index >= (uint)_historySnapshotTicks.Count)
            {
                return false;
            }

            snapshotTick = _historySnapshotTicks[index];
            return snapshotTick >= 0;
        }

        public PackedFrameSnapshot GetSnapshot(int tick)
        {
            return _historySnapshotsByTick[tick];
        }

        public void UpdateHistory(Frame frame, Action<Frame?> releaseDetachedFrame)
        {
            ArgumentNullException.ThrowIfNull(frame);
            ArgumentNullException.ThrowIfNull(releaseDetachedFrame);

            InvalidateMaterializedCache(releaseDetachedFrame);
            RemoveHistorySnapshot(frame.Tick);

            if (_historyByTick.TryGetValue(frame.Tick, out Frame? existing))
            {
                if (!ReferenceEquals(existing, frame))
                {
                    _historyByTick[frame.Tick] = frame;
                    releaseDetachedFrame(existing);
                }

                return;
            }

            AddHistoryFrame(frame, releaseDetachedFrame);
        }

        public void InvalidateMaterializedCache(Action<Frame?> releaseDetachedFrame)
        {
            ArgumentNullException.ThrowIfNull(releaseDetachedFrame);

            Frame? scratchFrame = _historicalScratchFrame;
            _historicalScratchFrame = null;

            if (_materializedHistoryByTick.Count > 0)
            {
                for (int i = 0; i < _materializedHistoryOrder.Count; i++)
                {
                    int tick = _materializedHistoryOrder[i];
                    if (_materializedHistoryByTick.Remove(tick, out Frame? cachedFrame) &&
                        !ReferenceEquals(cachedFrame, scratchFrame))
                    {
                        releaseDetachedFrame(cachedFrame);
                    }
                }

                _materializedHistoryOrder.Clear();
            }

            releaseDetachedFrame(scratchFrame);
        }

        public void StoreMaterializedFrame(Frame frame, Action<Frame?> releaseDetachedFrame)
        {
            ArgumentNullException.ThrowIfNull(frame);
            ArgumentNullException.ThrowIfNull(releaseDetachedFrame);

            int tick = frame.Tick;

            if (_materializedHistoryByTick.TryGetValue(tick, out Frame? existing))
            {
                if (!ReferenceEquals(existing, frame))
                {
                    _materializedHistoryByTick[tick] = frame;
                    releaseDetachedFrame(existing);
                }

                TouchMaterializedHistoryTick(tick);
                return;
            }

            _materializedHistoryByTick[tick] = frame;
            _materializedHistoryOrder.Add(tick);

            if (_materializedHistoryOrder.Count > SessionRuntimeDataDefaults.MaterializedHistoryCacheSize)
            {
                int evictedTick = _materializedHistoryOrder[0];
                _materializedHistoryOrder.RemoveAt(0);
                if (_materializedHistoryByTick.Remove(evictedTick, out Frame? evictedFrame))
                {
                    releaseDetachedFrame(evictedFrame);
                }
            }
        }

        public void SetScratchFrame(Frame frame, Action<Frame?> releaseDetachedFrame)
        {
            ArgumentNullException.ThrowIfNull(frame);
            ArgumentNullException.ThrowIfNull(releaseDetachedFrame);

            if (!ReferenceEquals(_historicalScratchFrame, frame))
            {
                releaseDetachedFrame(_historicalScratchFrame);
                _historicalScratchFrame = frame;
            }
        }

        public Frame RentFrameBuffer(int requiredEntityCapacity)
        {
            while (_recycledFrames.Count > 0)
            {
                Frame recycled = _recycledFrames.Pop();
                if (recycled.EntityCapacity == requiredEntityCapacity)
                {
                    return recycled;
                }

                recycled.Dispose();
            }

            return new Frame(requiredEntityCapacity);
        }

        public void RecycleFrame(Frame? frame)
        {
            if (frame == null)
            {
                return;
            }

            if (_recycledFrames.Count < SessionRuntimeDataDefaults.RecycledFrameLimit)
            {
                _recycledFrames.Push(frame);
                return;
            }

            frame.Dispose();
        }

        public bool Owns(Frame frame)
        {
            if (ReferenceEquals(frame, _historicalScratchFrame))
            {
                return true;
            }

            if (_materializedHistoryByTick.TryGetValue(frame.Tick, out Frame? materializedFrame) &&
                ReferenceEquals(frame, materializedFrame))
            {
                return true;
            }

            if (_historyByTick.TryGetValue(frame.Tick, out Frame? historicalFrame) &&
                ReferenceEquals(frame, historicalFrame))
            {
                return true;
            }

            return false;
        }

        public void CopyOwnedFramesTo(ISet<Frame> frames)
        {
            ArgumentNullException.ThrowIfNull(frames);

            if (_historicalScratchFrame != null)
            {
                frames.Add(_historicalScratchFrame);
            }

            foreach (Frame materializedFrame in _materializedHistoryByTick.Values)
            {
                frames.Add(materializedFrame);
            }

            foreach (Frame historicalFrame in _historyByTick.Values)
            {
                frames.Add(historicalFrame);
            }

            foreach (Frame recycledFrame in _recycledFrames)
            {
                frames.Add(recycledFrame);
            }
        }

        public void Clear()
        {
            _historicalScratchFrame = null;
            _highestHistoryTick = -1;
            _historyByTick.Clear();
            _historyOrder.Clear();
            _historySnapshotsByTick.Clear();
            _historySnapshotTicks.Clear();
            _materializedHistoryByTick.Clear();
            _materializedHistoryOrder.Clear();
            _recycledFrames.Clear();
        }

        private void AddHistoryFrame(Frame frame, Action<Frame?> releaseDetachedFrame)
        {
            _historyByTick.Add(frame.Tick, frame);
            _historyOrder.Enqueue(frame.Tick);
            _highestHistoryTick = System.Math.Max(_highestHistoryTick, frame.Tick);

            TrimExpiredHistory(releaseDetachedFrame);
            DemoteOldLiveHistory(releaseDetachedFrame);
        }

        private void TrimExpiredHistory(Action<Frame?> releaseDetachedFrame)
        {
            int minTick = _highestHistoryTick - SessionRuntimeDataDefaults.HistorySize + 1;

            while (_historyOrder.Count > 0 && _historyOrder.Peek() < minTick)
            {
                int expiredTick = _historyOrder.Dequeue();
                if (_historyByTick.Remove(expiredTick, out Frame? expiredFrame))
                {
                    RemoveHistorySnapshot(expiredTick);
                    releaseDetachedFrame(expiredFrame);
                }
            }

            TrimExpiredSnapshots(minTick);
        }

        private void DemoteOldLiveHistory(Action<Frame?> releaseDetachedFrame)
        {
            while (_historyByTick.Count > SessionRuntimeDataDefaults.LiveHistorySize && _historyOrder.Count > 0)
            {
                int demotedTick = _historyOrder.Dequeue();
                if (!_historyByTick.Remove(demotedTick, out Frame? demotedFrame))
                {
                    continue;
                }

                CaptureHistorySnapshotIfNeeded(demotedTick, demotedFrame);
                releaseDetachedFrame(demotedFrame);
            }
        }

        private void CaptureHistorySnapshotIfNeeded(int tick, Frame frame)
        {
            if (tick != 0 && (tick % SessionRuntimeDataDefaults.HistorySnapshotInterval) != 0)
            {
                return;
            }

            bool isNewSnapshot = !_historySnapshotsByTick.ContainsKey(tick);
            _historySnapshotsByTick[tick] = frame.CapturePackedSnapshot(ComponentSerializationMode.Prediction);
            if (isNewSnapshot)
            {
                InsertHistorySnapshotTick(tick);
            }
        }

        private void RemoveHistorySnapshot(int tick)
        {
            if (_historySnapshotsByTick.Remove(tick))
            {
                RemoveHistorySnapshotTick(tick);
            }
        }

        private void TrimExpiredSnapshots(int minTick)
        {
            if (_historySnapshotsByTick.Count == 0)
            {
                _historySnapshotTicks.Clear();
                return;
            }

            int firstWithinRangeIndex = _historySnapshotTicks.BinarySearch(minTick);
            if (firstWithinRangeIndex < 0)
            {
                firstWithinRangeIndex = ~firstWithinRangeIndex;
            }

            int anchorIndex = firstWithinRangeIndex - 1;
            for (int i = anchorIndex - 1; i >= 0; i--)
            {
                _historySnapshotsByTick.Remove(_historySnapshotTicks[i]);
                _historySnapshotTicks.RemoveAt(i);
            }
        }

        private void TouchMaterializedHistoryTick(int tick)
        {
            int index = _materializedHistoryOrder.IndexOf(tick);
            if (index < 0 || index == _materializedHistoryOrder.Count - 1)
            {
                return;
            }

            _materializedHistoryOrder.RemoveAt(index);
            _materializedHistoryOrder.Add(tick);
        }

        private void InsertHistorySnapshotTick(int tick)
        {
            int index = _historySnapshotTicks.BinarySearch(tick);
            if (index >= 0)
            {
                return;
            }

            _historySnapshotTicks.Insert(~index, tick);
        }

        private void RemoveHistorySnapshotTick(int tick)
        {
            int index = _historySnapshotTicks.BinarySearch(tick);
            if (index >= 0)
            {
                _historySnapshotTicks.RemoveAt(index);
            }
        }
    }
}
