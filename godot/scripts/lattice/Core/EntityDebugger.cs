using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Lattice.Core
{
    /// <summary>
    /// 实体调试代理 - 提供丰富的调试体验
    /// 
    /// 设计参考：FrameSync的DebuggerProxy
    /// </summary>
    public static class EntityDebugger
    {
        /// <summary>
        /// 获取实体名称的自定义委托
        /// 可由用户设置以提供更有意义的实体名称
        /// </summary>
        public delegate string EntityNameProviderDelegate(Entity entity, EntityRegistry? registry);

        /// <summary>
        /// 全局实体名称提供器
        /// </summary>
        public static EntityNameProviderDelegate? EntityNameProvider { get; set; }

        /// <summary>
        /// 获取实体的显示名称
        /// </summary>
        public static string GetName(Entity entity, EntityRegistry? registry = null)
        {
            if (EntityNameProvider != null)
            {
                try
                {
                    var name = EntityNameProvider(entity, registry);
                    if (!string.IsNullOrEmpty(name))
                        return name;
                }
                catch
                {
                    // 忽略用户委托的异常
                }
            }

            return entity.ToString();
        }

        /// <summary>
        /// 获取实体的详细调试信息
        /// </summary>
        public static string GetDetailedInfo(Entity entity, EntityRegistry registry)
        {
            if (!registry.IsValid(entity))
            {
                return $"Entity {entity} is INVALID";
            }

            var info = registry.GetDebugInfo(entity);
            var components = new List<string>();

            // 解析组件掩码
            ulong mask = info.ComponentMask;
            for (int i = 0; i < 64 && mask != 0; i++)
            {
                if ((mask & (1UL << i)) != 0)
                {
                    components.Add($"Component[{i}]");
                }
            }

            return $"Entity {GetName(entity, registry)}\n" +
                   $"  Index: {info.Index}\n" +
                   $"  Version: {info.Version} (0x{info.Version:X8})\n" +
                   $"  IsAlive: {info.IsAlive}\n" +
                   $"  Flags: {info.Flags}\n" +
                   $"  ArchetypeId: {info.ArchetypeId}\n" +
                   $"  ArchetypeRow: {info.ArchetypeRow}\n" +
                   $"  Components: {(components.Count > 0 ? string.Join(", ", components) : "none")}";
        }

        /// <summary>
        /// 打印注册表中所有实体的状态（调试辅助）
        /// </summary>
        public static void DumpRegistry(EntityRegistry registry, System.IO.TextWriter? writer = null)
        {
            writer ??= System.Console.Out;

            writer.WriteLine("=== Entity Registry Dump ===");
            writer.WriteLine($"Capacity: {registry.Capacity}");
            writer.WriteLine($"Total Count: {registry.Count}");
            writer.WriteLine($"Alive Count: {registry.AliveCount}");
            writer.WriteLine($"Free Count: {registry.FreeCount}");
            writer.WriteLine();

            int aliveCount = 0;
            int deadCount = 0;

            for (int i = 0; i < registry.Count; i++)
            {
                var version = registry.GetVersion(i);
                bool isAlive = (version & EntityRegistry.ActiveBit) != 0;

                if (isAlive)
                {
                    aliveCount++;
                    var entity = new Entity(i, version);
                    writer.WriteLine($"  [{i}] {GetName(entity, registry)} - Alive, Archetype={registry.GetArchetypeId(i)}");
                }
                else
                {
                    deadCount++;
                    int gen = version & EntityRegistry.VersionMask;
                    writer.WriteLine($"  [{i}] Version={gen} - Dead");
                }
            }

            writer.WriteLine();
            writer.WriteLine($"Summary: {aliveCount} alive, {deadCount} dead");
        }

        /// <summary>
        /// 验证注册表的一致性（调试用）
        /// </summary>
        public static bool ValidateConsistency(EntityRegistry registry, out string? error)
        {
            error = null;

            try
            {
                // 检查AliveCount与遍历结果一致
                int actualAlive = 0;
                for (int i = 0; i < registry.Count; i++)
                {
                    var version = registry.GetVersion(i);
                    if ((version & EntityRegistry.ActiveBit) != 0)
                    {
                        actualAlive++;
                    }
                }

                if (actualAlive != registry.AliveCount)
                {
                    error = $"AliveCount mismatch: registry says {registry.AliveCount}, actual {actualAlive}";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                error = $"Exception during validation: {ex.Message}";
                return false;
            }
        }
    }

    /// <summary>
    /// 实体序列化工具
    /// </summary>
    public static unsafe class EntitySerializer
    {
        /// <summary>
        /// 将Entity序列化为原始字节
        /// </summary>
        public static void Serialize(Entity entity, Span<byte> destination)
        {
            if (destination.Length < sizeof(ulong))
                throw new ArgumentException("Destination span too small", nameof(destination));

            ulong raw = entity.Raw;
            fixed (byte* ptr = destination)
            {
                *(ulong*)ptr = raw;
            }
        }

        /// <summary>
        /// 从原始字节反序列化Entity
        /// </summary>
        public static Entity Deserialize(ReadOnlySpan<byte> source)
        {
            if (source.Length < sizeof(ulong))
                throw new ArgumentException("Source span too small", nameof(source));

            fixed (byte* ptr = source)
            {
                ulong raw = *(ulong*)ptr;
                return new Entity(raw);
            }
        }

        /// <summary>
        /// 序列化到指针（用于网络/帧同步）
        /// </summary>
        public static void SerializeToPtr(Entity entity, void* ptr)
        {
            *(ulong*)ptr = entity.Raw;
        }

        /// <summary>
        /// 从指针反序列化
        /// </summary>
        public static Entity DeserializeFromPtr(void* ptr)
        {
            return new Entity(*(ulong*)ptr);
        }

        /// <summary>
        /// 序列化实体数组
        /// </summary>
        public static byte[] SerializeBatch(ReadOnlySpan<Entity> entities)
        {
            byte[] result = new byte[entities.Length * sizeof(ulong)];

            fixed (byte* ptr = result)
            fixed (Entity* src = entities)
            {
                ulong* dst = (ulong*)ptr;
                Entity* srcPtr = src;

                for (int i = 0; i < entities.Length; i++)
                {
                    dst[i] = srcPtr[i].Raw;
                }
            }

            return result;
        }

        /// <summary>
        /// 反序列化实体数组
        /// </summary>
        public static void DeserializeBatch(ReadOnlySpan<byte> data, Span<Entity> entities)
        {
            int count = data.Length / sizeof(ulong);
            if (count > entities.Length)
                throw new ArgumentException("Entities span too small", nameof(entities));

            fixed (byte* ptr = data)
            fixed (Entity* dst = entities)
            {
                ulong* src = (ulong*)ptr;
                Entity* dstPtr = dst;

                for (int i = 0; i < count; i++)
                {
                    dstPtr[i] = new Entity(src[i]);
                }
            }
        }
    }

    /// <summary>
    /// 调试点视窗属性（用于IDE调试器）
    /// </summary>
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    [DebuggerTypeProxy(typeof(EntityDebuggerProxy))]
    public readonly struct EntityDebugView
    {
        private readonly Entity _entity;

        public EntityDebugView(Entity entity)
        {
            _entity = entity;
        }

        private string DebuggerDisplay => _entity.ToString();

        public int Index => _entity.Index;
        public int Version => _entity.Version;
        public ulong Raw => _entity.Raw;
        public bool IsValid => _entity.IsValid;

        /// <summary>
        /// 版本号（去除ActiveBit）
        /// </summary>
        public int Generation => _entity.Version & EntityRegistry.VersionMask;

        /// <summary>
        /// 是否活跃（最高位标志）
        /// </summary>
        public bool IsActive => (_entity.Version & EntityRegistry.ActiveBit) != 0;

        private class EntityDebuggerProxy
        {
            private readonly Entity _entity;

            public EntityDebuggerProxy(Entity entity)
            {
                _entity = entity;
            }

            [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
            public Dictionary<string, object> Properties => new()
            {
                ["Index"] = _entity.Index,
                ["Version"] = _entity.Version,
                ["Generation"] = _entity.Version & EntityRegistry.VersionMask,
                ["IsActive"] = (_entity.Version & EntityRegistry.ActiveBit) != 0,
                ["Raw"] = _entity.Raw,
                ["IsValid"] = _entity.IsValid,
                ["Hex"] = $"0x{_entity.Raw:X16}"
            };
        }
    }
}
