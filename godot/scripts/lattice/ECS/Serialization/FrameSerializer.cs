// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

namespace Lattice.ECS.Serialization
{
    /// <summary>
    /// 帧序列化器接口（对齐 FrameSync IDeterministicFrameSerializer）
    /// </summary>
    public interface IFrameSerializer
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
    }

    /// <summary>
    /// 位流接口（对齐 FrameSync IBitStream）
    /// </summary>
    public interface IBitStream
    {
        void Serialize(ref ulong value);
        void Serialize(ref uint value);
        void Serialize(ref int value);
        void Serialize(ref short value);
        void Serialize(ref ushort value);
        void Serialize(ref byte value);
        void Serialize(ref bool value);
    }

    /// <summary>
    /// 帧序列化器（对齐 FrameSync）
    /// </summary>
    public class FrameSerializer : IFrameSerializer
    {
        private readonly IBitStream _stream;
        private readonly bool _writing;

        public FrameSerializer(IBitStream stream, bool writing)
        {
            _stream = stream;
            _writing = writing;
        }

        public bool Writing => _writing;
        public bool Reading => !_writing;
        public IBitStream Stream => _stream;
    }
}
