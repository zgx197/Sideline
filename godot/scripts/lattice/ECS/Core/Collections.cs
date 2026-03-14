using System;
using System.Collections.Generic;

namespace Lattice.ECS.Core
{
    /// <summary>
    /// 环形缓冲区 - ECS 内部使用
    /// </summary>
    public sealed class RingBuffer<T>
    {
        private readonly T[] _buffer;
        private int _head;
        private int _count;

        public RingBuffer(int capacity)
        {
            _buffer = new T[capacity];
            _head = 0;
            _count = 0;
        }

        public int Count => _count;
        public int Capacity => _buffer.Length;

        public void PushBack(T item)
        {
            _buffer[_head] = item;
            _head = (_head + 1) % _buffer.Length;
            if (_count < _buffer.Length)
                _count++;
        }

        public T? PopFront()
        {
            if (_count == 0) return default;

            int tail = (_head - _count + _buffer.Length) % _buffer.Length;
            var item = _buffer[tail];
            _count--;
            return item;
        }

        public void Clear()
        {
            _head = 0;
            _count = 0;
        }

        /// <summary>
        /// 获取指定索引的元素
        /// </summary>
        public T? GetAt(int index)
        {
            if (index < 0 || index >= _count)
                return default;

            int realIndex = (_head - _count + index + _buffer.Length) % _buffer.Length;
            return _buffer[realIndex];
        }

        /// <summary>
        /// 设置指定索引的元素
        /// </summary>
        public void SetAt(int index, T value)
        {
            if (index < 0 || index >= _count)
                throw new ArgumentOutOfRangeException(nameof(index));

            int realIndex = (_head - _count + index + _buffer.Length) % _buffer.Length;
            _buffer[realIndex] = value;
        }

        public IEnumerable<T> GetAll()
        {
            for (int i = 0; i < _count; i++)
            {
                int index = (_head - _count + i + _buffer.Length) % _buffer.Length;
                yield return _buffer[index]!;
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            return GetAll().GetEnumerator();
        }
    }
}
