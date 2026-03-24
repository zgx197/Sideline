# Lattice 热重载架构设计

> 基于 Photon Quantum 式 Stateless ECS 的热重载方案设计文档

---

## 目录

1. [背景与架构基础](#1-背景与架构基础)
2. [热重载方案对比](#2-热重载方案对比)
3. [推荐架构设计](#3-推荐架构设计)
4. [Sideline 项目具体方案](#4-sideline-项目具体方案)
5. [接入路线图](#5-接入路线图)
6. [技术实现细节](#6-技术实现细节)

---

## 1. 背景与架构基础

### 1.1 Photon Quantum 式架构特点

Lattice 参考 Photon Quantum 设计，具有以下核心特征：

| 特征 | 说明 | 对热重载的意义 |
|------|------|---------------|
| **Stateless Systems** | System 不持有任何可变状态 | System 可以随时替换，无需状态迁移 |
| **Data in Frame** | 所有游戏状态存储在 Frame 中 | 通过 Checkpoint/Restore 实现快速重启 |
| **Component CodeGen** | 组件通过 Source Generator/DSL 生成 | 自动生成序列化代码，支持状态持久化 |
| **Deterministic** | 确定性模拟，支持预测/回滚 | 热重载后可通过 Re-simulation 验证一致性 |

### 1.2 核心设计理念

```csharp
// ✅ Lattice 的 System 设计 - 完全无状态
public unsafe class MovementSystem : ISystem
{
    // 禁止：私有可变字段
    // private float _speed;
    
    public void OnUpdate(Frame frame, FP deltaTime)
    {
        // 所有数据来自 frame 参数
        var query = frame.Query<Transform, Velocity>();
        var enumerator = query.GetEnumerator();
        while (enumerator.MoveNext())
        {
            enumerator.Component1.Position += enumerator.Component2.Value * deltaTime;
        }
    }
}
```

**关键洞察**：由于 System 无状态，热重载时不需要"迁移状态"，只需要：
1. 保存 Frame Checkpoint（包含所有游戏状态）
2. 替换 System 实现
3. 恢复 Checkpoint

这使得热重载实现比传统 OOP 架构简单得多。

---

## 2. 热重载方案对比

### 2.1 主流方案一览

| 方案 | 原理 | 性能 | 平台支持 | 与 Lattice 契合度 |
|------|------|------|----------|------------------|
| **HybridCLR** | IL2CPP + 解释器执行补丁 | ~90% AOT | PC/Android/iOS | ⭐⭐⭐⭐⭐ 最佳 |
| **AssemblyLoadContext** | 卸载/重装 Assembly | 100% | PC/Mac/Linux | ⭐⭐⭐⭐ 适合开发 |
| **.NET Hot Reload** | Roslyn 增量编译 | 100% | 调试专用 | ⭐⭐ 限制太多 |
| **xLua 热修复** | Lua 替换 C# 方法 | ~70% | 全平台 | ⭐⭐⭐ 需要标记 |
| **Facet Lua** | 脚本层逻辑 | ~70% | 全平台 | ⭐⭐⭐⭐ 与 UI 统一 |

### 2.2 方案详细分析

#### 2.2.1 HybridCLR（强烈推荐）

**工作原理**：
- 主程序使用 IL2CPP AOT 编译，保持高性能
- 热更新代码编译为 IL，通过解释器执行
- 支持方法体级别的热更新

**与 Lattice 的契合点**：
```csharp
// 修改前
public void OnUpdate(Frame frame, FP deltaTime)
{
    t->Position += v->Value * deltaTime * FP._0_50; // 速度系数 0.5
}

// 修改后（热重载后生效）
public void OnUpdate(Frame frame, FP deltaTime)
{
    t->Position += v->Value * deltaTime * FP._0_75; // 改为 0.75
}
```

**优势**：
- ✅ 支持真正的方法体修改（不只是配置）
- ✅ 保持 C# 语法，无需改写为 Lua
- ✅ 支持移动端（iOS/Android）
- ✅ 与 Stateless System 完美契合（System 替换无状态迁移成本）

**局限**：
- 不支持修改字段（但 Quantum 式 System 本来就该无字段）
- 需要额外的构建流程（编译补丁包）

#### 2.2.2 AssemblyLoadContext（开发专用）

**工作原理**：
```csharp
// 1. 创建可收集的加载上下文
var context = new AssemblyLoadContext("Gameplay", isCollectible: true);

// 2. 加载 Assembly
var assembly = context.LoadFromAssemblyPath("Lattice.Gameplay.dll");

// 3. 需要更新时卸载
context.Unload();
GC.Collect(); // 强制清理

// 4. 加载新版本
var newContext = new AssemblyLoadContext("Gameplay_v2", isCollectible: true);
```

**适用场景**：
- PC 开发阶段快速迭代
- 不需要考虑 iOS 支持的项目

**优势**：
- ✅ 零依赖，纯 .NET
- ✅ 100% 原生性能
- ✅ 可以修改类结构（完全重新加载）

**局限**：
- ❌ **不支持 iOS**（iOS 禁止动态加载代码）
- ❌ 卸载有延迟（需要 GC）

#### 2.2.3 .NET Hot Reload（不推荐）

**限制**：
- 不能修改方法签名
- 不能新增字段/方法
- 仅调试模式可用

**结论**：不适合游戏开发的热重载需求。

#### 2.2.4 Facet Lua（脚本层）

**适用场景**：
- AI 行为树
- Buff/Debuff 效果
- UI 交互逻辑

**优势**：
- ✅ 与 Facet UI 框架统一技术栈
- ✅ 修改立即生效（无需编译）
- ✅ 适合非程序员（策划）调整

---

## 3. 推荐架构设计

### 3.1 三层热重载架构

```
┌─────────────────────────────────────────────────────────────┐
│  Layer 1: 配置层 (JSON/YAML)                                 │
│  ─────────────────────────                                   │
│  • 数值参数：伤害、速度、冷却时间、掉落率                        │
│  • 开关配置：功能开关、调试选项                                 │
│  • 实现：FileSystemWatcher 监控文件变化                        │
│  • 适用：90% 的日常调整                                        │
├─────────────────────────────────────────────────────────────┤
│  Layer 2: 逻辑层 (HybridCLR)                                 │
│  ─────────────────────                                       │
│  • System 实现：MovementSystem、CombatSystem                   │
│  • 算法逻辑：路径finding、战斗公式、物理计算                    │
│  • 实现：HybridCLR 热更新                                     │
│  • 适用：核心玩法调整，需要保持 C# 性能                         │
├─────────────────────────────────────────────────────────────┤
│  Layer 3: 脚本层 (Facet Lua)                                 │
│  ────────────────────                                        │
│  • AI 行为：敌人决策、技能释放逻辑                              │
│  • 效果逻辑：Buff/Debuff、触发器                               │
│  • UI 交互：面板逻辑、动画回调                                  │
│  • 实现：MoonSharp Lua Runtime                                │
└─────────────────────────────────────────────────────────────┘
```

### 3.2 快速 Checkpoint/Restart 机制

由于 System 无状态，可以实现极快的"软重启"：

```csharp
public class LatticeSession
{
    /// <summary>
    /// 快速重启 Session（用于代码热重载）
    /// 总耗时 < 100ms
    /// </summary>
    public void FastRestart()
    {
        // Step 1: 创建 Checkpoint（< 10ms）
        var checkpoint = CreateCheckpoint();
        
        // Step 2: 销毁当前 Session（System 状态，无数据）
        DestroySystems();
        
        // Step 3: 重新创建 System（加载新代码）
        InitializeSystems();
        
        // Step 4: 恢复游戏状态（< 50ms）
        RestoreFromCheckpoint(checkpoint);
    }
}
```

**对比传统重启**：
- 传统：退出 Godot → 编译 → 启动 Godot → 加载场景 → 进入游戏（30-60s）
- Checkpoint：内存操作 + System 重建（< 100ms）

---

## 4. Sideline 项目具体方案

### 4.1 按模块选择技术

| 模块 | 推荐技术 | 理由 |
|------|---------|------|
| **挂机收益计算** | JSON 配置 | 数值经常调，无需改代码 |
| **地下城战斗逻辑** | HybridCLR | 战斗手感需要精细调整，保持性能 |
| **敌人 AI** | Facet Lua | AI 行为多变，与 UI 层统一 |
| **窗口模式切换** | AssemblyLoadContext | 开发时快速迭代（仅 PC） |
| **物理/碰撞** | HybridCLR | 性能敏感，需要精确调试 |

### 4.2 平台差异化策略

```csharp
// 根据平台选择最优方案
public interface IHotReloadProvider
{
    void Initialize();
    bool TryReload<T>(string assemblyPath);
}

#if UNITY_EDITOR || WINDOWS || MAC || LINUX
// PC 开发：AssemblyLoadContext 最快
public class AssemblyLoadContextProvider : IHotReloadProvider { }
#else
// 移动端/发布：HybridCLR
public class HybridCLRProvider : IHotReloadProvider { }
#endif
```

### 4.3 与 Facet 的集成

```csharp
// Lattice Session 初始化时集成 Facet 的 Lua 热重载
public partial class Session
{
    private LuaReloadCoordinator? _luaReloadCoordinator;
    
    public void Initialize(LatticeConfig config)
    {
        // C# 层热重载
        _hotReloadProvider = config.HotReloadProvider;
        
        // Lua 层热重载（Facet）
        if (config.EnableLuaHotReload && config.UIManager != null)
        {
            _luaReloadCoordinator = new LuaReloadCoordinator(config.UIManager);
        }
    }
    
    protected virtual void UpdateSystems(Frame frame, FP deltaTime)
    {
        // 轮询 Lua 热重载
        _luaReloadCoordinator?.Poll("tick");
        
        // 执行 System 更新
        foreach (var system in _systems)
        {
            system.OnUpdate(frame, deltaTime);
        }
    }
}
```

---

## 5. 接入路线图

### Phase 1: 基础配置热重载（立即实施）

**目标**：解决 90% 的数值调整需求

**实现**：
```csharp
// Config/Gameplay.json
{
  "idle": {
    "goldPerSecond": 1.5,
    "maxOfflineTime": 7200
  },
  "dungeon": {
    "playerSpeed": 8.0,
    "enemyHPScale": 1.2
  }
}
```

**工作量**：1-2 天

### Phase 2: Checkpoint/Restart 机制（MVP 前）

**目标**：实现 < 100ms 的 Session 快速重启

**实现**：
- 完善 Frame 序列化（检查点）
- 实现 Session 快速重建
- 开发工具：一键重启按钮

**工作量**：3-5 天

### Phase 3: HybridCLR 集成（EA 前）

**目标**：支持移动端的代码热更新

**实现**：
- 接入 HybridCLR 工具链
- 设置补丁构建流程
- 实现运行时补丁加载

**工作量**：1-2 周

### Phase 4: 联机模式适配（联机 DLC）

**目标**：联机版本的安全性和一致性

**实现**：
- 开发模式：热重载开启
- 发布模式：热重载关闭，所有玩家使用相同代码版本
- 服务端验证：检查客户端代码哈希

**工作量**：1 周

---

## 6. 技术实现细节

### 6.1 HybridCLR 集成示例

```csharp
namespace Lattice.HotReload
{
    public class HybridCLRLoader
    {
        private readonly string _patchPath;
        
        /// <summary>
        /// 加载热更新补丁
        /// </summary>
        public bool LoadPatch(string patchName)
        {
            var dllPath = Path.Combine(_patchPath, $"{patchName}.dll");
            if (!File.Exists(dllPath))
            {
                return false;
            }
            
            // HybridCLR 加载补丁
            System.Reflection.Assembly.Load(File.ReadAllBytes(dllPath));
            
            // 刷新 Lattice System（利用 Stateless 特性）
            RefreshSystems();
            
            return true;
        }
        
        private void RefreshSystems()
        {
            // 1. 保存 Checkpoint
            var checkpoint = Session.Current.CreateCheckpoint();
            
            // 2. 重建所有 System（新代码自动生效）
            Session.Current.RebuildSystems();
            
            // 3. 恢复 Checkpoint
            Session.Current.RestoreFromCheckpoint(checkpoint);
        }
    }
}
```

### 6.2 AssemblyLoadContext 实现

```csharp
#if NET6_0_OR_GREATER
public class LatticeAssemblyLoadContext : AssemblyLoadContext
{
    public LatticeAssemblyLoadContext() : base(isCollectible: true) { }
    
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // 优先从当前域加载已存在的 Assembly
        var existing = Default.Assemblies.FirstOrDefault(a => a.FullName == assemblyName.FullName);
        if (existing != null)
        {
            return existing;
        }
        return base.Load(assemblyName);
    }
}
#endif
```

### 6.3 配置热重载实现

```csharp
public class ConfigurationHotReload
{
    private readonly FileSystemWatcher _watcher;
    private readonly Dictionary<string, Action<string>> _handlers;
    
    public ConfigurationHotReload(string configPath)
    {
        _watcher = new FileSystemWatcher(configPath, "*.json")
        {
            NotifyFilter = NotifyFilters.LastWrite
        };
        _watcher.Changed += OnConfigChanged;
        _watcher.EnableRaisingEvents = true;
    }
    
    private void OnConfigChanged(object sender, FileSystemEventArgs e)
    {
        // 延迟加载避免文件锁
        Task.Delay(100).ContinueWith(_ =>
        {
            var content = File.ReadAllText(e.FullPath);
            if (_handlers.TryGetValue(Path.GetFileName(e.FullPath), out var handler))
            {
                handler(content);
            }
        });
    }
}
```

---

## 附录：关键决策记录

### 决策 1：为什么不用 xLua 替代 HybridCLR？

**考虑**：xLua 也可以热更新 C# 代码

**结论**：
- xLua 需要修改源码添加 `[Hotfix]` 标记
- xLua 有桥接性能损耗
- HybridCLR 保持纯 C# 语法，更简洁

### 决策 2：为什么不直接用 .NET Hot Reload？

**考虑**：Visual Studio 自带 Hot Reload

**结论**：
- 仅调试模式可用
- 不能改方法签名
- 不支持发布到移动端

### 决策 3：Checkpoint 机制的必要性

**考虑**：HybridCLR 可以直接替换方法，为什么还需要 Checkpoint？

**结论**：
- HybridCLR 有平台限制（iOS 支持有限）
- Checkpoint 机制提供平台无关的"软重启"能力
- 对于 Component 结构变化，必须重建 Session

---

## 参考资源

- [HybridCLR 官方文档](https://focus-creative-games.github.io/hybridclr/)
- [Photon Quantum 架构文档](https://doc.photonengine.com/quantum/current/manual/quantum-intro)
- [.NET AssemblyLoadContext](https://docs.microsoft.com/en-us/dotnet/core/dependency-loading/understanding-assemblyloadcontext)
- [Facet Lua 热重载实现](../facet/Runtime/LuaReloadCoordinator.cs)

---

*文档版本: 1.0*  
*创建日期: 2026-03-21*  
*作者: Lattice 开发团队*
