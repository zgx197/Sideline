// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;
using Lattice.Core;

namespace Lattice.ECS.Core
{
    /// <summary>
    /// 帧序列化�?- FrameSync 风格
    /// 
    /// 用于组件的序列化和反序列化，支持位流打包
    /// </summary>
    public unsafe sealed class FrameSerializer
    {
        #region 字段

        /// <summary>序列化模�?/summary>
        public enum Mode
        {
            /// <summary>写入数据（序列化�?/summary>
            Serialize,
            /// <summary>读取数据（反序列化）</summary>
            Deserialize,
            /// <summary>计算校验�?/summary>
            Checksum
        }

        /// <summary>当前模式</summary>
        public Mode CurrentMode { get; }

        /// <summary>位流</summary>
        public BitStream Stream { get; }

        /// <summary>当前�?/summary>
        public Frame Frame { get; set; }

        /// <summary>是否正在写入</summary>
        public bool IsWriting => CurrentMode == Mode.Serialize;

        /// <summary>是否正在读取</summary>
        public bool IsReading => CurrentMode == Mode.Deserialize;

        /// <summary>是否在校验和模式</summary>
        public bool IsChecksum => CurrentMode == Mode.Checksum;

        #endregion

        #region 构造函�?
        public FrameSerializer(Mode mode, Frame frame, BitStream stream)
        {
            CurrentMode = mode;
            Frame = frame;
            Stream = stream ?? throw new ArgumentNullException(nameof(stream));

            // 设置流的模式
            stream.IsWriting = IsWriting;
        }

        public FrameSerializer(Mode mode, Frame frame, int bufferSize = 1024)
            : this(mode, frame, new BitStream(bufferSize))
        {
        }

        #endregion

        #region 基础类型序列�?
        /// <summary>
        /// 序列�?反序列化 int �?        /// </summary>
        public void Serialize(ref int value)
        {
            if (IsWriting)
                Stream.WriteInt(value);
            else
                value = Stream.ReadInt();
        }

        /// <summary>
        /// 序列�?反序列化 uint �?        /// </summary>
        public void Serialize(ref uint value)
        {
            if (IsWriting)
                Stream.WriteUInt(value);
            else
                value = Stream.ReadUInt();
        }

        /// <summary>
        /// 序列�?反序列化 short �?        /// </summary>
        public void Serialize(ref short value)
        {
            if (IsWriting)
                Stream.WriteShort(value);
            else
                value = Stream.ReadShort();
        }

        /// <summary>
        /// 序列�?反序列化 ushort �?        /// </summary>
        public void Serialize(ref ushort value)
        {
            if (IsWriting)
                Stream.WriteUShort(value);
            else
                value = Stream.ReadUShort();
        }

        /// <summary>
        /// 序列�?反序列化 byte �?        /// </summary>
        public void Serialize(ref byte value)
        {
            if (IsWriting)
                Stream.WriteByte(value);
            else
                value = Stream.ReadByte();
        }

        /// <summary>
        /// 序列�?反序列化 bool �?        /// </summary>
        public void Serialize(ref bool value)
        {
            if (IsWriting)
                Stream.WriteBool(value);
            else
                value = Stream.ReadBool();
        }

        /// <summary>
        /// 序列�?反序列化 long �?        /// </summary>
        public void Serialize(ref long value)
        {
            if (IsWriting)
                Stream.WriteLong(value);
            else
                value = Stream.ReadLong();
        }

        /// <summary>
        /// 序列�?反序列化 ulong �?        /// </summary>
        public void Serialize(ref ulong value)
        {
            if (IsWriting)
                Stream.WriteULong(value);
            else
                value = Stream.ReadULong();
        }

        /// <summary>
        /// 序列�?反序列化变长 int
        /// </summary>
        public void SerializeVarInt(ref int value)
        {
            if (IsWriting)
                Stream.WriteVarInt(value);
            else
                value = Stream.ReadVarInt();
        }

        /// <summary>
        /// 序列�?反序列化变长 uint
        /// </summary>
        public void SerializeVarUInt(ref uint value)
        {
            if (IsWriting)
                Stream.WriteVarUInt(value);
            else
                value = Stream.ReadVarUInt();
        }

        #endregion

        #region 定点数序列化

        /// <summary>
        /// 序列�?反序列化 FP（定点数�?        /// </summary>
        public void Serialize(ref Lattice.Math.FP value)
        {
            long raw = IsWriting ? value.RawValue : 0;
            Serialize(ref raw);
            if (IsReading)
            {
                value = new Lattice.Math.FP(raw);
            }
        }

        /// <summary>
        /// 序列�?反序列化 FPVector2
        /// </summary>
        public unsafe void Serialize(ref Lattice.Math.FPVector2 value)
        {
            fixed (Lattice.Math.FP* ptr = &value.X)
            {
                Serialize(ptr, sizeof(Lattice.Math.FP) * 2);
            }
        }

        /// <summary>
        /// 序列�?反序列化 FPVector3
        /// </summary>
        public unsafe void Serialize(ref Lattice.Math.FPVector3 value)
        {
            fixed (Lattice.Math.FP* ptr = &value.X)
            {
                Serialize(ptr, sizeof(Lattice.Math.FP) * 3);
            }
        }

        #endregion

        #region 原始内存序列�?
        /// <summary>
        /// 序列�?反序列化原始内存
        /// </summary>
        public void Serialize(void* data, int size)
        {
            if (IsWriting)
                Stream.WriteMemory(data, size);
            else
                Stream.ReadMemory(data, size);
        }

        /// <summary>
        /// 序列�?反序列化结构
        /// </summary>
        public void Serialize<T>(ref T value) where T : unmanaged
        {
            fixed (T* ptr = &value)
            {
                Serialize(ptr, sizeof(T));
            }
        }

        #endregion

        #region 实体和组件引�?
        /// <summary>
        /// 序列�?反序列化 EntityRef
        /// </summary>
        public void Serialize(ref EntityRef EntityRef)
        {
            int index = EntityRef.Index;
            int version = EntityRef.Version;
            Serialize(ref index);
            Serialize(ref version);
            EntityRef = new EntityRef(index, version);
        }

        /// <summary>
        /// 序列�?反序列化 ComponentSet
        /// </summary>
        public void Serialize(ref ComponentSet componentSet)
        {
            // ComponentSet �?8 �?ulong，共 64 字节
            fixed (ulong* ptr = componentSet.Set)
            {
                Serialize(ptr, 64);
            }
        }

        #endregion

        #region 流控�?
        /// <summary>
        /// 重置序列化器
        /// </summary>
        public void Reset()
        {
            Stream.Reset();
        }

        /// <summary>
        /// 获取序列化后的数�?        /// </summary>
        public byte[] GetData()
        {
            return Stream.ToArray();
        }

        /// <summary>
        /// 获取当前位置
        /// </summary>
        public int Position => Stream.BytePosition;

        #endregion
    }
}
