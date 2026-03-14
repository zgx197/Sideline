# Lattice ECS Core 鏍稿績妯″潡

> 纭畾鎬?ECS (Entity Component System) 妗嗘灦
> 
> 璁捐鍙傝€冿細FrameSyncEngine銆乁nity DOTS銆丅evy ECS

---

## 馃幆 璁捐鐞嗗康

### 娓愯繘寮忚凯浠ｅ紑鍙?

涓嶈拷姹備竴娆℃€у畬缇庯紝閲囩敤**寰幆杩唬**鏂瑰紡锛屾瘡涓樁娈甸兘鏈夊彲鐢ㄦ垚鏋滐細

```
杩唬 1: 鍩虹楠ㄦ灦 鈫?鑳借窇绠€鍗?Demo
杩唬 2: 瀹屽杽鏌ヨ 鈫?鏀寔澶嶆潅閫昏緫
杩唬 3: 浼樺寲鎬ц兘 鈫?Archetype + SIMD
杩唬 4: 缃戠粶鍚屾 鈫?棰勬祴鍥炴粴
```

### 鏍稿績鍘熷垯

1. **绠€鍗曚紭鍏?*锛氬厛瀹炵幇鑳界敤锛屽啀浼樺寲鎬ц兘
2. **鐙珛娴嬭瘯**锛氭瘡涓ā鍧楀彲鐙珛楠岃瘉
3. **鍚戝悗鍏煎**锛氳凯浠ｄ笉鐮村潖宸叉湁 API
4. **纭畾鎬?*锛氱浉鍚岃緭鍏ュ繀鐒剁浉鍚岃緭鍑?

---

## 馃彈锔?妯″潡鏋舵瀯

### 浜斿ぇ鐙珛妯″潡

```
鈹屸攢鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹?
鈹? 搴旂敤灞?(Application)                                    鈹?
鈹? 鈹溾攢鈹€ Game Logic                                         鈹?
鈹? 鈹溾攢鈹€ Mod System                                         鈹?
鈹? 鈹斺攢鈹€ Replay System                                      鈹?
鈹溾攢鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹?
鈹? 璋冨害灞?(Scheduling)      鈫?杩唬 2                      鈹?
鈹? 鈹溾攢鈹€ System Scheduler                                   鈹?
鈹? 鈹溾攢鈹€ Update / FixedUpdate / Render                      鈹?
鈹? 鈹斺攢鈹€ Event Bus                                          鈹?
鈹溾攢鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹?
鈹? 鏌ヨ灞?(Query)           鈫?杩唬 2                      鈹?
鈹? 鈹溾攢鈹€ Query<T>                                           鈹?
鈹? 鈹溾攢鈹€ Query<T1, T2>                                      鈹?
鈹? 鈹斺攢鈹€ Query Builder                                      鈹?
鈹溾攢鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹?
鈹? 鏁版嵁灞?(Data)            鈫?杩唬 1锛堟牳蹇冿級               鈹?
鈹? 鈹溾攢鈹€ World                                              鈹?
鈹? 鈹?  鈹斺攢鈹€ Frame (Snapshot)                               鈹?
鈹? 鈹?      鈹溾攢鈹€ Entity Manager                             鈹?
鈹? 鈹?      鈹斺攢鈹€ Component Storage                          鈹?
鈹? 鈹?          鈹斺攢鈹€ Dense Array [Entity + Components]      鈹?
鈹? 鈹斺攢鈹€ Archetype (Optional)                               鈹?
鈹溾攢鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹?
鈹? 鎺ュ彛灞?(Interface)       鈫?杩唬 1锛堝熀纭€锛?              鈹?
鈹? 鈹溾攢鈹€ Entity (ID + Version)                              鈹?
鈹? 鈹溾攢鈹€ IComponent (Marker)                                鈹?
鈹? 鈹斺攢鈹€ ISystem (Behavior)                                 鈹?
鈹斺攢鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹?
```

---

## 馃殌 杩唬璺嚎鍥?

### 杩唬 1锛氬熀纭€楠ㄦ灦锛?-3 鍛級

**鐩爣**锛氳兘鍒涘缓瀹炰綋銆佹坊鍔犵粍浠躲€侀亶鍘嗘洿鏂?

```csharp
// 鏈熸湜鐢ㄦ硶
var world = new World();
var entity = world.CreateEntity();
world.Add<Position>(entity, new Position { X = FP._1, Y = FP._2 });

// 绠€鍗曢亶鍘?
foreach (var (e, pos) in world.Query<Position>())
{
    Console.WriteLine($"Entity {e} at {pos.X}");
}
```

