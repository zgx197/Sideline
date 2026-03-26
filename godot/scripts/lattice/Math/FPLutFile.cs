// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

#nullable enable

using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace Lattice.Math
{
    /// <summary>
    /// LUT 文件读写器 - 支持版本兼容的 LUT 文件格式
    /// <para>文件格式: [Header(32 bytes)][Data(variable)]</para>
    /// </summary>
    public static class FPLutFile
    {
        /// <summary>
        /// LUT 文件扩展名
        /// </summary>
        public const string FileExtension = ".fplut";

        /// <summary>
        /// 从文件加载 LUT
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>解析后的 LUT 数据</returns>
        /// <exception cref="FileNotFoundException">文件不存在</exception>
        /// <exception cref="InvalidDataException">文件格式无效</exception>
        /// <exception cref="InvalidDataException">版本不兼容</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPLutData Load(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("LUT file not found", filePath);

            byte[] fileData = File.ReadAllBytes(filePath);
            return LoadFromBytes(fileData);
        }

        /// <summary>
        /// 从字节数组加载 LUT
        /// </summary>
        /// <param name="data">原始字节数据</param>
        /// <returns>解析后的 LUT 数据</returns>
        public static FPLutData LoadFromBytes(byte[] data)
        {
            if (data.Length < FPLutHeader.HeaderSize)
                throw new InvalidDataException($"Data too small for LUT header: {data.Length} bytes");

            // 解析头部
            var header = FPLutHeader.FromBytes(data);

            // 验证数据大小
            long expectedDataSize = header.EntryCount * header.EntrySize;
            long actualDataSize = data.Length - FPLutHeader.HeaderSize;

            if (actualDataSize < expectedDataSize)
                throw new InvalidDataException(
                    $"LUT data truncated: expected {expectedDataSize} bytes, got {actualDataSize}");

            // 验证校验和（如果启用）
            if (header.Checksum != 0)
            {
                byte[] payload = new byte[expectedDataSize];
                Buffer.BlockCopy(data, FPLutHeader.HeaderSize, payload, 0, (int)expectedDataSize);

                if (!header.VerifyChecksum(payload))
                    throw new InvalidDataException("LUT checksum verification failed");
            }

            // 提取数据
            long[] longData;
            int[] intData;

            if (header.EntrySize == 8)
            {
                longData = new long[header.EntryCount];
                Buffer.BlockCopy(data, FPLutHeader.HeaderSize, longData, 0, (int)expectedDataSize);
                intData = Array.Empty<int>();
            }
            else
            {
                intData = new int[header.EntryCount];
                Buffer.BlockCopy(data, FPLutHeader.HeaderSize, intData, 0, (int)expectedDataSize);
                longData = Array.Empty<long>();
            }

            return new FPLutData(header, longData, intData);
        }

        /// <summary>
        /// 保存 LUT 到文件
        /// </summary>
        /// <param name="filePath">目标文件路径</param>
        /// <param name="data">LUT 数据</param>
        /// <param name="version">文件版本（默认当前版本）</param>
        public static void Save(string filePath, FPLutData data, uint? version = null)
        {
            byte[] bytes = SaveToBytes(data, version);
            File.WriteAllBytes(filePath, bytes);
        }

        /// <summary>
        /// 将 LUT 数据序列化为字节数组
        /// </summary>
        /// <param name="data">LUT 数据</param>
        /// <param name="version">文件版本（默认当前版本）</param>
        /// <returns>序列化后的字节数组</returns>
        public static byte[] SaveToBytes(FPLutData data, uint? version = null)
        {
            var header = data.Header;
            header.Version = version ?? FPLutHeader.CurrentVersion;
            header.FileMagic = FPLutHeader.Magic;

            // 计算校验和
            if (header.EntrySize == 8 && data.LongData.Length > 0)
            {
                byte[] payload = new byte[data.LongData.Length * 8];
                Buffer.BlockCopy(data.LongData, 0, payload, 0, payload.Length);
                header.Checksum = FPLutHeader.ComputeChecksum(payload);
            }
            else if (header.EntrySize == 4 && data.IntData.Length > 0)
            {
                byte[] payload = new byte[data.IntData.Length * 4];
                Buffer.BlockCopy(data.IntData, 0, payload, 0, payload.Length);
                header.Checksum = FPLutHeader.ComputeChecksum(payload);
            }

            // 序列化
            byte[] headerBytes = header.ToBytes();
            byte[] result = new byte[FPLutHeader.HeaderSize +
                (header.EntrySize == 8 ? data.LongData.Length * 8 : data.IntData.Length * 4)];

            Buffer.BlockCopy(headerBytes, 0, result, 0, FPLutHeader.HeaderSize);

            if (header.EntrySize == 8 && data.LongData.Length > 0)
            {
                Buffer.BlockCopy(data.LongData, 0, result, FPLutHeader.HeaderSize, data.LongData.Length * 8);
            }
            else if (data.IntData.Length > 0)
            {
                Buffer.BlockCopy(data.IntData, 0, result, FPLutHeader.HeaderSize, data.IntData.Length * 4);
            }

            return result;
        }

        /// <summary>
        /// 检查文件是否为有效的 LUT 文件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>是否有效</returns>
        public static bool IsValidLutFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return false;

                byte[] data = File.ReadAllBytes(filePath);
                if (data.Length < FPLutHeader.HeaderSize)
                    return false;

                uint magic = BitConverter.ToUInt32(data, 0);
                return magic == FPLutHeader.Magic;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 获取 LUT 文件信息（不加载完整数据）
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>文件头信息</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPLutHeader GetFileInfo(string filePath)
        {
            byte[] headerData = new byte[FPLutHeader.HeaderSize];
            using (var fs = File.OpenRead(filePath))
            {
                fs.Read(headerData, 0, FPLutHeader.HeaderSize);
            }
            return FPLutHeader.FromBytes(headerData);
        }

        /// <summary>
        /// 批量转换旧版本 LUT 文件
        /// </summary>
        /// <param name="sourceDir">源目录</param>
        /// <param name="targetDir">目标目录</param>
        /// <param name="targetVersion">目标版本</param>
        public static void BatchConvert(string sourceDir, string targetDir, uint targetVersion)
        {
            Directory.CreateDirectory(targetDir);

            foreach (string file in Directory.GetFiles(sourceDir, "*" + FileExtension))
            {
                string fileName = Path.GetFileName(file);
                string targetPath = Path.Combine(targetDir, fileName);

                try
                {
                    var data = Load(file);
                    Save(targetPath, data, targetVersion);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Failed to convert {fileName}: {ex.Message}", ex);
                }
            }
        }
    }

    /// <summary>
    /// LUT 数据结构
    /// </summary>
    public readonly struct FPLutData
    {
        /// <summary>文件头</summary>
        public readonly FPLutHeader Header;

        /// <summary>64-bit 数据（EntrySize == 8 时使用）</summary>
        public readonly long[] LongData;

        /// <summary>32-bit 数据（EntrySize == 4 时使用）</summary>
        public readonly int[] IntData;

        public FPLutData(FPLutHeader header, long[] longData, int[] intData)
        {
            Header = header;
            LongData = longData;
            IntData = intData;
        }

        /// <summary>
        /// 创建 64-bit LUT 数据
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPLutData CreateLong(LutType type, long[] data)
        {
            var header = new FPLutHeader(
                FPLutHeader.CurrentVersion,
                type,
                (uint)data.Length,
                8,  // 8 bytes per entry
                0   // checksum computed on save
            );
            return new FPLutData(header, data, Array.Empty<int>());
        }

        /// <summary>
        /// 创建 32-bit LUT 数据
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPLutData CreateInt(LutType type, int[] data)
        {
            var header = new FPLutHeader(
                FPLutHeader.CurrentVersion,
                type,
                (uint)data.Length,
                4,  // 4 bytes per entry
                0   // checksum computed on save
            );
            return new FPLutData(header, Array.Empty<long>(), data);
        }
    }

    /// <summary>
    /// LUT 版本兼容性检查器
    /// </summary>
    public static class FPLutCompatibility
    {
        /// <summary>
        /// 检查版本兼容性
        /// </summary>
        /// <param name="fileVersion">文件版本</param>
        /// <returns>兼容性结果</returns>
        public static CompatibilityResult CheckCompatibility(uint fileVersion)
        {
            uint current = FPLutHeader.CurrentVersion;
            uint min = FPLutHeader.MinCompatibleVersion;

            if (fileVersion == current)
                return CompatibilityResult.ExactMatch;

            if (fileVersion > current)
                return CompatibilityResult.NewerVersion;

            if (fileVersion >= min)
                return CompatibilityResult.BackwardCompatible;

            return CompatibilityResult.Incompatible;
        }

        /// <summary>
        /// 获取版本变更说明
        /// </summary>
        public static string GetVersionChanges(uint fromVersion, uint toVersion)
        {
            // 版本变更日志
            if (fromVersion == 0x00010000 && toVersion == 0x00010000)
                return "Same version (1.0.0)";

            return $"Unknown version transition: {FPLutHeader.VersionToString(fromVersion)} -> {FPLutHeader.VersionToString(toVersion)}";
        }
    }

    /// <summary>
    /// 兼容性结果
    /// </summary>
    public enum CompatibilityResult
    {
        /// <summary>完全匹配</summary>
        ExactMatch,

        /// <summary>向后兼容（旧版本）</summary>
        BackwardCompatible,

        /// <summary>新版本（需要更新）</summary>
        NewerVersion,

        /// <summary>不兼容</summary>
        Incompatible
    }
}
