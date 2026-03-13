// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using BenchmarkDotNet.Attributes;
using Lattice.Math;

namespace Lattice.Tests.Performance
{
    /// <summary>
    /// 向量运算性能基准测试
    /// </summary>
    [MemoryDiagnoser]
    public class VectorBenchmarks
    {
        private FPVector2 _v2a;
        private FPVector2 _v2b;
        private FPVector3 _v3a;
        private FPVector3 _v3b;
        private System.Numerics.Vector2 _f2a;
        private System.Numerics.Vector2 _f2b;
        private System.Numerics.Vector3 _f3a;
        private System.Numerics.Vector3 _f3b;

        [GlobalSetup]
        public void Setup()
        {
            _v2a = new FPVector2(1, 2);
            _v2b = new FPVector2(3, 4);
            _v3a = new FPVector3(1, 2, 3);
            _v3b = new FPVector3(4, 5, 6);
            _f2a = new System.Numerics.Vector2(1, 2);
            _f2b = new System.Numerics.Vector2(3, 4);
            _f3a = new System.Numerics.Vector3(1, 2, 3);
            _f3b = new System.Numerics.Vector3(4, 5, 6);
        }

        #region FPVector2 vs float

        [Benchmark(Baseline = true)]
        public float Float2_Addition()
        {
            var result = _f2a + _f2b;
            return result.X + result.Y;
        }

        [Benchmark]
        public FP FPVector2_Addition()
        {
            var result = _v2a + _v2b;
            return result.X + result.Y;
        }

        [Benchmark]
        public float Float2_Dot()
        {
            return System.Numerics.Vector2.Dot(_f2a, _f2b);
        }

        [Benchmark]
        public FP FPVector2_Dot()
        {
            return FPVector2.Dot(_v2a, _v2b);
        }

        [Benchmark]
        public float Float2_Length()
        {
            return _f2a.Length();
        }

        [Benchmark]
        public FP FPVector2_Magnitude()
        {
            return _v2a.Magnitude;
        }

        [Benchmark]
        public System.Numerics.Vector2 Float2_Normalize()
        {
            return System.Numerics.Vector2.Normalize(_f2a);
        }

        [Benchmark]
        public FPVector2 FPVector2_Normalize()
        {
            return _v2a.Normalized;
        }

        #endregion

        #region FPVector3 vs float

        [Benchmark(Baseline = true)]
        public float Float3_Addition()
        {
            var result = _f3a + _f3b;
            return result.X + result.Y + result.Z;
        }

        [Benchmark]
        public FP FPVector3_Addition()
        {
            var result = _v3a + _v3b;
            return result.X + result.Y + result.Z;
        }

        [Benchmark]
        public float Float3_Dot()
        {
            return System.Numerics.Vector3.Dot(_f3a, _f3b);
        }

        [Benchmark]
        public FP FPVector3_Dot()
        {
            return FPVector3.Dot(_v3a, _v3b);
        }

        [Benchmark]
        public System.Numerics.Vector3 Float3_Cross()
        {
            return System.Numerics.Vector3.Cross(_f3a, _f3b);
        }

        [Benchmark]
        public FPVector3 FPVector3_Cross()
        {
            return FPVector3.Cross(_v3a, _v3b);
        }

        [Benchmark]
        public float Float3_Length()
        {
            return _f3a.Length();
        }

        [Benchmark]
        public FP FPVector3_Magnitude()
        {
            return _v3a.Magnitude;
        }

        [Benchmark]
        public System.Numerics.Vector3 Float3_Normalize()
        {
            return System.Numerics.Vector3.Normalize(_f3a);
        }

        [Benchmark]
        public FPVector3 FPVector3_Normalize()
        {
            return _v3a.Normalized;
        }

        #endregion

        #region 批量操作性能

        [Benchmark]
        public FP Batch_Normalize2D()
        {
            FP sum = FP._0;
            var v = new FPVector2(3, 4);
            for (int i = 0; i < 1000; i++)
            {
                var n = v.Normalized;
                sum += n.X + n.Y;
                v = new FPVector2(v.X + FP._0_01, v.Y + FP._0_01);
            }
            return sum;
        }

