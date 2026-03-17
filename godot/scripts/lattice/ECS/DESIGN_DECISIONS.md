# Lattice ECS 设计决策记录

## 1. ComponentSet 优化决策

### 1.1 命名对齐

**决策：** 操作方法名称与 FrameSync 对齐
- `Contains` → `IsSet`（主方法）
- 保留 `Contains` 作为别名（向后兼容）

**原因：**
- FrameSync 使用 `IsSet` 表达"位是否被设置"的语义
- 与位图底层实现更贴合

### 1.2 子集优化

**决策：** 实现 `ComponentSet64` 和 `ComponentSet256`

**性能对比：**

| 操作 | ComponentSet (512位) | ComponentSet64 | 提升 |
|------|---------------------|----------------|------|
| IsSupersetOf | 8 次比较 | 1 次比较 | **8x** |
| UnionWith | 8 次或运算 | 1 次或运算 | **8x** |
| 内存占用 | 64 字节 | 8 字节 | **8x** |

**使用场景：**
- 前 64 个组件类型（最常见）：使用 `ComponentSet64`
- 前 256 个组件类型：使用 `ComponentSet256`
- 完整 512 个：使用 `ComponentSet`

### 1.3 预计算优化对比

**FrameSync 方式（运行时计算）：**
```csharp
public bool IsSet(int index) {
    return (Set[index / 64] & (1L << index % 64)) != 0;
    // 指令：DIV + MOD + SHL + AND + CMP
    // 延迟：~10-20 周期（除法）
}
```

**Lattice 方式（预计算）：**
```csharp
public bool IsSet<T>() {
    return (Set[ComponentTypeId<T>.BlockIndex] & ComponentTypeId<T>.BitMask) != 0;
    // 指令：MOV + AND + CMP（BlockIndex/BitMask 是编译期常量）
    // 延迟：~3-5 周期
}
```

**结论：** Lattice 方式快 **2-4 倍**，内存代价可忽略（1000 类型 × 32 字节 = 32KB）

---

## 2. ComponentTypeId 存储架构

### 2.1 存储方式对比

| 维度 | FrameSync（分离数组） | Lattice（结构体数组） | 结论 |
|------|----------------------|----------------------|------|
| 缓存局部性 | ❌ 分散存储 | ✅ 连续存储 | Lattice 更优 |
| 访问模式 | 多次内存加载 | 一次加载所有字段 | Lattice 更优 |
| GC 压力 | 多个数组对象 | 单个数组对象 | Lattice 稍优 |
| 代码可读性 | 分散访问 | 面向对象 | Lattice 更优 |

### 2.2 线程安全

**FrameSync：** 单线程假设（游戏启动时注册）
**Lattice：** 双检锁（支持运行时动态注册）

**结论：** Lattice 方案更安全，性能代价极低

---

## 3. 稀疏映射管理位置

### 3.1 架构对比

**FrameSync 方式（FrameBase 集中管理）：**
```
FrameBase
├── EntityData[] (包含 ComponentSet)
├── 稀疏映射（统一的 Entity->Component 映射）
└── 直接访问所有组件集合
```

**Lattice 方式（Storage 分散管理）：**
```
Frame
├── 实体管理（EntityRegistry）
├── 组件集合数组（Entity->ComponentSet）
└── Storage 字典（Type->Storage）
    └── 每个 Storage 有自己的稀疏映射
```

### 3.2 性能分析

| 操作 | FrameSync（集中） | Lattice（分散） | 差距 |
|------|------------------|----------------|------|
| 获取实体组件集合 | O(1) 直接访问 | O(1) 数组访问 | **无差距** |
| 查询组件存储 | 直接嵌入 | Dictionary 查找 | **~5-10ns** |
| 缓存友好性 | 实体数据连续 | 存储分散 | **FrameSync 略优** |
| 遍历所有组件 | 一次线性扫描 | 需遍历多个 Storage | **FrameSync 更优** |

**实际性能差距：**
- 随机访问组件：Lattice 慢 5-10ns（可忽略）
- 批量遍历：Lattice 需要多遍历一层 Dictionary（轻微影响）
- 内存占用：Lattice 每个 Storage 有独立稀疏数组（多 10-20%）

### 3.3 设计取舍

**保持当前架构的原因：**
1. **更清晰的分层** - Frame 协调，Storage 负责数据
2. **更好的扩展性** - 可替换存储后端（如将来支持 GPU）
3. **更易于测试** - Storage 可独立单元测试
4. **Godot 适配** - 不需要像 Unity 那样紧密集成 Native 代码

**如果追求极致性能：**
可考虑将稀疏映射上提到 Frame 级别，类似 FrameSync 的 `EntityData` 数组。

---

## 4. 序列化代码生成方案

### 4.1 C# Source Generator vs 自定义 DSL

| 维度 | Source Generator | 自定义 DSL |
|------|------------------|------------|
| 运行时性能 | ✅ 零开销 | ✅ 零开销 |
| 跨平台一致性 | ✅ 确定性强 | ⚠️ 依赖 DSL 编译器 |
| IDE 支持 | ✅ 完美 | ❌ 需自建 |
| 维护成本 | ✅ 低 | ❌ 高 |
| 表达能力 | ⚠️ 受限于 C# 语法 | ✅ 可自定义语法 |

### 4.2 决策

**Phase 1：** 使用 C# Source Generator
- 立即可用
- 零额外工具链
- 与 Godot/.NET 8 无缝集成

**Phase 2（可选）：** 添加 DSL 编译器
- 将 `.qtn` 编译为 C#
- Source Generator 继续处理生成的 C#
- 两种方案共存

---

## 5. 生命周期回调命名

**已与 FrameSync 对齐：**

| FrameSync | Lattice | 状态 |
|-----------|---------|------|
| `OnAdded` | `OnAdded` | ✅ |
| `OnRemoved` | `OnRemoved` | ✅ |
| `Set` | `Set<T>()` | ✅ 别名 |
| `Remove` | `Remove<T>()` | ✅ 别名 |

---

## 6. 实体引用对齐

**已与 FrameSync EntityRef 对齐：**

| 特性 | FrameSync | Lattice | 状态 |
|------|-----------|---------|------|
| 内存布局 | Index+Version+Raw | Index+Version+Raw | ✅ |
| 快速比较 | Raw == other.Raw | Raw == other.Raw | ✅ |
| 无效值 | EntityRef.None | Entity.None | ✅ |
| 字符串格式 | E.{Index}.{Version} | E.{Index}.{Version} | ✅ |
| 解析 | TryParse | TryParse | ✅ |

---

## 7. 未来扩展建议

### 7.1 性能优化方向

1. **稀疏映射上提** - 如果 profiling 显示 Dictionary 查找是瓶颈
2. **SIMD 批量操作** - 组件遍历使用 Vector<T>
3. **内存池** - 实体和组件的预分配池

### 7.2 功能扩展

1. **DSL 支持** - 可选的 `.qtn` 文件支持
2. **多线程系统** - Job System 集成
3. **网络同步** - 自动序列化标记

---

*最后更新：2026-03-17*
