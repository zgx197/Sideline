# Phase 2 实现完成总结

## 新增文件

### 1. ComponentCallbacks.cs
组件回调集合 - FrameSync 风格的序列化和生命周期钩子。

```csharp
public unsafe delegate void ComponentSerializeDelegate(void* component, FrameSerializer serializer);
public unsafe delegate void ComponentChangedDelegate(Entity entity, void* component, Frame frame);

public readonly struct ComponentCallbacks
{
    public ComponentSerializeDelegate Serialize;
    public ComponentChangedDelegate? OnAdded;
    public ComponentChangedDelegate? OnRemoved;
}
```

### 2. ComponentFlags.cs
组件标志枚举 - 控制组件的行为特性。

```csharp
[Flags]
public enum ComponentFlags : uint
{
    None = 0,
    DontSerialize = 1 << 0,           // 不序列化
    Singleton = 1 << 1,               // 单例组件
    ExcludeFromPrediction = 1 << 2,   // 预测时排除
    ExcludeFromCheckpoints = 1 << 3,  // 检查点排除
    SignalOnChanged = 1 << 4,         // 变更时触发事件
    DontClearOnRollback = 1 << 5,     // 回滚时不清理
    HideInDump = 1 << 6               // 帧转储中隐藏
}
```

### 3. FrameSerializer.cs
帧序列化器基础结构 - Phase 3 将实现完整的位流序列化。

```csharp
public sealed class FrameSerializer
{
    public enum Mode { Writing, Reading }
    
    public void Serialize(ref int value);
    public void Serialize(ref FP value);
    public unsafe void Serialize(void* data, int size);
}
```

## 修改文件

### ComponentTypeId.cs - 完全重写
新增功能：
- **ID 从 1 开始**（0 保留为 null）
- **Builder 模式** - 显式注册组件类型
- **完整元数据** - Size/Flags/Callbacks/BlockIndex/BitOffset/BitMask
- **反向查找** - Type→Id, Name→Id, Id→Type

```csharp
// 新的注册方式（显式 Builder）
var builder = registry.CreateBuilder(expectedTypeCount: 64);
builder.Add<Position>(
    serialize: (void* ptr, FrameSerializer s) => { /* ... */ },
    onAdded: (entity, ptr, frame) => { /* ... */ },
    onRemoved: (entity, ptr, frame) => { /* ... */ },
    flags: ComponentFlags.None
);
builder.Add<Velocity>(...);
builder.Finish();

// 自动注册（向后兼容）
int id = ComponentTypeId<Position>.Id;  // 首次访问时自动注册
```

### Frame.cs - 集成生命周期回调
- 添加组件时调用 `OnAdded` 回调
- 移除组件时调用 `OnRemoved` 回调
- 泛型约束更新为 `unmanaged, IComponent`

### Query.cs - 更新约束
所有泛型约束从 `struct` 更新为 `unmanaged, IComponent`。

## 与 FrameSync 的对齐程度

| FrameSync 特性 | Lattice Phase 2 | 状态 |
|----------------|-----------------|------|
| ID 从 1 开始 | ✅ 实现 | 完成 |
| Builder 模式 | ✅ 实现 | 完成 |
| Size/Flags/Callbacks | ✅ 实现 | 完成 |
| ComponentCallbacks | ✅ 实现 | 完成 |
| ComponentFlags | ✅ 实现 | 完成 |
| 反向查找 | ✅ 实现 | 完成 |
| 生命周期回调 | ✅ 实现 | 完成 |

**Phase 2 完成度：100%**

## 使用示例

### 显式注册（推荐）