#### 1.1 Entity Module锛堚渽 宸插畬鎴愶級

**鑱岃矗**锛氳交閲忕骇 ID 鏍囪瘑 + 鐢熷懡鍛ㄦ湡绠＄悊

**鏍稿績瀹炵幇**锛?
```csharp
// Entity.cs - 8瀛楄妭杞婚噺绾ф爣璇嗙
[StructLayout(LayoutKind.Explicit)]
public readonly struct Entity : IEquatable<Entity>
{
    [FieldOffset(0)] public readonly int Index;    // 鏁扮粍绱㈠紩
    [FieldOffset(4)] public readonly int Version;  // 鐗堟湰鍙凤紙闃叉ABA闂锛?
    [FieldOffset(0)] public readonly ulong Raw;    // 蹇€熸瘮杈?
    
    public bool IsValid => Raw != 0;
    public static readonly Entity None = default;
}

// EntityRegistry.cs - ID鍒嗛厤涓庡洖鏀?
internal sealed class EntityRegistry
{
    public Entity Create();              // 鍒嗛厤鏂癐D锛堜紭鍏堝鐢ㄧ┖闂叉Ы浣嶏級
    public bool Destroy(Entity entity);  // 閿€姣佸苟鍥炴敹ID
    public bool IsValid(Entity entity);  // 楠岃瘉寮曠敤鏈夋晥鎬?
    
    // 鍏抽敭鐗规€э細
    // - LIFO绌洪棽鍒楄〃锛堢紦瀛樺弸濂斤級
    // - 鐗堟湰鍙烽€掑锛堥槻姝㈡偓绌哄紩鐢級
    // - 娲昏穬鏍囧織浣嶏紙澶嶇敤Version鏈€楂樹綅锛?
}

// EntityMeta.cs - 瀹炰綋鍏冩暟鎹紙鍐呴儴浣跨敤锛?
internal struct EntityMeta
{
    public Entity Ref;          // 瀹炰綋寮曠敤
    public int ArchetypeId;     // 鎵€鍦ˋrchetype锛堥鐣欙級
    public int ArchetypeRow;    // 鍦ˋrchetype涓殑琛岋紙棰勭暀锛?
    
    public const int ActiveBit = int.MinValue;   // 0x80000000
    public const int VersionMask = int.MaxValue; // 0x7FFFFFFF
}
```

**璁捐瑕佺偣**锛?
- **Generational Index**锛欼ndex + Version 缁勫悎锛屽鐢ㄦЫ浣嶆椂鐗堟湰閫掑锛岄槻姝?ABA 闂
- **绌洪棽鍒楄〃**锛歚Stack<int>` LIFO 缁撴瀯锛岀紦瀛樺弸濂?
- **鐗堟湰绠＄悊**锛?1浣嶇増鏈彿锛堢害21浜挎澶嶇敤锛夛紝鏈€楂樹綅浣滄椿璺冩爣蹇?
- **蹇€熼獙璇?*锛歚Raw` 瀛楁 64浣嶆瘮杈冿紝O(1) 鏈夋晥鎬ф鏌?

**娴嬭瘯瑕嗙洊**锛歚Lattice.Tests/Core/EntityTests.cs`
- Entity 鍒涘缓/閿€姣?楠岃瘉
- ID 鍥炴敹涓庣増鏈€掑
- 鎮┖寮曠敤妫€娴?

**娴嬭瘯瑕佺偣**锛?
- ID 鍞竴鎬?
- 鐗堟湰閫掑
- Null 瀹炰綋澶勭悊

#### 1.2 Component Storage锛? 澶╋級猸?

**鑱岃矗**锛氬崟绫诲瀷缁勪欢鐨勫鍒犳煡鏀?

**鏍稿績璁捐**锛?
```csharp
public class ComponentStorage<T> where T : struct
{
    private T[] _components;           // 缁勪欢鏁版嵁
    private Entity[] _entities;        // 瀵瑰簲瀹炰綋
    private Dictionary<Entity, int> _entityToIndex; // 鏌ユ壘琛?
    
    public void Add(Entity entity, in T component);
    public void Remove(Entity entity);
    public ref T Get(Entity entity);   // ref 鍏佽淇敼
    public bool Has(Entity entity);
    
