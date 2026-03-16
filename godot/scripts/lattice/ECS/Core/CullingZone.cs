// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using Lattice.Math;

namespace Lattice.ECS.Core
{
    /// <summary>
    /// 裁剪区域（预测区域），定义需要同步的物理范围
    /// 对齐 FrameSync 的预测区域概念
    /// </summary>
    public struct CullingZone
    {
        /// <summary>中心位置（2D）</summary>
        public FPVector2 Center2D;

        /// <summary>中心位置（3D）</summary>
        public FPVector3 Center3D;

        /// <summary>半径</summary>
        public FP Radius;

        /// <summary>是否使用 3D 裁剪</summary>
        public bool Is3D;

        /// <summary>创建 2D 裁剪区域</summary>
        public static CullingZone Create2D(FPVector2 center, FP radius)
        {
            return new CullingZone
            {
                Center2D = center,
                Radius = radius,
                Is3D = false
            };
        }

        /// <summary>创建 3D 裁剪区域</summary>
        public static CullingZone Create3D(FPVector3 center, FP radius)
        {
            return new CullingZone
            {
                Center3D = center,
                Radius = radius,
                Is3D = true
            };
        }

        /// <summary>检查 2D 位置是否在区域内</summary>
        public bool Contains(FPVector2 position)
        {
            if (Is3D) return false;

            var diff = position - Center2D;
            return diff.SqrMagnitude <= Radius * Radius;
        }

        /// <summary>检查 3D 位置是否在区域内</summary>
        public bool Contains(FPVector3 position)
        {
            if (!Is3D) return false;

            var diff = position - Center3D;
            return diff.SqrMagnitude <= Radius * Radius;
        }

        /// <summary>全局默认裁剪区域</summary>
        public static CullingZone Default => new()
        {
            Center2D = FPVector2.Zero,
            Radius = (FP)100,  // 默认 100 单位半径
            Is3D = false
        };
    }
}
