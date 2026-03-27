// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;
using System.Runtime.CompilerServices;

namespace Lattice.ECS.Core
{
    /// <summary>
    /// 位流 - FrameSync 风格的高效二进制序列化
    /// 
    /// 特性：
    /// 1. 位级别的读写（Bit-packing）
    /// 2. 支持有符号/无符号整数变长编码
    /// 3. 支持原始内存拷贝
    /// 4. 可扩展的缓冲区
    /// </summary>
    public unsafe class BitStream
    {
        #region 字段

        /// <summary>数据缓冲区</summary>
        private byte[] _buffer;

        /// <summary>当前逻辑终点（读取模式下可能小于缓冲区长度）。</summary>
        private int _byteLimit;

        /// <summary>当前字节位置</summary>
        private int _bytePosition;

        /// <summary>当前位位置（0-7）</summary>
        private int _bitPosition;

        /// <summary>是否正在写入</summary>
        public bool IsWriting { get; set; }

        /// <summary>是否正在读取</summary>
        public bool IsReading => !IsWriting;

        /// <summary>当前字节位置</summary>
        public int BytePosition => _bytePosition;

        /// <summary>缓冲区长度</summary>
        public int Length => _buffer.Length;

        /// <summary>已使用的字节数</summary>
        public int BytesUsed => _bytePosition + (_bitPosition > 0 ? 1 : 0);

        #endregion

        #region 构造函数

        /// <summary>
        /// 创建位流（写入模式）
        /// </summary>
        public BitStream(int initialCapacity = 1024)
        {
            _buffer = new byte[initialCapacity];
            _byteLimit = _buffer.Length;
            _bytePosition = 0;
            _bitPosition = 0;
            IsWriting = true;
        }

        /// <summary>
        /// 创建位流（从现有数据读取）
        /// </summary>
        public BitStream(byte[] data, int offset = 0, int length = -1)
        {
            _buffer = data;
            _byteLimit = length >= 0 ? System.Math.Min(data.Length, offset + length) : data.Length;
            _bytePosition = offset;
            _bitPosition = 0;
            IsWriting = false;
        }

        #endregion

        #region 基础位操作

        /// <summary>
        /// 写入一个位
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteBit(bool value)
        {
            if (IsReading)
                throw new InvalidOperationException("Cannot write in reading mode");

            EnsureCapacity(_bytePosition + 1);

            if (value)
            {
                _buffer[_bytePosition] |= (byte)(1 << _bitPosition);
            }

            _bitPosition++;
            if (_bitPosition >= 8)
            {
                _bitPosition = 0;
                _bytePosition++;
            }
        }

        /// <summary>
        /// 读取一个位
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ReadBit()
        {
            if (IsWriting)
                throw new InvalidOperationException("Cannot read in writing mode");

            if (_bytePosition >= _byteLimit)
                throw new EndOfStreamException($"Attempted to read beyond buffer length ({_byteLimit} bytes at position {_bytePosition})");

            bool value = (_buffer[_bytePosition] & (1 << _bitPosition)) != 0;

            _bitPosition++;
            if (_bitPosition >= 8)
            {
                _bitPosition = 0;
                _bytePosition++;
            }

            return value;
        }

        /// <summary>
        /// 写入指定数量的位
        /// </summary>
        public void WriteBits(uint value, int bits)
        {
            if (bits < 0 || bits > 32)
                throw new ArgumentOutOfRangeException(nameof(bits));

            for (int i = 0; i < bits; i++)
            {
                WriteBit(((value >> i) & 1) != 0);
            }
        }

        /// <summary>
        /// 读取指定数量的位
        /// </summary>
        public uint ReadBits(int bits)
        {
            if (bits < 0 || bits > 32)
                throw new ArgumentOutOfRangeException(nameof(bits));

            uint value = 0;
            for (int i = 0; i < bits; i++)
            {
                if (ReadBit())
                    value |= (1u << i);
            }
            return value;
        }

        #endregion

        #region 整数序列化