    // 閬嶅巻鏀寔
    public Span<T> GetSpan();
    public Span<Entity> GetEntities();
}
```

**娴嬭瘯瑕佺偣**锛?
- 澧炲垹鏌ユ敼姝ｇ‘鎬?
- 寮曠敤绋冲畾鎬э紙ref 淇敼鍚庢寔涔呭寲锛?
- 閬嶅巻椤哄簭涓€鑷存€?

#### 1.3 Frame锛? 澶╋級

**鑱岃矗**锛氬崟甯у畬鏁寸姸鎬?

**鏍稿績璁捐**锛?
```csharp
public class Frame
{
    // 姣忕缁勪欢绫诲瀷涓€涓瓨鍌?
    private readonly Dictionary<Type, object> _storages = new();
    private readonly EntityManager _entityManager;
    
    // 瀹炰綋鎿嶄綔
    public Entity CreateEntity();
    public void DestroyEntity(Entity entity);
    
    // 缁勪欢鎿嶄綔
    public void Add<T>(Entity entity, in T component) where T : struct;
    public void Remove<T>(Entity entity) where T : struct;
    public ref T Get<T>(Entity entity) where T : struct;
    public bool Has<T>(Entity entity) where T : struct;
}
```

**娴嬭瘯瑕佺偣**锛?
- 瀹炰綋鐢熷懡鍛ㄦ湡
- 缁勪欢澧炲垹鏌ユ敼
- 澶氱被鍨嬬粍浠跺叡瀛?

#### 1.4 World + 绠€鍗曠郴缁燂紙3 澶╋級

**鑱岃矗**锛氬叆鍙?+ 绠€鍗曢亶鍘?

**鏍稿績璁捐**锛?
```csharp
public class World
{
    public Frame CurrentFrame { get; }
    
    // 绠€鍗曠郴缁熷鎵橈紙杩唬 1 鍏堢敤 Action锛岃凯浠?2 鍐嶆娊璞℃帴鍙ｏ級
    private readonly List<Action<World>> _systems = new();
    
    public void AddSystem(Action<World> system);
    public void Tick(FP deltaTime);
    
    // 绠€鍗曟煡璇紙杩唬 1 鍙敮鎸佸崟绫诲瀷锛?
    public IEnumerable<(Entity, T)> Query<T>() where T : struct;
}
```

**娴嬭瘯瑕佺偣**锛?
- 绯荤粺鎵ц椤哄簭
- 鏁版嵁娴佹纭€?
- Tick 寰幆绋冲畾鎬?

#### 杩唬 1 楠屾敹鏍囧噯

```csharp
// 鑳借繍琛岃繖涓?Demo 鍗抽€氳繃
var world = new World();

// 鍒涘缓 1000 涓疄浣?
for (int i = 0; i < 1000; i++)
{
    var e = world.CreateEntity();
    world.Add<Position>(e, new Position { X = FP.FromInt(i), Y = FP.Zero });
    if (i % 2 == 0)
        world.Add<Velocity>(e, new Velocity { X = FP._0_10, Y = FP.Zero });
}

// 绯荤粺鏇存柊
world.AddSystem(world =>
{
    foreach (var (e, pos) in world.Query<Position>())
    {
        if (world.TryGet<Velocity>(e, out var vel))
        {
            pos.X += vel.X;
        }
    }
});

// 杩愯 60 甯?
for (int i = 0; i < 60; i++)
    world.Tick(FP.FromRaw(FP.Raw._0_016)); // 16ms
```

---

### 杩唬 2锛氬畬鍠勬煡璇笌绯荤粺锛? 鍛級

**鐩爣**锛氭敮鎸佸缁勪欢鏌ヨ銆佺郴缁熸帴鍙ｆ娊璞°€佷簨浠舵€荤嚎

#### 2.1 Query Module锛? 澶╋級猸?

**鑱岃矗**锛氶珮鏁堝缁勪欢鏌ヨ

**鏍稿績璁捐**锛?
```csharp
// 澶氱粍浠舵煡璇?
public ref struct Query<T1, T2> where T1 : struct where T2 : struct
{
    public bool MoveNext();
    public (Entity, ref T1, ref T2) Current { get; }
}