```csharp
// 注册所有组件类型
var registry = ComponentTypeRegistry.Global;
var builder = registry.CreateBuilder(expectedTypeCount: 10);

builder.Add<Position>(
    serialize: (void* ptr, FrameSerializer s) =>
    {
        Position* pos = (Position*)ptr;
        s.Serialize(ref pos->X);
        s.Serialize(ref pos->Y);
    },
    onAdded: (entity, ptr, frame) =>
    {
        Console.WriteLine($"Position added to entity {entity}");
    },
    flags: ComponentFlags.None
);

builder.Add<Velocity>(
    serialize: (void* ptr, FrameSerializer s) => { /* ... */ },
    flags: ComponentFlags.ExcludeFromPrediction  // 预测时排除
);

builder.Finish();
```

### 使用元数据

```csharp
// 获取类型信息
var info = registry.GetTypeInfo(id);
Console.WriteLine($"Type: {info.Name}, Size: {info.Size}, Flags: {info.Flags}");

// 获取回调
var callbacks = registry.GetTypeCallbacks(id);
callbacks.Serialize(ptr, serializer);

// 反向查找
Type type = registry.GetType(id);
int id = registry.GetTypeId("Position");
```

### 自动回调触发

```csharp
// 创建帧
var frame = new Frame(tick: 0, deltaTime: FP.One, registry);
var entity = frame.CreateEntity();

// 添加组件 - 自动触发 OnAdded
frame.AddComponent(entity, new Position { X = 10, Y = 20 });
// 输出: "Position added to entity E.00000.001"

// 移除组件 - 自动触发 OnRemoved
frame.RemoveComponent<Position>(entity);
// 输出: "Position removed from entity E.00000.001"
```

## Phase 1 + Phase 2 完整架构

```
┌─────────────────────────────────────────────────────────────┐
│                    ComponentTypeRegistry                     │
│  ┌───────────────────────────────────────────────────────┐  │
│  │  Builder Pattern                                       │  │
│  │  - Add<T>(callbacks, flags)                            │  │
│  │  - Finish()                                            │  │
│  └───────────────────────────────────────────────────────┘  │
│  ┌───────────────────────────────────────────────────────┐  │
│  │  Metadata Arrays (ID -> Info)                          │  │
│  │  - Type, Name, Size, Flags, Callbacks                  │  │
│  │  - BlockIndex, BitOffset, BitMask                      │  │
│  └───────────────────────────────────────────────────────┘  │
│  ┌───────────────────────────────────────────────────────┐  │
│  │  Lookup Tables                                         │  │
│  │  - Type -> ID                                          │  │
│  │  - Name -> ID                                          │  │
│  │  - ID -> Type                                          │  │
│  └───────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│              ComponentTypeId<T> (Generic Singleton)          │
│  - Id (readonly, 编译期缓存)                                  │
│  - Size, Flags, Callbacks                                   │
│  - BlockIndex, BitOffset, BitMask                           │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                    ComponentSet (512-bit)                    │
│  - Bits[8] (8 x ulong)                                      │
│  - Contains/Add/Remove<T>() (使用预计算索引)                   │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│              ComponentStorageUnsafe<T> (Phase 1)             │
│  ┌───────────────────────────────────────────────────────┐  │
│  │  ComponentDataBuffer (非托管内存)                       │  │
│  │  - Block** Blocks                                       │  │
│  │  - Version (迭代安全检查)                               │  │
│  └───────────────────────────────────────────────────────┘  │
│  ┌───────────────────────────────────────────────────────┐  │
│  │  ComponentBlockIterator<T> (批量遍历)                   │  │
│  │  - NextBlock(out entities, out components, out count)   │  │
│  └───────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                          Frame                               │
│  - ComponentTypeRegistry                                    │
│  - EntityRegistry                                           │
│  - ComponentStorage[]                                       │
│  - Add/Remove/GetComponent<T>() (触发生命周期回调)             │
│  - GetComponentBlockIterator<T>() (Phase 1)                 │
└─────────────────────────────────────────────────────────────┘
```

## 下一步（Phase 3）

- 实现完整的位流序列化（BitStream）
- Delta 压缩
- 帧快照/回滚支持
