# LUT Generator 说明

## 设计决策：为什么 LUT 生成使用浮点？

### 问题
 LutGenerator 使用 `double Math.Sqrt` / `Math.Sin` 等浮点运算生成查找表，这是否破坏了确定性？

### 答案
**不影响运行时确定性**，原因如下：

1. **生成时 vs 运行时分离**
   ```
   开发时：运行 LutGenerator → 生成 LUT 文件（使用浮点）
   提交时：将 LUT 文件提交 Git（纯整数数组）
   运行时：直接加载 LUT 文件（无浮点运算）
   ```

2. **确定性保障流程**
   - LUT 文件一旦生成，内容就是固定的整数数组
   - 运行时只读取预生成的整数，不涉及任何浮点运算
   - 不同平台/机器运行结果完全一致

3. **为什么生成时可以用浮点？**
   - 生成过程**不依赖**运行时确定性
   - 生成结果**经过验证**后提交，成为单一数据源
   - 类似于美术资源烘焙：用工具生成，运行时直接使用

### 类比

| 类比 | 工具阶段 | 运行时阶段 |
|-----|---------|-----------|
| **纹理压缩** | Photoshop (浮点颜色) → 压缩为 DDS | 直接加载 DDS |
| **烘焙光照** | Unity 光照计算 → 烘焙贴图 | 直接采样贴图 |
| **LUT 生成** | Math.Sqrt → 整数数组 | 直接查表 |

### 验证方法

```bash
# 验证运行时无浮点
dotnet test --filter "FullyQualifiedName~NoFloatUsageTests"
```

### 重新生成 LUT

```bash
cd godot/scripts/lattice/Tools/LutGenerator
dotnet run -- "../../Math/Generated"
```

**何时需要重新生成？**
- 修改 LUT 大小（如 1024 → 4096）
- 修改精度（如额外位数调整）
- 添加新的 LUT 类型

---

## 架构

```
LutGenerator/
├── Program.cs           # 入口
├── LutGeneratorCore.cs  # 生成逻辑
└── LutType.cs           # LUT 类型枚举

输出：
Math/Generated/
├── FPSqrtLut.cs         # 65537 条目
├── FPAcosLut.cs         # 65537 条目
├── FPAtanLut.cs         # 257 条目
└── FPSinCosLut.cs       # 1024+4096 条目
```
