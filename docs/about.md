---
layout: default
title: 关于游戏
description: 了解 Sideline 的核心玩法、设计理念和开发计划
---

<section class="hero">
    <h1>关于 SIDELINE</h1>
    <p class="hero-subtitle">工作间隙的地下冒险</p>
</section>

<div class="content-container">

## 🎮 游戏理念

**Sideline** 是一款专为"打工人"设计的 2D 俯视角独立游戏。

灵感来源于一个简单的问题：*"如果能在工作间隙偷偷经营一个地下世界会怎样？"*

游戏的核心体验是**双重生活**——表面上你在电脑前认真工作，实际上屏幕角落里有一个小窗口，你的地下王国正在悄然运转。

---

## 🕹️ 双模式设计

### 挂机模式 (Idle Mode)

<div class="pixel-card pixel-card-green">

**窗口规格**
- 尺寸: 400 x 300 像素
- 无边框设计
- 始终置顶显示
- 支持拖拽移动
- 默认位置: 屏幕右下角

**核心功能**
- 资源自动收集（金币、材料、经验）
- 离线收益计算
- 角色养成状态概览
- 任务进度追踪
- 通知提醒系统

</div>

**设计理念**：挂机模式应该像一个"宠物"，安静地在角落陪伴你，不会干扰工作，但偶尔会让你会心一笑。

### 刷宝模式 (Dungeon Mode)

<div class="pixel-card pixel-card-gold">

**窗口规格**
- 尺寸: 1280 x 720 (支持全屏)
- 标准窗口边框
- 不强制置顶
- 沉浸式体验

**核心玩法**
- Roguelike 地下城探索
- 实时战斗系统
- 装备刷取与打造
- 随机地图生成 (BSP 算法)
- 多样的敌人与 Boss

</div>

**设计理念**：刷宝模式是真正的"下班后的冒险"，提供完整的动作 RPG 体验。

---

## 📅 开发路线图

### Phase 0 — 技术验证 <span class="tag tag-green">当前阶段</span>

- [x] Godot 4 无边框窗口原型
- [ ] 最小 ECS 框架（Entity / Component / System）
- [ ] FP 定点数库验证
- [ ] 状态快照与回放系统

### Phase 1 — 核心玩法

- [ ] 随机地图生成（BSP 算法）
- [ ] 基础战斗系统
- [ ] 装备系统 + 存档功能
- [ ] 挂机资源收集完整实现
- [ ] 音效与音乐

### Phase 2 — EA 发布准备

- [ ] Steam SDK 接入
- [ ] 战斗回放系统
- [ ] 内容填充（地图、敌人、装备）
- [ ] 平衡性调整
- [ ] EA 版本发布

### Phase 3 — 联机 DLC

- [ ] Lattice 网络层实现
- [ ] Lockstep 帧同步
- [ ] Steam Relay 集成
- [ ] 多人地下城
- [ ] 联机模式发布

---

## 🏗️ 技术架构

### 分层设计

```
┌──────────────────────────────────────┐
│          渲染层（Godot 4）            │
│  Node2D 纯渲染，无游戏逻辑            │
├──────────────────────────────────────┤
│       GodotRenderBridge              │
│  每帧同步 SimulationWorld 状态到 Node │
├──────────────────────────────────────┤
│       SimulationWorld（Lattice）      │
│  Entity / Component / System         │
│  FixedPoint Math / 确定性物理         │
│  StateSnapshot / InputBuffer         │
├──────────────────────────────────────┤
│       网络层（Phase 3）               │
│  Lockstep / Steam Relay              │
└──────────────────────────────────────┘
```

### 为什么选择自研 ECS？

1. **确定性**: 需要完全可重现的战斗过程，用于回放和联机同步
2. **帧同步**: 计划中的联机模式需要 Lockstep 架构
3. **学习**: 深入理解游戏引擎底层原理
4. **定制化**: 针对 2D 俯视角游戏优化

---

## 👤 关于开发者

Sideline 是一个**个人独立游戏项目**，由热爱游戏的开发者利用业余时间创作。

**开发理念**
- 专注核心玩法，拒绝商业化套路
- 技术驱动，追求代码质量
- 倾听社区反馈，持续迭代

**联系方式**
- 📧 邮箱: contact@sideline.game
- 🐙 GitHub: [项目仓库](https://github.com)
- 🎮 Steam: [愿望单](https://store.steampowered.com)

---

## 🙏 特别感谢

- **Godot 社区** - 开源游戏引擎的力量
- **JetBrains** - Rider IDE 让 C# 开发更愉悦
- **所有支持者** - 你们的关注是开发的最大动力

---

<div style="text-align: center; margin-top: 60px;">
    <h3 style="color: var(--color-accent-gold);">准备好开始你的地下冒险了吗？</h3>
    <a href="{{ '/' | relative_url }}#download" class="pixel-btn pixel-btn-primary" style="margin-top: 20px;">立即下载</a>
</div>

</div>
