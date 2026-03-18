# Lattice ECS 架构设计（完整游戏框架版）

## 设计目标

1. **高性能**：单线程性能优先，多线程辅助
2. **预测回滚**：支持 Lockstep 帧同步（预测/回滚模型）
3. **完整生态**：序列化、裁剪、物理、网络
4. **可扩展**：支持自定义系统、组件、事件

## 分层架构

```
┌─────────────────────────────────────────────────────────────┐
│                    Game Layer（游戏层）                       │
│  - GameFrame : FrameBase                                    │
│  - GameSystems（战斗系统、经济系统）                          │
│  - Components（Position, Health, AIState...）               │
├─────────────────────────────────────────────────────────────┤
│                  Framework Layer（框架层）                    │
│  - FrameBase（核心 ECS API）                                 │
│  - CommandBuffer（命令缓冲，用于预测回滚）                     │
│  - CullingSystem（裁剪系统）                                  │
│  - Serialization（BitStream 序列化）                         │
│  - PhysicsBridge（物理集成）                                  │
├─────────────────────────────────────────────────────────────┤
│                    Core Layer（核心层）                       │
│  - Storage<T>（组件存储）                                     │
│  - EntityManager（实体管理）                                  │
│  - ComponentTypeRegistry（类型注册表）                        │
│  - Unsafe API（指针操作）                                     │
├─────────────────────────────────────────────────────────────┤
│                   Memory Layer（内存层）                      │
│  - Allocator（自定义分配器）                                  │
│  - MemorySnapshot（内存快照，用于回滚）                        │
│  - DeltaCompression（差分压缩）                               │
└─────────────────────────────────────────────────────────────┘
```

## 存储架构决策

### 为什么选择分离存储？

| 需求 | 分离存储优势 | 联合存储劣势 |
|------|-------------|-------------|
| **预测回滚** | 可复制单个组件类型（快照更小） | 必须复制整个 Block |
| **Delta 序列化** | 可针对单个类型序列化 | 需要跳过无关数据 |
| **裁剪** | 只对 Transform 等裁剪 | 所有数据一起处理 |
| **物理集成** | 物理组件独立管理 | 混合布局干扰物理引擎 |
| **网络同步** | 只同步变化组件 | 带宽浪费 |

### 核心数据结构

```csharp
// 组件存储（Core Layer）
public unsafe struct Storage<T>
{
    Block* _blocks;           // 数据块
    ushort* _sparse;          // 稀疏索引
    int _count;
    int _version;
    
    // 预测回滚支持
    int _checkpointVersion;   // 检查点版本
    
    // 裁剪支持
    BitSet256 _culled;        // 裁剪标记（可选）
}

// 框架层包装（Framework Layer）
public unsafe class ComponentBuffer<T> where T : unmanaged, IComponent
{
    Storage<T> _storage;
    
    // 序列化元数据
    ComponentSerializer<T> _serializer;
    
    // 回调系统（Reactive）
    ComponentCallbacks<T> _callbacks;
}
```

## 预测回滚支持

### 1. 命令缓冲（Command Buffer）

```csharp
public unsafe class CommandBuffer
{
    // 延迟执行的命令队列
    // - CreateEntity
    // - DestroyEntity  
    // - AddComponent
    // - RemoveComponent
    // - SetComponent
    
    public void Playback(FrameBase frame);  // 在帧开始时执行
}
```

### 2. 内存快照（Snapshot）

```csharp
public unsafe struct MemorySnapshot
{
    // 只记录变化的部分（Copy-on-Write 风格）
    // 使用 DeltaCompression 最小化快照大小
    
    public static MemorySnapshot Capture(FrameBase frame);
    public void Restore(FrameBase frame);
}
```

### 3. 确定性保证

```csharp
// 所有操作必须是确定性的
- 使用 FP（定点数）而非 float
- 不使用 Dictionary（哈希不确定）
- 使用确定性的排序算法
- 不使用多线程随机化执行顺序
```

## 序列化系统

### 1. 组件标记

```csharp
public interface IComponent : IBitStreamSerializable
{
}

// 自动生成的序列化代码
public unsafe partial struct Position : IComponent
{
    public void Serialize(BitStream stream)
    {
        stream.Serialize(ref X);
        stream.Serialize(ref Y);
    }
}
```

### 2. Delta 压缩

```csharp
public unsafe class DeltaCompressor
{
    // 只传输变化字段
    // 使用位图标记哪些字段变化了
    
    public void WriteDelta<T>(BitStream stream, T* baseline, T* current);
    public void ReadDelta<T>(BitStream stream, T* baseline, T* output);
}
```

## 裁剪系统（Culling）

### 1. 多线程安全标记

```csharp
public unsafe class CullingSystem
{
    // 使用原子操作标记裁剪状态
    // 64 个实体一组（一个 long）
    
    public void MarkCulled(int entityIndex);
    public bool IsCulled(int entityIndex);
    
    // 多线程任务
    public void ScheduleCulling(FrameBase frame, TaskContext tasks);
}
```

### 2. Filter 集成

```csharp
public ref struct Filter<T>
{
    public bool MoveNext()
    {
        while (iterator.Next(out entity, out ptr))
        {
            if (cullingSystem.IsCulled(entity.Index)) continue;
            // ...
        }
    }
}
```

## 物理集成

### 1. 物理组件特殊处理

```csharp
// 物理组件使用连续内存布局
public unsafe struct PhysicsBody
{
    public FPVector2 Position;
    public FPVector2 Velocity;
    public FP Mass;
    // ...
}

// 物理系统直接使用 Storage 的内存
public unsafe class PhysicsSystem : ISystem
{
    public void Update(FrameBase frame)
    {
        var iterator = frame.Unsafe.GetComponentBlockIterator<PhysicsBody>();
        while (iterator.NextBlock(out entities, out bodies, out count))
        {
            // 直接传递 bodies 指针给物理引擎
            PhysicsEngine.Simulate(bodies, count, frame.DeltaTime);
        }
    }
}
```

## 性能优化路径

### Phase 7: SIMD 优化
- 使用 System.Runtime.Intrinsics
- 对批量组件操作进行向量化

### Phase 8: Full-Owning Groups
- 针对频繁一起访问的组件组合
- SOA → AOS 转换，最大化缓存命中率

### Phase 9: 多线程系统
- 只读系统并行执行
- 任务窃取（Work Stealing）调度

## 与 FrameSync 的对比

| 特性 | Lattice | FrameSync |
|------|---------|-----------|
| 存储策略 | 分离（更适合回滚） | 联合 |
| 命令缓冲 | 计划实现 | ✅ 成熟 |
| 序列化 | 计划实现 | ✅ 完整 |
| 裁剪 | 计划实现 | ✅ 支持 |
| 物理集成 | 计划实现 | ✅ 支持 |
| 多线程 | Phase 9 | ✅ 支持 |
| 代码生成 | 使用 Source Generator | 使用 Qtn |

## 下一步实施建议

1. **Phase 7**: SIMD 优化（立即可做）
2. **Phase 8**: Full-Owning Groups（性能极致）
3. **Framework Layer**: 实现 CommandBuffer 和 Serialization
4. **Integration**: 物理引擎集成（Godot Physics）

核心原则是：**Core Layer 保持简洁高效，Framework Layer 提供完整功能**。
