// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;
using System.Buffers.Binary;
using Lattice.ECS.Serialization;

namespace Lattice.ECS.Core
{
    /// <summary>
    /// 帧状态写入器基类。
    /// </summary>
    internal abstract unsafe class FrameStateWriter
    {
        public abstract void WriteByte(byte value);

        public abstract void WriteBytes(void* data, int length);

        public abstract void WriteBytes(ReadOnlySpan<byte> data);

        public abstract int BytesWritten { get; }

        public void WriteInt32(int value)
        {
            Span<byte> buffer = stackalloc byte[sizeof(int)];
            BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
            WriteBytes(buffer);
        }

        public void WriteInt64(long value)
        {
            Span<byte> buffer = stackalloc byte[sizeof(long)];
            BinaryPrimitives.WriteInt64LittleEndian(buffer, value);
            WriteBytes(buffer);
        }

        public void WriteUInt16(ushort value)
        {
            Span<byte> buffer = stackalloc byte[sizeof(ushort)];
            BinaryPrimitives.WriteUInt16LittleEndian(buffer, value);
            WriteBytes(buffer);
        }

        public void WriteUInt64(ulong value)
        {
            Span<byte> buffer = stackalloc byte[sizeof(ulong)];
            BinaryPrimitives.WriteUInt64LittleEndian(buffer, value);
            WriteBytes(buffer);
        }
    }

    /// <summary>
    /// 直接写入连续字节缓冲区的状态写入器。
    /// </summary>
    internal sealed unsafe class FrameStateBufferWriter : FrameStateWriter
    {
        private byte[] _buffer;
        private int _position;

        public FrameStateBufferWriter(int initialCapacity = 1024)
        {
            _buffer = new byte[initialCapacity];
        }

        public int Length => _position;

        public override int BytesWritten => _position;

        public override void WriteByte(byte value)
        {
            EnsureCapacity(sizeof(byte));
            _buffer[_position++] = value;
        }

        public override void WriteBytes(void* data, int length)
        {
            if (length <= 0)
            {
                return;
            }

            EnsureCapacity(length);
            fixed (byte* destination = &_buffer[_position])
            {
                Buffer.MemoryCopy(data, destination, length, length);
            }

            _position += length;
        }

        public override void WriteBytes(ReadOnlySpan<byte> data)
        {
            if (data.Length == 0)
            {
                return;
            }

            EnsureCapacity(data.Length);
            data.CopyTo(_buffer.AsSpan(_position));
            _position += data.Length;
        }

        public byte[] ToArray()
        {
            byte[] data = new byte[_position];
            Buffer.BlockCopy(_buffer, 0, data, 0, _position);
            return data;
        }

        public PackedFrameSnapshot ToSnapshot(int tick, int entityCapacity, ComponentSchemaManifest schemaManifest)
        {
            byte[] data = _buffer;
            _buffer = Array.Empty<byte>();
            return new PackedFrameSnapshot(tick, entityCapacity, data, _position, PackedFrameSnapshot.CurrentFormatVersion, schemaManifest);
        }

        private void EnsureCapacity(int appendLength)
        {
            int required = _position + appendLength;
            if (required <= _buffer.Length)
            {
                return;
            }

            int newCapacity = System.Math.Max(required, _buffer.Length * 2);
            byte[] newBuffer = new byte[newCapacity];
            Buffer.BlockCopy(_buffer, 0, newBuffer, 0, _position);
            _buffer = newBuffer;
        }
    }

    /// <summary>
    /// 仅统计写入字节数的状态写入器。
    /// </summary>
    internal sealed unsafe class FrameStateSizingWriter : FrameStateWriter
    {
        private int _bytesWritten;

        public override int BytesWritten => _bytesWritten;

        public override void WriteByte(byte value)
        {
            _bytesWritten += sizeof(byte);
        }

        public override void WriteBytes(void* data, int length)
        {
            if (length > 0)
            {
                _bytesWritten += length;
            }
        }

        public override void WriteBytes(ReadOnlySpan<byte> data)
        {
            _bytesWritten += data.Length;
        }
    }

