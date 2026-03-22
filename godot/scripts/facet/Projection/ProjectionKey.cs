#nullable enable

using System;

namespace Sideline.Facet.Projection
{
    /// <summary>
    /// Projection 存储主键。
    /// 用于在 ProjectionStore 中唯一标识一类视图模型槽位。
    /// </summary>
    public readonly record struct ProjectionKey(string Value)
    {
        /// <summary>
        /// 从字符串创建 ProjectionKey，并在创建时校验非空白输入。
        /// </summary>
        public static ProjectionKey From(string value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            return new ProjectionKey(value);
        }

        /// <summary>
        /// 返回底层字符串值，便于日志与调试输出。
        /// </summary>
        public override string ToString()
        {
            return Value;
        }
    }
}
