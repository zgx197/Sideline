# Phase 3 实现完成总结

> 历史归档文档：
> 本文记录的是早期 Phase 3 设计与实现总结，其中大量 `FrameSnapshot`、压缩和整体架构描述已经不再等同于当前主干。
> 当前实现状态请以 `godot/scripts/lattice/README.md` 和 `godot/scripts/lattice/ECS/Framework/SystemDesignNotes.md` 为准。

## 新增文件

### 1. BitStream.cs
位流 - FrameSync 风格的位级序列化。

**核心特性：**
- 位级读写（Bit-packing）
- 变长整数编码（VarInt / ZigZag）
- 原始内存操作
- 自动扩容

```csharp
public class BitStream
{
    public void WriteBit(bool value);
    public void WriteBits(uint value, int bits);
    public void WriteVarInt(int value);    // ZigZag 编码
    public void WriteVarUInt(uint value);
    public void WriteMemory(void* source, int length);
    
    public bool ReadBit();
    public uint ReadBits(int bits);
    public int ReadVarInt();
    public void ReadMemory(void* destination, int length);
}
```

### 2. FrameSerializer.cs（完全重写）
帧序列化器 - 基于 BitStream 的完整序列化。

```csharp
public sealed class FrameSerializer
{
    public enum Mode { Serialize, Deserialize, Checksum }
    
    // 基础类型
    public void Serialize(ref int value);
    public void Serialize(ref bool value);
    public void Serialize(ref long value);
    
    // 变长编码
    public void SerializeVarInt(ref int value);
    public void SerializeVarUInt(ref uint value);
    
    // 定点数
    public void Serialize(ref FP value);
    public void Serialize(ref FPVector2 value);
    public void Serialize(ref FPVector3 value);
    
    // 原始内存
    public void Serialize(void* data, int size);
    public void Serialize<T>(ref T value) where T : unmanaged;
    
    // ECS 类型
    public void Serialize(ref Entity entity);
    public void Serialize(ref ComponentSet componentSet);
}
```

### 3. DeltaCompressor.cs
Delta 压缩器 - 计算帧间差异。

```csharp
public sealed class DeltaCompressor
{
    // 压缩：计算差异
    public int Compress(byte[] baseline, byte[] current, byte[] deltaOutput);
    
    // 解压缩：应用差异
    public void Decompress(byte[] baseline, byte[] delta, int deltaLength, byte[] output);
    
    // 查找变化的块
    public int FindChangedBlocks(byte[] baseline, byte[] current, int blockSize, Span<int> changedBlocks);
}
```

### 4. FrameSnapshot.cs
帧快照 - 完整的状态保存/恢复。

```csharp
public sealed class FrameSnapshot
{
    public int Tick { get; }
    public byte[] Data { get; }
    public ulong Checksum { get; }
    
    // 创建快照
    public static FrameSnapshot Capture(Frame frame, ComponentTypeRegistry registry);
    
    // 恢复快照
    public void Restore(Frame frame, ComponentTypeRegistry registry);
    
    // 验证
    public bool Validate();
    
    // 压缩/解压缩
    public byte[] Compress();
    public static FrameSnapshot Decompress(int tick, byte[] compressedData, ulong checksum);
    
    // 校验和
    public static ulong CalculateChecksum(byte[] data);
}
```

## 修改文件

### ComponentSet.cs
- 字段名 `Bits` → `Set`（与 FrameSync 对齐）
- 所有相关引用已更新

### Frame.cs
- 替换 `CalculateChecksum()` 实现，使用 FrameSnapshot
- 添加 `CreateSnapshot()` 和 `RestoreFromSnapshot()`
- 更新 `CopyFrom()` 使用快照机制

## 与 FrameSync 的对齐程度

| FrameSync 特性 | Lattice Phase 3 | 状态 |
|----------------|-----------------|------|
| BitStream（位流） | ✅ 实现 | 完成 |
| FrameSerializer | ✅ 实现 | 完成 |
| VarInt / ZigZag | ✅ 实现 | 完成 |
| Delta 压缩 | ✅ 实现 | 完成 |
| 帧快照 | ✅ 实现 | 完成 |
| 校验和 | ✅ 实现 | 完成 |
| GZip 压缩 | ✅ 实现 | 完成 |

