using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Lattice.Core
{
    /// <summary>
    /// 命令缓冲区 - 延迟执行实体和组件操作
    /// 
    /// 设计参考：FrameSync的延迟销毁、Bevy的Commands
    /// </summary>
    public sealed class CommandBuffer
    {
        private readonly EntityRegistry _registry;

        // 延迟销毁队列
        private Entity[] _destroyQueue;
        private int _destroyCount;

        // 默认初始容量
        private const int DefaultCapacity = 64;

        /// <summary>
        /// 待销毁实体数量
        /// </summary>
        public int PendingDestroyCount => _destroyCount;

        /// <summary>
        /// 是否有待执行命令
        /// </summary>
        public bool HasPendingCommands => _destroyCount > 0;

        public CommandBuffer(EntityRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _destroyQueue = new Entity[DefaultCapacity];
            _destroyCount = 0;
        }

        // === 实体操作 ===

        /// <summary>
        /// 延迟销毁实体
        /// </summary>
        public void Destroy(Entity entity)
        {
            if (_destroyCount >= _destroyQueue.Length)
            {
                Array.Resize(ref _destroyQueue, _destroyQueue.Length * 2);
            }
            _destroyQueue[_destroyCount++] = entity;
        }

        /// <summary>
        /// 批量延迟销毁
        /// </summary>
        public void DestroyBatch(ReadOnlySpan<Entity> entities)
        {
            int newCount = _destroyCount + entities.Length;
            if (newCount > _destroyQueue.Length)
            {
                int newSize = _destroyQueue.Length * 2;
                while (newSize < newCount) newSize *= 2;
                Array.Resize(ref _destroyQueue, newSize);
            }

            for (int i = 0; i < entities.Length; i++)
            {
                _destroyQueue[_destroyCount++] = entities[i];
            }
        }

        /// <summary>
        /// 创建并立即返回实体（延迟销毁可用）
        /// </summary>
        public Entity Create()
        {
            return _registry.Create();
        }

        /// <summary>
        /// 批量创建实体
        /// </summary>
        public void CreateBatch(Span<Entity> output)
        {
            _registry.CreateBatch(output);
        }

        // === 命令执行 ===

        /// <summary>
        /// 执行所有缓冲的命令
        /// </summary>
        public void Execute()
        {
            ExecuteDestroys();
        }

        /// <summary>
        /// 执行销毁命令（通常在帧末调用）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ExecuteDestroys()
        {
            if (_destroyCount == 0) return;

            for (int i = 0; i < _destroyCount; i++)
            {
                _registry.DestroyImmediate(_destroyQueue[i]);
                _destroyQueue[i] = default;
            }
            _destroyCount = 0;
        }

        /// <summary>
        /// 清空所有命令（不执行）
        /// </summary>
        public void Clear()
        {
            for (int i = 0; i < _destroyCount; i++)
            {
                _destroyQueue[i] = default;
            }
            _destroyCount = 0;
        }

        /// <summary>
        /// 创建并添加组件（扩展接口，待实现）
        /// </summary>
        public Entity CreateWithComponents(params Type[] componentTypes)
        {
            var entity = _registry.Create();
            // TODO: 添加组件逻辑
            return entity;
        }
    }

    /// <summary>
    /// 命令缓冲区扩展方法
    /// </summary>
    public static class CommandBufferExtensions
    {
        /// <summary>
        /// 批量销毁多个实体
        /// </summary>
        public static void Destroy(this CommandBuffer commands, IEnumerable<Entity> entities)
        {
            foreach (var entity in entities)
            {
                commands.Destroy(entity);
            }
        }

        /// <summary>
        /// 销毁满足条件的实体
        /// </summary>
        public static void DestroyWhere(this CommandBuffer commands, IEnumerable<Entity> entities, Predicate<Entity> predicate)
        {
            foreach (var entity in entities)
            {
                if (predicate(entity))
                {
                    commands.Destroy(entity);
                }
            }
        }

        /// <summary>
        /// 创建指定数量的实体
        /// </summary>
        public static Entity[] CreateMany(this CommandBuffer commands, int count)
        {
            var entities = new Entity[count];
            commands.CreateBatch(entities);
            return entities;
        }
    }
}