        [Benchmark]
        public FP Batch_Normalize3D()
        {
            FP sum = FP._0;
            var v = new FPVector3(1, 2, 3);
            for (int i = 0; i < 1000; i++)
            {
                var n = v.Normalized;
                sum += n.X + n.Y + n.Z;
                v = new FPVector3(v.X + FP._0_01, v.Y + FP._0_01, v.Z + FP._0_01);
            }
            return sum;
        }

        [Benchmark]
        public FP Batch_Dot2D()
        {
            FP sum = FP._0;
            var a = new FPVector2(1, 2);
            var b = new FPVector2(3, 4);
            for (int i = 0; i < 10000; i++)
            {
                sum += FPVector2.Dot(a, b);
            }
            return sum;
        }

        [Benchmark]
        public FP Batch_Dot3D()
        {
            FP sum = FP._0;
            var a = new FPVector3(1, 2, 3);
            var b = new FPVector3(4, 5, 6);
            for (int i = 0; i < 10000; i++)
            {
                sum += FPVector3.Dot(a, b);
            }
            return sum;
        }

        [Benchmark]
        public FP Batch_Cross3D()
        {
            FP sum = FP._0;
            var a = new FPVector3(1, 0, 0);
            var b = new FPVector3(0, 1, 0);
            for (int i = 0; i < 5000; i++)
            {
                var c = FPVector3.Cross(a, b);
                sum += c.Z;
            }
            return sum;
        }

        #endregion

        #region Swizzle 性能

        [Benchmark]
        public FPVector2 Swizzle_XX_2D()
        {
            FPVector2 sum = FPVector2.Zero;
            var v = new FPVector2(1, 2);
            for (int i = 0; i < 10000; i++)
            {
                sum += v.XX;
            }
            return sum;
        }

        [Benchmark]
        public FPVector3 Swizzle_XYZ_3D()
        {
            FPVector3 sum = FPVector3.Zero;
            var v = new FPVector3(1, 2, 3);
            for (int i = 0; i < 10000; i++)
            {
                sum += v.XYZ;
            }
            return sum;
        }

        [Benchmark]
        public FPVector3 Swizzle_ZYX_3D()
        {
            FPVector3 sum = FPVector3.Zero;
            var v = new FPVector3(1, 2, 3);
            for (int i = 0; i < 10000; i++)
            {
                sum += v.ZYX;
            }
            return sum;
        }

        #endregion

        #region 几何运算性能

        [Benchmark]
        public FP Batch_Angle2D()
        {
            FP sum = FP._0;
            var a = FPVector2.Right;
            for (int i = 0; i < 360; i++)
            {
                var b = FPVector2.Rotate(a, FP.FromRaw(i * FP.ONE));
                sum += FPVector2.Angle(a, b);
            }
            return sum;
        }

        [Benchmark]
        public FP Batch_Distance2D()
        {
            FP sum = FP._0;
            var a = new FPVector2(0, 0);
            for (int i = 0; i < 1000; i++)
            {
                var b = new FPVector2(i, i);
                sum += FPVector2.Distance(a, b);
            }
            return sum;
        }

        [Benchmark]
        public FP Batch_DistanceSquared2D()
        {
            FP sum = FP._0;
            var a = new FPVector2(0, 0);
            for (int i = 0; i < 1000; i++)
            {
                var b = new FPVector2(i, i);
                sum += FPVector2.DistanceSquared(a, b);
            }
            return sum;
        }

        [Benchmark]
        public FP Batch_Reflect2D()
        {
            FP sum = FP._0;
            var dir = new FPVector2(1, -1).Normalized;
            var normal = FPVector2.Up;
            for (int i = 0; i < 1000; i++)
            {
                var reflected = FPVector2.Reflect(dir, normal);
                sum += reflected.Y;
            }
            return sum;
        }

        [Benchmark]
        public FP Batch_Project2D()
        {
            FP sum = FP._0;
            var v = new FPVector2(3, 4);
            var axis = FPVector2.Right;
            for (int i = 0; i < 1000; i++)
            {
                var proj = FPVector2.Project(v, axis);
                sum += proj.X;
            }
            return sum;
        }