        /// <summary>
        /// 写入 int（32位）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteInt(int value)
        {
            WriteBits((uint)value, 32);
        }

        /// <summary>
        /// 读取 int（32位）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadInt()
        {
            return (int)ReadBits(32);
        }

        /// <summary>
        /// 写入 uint（32位）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUInt(uint value)
        {
            WriteBits(value, 32);
        }

        /// <summary>
        /// 读取 uint（32位）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint ReadUInt()
        {
            return ReadBits(32);
        }

        /// <summary>
        /// 写入 short（16位）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteShort(short value)
        {
            WriteBits((uint)(ushort)value, 16);
        }

        /// <summary>
        /// 读取 short（16位）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public short ReadShort()
        {
            return (short)ReadBits(16);
        }

        /// <summary>
        /// 写入 ushort（16位）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUShort(ushort value)
        {
            WriteBits(value, 16);
        }

        /// <summary>
        /// 读取 ushort（16位）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ushort ReadUShort()
        {
            return (ushort)ReadBits(16);
        }

        /// <summary>
        /// 写入 byte（8位）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteByte(byte value)
        {
            WriteBits(value, 8);
        }

        /// <summary>
        /// 读取 byte（8位）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte ReadByte()
        {
            return (byte)ReadBits(8);
        }

        /// <summary>
        /// 写入 bool（1位）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteBool(bool value)
        {
            WriteBit(value);
        }

        /// <summary>
        /// 读取 bool（1位）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ReadBool()
        {
            return ReadBit();
        }

        /// <summary>
        /// 写入 long（64位）
        /// </summary>
        public void WriteLong(long value)
        {
            WriteBits((uint)value, 32);
            WriteBits((uint)(value >> 32), 32);
        }

        /// <summary>
        /// 读取 long（64位）
        /// </summary>
        public long ReadLong()
        {
            long low = ReadBits(32);
            long high = ReadBits(32);
            return low | (high << 32);
        }

        /// <summary>
        /// 写入 ulong（64位）
        /// </summary>
        public void WriteULong(ulong value)
        {
            WriteBits((uint)value, 32);
            WriteBits((uint)(value >> 32), 32);
        }

        /// <summary>
        /// 读取 ulong（64位）
        /// </summary>
        public ulong ReadULong()
        {
            ulong low = ReadBits(32);
            ulong high = ReadBits(32);
            return low | (high << 32);
        }

        #endregion

        #region 变长整数（VarInt）

        /// <summary>
        /// 写入变长 uint（小值更省空间）
        /// </summary>
        public void WriteVarUInt(uint value)
        {
            // 每个字节使用 7 位数据 + 1 位继续标志
            while (value >= 0x80)
            {
                WriteByte((byte)(value | 0x80));
                value >>= 7;
            }
            WriteByte((byte)value);
        }

        /// <summary>
        /// 读取变长 uint
        /// </summary>
        public uint ReadVarUInt()
        {
            uint value = 0;
            int shift = 0;
            byte b;

            do
            {
                b = ReadByte();
                value |= (uint)(b & 0x7F) << shift;
                shift += 7;
            } while ((b & 0x80) != 0);

            return value;
        }

        /// <summary>
        /// 写入变长 int（ZigZag 编码）
        /// </summary>
        public void WriteVarInt(int value)
        {
            // ZigZag 编码：将符号位移到最低位
            uint encoded = (uint)((value << 1) ^ (value >> 31));
            WriteVarUInt(encoded);
        }

        /// <summary>
        /// 读取变长 int
        /// </summary>
        public int ReadVarInt()
        {
            uint encoded = ReadVarUInt();
            return (int)((encoded >> 1) ^ -(encoded & 1));
        }

        #endregion

        #region 原始内存操作

        /// <summary>
        /// 写入原始字节数组
        /// </summary>
        public void WriteBytes(byte[] data, int offset, int length)
        {
            // 对齐到字节边界
            if (_bitPosition != 0)
            {
                _bitPosition = 0;
                _bytePosition++;
            }

            EnsureCapacity(_bytePosition + length);

            Buffer.BlockCopy(data, offset, _buffer, _bytePosition, length);
            _bytePosition += length;
        }

