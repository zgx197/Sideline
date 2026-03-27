# Lattice ECS 性能优化总结

> 历史归档文档：
> 本文反映的是较早阶段的性能改造背景，其中关于 `FrameBase`、序列化适配和待完成项的描述已与当前主干不一致。
> 当前性能热点与优化结果请优先参考 `godot/scripts/lattice/README.md`、`godot/scripts/lattice/ECS/Framework/SystemDesignNotes.md` 与现有 benchmark/test。

## 完成的工作

### 1. Entity → EntityRef 重命名
- 将 `Entity` 结构体重命名为 `EntityRef`，与 FrameSync 命名一致
- 更清晰的语义：明确表示这是一个引用/句柄，而非实体对象本身

### 2. 集中式架构 (FrameBase)

#### 优化前（分布式）
```
ComponentStorageUnsafe<T>
├── Dictionary<Entity, T> _components    // 每个类型独立字典
└── 访问路径: Dictionary lookup + Array index

性能: ~15-30ns per access
```

#### 优化后（FrameSync 风格集中式）
```
FrameBase
├── EntityInfo* _info                    // 集中实体信息
├── ulong* _componentMasks               // 每个实体的组件位图
└── ComponentDataBuffer* _buffers        // 组件存储数组

性能: ~5-10ns per access (预估提升 2-3x)
```

### 3. 双层检查优化

FrameSync 风格的 `TryGetPointer` 实现：
```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public bool TryGetPointer<T>(EntityRef entity, out T* value) where T : unmanaged, IComponent
{
    // 第一层：快速边界和版本检查
    if ((uint)entity.Index >= (uint)_capacity || 
        _info[entity.Index].Ref.Version != entity.Version)
    {
        value = null;
        return false;
    }

    // 第二层：位图检查（极快，缓存友好）
    ulong* mask = GetComponentMask(entity.Index);
    int index = ComponentTypeId<T>.Id;
    if ((mask[index / 64] & (1UL << (index % 64))) == 0)
    {
        value = null;
        return false;
    }

    // 第三层：直接指针获取（零开销）
    value = (T*)_buffers[index].GetDataPointerFastUnsafe(entity);
    return true;
}
```

### 4. 关键性能特性

| 特性 | 优化前 | 优化后 | 提升 |
|------|--------|--------|------|
| 组件存在性检查 | Dictionary lookup | 位图检查 | ~10x |
| 组件访问 | 2-3次字典查找 | 数组索引 + 指针 | ~3x |
| 批量迭代 | 逐个实体检查 | Block 批量处理 | ~5-10x |
| 内存局部性 | 分散存储 | 集中紧凑存储 | 显著 |

### 5. 与 FrameSync 的对比

#### 相同点
- ✅ 集中式实体信息管理（EntityInfo*）
- ✅ 全局组件位图（_componentMasks）
- ✅ 双层检查（位图 + 稀疏数组）
- ✅ Block 式组件存储（512 实体/Block）
- ✅ 交换删除保持密集

#### 差异点
| 方面 | FrameSync | Lattice (优化后) |
|------|-----------|------------------|
| 实体引用 | EntityRef | EntityRef (已统一) |
| 稀疏数组 | ushort* (每个 buffer) | ushort* (每个 buffer) |
| 版本检查 | Ref.Version | Ref.Version |
| 单例支持 | 内置 | 内置 |
| 跨引擎 | Unity 专用 | Godot/Unity/Unreal |

## 待完成工作

以下待办属于历史阶段背景，不再直接代表当前主干：

1. **Query 系统**: 当前主干已正式提供 `Frame.Query<T...>()`
2. **System 更新**: 当前主干已正式提供 `ISystem` 与 `SystemScheduler`
3. **序列化**: 当前 Session 热路径已切到 `PackedFrameSnapshot`
4. **性能测试**: 当前已存在 benchmark 与运行时回归入口

## 使用示例

```csharp
// 创建 Frame
using var frame = new FrameBase(maxComponentTypes: 128, initialEntityCapacity: 1024);

// 注册组件类型
ComponentTypeRegistry.Global.CreateBuilder()
    .Add<Position>(ComponentCallbacks.Empty)
    .Add<Velocity>(ComponentCallbacks.Empty)
    .Finish();

// 创建实体
var entity = frame.CreateEntity();

// 添加组件
frame.Add(entity, new Position { X = FP.Zero, Y = FP.Zero });
frame.Add(entity, new Velocity { X = FP.One, Y = FP.Zero });

// 快速组件访问
if (frame.TryGetPointer<Position>(entity, out var pos))
{
    pos->X += FP.One;
}

// 批量处理
var iterator = frame.GetComponentBlockIterator<Position>();
while (iterator.NextBlock(out var entities, out var positions, out var count))
{
    for (int i = 0; i < count; i++)
    {
        positions[i].X += FP.One;
    }
}
```