        #endregion

        #region 批量数组操作

        [Benchmark]
        public void Batch_NormalizeArray2D()
        {
            var input = new FPVector2[100];
            var output = new FPVector2[100];
            for (int i = 0; i < 100; i++)
            {
                input[i] = new FPVector2(i, i + 1);
            }
            
            FPMath.NormalizeBatch(input, output);
        }

        [Benchmark]
        public void Batch_NormalizeArray3D()
        {
            var input = new FPVector3[100];
            var output = new FPVector3[100];
            for (int i = 0; i < 100; i++)
            {
                input[i] = new FPVector3(i, i + 1, i + 2);
            }
            
            FPMath.NormalizeBatch(input, output);
        }

        [Benchmark]
        public void Batch_DotArray2D()
        {
            var a = new FPVector2[100];
            var b = new FPVector2[100];
            var output = new FP[100];
            for (int i = 0; i < 100; i++)
            {
                a[i] = new FPVector2(i, i + 1);
                b[i] = new FPVector2(i + 2, i + 3);
            }
            
            FPMath.DotBatch(a, b, output);
        }

        [Benchmark]
        public void Batch_LerpArray()
        {
            var a = new FP[100];
            var b = new FP[100];
            var output = new FP[100];
            for (int i = 0; i < 100; i++)
            {
                a[i] = (FP)i;
                b[i] = (FP)(i + 10);
            }
            
            FPMath.LerpBatch(a, b, FP._0_50, output);
        }

        [Benchmark]
        public void Batch_ClampArray()
        {
            var input = new FP[100];
            var output = new FP[100];
            for (int i = 0; i < 100; i++)
            {
                input[i] = (FP)(i - 50);  // -50 to 49
            }
            
            FPMath.ClampBatch(input, FP._0, FP._10, output);
        }

        #endregion

        #region 模拟游戏场景

        [Benchmark]
        public FPVector2 Simulate_Physics2D()
        {
            var position = new FPVector2(0, 0);
            var velocity = new FPVector2(1, 1);
            var acceleration = new FPVector2(0, -FP._0_10);  // 重力
            
            for (int i = 0; i < 1000; i++)
            {
                velocity += acceleration;
                position += velocity;
                
                // 简单的地面碰撞
                if (position.Y < FP._0)
                {
                    position = new FPVector2(position.X, FP._0);
                    velocity = new FPVector2(velocity.X, -velocity.Y * FP._0_75);  // 弹性碰撞
                }
            }
            
            return position;
        }

        [Benchmark]
        public FPVector3 Simulate_Physics3D()
        {
            var position = new FPVector3(0, 0, 0);
            var velocity = new FPVector3(1, 1, 1);
            var acceleration = new FPVector3(0, -FP._0_10, 0);  // 重力
            
            for (int i = 0; i < 1000; i++)
            {
                velocity += acceleration;
                position += velocity;
                
                // 简单的地面碰撞
                if (position.Y < FP._0)
                {
                    position = new FPVector3(position.X, FP._0, position.Z);
                    velocity = new FPVector3(velocity.X, -velocity.Y * FP._0_75, velocity.Z);
                }
            }
            
            return position;
        }

        [Benchmark]
        public FP Simulate_Rotation2D()
        {
            var direction = FPVector2.Right;
            FP angularVelocity = FP._0_01;  // 弧度/帧
            FP totalRotation = FP._0;
            
            for (int i = 0; i < 3600; i++)  // 模拟 10 圈
            {
                direction = FPVector2.Rotate(direction, angularVelocity);
                totalRotation += angularVelocity;
            }
            
            return totalRotation;
        }

        [Benchmark]
        public FP Simulate_ObjectCounting()
        {
            FP total = FP._0;
            
            // 模拟检查 1000 个对象的可见性/距离
            for (int i = 0; i < 1000; i++)
            {
                var objPos = new FPVector3(i % 10, i / 10, 0);
                var playerPos = FPVector3.Zero;
                var distance = FPVector3.Distance(objPos, playerPos);
                
                if (distance < 50)
                {
                    total += FP._1;
                }
            }
            
            return total;
        }

        #endregion
    }
}