        /// <summary>
        /// 读取原始字节数组
        /// </summary>
        public void ReadBytes(byte[] destination, int offset, int length)
        {
            // 对齐到字节边界
            if (_bitPosition != 0)
            {
                _bitPosition = 0;
                _bytePosition++;
            }

            if (_bytePosition + length > _byteLimit)
                throw new EndOfStreamException();

            Buffer.BlockCopy(_buffer, _bytePosition, destination, offset, length);
            _bytePosition += length;
        }

        /// <summary>
        /// 写入非托管内存
        /// </summary>
        public void WriteMemory(void* source, int length)
        {
            // 对齐到字节边界
            if (_bitPosition != 0)
            {
                _bitPosition = 0;
                _bytePosition++;
            }

            EnsureCapacity(_bytePosition + length);

            fixed (byte* dest = &_buffer[_bytePosition])
            {
                Buffer.MemoryCopy(source, dest, length, length);
            }

            _bytePosition += length;
        }

        /// <summary>
        /// 读取到非托管内存
        /// </summary>
        public void ReadMemory(void* destination, int length)
        {
            // 对齐到字节边界
            if (_bitPosition != 0)
            {
                _bitPosition = 0;
                _bytePosition++;
            }

            if (_bytePosition + length > _byteLimit)
                throw new EndOfStreamException();

            fixed (byte* src = &_buffer[_bytePosition])
            {
                Buffer.MemoryCopy(src, destination, length, length);
            }

            _bytePosition += length;
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 获取写入的数据
        /// </summary>
        public byte[] ToArray()
        {
            int length = BytesUsed;
            byte[] result = new byte[length];
            Buffer.BlockCopy(_buffer, 0, result, 0, length);
            return result;
        }

        /// <summary>
        /// 获取当前已写入数据的只读视图，不发生额外分配。
        /// </summary>
        public ReadOnlySpan<byte> GetWrittenSpan()
        {
            return new ReadOnlySpan<byte>(_buffer, 0, BytesUsed);
        }

        /// <summary>
        /// 重置流（用于复用）
        /// </summary>
        public void Reset()
        {
            _bytePosition = 0;
            _bitPosition = 0;
            if (IsWriting)
            {
                _byteLimit = _buffer.Length;
            }
        }

        /// <summary>
        /// 确保容量
        /// </summary>
        private void EnsureCapacity(int required)
        {
            if (required <= _buffer.Length)
                return;

            int newCapacity = System.Math.Max(required, _buffer.Length * 2);
            byte[] newBuffer = new byte[newCapacity];
            Buffer.BlockCopy(_buffer, 0, newBuffer, 0, _buffer.Length);
            _buffer = newBuffer;
            _byteLimit = _buffer.Length;
        }

        #endregion

        #region 缓冲区安全检查

        /// <summary>
        /// 检查剩余写入空间是否足够
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CanWrite(int bytes) => _bytePosition + bytes <= _buffer.Length;

        /// <summary>
        /// 检查剩余读取空间是否足够
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CanRead(int bytes) => _bytePosition + bytes <= _byteLimit;

        /// <summary>
        /// 验证可以读取指定数量的字节
        /// </summary>
        public void VerifyCanRead(int bytes, string operation = "read")
        {
            if (!CanRead(bytes))
                throw new EndOfStreamException(
                    $"Insufficient data to {operation}. " +
                    $"Required: {bytes} bytes, Available: {_byteLimit - _bytePosition} bytes " +
                    $"(at position {_bytePosition})");
        }

        /// <summary>
        /// 获取剩余可读字节数
        /// </summary>
        public int RemainingBytes => System.Math.Max(0, _byteLimit - _bytePosition);

        /// <summary>
        /// 获取当前位置信息（用于调试）
        /// </summary>
        public string PositionInfo =>
            $"Byte: {_bytePosition}, Bit: {_bitPosition}, Length: {_byteLimit}";

        #endregion
    }
}
