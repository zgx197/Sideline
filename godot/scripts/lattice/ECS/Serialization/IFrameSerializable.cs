// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

namespace Lattice.ECS.Serialization
{
    /// <summary>
    /// 帧序列化接口，对齐 FrameSync 序列化设计
    /// 用于支持帧快照、回放和确定性校验
    /// </summary>
    public interface IFrameSerializable
    {
        /// <summary>
        /// 序列化到字节缓冲区
        /// </summary>
        /// <param name="buffer">目标缓冲区</param>
        /// <param name="offset">起始偏移</param>
        /// <returns>写入的字节数</returns>
        int Serialize(byte[] buffer, int offset);

        /// <summary>
        /// 从字节缓冲区反序列化
        /// </summary>
        /// <param name="buffer">源缓冲区</param>
        /// <param name="offset">起始偏移</param>
        /// <returns>读取的字节数</returns>
        int Deserialize(byte[] buffer, int offset);

        /// <summary>
        /// 序列化后的字节大小
        /// </summary>
        int SerializedSize { get; }
    }

    /// <summary>
    /// 不安全帧序列化接口（用于高性能场景）
    /// </summary>
    public unsafe interface IUnsafeFrameSerializable
    {
        /// <summary>
        /// 序列化到指针
        /// </summary>
        /// <param name="ptr">目标指针</param>
        /// <param name="size">缓冲区大小</param>
        /// <returns>写入的字节数</returns>
        int Serialize(byte* ptr, int size);

        /// <summary>
        /// 从指针反序列化
        /// </summary>
        /// <param name="ptr">源指针</param>
        /// <param name="size">缓冲区大小</param>
        /// <returns>读取的字节数</returns>
        int Deserialize(byte* ptr, int size);

        /// <summary>
        /// 序列化后的字节大小
        /// </summary>
        int SerializedSize { get; }
    }
}
