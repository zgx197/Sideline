# 确定性编程技术指南

> 本文档总结 Lattice 定点数库实现中的关键技术点，供团队学习参考。
> 
> 关联代码：`godot/scripts/lattice/`

---

## 目录

1. [LUT（查找表）优化技术](#1-lut查找表优化技术)
2. [编译时 API 限制（BannedSymbols）](#2-编译时-api-限制bannedsymbols)
3. [定点数数学运算技巧](#3-定点数数学运算技巧)
4. [跨平台确定性保障](#4-跨平台确定性保障)

---

## 1. LUT（查找表）优化技术

### 1.1 核心思想

**空间换时间**：将昂贵的计算（如平方根、三角函数）预先计算并存储为数组，运行时直接查表。

```
传统方式：运行时计算 sqrt(x) → 需要迭代/浮点运算 → 慢且不确定
LUT 方式：预先计算 sqrt(i) 存入数组 → 运行时直接取 table[i] → 快且确定
```

### 1.2 FrameSync 风格 LUT 设计

#### 1.2.1 平方根 LUT 结构

```csharp
// FPSqrtLut.cs 结构
internal static class FPSqrtLut
{
    // 额外精度位数（FrameSync 使用 6 位）
    public const int AdditionalPrecisionBits = 6;
    
    // 查找表大小：65537 条目覆盖 [0, 1.0]
    public const int TableSize = 65537;
    
    // 存储值 = sqrt(i / 65536.0) * 4194304
    // 使用时需右移 6 位得到 Q16.16 格式
    public static readonly int[] Table = new int[] { ... };
}
```

**为什么需要额外精度？**

```
问题：sqrt(0.5) ≈ 0.707，用 Q16.16 表示为 0xB504 (46340)
      但 0.707 * 65536 = 46340.7 → 取整后精度损失

解决：存储时左移 6 位，使用时右移 6 位
      存储：0.707 * 65536 * 64 = 2,965,888 (整数)
      使用：2,965,888 >> 6 = 46,342 (更精确)
```

#### 1.2.2 大数处理：指数分解

对于超过 1.0 的数，使用**尾数+指数分解**：

```csharp
public static long SqrtRaw(long x)
{
    // 小值：直接查表
    if (x <= 65536L)
    {
        return FPSqrtLut.Table[x] >> 6;
    }
    
    // 大值：分解为 x = mantissa * 2^exponent
    // sqrt(x) = sqrt(mantissa) * 2^(exponent/2)
    
    // 1. 计算 log2（找最高有效位）
    int log2 = 0;
    if ((raw >> 32) != 0L) { raw >>= 32; log2 += 32; }
    if ((raw >> 16) != 0L) { raw >>= 16; log2 += 16; }
    // ... 逐级二分查找最高位
    
    // 2. 计算指数偏移，使值落在查表范围内
    int exponent = log2 - 16 + 2;
    
    // 3. 查表获取尾数的平方根
    int mantissaSqrt = FPSqrtLut.Table[x >> exponent];
    
    // 4. 还原结果：sqrt(x) = sqrt(mantissa) * 2^(exponent/2)
    long result = (long)mantissaSqrt << (exponent >> 1);
    return result >> 6;  // 去除额外精度
}
```

**算法复杂度对比**：

| 方法 | 时间复杂度 | 空间复杂度 | 确定性 |
|------|-----------|-----------|--------|
| 浮点 Math.Sqrt | O(1) | O(1) | ❌ 平台差异 |
| 牛顿迭代 | O(log n) | O(1) | ✅ 但慢 |
| LUT 查表 | O(1) | O(65537) | ✅ 快且确定 |

### 1.3 LUT 生成策略

#### 1.3.1 预生成 vs 运行时生成

| 策略 | 优点 | 缺点 | 适用场景 |
|------|------|------|----------|
| 运行时生成 | 文件小，灵活 | 启动慢，首次加载卡 | 小型 LUT (<1KB) |
| 预生成 | 启动快，确定性 | 文件大，需提交 Git | 大型 LUT (>100KB) |

#### 1.3.2 我们的混合策略

```
开发时：运行 LutGenerator 生成 LUT 文件
提交时：将 LUT 文件提交到 Git
构建时：直接使用预生成文件（无需 dotnet run）
更新时：修改生成器代码 → 重新运行生成器 → 提交新 LUT
```

### 1.4 LUT 实践技巧

#### 技巧 1：线性插值提高精度

```csharp
// 对于非整数索引，使用线性插值
private static FP AtanTableLookup(FP x)
{
    // 映射到表索引（带小数部分）
    long idxLong = (r * ATAN_TABLE_SIZE) / ONE;
    int idx = (int)idxLong;
    
    // 获取相邻两个值
    long lower = FPAtanLut.Table[idx];
    long upper = FPAtanLut.Table[idx + 1];
    
    // 计算小数部分权重
    long frac = (r * ATAN_TABLE_SIZE) % ONE;
    
    // 线性插值：result = lower + (upper - lower) * frac
    long interpolated = lower + ((upper - lower) * frac) / ONE;
    
    return new FP(interpolated);
}
```

#### 技巧 2：区间映射减少表大小

```csharp
// Acos 只定义在 [-1, 1]，映射到 [0, 65536] 查表
int index = (int)((clamped + FP.ONE) * 32768 >> 16);

// 相比直接存储 [-65536, 65536] 范围，节省一半空间
```

---

## 2. 编译时 API 限制（BannedSymbols）

### 2.1 问题背景

**帧同步游戏的最大敌人**：非确定性代码。

```csharp
// 开发者不小心写了：
float damage = baseDamage * multiplier;  // ❌ 不同平台结果不同

// 应该使用：
FP damage = baseFP * multiplierFP;       // ✅ 所有平台结果相同
```

### 2.2 BannedApiAnalyzers 原理

```
编译流程：
C# 代码 → Roslyn 编译器 → Analyzer 检查 → 发现禁用 API → 报告警告/错误
```

### 2.3 配置详解

#### 2.3.1 BannedSymbols.txt 格式

```text
# 格式：<文档ID>; <错误信息>

# 类型（Type）
T:System.Single; 禁止使用 float 类型，请使用 FP 替代
T:System.Double; 禁止使用 double 类型，请使用 FP 替代
T:System.MathF; 禁止使用 MathF，请使用 FPMath 或 FP

# 方法（Method）- 完整签名
M:System.Math.Abs(System.Double); 使用 FPMath.Abs
M:System.Math.Sqrt(System.Double); 使用 FPMath.Sqrt
M:System.Math.Sin(System.Double); 使用 FP.Sin
M:System.Math.Cos(System.Double); 使用 FP.Cos

# 属性/字段（Property/Field）
F:System.Math.PI; 使用 FP.Pi
P:System.Math.PI; 使用 FP.Pi
```

#### 2.3.2 文档 ID 格式

| 前缀 | 含义 | 示例 |
|------|------|------|
| `T:` | 类型 | `T:System.Single` |
| `M:` | 方法 | `M:System.Math.Sqrt(System.Double)` |
| `P:` | 属性 | `P:System.String.Length` |
| `F:` | 字段 | `F:System.Console.Out` |
| `E:` | 事件 | `E:System.AppDomain.AssemblyLoad` |

### 2.4 项目集成

#### 2.4.1 NuGet 包引用

```xml
<!-- Lattice.csproj -->
<ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.BannedApiAnalyzers" Version="3.3.4">
        <PrivateAssets>all</PrivateAssets>
        <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <AdditionalFiles Include="BannedSymbols.txt" />
</ItemGroup>
```

**PrivateAssets=all 的作用**：
- 确保这个 Analyzer 不会传递给依赖 Lattice 的项目
- 只有 Lattice 自身编译时会检查

#### 2.4.2 编译时效果

```csharp
// 代码中使用了 float
public FP Calculate(float input)  // ❌ 编译警告 RS0030
{
    return FP.FromFloat_UNSAFE(input);
}
```

编译器输出：
```
warning RS0030: The symbol 'float' is banned in this project: 
禁止使用 float 类型，请使用 FP 替代
```

### 2.5 例外处理

#### 2.5.1 局部禁用检查

```csharp
// 在 UNSAFE 方法中允许使用 float
#pragma warning disable RS0030
public static FP FromFloat_UNSAFE(float f) => new((long)(f * ONE));
#pragma warning restore RS0030
```

#### 2.5.2 DEBUG 条件编译

```csharp
#if DEBUG
    [Obsolete("只能在编辑器使用", true)]
#endif
public static FP FromFloat_UNSAFE(float f) => ...
```

### 2.6 进阶：自定义 Roslyn Analyzer

如果 BannedSymbols 不够用，可以编写自定义 Analyzer：

```csharp
// 检测非法的浮点运算
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class FloatOperationAnalyzer : DiagnosticAnalyzer
{
    public override void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        // 检测 float + float 表达式
        // 检测 (float)int 转换
        // ...
    }
}
```

---

## 3. 定点数数学运算技巧

### 3.1 Q16.16 格式详解

```
Q16.16 表示：16 位整数部分 + 16 位小数部分

64-bit long: [63...........................0]
              [符号|整数部分(16位)|小数部分(16位)]
              
实际使用：
- 小数部分：低 16 位 (bit 0-15)
- 整数部分：bit 16-47（共 32 位，实际只用一部分）
- 符号位：bit 63
```

### 3.2 基本运算实现

#### 3.2.1 加减法（直接运算）

```csharp
public static FP operator +(FP a, FP b) => new(a.RawValue + b.RawValue);
public static FP operator -(FP a, FP b) => new(a.RawValue - b.RawValue);
```

**为什么不需要调整？**
- 两个 Q16.16 数相加，结果仍是 Q16.16
- 例：1.0 (65536) + 2.0 (131072) = 3.0 (196608) ✓

#### 3.2.2 乘法（需要移位）

```csharp
public static FP operator *(FP a, FP b)
{
    // a * b = (a.Raw / 65536) * (b.Raw / 65536) = (a.Raw * b.Raw) / 65536^2
    // 结果需要右移 16 位回到 Q16.16
    return new FP((a.RawValue * b.RawValue) >> FRACTIONAL_BITS);
}
```

**溢出风险**：
```csharp
// long.MaxValue ≈ 9.2e18
// 两个 FP 相乘：raw1 * raw2 最大可达 (3e9)^2 = 9e18（接近上限）
public static readonly FP UseableMax = new(3037000499L); // sqrt(long.MaxValue)
```

#### 3.2.3 除法（需要预移位）

```csharp
public static FP operator /(FP a, FP b)
{
    // a / b = (a.Raw / 65536) / (b.Raw / 65536) = a.Raw / b.Raw
    // 但这样会丢失小数精度
    // 
    // 解决：先将 a 左移 16 位
    // (a.Raw << 16) / b.Raw = (a.Raw / b.Raw) * 65536 ✓
    
    return new FP((a.RawValue << FRACTIONAL_BITS) / b.RawValue);
}
```

**溢出检查**：
```csharp
public static FP operator /(FP a, FP b)
{
    if (b.RawValue == 0) throw new DivideByZeroException();
    
    // 检查左移是否会溢出
    if (a.RawValue > (long.MaxValue >> FRACTIONAL_BITS) ||
        a.RawValue < (long.MinValue >> FRACTIONAL_BITS))
    {
        // 大数除法：先除后移
        return new FP((a.RawValue / b.RawValue) << FRACTIONAL_BITS);
    }
    
    // 正常情况：先移后除（更精确）
    return new FP((a.RawValue << FRACTIONAL_BITS) / b.RawValue);
}
```

### 3.3 四舍五入技巧

```csharp
// 乘法时添加 0.5 实现四舍五入
(a * b + 32768) >> 16  // 32768 = 0.5 in Q16.16

// 数学原理：
// x.5 的二进制表示：...0.1
// x.5 + 0.5 = ...1.0 → 右移后进位
```

### 3.4 快速位运算

```csharp
// 取整（向零取整）
public static FP Truncate(FP value)
{
    // 清除低 16 位小数部分
    return new FP(value.RawValue & ~65535L);  // ~0xFFFF = 0xFFFFFFFFFFFF0000
}

// 向下取整
public static FP Floor(FP value)
{
    // 正数：清除小数部分
    // 负数：清除小数部分后减 1（如果原本有小数）
    if (value.RawValue >= 0)
        return new FP(value.RawValue & -65536L);
    else
        return new FP(((value.RawValue + 1) & -65536L) - 65536);
}

// 判断奇偶（用于向偶数取整）
public static FP Round(FP value)
{
    long fractional = value.RawValue & 0xFFFF;
    if (fractional < 32768) return Floor(value);
    if (fractional > 32768) return Ceiling(value);
    // 正好是 0.5，向偶数取整
    return (value.RawValue & 0x10000) != 0 ? Ceiling(value) : Floor(value);
}
```

---

## 4. 跨平台确定性保障

### 4.1 确定性威胁清单

| 威胁 | 原因 | 解决方案 |
|------|------|----------|
| 浮点运算 | IEEE 754 允许精度差异 | 使用定点数 |
| 随机数 | 不同平台算法不同 | 使用确定性 RNG（如 xorshift） |
| 字典遍历 | 哈希算法平台差异 | 使用 SortedDictionary 或排序后遍历 |
| 多线程 | 执行顺序不确定 | 使用单线程或确定性调度 |
| 序列化 | 浮点文本表示差异 | 使用二进制或定点数存储 |

### 4.2 确定性验证测试

```csharp
[Fact]
public void Sqrt_ShouldBeDeterministic()
{
    // 相同输入必须产生相同输出
    var fp = FP.FromRaw(12345678);
    var sqrt1 = FPMath.Sqrt(fp);
    var sqrt2 = FPMath.Sqrt(fp);
    
    Assert.Equal(sqrt1.RawValue, sqrt2.RawValue);
}

[Fact]
public void Sqrt_ShouldMatchExpectedValue()
{
    // 验证与期望值一致（预计算的高精度值）
    var fp = FP._2;  // 2.0
    var sqrt = FPMath.Sqrt(fp);
    
    // √2 ≈ 1.4142135623730951
    // Q16.16: 1.4141998291015625 (Raw = 92681)
    Assert.Equal(92681, sqrt.RawValue);
}
```

### 4.3 持续集成检查

```yaml
# CI 配置示例
- name: Run Determinism Tests
  run: dotnet test --filter "FullyQualifiedName~Determinism"
  
- name: Check for Float Usage
  run: |
    # 编译项目，确保没有 RS0030 警告
    dotnet build -warnaserror 2>&1 | grep -i "RS0030" && exit 1 || exit 0
```

---

## 5. 参考资源

### 5.1 FrameSync 参考

- **Photon Quantum**: 商业帧同步引擎，Q48.16 定点数
- **FrameSyncEngine**: GitHub 开源实现，查表算法

### 5.2 相关论文/文章

- "What Every Computer Scientist Should Know About Floating-Point Arithmetic"
- "Fixed-Point Arithmetic: An Introduction" by Randy Yates

### 5.3 工具推荐

- **IEEE 754 Converter**: 在线浮点/十六进制转换
- **Desmos**: 函数可视化，用于验证 LUT 精度

---

## 附录：快速参考表

### Q16.16 常用值

| 值 | 十进制 | Raw (long) | 十六进制 |
|---|--------|-----------|----------|
| 0 | 0 | 0 | 0x0 |
| 0.5 | 0.5 | 32768 | 0x8000 |
| 1 | 1.0 | 65536 | 0x10000 |
| 2 | 2.0 | 131072 | 0x20000 |
| π | 3.14159... | 205887 | 0x3243F |
| 2π | 6.28318... | 411775 | 0x6487F |

### 运算公式速查

```csharp
// 乘法
(a * b) >> 16

// 除法  
(a << 16) / b

// 乘除混合（防止溢出）
((a * b) / c) >> 16  // 错误，会溢出
(a / c) * (b >> 8) >> 8  // 更好，但损失精度
```

---

## 5. FrameSync 免除法归一化优化

### 5.1 问题：传统归一化的性能瓶颈

```csharp
// 传统方法：需要两次除法
public FPVector2 Normalized
{
    get {
        FP mag = Magnitude;           // 第1次：Sqrt (查表)
        return this / mag;            // 第2次：除法 (慢)
    }
}
```

### 5.2 FrameSync 解决方案：倒数乘法

```csharp
// 优化方法：使用倒数乘法避免除法
public static FPVector2 Normalize(FPVector2 value)
{
    ulong sqrmag = (ulong)(x*x + y*y);
    if (sqrmag == 0) return Zero;
    
    // 1. 获取 sqrt 的指数-尾数分解
    var sqrt = FPMath.GetSqrtDecomp(sqrmag);
    
    // 2. 计算尾数的倒数：2^44 / mantissa
    long reciprocal = 17592186044416L / sqrt.Mantissa;
    
    // 3. 计算位移量
    int shift = 22 + sqrt.Exponent - 8;
    
    // 4. 乘法代替除法
    return new FPVector2(
        new FP(x.RawValue * reciprocal >> shift),
        new FP(y.RawValue * reciprocal >> shift)
    );
}
```

### 5.3 原理详解

**数学基础**：
```
给定向量 v = (x, y)，长度平方 |v|² = x² + y²

归一化：v' = v / |v| = v / sqrt(|v|²)

FrameSync 技巧：
1. 分解 sqrt(|v|²) = mantissa * 2^exponent
2. 1/sqrt = 1/mantissa * 2^(-exponent)
3. 使用大常数 2^44 计算倒数：2^44 / mantissa
4. 通过位移调整指数：result >> (22 + exponent - 8)
```

**为什么是 2^44？**
```
44 = 22 (sqrt 精度) + 16 (Q16.16) + 6 (额外精度)

17592186044416 = 2^44
```

### 5.4 性能对比

| 方法 | 运算 | 相对速度 |
|-----|------|---------|
| 传统 | Sqrt + 除法 | 1.0x |
| InvSqrt | Sqrt + 乘法 | 1.5x |
| FrameSync | 查表 + 倒数乘法 | 2-3x |

---

## 6. Source Generator 自动生成代码

### 6.1 问题：手写 Swizzle 重复且易错

```csharp
// 手写 30+ Swizzle 属性，容易出错
public readonly FPVector2 XX => new(X, X);
public readonly FPVector2 XY => new(X, Y);
// ... 还有 28 个
```

### 6.2 解决方案：Source Generator

```csharp
// 定义元数据
[GenerateSwizzle]
public partial struct FPVector2 { }

// Source Generator 自动生成：
// partial struct FPVector2 {
//     public readonly FPVector2 XX => new(X, X);
//     ...
// }
```

### 6.3 实现思路

```csharp
[Generator]
public class SwizzleGenerator : ISourceGenerator
{
    public void Execute(GeneratorExecutionContext context)
    {
        var compilation = context.Compilation;
        
        // 1. 查找标记了 [GenerateSwizzle] 的类型
        var types = GetTypesWithAttribute(compilation, "GenerateSwizzle");
        
        // 2. 为每个类型生成 Swizzle 代码
        foreach (var type in types)
        {
            var source = GenerateSwizzleCode(type);
            context.AddSource($"{type.Name}.Swizzle.g.cs", source);
        }
    }
    
    string GenerateSwizzleCode(INamedTypeSymbol type)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// Auto-generated");
        sb.AppendLine($"partial struct {type.Name}");
        sb.AppendLine("{");
        
        // 生成所有 Swizzle 组合
        var components = GetComponents(type); // ["X", "Y"] 或 ["X", "Y", "Z"]
        foreach (var combo in GetSwizzleCombinations(components))
        {
            GenerateSwizzleProperty(sb, combo);
        }
        
        sb.AppendLine("}");
        return sb.ToString();
    }
}
```

### 6.4 优势

- **零运行时开销**：生成的是普通 C# 代码
- **编译时检查**：语法错误在编译时发现
- **IDE 支持**：IntelliSense 能显示生成的成员
- **易于维护**：修改生成逻辑即可批量更新

---

*文档版本: 1.1*  
*最后更新: 2026-03-13*  
*作者: Lattice Team*
