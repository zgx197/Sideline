using System;
using System.Collections.Generic;

namespace Lattice.ECS.Session
{
    /// <summary>
    /// SessionRuntime 的输入保留与聚合存储。
    /// 负责 `(playerId, tick)` 输入写入、固定窗口保留以及 `SessionInputSet` 聚合。
    /// </summary>
    internal sealed class SessionInputStore
    {
        private readonly Dictionary<int, InputBuffer> _inputBuffers = new();
        private readonly List<SessionPlayerInput> _inputSetScratch = new();
        private readonly int _retentionCapacity;

        public SessionInputStore(int retentionCapacity)
        {
            _retentionCapacity = System.Math.Max(1, retentionCapacity);
        }

        public void SetPlayerInput(int playerId, int tick, IPlayerInput input)
        {
            if (!_inputBuffers.TryGetValue(playerId, out InputBuffer? buffer))
            {
                buffer = new InputBuffer(_retentionCapacity);
                _inputBuffers[playerId] = buffer;
            }

            buffer.SetInput(tick, input);
        }

        public IPlayerInput? GetPlayerInput(int playerId, int tick)
        {
            if (!_inputBuffers.TryGetValue(playerId, out InputBuffer? buffer))
            {
                return null;
            }

            return buffer.GetInput(tick);
        }

        public SessionInputSet CollectInputSet(int tick)
        {
            _inputSetScratch.Clear();

            foreach (KeyValuePair<int, InputBuffer> pair in _inputBuffers)
            {
                IPlayerInput? input = pair.Value.GetInput(tick);
                if (input == null)
                {
                    continue;
                }

                _inputSetScratch.Add(new SessionPlayerInput(input));
            }

            _inputSetScratch.Sort(static (left, right) => left.PlayerId.CompareTo(right.PlayerId));
            return new SessionInputSet(tick, _inputSetScratch);
        }

        public void Clear()
        {
            _inputBuffers.Clear();
            _inputSetScratch.Clear();
        }
    }

    /// <summary>
    /// 按固定窗口保留玩家输入的 ring buffer。
    /// </summary>
    internal sealed class InputBuffer
    {
        private readonly IPlayerInput?[] _inputs;
        private readonly int[] _ticks;
        private readonly int _capacity;
        private int _latestTick = int.MinValue;

        public InputBuffer(int capacity)
        {
            _capacity = System.Math.Max(1, capacity);
            _inputs = new IPlayerInput[_capacity];
            _ticks = new int[_capacity];

            for (int i = 0; i < _ticks.Length; i++)
            {
                _ticks[i] = int.MinValue;
            }
        }

        public void SetInput(int tick, IPlayerInput input)
        {
            if (tick > _latestTick)
            {
                _latestTick = tick;
            }

            if (tick < GetMinTick())
            {
                return;
            }

            int index = ToIndex(tick);
            _inputs[index] = input;
            _ticks[index] = tick;
        }

        public IPlayerInput? GetInput(int tick)
        {
            if (_latestTick == int.MinValue || tick < GetMinTick())
            {
                return null;
            }

            int index = ToIndex(tick);
            return _ticks[index] == tick ? _inputs[index] : null;
        }

        private int GetMinTick()
        {
            if (_latestTick == int.MinValue)
            {
                return int.MaxValue;
            }

            return _latestTick - _capacity + 1;
        }

        private int ToIndex(int tick)
        {
            int index = tick % _capacity;
            return index < 0 ? index + _capacity : index;
        }
    }
}
