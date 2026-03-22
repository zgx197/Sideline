#nullable enable

namespace Sideline.Facet.Application
{
    /// <summary>
    /// Facet 应用层命令标记接口。
    /// </summary>
    public interface ICommand
    {
    }

    /// <summary>
    /// 带返回值的 Facet 应用层命令标记接口。
    /// </summary>
    public interface ICommand<TResult>
    {
    }
}