// 鏌ヨ鏉′欢锛堝彲閫夛級
public ref struct Query<T> where T : struct
{
    public Query<T> With<TOther>() where TOther : struct;
    public Query<T> Without<TOther>() where TOther : struct;
}
```

**娴嬭瘯瑕佺偣**锛?
- 澶氱粍浠惰仈鍚堟煡璇㈡纭€?
- 鎬ц兘鍩哄噯锛坴s 杩唬 1 鐨勭畝鍗曢亶鍘嗭級
- 鍐呭瓨鍒嗛厤闆?GC

#### 2.2 System Interface锛? 澶╋級

**鑱岃矗**锛氭娊璞＄郴缁熸帴鍙?

**鏍稿績璁捐**锛?
```csharp
public interface ISystem
{
    void OnInit(World world);
    void OnUpdate(World world);
    void OnDestroy(World world);
}

// 绯荤粺鍒嗙粍
public enum SystemGroup { Update, FixedUpdate, Render }

[SystemGroup(SystemGroup.Update)]
public class MovementSystem : ISystem
{
    public void OnUpdate(World world)
    {
        foreach (var (e, pos, vel) in world.Query<Position, Velocity>())
        {
            pos.X += vel.X * world.DeltaTime;
        }
    }
}
```

#### 2.3 Scheduler锛? 澶╋級

**鑱岃矗**锛氱郴缁熻皟搴︿笌鍒嗙粍

**鏍稿績璁捐**锛?
```csharp
public class Scheduler
{
    // 鍒嗙粍鎵ц
    public void Update(World world);       // 姣忓抚
    public void FixedUpdate(World world);  // 鍥哄畾鏃堕棿姝?
    public void Render(World world);       // 娓叉煋
    
    // 椤哄簭鎺у埗
    public void SetExecutionOrder<TBefore, TAfter>();
}
```

#### 杩唬 2 楠屾敹鏍囧噯

- 鏀寔 `Query<Position, Velocity>` 鑱斿悎鏌ヨ
- 绯荤粺鐢熷懡鍛ㄦ湡绠＄悊锛圛nit 鈫?Update 鈫?Destroy锛?
- 绯荤粺鍒嗙粍鎵ц锛圲pdate / FixedUpdate锛?

---

### 杩唬 3锛氭€ц兘浼樺寲锛? 鍛級

**鐩爣**锛欰rchetype 鍒嗗潡銆丼IMD 鍔犻€熴€佺紦瀛樹紭鍖?

#### 3.1 Archetype Module锛? 澶╋級猸?

**鑱岃矗**锛氱粍浠剁粍鍚堢被鍨嬬鐞?

**鏍稿績璁捐**锛?
```csharp
public class Archetype
{
    public ComponentType[] ComponentTypes { get; }
    public int EntityCount { get; }
    
    // 鍒嗗潡瀛樺偍
    internal Chunk[] Chunks;
    
    // 瀹炰綋杩佺Щ
    public void MoveEntity(Entity entity, Archetype newArchetype);
}

public class Chunk
{
    public const int Capacity = 128; // 姣忓潡瀹圭撼瀹炰綋鏁?
    public byte[] Memory;            // 鍘熷鍐呭瓨
    public int Count;                // 褰撳墠瀹炰綋鏁?
}
```

**娴嬭瘯瑕佺偣**锛?
- 瀹炰綋杩佺Щ姝ｇ‘鎬?
- 鍐呭瓨杩炵画鎬?
- 閬嶅巻鎬ц兘鎻愬崌锛坴s 杩唬 1锛?

#### 3.2 SIMD Optimization锛? 澶╋級

**鑱岃矗**锛氭壒閲忕粍浠?SIMD 澶勭悊

**鏍稿績璁捐**锛?
```csharp
public static class BatchOperations
{
    public static void AddBatch(Span<FP> a, Span<FP> b, Span<FP> result);
    public static void MultiplyBatch(Span<FP> a, Span<FP> b, Span<FP> result);
}
```

#### 3.3 Query Cache锛? 澶╋級

**鑱岃矗**锛氭煡璇㈢粨鏋滅紦瀛?

**鏍稿績璁捐**锛?
```csharp
public class QueryCache
{
    // 缂撳瓨绗﹀悎鏉′欢鐨?Archetype 鍒楄〃
    private readonly Dictionary<QueryDesc, Archetype[]> _cache = new();
}
```

#### 杩唬 3 楠屾敹鏍囧噯

- 1000 瀹炰綋閬嶅巻鎬ц兘姣旇凯浠?1 鎻愬崌 3x+
- 鍐呭瓨鍒嗛厤闆?GC
- 缂撳瓨鏈懡涓巼 < 10%

---

### 杩唬 4锛氱綉缁滃悓姝ワ紙2-3 鍛級

**鐩爣**锛氶娴嬪洖婊氥€佺姸鎬佸揩鐓с€佺‘瀹氭€ч獙璇?

#### 4.1 Snapshot Module锛? 澶╋級

**鑱岃矗**锛氬抚鐘舵€佸簭鍒楀寲

**鏍稿績璁捐**锛?
```csharp
public class FrameSnapshot
{
    public byte[] Serialize(Frame frame);
    public Frame Deserialize(byte[] data);
    public long CalculateChecksum(Frame frame);
}
```

#### 4.2 Multi-Frame World锛? 澶╋級猸?

**鑱岃矗**锛氬甯х鐞嗭紙Verified / Predicted锛?

**鏍稿績璁捐**锛?
```csharp
public class World
{
    public Frame VerifiedFrame { get; }   // 鏈嶅姟鍣ㄧ‘璁ゅ抚
    public Frame PredictedFrame { get; }  // 鏈湴棰勬祴甯?
    public Frame PreviousFrame { get; }   // 涓婁竴甯э紙鎻掑€肩敤锛?
    
