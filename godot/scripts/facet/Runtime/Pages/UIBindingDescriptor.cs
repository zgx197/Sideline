#nullable enable

namespace Sideline.Facet.Runtime
{
    /// <summary>
    /// 单条 Binding 的结构化描述。
    /// 用于诊断页面当前到底注册了哪些绑定关系。
    /// </summary>
    public sealed record UIBindingDescriptor(
        string Kind,
        string Key,
        string TargetType,
        string? Notes = null);
}
