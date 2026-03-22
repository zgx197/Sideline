# Facet AI 辅助 UI 工作流设计文档

> 本文档描述从自然语言到 Facet 框架 UI 实现的完整工作流设计，包括工具选型、AI Prompt 模板和实现路线图。

**版本**: 1.0  
**日期**: 2026-03-21  
**作者**: AI Assistant + 项目团队讨论整理

---

## 目录

1. [背景与目标](#1-背景与目标)
2. [整体架构](#2-整体架构)
3. [详细工作流程](#3-详细工作流程)
4. [工具选型分析](#4-工具选型分析)
5. [AI Prompt 模板库](#5-ai-prompt-模板库)
6. [Penpot 插件开发规范](#6-penpot-插件开发规范)
7. [实现路线图](#7-实现路线图)
8. [附录](#8-附录)

---

## 1. 背景与目标

### 1.1 问题背景

在 Sideline 项目开发过程中，作为游戏开发工程师，我们面临以下 UI 设计挑战：

- **专业设计工具门槛高**：专职游戏设计师资源有限
- **文字描述转 UI 效率低**：用自然语言描述 UI 布局，AI 生成的结果不够精准
- **图片难以表达层级和流程**：静态截图无法清晰表达界面层级结构和交互流程
- **重复劳动**：从设计稿到 Facet 框架代码需要手工转换

### 1.2 核心目标

建立一套 **"自然语言 → AI 理解 → 原型工具 → Facet 布局"** 的自动化工作流：

1. **降低 UI 设计门槛**：通过 AI 辅助快速生成 UI 原型
2. **保留可视化调整能力**：在原型工具中进行人工精调
3. **自动化代码生成**：从原型工具直接导出 Facet 框架代码
4. **支持热重载开发**：生成的 Lua 代码支持 Facet 运行时热更新

### 1.3 设计原则

- **AI 消费结构化数据**：让 AI 读取原型工具的导出数据，而非文字描述
- **分层架构**：布局（原型工具）+ 行为（Lua）+ 数据（Projection）分离
- **开放标准**：优先选择开源、自托管、开放文件格式的工具
- **渐进式落地**：从 MVP 开始，逐步完善工具链

---

## 2. 整体架构

### 2.1 四层工作流架构

```
┌─────────────────────────────────────────────────────────────────────┐
│ Layer 4: 自然语言描述层                                               │
│ 输入: "暗黑风格 RPG 背包界面，左侧角色立绘，右侧 6x4 物品格子..."         │
│ 工具: AI 设计生成器 (Magic Patterns, First Draft, etc.)              │
└─────────────────────────────────────────────────────────────────────┘
                                    ↓
┌─────────────────────────────────────────────────────────────────────┐
│ Layer 3: 原型工具层 (可视化调整)                                       │
│ 工具: Penpot (推荐) / Figma / 即时设计                                │
│ 关键活动:                                                            │
│   - 调整布局、间距、对齐                                              │
│   - 规范节点命名 ($NodeName 格式)                                     │
│   - 设置 Flex/Grid 布局                                               │
│   - 标注交互热点                                                      │
└─────────────────────────────────────────────────────────────────────┘
                                    ↓
┌─────────────────────────────────────────────────────────────────────┐
│ Layer 2: 结构化数据导出层                                             │
│ 格式: JSON / Design Tokens / 开放文件格式                              │
│ 内容: 完整层级树、布局属性、节点关系、样式定义                          │
└─────────────────────────────────────────────────────────────────────┘
                                    ↓
┌─────────────────────────────────────────────────────────────────────┐
│ Layer 1: Facet 代码生成层                                             │
│ 输出:                                                               │
│   - UIPageDefinition.cs    (页面定义)                                │
│   - {page_name}.tscn       (Godot 场景)                              │
│   - {page_name}_runtime.lua (Lua 控制器)                             │
└─────────────────────────────────────────────────────────────────────┘
```

### 2.2 Facet 框架三层代码结构

生成的 Facet 代码遵循 Facet 框架规范：

```
FacetPage/
├── UIPageDefinition.cs           # 页面元数据定义
│   ├── pageId: "client.inventory"
│   ├── layoutType: Scene
│   ├── layoutPath: "res://ui/scenes/inventory.tscn"
│   ├── layer: "Main"
│   ├── cachePolicy: KeepAlive
│   └── controllerScript: "inventory_runtime"
│
├── inventory.tscn               # Godot 场景文件
│   ├── InventoryRoot (Control)
│   │   ├── CharacterPanel (PanelContainer)
│   │   │   ├── CharacterImage (TextureRect)
│   │   │   ├── CharacterName (Label)
│   │   │   └── CharacterLevel (Label)
│   │   ├── ItemGrid (GridContainer)
│   │   │   └── Slot_1 ~ Slot_24 (TextureRect)
│   │   ├── GoldLabel (Label)
│   │   └── CloseButton (Button)
│
└── inventory_runtime.lua        # Lua 页面控制器
    ├── State Keys 定义
    ├── register_bindings()      # Binding 注册
    ├── OnInit / OnShow / OnRefresh / OnHide / OnDispose
    └── 页面逻辑实现
```

---

## 3. 详细工作流程

### 3.1 Step 1: AI 生成原型初稿

**目标**：从自然语言快速生成可编辑的 UI 布局原型。

#### 3.1.1 工具选择

| 工具 | 特点 | 导出格式 | 推荐场景 |
|-----|------|---------|---------|
| **Magic Patterns** | Figma 插件，文字生成 UI | Figma 文件 | Figma 用户 |
| **Figma First Draft** | Figma 官方 AI | Figma 文件 | Figma 用户 |
| **Google Stitch** | Google Labs 实验工具 | 图片/Figma | 快速概念验证 |
| **UXMagic** | 文字/截图转设计 | Figma/Penpot | 多种输入源 |
| **即时设计 AI** | 中文友好 | 即时设计文件 | 中文团队 |

#### 3.1.2 推荐 Prompt 模板

```
【游戏 UI 设计需求】

游戏类型: [RPG/SLG/卡牌/射击等]
界面类型: [背包/商店/技能树/主菜单/HUD等]

布局结构:
- 顶部: [标题栏/资源栏/导航]
- 左侧: [主要信息区]
- 中间: [核心内容区]
- 右侧: [辅助信息/操作区]
- 底部: [操作按钮/状态栏]

风格描述:
- 主题: [科幻/奇幻/废土/二次元/像素等]
- 配色: [主色/辅色/背景色]
- 质感: [金属/玻璃/羊皮纸/全息等]
- 字体: [科技感/手写体/像素字]

交互要求:
- [按钮点击效果]
- [列表滚动方式]
- [弹窗出现动画]

约束:
- 使用 Auto Layout / Flex 布局
- 图层命名使用英文，采用 camelCase
- 交互元素单独成组
```

#### 3.1.3 输出要求

AI 生成的原型应满足：
- ✅ 使用布局系统（Flex/Grid/Auto Layout）
- ✅ 图层有清晰命名（便于 Facet 节点解析）
- ✅ 基础层级结构正确
- ❌ 不需要精细视觉（后续替换美术资源）

---

### 3.2 Step 2: 原型工具精调

**目标**：在原型工具中精调布局，确保结构正确。

#### 3.2.1 Penpot 操作指南（推荐）

**为什么是 Penpot？**
- 完全开源（MPL-2.0）
- 可自托管
- 开放文件格式（ZIP + JSON + SVG）
- 免费开发者工具（Inspect 面板）
- 原生支持 Design Tokens

**精调检查清单：**

```markdown
□ 布局结构
  □ 使用 Board（画板）定义页面根节点
  □ 使用 Flex Layout 定义水平/垂直排列
  □ 使用 Grid Layout 定义网格（如背包格子）
  □ 设置合理的间距（padding/gap）

□ 节点命名规范（关键！）
  □ 使用英文 camelCase 命名
  □ 格式: {组件类型}{功能}{状态}
    - 示例: CharacterPanel, ItemGrid, CloseButton
  □ 避免特殊字符和空格
  □ 保持命名与 Facet 代码中的节点 Key 一致

□ 交互标注
  □ 为可点击元素添加注释
  □ 使用 Prototype 功能连接页面跳转
  □ 标注特殊状态（hover, disabled, selected）

□ 样式规范
  □ 使用 Design Tokens 定义颜色
  □ 使用 Text Styles 定义字体层级
  □ 标注需要替换的美术资源
```

#### 3.2.2 布局映射规则

| Penpot 布局 | Godot 对应节点 | Facet 用途 |
|------------|---------------|-----------|
| Flex Layout (Vertical) | VBoxContainer | 垂直排列区域 |
| Flex Layout (Horizontal) | HBoxContainer | 水平排列区域 |
| Grid Layout | GridContainer | 网格布局（背包等）|
| Board (Frame) | PanelContainer / Control | 页面/面板容器 |
| Rectangle | ColorRect / Panel | 背景/装饰 |
| Text | Label | 文本显示 |
| Image | TextureRect | 图片显示 |
| Component Instance | 预置场景实例 | 可复用组件 |

---

### 3.3 Step 3: 导出结构化数据

**目标**：从原型工具导出机器可读的布局数据。

#### 3.3.1 Penpot 导出方式

**方式 A: 标准文件导出（推荐 MVP）**

```bash
# 1. Penpot 中导出为 .penpot 文件（实际是 ZIP）
# 2. 解压获取内部结构
unzip design.penpot -d design_extracted/

# 内部结构
design_extracted/
├── manifest.json          # 文件元数据
├── pages/
│   └── page-1.json        # 完整节点树（关键！）
├── shapes/                # SVG 矢量数据
└── media/                 # 图片资源
```

**page-1.json 关键字段：**

```json
{
  "name": "InventoryPage",
  "type": "board",
  "x": 0, "y": 0,
  "width": 1920,
  "height": 1080,
  "layout": {
    "type": "flex",
    "dir": "row",
    "gap": 16,
    "padding": 24
  },
  "shapes": [
    {
      "name": "CharacterPanel",
      "type": "board",
      "layout": { "type": "flex", "dir": "column" },
      "shapes": [
        { "name": "CharacterImage", "type": "rect" },
        { "name": "CharacterName", "type": "text" }
      ]
    }
  ]
}
```

**方式 B: 使用 penpot-export CLI**

```bash
# 安装官方 CLI
npm install -g @penpot-export/cli

# 配置 penpot-export.config.js
module.exports = {
  instance: 'https://your-penpot.com',
  accessToken: process.env.PENPOT_TOKEN,
  files: [{
    fileId: 'your-file-id',
    pages: [{
      pageId: 'your-page-id',
      format: 'json',
      output: 'facet-export/page-structure.json'
    }]
  }]
};

# 执行导出
npx penpot-export
```

**方式 C: 开发专用 Facet 插件（长期目标）**

开发 Penpot 插件直接导出 Facet 格式，见第 6 章。

---

### 3.4 Step 4: AI 生成 Facet 代码

**目标**：将结构化数据转换为 Facet 框架代码。

#### 3.4.1 输入数据格式

```json
{
  "facetExport": {
    "version": "1.0",
    "pageId": "client.inventory",
    "pageName": "InventoryPage",
    "nodes": [
      {
        "key": "InventoryRoot",
        "type": "root",
        "rect": { "x": 0, "y": 0, "w": 1920, "h": 1080 },
        "layout": { "type": "flex", "dir": "row" },
        "children": [
          {
            "key": "CharacterPanel",
            "type": "panel",
            "rect": { "x": 24, "y": 24, "w": 400, "h": 800 },
            "layout": { "type": "flex", "dir": "column", "gap": 16 },
            "children": [
              { "key": "CharacterImage", "type": "image", "rect": {...} },
              { "key": "CharacterName", "type": "label", "rect": {...} }
            ]
          },
          {
            "key": "ItemGrid",
            "type": "grid",
            "rect": { "x": 448, "y": 24, "w": 800, "h": 600 },
            "layout": { "type": "grid", "cols": 6, "rows": 4, "gap": 10 },
            "children": [
              { "key": "Slot_1", "type": "slot", ... },
              ...
            ]
          }
        ]
      }
    ],
    "bindings": [
      { "nodeKey": "CharacterName", "type": "text", "stateKey": "character.name" },
      { "nodeKey": "GoldLabel", "type": "text", "stateKey": "inventory.gold" },
      { "nodeKey": "CloseButton", "type": "command", "command": "inventory.close" },
      { "nodeKey": "ItemGrid", "type": "list", "stateKey": "inventory.items" }
    ]
  }
}
```

#### 3.4.2 AI Prompt 模板

见第 5 章完整 Prompt 模板库。

---

## 4. 工具选型分析

### 4.1 原型工具对比

| 特性 | Penpot (推荐) | Figma | 即时设计 |
|-----|--------------|-------|---------|
| **许可证** | MPL-2.0 开源 | 闭源商业 | 闭源商业 |
| **费用** | 完全免费 | 免费版受限 | 免费版受限 |
| **自托管** | ✅ Docker | ❌ | ❌ |
| **文件格式** | ZIP+JSON+SVG（开放）| 二进制闭源 | 私有格式 |
| **插件开发** | JavaScript/TypeScript | JavaScript | JavaScript |
| **AI 集成** | 需外部工具 | First Draft 等内置 | 内置 AI |
| **开发者模式** | ✅ 免费 | ❌ 付费 | ✅ 免费 |
| **MPL-2.0 限制** | 可接受 | 无 | 无 |
| **中文支持** | 一般 | 一般 | ✅ 优秀 |

### 4.2 推荐结论

**首选: Penpot**
- 开源可定制，可开发 Facet 专用插件
- 开放格式，AI 可直接读取 JSON 结构
- 无商业使用限制，适合长期发展
- 开发者友好（Flex/Grid 直接对应 CSS）

**备选: 即时设计**
- 如果团队中文需求强
- 国内访问速度快
- 但插件生态不如 Figma/Penpot 成熟

### 4.3 MPL-2.0 许可证说明

**对 Facet 项目的影响：**

| 场景 | 要求 |
|-----|------|
| 仅使用 Penpot | 无限制，自由使用 |
| 修改 Penpot 源码 | 需开源修改的文件（通常不需要）|
| 开发 Penpot 插件 | 插件文件可用任意许可证（包括闭源）|
| 自托管 Penpot | 无限制，无需公开 |

**结论**: MPL-2.0 对游戏项目几乎无影响，可放心使用。

---

## 5. AI Prompt 模板库

### 5.1 Prompt 1: Penpot 结构转 Facet 页面定义

```
你是一位 Facet 框架专家。请根据以下 Penpot 导出的 JSON 结构，生成 Facet 的 UIPageDefinition C# 代码。

【输入数据】
```json
{PASTE_PENPOT_JSON_HERE}
```

【Facet UIPageDefinition 规范】
- 命名空间: Sideline.Facet.Runtime
- 类名: 使用 {PageName}PageDefinition
- pageId 格式: "client.{lowercase_page_name}"
- layoutType: Scene（手工布局场景）
- layoutPath: "res://ui/scenes/{page_name}.tscn"
- layer: 根据页面类型选择 "Main" / "Popup" / "Overlay"
- cachePolicy: KeepAlive 或 Discard

【输出要求】
生成完整的 C# 代码，包含：
1. using 语句
2. 命名空间声明
3. 类定义和构造函数
4. 所有属性的正确赋值

【示例输出】
```csharp
using Sideline.Facet.Runtime;

namespace Sideline.Facet.Pages
{
    public static class InventoryPageDefinition
    {
        public static UIPageDefinition Create()
        {
            return new UIPageDefinition(
                pageId: "client.inventory",
                layoutType: UIPageLayoutType.Scene,
                layoutPath: "res://ui/scenes/inventory.tscn",
                layer: "Main",
                cachePolicy: UIPageCachePolicy.KeepAlive,
                controllerScript: "inventory_runtime"
            );
        }
    }
}
```
```

### 5.2 Prompt 2: 生成 Godot 场景文件 (.tscn)

```
请根据以下 Penpot 结构生成 Godot 4.x 的 .tscn 场景文件。

【输入数据】
```json
{PASTE_STRUCTURE_HERE}
```

【映射规则】
1. Penpot Board (Frame) → Godot PanelContainer 或 Control
2. Penpot Flex Layout (Vertical) → VBoxContainer
3. Penpot Flex Layout (Horizontal) → HBoxContainer
4. Penpot Grid Layout → GridContainer
5. Penpot Text → Label (使用 Theme 设置字体)
6. Penpot Rectangle/Image → TextureRect 或 ColorRect
7. Penpot Button → Button

【布局属性映射】
- Flex gap → 对应 Container 的 Separation 或子节点 Margin
- Flex padding → 父节点的 Margin
- Grid cols/rows → GridContainer 的 Columns

【输出要求】
1. 生成完整的 .tscn 格式（类似 INI 的 Godot 场景格式）
2. 使用正确的 node paths 和类型
3. 节点名必须与 Penpot 图层名一致（Facet 节点解析依赖）
4. 添加锚点设置（Anchor）适配不同分辨率

【输出格式】
```
[gd_scene load_steps=1 format=3 uid="uid://..."]

[node name="InventoryRoot" type="Control"]
layout_mode = 3
anchors_preset = 15
anchor_right = 1.0
anchor_bottom = 1.0
...

[node name="CharacterPanel" type="PanelContainer" parent="."]
layout_mode = 1
...
```
```

### 5.3 Prompt 3: 生成 Lua 控制器

```
请为 Facet 框架生成 Lua 页面控制器代码。

【页面信息】
- 页面 ID: {PAGE_ID}
- 页面名称: {PAGE_NAME}
- 功能描述: {FUNCTION_DESCRIPTION}

【节点列表】
| 节点 Key | 类型 | 用途 | Binding 类型 |
|---------|------|------|-------------|
| CharacterName | Label | 角色名称 | text |
| CharacterLevel | Label | 角色等级 | text |
| CharacterImage | TextureRect | 角色头像 | texture |
| ItemGrid | GridContainer | 物品网格 | list |
| GoldLabel | Label | 金币数量 | text |
| CloseButton | Button | 关闭按钮 | command |

【Facet Lua API 参考】
```lua
-- 获取页面级 Binding
local page = api:GetPageBindings()

-- 文本绑定
page:BindStateText(nodeKey, stateKey, fallback)

-- 显隐绑定
page:BindStateVisibility(nodeKey, stateKey, fallback)

-- 交互状态绑定
page:BindStateInteractable(nodeKey, stateKey, fallback)

-- 命令绑定
page:BindCommand(nodeKey, commandKey)

-- 获取组件级 Binding（用于列表等复杂组件）
local component = api:GetComponentBindings(componentId, rootKey)

-- 结构化列表绑定
component:BindStateStructuredList(
    containerKey,
    templateKey,
    stateKey,
    primaryLabelKey,
    secondaryLabelKey,
    tertiaryLabelKey,
    emptyLabelKey
)

-- 状态存取
api:SetStateString(key, value)
api:GetStateString(key, fallback)
api:SetStateNumber(key, value)
api:GetStateNumber(key, fallback)
api:SetStateBoolean(key, value)
api:GetStateBoolean(key, fallback)

-- 页面路由
api:TryOpenPage(pageId, arguments, pushHistory)
api:TryGoBack()

-- 日志
api:LogInfoText(message)
```

【输出要求】
生成完整的 Lua 文件，包含：
1. State Key 常量定义（使用 facet.{page}.{field} 格式）
2. register_bindings() 函数
3. 完整的生命周期函数（OnInit, OnShow, OnRefresh, OnHide, OnDispose）
4. 适当的注释说明

【代码模板】
```lua
local reload_key = "facet.{page}.reload_count"
-- 其他 state keys...

local function register_bindings(api)
    local page = api:GetPageBindings()
    if page == nil then
        return
    end
    
    -- 在这里注册所有 bindings
    
    page:Refresh("lua.{page}.bindings_registered")
end

function OnInit(api)
    api:SetStateNumber(reload_key, 0)
    register_bindings(api)
    api:LogInfoText("{Page} Lua OnInit")
end

function OnShow(api)
    api:LogInfoText("{Page} Lua OnShow")
end

function OnRefresh(api)
    local current = api:GetStateNumber(reload_key, 0) + 1
    api:SetStateNumber(reload_key, current)
    
    local page = api:GetPageBindings()
    if page ~= nil then
        page:Refresh("lua.{page}.refresh")
    end
    
    api:LogInfoText("{Page} Lua OnRefresh count=" .. tostring(current))
end

function OnHide(api)
    api:LogInfoText("{Page} Lua OnHide")
end

function OnDispose(api)
    api:LogInfoText("{Page} Lua OnDispose")
end
```
```

### 5.4 Prompt 4: 完整工作流（一体化）

```
请作为 Facet AI 工作流引擎，完成从 Penpot JSON 到 Facet 代码的完整转换。

【输入：Penpot 导出 JSON】
```json
{PASTE_FULL_JSON}
```

【任务】
分析上述结构，生成 Facet 框架所需的三个文件：

1. UIPageDefinition.cs - C# 页面定义
2. {PageName}.tscn - Godot 场景文件
3. {PageName}_runtime.lua - Lua 控制器

【输出格式】
使用以下分隔符输出三个文件：

=== File: UIPageDefinition.cs ===
[代码内容]

=== File: {PageName}.tscn ===
[代码内容]

=== File: {PageName}_runtime.lua ===
[代码内容]

=== Summary ===
| 节点 Key | Godot 类型 | Binding 类型 | 说明 |
|---------|-----------|-------------|------|
| ... | ... | ... | ... |
```

---

## 6. Penpot 插件开发规范

### 6.1 插件架构设计

开发 Facet 专用导出插件，直接生成 Facet 格式代码。

```
penpot-facet-plugin/
├── manifest.json           # 插件清单
├── plugin.html            # 插件 UI
├── plugin.js              # 插件逻辑
├── src/
│   ├── parser.ts          # Penpot 结构解析
│   ├── generator/
│   │   ├── csharp.ts      # C# 代码生成
│   │   ├── godot.ts       # Godot 场景生成
│   │   └── lua.ts         # Lua 代码生成
│   └── exporter.ts        # 文件导出
└── package.json
```

### 6.2 manifest.json

```json
{
  "name": "Facet Export for Godot",
  "description": "Export Penpot designs to Facet framework (Godot + Lua)",
  "code": "/plugin.js",
  "icon": "/icon.png",
  "permissions": [
    "content:read",
    "file:read",
    "allow:downloads"
  ]
}
```

### 6.3 核心解析逻辑

```typescript
// parser.ts
interface FacetNode {
  key: string;
  type: 'root' | 'panel' | 'label' | 'button' | 'image' | 'list' | 'grid';
  rect: { x: number; y: number; w: number; h: number };
  layout?: {
    type: 'flex' | 'grid';
    dir?: 'row' | 'column';
    gap?: number;
    padding?: number;
    cols?: number;
    rows?: number;
  };
  children?: FacetNode[];
}

function parsePenpotShape(shape: Shape): FacetNode {
  return {
    key: shape.name,
    type: mapShapeType(shape.type),
    rect: {
      x: shape.x,
      y: shape.y,
      w: shape.width,
      h: shape.height
    },
    layout: shape.layout ? {
      type: shape.layout.type,
      dir: shape.layout.dir,
      gap: shape.layout.gap,
      padding: shape.layout.padding,
      cols: shape.layout.cols,
      rows: shape.layout.rows
    } : undefined,
    children: shape.children?.map(parsePenpotShape)
  };
}

function mapShapeType(type: string): FacetNode['type'] {
  const mapping: Record<string, FacetNode['type']> = {
    'board': 'panel',
    'text': 'label',
    'rect': 'image',
    'button': 'button',
    'group': 'panel'
  };
  return mapping[type] || 'panel';
}
```

### 6.4 代码生成器

```typescript
// generator/lua.ts
export function generateLuaController(
  pageName: string,
  nodes: FacetNode[],
  bindings: Binding[]
): string {
  const stateKeys = generateStateKeys(nodes, bindings);
  const bindingCode = generateBindingCode(bindings);
  
  return `-- ${pageName} Runtime Controller
-- Auto-generated by Facet Exporter

${stateKeys}

local function register_bindings(api)
    local page = api:GetPageBindings()
    if page == nil then
        return
    end

${bindingCode}

    page:Refresh("lua.${pageName}.bindings_registered")
end

function OnInit(api)
    api:SetStateNumber(reload_key, 0)
    register_bindings(api)
    api:LogInfoText("${pageName} Lua OnInit")
end

function OnShow(api)
    api:LogInfoText("${pageName} Lua OnShow")
end

function OnRefresh(api)
    local current = api:GetStateNumber(reload_key, 0) + 1
    api:SetStateNumber(reload_key, current)
    
    local page = api:GetPageBindings()
    if page ~= nil then
        page:Refresh("lua.${pageName}.refresh")
    end
    
    api:LogInfoText("${pageName} Lua OnRefresh count=" .. tostring(current))
end

function OnHide(api)
    api:LogInfoText("${pageName} Lua OnHide")
end

function OnDispose(api)
    api:LogInfoText("${pageName} Lua OnDispose")
end
`;
}
```

---

## 7. 实现路线图

### Phase 1: MVP（2-4 周）

**目标**: 验证工作流可行性

| 任务 | 负责人 | 产出 |
|-----|-------|------|
| 部署 Penpot 自托管实例 | DevOps | 内网 Penpot 服务 |
| 建立 AI Prompt 模板库 | 开发者 | 3 个核心 Prompt 模板 |
| 手动验证完整工作流 | 开发者 | 1 个完整 UI 页面示例 |
| 编写本文档 | 团队 | 完整工作流文档 |

**验证流程**:
1. 用 Magic Patterns 生成初稿 → 导入 Penpot
2. 在 Penpot 中精调布局和命名
3. 导出标准文件并解压获取 JSON
4. 使用 AI Prompt 生成 Facet 代码
5. 集成到 Godot 项目并测试热重载

### Phase 2: 工具化（4-8 周）

**目标**: 减少人工步骤，提高效率

| 任务 | 产出 |
|-----|------|
| 开发 Penpot Facet 插件 v0.1 | 一键导出 Facet JSON |
| 开发 AI 代码生成服务 | 本地/云端 API，输入 JSON 输出代码 |
| 建立项目模板 | Facet UI 页面模板库 |
| 编写插件使用文档 | 团队内部 Wiki |

**技术栈**:
- Penpot 插件: TypeScript + Vite
- AI 服务: Python + FastAPI + Claude API / OpenAI API
- 模板管理: Git 子模块或 NPM 包

### Phase 3: 自动化（8-12 周）

**目标**: 完整自动化 pipeline

| 任务 | 产出 |
|-----|------|
| 集成 AI 到 Penpot 插件 | 插件内直接调用 AI 生成代码 |
| 实现 Godot 自动导入 | 代码生成后自动同步到 Godot 项目 |
| 开发 UI 组件库 | 常用游戏 UI 组件（按钮、列表、弹窗）|
| 建立设计规范 | 团队 UI/UX 设计标准文档 |

**自动化流程**:
```
Penpot 设计完成
    ↓ 点击 "Export to Facet"
    ↓ 插件调用 AI 服务
    ↓ 生成三层代码
    ↓ 自动写入 Godot 项目目录
    ↓ 触发 Facet 热重载
    ↓ 在 Godot 中实时预览
```

### Phase 4: 优化与扩展（12+ 周）

- 支持更多原型工具（Figma、即时设计）
- 开发可视化 Binding 编辑器
- 支持动画参数导出
- 集成版本控制（设计稿与代码版本对应）

---

## 8. 附录

### 附录 A: 命名规范

**Penpot 图层命名**:
```
{Component}{Function}{State}

示例:
- CharacterPanel          (角色面板)
- CharacterImage          (角色头像)
- CharacterNameLabel      (角色名称标签)
- ItemGrid                (物品网格)
- Slot_1 ~ Slot_24        (格子 1-24)
- GoldLabel               (金币标签)
- GoldIcon                (金币图标)
- CloseButton             (关闭按钮)
- CloseButton_Hover       (关闭按钮-悬停状态)
```

**State Key 命名**:
```
facet.{page}.{field}

示例:
- facet.inventory.gold
- facet.inventory.items
- facet.inventory.selected_slot
- facet.character.name
- facet.character.level
```

### 附录 B: 布局参数映射表

| Penpot 属性 | Godot 属性 | 说明 |
|------------|-----------|------|
| x, y | position | 相对父节点位置 |
| width, height | size | 尺寸 |
| layout: flex, dir: row | HBoxContainer | 水平排列 |
| layout: flex, dir: column | VBoxContainer | 垂直排列 |
| layout: grid | GridContainer | 网格布局 |
| gap | separation / theme_override | 间距 |
| padding | margin (container) | 内边距 |
| horizontal-align | alignment (horizontal) | 水平对齐 |
| vertical-align | alignment (vertical) | 垂直对齐 |

### 附录 C: 常见问题

**Q: Penpot 生成的布局在 Godot 中显示不一致？**
A: 确保 Penpot 和 Godot 使用相同的锚点设置。建议使用 Anchor Preset 实现响应式布局。

**Q: 如何处理复杂的 UI 动画？**
A: Penpot 仅负责静态布局。动画在 Godot 中通过 AnimationPlayer 或 Tween 实现，可在 Lua 代码中触发动画。

**Q: 是否支持 9-slice 九宫格图片？**
A: Penpot 本身不直接支持 9-slice 设计。建议在 Penpot 中标注"需要九宫格"，在 Godot 中使用 NinePatchRect 节点。

**Q: 如何处理 UI 多语言？**
A: 使用 Facet 的 Projection 系统，将文本内容放入 Projection，Lua 中通过 Binding 绑定到 Label，根据语言设置切换 Projection 数据。

### 附录 D: 参考资源

**Penpot**:
- 官网: https://penpot.app
- GitHub: https://github.com/penpot
- 插件开发文档: https://help.penpot.app/plugins/
- API 参考: https://penpot.app/technical-guide/

**Facet 框架**:
- 项目路径: `D:\UGit\Sideline\godot\scripts\facet`
- README: `D:\UGit\Sideline\godot\scripts\facet\README.md`

**AI 工具**:
- Magic Patterns: https://magicpatterns.com
- Claude API: https://docs.anthropic.com

---

**文档结束**

如有问题或建议，请在团队会议中讨论或更新此文档。
