// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Lattice.Math;

namespace Lattice.Tests.Performance
{
    /// <summary>
    /// FP 性能基准测试
    /// 运行：dotnet run --configuration Release --project Lattice.Tests --filter "*FPBenchmarks*"
    /// </summary>
    [MemoryDiagnoser]
    [SimpleJob(RuntimeMoniker.Net80, launchCount: 1, warmupCount: 3, iterationCount: 5)]
    public class FPBenchmarks
    {
        private FP _a;
        private FP _b;
        private float _fa;
        private float _fb;
        private FPVector2 _v2a;
        private FPVector2 _v2b;
        private FPVector3 _v3a;
        private FPVector3 _v3b;

        [GlobalSetup]
        public void Setup()
        {
            FPLut.InitializeBuiltIn();
            
            _a = FP.FromRaw(123456);
            _b = FP.FromRaw(654321);
            _fa = 1.88427734f;
            _fb = 9.98626709f;
            _v2a = new FPVector2(1, 2);
            _v2b = new FPVector2(3, 4);
            _v3a = new FPVector3(1, 2, 3);
            _v3b = new FPVector3(4, 5, 6);
        }

        #region FP vs float 基础运算

        [Benchmark(Baseline = true, Description = "float +")]
        public float Float_Addition() => _fa + _fb;

        [Benchmark(Description = "FP +")]
        public FP FP_Addition() => _a + _b;

        [Benchmark(Description = "float *")]
        public float Float_Multiplication() => _fa * _fb;

        [Benchmark(Description = "FP *")]
        public FP FP_Multiplication() => _a * _b;

        [Benchmark(Description = "FP MultiplyFast")]
        public FP FP_MultiplyFast() => FP.MultiplyFast(_a, _b);

        [Benchmark(Description = "float /")]
        public float Float_Division() => _fa / _fb;

        [Benchmark(Description = "FP /")]
        public FP FP_Division() => _a / _b;

        #endregion

        #region 数学函数

        [Benchmark(Description = "MathF.Sin")]
        public float Float_Sin() => MathF.Sin(_fa);

        [Benchmark(Description = "FP.Sin")]
        public FP FP_Sin() => FP.Sin(_a);

        [Benchmark(Description = "MathF.Cos")]
        public float Float_Cos() => MathF.Cos(_fa);

        [Benchmark(Description = "FP.Cos")]
        public FP FP_Cos() => FP.Cos(_a);

        [Benchmark(Description = "MathF.Sqrt")]
        public float Float_Sqrt() => MathF.Sqrt(_fa);

        [Benchmark(Description = "FPMath.Sqrt")]
        public FP FP_Sqrt() => FPMath.Sqrt(_a);

        [Benchmark(Description = "MathF.Atan2")]
        public float Float_Atan2() => MathF.Atan2(_fa, _fb);

        [Benchmark(Description = "FP.Atan2")]
        public FP FP_Atan2() => FP.Atan2(_a, _b);

        #endregion

        #region 向量运算

        [Benchmark(Description = "FPVector2 +")]
        public FPVector2 FPVector2_Add() => _v2a + _v2b;

        [Benchmark(Description = "FPVector2 Dot")]
        public FP FPVector2_Dot() => FPVector2.Dot(_v2a, _v2b);

        [Benchmark(Description = "FPVector2 Normalize")]
        public FPVector2 FPVector2_Normalize() => _v2a.Normalized;

        [Benchmark(Description = "FPVector3 +")]
        public FPVector3 FPVector3_Add() => _v3a + _v3b;

        [Benchmark(Description = "FPVector3 Dot")]
        public FP FPVector3_Dot() => FPVector3.Dot(_v3a, _v3b);

        [Benchmark(Description = "FPVector3 Cross")]
        public FPVector3 FPVector3_Cross() => FPVector3.Cross(_v3a, _v3b);

        [Benchmark(Description = "FPVector3 Normalize")]
        public FPVector3 FPVector3_Normalize() => _v3a.Normalized;

        #endregion

        #region 批量操作

        [Benchmark(Description = "Batch Normalize 2D x100")]
        public void Batch_Normalize2D()
        {
            var input = new FPVector2[100];
            var output = new FPVector2[100];
            for (int i = 0; i < 100; i++)
                input[i] = new FPVector2(i, i + 1);
            
            FPMath.NormalizeBatch(input, output);
        }

        [Benchmark(Description = "Batch Normalize 3D x100")]
        public void Batch_Normalize3D()
        {
            var input = new FPVector3[100];
            var output = new FPVector3[100];
            for (int i = 0; i < 100; i++)
                input[i] = new FPVector3(i, i + 1, i + 2);
            
            FPMath.NormalizeBatch(input, output);
        }

        #endregion

        #region 模拟场景

        [Benchmark(Description = "Physics 2D x1000")]
        public FPVector2 Simulate_Physics2D()
        {
            var pos = new FPVector2(0, 0);
            var vel = new FPVector2(1, 1);
            var acc = new FPVector2(0, -FP.Raw._0_10);
            
            for (int i = 0; i < 1000; i++)
            {
                vel += acc;
                pos += vel;
                
                if (pos.Y < FP._0)
                {
                    pos = new FPVector2(pos.X, FP._0);
                    vel = new FPVector2(vel.X, -vel.Y * FP.Raw._0_75);
                }
            }
            
            return pos;
        }

        [Benchmark(Description = "Rotation 2D x360")]
        public FP Simulate_Rotation2D()
        {
            var dir = FPVector2.Right;
            FP sum = FP._0;
            
            for (int i = 0; i < 360; i++)
            {
                dir = FPVector2.Rotate(dir, FP.FromRaw(FP.Raw._Deg2Rad));
                sum += dir.X;
            }
            
            return sum;
        }

        #endregion
    }
}
