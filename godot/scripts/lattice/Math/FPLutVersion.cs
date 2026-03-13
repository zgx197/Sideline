// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

#nullable enable

using System;
using System.IO;
using System.Text;

namespace Lattice.Math
{
    /// <summary>
    /// LUT 文件版本头
    /// </summary>
    public struct FPLutHeader : IEquatable<FPLutHeader>
    {
        /// <summary>魔数：'FPLT' = 0x464P4C54 (FP = 0x464P, LT = 0x4C54)</summary>
        public const uint Magic = 0x464F4C54;  // 'F' 'P' 'L' 'T' ASCII

        /// <summary>当前版本：1.0.0 = 0x00010000</summary>
        public const uint CurrentVersion = 0x00010000;

        /// <summary>兼容的最旧版本：1.0.0</summary>
        public const uint MinCompatibleVersion = 0x00010000;

        /// <summary>魔数（4 bytes）</summary>
        public uint FileMagic;

        /// <summary>版本号（4 bytes）：主.次.修.预 = 1 byte each</summary>
        public uint Version;

        /// <summary>LUT 类型（4 bytes）</summary>
        public LutType Type;

        /// <summary>条目数量（4 bytes）</summary>
        public uint EntryCount;

        /// <summary>每个条目字节数（4 bytes）</summary>
        public uint EntrySize;

        /// <summary>校验和 CRC32（4 bytes）</summary>
        public uint Checksum;

        /// <summary>保留字段（16 bytes）</summary>
        public ulong Reserved1;
        public ulong Reserved2;

        /// <summary>头部总大小：32 bytes</summary>
        public const int HeaderSize = 32;

        public FPLutHeader(uint version, LutType type, uint entryCount, uint entrySize, uint checksum)
        {
            FileMagic = Magic;
            Version = version;
            Type = type;
            EntryCount = entryCount;
            EntrySize = entrySize;
            Checksum = checksum;
            Reserved1 = 0;
            Reserved2 = 0;
        }

        /// <summary>
        /// 从字节数组解析头部
        /// </summary>
        public static FPLutHeader FromBytes(byte[] data)
        {
            if (data.Length < HeaderSize)
                throw new InvalidDataException($"LUT header too small: {data.Length} bytes");

            var header = new FPLutHeader
            {
                FileMagic = BitConverter.ToUInt32(data, 0),
                Version = BitConverter.ToUInt32(data, 4),
                Type = (LutType)BitConverter.ToUInt32(data, 8),
                EntryCount = BitConverter.ToUInt32(data, 12),
                EntrySize = BitConverter.ToUInt32(data, 16),
                Checksum = BitConverter.ToUInt32(data, 20),
                Reserved1 = BitConverter.ToUInt64(data, 24),
                Reserved2 = BitConverter.ToUInt64(data, 32)
            };

            header.Validate();
            return header;
        }

        /// <summary>
        /// 将头部写入字节数组
        /// </summary>
        public byte[] ToBytes()
        {
            var data = new byte[HeaderSize];
            BitConverter.GetBytes(FileMagic).CopyTo(data, 0);
            BitConverter.GetBytes(Version).CopyTo(data, 4);
            BitConverter.GetBytes((uint)Type).CopyTo(data, 8);
            BitConverter.GetBytes(EntryCount).CopyTo(data, 12);
            BitConverter.GetBytes(EntrySize).CopyTo(data, 16);
            BitConverter.GetBytes(Checksum).CopyTo(data, 20);
            BitConverter.GetBytes(Reserved1).CopyTo(data, 24);
            BitConverter.GetBytes(Reserved2).CopyTo(data, 32);
            return data;
        }

        /// <summary>
        /// 验证头部有效性
        /// </summary>
        public void Validate()
        {
            if (FileMagic != Magic)
                throw new InvalidDataException($"Invalid LUT magic: 0x{FileMagic:X8}, expected 0x{Magic:X8}");

            if (Version < MinCompatibleVersion)
                throw new InvalidDataException($"LUT version {VersionToString(Version)} is too old. Minimum: {VersionToString(MinCompatibleVersion)}");

            if (EntryCount == 0)
                throw new InvalidDataException("LUT entry count is zero");

            if (EntrySize != 4 && EntrySize != 8)
                throw new InvalidDataException($"Invalid entry size: {EntrySize}, expected 4 or 8");
        }

        /// <summary>
        /// 验证数据校验和
        /// </summary>
        public bool VerifyChecksum(byte[] data)
        {
            uint computed = ComputeChecksum(data);
            return computed == Checksum;
        }

        /// <summary>
        /// 计算 CRC32 校验和
        /// </summary>
        public static uint ComputeChecksum(byte[] data)
        {
            // 简化实现：实际使用 System.IO.Hashing.Crc32
            uint crc = 0xFFFFFFFF;
            foreach (byte b in data)
            {
                crc ^= b;
                for (int i = 0; i < 8; i++)
                    crc = (crc >> 1) ^ (0xEDB88320u & (uint)(-(crc & 1)));
            }
            return ~crc;
        }

        /// <summary>
        /// 版本号转字符串
        /// </summary>
        public static string VersionToString(uint version)
        {
            byte major = (byte)(version >> 24);
            byte minor = (byte)(version >> 16);
            byte patch = (byte)(version >> 8);
            byte pre = (byte)version;

            if (pre == 0)
                return $"{major}.{minor}.{patch}";
            return $"{major}.{minor}.{patch}-pre{pre}";
        }

        public bool Equals(FPLutHeader other) =>
            FileMagic == other.FileMagic &&
            Version == other.Version &&
            Type == other.Type;

        public override bool Equals(object? obj) => obj is FPLutHeader other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(FileMagic, Version, Type);
        public override string ToString() => $"FPLut v{VersionToString(Version)}, {Type}, {EntryCount} entries";
    }

    /// <summary>
    /// LUT 类型枚举
    /// </summary>
    public enum LutType : uint
    {
        Unknown = 0,
        SinCos = 1,
        Tan = 2,
        Asin = 3,
        Acos = 4,
        Atan = 5,
        Sqrt = 6,
        Exp = 7,
        Log = 8,
        Custom = 0xFFFFFFFF
    }
}
