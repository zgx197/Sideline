# Lattice 性能优化指南

> 本文档列出 Lattice 代码中所有可优化的性能点，按优先级排序。
> 
> 基准：与 FrameSync 最佳实践对比

---

## 优化优先级总览

| 优先级 | 优化项 | 预期性能提升 | 实现难度 |
|-------|--------|------------|---------|
| 🔴 **P0** | 内联标记补全 | 10-20% | 简单 |
| 🔴 **P0** | Angle 计算优化 | 30-40% | 中等 |
| 🟡 **P1** | Slerp 算法实现 | 新增功能 | 复杂 |
| 🟡 **P1** | 批量运算 API | 2-3x | 中等 |
| 🟢 **P2** | 内存布局优化 | 5-10% | 中等 |

---

## 🔴 P0 关键优化

### 1. 内联标记补全

**问题**：部分高频调用方法缺少 `AggressiveInlining`

```csharp
// 当前：Angle 没有内联标记
public static FP Angle(FPVector2 a, FPVector2 b)  // ❌ 无内联
{
    FP dot = Dot(a, b);
    // ...
}

// 优化：
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static FP Angle(FPVector2 a, FPVector2 b)  // ✅
```

**需要补全内联的方法**：

| 文件 | 方法 | 调用频率 |
|-----|------|---------|
| FPVector2.cs | `Angle` | 高 |
| FPVector2.cs | `SignedAngle` | 中 |
| FPVector2.cs | `MoveTowards` | 高 |
| FPVector2.cs | `SmoothDamp` | 高 |
| FPVector2.cs | `ClampMagnitude` | 中 |
| FPVector3.cs | `ProjectOnPlane` | 中 |
| FP.cs | `ToString` | 中 |

**预期收益**：减少 10-20% 的调用开销（高频场景）

---

### 2. Angle 计算优化

**问题**：当前实现调用两次 Sqrt

```csharp
// 当前实现 (Angle)
public static FP Angle(FPVector2 a, FPVector2 b)
{
    FP dot = Dot(a, b);
    FP magA = a.SqrMagnitude;
    FP magB = b.SqrMagnitude;
    
    // ❌ 两次 Sqrt！
    FP cos = dot / (FPMath.Sqrt(magA) * FPMath.Sqrt(magB));
    return FP.Acos(cos);
}
```

**优化方案**：合并平方根计算

```csharp
// 优化实现
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static FP Angle(FPVector2 a, FPVector2 b)
{
    FP dot = Dot(a, b);
    FP sqrMagA = a.SqrMagnitude;
    FP sqrMagB = b.SqrMagnitude;
    
    if (sqrMagA.RawValue == 0 || sqrMagB.RawValue == 0) 
        return FP.Zero;
    
    // ✅ 一次 Sqrt：sqrt(|a|² * |b|²) = |a| * |b|
    FP cos = dot / FPMath.Sqrt(sqrMagA * sqrMagB);
    cos = FPMath.Clamp(cos, -FP._1, FP._1);
    return FP.Acos(cos);
}
```

**预期收益**：减少 30-40% 计算时间（减少一次 Sqrt）

---

### 3. SqrMagnitude 溢出检查优化

**问题**：DEBUG 模式有溢出检查，RELEASE 模式也可以保留安全检查

```csharp
// 当前：每次乘法后都加 32768（四舍五入）
public readonly FP SqrMagnitude
{
    get
    {
        long x2 = (X.RawValue * X.RawValue + 32768) >> 16;  // 加法是额外开销
        long y2 = (Y.RawValue * Y.RawValue + 32768) >> 16;
        return new FP(x2 + y2);
    }
}
```

**优化方案**：提供快速版本（截断）和精确版本（四舍五入）

```csharp
// 快速版本：截断（向零取整）
public readonly FP SqrMagnitudeFast
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    get
    {
        long x2 = (X.RawValue * X.RawValue) >> 16;
        long y2 = (Y.RawValue * Y.RawValue) >> 16;
        return new FP(x2 + y2);
    }
}

// 精确版本：四舍五入（默认）
public readonly FP SqrMagnitude
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    get
    {
        long x2 = (X.RawValue * X.RawValue + 32768) >> 16;
        long y2 = (Y.RawValue * Y.RawValue + 32768) >> 16;
        return new FP(x2 + y2);
    }
}
```

**预期收益**：Fast 版本减少 2 个加法操作，约 5-8% 提升

---

## 🟡 P1 重要优化

### 4. Lerp 无分支优化

**问题**：Clamp01 有分支判断

```csharp
// 当前：有分支
public static FP Lerp(FP a, FP b, FP t)
{
    t = Clamp01(t);  // ❌ 两次比较分支
    return a + (b - a) * t;
}
```

**优化方案**：使用位运算消除分支（FrameSync 风格）

```csharp
// 优化：无分支版本
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static FP LerpFast(FP a, FP b, FP t)
{
    // 使用位运算限制 t 在 [0, 1]
    // 如果 t < 0, raw = 0; 如果 t > 1, raw = ONE
    long raw = t.RawValue;
    raw = raw < 0 ? 0 : (raw > FP.ONE ? FP.ONE : raw);
    return new FP(a.RawValue + ((b.RawValue - a.RawValue) * raw + 32768 >> 16));
}
```

---

### 5. 批量运算 API

**问题**：逐个计算向量的开销大

