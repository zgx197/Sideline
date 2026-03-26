using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace Lattice.ECS.Session
{
    /// <summary>
    /// 当前运行时正式推荐的玩家输入模型。
    /// 输入对象本身只表达“哪个玩家、哪个 tick 的输入状态是什么”，
    /// 不再把传输层 payload 序列化职责直接绑进核心输入接口。
    /// </summary>
    public interface IPlayerInput
    {
        int PlayerId { get; }

        int Tick { get; }
    }

    /// <summary>
    /// 输入 payload 的外层编解码适配器。
    /// 当运行时需要把输入状态与网络 / 文件 / 回放 payload 对接时，应通过外层 codec 完成，
    /// 而不是把序列化职责重新塞回 `IPlayerInput` 主契约。
    /// 若该 codec 会形成稳定 payload 协议，请优先实现 `IVersionedInputPayloadCodec{TInput}`，
    /// 显式声明 codec 身份与版本。
    /// </summary>
    public interface IInputPayloadCodec<TInput>
        where TInput : IPlayerInput
    {
        byte[] Serialize(TInput input);

        TInput Deserialize(int playerId, int tick, byte[] payload);
    }

    /// <summary>
    /// 兼容旧输入接口。
    /// 新代码默认应优先使用 `IPlayerInput` 与外层 `IInputPayloadCodec{TInput}`。
    /// </summary>
    [Obsolete("Use IPlayerInput for the runtime input model and move payload serialization into IInputPayloadCodec<TInput>.", false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public interface IInputCommand : IPlayerInput
    {
        byte[] Serialize();

        void Deserialize(byte[] data);
    }

    /// <summary>
    /// 当前运行时正式承诺的输入契约模型类型。
    /// </summary>
    public enum SessionInputContractKind
    {
        /// <summary>
        /// 以 tick 为范围聚合多个玩家输入，形成稳定 `SessionInputSet`。
        /// </summary>
        TickScopedPlayerInputSet = 0
    }

    /// <summary>
    /// 缺失输入的正式语义。
    /// </summary>
    public enum SessionMissingInputPolicy
    {
        /// <summary>
        /// 缺失输入不会自动补默认值或沿用上一帧输入，而是直接从当前 tick 的输入集中省略。
        /// </summary>
        OmitMissingPlayers = 0
    }

    /// <summary>
    /// 同一 `(playerId, tick)` 被重复写入时的正式语义。
    /// </summary>
    public enum SessionInputWritePolicy
    {
        /// <summary>
        /// 同一玩家同一 tick 的输入以最后一次写入为准。
        /// </summary>
        LatestWriteWins = 0
    }

    /// <summary>
    /// 当前运行时对 tick 内玩家输入遍历顺序的正式语义。
    /// </summary>
    public enum SessionInputOrder
    {
        /// <summary>
        /// 按玩家 ID 升序遍历输入，避免依赖字典枚举顺序。
        /// </summary>
        PlayerIdAscending = 0
    }

    /// <summary>
    /// 当前运行时正式承诺的输入契约能力。
    /// </summary>
    [Flags]
    public enum SessionRuntimeInputCapability
    {
        None = 0,
        TickScopedInputAggregation = 1 << 0,
        SparsePlayerInput = 1 << 1,
        StablePlayerOrdering = 1 << 2,
        LatestWriteWins = 1 << 3,
        TransportDecoupledInputModel = 1 << 4
    }

    /// <summary>
    /// 当前运行时明确不承诺的输入契约能力。
    /// </summary>
    [Flags]
    public enum UnsupportedSessionRuntimeInputCapability
    {
        None = 0,
        ImplicitPreviousInputCarryForward = 1 << 0,
        BuiltInDefaultInputSynthesis = 1 << 1,
        ConfigurableInputAggregation = 1 << 2,
        ConfigurableMissingInputPolicy = 1 << 3,
        BuiltInTransportSerialization = 1 << 4
    }

    /// <summary>
    /// 当前运行时的正式输入契约边界描述。
    /// 用于把“输入模型、聚合顺序、缺失语义、重复写入语义”显式暴露在代码层。
    /// </summary>
    public sealed class SessionRuntimeInputBoundary
    {
        public SessionRuntimeInputBoundary(
            SessionInputContractKind contractKind,
            SessionMissingInputPolicy missingInputPolicy,
            SessionInputWritePolicy writePolicy,
            SessionInputOrder playerOrder,
            SessionRuntimeInputCapability supportedCapabilities,
            UnsupportedSessionRuntimeInputCapability unsupportedCapabilities)
        {
            ContractKind = contractKind;
            MissingInputPolicy = missingInputPolicy;
            WritePolicy = writePolicy;
            PlayerOrder = playerOrder;
            SupportedCapabilities = supportedCapabilities;
            UnsupportedCapabilities = unsupportedCapabilities;
        }

        public SessionInputContractKind ContractKind { get; }

        public SessionMissingInputPolicy MissingInputPolicy { get; }

        public SessionInputWritePolicy WritePolicy { get; }

        public SessionInputOrder PlayerOrder { get; }

        public SessionRuntimeInputCapability SupportedCapabilities { get; }

        public UnsupportedSessionRuntimeInputCapability UnsupportedCapabilities { get; }

        public bool Supports(SessionRuntimeInputCapability capability)
        {
            return (SupportedCapabilities & capability) == capability;
        }

        public bool ExplicitlyDoesNotSupport(UnsupportedSessionRuntimeInputCapability capability)
        {
            return (UnsupportedCapabilities & capability) == capability;
        }
    }

    /// <summary>
    /// 单个玩家在某个 tick 内的输入条目。
    /// </summary>
    public readonly struct SessionPlayerInput
    {
        public SessionPlayerInput(IPlayerInput input)
        {
            ArgumentNullException.ThrowIfNull(input);
            Input = input;
        }

        public IPlayerInput Input { get; }

        public int PlayerId => Input.PlayerId;

        public int Tick => Input.Tick;

        public bool TryGet<TInput>(out TInput? input)
            where TInput : class, IPlayerInput
        {
            input = Input as TInput;
            return input != null;
        }
    }

    /// <summary>
    /// 某个 tick 聚合后的输入集合。
    /// 当前正式语义是：仅包含实际写入的玩家输入，并按玩家 ID 升序稳定遍历。
    /// </summary>
    public readonly struct SessionInputSet
    {
        private readonly List<SessionPlayerInput>? _inputs;

        internal SessionInputSet(int tick, List<SessionPlayerInput> inputs)
        {
            Tick = tick;
            _inputs = inputs;
        }

        public int Tick { get; }

        public int Count => _inputs?.Count ?? 0;

        public SessionPlayerInput this[int index] => _inputs![index];

        public bool TryGetPlayerInput<TInput>(int playerId, out TInput? input)
            where TInput : class, IPlayerInput
        {
            if (_inputs != null)
            {
                for (int i = 0; i < _inputs.Count; i++)
                {
                    if (_inputs[i].PlayerId == playerId && _inputs[i].TryGet(out input))
                    {
                        return true;
                    }
                }
            }

            input = null;
            return false;
        }

        public Enumerator GetEnumerator()
        {
            return new Enumerator(_inputs);
        }

        public struct Enumerator
        {
            private readonly List<SessionPlayerInput>? _inputs;
            private int _index;

            internal Enumerator(List<SessionPlayerInput>? inputs)
            {
                _inputs = inputs;
                _index = -1;
            }

            public SessionPlayerInput Current => _inputs![_index];

            public bool MoveNext()
            {
                if (_inputs == null)
                {
                    return false;
                }

                int next = _index + 1;
                if (next >= _inputs.Count)
                {
                    return false;
                }

                _index = next;
                return true;
            }
        }
    }
}