    /// <summary>
    /// 仅计算校验和的状态写入器，不保留实际缓冲区。
    /// </summary>
    internal sealed unsafe class FrameStateChecksumWriter : FrameStateWriter
    {
        private const ulong FnvPrime = 0x00000100000001B3;
        private const ulong FnvOffset = 0xCBF29CE484222325;

        private ulong _hash = FnvOffset;
        private int _bytesWritten;

        public ulong Checksum => _hash;

        public override int BytesWritten => _bytesWritten;

        public override void WriteByte(byte value)
        {
            _hash ^= value;
            _hash *= FnvPrime;
            _bytesWritten += sizeof(byte);
        }

        public override void WriteBytes(void* data, int length)
        {
            if (length <= 0)
            {
                return;
            }

            ulong hash = _hash;
            byte* cursor = (byte*)data;
            for (int i = 0; i < length; i++)
            {
                hash ^= cursor[i];
                hash *= FnvPrime;
            }

            _hash = hash;
            _bytesWritten += length;
        }

        public override void WriteBytes(ReadOnlySpan<byte> data)
        {
            if (data.IsEmpty)
            {
                return;
            }

            ulong hash = _hash;
            for (int i = 0; i < data.Length; i++)
            {
                hash ^= data[i];
                hash *= FnvPrime;
            }

            _hash = hash;
            _bytesWritten += data.Length;
        }
    }

    /// <summary>
    /// 帧状态读取器。
    /// </summary>
    internal sealed class FrameStateReader
    {
        private readonly byte[] _buffer;
        private readonly int _end;
        private int _position;

        public FrameStateReader(byte[] buffer, int length)
        {
            _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            _position = 0;
            _end = System.Math.Min(buffer.Length, length);
        }

        public int Position => _position;

        public byte ReadByte()
        {
            EnsureCanRead(sizeof(byte));
            return _buffer[_position++];
        }

        public int ReadInt32()
        {
            EnsureCanRead(sizeof(int));
            int value = BinaryPrimitives.ReadInt32LittleEndian(_buffer.AsSpan(_position, sizeof(int)));
            _position += sizeof(int);
            return value;
        }

        public long ReadInt64()
        {
            EnsureCanRead(sizeof(long));
            long value = BinaryPrimitives.ReadInt64LittleEndian(_buffer.AsSpan(_position, sizeof(long)));
            _position += sizeof(long);
            return value;
        }

        public ushort ReadUInt16()
        {
            EnsureCanRead(sizeof(ushort));
            ushort value = BinaryPrimitives.ReadUInt16LittleEndian(_buffer.AsSpan(_position, sizeof(ushort)));
            _position += sizeof(ushort);
            return value;
        }

        public ulong ReadUInt64()
        {
            EnsureCanRead(sizeof(ulong));
            ulong value = BinaryPrimitives.ReadUInt64LittleEndian(_buffer.AsSpan(_position, sizeof(ulong)));
            _position += sizeof(ulong);
            return value;
        }

        public ReadOnlySpan<byte> ReadBytes(int length)
        {
            EnsureCanRead(length);
            ReadOnlySpan<byte> slice = new ReadOnlySpan<byte>(_buffer, _position, length);
            _position += length;
            return slice;
        }

        public unsafe void ReadBytes(void* destination, int length)
        {
            EnsureCanRead(length);
            fixed (byte* source = &_buffer[_position])
            {
                Buffer.MemoryCopy(source, destination, length, length);
            }

            _position += length;
        }

        public BitStream CreateBitStreamSlice(int length)
        {
            EnsureCanRead(length);
            var stream = new BitStream(_buffer, _position, length);
            _position += length;
            return stream;
        }

        private void EnsureCanRead(int length)
        {
            if (_position + length > _end)
            {
                throw new InvalidOperationException("Packed frame snapshot payload is truncated.");
            }
        }
    }

    /// <summary>
    /// 连续字节表示的帧快照。
    /// 主要供 Session 的 checkpoint、采样历史和其他运行时热路径使用。
    /// </summary>
    public sealed class PackedFrameSnapshot
    {
        public const int CurrentFormatVersion = 1;

