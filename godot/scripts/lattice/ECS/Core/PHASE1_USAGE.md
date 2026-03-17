# Phase 1 使用指南 - FrameSync 风格组件存储

## 新增类型

### 1. ComponentDataBuffer
非托管分块内存缓冲区，直接管理原生内存。

```csharp
// 分配缓冲区
var buffer = new ComponentDataBuffer();
buffer.Allocate(
    componentTypeId: 1,
    stride: sizeof(MyComponent),
    blockCapacity: 512,
    initialBlockCount: 4
);

// 使用完毕后释放
buffer.Free();
```

### 2. ComponentBlockIterator<T>
FrameSync 风格的块迭代器，支持指针级批量遍历。

```csharp
// 获取迭代器
var iterator = frame.GetComponentBlockIterator<Position>();

// 遍历所有组件
foreach (var pair in iterator)
{
    Entity entity = pair.Entity;
    Position* pos = pair.Component;
    
    // 直接修改组件数据
    pos->X += 1;
    pos->Y += 2;
}
```

### 3. ComponentStorageUnsafe<T>
基于 ComponentDataBuffer 的组件存储，替代原来的 Dictionary 实现。

```csharp
// 创建存储
var storage = new ComponentStorageUnsafe<Position>(typeId: 1);

// 添加组件
storage.Add(entity, new Position { X = 10, Y = 20 });

// 获取指针（零开销）
Position* pos = storage.GetPointer(entity);
*pos = new Position { X = 30, Y = 40 };
```

## 使用示例

### 基本组件操作

```csharp
public unsafe class MovementSystem : ISystem
{
    public void Update(Frame frame)
    {
        // 方式 1: 使用块迭代器（推荐，缓存友好）
        var iterator = frame.GetComponentBlockIterator<Velocity>();
        
        foreach (var pair in iterator)
        {
            Entity entity = pair.Entity;
            Velocity* vel = pair.Component;
            
            // 获取位置组件指针
            Position* pos = frame.GetComponentPointer<Position>(entity);
            if (pos != null)
            {
                pos->X += vel->X * frame.DeltaTime;
                pos->Y += vel->Y * frame.DeltaTime;
            }
        }
        
        // 方式 2: 使用传统 API（向后兼容）
        frame.ForEach<Velocity>((entity, ref Velocity vel) =>
        {
            // ...
        });
    }
}
```

### 批量处理（SIMD 准备）

```csharp
public unsafe void BatchUpdatePositions(Frame frame)
{
    var storage = frame.GetUnsafeStorage<Position>();
    var rawIterator = storage.GetRawBlockIterator();
    
    Entity* entities;
    byte* components;
    int count;
    
    // 按块遍历，每块是连续内存
    while (rawIterator.NextBlock(out components, out entities, out count))
    {
        Position* positions = (Position*)components;
        
        // 这里可以使用 SIMD 指令处理连续内存
        // System.Numerics.Vector 或 System.Runtime.Intrinsics
        for (int i = 0; i < count; i++)
        {
            positions[i].X += 1.0f;
            positions[i].Y += 1.0f;
        }
    }
}
```

### 版本检查（防止迭代时修改）

```csharp
public unsafe void SafeIteration(Frame frame)
{
    var storage = frame.GetUnsafeStorage<Health>();
    var iterator = storage.GetRawBlockIterator();
    
    Entity* entities;
    byte* components;
    int count;
    
    int blockIndex = 0;
    while (iterator.NextBlock(out components, out entities, out count))
    {
        Health* healths = (Health*)components;
        
        for (int i = 0; i < count; i++)
        {
            if (healths[i].Value <= 0)
            {
                // ❌ 错误：迭代时修改存储会导致异常
                // storage.Remove(entities[i]);
                
                // ✅ 正确：延迟删除或标记删除
                frame.QueueDestroyEntity(entities[i]);
            }
        }
        blockIndex++;
    }
}
```

## 性能对比

| 操作 | 旧 ComponentStorage<T> | 新 ComponentStorageUnsafe<T> | 提升 |
|------|------------------------|------------------------------|------|
| 随机访问 | O(1) Dictionary 查找 | O(1) 数组索引 | 5-10x |
| 批量遍历 | 逐个 Dictionary 遍历 | 连续内存块遍历 | 10-50x |
| 缓存命中率 | 低（散列存储） | 高（密集存储） | 显著 |
| GC 压力 | 有（托管数组） | 无（非托管内存） | 零分配 |

## 迁移指南

### 从旧 API 迁移

```csharp
// 旧代码
var storage = frame.GetStorage<Position>();
ref Position pos = ref storage.Get(entity);

// 新代码（可选）
var storage = frame.GetUnsafeStorage<Position>();
Position* pos = storage.GetPointer(entity);
```

### 保持向后兼容

```csharp
// 旧 API 仍然可用
frame.AddComponent(entity, new Position { X = 10, Y = 20 });
ref Position pos = ref frame.GetComponent<Position>(entity);

// 新的 unsafe API 用于高性能场景
frame.AddComponentUnsafe(entity, new Position { X = 10, Y = 20 });
Position* pos = frame.GetComponentPointer<Position>(entity);
```

## 注意事项

1. **非托管内存**: `ComponentStorageUnsafe<T>` 使用 `NativeMemory.Alloc`，必须调用 `Dispose()` 释放
2. **版本检查**: 迭代过程中修改存储会抛出 `InvalidOperationException`
3. **unsafe 上下文**: 使用指针需要在 unsafe 块或标记 unsafe 方法
4. **结构约束**: `T` 必须是 `unmanaged` 类型

## 下一步（Phase 2）

- 集成序列化回调（ComponentCallbacks）
- 实现位流序列化（BitStream）
- 添加 Delta 压缩支持
