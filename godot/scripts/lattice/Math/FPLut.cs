// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

#nullable enable

using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace Lattice.Math
{
    /// <summary>
    /// LUT (查找表) 管理器：运行时加载数学函数查找表
    /// <para>支持文件系统加载和嵌入资源加载</para>
    /// </summary>
    public static class FPLut
    {
        #region 常量

        /// <summary>LUT 精度：16 位小数</summary>
        public const int Precision = 16;

        /// <summary>π 的原始值</summary>
        public const long PI = 205887L;

        /// <summary>2π 的原始值</summary>
        public const long PiTimes2 = 411775L;

        /// <summary>π/2 的原始值</summary>
        public const long PiOver2 = 102944L;

        /// <summary>1 的原始值</summary>
        public const long ONE = 65536L;

        #endregion

        #region LUT 表（延迟加载）

        /// <summary>Sin/Cos 查找表</summary>
        private static long[]? _sinCosTable;

        /// <summary>Tan 查找表</summary>
        private static long[]? _tanTable;

        /// <summary>Asin 查找表</summary>
        private static long[]? _asinTable;

        /// <summary>Acos 查找表</summary>
        private static long[]? _acosTable;

        /// <summary>Atan 查找表</summary>
        private static long[]? _atanTable;

        /// <summary>Sqrt 查找表</summary>
        private static int[]? _sqrtTable;

        #endregion

        #region 公共访问属性

        /// <summary>LUT 是否已加载</summary>
        public static bool IsLoaded => _sinCosTable != null && _sqrtTable != null;

        /// <summary>LUT 版本</summary>
        public static string Version { get; private set; } = "not loaded";

        /// <summary>Sin/Cos 查找表（自动加载检查）</summary>
        public static long[] SinCosTable => _sinCosTable ?? throw CreateNotLoadedException();

        /// <summary>Tan 查找表</summary>
        public static long[] TanTable => _tanTable ?? throw CreateNotLoadedException();

        /// <summary>Asin 查找表</summary>
        public static long[] AsinTable => _asinTable ?? throw CreateNotLoadedException();

        /// <summary>Acos 查找表</summary>
        public static long[] AcosTable => _acosTable ?? throw CreateNotLoadedException();

        /// <summary>Atan 查找表</summary>
        public static long[] AtanTable => _atanTable ?? throw CreateNotLoadedException();

        /// <summary>Sqrt 查找表</summary>
        public static int[] SqrtTable => _sqrtTable ?? throw CreateNotLoadedException();

        #endregion

        #region 初始化方法

        /// <summary>
        /// 从文件系统加载 LUT
        /// </summary>
        /// <param name="lutDirectory">LUT 文件目录路径</param>
        /// <remarks>
        /// 需要以下文件：
        /// - FPSinCos.bin
        /// - FPTan.bin
        /// - FPAsin.bin
        /// - FPAcos.bin
        /// - FPAtan.bin
        /// - FPSqrt.bin
        /// - version.txt（可选）
        /// </remarks>
        public static void Initialize(string lutDirectory)
        {
            if (IsLoaded) return;

            if (!Directory.Exists(lutDirectory))
                throw new DirectoryNotFoundException($"LUT directory not found: {lutDirectory}");

            // 加载版本文件（如果存在）
            string versionPath = Path.Combine(lutDirectory, "version.txt");
            if (File.Exists(versionPath))
            {
                Version = File.ReadAllText(versionPath).Trim();
            }

            // 加载各 LUT 表
            _sinCosTable = LoadTable<long>(Path.Combine(lutDirectory, "FPSinCos.bin"));
            _tanTable = LoadTable<long>(Path.Combine(lutDirectory, "FPTan.bin"));
            _asinTable = LoadTable<long>(Path.Combine(lutDirectory, "FPAsin.bin"));
            _acosTable = LoadTable<long>(Path.Combine(lutDirectory, "FPAcos.bin"));
            _atanTable = LoadTable<long>(Path.Combine(lutDirectory, "FPAtan.bin"));
            _sqrtTable = LoadTable<int>(Path.Combine(lutDirectory, "FPSqrt.bin"));

            ValidateTables();
        }

        /// <summary>
        /// 从嵌入资源加载 LUT（适用于 Godot 导出版本）
        /// </summary>
        public static void InitializeFromEmbedded()
        {
            if (IsLoaded) return;

            var assembly = typeof(FPLut).Assembly;

            _sinCosTable = LoadFromResource<long>(assembly, "Lattice.LUT.FPSinCos.bin");
            _tanTable = LoadFromResource<long>(assembly, "Lattice.LUT.FPTan.bin");
            _asinTable = LoadFromResource<long>(assembly, "Lattice.LUT.FPAsin.bin");
            _acosTable = LoadFromResource<long>(assembly, "Lattice.LUT.FPAcos.bin");
            _atanTable = LoadFromResource<long>(assembly, "Lattice.LUT.FPAtan.bin");
            _sqrtTable = LoadFromResource<int>(assembly, "Lattice.LUT.FPSqrt.bin");

            Version = "embedded";
            ValidateTables();
        }

        /// <summary>
        /// 使用预编译的内嵌 LUT（当前实现，兼容性）
        /// </summary>
        public static void InitializeBuiltIn()
        {
            if (IsLoaded) return;

            _sinCosTable = FPSinCosLut.SinAccurate;
            _acosTable = FPAcosLut.Table;
            _atanTable = FPAtanLut.Table;
            _sqrtTable = FPSqrtLut.Table;

            // Tan 和 Asin 使用近似计算或从 SinCos 派生
            _tanTable = GenerateTanTable();
            _asinTable = GenerateAsinTable();

            Version = "builtin";
        }

        #endregion

        #region 辅助方法

        private static T[] LoadTable<T>(string path) where T : unmanaged
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"LUT file not found: {path}");

            byte[] bytes = File.ReadAllBytes(path);
            int elementSize = Unsafe.SizeOf<T>();
            int elementCount = bytes.Length / elementSize;

            T[] result = new T[elementCount];
            Buffer.BlockCopy(bytes, 0, result, 0, bytes.Length);

            return result;
        }

        private static T[] LoadFromResource<T>(System.Reflection.Assembly assembly, string resourceName) where T : unmanaged
        {
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
                throw new InvalidOperationException($"Embedded resource not found: {resourceName}");

            byte[] bytes = new byte[stream.Length];
            stream.Read(bytes, 0, bytes.Length);

            int elementSize = Unsafe.SizeOf<T>();
            int elementCount = bytes.Length / elementSize;

            T[] result = new T[elementCount];
            Buffer.BlockCopy(bytes, 0, result, 0, bytes.Length);

            return result;
        }

        private static void ValidateTables()
        {
            if (_sinCosTable?.Length < 4096)
                throw new InvalidDataException($"SinCos LUT too small: {_sinCosTable?.Length}");

            if (_sqrtTable?.Length < 65536)
                throw new InvalidDataException($"Sqrt LUT too small: {_sqrtTable?.Length}");

            if (_acosTable?.Length < 65536)
                throw new InvalidDataException($"Acos LUT too small: {_acosTable?.Length}");
        }

        private static InvalidOperationException CreateNotLoadedException()
        {
            return new InvalidOperationException(
                "LUT not loaded. Call FPLut.Initialize(path) or FPLut.InitializeFromEmbedded() first.");
        }

        private static long[] GenerateTanTable()
        {
            // 简化实现：从 SinCos 表派生
            var table = new long[4096];
            for (int i = 0; i < 4096; i++)
            {
                double angle = i * System.Math.PI * 2 / 4096;
#pragma warning disable RS0030 // LUT 生成是确定性的，允许使用 Math 函数
                table[i] = (long)(System.Math.Tan(angle) * ONE);
#pragma warning restore RS0030
            }
            return table;
        }

        private static long[] GenerateAsinTable()
        {
            // 简化实现：线性近似
            var table = new long[65537];
            for (int i = 0; i <= 65536; i++)
            {
                double x = (i - 32768) / 32768.0; // [-1, 1]
                if (x < -1) x = -1;
                if (x > 1) x = 1;
#pragma warning disable RS0030 // LUT 生成是确定性的，允许使用 Math 函数
                table[i] = (long)(System.Math.Asin(x) * ONE);
#pragma warning restore RS0030
            }
            return table;
        }

        #endregion
    }
}
