using System;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Lattice.Math;

namespace Lattice.Benchmarks;

/// <summary>
/// FP 定点数性能基准测试
/// </summary>
[RankColumn]
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, iterationCount: 5, warmupCount: 3)]
public class FPMathBenchmarks
{
    private FP a, b;
    private float fa, fb;

    [GlobalSetup]
    public void Setup()
    {
        a = FP.FromRaw(123456);
        b = FP.FromRaw(654321);
        fa = 1.88427734f;
        fb = 9.98626709f;
    }

    #region 基础运算对比

    [Benchmark(Baseline = true)]
    public float Float_Addition() => fa + fb;

    [Benchmark]
    public FP FP_Addition() => a + b;

    [Benchmark]
    public float Float_Multiplication() => fa * fb;

    [Benchmark]
    public FP FP_Multiplication() => a * b;

    [Benchmark]
    public float Float_Division() => fa / fb;

    [Benchmark]
    public FP FP_Division() => a / b;

    #endregion

    #region FP 专项运算

    [Benchmark]
    public FP FP_Abs() => FP.Abs(a);

    [Benchmark]
    public FP FP_MinMax() => FP.Min(a, b) + FP.Max(a, b);

    [Benchmark]
    public FP FP_Clamp() => FP.Clamp(a, FP._0, FP._1);

    [Benchmark]
    public FP FP_Lerp() => FP.Lerp(a, b, FP._0_50);

    #endregion

    #region 复杂运算链

    [Benchmark]
    public FP ComplexOperation()
    {
        FP result = a;
        for (int i = 0; i < 100; i++)
        {
            result = (result * b + FP.Pi) / FP._2 - FP._1_50;
            result = FP.Clamp(result, FP.UseableMin, FP.UseableMax);
        }
        return result;
    }

    #endregion
}

/// <summary>
/// 批量运算基准（模拟游戏循环）
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, iterationCount: 5, warmupCount: 3)]
public class FPBatchBenchmarks
{
    private const int N = 1000;
    private FP[] values = null!;
    private FP[] results = null!;

    [GlobalSetup]
    public void Setup()
    {
        values = new FP[N];
        results = new FP[N];
        for (int i = 0; i < N; i++)
        {
            values[i] = FP.FromRaw(i * 100);
        }
    }

    [Benchmark]
    public void BatchAddition()
    {
        for (int i = 0; i < N; i++)
        {
            results[i] = values[i] + FP._1;
        }
    }

    [Benchmark]
    public void BatchMultiplication()
    {
        for (int i = 0; i < N; i++)
        {
            results[i] = values[i] * FP._0_10;
        }
    }

    [Benchmark]
    public void BatchLerp()
    {
        for (int i = 0; i < N - 1; i++)
        {
            results[i] = FP.Lerp(values[i], values[i + 1], FP._0_50);
        }
    }
}

/// <summary>
/// 与浮点数对比基准
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, iterationCount: 5, warmupCount: 3)]
public class FPvsFloatBenchmarks
{
    private FP[] fpValues = null!;
    private float[] floatValues = null!;
    private const int N = 10000;

    [GlobalSetup]
    public void Setup()
    {
        fpValues = new FP[N];
        floatValues = new float[N];
        var random = new Random(42);
        for (int i = 0; i < N; i++)
        {
            float f = (float)(random.NextDouble() * 1000);
            floatValues[i] = f;
            fpValues[i] = (int)f;  // 简化为整数
        }
    }

    [Benchmark(Baseline = true)]
    public float FloatPhysicsSimulation()
    {
        float position = 0;
        float velocity = 10.0f;
        float acceleration = 0.1f;
        float damping = 0.99f;

        for (int i = 0; i < N; i++)
        {
            velocity += acceleration;
            velocity *= damping;
            position += velocity;
            if (position > 1000 || position < -1000)
            {
                velocity = -velocity;
            }
        }
        return position;
    }

    [Benchmark]
    public FP FPPhysicsSimulation()
    {
        FP position = FP._0;
        FP velocity = FP._10;
        FP acceleration = FP._0_10;
        FP damping = FP.FromRaw(FP.Raw._1 - FP.Raw._0_01);

        for (int i = 0; i < N; i++)
        {
            velocity += acceleration;
            velocity *= damping;
            position += velocity;
            if (position > FP._1000 || position < -FP._1000)
            {
                velocity = -velocity;
            }
        }
        return position;
    }
}
