// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;
using System.Collections.Generic;
using Lattice.ECS.Core;
using Lattice.Math;

namespace Lattice.ECS.Framework
{
    /// <summary>
    /// 系统分组，仅作为系统层级容器。
    /// </summary>
    public sealed class SystemGroup : ISystem
    {
        private readonly ISystem[] _systems;
        private readonly SystemMetadata _metadata;

        public SystemGroup(string name, params ISystem[] systems)
            : this(name, SystemMetadata.GroupDefault, enabledByDefault: true, systems)
        {
        }

        public SystemGroup(string name, bool enabledByDefault, params ISystem[] systems)
            : this(name, SystemMetadata.GroupDefault, enabledByDefault, systems)
        {
        }

        public SystemGroup(string name, SystemMetadata metadata, params ISystem[] systems)
            : this(name, metadata, enabledByDefault: true, systems)
        {
        }

        public SystemGroup(string name, SystemMetadata metadata, bool enabledByDefault, params ISystem[] systems)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            Name = name;
            EnabledByDefault = enabledByDefault;
            _metadata = metadata with { Kind = SystemExecutionKind.Group };
            _systems = systems ?? Array.Empty<ISystem>();

            for (int i = 0; i < _systems.Length; i++)
            {
                ArgumentNullException.ThrowIfNull(_systems[i]);
            }
        }

        /// <summary>组名称。</summary>
        public string Name { get; }

        /// <summary>组元数据。</summary>
        public SystemMetadata Metadata => _metadata;

        /// <summary>组默认是否启用。</summary>
        public bool EnabledByDefault { get; }

        /// <summary>子系统列表。</summary>
        public IReadOnlyList<ISystem> Systems => _systems;

        public void OnInit(Frame frame)
        {
        }

        public void OnEnabled(Frame frame)
        {
        }

        public void OnDisabled(Frame frame)
        {
        }

        public void OnUpdate(Frame frame, FP deltaTime)
        {
        }

        public void OnDispose(Frame frame)
        {
        }

        public void OnDestroy(Frame frame)
        {
            OnDispose(frame);
        }
    }
}