    public void Rollback(int toFrameNumber, FrameSnapshot serverFrame);
    public void Resimulate(int fromFrameNumber);
}
```

#### 4.3 Determinism Validation锛? 澶╋級

**鑱岃矗**锛氱‘瀹氭€ч獙璇?

**鏍稿績璁捐**锛?
```csharp
public class DeterminismValidator
{
    public void RecordInput(int frame, InputCommand input);
    public void ValidateChecksum(int frame, long expectedChecksum);
}
```

#### 杩唬 4 楠屾敹鏍囧噯

- 鏀寔鍥炴粴鍒颁换鎰忓巻鍙插抚
- 鍥炴粴鍚庨噸鏂版ā鎷熺粨鏋滀竴鑷?
- 璺ㄥ钩鍙版牎楠屽拰涓€鑷?

---

## 馃搧 鐩綍缁撴瀯

```
lattice/Core/                       # ECS 鏍稿績妯″潡
鈹溾攢鈹€ README.md                       # 鏈枃妗?
鈹?
鈹溾攢鈹€ Entity.cs                       # 鉁?Entity ID 瀹炵幇
鈹溾攢鈹€ EntityMeta.cs                   # 鉁?瀹炰綋鍏冩暟鎹?
鈹溾攢鈹€ EntityRegistry.cs               # 鉁?瀹炰綋娉ㄥ唽琛?
鈹?
鈹溾攢鈹€ ComponentStorage.cs             # 馃毀 缁勪欢瀛樺偍锛堣凯浠?1.2锛?
鈹溾攢鈹€ Frame.cs                        # 馃毀 鍗曞抚鏁版嵁锛堣凯浠?1.3锛?
鈹斺攢鈹€ World.cs                        # 馃毀 涓栫晫绠＄悊鍣紙杩唬 1.4锛?

lattice/Query/                      # 鏌ヨ妯″潡锛堣凯浠?2锛?
鈹溾攢鈹€ Query.cs
鈹溾攢鈹€ Query{T}.cs
鈹斺攢鈹€ Query{T1,T2}.cs

lattice/System/                     # 绯荤粺妯″潡锛堣凯浠?2锛?
鈹溾攢鈹€ ISystem.cs
鈹斺攢鈹€ Scheduler.cs

lattice/Archetype/                  # Archetype 浼樺寲锛堣凯浠?3锛?
鈹溾攢鈹€ Archetype.cs
鈹斺攢鈹€ Chunk.cs

lattice/Snapshot/                   # 蹇収鍚屾锛堣凯浠?4锛?
鈹斺攢鈹€ FrameSnapshot.cs
```

---

## 馃И 娴嬭瘯绛栫暐

### 鍗曞厓娴嬭瘯锛堟瘡涓ā鍧楋級

```
Lattice.Tests/Core/
鈹溾攢鈹€ Iteration1/
鈹?  鈹溾攢鈹€ EntityTests.cs
鈹?  鈹溾攢鈹€ ComponentStorageTests.cs
鈹?  鈹溾攢鈹€ FrameTests.cs
鈹?  鈹斺攢鈹€ WorldTests.cs
鈹斺攢鈹€ ...
```

### 闆嗘垚娴嬭瘯锛堟瘡涓凯浠ｏ級

```csharp
// 杩唬 1 闆嗘垚娴嬭瘯锛氱畝鍗?Demo
[Fact]
public void Iteration1_Demo_ShouldWork()
{
    // 鍒涘缓涓栫晫 鈫?娣诲姞瀹炰綋 鈫?杩愯绯荤粺 鈫?楠岃瘉缁撴灉
}

