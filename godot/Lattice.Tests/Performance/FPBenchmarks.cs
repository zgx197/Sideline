using BenchmarkDotNet.Attributes;
using Lattice.Math;

namespace Lattice.Tests.Performance;

/// <summary>
/// FP 性能基准测试
/// 运行：dotnet run --configuration Release --project Lattice.Tests --filter "*FPBenchmarks*"
/// </summary>
[MemoryDiagnoser]  // 内存分配统计
public class FPBenchmarks
{
    private FP a;
    private FP b;
    private float fa;
    private float fb;

    [GlobalSetup]
    public void Setup()
    {
        a = FP.FromRaw(123456);
        b = FP.FromRaw(654321);
        fa = 1.88427734f;
        fb = 9.98626709f;
    }

    #region 与 float 对比

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

    #region FP 专项测试

    [Benchmark]
    public FP FP_Abs() => FP.Abs(a);

    [Benchmark]
    public FP FP_MinMax() => FP.Min(a, b) + FP.Max(a, b);

    [Benchmark]
    public FP FP_Clamp() => FP.Clamp(a, FP._0, FP._1);

    [Benchmark]
    public FP FP_Lerp() => FP.Lerp(a, b, FP._0_50);

    [Benchmark]
    public FP FP_MultiplyPrecise() => FP.MultiplyPrecise(a, b);

    #endregion

    #region 批量运算（模拟游戏循环）

    [Benchmark]
    public FP ComplexOperationChain()
    {
        // 模拟游戏中常见的复杂计算
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
/// 程序入口（运行 Benchmark）
/// 注意：实际运行时需单独创建 Console 项目
/// </summary>
// public class Program
// {
//     public static void Main(string[] args)
//     {
//         BenchmarkRunner.Run<FPBenchmarks>();
//     }
// }
