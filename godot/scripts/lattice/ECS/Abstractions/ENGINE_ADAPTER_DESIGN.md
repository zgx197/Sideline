# Lattice ECS 引擎适配器设计

## 设计目标

让 Lattice ECS 核心与具体游戏引擎解耦，支持：
- **Godot**（当前主要目标）
- **Unity**（未来扩展）
- **自定义引擎**（C++/Rust 等）
- **纯 .NET**（服务器/无图形）

## 架构分层

```
┌─────────────────────────────────────────────────────────────┐
│                      游戏逻辑层                               │
│  - Systems (MovementSystem, CombatSystem, ...)               │
│  - Components (Position, Health, ...)                        │
│  - Game-specific code                                        │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                    Lattice ECS Core                          │
│  - Frame (ECS 世界)                                          │
│  - Entity (实体)                                             │
│  - ComponentSet (组件集合)                                    │
│  - ComponentStorage (组件存储)                                │
│  - Query (查询系统)                                           │
│  - Session (网络同步)                                         │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                 IEngineAdapter (抽象接口)                     │
│  - 时间管理 (GameTime, DeltaTime)                            │
│  - 视图同步 (CreateEntityView, UpdateTransform)              │
│  - 输入系统 (GetPlayerInput)                                  │
│  - 资源管理 (LoadResource, InstantiatePrefab)                │
│  - 日志调试 (Log, DrawDebug)                                  │
└─────────────────────────────────────────────────────────────┘
                              │
              ┌───────────────┼───────────────┐
              ▼               ▼               ▼
┌─────────────────┐ ┌─────────────────┐ ┌─────────────────┐
│  GodotAdapter   │ │  UnityAdapter   │ │  CustomAdapter  │
│  (Godot Node)   │ │  (MonoBehaviour)│ │  (自定义实现)    │
└─────────────────┘ └─────────────────┘ └─────────────────┘
```

## 核心接口

### IEngineAdapter

引擎适配器的核心接口，由具体引擎实现：

```csharp
public interface IEngineAdapter
{
    // 时间管理
    FP GameTime { get; }
    FP FixedDeltaTime { get; }
    int FrameNumber { get; }
    
    // 实体视图同步
    IEntityView CreateEntityView(Entity entity);
    void DestroyEntityView(Entity entity, IEntityView view);
    void UpdateEntityTransform(Entity entity, FPVector3 position, FPQuaternion rotation);
    
    // 输入系统
    TInput GetPlayerInput<TInput>(int playerId);
    bool IsInputAvailable(int playerId);
    
    // 资源管理
    TResource LoadResource<TResource>(string path);
    IEntityView InstantiatePrefab(string prefabPath, FPVector3 position);
    
    // 日志调试
    void Log(LogLevel level, string message);
    void DrawDebugLine(FPVector3 start, FPVector3 end, Color color);
    
    // 生命周期
    void Initialize();
    void BeforeTick(Frame frame);
    void AfterTick(Frame frame);
    void Shutdown();
}
```

### IEntityView

实体在引擎中的可视化表示：

```csharp
public interface IEntityView
{
    Entity Entity { get; }
    bool IsValid { get; }
    
    void SetPosition(FPVector3 position);
    void SetRotation(FPQuaternion rotation);
    void SetActive(bool active);
    void Destroy();
}
```

## 适配器实现示例

### Godot 适配器

```csharp
public class GodotEngineAdapter : IEngineAdapter
{
    private SceneTree _sceneTree;
    private Node _entityRoot;
    
    public void Initialize()
    {
        _entityRoot = new Node { Name = "LatticeEntities" };
        _sceneTree.Root.AddChild(_entityRoot);
    }
    
    public IEntityView CreateEntityView(Entity entity)
    {
        var node = new Node2D { Name = $"Entity_{entity.Index}" };
        _entityRoot.AddChild(node);
        return new GodotEntityView(entity, node);
    }
    
    // ... 其他实现
}
```

### Unity 适配器（未来）

```csharp
public class UnityEngineAdapter : IEngineAdapter
{
    private GameObject _entityRoot;
    
    public void Initialize()
    {
        _entityRoot = new GameObject("LatticeEntities");
        Object.DontDestroyOnLoad(_entityRoot);
    }
    
    public IEntityView CreateEntityView(Entity entity)
    {
        var go = new GameObject($"Entity_{entity.Index}");
        go.transform.SetParent(_entityRoot.transform);
        return new UnityEntityView(entity, go);
    }
    
    // ... 其他实现
}
```

### 纯 .NET 适配器（服务器）

```csharp
public class HeadlessEngineAdapter : IEngineAdapter
{
    // 无图形、无视图同步
    // 只有时间管理和日志
    
    public IEntityView CreateEntityView(Entity entity)
    {
        return new NullEntityView(entity); // 空实现
    }
    
    public void UpdateEntityTransform(Entity entity, FPVector3 position, FPQuaternion rotation)
    {
        // 空实现
    }
}
```

## 使用方式

```csharp
// 初始化时注入适配器
var adapter = new GodotEngineAdapter(GetTree());
var session = new LatticeSession(adapter, config);

// 游戏循环
public override void _PhysicsProcess(double delta)
{
    // 1. 获取输入
    var input = adapter.GetPlayerInput<FrameInput>(0);
    
    // 2. 更新 ECS 世界
    session.Tick(input);
    
    // 3. 同步视图（在 AfterTick 中自动完成）
}
```

## 多引擎支持的好处

1. **移植性** - 同一套游戏逻辑可以在不同引擎运行
2. **测试** - 可以在纯 .NET 环境单元测试
3. **服务器** - 使用 HeadlessAdapter 运行确定性模拟
4. **混合** - 客户端用 Godot，服务器用纯 .NET

## 实现优先级

| 适配器 | 优先级 | 说明 |
|--------|--------|------|
| GodotAdapter | P0 | 当前主要目标 |
| HeadlessAdapter | P1 | 服务器/测试需求 |
| UnityAdapter | P2 | 未来扩展 |
| CustomAdapter | P3 | 按需实现 |

---

*最后更新：2026-03-17*
