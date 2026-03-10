# Sideline

[![License: GPL v3](https://img.shields.io/badge/License-GPLv3-blue.svg)](https://www.gnu.org/licenses/gpl-3.0)

A 2D top-down indie game featuring idle management and dungeon crawling, built with Lattice ECS framework.

**工作间隙偷偷经营地下世界的独立游戏**

---

## 🎮 游戏特色

- **双模式窗口**: 挂机模式(400x300无边框) + 刷宝模式(1280x720全屏)
- **暗黑刷宝**: Roguelike地下城，随机地图，丰富装备系统
- **挂机养成**: 离线收益，随时关注你的地下世界
- **确定性联机**: 基于自研Lattice ECS框架，支持Lockstep帧同步

## 🛠️ 技术栈

| 层级 | 技术 | 说明 |
|------|------|------|
| 渲染层 | Godot 4.6.1 + C# | 2D俯视角渲染 |
| 逻辑层 | Lattice (自研) | 确定性ECS帧同步框架 |
| 桥接层 | GodotRenderBridge | 状态同步 |
| 网络层 | Steam Relay (计划中) | Lockstep联机 |

## 🚀 快速开始

```bash
# 克隆仓库
git clone https://github.com/zgx197/Sideline.git
cd Sideline/godot

# 构建项目
dotnet build

# 运行游戏
dotnet run
```

## 📖 文档

- [项目官网](https://zgx197.github.io/Sideline/)
- [开发文档](https://zgx197.github.io/Sideline/docs/)
- [更新日志](https://zgx197.github.io/Sideline/blog/)

## 📄 许可证

本项目采用 [GNU General Public License v3.0 (GPL-3.0)](LICENSE) 许可证。

这意味着：
- ✅ 你可以自由使用、修改和分发本软件
- ✅ 任何基于本软件的衍生作品也必须使用 GPL-3.0 开源
- ❌ 不能用于闭源商业软件

---

**Copyright (C) 2026 Sideline Team**
