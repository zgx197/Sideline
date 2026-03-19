#nullable enable

using System;
using System.Collections.Generic;

namespace Sideline.Facet.Runtime
{
    /// <summary>
    /// Facet 最小服务容器。
    /// </summary>
    public sealed class FacetServices
    {
        private readonly Dictionary<Type, object> _services = new();

        /// <summary>
        /// 注册或替换单例服务。
        /// </summary>
        public void RegisterSingleton<TService>(TService instance) where TService : class
        {
            ArgumentNullException.ThrowIfNull(instance);
            _services[typeof(TService)] = instance;
        }

        /// <summary>
        /// 尝试获取服务。
        /// </summary>
        public bool TryGet<TService>(out TService? instance) where TService : class
        {
            if (_services.TryGetValue(typeof(TService), out object? service))
            {
                instance = (TService)service;
                return true;
            }

            instance = null;
            return false;
        }

        /// <summary>
        /// 获取必须存在的服务。
        /// </summary>
        public TService GetRequired<TService>() where TService : class
        {
            if (TryGet<TService>(out TService? instance) && instance != null)
            {
                return instance;
            }

            throw new InvalidOperationException($"Facet service not registered: {typeof(TService).FullName}");
        }

        /// <summary>
        /// 检查服务是否已注册。
        /// </summary>
        public bool Contains<TService>() where TService : class
        {
            return _services.ContainsKey(typeof(TService));
        }
    }
}