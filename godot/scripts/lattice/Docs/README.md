# Lattice 文档目录

本目录包含 Lattice 确定性 ECS 框架的设计文档和使用指南。

## 文档列表

| 文档 | 说明 |
|------|------|
| [HotReloadArchitecture.md](./HotReloadArchitecture.md) | 热重载架构设计方案 - 基于 Quantum 式 Stateless ECS 的热重载技术选型与实现方案 |

## 设计原则

Lattice 遵循以下设计原则：

1. **Stateless Systems** - System 不持有可变状态，便于热重载和确定性验证
2. **Data in Frame** - 所有游戏状态存储在 Frame，支持 Checkpoint/Restore
3. **Deterministic First** - 优先保证确定性，支持预测/回滚网络同步
4. **Performance Critical** - 使用 `unsafe` 代码和稀疏集实现高性能 ECS

## 相关链接

- [Lattice 核心实现](../ECS/)
- [Facet UI 框架](../facet/)
