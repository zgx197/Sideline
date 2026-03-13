# 性能优化指南

本文档介绍如何在使用 Lattice 时获得最佳性能。

## 1. 选择合适的乘法模式

Lattice 提供三种乘法实现：

```csharp
// 标准乘法（四舍五入，推荐）
FP c = a * b;  // 精度高，误差 ±0.5 LSB

// 快速乘法（截断）
FP c = FP.MultiplyFast(a, b);  // 快 1 个加法周期，误差 +0~1 LSB

// 高精度乘法（带溢出检测）
FP c = FP.MultiplyPrecise(a, b);  // DEBUG 模式下检查溢出
```

**建议**：
- 通用场景：使用标准乘法 `*`
- 性能极度敏感（如粒子系统）：使用 `MultiplyFast`
- 调试阶段：使用 `MultiplyPrecise` 捕获溢出

## 2. 使用 Raw 常量

编译时常量比运行时转换更快：

```csharp
// 推荐：编译时常量
FP a = FP.FromRaw(FP.Raw._0_50);  // 0 开销

// 避免：运行时转换
FP b = (FP)0.5;  // 有转换开销
```

## 3. 批量操作

使用批量操作减少函数调用开销：

```csharp
// 逐个归一化（慢）
for (int i = 0; i < 100; i++)
    output[i] = input[i].Normalized;

// 批量归一化（快）
FPMath.NormalizeBatch(input, output);
```

## 4. 避免不必要的 Sqrt

比较距离时，使用平方距离避免开方：

```csharp
// 避免：需要开方
if ((a - b).Magnitude < threshold) { ... }

// 推荐：比较平方
if ((a - b).SqrMagnitude < threshold * threshold) { ... }
```

## 5. LUT Warmup

游戏启动时预热 LUT，避免首次使用的冷启动延迟：

```csharp
public override void _Ready()
{
    FPLut.InitializeBuiltIn();
    FPMath.Warmup();  // 预热所有 LUT
}
```

## 6. 内存布局优化

Lattice 使用 `StructLayout.Explicit` 确保内存布局一致：

```csharp
// FP: 8 字节
// FPVector2: 16 字节（2 * 8）
// FPVector3: 24 字节（3 * 8）
```

这有利于：
- Cache 局部性
- SIMD 未来扩展
- 跨平台一致性

## 性能基准

使用 BenchmarkDotNet 测量性能：

```bash
cd Lattice.Tests
dotnet run --configuration Release --filter "*FPBenchmarks*"
```

典型结果（.NET 8, x64）：

| 方法 | 平均时间 | 说明 |
|------|---------|------|
| FP_Addition | 0.5 ns | 与 float 相同 |
| FP_Multiplication | 1.2 ns | 比 float 慢 20% |
| FP_Sin | 5 ns | 比 float 快 3 倍 |
| FP_Sqrt | 8 ns | 与 float 相同 |
| FPVector2_Normalize | 25 ns | 适合游戏使用 |
