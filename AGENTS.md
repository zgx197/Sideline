# Sideline - AGENTS.md

> 本文档供 AI 编程助手阅读，用于快速理解项目结构和技术约定。

---

## 项目概述

**Sideline** 是一款 2D 俯视角独立游戏，核心玩法结合挂机养成与地下城刷宝。游戏采用独特的双模式窗口设计：

- **挂机模式 (Idle)**：无边框小窗口 (400x300)，始终置顶，可拖拽至屏幕角落，自动收集资源
- **刷宝模式 (Dungeon)**：正常大窗口 (1280x720)，可边框/全屏切换，进入地下城战斗

设计理念来源于"工作间隙偷偷经营地下世界"的双重生活体验，目标平台为 Steam。

---

## 技术栈

| 层级 | 技术 | 说明 |
|------|------|------|
| 渲染引擎 | Godot 4.6.1 | 使用 **Mono/.NET 版**，非 Steam 标准版 |
| 编程语言 | C# | 项目内**只使用 C#**，禁止 `.gd` 脚本 |
| 目标框架 | .NET 8.0 | Android 构建使用 .NET 9.0 |
| 渲染器 | GL Compatibility | 2D 游戏首选，兼容性更好 |
| 逻辑框架 | Lattice (定点数库就绪，ECS待实现) | 自研确定性 ECS 帧同步框架，694测试通过，跨平台CI |

### 开发环境要求

- **Godot 版本**: 4.6.1-stable mono 版
- **.NET SDK**: 9.0.202（兼容 Godot 4.x，要求 .NET 8+）
- **IDE**: Windsurf（VS Code 分支）配合 C# Dev Kit + Godot Tools 插件

---

## 项目结构

```
Sideline/
├── godot/                       # Godot 4 项目根目录
│   ├── project.godot            # Godot 项目配置文件
│   ├── Sideline.csproj          # C# 项目文件
│   ├── Sideline.sln             # Visual Studio 解决方案
│   ├── .editorconfig            # 编辑器配置（UTF-8）
│   ├── icon.svg                 # 项目图标
│   │
│   ├── scenes/                  # 场景文件 (.tscn)
│   │   ├── main/Main.tscn       # 主场景/启动场景
│   │   ├── ui/IdlePanel.tscn    # 挂机模式 UI
│   │   └── ui/DungeonPanel.tscn # 地下城模式 UI
│   │
│   ├── scripts/                 # C# 脚本
│   │   ├── main/Main.cs         # 主场景控制器
│   │   ├── ui/IdlePanel.cs      # 挂机面板逻辑
│   │   ├── ui/DungeonPanel.cs   # 地下城面板逻辑
│   │   └── window/WindowManager.cs  # 窗口管理器
│   │
│   ├── assets/                  # 游戏资源（图片、音频、字体等）
│   └── .godot/                  # Godot 编辑器缓存（Git 忽略）
│
├── docs/design/                 # 设计文档
│   └── GameDesign.md            # 完整游戏设计文档
│
├── .vscode/                     # VS Code 配置
│   └── settings.json            # Godot 编辑器路径配置
│
├── .gitignore                   # Git 忽略规则
└── .windsurfignore              # Windsurf AI 索引忽略规则
```

---

## 构建与运行

### 命令行构建

```bash
# 进入 Godot 项目目录
cd godot

# 构建 C# 项目
dotnet build

# 或者使用发布配置
dotnet build -c Release
```

### 从 Godot 编辑器运行

1. 使用 **Godot 4.6.1 Mono 版** 打开 `godot/project.godot`
2. 按 `F5` 或点击"运行项目"按钮
3. 重要设置：`编辑器 → 编辑器设置 → Run → Window Placement → Embed Game in Editor` 需**关闭**，确保以独立窗口运行

### 构建配置

解决方案包含以下构建配置：
- `Debug|Any CPU` - 调试构建
- `ExportDebug|Any CPU` - 导出调试构建
- `ExportRelease|Any CPU` - 导出发布构建

---

## 代码规范

### 语言与编码

- **只使用 C#**，禁止创建 `.gd` 文件
- 文件编码：**UTF-8**
- 注释语言：**中文**（与代码库保持一致）

### 命名约定

- 类名：PascalCase (如 `WindowManager`, `IdlePanel`)
- 方法名：PascalCase (如 `ToggleMode()`, `OnSwitchPressed()`)
- 私有字段：以下划线开头 + camelCase (如 `_windowManager`, `_isDragging`)
- 常量/只读静态字段：PascalCase (如 `IdleWindowSize`, `DungeonWindowSize`)

### 节点引用

