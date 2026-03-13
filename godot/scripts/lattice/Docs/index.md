# Lattice Math Documentation

欢迎来到 **Lattice** 定点数数学库的文档站点。

## 什么是 Lattice？

Lattice 是一个专为**确定性帧同步游戏**设计的高性能定点数数学库，完全使用 C# 编写，适用于 .NET 8 和 Godot 4 引擎。

## 核心特性

| 特性 | 说明 |
|------|------|
| 🎯 **确定性保障** | 三层防护：编译时分析器 + 构建时代码生成 + 运行时 IL 验证 |
| ⚡ **高性能** | LUT 查找表 + AggressiveInlining + 零堆分配 |
| 🔧 **易用性** | 与 float 相似的 API，隐式转换支持 |
| 📐 **完整功能** | 2D/3D 向量、四元数、矩阵、碰撞检测 |

## 快速开始

### 1. 安装

```bash
dotnet add package Lattice.Math
```

### 2. 初始化

```csharp
using Lattice.Math;

// 游戏启动时初始化 LUT
FPLut.InitializeBuiltIn();  // 使用内置 LUT
// 或
FPLut.Initialize("path/to/lut/files");  // 从文件加载
```

### 3. 基本使用

```csharp
// 创建定点数
FP a = 3.5;                    // 隐式转换
FP b = FP.FromRaw(FP.Raw._1_50);  // 使用常量

// 数学运算
FP c = a + b;                  // 加法
FP d = a * b;                  // 乘法（四舍五入）
FP e = FP.Sqrt(c);             // 平方根
FP f = FP.Sin(a);              // 正弦

// 向量运算
var v1 = new FPVector2(3, 4);
var v2 = new FPVector3(1, 2, 3);
var normalized = v1.Normalized;
var dot = FPVector2.Dot(v1, v2.XY);
```

## API 参考

- [FP (定点数)](api/Lattice.Math.FP.yml) - 核心定点数类型
- [FPVector2](api/Lattice.Math.FPVector2.yml) - 2D 向量
- [FPVector3](api/Lattice.Math.FPVector3.yml) - 3D 向量
- [FPMath](api/Lattice.Math.FPMath.yml) - 数学函数

## 性能对比

| 操作 | float (ns) | FP (ns) | 比值 |
|------|-----------|---------|------|
| 加法 | ~0.5 | ~0.5 | 1x |
| 乘法 | ~1.0 | ~1.2 | 1.2x |
| Sin | ~15 | ~5 | **0.3x** |
| Sqrt | ~8 | ~8 | 1x |

*测试环境：.NET 8, x64, Release*

## 更多资源

- [GitHub 仓库](https://github.com/yourorg/lattice)
- [性能优化指南](articles/performance.md)
- [确定性编程技巧](articles/determinism.md)
