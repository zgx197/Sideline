// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System.Text;

namespace Lattice.Tools
{
    /// <summary>
    /// LUT 生成器核心
    /// </summary>
    public static class LutGeneratorCore
    {
        public const int ONE = 65536;           // Q16.16 的 1.0
        public const int PRECISION_BITS = 16;
        
        // 各种 LUT 的尺寸配置
        public const int SQRT_TABLE_SIZE = 65537;    // [0, 65536]
        public const int ACOS_TABLE_SIZE = 65537;    // [-1, 1] 映射到 [0, 65536]
        public const int ATAN_TABLE_SIZE = 256;      // [0, 1] -> [0, π/4]
        
        // SinCos 双精度策略（参考 FrameSync）
        public const int SINCOS_FAST_SIZE = 1024;    // 快速模式：Cache 友好，适合一般计算
        public const int SINCOS_ACCURATE_SIZE = 4096; // 高精度：3D 旋转、物理模拟
        
        public const int TAN_TABLE_SIZE = 1024;      // [0, 2π)
        
        // FrameSync 风格额外精度
        public const int SQRT_EXTRA_BITS = 6;
        public const long SQRT_ONE_WITH_PRECISION = (long)ONE << SQRT_EXTRA_BITS; // 4194304

        /// <summary>
        /// 生成所有 LUT 文件
        /// </summary>
        public static void GenerateAll(string outputDirectory)
        {
            Directory.CreateDirectory(outputDirectory);
            
            Console.WriteLine("Lattice LUT Generator");
            Console.WriteLine("=====================");
            Console.WriteLine($"Output: {outputDirectory}\n");
            
            GenerateSqrtLut(outputDirectory);
            GenerateAcosLut(outputDirectory);
            GenerateAtanLut(outputDirectory);
            GenerateSinCosLut(outputDirectory);
            
            Console.WriteLine("\nAll LUTs generated successfully!");
        }

        /// <summary>
        /// 生成平方根查找表
        /// </summary>
        public static void GenerateSqrtLut(string outputDirectory)
        {
            string filePath = Path.Combine(outputDirectory, "FPSqrtLut.cs");
            Console.WriteLine("Generating Sqrt LUT...");
            
            var values = new int[SQRT_TABLE_SIZE];
            for (int i = 0; i < SQRT_TABLE_SIZE; i++)
            {
                double value = i / (double)ONE;
                double sqrt = Math.Sqrt(value);
                values[i] = (int)(sqrt * SQRT_ONE_WITH_PRECISION + 0.5);
            }
            
            WriteLutFile(filePath, "FPSqrtLut", "int", values, 
                $"平方根查找表 - 存储值 = sqrt(i / {ONE}.0) * {SQRT_ONE_WITH_PRECISION}",
                $"使用时需右移 {SQRT_EXTRA_BITS} 位得到 Q16.16 格式");
            
            Console.WriteLine($"  Entries: {SQRT_TABLE_SIZE:N0}");
            Console.WriteLine($"  Size: {new FileInfo(filePath).Length / 1024.0:F1} KB");
        }

        /// <summary>
        /// 生成反余弦查找表
        /// </summary>
        public static void GenerateAcosLut(string outputDirectory)
        {
            string filePath = Path.Combine(outputDirectory, "FPAcosLut.cs");
            Console.WriteLine("Generating Acos LUT...");
            
            var values = new long[ACOS_TABLE_SIZE];
            for (int i = 0; i < ACOS_TABLE_SIZE; i++)
            {
                // i ∈ [0, 65536] 映射到 x ∈ [-1, 1]
                double x = (i / 32768.0) - 1.0;
                // 限制在 [-1, 1] 避免浮点误差
                x = Math.Max(-1.0, Math.Min(1.0, x));
                double acos = Math.Acos(x);
                values[i] = (long)(acos * ONE + 0.5);
            }
            
            WriteLutFile(filePath, "FPAcosLut", "long", values,
                $"反余弦查找表 - Acos(x), x ∈ [-1, 1] 映射到索引 [0, {ACOS_TABLE_SIZE - 1}]",
                "输出范围 [0, π]");
            
            Console.WriteLine($"  Entries: {ACOS_TABLE_SIZE:N0}");
            Console.WriteLine($"  Size: {new FileInfo(filePath).Length / 1024.0:F1} KB");
        }