        public PackedFrameSnapshot(
            int tick,
            int entityCapacity,
            byte[] data,
            int length,
            int formatVersion,
            ComponentSchemaManifest schemaManifest)
        {
            Tick = tick;
            EntityCapacity = entityCapacity;
            Data = data ?? Array.Empty<byte>();
            Length = length;
            FormatVersion = formatVersion;
            SchemaManifest = schemaManifest;
        }

        public int Tick { get; }

        public int EntityCapacity { get; }

        public byte[] Data { get; }

        public int Length { get; }

        public int FormatVersion { get; }

        public ComponentSchemaManifest SchemaManifest { get; }
    }

    /// <summary>
    /// 直接把位级序列化结果写入状态写入器，避免额外的 BitStream 中转缓冲区。
    /// </summary>
    internal sealed unsafe class FrameStateBitStreamWriter : IBitStream
    {
        private readonly FrameStateWriter _writer;
        private byte _pendingByte;
        private int _bitPosition;
        private int _payloadBytesWritten;

        public FrameStateBitStreamWriter(FrameStateWriter writer)
        {
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));
            Writing = true;
        }

        public bool Writing { get; set; }

        public bool Reading => !Writing;

        internal void BeginPayload()
        {
            if (!Writing)
            {
                throw new InvalidOperationException("Cannot begin payload while reading.");
            }

            if (_bitPosition != 0)
            {
                throw new InvalidOperationException("Previous payload was not finalized before starting the next payload.");
            }

            _pendingByte = 0;
            _payloadBytesWritten = 0;
        }

        internal int EndPayload()
        {
            FlushPendingBits();
            int written = _payloadBytesWritten;
            _payloadBytesWritten = 0;
            return written;
        }

        public void Serialize(ref long value)
        {
            if (Writing)
            {
                if (TryWriteAlignedInt64(value))
                {
                    return;
                }

                WriteBits((uint)value, 32);
                WriteBits((uint)(value >> 32), 32);
                return;
            }

            value = 0;
        }

        public void Serialize(ref ulong value)
        {
            if (Writing)
            {
                if (TryWriteAlignedUInt64(value))
                {
                    return;
                }

                WriteBits((uint)value, 32);
                WriteBits((uint)(value >> 32), 32);
                return;
            }

            value = 0;
        }

        public void Serialize(ref uint value)
        {
            if (Writing)
            {
                if (TryWriteAlignedUInt32(value))
                {
                    return;
                }

                WriteBits(value, 32);
                return;
            }

            value = 0;
        }

        public void Serialize(ref int value)
        {
            if (Writing)
            {
                if (TryWriteAlignedInt32(value))
                {
                    return;
                }

                WriteBits((uint)value, 32);
                return;
            }

            value = 0;
        }

        public void Serialize(ref short value)
        {
            if (Writing)
            {
                if (TryWriteAlignedInt16(value))
                {
                    return;
                }

                WriteBits((uint)(ushort)value, 16);
                return;
            }

            value = 0;
        }

        public void Serialize(ref ushort value)
        {
            if (Writing)
            {
                if (TryWriteAlignedUInt16(value))
                {
                    return;
                }

                WriteBits(value, 16);
                return;
            }

            value = 0;
        }

        public void Serialize(ref byte value)
        {
            if (Writing)
            {
                if (_bitPosition == 0)
                {
                    _writer.WriteByte(value);
                    _payloadBytesWritten += sizeof(byte);
                    return;
                }

                WriteBits(value, 8);
                return;
            }

            value = 0;
        }

        public void Serialize(ref bool value)
        {
            if (Writing)
            {
                WriteBit(value);
                return;
            }

            value = false;
        }

        public void Serialize(void* data, int size)
        {
            if (size < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(size));
            }

            AlignToByteBoundary();
            _writer.WriteBytes(data, size);
            _payloadBytesWritten += size;
        }

        public byte[] ToArray()
        {
            throw new NotSupportedException("Direct frame-state bit stream does not expose an intermediate byte array.");
        }

        public void Reset()
        {
            throw new NotSupportedException("Direct frame-state bit stream cannot reset previously written payload bytes.");
        }

        private void WriteBit(bool value)
        {
            if (!Writing)
            {
                throw new InvalidOperationException("Cannot write in reading mode.");
            }

            if (value)
            {
                _pendingByte |= (byte)(1 << _bitPosition);
            }

            _bitPosition++;
            if (_bitPosition >= 8)
            {
                FlushPendingBits();
            }
        }

        private void WriteBits(uint value, int bits)
        {
            for (int i = 0; i < bits; i++)
            {
                WriteBit(((value >> i) & 1U) != 0);
            }
        }

        private bool TryWriteAlignedInt16(short value)
        {
            if (_bitPosition != 0)
            {
                return false;
            }

            Span<byte> buffer = stackalloc byte[sizeof(short)];
            BinaryPrimitives.WriteInt16LittleEndian(buffer, value);
            _writer.WriteBytes(buffer);
            _payloadBytesWritten += buffer.Length;
            return true;
        }

        private bool TryWriteAlignedUInt16(ushort value)
        {
            if (_bitPosition != 0)
            {
                return false;
            }

            Span<byte> buffer = stackalloc byte[sizeof(ushort)];
            BinaryPrimitives.WriteUInt16LittleEndian(buffer, value);
            _writer.WriteBytes(buffer);
            _payloadBytesWritten += buffer.Length;
            return true;
        }

        private bool TryWriteAlignedInt32(int value)
        {
            if (_bitPosition != 0)
            {
                return false;
            }

            Span<byte> buffer = stackalloc byte[sizeof(int)];
            BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
            _writer.WriteBytes(buffer);
            _payloadBytesWritten += buffer.Length;
            return true;
        }

        private bool TryWriteAlignedUInt32(uint value)
        {
            if (_bitPosition != 0)
            {
                return false;
            }

            Span<byte> buffer = stackalloc byte[sizeof(uint)];
            BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
            _writer.WriteBytes(buffer);
            _payloadBytesWritten += buffer.Length;
            return true;
        }

        private bool TryWriteAlignedInt64(long value)
        {
            if (_bitPosition != 0)
            {
                return false;
            }

            Span<byte> buffer = stackalloc byte[sizeof(long)];
            BinaryPrimitives.WriteInt64LittleEndian(buffer, value);
            _writer.WriteBytes(buffer);
            _payloadBytesWritten += buffer.Length;
            return true;
        }

        private bool TryWriteAlignedUInt64(ulong value)
        {
            if (_bitPosition != 0)
            {
                return false;
            }

            Span<byte> buffer = stackalloc byte[sizeof(ulong)];
            BinaryPrimitives.WriteUInt64LittleEndian(buffer, value);
            _writer.WriteBytes(buffer);
            _payloadBytesWritten += buffer.Length;
            return true;
        }

        private void FlushPendingBits()
        {
            if (_bitPosition == 0)
            {
                return;
            }

            _writer.WriteByte(_pendingByte);
            _pendingByte = 0;
            _bitPosition = 0;
            _payloadBytesWritten += sizeof(byte);
        }

        private void AlignToByteBoundary()
        {
            if (_bitPosition != 0)
            {
                FlushPendingBits();
            }
        }
    }

    /// <summary>
    /// 直接从状态读取器消费固定位长 payload，避免每个条目构造一个 BitStream 实例。
    /// </summary>
    internal sealed unsafe class FrameStateBitStreamReader : IBitStream
    {
        private readonly FrameStateReader _reader;
        private byte _currentByte;
        private int _bitPosition;
        private int _remainingBytes;

        public FrameStateBitStreamReader(FrameStateReader reader)
        {
            _reader = reader ?? throw new ArgumentNullException(nameof(reader));
            Writing = false;
        }

        public bool Writing { get; set; }

        public bool Reading => !Writing;

        internal void BeginPayload(int length)
        {
            if (length < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            if (Writing)
            {
                throw new InvalidOperationException("Cannot begin payload while writing.");
            }

            _currentByte = 0;
            _bitPosition = 0;
            _remainingBytes = length;
        }

        internal void EndPayload()
        {
            if (_remainingBytes != 0)
            {
                throw new InvalidOperationException("Serialized payload was not fully consumed during restore.");
            }

            _currentByte = 0;
            _bitPosition = 0;
        }

        public void Serialize(ref long value)
        {
            if (Writing)
            {
                return;
            }

            if (_bitPosition == 0)
            {
                EnsureRemaining(sizeof(long));
                value = _reader.ReadInt64();
                _remainingBytes -= sizeof(long);
                return;
            }

            long low = ReadBits(32);
            long high = ReadBits(32);
            value = low | (high << 32);
        }

        public void Serialize(ref ulong value)
        {
            if (Writing)
            {
                return;
            }

            if (_bitPosition == 0)
            {
                EnsureRemaining(sizeof(ulong));
                value = _reader.ReadUInt64();
                _remainingBytes -= sizeof(ulong);
                return;
            }

            ulong low = ReadBits(32);
            ulong high = ReadBits(32);
            value = low | (high << 32);
        }

        public void Serialize(ref uint value)
        {
            if (Writing)
            {
                return;
            }

            if (_bitPosition == 0)
            {
                EnsureRemaining(sizeof(uint));
                value = unchecked((uint)_reader.ReadInt32());
                _remainingBytes -= sizeof(uint);
                return;
            }

            value = ReadBits(32);
        }

        public void Serialize(ref int value)
        {
            if (Writing)
            {
                return;
            }

            if (_bitPosition == 0)
            {
                EnsureRemaining(sizeof(int));
                value = _reader.ReadInt32();
                _remainingBytes -= sizeof(int);
                return;
            }

            value = (int)ReadBits(32);
        }

        public void Serialize(ref short value)
        {
            if (Writing)
            {
                return;
            }

            if (_bitPosition == 0)
            {
                EnsureRemaining(sizeof(short));
                value = unchecked((short)_reader.ReadUInt16());
                _remainingBytes -= sizeof(short);
                return;
            }

            value = (short)ReadBits(16);
        }

        public void Serialize(ref ushort value)
        {
            if (Writing)
            {
                return;
            }

            if (_bitPosition == 0)
            {
                EnsureRemaining(sizeof(ushort));
                value = _reader.ReadUInt16();
                _remainingBytes -= sizeof(ushort);
                return;
            }

            value = (ushort)ReadBits(16);
        }

        public void Serialize(ref byte value)
        {
            if (Writing)
            {
                return;
            }

            if (_bitPosition == 0)
            {
                EnsureRemaining(sizeof(byte));
                value = _reader.ReadByte();
                _remainingBytes -= sizeof(byte);
                return;
            }

            value = (byte)ReadBits(8);
        }

        public void Serialize(ref bool value)
        {
            if (Writing)
            {
                return;
            }

            value = ReadBit();
        }

        public void Serialize(void* data, int size)
        {
            if (size < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(size));
            }

            AlignToByteBoundary();
            EnsureRemaining(size);
            _reader.ReadBytes(data, size);
            _remainingBytes -= size;
        }

        public byte[] ToArray()
        {
            throw new NotSupportedException("Direct frame-state bit stream reader does not materialize intermediate byte arrays.");
        }

        public void Reset()
        {
            throw new NotSupportedException("Direct frame-state bit stream reader cannot rewind an already-consumed payload.");
        }

        private bool ReadBit()
        {
            if (Writing)
            {
                throw new InvalidOperationException("Cannot read in writing mode.");
            }

            if (_bitPosition == 0)
            {
                EnsureRemaining(sizeof(byte));
                _currentByte = _reader.ReadByte();
                _remainingBytes--;
            }

            bool value = (_currentByte & (1 << _bitPosition)) != 0;
            _bitPosition++;
            if (_bitPosition >= 8)
            {
                _bitPosition = 0;
            }

            return value;
        }

        private uint ReadBits(int bits)
        {
            uint value = 0;
            for (int i = 0; i < bits; i++)
            {
                if (ReadBit())
                {
                    value |= 1U << i;
                }
            }

            return value;
        }

        private void AlignToByteBoundary()
        {
            if (_bitPosition != 0)
            {
                _bitPosition = 0;
            }
        }

        private void EnsureRemaining(int length)
        {
            if (length > _remainingBytes)
            {
                throw new InvalidOperationException("Packed frame snapshot payload is truncated.");
            }
        }
    }
}
