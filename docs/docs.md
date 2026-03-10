---
layout: default
title: 开发文档
description: Sideline 技术文档、API 参考和开发指南
---

<section class="hero">
    <h1>开发文档</h1>
    <p class="hero-subtitle">技术架构、API 参考与开发指南</p>
</section>

<div class="content-container">

## 📚 文档导航

<div class="features-grid" style="margin-top: 40px;">
    <a href="#architecture" class="pixel-card feature-item" style="text-decoration: none; color: inherit;">
        <div class="feature-icon">🏗️</div>
        <h3>架构设计</h3>
        <p>了解 Sideline 的分层架构和 Lattice ECS 框架设计</p>
    </a>
    
    <a href="#api-reference" class="pixel-card feature-item" style="text-decoration: none; color: inherit;">
        <div class="feature-icon">📖</div>
        <h3>API 参考</h3>
        <p>核心类和方法的详细文档</p>
    </a>
    
    <a href="#build-guide" class="pixel-card feature-item" style="text-decoration: none; color: inherit;">
        <div class="feature-icon">🔨</div>
        <h3>构建指南</h3>
        <p>从源码构建项目的完整步骤</p>
    </a>
    
    <a href="#contrib-guide" class="pixel-card feature-item" style="text-decoration: none; color: inherit;">
        <div class="feature-icon">🤝</div>
        <h3>贡献指南</h3>
        <p>如何参与 Sideline 的开发</p>
    </a>
</div>

---

## 🏗️ 架构设计 {#architecture}

### 整体架构

Sideline 采用**分层架构**，将渲染与逻辑完全分离：

| 层级 | 职责 | 技术 |
|------|------|------|
| 渲染层 | 视觉呈现、用户输入 | Godot 4 + C# |
| 桥接层 | 状态同步、命令转发 | GodotRenderBridge |
| 逻辑层 | 游戏逻辑、物理模拟 | Lattice ECS |
| 网络层 | 联机同步、存档管理 | Steam Relay / 本地存储 |

### Lattice ECS 框架

Lattice 是自研的**确定性 ECS（Entity-Component-System）框架**，设计目标：

1. **完全确定性**: 给定相同的初始状态和输入序列，必须产生完全相同的结果
2. **帧同步支持**: 原生支持 Lockstep 联机架构
3. **零 GC**: 使用对象池和结构体，减少垃圾回收
4. **纯 C#**: 不依赖 Godot，可用于服务器端验证

#### 核心概念

**Entity（实体）**
```csharp
// 实体只是一个 ID，不包含任何数据
public struct Entity
{
    public int Id;
    public int Version;  // 用于检测失效引用
}
```

**Component（组件）**
```csharp
// 纯数据结构，无方法
public struct Position : IComponent
{
    public FixedPoint X;
    public FixedPoint Y;
}

public struct Health : IComponent
{
    public int Current;
    public int Max;
}
```

**System（系统）**
```csharp
// 处理特定组件组合的逻辑
public class MovementSystem : System<Position, Velocity>
{
    public override void Update(Entity entity, ref Position pos, ref Velocity vel)
    {
        pos.X += vel.X * Time.DeltaTime;
        pos.Y += vel.Y * Time.DeltaTime;
    }
}
```

**World（世界）**
```csharp
// 管理所有实体和组件
public class SimulationWorld
{
    public Entity CreateEntity();
    public void AddComponent<T>(Entity entity, T component) where T : IComponent;
    public ref T GetComponent<T>(Entity entity) where T : IComponent;
    public StateSnapshot CaptureSnapshot();
    public void RestoreSnapshot(StateSnapshot snapshot);
}
```

### 确定性保证

#### 定点数 (FixedPoint)

使用 Q15.16 定点数代替浮点数，确保跨平台一致性：

```csharp
public struct FixedPoint
{
    private long _rawValue;
    private const int FRACTIONAL_BITS = 16;
    
    // 所有运算都是整数运算，保证确定性
    public static FixedPoint operator +(FixedPoint a, FixedPoint b)
    {
        return new FixedPoint { _rawValue = a._rawValue + b._rawValue };
    }
    
    public static FixedPoint operator *(FixedPoint a, FixedPoint b)
    {
        return new FixedPoint { _rawValue = (a._rawValue * b._rawValue) >> FRACTIONAL_BITS };
    }
}
```