        /// <summary>
        /// 生成反正切查找表
        /// </summary>
        public static void GenerateAtanLut(string outputDirectory)
        {
            string filePath = Path.Combine(outputDirectory, "FPAtanLut.cs");
            Console.WriteLine("Generating Atan LUT...");
            
            var values = new long[ATAN_TABLE_SIZE + 1];
            for (int i = 0; i <= ATAN_TABLE_SIZE; i++)
            {
                // i ∈ [0, ATAN_TABLE_SIZE] 映射到 ratio ∈ [0, 1]
                double ratio = i / (double)ATAN_TABLE_SIZE;
                double atan = Math.Atan(ratio);
                values[i] = (long)(atan * ONE + 0.5);
            }
            
            WriteLutFile(filePath, "FPAtanLut", "long", values,
                $"反正切查找表 - Atan(x), x ∈ [0, 1]",
                $"输出范围 [0, π/4] = [0, {Math.PI / 4 * ONE:F0}]");
            
            Console.WriteLine($"  Entries: {ATAN_TABLE_SIZE + 1:N0}");
            Console.WriteLine($"  Size: {new FileInfo(filePath).Length / 1024.0:F1} KB");
        }

        /// <summary>
        /// 生成正弦/余弦查找表（双精度策略）
        /// </summary>
        public static void GenerateSinCosLut(string outputDirectory)
        {
            string filePath = Path.Combine(outputDirectory, "FPSinCosLut.cs");
            Console.WriteLine("Generating SinCos LUTs (Dual Precision)...");
            
            // 生成两个精度的表
            var (sinFast, cosFast) = GenerateSinCosTables(SINCOS_FAST_SIZE);
            var (sinAccurate, cosAccurate) = GenerateSinCosTables(SINCOS_ACCURATE_SIZE);
            
            // 写入组合文件
            var sb = new StringBuilder();
            sb.AppendLine(GenerateFileHeader("FPSinCosLut"));
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// 正弦/余弦查找表 - 双精度策略（参考 FrameSync）");
            sb.AppendLine("    /// <para>Fast (1024): Cache 友好，适合一般计算</para>");
            sb.AppendLine("    /// <para>Accurate (4096): 高精度，适合 3D 旋转/物理</para>");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    internal static class FPSinCosLut");
            sb.AppendLine("    {");
            
            // Fast 模式常量
            sb.AppendLine("        #region Fast Mode (1024 entries)");
            sb.AppendLine("        public const int FastSize = 1024;");
            sb.AppendLine("        public const int FastIndexBits = 10;  // log2(1024)");
            sb.AppendLine("        public const int FastIndexMask = 0x3FF;");
            sb.AppendLine();
            
            // Fast Sin 表
            sb.AppendLine("        /// <summary>快速正弦查找表（1024 条目，适合一般计算）</summary>");
            WriteArray(sb, "SinFast", "long", sinFast);
            sb.AppendLine();
            
            // Fast Cos 表
            sb.AppendLine("        /// <summary>快速余弦查找表（1024 条目，适合一般计算）</summary>");
            WriteArray(sb, "CosFast", "long", cosFast);
            sb.AppendLine("        #endregion");
            sb.AppendLine();
            
            // Accurate 模式常量
            sb.AppendLine("        #region Accurate Mode (4096 entries)");
            sb.AppendLine("        public const int AccurateSize = 4096;");
            sb.AppendLine("        public const int AccurateIndexBits = 12;  // log2(4096)");
            sb.AppendLine("        public const int AccurateIndexMask = 0xFFF;");
            sb.AppendLine();
            
            // Accurate Sin 表
            sb.AppendLine("        /// <summary>高精度正弦查找表（4096 条目，适合 3D/物理）</summary>");
            WriteArray(sb, "SinAccurate", "long", sinAccurate);
            sb.AppendLine();
            
            // Accurate Cos 表
            sb.AppendLine("        /// <summary>高精度余弦查找表（4096 条目，适合 3D/物理）</summary>");
            WriteArray(sb, "CosAccurate", "long", cosAccurate);
            sb.AppendLine("        #endregion");
            
            sb.AppendLine("    }");
            sb.AppendLine("}");
            
            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
            
            Console.WriteLine($"  Fast: {SINCOS_FAST_SIZE:N0} entries x 2 (Sin+Cos)");
            Console.WriteLine($"  Accurate: {SINCOS_ACCURATE_SIZE:N0} entries x 2 (Sin+Cos)");
            Console.WriteLine($"  Total Size: {new FileInfo(filePath).Length / 1024.0:F1} KB");
        }
        
