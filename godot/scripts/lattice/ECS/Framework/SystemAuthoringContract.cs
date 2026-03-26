using System;

namespace Lattice.ECS.Framework
{
    /// <summary>
    /// 系统对普通帧状态的访问意图。
    /// 当前主要用于声明系统作者契约，而不是提供一套更重的系统基类家族。
    /// </summary>
    public enum SystemFrameAccess
    {
        ReadOnly = 0,
        ReadWrite = 1
    }

    /// <summary>
    /// 系统对全局状态 API 的访问意图。
    /// </summary>
    public enum SystemGlobalAccess
    {
        None = 0,
        ReadOnly = 1,
        ReadWrite = 2
    }

    /// <summary>
    /// 系统是否允许发起结构性修改。
    /// 当前正式语义只有“不允许”与“允许发起延迟结构性修改”两档。
    /// </summary>
    public enum SystemStructuralChangeAccess
    {
        None = 0,
        Deferred = 1
    }

    /// <summary>
    /// 轻量系统作者契约。
    /// 用于声明系统在 `OnUpdate(...)` 中允许使用的状态访问能力，
    /// 让 phase、global state 与结构性修改规则不再只靠团队默契维持。
    /// </summary>
    public readonly struct SystemAuthoringContract : IEquatable<SystemAuthoringContract>
    {
        public SystemAuthoringContract(
            SystemFrameAccess frameAccess = SystemFrameAccess.ReadWrite,
            SystemGlobalAccess globalAccess = SystemGlobalAccess.None,
            SystemStructuralChangeAccess structuralChangeAccess = SystemStructuralChangeAccess.None)
        {
            if (frameAccess == SystemFrameAccess.ReadOnly && globalAccess == SystemGlobalAccess.ReadWrite)
            {
                throw new ArgumentException("Read-only systems cannot declare writable global state access.", nameof(globalAccess));
            }

            if (frameAccess == SystemFrameAccess.ReadOnly && structuralChangeAccess != SystemStructuralChangeAccess.None)
            {
                throw new ArgumentException("Read-only systems cannot declare structural change access.", nameof(structuralChangeAccess));
            }

            FrameAccess = frameAccess;
            GlobalAccess = globalAccess;
            StructuralChangeAccess = structuralChangeAccess;
        }

        /// <summary>
        /// 默认系统契约：
        /// 允许读写普通组件数据，但不默认开放 global state 与结构性修改。
        /// </summary>
        public static SystemAuthoringContract Default => new();

        /// <summary>
        /// 只读系统默认契约。
        /// </summary>
        public static SystemAuthoringContract ReadOnly => new(SystemFrameAccess.ReadOnly);

        public SystemFrameAccess FrameAccess { get; }

        public SystemGlobalAccess GlobalAccess { get; }

        public SystemStructuralChangeAccess StructuralChangeAccess { get; }

        public bool AllowsGlobalReads => GlobalAccess != SystemGlobalAccess.None;

        public bool AllowsGlobalWrites => GlobalAccess == SystemGlobalAccess.ReadWrite;

        public bool AllowsStructuralChanges => StructuralChangeAccess == SystemStructuralChangeAccess.Deferred;

        public bool Equals(SystemAuthoringContract other)
        {
            return FrameAccess == other.FrameAccess &&
                   GlobalAccess == other.GlobalAccess &&
                   StructuralChangeAccess == other.StructuralChangeAccess;
        }

        public override bool Equals(object? obj)
        {
            return obj is SystemAuthoringContract other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine((int)FrameAccess, (int)GlobalAccess, (int)StructuralChangeAccess);
        }

        internal void ValidateForPhase(SystemPhase phase, Type systemType)
        {
            if (!AllowsGlobalWrites)
            {
                return;
            }

            if (phase == SystemPhase.Input || phase == SystemPhase.Resolve || phase == SystemPhase.Cleanup)
            {
                return;
            }

            throw new InvalidOperationException(
                $"System {systemType.Name} declares writable global state access, but phase {phase} only supports global writes in Input, Resolve, or Cleanup.");
        }
    }
}