**Phase 3 完成度：100%**

## 使用示例

### 基础序列化

```csharp
// 创建序列化器
var stream = new BitStream(initialCapacity: 1024);
var serializer = new FrameSerializer(
    FrameSerializer.Mode.Serialize, 
    frame, 
    stream
);

// 序列化数据
int health = 100;
FP speed = FP.FromInt(5);
bool isActive = true;

serializer.Serialize(ref health);
serializer.Serialize(ref speed);
serializer.Serialize(ref isActive);

// 获取序列化后的数据
byte[] data = stream.ToArray();
```

### 帧快照

```csharp
// 创建快照
var snapshot = frame.CreateSnapshot();
Console.WriteLine($"Snapshot: Tick={snapshot.Tick}, Size={snapshot.DataLength}, Checksum={snapshot.Checksum}");

// 验证快照
if (!snapshot.Validate())
{
    Console.WriteLine("Snapshot corrupted!");
}

// 压缩快照
byte[] compressed = snapshot.Compress();
Console.WriteLine($"Compressed: {snapshot.DataLength} -> {compressed.Length} bytes");
```

### 回滚

```csharp
// 保存历史快照
var history = new Dictionary<int, FrameSnapshot>();
history[frame.Tick] = frame.CreateSnapshot();

// 预测若干帧...
for (int i = 0; i < 5; i++)
{
    frame.Tick++;
    UpdateSystems(frame);  // 执行系统更新
}

// 收到服务器确认，需要回滚到第 10 帧
if (history.TryGetValue(10, out var baseline))
{
    // 恢复到基线帧
    frame.RestoreFromSnapshot(baseline);
    
    // 重新模拟到当前帧
    for (int tick = 11; tick <= currentTick; tick++)
    {
        frame.Tick = tick;
        UpdateSystems(frame);
    }
}
```

### Delta 压缩

```csharp
var compressor = new DeltaCompressor();

// 计算差异
byte[] baseline = oldSnapshot.Data;
byte[] current = newSnapshot.Data;
byte[] delta = new byte[64 * 1024];  // 64KB 缓冲区

int deltaSize = compressor.Compress(baseline, current, delta);
Console.WriteLine($"Delta: {current.Length} -> {deltaSize} bytes ({100.0 * deltaSize / current.Length:F1}%)");

// 网络传输 delta...

// 接收端解压缩
byte[] reconstructed = new byte[baseline.Length];
compressor.Decompress(baseline, delta, deltaSize, reconstructed);
```

## 完整架构（Phase 1+2+3）