        /// <summary>
        /// 生成指定大小的 Sin/Cos 表
        /// </summary>
        private static (long[] sin, long[] cos) GenerateSinCosTables(int size)
        {
            var sin = new long[size];
            var cos = new long[size];
            
            for (int i = 0; i < size; i++)
            {
                double angle = (i * 2.0 * Math.PI) / size;
                sin[i] = (long)(Math.Sin(angle) * ONE + 0.5);
                cos[i] = (long)(Math.Cos(angle) * ONE + 0.5);
            }
            
            return (sin, cos);
        }

        #region 辅助方法

        private static void WriteLutFile<T>(string filePath, string className, string typeName, 
            T[] values, string description, string usageNote)
        {
            var sb = new StringBuilder();
            sb.AppendLine(GenerateFileHeader(className));
            sb.AppendLine("    /// <summary>");
            sb.AppendLine($"    /// {description}");
            sb.AppendLine($"    /// <para>{usageNote}</para>");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine($"    internal static class {className}");
            sb.AppendLine("    {");
            sb.AppendLine($"        public const int TableSize = {values.Length};");
            if (className == "FPSqrtLut")
            {
                sb.AppendLine($"        public const int AdditionalPrecisionBits = {SQRT_EXTRA_BITS};");
            }
            sb.AppendLine();
            WriteArray(sb, "Table", typeName, values);
            sb.AppendLine("    }");
            sb.AppendLine("}");
            
            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        private static void WriteArray<T>(StringBuilder sb, string name, string typeName, T[] values)
        {
            sb.AppendLine($"        public static readonly {typeName}[] {name} = new {typeName}[]");
            sb.AppendLine("        {");
            
            const int valuesPerLine = 16;
            for (int i = 0; i < values.Length; i++)
            {
                if (i % valuesPerLine == 0)
                    sb.Append("            ");
                
                sb.Append(values[i]?.ToString() ?? "0");
                
                if (i < values.Length - 1)
                    sb.Append(", ");
                
                if ((i + 1) % valuesPerLine == 0)
                    sb.AppendLine($" // {i - valuesPerLine + 1}..{i}");
            }
            
            if (values.Length % valuesPerLine != 0)
                sb.AppendLine();
            
            sb.AppendLine("        };");
        }

        private static string GenerateFileHeader(string className)
        {
            return $"// Copyright (c) 2026 Sideline Authors. All rights reserved.\n" +
                   $"// Licensed under GPL-3.0.\n" +
                   $"\n" +
                   $"// <auto-generated>\n" +
                   $"// 此文件由 LutGenerator 自动生成，请勿手动修改。\n" +
                   $"// 生成时间: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC\n" +
                   $"// 类名: {className}\n" +
                   $"// </auto-generated>\n" +
                   $"\n" +
                   $"namespace Lattice.Math\n" +
                   $"{{\n";
        }

        #endregion
    }
}