// 杩唬 4 闆嗘垚娴嬭瘯锛氬洖婊?
[Fact]
public void Iteration4_Rollback_ShouldBeDeterministic()
{
    // 杩愯 100 甯?鈫?鍥炴粴鍒?50 甯?鈫?閲嶆柊妯℃嫙 鈫?鏍￠獙鍜屼竴鑷?
}
```

### 鎬ц兘鍩哄噯

```csharp
[MemoryDiagnoser]
public class QueryBenchmark
{
    [Benchmark(Baseline = true)]
    public void Iteration1_SimpleQuery() { /* ... */ }
    
    [Benchmark]
    public void Iteration2_MultiComponentQuery() { /* ... */ }
    
    [Benchmark]
    public void Iteration3_ArchetypeQuery() { /* ... */ }
}
```

---

## 馃 寰呭喅绛栭棶棰?

### 杩唬 1 蹇呴』鍐崇瓥

1. **~~Entity 鐗堟湰鍙穨~**锛氣渽 宸茬‘瀹?- 浣跨敤 Generational Index锛圛ndex + Version锛夛紝鍙傝€?FrameSync/Bevy 璁捐
2. **缁勪欢瀛樺偍鎵╁**锛氬浐瀹氬閲?vs 鍔ㄦ€佹墿瀹癸紵锛堝缓璁細鍥哄畾锛岄伩鍏嶅紩鐢ㄥけ鏁堬級
3. **鏌ヨ杩斿洖鍊?*锛歚IEnumerable` vs `Span` vs 鍥炶皟锛燂紙寤鸿锛氳凯浠ｅ櫒妯″紡锛岄浂鍒嗛厤锛?

### 杩唬 2 蹇呴』鍐崇瓥

1. **澶氱粍浠舵煡璇㈢畻娉?*锛氶亶鍘嗗皬闆嗗悎 + HashSet 妫€鏌?vs 浣嶅浘绱㈠紩锛?
2. **绯荤粺鎵ц椤哄簭**锛氭樉寮忔寚瀹?vs 渚濊禆娉ㄥ叆鑷姩鎺掑簭锛?

### 杩唬 3 鍙€変紭鍖?

1. **Archetype 鏄惁蹇呴』**锛氳凯浠?1-2 涓嶇敤锛岃凯浠?3 寮曞叆浣滀负浼樺寲
2. **Source Generator**锛氳凯浠?3 寮曞叆锛岃嚜鍔ㄧ敓鎴愮粍浠跺厓鏁版嵁

---

## 馃搳 褰撳墠鐘舵€?

### 宸插疄鐜?鉁?

| 妯″潡 | 鏂囦欢 | 鐘舵€?| 娴嬭瘯 |
|------|------|------|------|
| Entity | `Entity.cs` | 鉁?瀹屾垚 | `EntityTests.cs` |
| EntityMeta | `EntityMeta.cs` | 鉁?瀹屾垚 | `EntityMetaTests.cs` |
| EntityRegistry | `EntityRegistry.cs` | 鉁?瀹屾垚 | `EntityRegistryTests.cs` |

### 杩涜涓?馃毀

| 妯″潡 | 渚濊禆 | 棰勮鏃堕棿 |
|------|------|----------|
| Component Storage | Entity | 5澶?|
| Frame | Entity, Storage | 3澶?|
| World | Frame, Storage | 3澶?|

---

## 馃幆 涓嬩竴姝ヨ鍔?

1. **瀹炵幇 Component Storage**锛堣凯浠?1.2锛?
   - 鍗曠被鍨嬬粍浠剁殑澧炲垹鏌ユ敼
   - ref 杩斿洖鏀寔鍘熷湴淇敼
   - 閬嶅巻鏀寔锛圫pan/Enumerator锛?

2. **璁捐鍐崇瓥**锛?
   - 瀛樺偍鎵╁绛栫暐锛堝浐瀹?vs 鍔ㄦ€侊級
   - 鏌ヨ杩斿洖鏂瑰紡锛圛Enumerable vs ref struct Enumerator锛?

鍑嗗濂界户缁?**杩唬 1.2锛欳omponent Storage** 鐨勮璁¤璁哄悧锛
