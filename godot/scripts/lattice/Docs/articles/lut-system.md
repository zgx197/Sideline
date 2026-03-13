# LUT 查找表系统

Lattice 使用 LUT（Lookup Table）实现高性能确定性数学函数。

## 为什么使用 LUT？

| 方案 | 速度 | 精度 | 确定性 | 适用性 |
|------|------|------|--------|--------|
| 硬件浮点 | 快 | 高 | ❌ 不确定 | 单机游戏 |
| 软件浮点 | 慢 | 高 | ✅ 确定 | 不推荐 |
| **LUT** | **快** | **中** | **✅ 确定** | **帧同步** |
| CORDIC | 中 | 中 | ✅ 确定 | 嵌入式 |

## LUT 配置

### 内置 LUT（推荐）

```csharp
// 使用编译时嵌入的 LUT
FPLut.InitializeBuiltIn();
```

优点：
- 零配置
- 无需额外文件
- 适合大多数游戏

### 文件 LUT

```csharp
// 从文件系统加载
FPLut.Initialize("res://addons/lattice/lut/");
```

优点：
- 可热更新
- 可调节精度
- 内存可控

### 嵌入资源 LUT

```csharp
// 从 C# 嵌入资源加载
FPLut.InitializeFromEmbedded();
```

优点：
- Godot 导出友好
- 单文件部署

## LUT 文件格式

### 目录结构

```
lut/
├── FPSinCos.bin    # 32 KB (4096 * 2 * 8)
├── FPTan.bin       # 32 KB (4096 * 8)
├── FPAsin.bin      # 512 KB (65537 * 8)
├── FPAcos.bin      # 512 KB (65537 * 8)
├── FPAtan.bin      # 512 KB (65537 * 8)
├── FPSqrt.bin      # 256 KB (65536 * 4)
└── version.txt     # 版本信息
```

### 文件格式

二进制文件，直接使用 `Buffer.BlockCopy` 读取：

```csharp
// C# 读取示例
byte[] bytes = File.ReadAllBytes("FPSinCos.bin");
long[] table = new long[bytes.Length / 8];
Buffer.BlockCopy(bytes, 0, table, 0, bytes.Length);
```

### 生成 LUT

使用 LutFileGenerator 工具：

```bash
dotnet run --project Tools/LutFileGenerator -- output/
```

## 精度说明

| LUT | 大小 | 精度 | 用途 |
|-----|------|------|------|
| Sin/Cos | 4096 | 0.0015 弧度 | 一般计算 |
| Tan | 4096 | 0.0015 弧度 | 角度计算 |
| Asin | 65537 | 0.00003 | 反三角 |
| Acos | 65537 | 0.00003 | 反三角 |
| Atan | 65537 | 0.004 | 角度计算 |
| Sqrt | 65536 | 0.00002 | 长度计算 |

## 自定义 LUT

### 调整精度

修改 `LutFileGenerator` 的常量：

```csharp
// 更高精度的 Sin/Cos
const int SinCosLutSize = 8192;  // 默认 4096
```

### 自定义函数

```csharp
// 生成自定义 LUT
var customLut = new long[1024];
for (int i = 0; i < 1024; i++)
{
    double x = i / 1024.0;
    customLut[i] = (long)(MyFunction(x) * FP.ONE);
}

// 保存
File.WriteAllBytes("FPCustom.bin", 
    customLut.SelectMany(BitConverter.GetBytes).ToArray());
```

## 性能优化

### LUT Warmup

首次访问 LUT 可能触发冷启动，建议预热：

```csharp
public override void _Ready()
{
    FPLut.InitializeBuiltIn();
    
    // 预热：触发 JIT 和缓存
    FPMath.Warmup();
}
```

### 缓存友好

LUT 设计为顺序访问友好：

```csharp
// 好：顺序访问
for (int i = 0; i < 4096; i++)
    FP sin = FPLut.SinCosTable[i * 2];

// 避免：随机访问（缓存不友好）
for (int i = 0; i < 1000; i++)
    FP sin = FPLut.SinCosTable[random.Next(4096) * 2];
```

## 内存占用

总内存占用：约 1.8 MB

| LUT | 内存 |
|-----|------|
| Sin/Cos | 32 KB |
| Tan | 32 KB |
| Asin | 512 KB |
| Acos | 512 KB |
| Atan | 512 KB |
| Sqrt | 256 KB |
| **总计** | **~1.8 MB** |

移动端优化：可仅加载 Fast LUT，内存降至 ~500 KB。
