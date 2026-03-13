# Lattice 数学查找表 (LUT)

此目录包含 Lattice 定点数数学库预生成的查找表。

## 文件说明

| 文件 | 大小 | 用途 |
|------|------|------|
| `FPSqrtLut.cs` | ~692 KB | 平方根查找表 [0, 1] → [0, 1] |
| `FPAcosLut.cs` | ~600 KB | 反余弦查找表 [-1, 1] → [0, π] |
| `FPAtanLut.cs` | ~3 KB | 反正切查找表 [0, 1] → [0, π/4] |
| `FPSinCosLut.cs` | ~19 KB | 正弦/余弦查找表 [0, 2π) |

## 生成方式

这些文件由 `LutGenerator` 工具在**构建时**生成，而非运行时计算。

### 手动重新生成

```bash
# Windows
cd godot/scripts/lattice/Tools
generate-luts.bat

# 或直接使用 dotnet
cd godot/scripts/lattice/Tools/LutGenerator
dotnet run -- "../../../Math/Generated"
```

### 自动集成（可选）

取消注释 `Lattice.csproj` 中的以下行启用自动 LUT 生成：

```xml
<Import Project="Lattice.targets" />
```

启用后，每次构建时会自动检查并重新生成 LUT（仅当生成器代码有变更时）。

## 设计原理

### 为什么选择预生成 LUT？

1. **严格确定性**：运行时无任何浮点运算，避免跨平台差异
2. **启动性能**：无需运行时计算，直接加载常量数组
3. **可预测内存**：LUT 大小固定，无动态分配
4. **缓存友好**：数组连续存储，CPU 缓存命中率高

### FrameSync 风格实现

参考 FrameSyncEngine 设计：
- **Sqrt**: 额外 6 位精度，结果需右移 6 位
- **Acos**: 65537 条目覆盖 [-1, 1] 完整范围
- **Atan**: 257 条目 + 线性插值
- **SinCos**: 1024 条目覆盖 [0, 2π)

## 注意事项

- **不要手动修改**这些文件，会被重新生成覆盖
- 如需调整 LUT 大小或精度，修改 `LutGeneratorCore.cs`
- 提交代码时**应包含**生成的 LUT 文件，避免 CI 依赖 dotnet CLI
