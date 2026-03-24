// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;
using Lattice.Core;
using CoreBitStream = Lattice.ECS.Core.BitStream;

namespace Lattice.ECS.Serialization
{
    /// <summary>
    /// 帧序列化器接口（对齐 FrameSync IDeterministicFrameSerializer）
    /// </summary>
    public unsafe interface IFrameSerializer
    {
        /// <summary>
        /// 是否正在写入（序列化）
        /// </summary>
        bool Writing { get; }

        /// <summary>
        /// 是否正在读取（反序列化）
        /// </summary>
        bool Reading { get; }

        /// <summary>
        /// 序列化流
        /// </summary>
        IBitStream Stream { get; }

        void Serialize(ref long value);
        void Serialize(ref ulong value);
        void Serialize(ref int value);
        void Serialize(ref uint value);
        void Serialize(ref short value);
        void Serialize(ref ushort value);
        void Serialize(ref byte value);
        void Serialize(ref bool value);
        void Serialize(ref EntityRef value);
        void Serialize<T>(ref T value) where T : unmanaged;
        void Serialize(void* data, int size);
    }

    /// <summary>
    /// 位流接口（对齐 FrameSync IBitStream）
    /// </summary>
    public unsafe interface IBitStream
    {
        bool Writing { get; set; }
        bool Reading { get; }
        void Serialize(ref long value);
        void Serialize(ref ulong value);
        void Serialize(ref uint value);
        void Serialize(ref int value);
        void Serialize(ref short value);
        void Serialize(ref ushort value);
        void Serialize(ref byte value);
        void Serialize(ref bool value);
        void Serialize(void* data, int size);
        byte[] ToArray();
        void Reset();
    }

    /// <summary>
    /// 基于现有 Core.BitStream 的序列化适配器。
    /// </summary>
    public unsafe sealed class BitStreamAdapter : IBitStream
    {
        private readonly CoreBitStream _stream;

        public BitStreamAdapter(CoreBitStream stream)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        }

        public bool Writing
        {
            get => _stream.IsWriting;
            set => _stream.IsWriting = value;
        }

        public bool Reading => _stream.IsReading;

        public void Serialize(ref long value)
        {
            if (Writing)
            {
                _stream.WriteLong(value);
                return;
            }

            value = _stream.ReadLong();
        }

        public void Serialize(ref ulong value)
        {
            if (Writing)
            {
                _stream.WriteULong(value);
                return;
            }

            value = _stream.ReadULong();
        }

        public void Serialize(ref uint value)
        {
            if (Writing)
            {
                _stream.WriteUInt(value);
                return;
            }

            value = _stream.ReadUInt();
        }

        public void Serialize(ref int value)
        {
            if (Writing)
            {
                _stream.WriteInt(value);
                return;
            }

            value = _stream.ReadInt();
        }

        public void Serialize(ref short value)
        {
            if (Writing)
            {
                _stream.WriteShort(value);
                return;
            }

            value = _stream.ReadShort();
        }

        public void Serialize(ref ushort value)
        {
            if (Writing)
            {
                _stream.WriteUShort(value);
                return;
            }

            value = _stream.ReadUShort();
        }

        public void Serialize(ref byte value)
        {
            if (Writing)
            {
                _stream.WriteByte(value);
                return;
            }

            value = _stream.ReadByte();
        }

        public void Serialize(ref bool value)
        {
            if (Writing)
            {
                _stream.WriteBool(value);
                return;
            }

            value = _stream.ReadBool();
        }

        public void Serialize(void* data, int size)
        {
            if (size < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(size));
            }

            if (Writing)
            {
                _stream.WriteMemory(data, size);
                return;
            }

            _stream.ReadMemory(data, size);
        }

        public byte[] ToArray()
        {
            return _stream.ToArray();
        }

        public void Reset()
        {
            _stream.Reset();
        }
    }

    /// <summary>
    /// 帧序列化器（对齐 FrameSync）
    /// </summary>
    public unsafe sealed class FrameSerializer : IFrameSerializer
    {
        private readonly IBitStream _stream;

        public FrameSerializer(IBitStream stream, bool writing)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _stream.Writing = writing;
        }

        public FrameSerializer(CoreBitStream stream, bool writing)
            : this(new BitStreamAdapter(stream), writing)
        {
        }

        public bool Writing => _stream.Writing;
        public bool Reading => _stream.Reading;
        public IBitStream Stream => _stream;

        public void Serialize(ref long value) => _stream.Serialize(ref value);
        public void Serialize(ref ulong value) => _stream.Serialize(ref value);
        public void Serialize(ref int value) => _stream.Serialize(ref value);
        public void Serialize(ref uint value) => _stream.Serialize(ref value);
        public void Serialize(ref short value) => _stream.Serialize(ref value);
        public void Serialize(ref ushort value) => _stream.Serialize(ref value);
        public void Serialize(ref byte value) => _stream.Serialize(ref value);
        public void Serialize(ref bool value) => _stream.Serialize(ref value);

        public void Serialize(ref EntityRef value)
        {
            ulong raw = value.Raw;
            _stream.Serialize(ref raw);

            if (Reading)
            {
                value = EntityRef.FromRaw(raw);
            }
        }

        public void Serialize<T>(ref T value) where T : unmanaged
        {
            fixed (T* valuePtr = &value)
            {
                Serialize(valuePtr, sizeof(T));
            }
        }

        public void Serialize(void* data, int size)
        {
            _stream.Serialize(data, size);
        }
    }
}
