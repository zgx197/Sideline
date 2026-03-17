// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;

namespace Lattice.ECS.Core
{
    /// <summary>
    /// 自动生成组件序列化代码的特性
    /// 
    /// 使用示例：
    /// [GenerateComponent]
    /// public struct Position : IComponent {
    ///     public FP X;
    ///     public FP Y;
    /// }
    /// 
    /// Source Generator 会自动生成：
    /// - Serialize 方法
    /// - OnAdded/OnRemoved 委托（可选）
    /// - 注册代码
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
    public sealed class GenerateComponentAttribute : Attribute
    {
        /// <summary>组件标志</summary>
        public ComponentFlags Flags { get; set; } = ComponentFlags.None;

        /// <summary>是否生成 OnAdded 回调</summary>
        public bool GenerateOnAdded { get; set; } = false;

        /// <summary>是否生成 OnRemoved 回调</summary>
        public bool GenerateOnRemoved { get; set; } = false;

        /// <summary>自定义序列化逻辑（如果为空则自动生成）</summary>
        public string? CustomSerializer { get; set; } = null;

        public GenerateComponentAttribute()
        {
        }
    }

    /// <summary>
    /// 标记字段不参与序列化
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
    public sealed class DontSerializeAttribute : Attribute
    {
    }

    /// <summary>
    /// 标记字段使用变长编码
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
    public sealed class VarIntAttribute : Attribute
    {
    }

    /// <summary>
    /// 标记字段使用指定比特数（用于小范围值）
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
    public sealed class BitFieldAttribute : Attribute
    {
        public int Bits { get; }

        public BitFieldAttribute(int bits)
        {
            Bits = bits;
        }
    }
}
