// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

// 这个文件会被嵌入到使用 Source Generator 的程序集中

namespace Lattice.Generators
{
    /// <summary>
    /// 标记一个向量类型需要自动生成 Swizzle 属性
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
    public sealed class GenerateSwizzleAttribute : System.Attribute
    {
        /// <summary>
        /// 最大 Swizzle 维度（2 或 3）
        /// </summary>
        public int MaxDimension { get; set; } = 3;

        /// <summary>
        /// 是否包含 O（Zero）分量，如 XYO
        /// </summary>
        public bool IncludeZero { get; set; } = true;

        public GenerateSwizzleAttribute()
        {
        }
    }
}