- 在 `.tscn` 场景文件中使用 `unique_name_in_owner = true` 标记需要代码访问的节点
- 在 C# 中使用 `%NodeName` 语法通过 `GetNode<T>()` 获取
- 示例：`_titleLabel = GetNode<Label>("%TitleLabel");`

### 信号 (Signals)

- 使用 C# 事件风格定义信号
- 命名规范：`[动作]Requested` 表示请求事件
- 示例：`SwitchToDungeonRequested`, `ModeChanged`

### XML 文档注释

- 公共类和方法应添加中文 XML 文档注释
- 示例：
```csharp
/// <summary>
/// 窗口管理器：负责无边框窗口的拖拽、置顶、模式切换
/// </summary>
public partial class WindowManager : Node
{
    /// <summary>
    /// 切换到挂机模式：无边框、小窗口、置顶、可拖拽
    /// </summary>
    private void ApplyIdleMode() { ... }
}
```

---

## 架构说明

### 双模式窗口系统

```
┌─────────────────────────────────────────────────────┐
│                    Main.cs                          │
│              （主控制器，协调模式切换）               │
└──────────────────────┬──────────────────────────────┘
                       │
        ┌──────────────┼──────────────┐
        ▼              ▼              ▼
┌──────────────┐ ┌──────────┐ ┌────────────────┐
│ WindowManager │ │IdlePanel │ │  DungeonPanel  │
│  （窗口管理）  │ │(挂机UI)  │ │   (刷宝UI)     │
└──────────────┘ └──────────┘ └────────────────┘
```

### 核心类职责

| 类 | 职责 |
|----|------|
| `Main` | 主场景入口，初始化各模块，处理模式切换信号 |
| `WindowManager` | 管理窗口尺寸、边框、置顶状态，处理 ESC 键和拖拽逻辑 |
| `IdlePanel` | 挂机模式 UI，模拟资源收集（每秒+1金币） |
| `DungeonPanel` | 地下城模式 UI，Phase 0 仅显示占位信息 |

### 窗口模式规格

| 属性 | 挂机模式 (Idle) | 刷宝模式 (Dungeon) |
|------|-----------------|-------------------|
| 尺寸 | 400 x 300 | 1280 x 720 |
| 边框 | 无边框 | 有边框 |
| 置顶 | 始终置顶 | 不置顶 |
| 位置 | 屏幕右下角 | 屏幕居中 |
| 拖拽 | 支持 | 不支持 |

### 输入映射

在 `project.godot` 中定义：
- `ui_toggle_mode` - ESC 键 (KEY_ESCAPE)，用于切换窗口模式

---

## 开发路线图

### Phase 0 — 技术验证（当前）
- [x] Godot 4 无边框窗口原型
- [x] FP 定点数库验证（694测试通过，SIMD/Unsafe优化完成）
- [ ] 最小 ECS 框架实现

### Phase 1 — 核心玩法
- [ ] 随机地图生成（BSP）
- [ ] 战斗系统
- [ ] 装备系统 + 存档
- [ ] 挂机资源收集完整实现

### Phase 2 — EA 发布准备
- [ ] Steam SDK 接入
- [ ] 战斗回放
- [ ] 内容完善

### Phase 3 — 联机 DLC
- [ ] Lockstep 联机
- [ ] Steam Relay 集成

---

## 相关文档

- `docs/design/GameDesign.md` - 完整游戏设计文档（技术选型、Lattice 框架设计、AI 辅助开发规划）
- `docs/development/Workflow.md` - **开发流程规范**（分支策略、提交规范、CI/CD 流程）

---

## 代码格式规范

### 强制要求（AI 必须遵守）

项目配置了严格的代码格式检查（通过 `.editorconfig` 和 CI），**不符合格式的代码会导致 CI 失败**。

#### 格式规则（由 .editorconfig 定义）

| 规则 | 配置 | 说明 |
|------|------|------|
| **缩进** | 4 个空格 | 禁止使用 Tab |
| **换行符** | LF (`\n`) | 统一 Unix 风格换行 |
| **文件编码** | UTF-8 | 无 BOM |
| **行尾空格** | 自动移除 | 保持干净 |
| **文件末尾** | 必须有空行 | 标准 POSIX |

#### 命名规范

| 元素 | 规范 | 示例 |
|------|------|------|
| **类名** | PascalCase | `FPLutCacheOptimized`, `WorldPosition` |
| **方法名** | PascalCase | `AddBatch()`, `ClampBranchless()` |
| **私有字段** | `_` + camelCase | `_rawValue`, `_sinCosTable` |
| **常量** | PascalCase | `MulRound`, `CHUNK_SIZE` |
| **接口** | `I` + PascalCase | `IComponent` |
| **泛型参数** | `T` + 描述 | `TComponent`, `TSystem` |