#### 随机数生成器

使用可种子化的确定性随机数生成器：

```csharp
public class DeterministicRandom
{
    private uint _state;
    
    public void Seed(uint seed) => _state = seed;
    
    public int Next(int min, int max)
    {
        // 确定性算法，相同的种子产生相同的序列
        _state = _state * 1103515245 + 12345;
        return min + (int)(_state % (uint)(max - min));
    }
}
```

---

## 📖 API 参考 {#api-reference}

### WindowManager 类

负责窗口模式管理和无边框窗口交互。

```csharp
public partial class WindowManager : Node
{
    // 当前游戏模式
    public GameMode CurrentMode { get; private set; }
    
    // 切换挂机/刷宝模式
    public void ToggleMode();
    
    // 信号：模式改变时触发
    public delegate void ModeChangedEventHandler(int mode);
}
```

### Main 类

主场景控制器，协调各模块。

```csharp
public partial class Main : Node
{
    // 初始化各模块
    public override void _Ready();
    
    // 处理模式切换
    private void OnModeChanged(int mode);
}
```

### IdlePanel 类

挂机模式 UI 面板。

```csharp
public partial class IdlePanel : Control
{
    // 更新资源显示
    public void UpdateResources(ResourceData data);
    
    // 播放收集动画
    public void PlayCollectAnimation(string resourceType, int amount);
}
```

---

## 🔨 构建指南 {#build-guide}

### 环境要求

- **Godot 版本**: 4.6.1-stable **mono** 版
- **.NET SDK**: 8.0+
- **IDE**: Windsurf / VS Code / Rider

### 安装步骤

**1. 克隆仓库**
```bash
git clone https://github.com/username/Sideline.git
cd Sideline
```

**2. 使用 Godot 编辑器打开项目**
```bash
# 打开项目文件
Godot_v4.6.1-stable_mono_win64.exe --editor godot/project.godot
```

**3. 命令行构建**
```bash
cd godot
dotnet build
```

**4. 运行项目**
- 在 Godot 编辑器中按 `F5`
- 或在命令行: `dotnet run`

### 重要设置

**关闭嵌入游戏窗口**

`编辑器 → 编辑器设置 → Run → Window Placement → Embed Game in Editor` 需**关闭**，确保以独立窗口运行。

**配置 Godot 路径 (VS Code)**

编辑 `.vscode/settings.json`:
```json
{
    "godotTools.editorPath.godot4": "D:\\GodotCSharp\\Godot_v4.6.1-stable_mono_win64\\Godot_v4.6.1-stable_mono_win64.exe"
}
```

---

## 🤝 贡献指南 {#contrib-guide}

### 如何参与

1. **Fork 仓库**
2. **创建特性分支**: `git checkout -b feature/amazing-feature`
3. **提交更改**: `git commit -m 'Add amazing feature'`
4. **推送分支**: `git push origin feature/amazing-feature`
5. **创建 Pull Request**

### 代码规范

- **只使用 C#**，禁止 `.gd` 脚本
- 注释使用**中文**
- 使用 XML 文档注释 (`/// <summary>`)
- 遵循现有命名规范

### 命名规范

```csharp
// 类名：PascalCase
public partial class WindowManager : Node { }

// 方法名：PascalCase
public void ToggleMode() { }

// 私有字段：下划线前缀 + camelCase
private bool _isDragging;

// 常量：PascalCase
private static readonly Vector2I IdleWindowSize = new(400, 300);
```

### 报告问题

发现 Bug 或有功能建议？请提交 [Issue](https://github.com/username/Sideline/issues)。

---

## 📄 更多资源

- [项目设计文档]({{ '/docs/design' | relative_url }}) - 详细的游戏设计和技术架构
- [API 完整文档](https://github.com/username/Sideline/wiki) - GitHub Wiki
- [开发日志]({{ '/blog/' | relative_url }}) - 最新开发进展

</div>