```
┌─────────────────────────────────────────────────────────────┐
│                    FrameSnapshot                             │
│  ┌───────────────────────────────────────────────────────┐  │
│  │  Capture(Frame) -> Snapshot                            │  │
│  │  - Serialize Frame -> BitStream                        │  │
│  │  - Calculate Checksum (FNV/CRC)                        │  │
│  └───────────────────────────────────────────────────────┘  │
│  ┌───────────────────────────────────────────────────────┐  │
│  │  Restore(Snapshot) -> Frame                            │  │
│  │  - Deserialize BitStream -> Frame                      │  │
│  │  - Recreate Entities/Components                        │  │
│  └───────────────────────────────────────────────────────┘  │
│  ┌───────────────────────────────────────────────────────┐  │
│  │  Compress/Decompress (GZip)                            │  │
│  └───────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                   DeltaCompressor                            │
│  - Compress(baseline, current) -> delta                     │
│  - Decompress(baseline, delta) -> current                   │
│  - FindChangedBlocks() for partial updates                  │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                   FrameSerializer                            │
│  ┌───────────────────────────────────────────────────────┐  │
│  │  BitStream                                            │  │
│  │  - Write/Read Bit                                     │  │
│  │  - Write/Read Int (32-bit)                            │  │
│  │  - Write/Read VarInt (ZigZag)                         │  │
│  │  - Write/Read Memory                                  │  │
│  └───────────────────────────────────────────────────────┘  │
│  ┌───────────────────────────────────────────────────────┐  │
│  │  Serialization Methods                                │  │
│  │  - Entity, ComponentSet                               │  │
│  │  - FP, FPVector2, FPVector3                           │  │
│  │  - Generic <T> unmanaged                              │  │
│  └───────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│            ComponentTypeRegistry + ComponentTypeId<T>        │
│  ┌───────────────────────────────────────────────────────┐  │
│  │  Builder Pattern (Phase 2)                            │  │
│  │  - Add<T>(callbacks, flags)                           │  │
│  │  - Serialize/OnAdded/OnRemoved callbacks              │  │
│  └───────────────────────────────────────────────────────┘  │
│  ┌───────────────────────────────────────────────────────┐  │
│  │  Metadata (Size, Flags, BlockIndex, BitMask...)       │  │
│  └───────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│              ComponentStorageUnsafe<T> (Phase 1)             │
│  ┌───────────────────────────────────────────────────────┐  │
│  │  ComponentDataBuffer (非托管内存)                       │  │
│  │  ComponentBlockIterator (批量遍历)                      │  │
│  └───────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                          Frame                               │
│  - Tick, DeltaTime, IsVerified                              │
│  - EntityRegistry, ComponentStorages[]                      │
│  - Add/Remove/Get Component                                 │
│  - CreateSnapshot / RestoreFromSnapshot                     │
│  - CalculateChecksum                                        │
└─────────────────────────────────────────────────────────────┘
```

## 网络同步流程示例

```csharp
// 客户端
void OnLocalInput(Frame frame, Input input)
{
    // 1. 保存当前帧快照
    var snapshot = frame.CreateSnapshot();
    
    // 2. 应用本地输入
    ApplyInput(frame, input);
    
    // 3. 发送输入到服务器（而不是完整帧）
    Network.SendInput(frame.Tick, input);
}

// 服务器
void OnServerTick()
{
    // 1. 收集所有客户端输入
    var inputs = CollectInputs();
    
    // 2. 模拟帧
    UpdateFrame(currentFrame, inputs);
    
    // 3. 创建快照并计算校验和
    var snapshot = currentFrame.CreateSnapshot();
    
    // 4. 发送给客户端（可以压缩或 Delta）
    foreach (var client in Clients)
    {
        byte[] data = snapshot.Compress();
        Network.SendSnapshot(client, currentFrame.Tick, data, snapshot.Checksum);
    }
}

// 客户端收到服务器快照
void OnServerSnapshot(int serverTick, byte[] compressedData, ulong serverChecksum)
{
    // 1. 解压缩
    var snapshot = FrameSnapshot.Decompress(serverTick, compressedData, serverChecksum);
    
    // 2. 验证
    if (!snapshot.Validate())
    {
        Log.Error("Invalid snapshot received!");
        return;
    }
    
    // 3. 比较校验和
    var localSnapshot = frame.CreateSnapshot();
    if (localSnapshot.Checksum != serverChecksum)
    {
        // 4. 回滚并重新模拟
        Log.Info($"Desync detected at {frame.Tick}, rolling back...");
        frame.RestoreFromSnapshot(snapshot);
        
        // 重新模拟到当前帧
        for (int tick = serverTick + 1; tick <= predictedTick; tick++)
        {
            ReplayFrame(tick);
        }
    }
}
```

## 总结

**Phase 1+2+3 完成度：100%**

所有计划功能已实现：
- ✅ Phase 1: 非托管存储 + 批量迭代
- ✅ Phase 2: 组件元数据 + 生命周期回调  
- ✅ Phase 3: 序列化 + Delta 压缩 + 快照/回滚

Lattice 现在具备与 FrameSync 相同的核心 ECS 功能，可用于确定性帧同步游戏开发。