#### AI 编码注意事项

1. **缩进必须使用 4 个空格**，不能使用 Tab
   ```csharp
   // ✅ 正确
   public static FP Add(FP a, FP b)
   {
       return new FP(a.RawValue + b.RawValue);
   }
   
   // ❌ 错误（Tab 缩进）
   public static FP Add(FP a, FP b)
   {
   	return new FP(a.RawValue + b.RawValue);
   }
   ```

2. **花括号必须另起一行**（Allman 风格）
   ```csharp
   // ✅ 正确
   if (condition)
   {
       DoSomething();
   }
   
   // ❌ 错误（K&R 风格）
   if (condition) {
       DoSomething();
   }
   ```

3. **提交前自动格式化**
   ```bash
   cd godot/scripts/lattice
   dotnet format  # AI 应在提交前运行此命令
   ```

### 格式检查流程

```
本地开发 ──► dotnet format ──► 提交代码
                    │
                    ▼
              GitHub Actions
                    │
              格式检查通过 ──► 继续构建测试
              格式检查失败 ──► CI 失败，需要修复
```

## Git 提交规范

### 语言要求（⚠️ 严格执行）

**AI 助手生成 Commit Message 时必须使用中文**，尽量少用或不用英文。类型关键字（feat/fix/docs等）可保留英文，描述部分必须用中文。

```bash
# ✅ 正确示例（纯中文或中文为主）
feat(lattice): 添加 SIMD 整数批处理操作
fix(ci): 修复 PowerShell 兼容性问题
docs: 更新 README 性能基准测试
refactor: 重构 FP 乘法运算逻辑

# ⚠️ 避免（英文过多）
feat(lattice): add SIMD integer batch operations
fix(ci): resolve PowerShell compatibility issues

# ❌ 禁止（纯英文）
feat(lattice): implement SIMD batch processing with hardware acceleration
```

### 类型关键字（保留英文）

| 关键字 | 中文含义 | 使用场景 |
|--------|----------|----------|
| `feat` | 新功能 | 添加新功能、新模块 |
| `fix` | 修复 | 修复 Bug、问题 |
| `docs` | 文档 | 更新文档、注释 |
| `style` | 格式 | 代码格式化、样式调整 |
| `refactor` | 重构 | 重构代码，不修改功能 |
| `perf` | 性能 | 性能优化 |
| `test` | 测试 | 添加或修改测试 |
| `ci` | 持续集成 | CI/CD 配置修改 |
| `chore` | 构建 | 构建工具、依赖更新 |

### Commit Message 格式

```
<type>(<scope>): <subject>

<body>

<footer>
```

#### Type（必须）
- `feat`: 新功能
- `fix`: Bug 修复
- `docs`: 文档更新
- `style`: 代码格式（不影响功能）
- `refactor`: 重构
- `perf`: 性能优化
- `test`: 测试相关
- `ci`: CI/CD 配置
- `chore`: 构建/工具链

#### Scope（可选）
- `lattice`: 定点数库
- `ecs`: ECS 核心
- `ui`: 界面系统
- `ci`: CI/CD

#### Subject（必须）
- 简洁描述变更内容
- 首字母小写（英文）或直接中文
- 不超过 72 字符
- 不加句号

### 完整示例（纯中文）

```bash
feat(lattice): 添加 WorldPosition 无限世界坐标系统

- 实现基于 Chunk 的坐标系统（块大小 = 1024）
- 使用位运算实现无分支溢出处理
- 包含 45 个跨块场景的单元测试
- 支持负坐标和超大世界

关联需求: #45
```

### 常用中文描述词汇

| 中文 | 使用场景 |
|------|----------|
| 添加/新增 | 新功能、新文件 |
| 修复 | Bug 修复 |
| 更新 | 文档、配置修改 |
| 重构 | 代码重构 |
| 优化 | 性能、逻辑改进 |
| 删除/移除 | 删除代码、文件 |
| 调整 | 参数、配置微调 |
| 实现 | 完成功能开发 |
| 引入 | 引入新依赖、新机制 |
| 支持 | 添加新特性支持 |

---

## 注意事项

1. **Godot 版本**: 必须使用 **Mono/.NET 版**，Steam 版 Godot 不支持 C#
2. **编辑器设置**: 关闭"在游戏内嵌窗口"选项，否则无边框窗口特性无法正常测试
3. **资源管理**: AI 生成的原始素材存放于 `ai/generated/`，经筛选处理后移入 `godot/assets/`
4. **Git 提交**: `.godot/` 和构建产物已加入 `.gitignore`，无需提交
5. **提交语言**: **强制英文**，参考上方 Git 提交规范
