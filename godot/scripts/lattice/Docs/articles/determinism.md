# 确定性编程技巧（历史归档）

> 历史归档文档：
>
> 本文档保留为专题说明，但其中部分表述仍停留在早期“数学库 / 技术文章”口径。当前主干的正式 determinism 边界与静态守卫请优先参考 `SystemDesignNotes.md` 与实际 analyzer/test。

确保游戏在所有平台上产生完全一致的结果。

## 1. 禁止浮点数

Lattice 提供三层防护禁止浮点数：

### 编译时：BannedApiAnalyzers

```csharp
// 这会触发编译错误
float x = 1.5f;  // 错误：禁止使用 float
```

### 构建时：Source Generators

Swizzle 代码自动生成，避免手写错误。

### 运行时：NoFloatUsageTests

```csharp
// 运行测试验证没有浮点指令
[Fact]
public void NoFloatInstructions()
{
    var methods = GetAllFPMathMethods();
    foreach (var method in methods)
    {
        AssertNoFloatInstructions(method);
    }
}
```

## 2. 安全使用 FP

### ✅ 正确用法

```csharp
// 使用整数隐式转换
FP a = 5;           // 精确
FP b = -100;        // 精确

// 使用 Raw 常量
FP c = FP.FromRaw(FP.Raw._0_50);
FP d = FP.FromRaw(FP.Raw._PI);

// 数学运算
FP e = FP.Sqrt(a);  // 确定性 LUT
FP f = FP.Sin(b);   // 确定性 LUT
```

### ❌ 错误用法

```csharp
// 禁止使用 float/double
FP a = 0.5f;        // 编译错误！
FP b = 3.14159;     // 编译错误！

// 运行时转换（仅调试/编辑器可用）
FP c = FP.FromFloat_UNSAFE(0.5f);  // 警告：非确定性！
```

## 3. 帧同步最佳实践

### 输入处理

```csharp
public void Update(InputState input)
{
    // 将输入转换为 FP（在输入边界）
    FP dx = input.Horizontal;   // int 隐式转换
    FP dy = input.Vertical;     // int 隐式转换
    
    // 所有游戏逻辑使用 FP
    velocity += new FPVector2(dx, dy) * acceleration;
    position += velocity * FP.Raw._0_01;  // 使用常量
}
```

### 校验和验证

```csharp
public void FixedUpdate()
{
    // 每帧计算校验和
    ulong checksum = ComputeChecksum();
    
    // 发送给服务器验证
    network.SendChecksum(frameNumber, checksum);
    
    // 校验和不匹配表示不同步
}

ulong ComputeChecksum()
{
    ulong hash = 14695981039346656037;  // FNV-1a offset
    
    foreach (var entity in entities)
    {
        hash ^= (ulong)entity.Position.X.RawValue;
        hash *= 1099511628211;
        hash ^= (ulong)entity.Position.Y.RawValue;
        hash *= 1099511628211;
    }
    
    return hash;
}
```

## 4. 平台一致性

### StructLayout.Explicit

Lattice 使用显式布局确保内存布局一致：

```csharp
[StructLayout(LayoutKind.Explicit, Size = 8)]
public readonly struct FP
{
    [FieldOffset(0)] public readonly long RawValue;
}
```

这保证了：
- x86/x64/ARM 布局相同
- 序列化结果一致
- 网络同步可靠

### LUT 一致性

所有三角函数使用整数 LUT：

```csharp
// Sin 使用查找表，纯整数运算
public static FP Sin(FP x)
{
    int index = (x.RawValue >> 4) & 0xFFF;  // 确定性的位运算
    return new FP(SinCosTable[index]);
}
```

## 5. 调试不同步

### 帧转储

```csharp
public void OnChecksumMismatch(ulong expected, ulong actual)
{
    // 保存帧状态
    var dump = new FrameDump
    {
        FrameNumber = currentFrame,
        Entities = entities.ToArray(),
        RandomState = rng.State
    };
    
    File.WriteAllText($"framedump_{currentFrame}.json", 
        JsonSerializer.Serialize(dump));
}
```

### 逐步对比

```csharp
[Conditional("DEBUG")]
public void VerifyDeterminism()
{
    // 运行两次，比较结果
    var result1 = Simulate(seed, frames);
    var result2 = Simulate(seed, frames);
    
    Debug.Assert(result1.Checksum == result2.Checksum);
}
```