```csharp
// 当前：逐个 Normalize（假设 1000 个向量）
foreach (var v in vectors)
{
    result[i] = v.Normalized;  // 每次都有函数调用开销
}
```

**优化方案**：提供批量运算 API（SIMD 友好）

```csharp
// 批量归一化
public static void NormalizeBatch(
    ReadOnlySpan<FPVector3> input, 
    Span<FPVector3> output)
{
    for (int i = 0; i < input.Length; i++)
    {
        output[i] = Normalize(input[i]);  // 可以内联展开
    }
}

// 批量点积（Cache 友好）
public static void DotBatch(
    ReadOnlySpan<FPVector3> a,
    ReadOnlySpan<FPVector3> b,
    Span<FP> output)
{
    for (int i = 0; i < a.Length; i++)
    {
        output[i] = Dot(a[i], b[i]);
    }
}
```

**预期收益**：批量处理提升 Cache 命中率，2-3x 性能提升

---

### 6. FP 与 long 的运算符

**问题**：目前只有 `int` 的混合运算

```csharp
// 当前：只有 int
public static FP operator *(FP a, int b) => new(a.RawValue * b);

// 缺失：long 运算（int 溢出风险）
// public static FP operator *(FP a, long b);
```

**优化方案**：添加 long 运算符（避免 int 溢出）

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static FP operator *(FP a, long b) => new(a.RawValue * b);

[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static FP operator /(FP a, long b) => new(a.RawValue / b);
```

---

## 🟢 P2 进阶优化

### 7. Slerp (球面插值) 优化实现

**问题**：当前只有 Lerp，3D 旋转需要 Slerp

```csharp
// FrameSync 风格的快速 Slerp（近似）
public static FPQuaternion SlerpFast(FPQuaternion a, FPQuaternion b, FP t)
{
    FP dot = Dot(a, b);
    
    // 如果点积为负，取反一个四元数保证最短路径
    if (dot.RawValue < 0)
    {
        b = new FPQuaternion(-b.X, -b.Y, -b.Z, -b.W);
        dot = -dot;
    }
    
    // 使用 Lerp 近似（点积接近 1 时）
    if (dot.RawValue > 0.9995 * FP.ONE)
    {
        return Normalize(a + (b - a) * t);
    }
    
    // 完整 Slerp
    FP theta = FP.Acos(dot);
    FP sinTheta = FP.Sin(theta);
    FP w1 = FP.Sin((FP._1 - t) * theta) / sinTheta;
    FP w2 = FP.Sin(t * theta) / sinTheta;
    
    return a * w1 + b * w2;
}
```

---

### 8. 内存对齐与 StructLayout

**问题**：当前没有显式内存布局

```csharp
// 当前：默认布局
public readonly struct FPVector3 { ... }
```

**优化方案**：显式布局优化（FrameSync 风格）

```csharp
[StructLayout(LayoutKind.Sequential, Size = 24)]  // 3 * 8 bytes
public readonly struct FPVector3
{
    // 确保内存连续，利于 SIMD
}
```

---

### 9. 查找表加载优化

**问题**：LUT 静态初始化可能影响启动时间

```csharp
// 当前：静态初始化
private static readonly long[] SinTable = InitSinTable();
```

**优化方案**：延迟加载 + 预加载选项

```csharp
// 延迟加载（首次使用时初始化）
private static long[]? _sinTable;
public static long[] SinTable => _sinTable ??= InitSinTable();

// 或者：显式预加载 API
public static void Warmup()
{
    _ = SinTable;  // 触发加载
    _ = CosTable;
}
```

---

### 10. 字符串转换优化

**问题**：ToString 使用整数算法但仍有除法

```csharp
// 当前：逐位除法
public string ToStringInternal(int decimalPlaces)
{
    long scaledFrac = (fracPart * Pow10(decimalPlaces)) / ONE;  // 除法
    // ...
}
```

**优化方案**：预计算 10 的幂次表

```csharp
// 预计算表（编译时常量）
private static readonly long[] Pow10Table = new long[]
{
    1, 10, 100, 1000, 10000, 
    100000, 1000000, 10000000, 100000000, 1000000000
};

// 使用乘法代替除法（如果可能）
// 或者：查表获取除数
```

---

## 优化检查清单

### 立即执行（本周）
- [ ] 为所有高频方法添加 `AggressiveInlining`
- [ ] 优化 `Angle` 计算（减少一次 Sqrt）
- [ ] 添加 `SqrMagnitudeFast` 版本

### 短期执行（本月）
- [ ] 实现 `SlerpFast`
- [ ] 添加批量运算 API
- [ ] 添加 `FP * long` 运算符

### 长期执行（季度）
- [ ] 内存布局优化
- [ ] LUT 延迟加载
- [ ] 性能基准测试框架

---

## 性能测试建议

```csharp
// 使用 BenchmarkDotNet 验证优化效果
[MemoryDiagnoser]
public class FPMathBenchmarks
{
    [Benchmark(Baseline = true)]
    public FP Angle_Original() => FPVector2.Angle(v1, v2);
    
    [Benchmark]
    public FP Angle_Optimized() => FPVector2.AngleFast(v1, v2);
    
    [Benchmark]
    public FP Sqrt() => FPMath.Sqrt(FP._2);
    
    [Benchmark]
    public FP InvSqrt() => FPMath.InvSqrt(FP._2);
}
```

---

*文档版本：1.0*  
*更新日期：2026-03-13*
