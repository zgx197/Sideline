#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;


namespace Sideline.Facet.Runtime
{
    /// <summary>
    /// Facet 闁哄牃鍋撻悘蹇撶箲濠€鍥礉閳ユ剚鍟囬柛锝冨妸閳?    /// </summary>
    public sealed class FacetServices : IDisposable
    {
        private readonly Dictionary<Type, object> _services = new();

        /// <summary>
        /// 婵炲鍔岄崬浠嬪箣閺嶃劍绂岄柟骞垮灩瀹曠喐绗熺€ｎ偅绠涢柛鏂呮壋鍋?        /// </summary>
        public void RegisterSingleton<TService>(TService instance) where TService : class
        {
            ArgumentNullException.ThrowIfNull(instance);
            _services[typeof(TService)] = instance;
        }

        /// <summary>
        /// 閻忓繑绻嗛惁顖炴嚔瀹勬澘绲块柡鍫濈Т婵喖濡?        /// </summary>
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
        /// 闁兼儳鍢茶ぐ鍥疀閸涙番鈧繒鈧稒锚濠€顏堟儍閸曨剚绠涢柛鏂呮壋鍋?        /// </summary>
        public TService GetRequired<TService>() where TService : class
        {
            if (TryGet<TService>(out TService? instance) && instance != null)
            {
                return instance;
            }

            throw new InvalidOperationException($"Facet service not registered: {typeof(TService).FullName}");
        }

        /// <summary>
        /// 婵☆偀鍋撻柡灞诲劜濠€鍥礉閳╁啯笑闁告熬绠戦崙鈥斥枖閵娿儱鏂€闁?        /// </summary>
        public bool Contains<TService>() where TService : class
        {
            return _services.ContainsKey(typeof(TService));
        }

        public void Dispose()
        {
            HashSet<object> disposedInstances = new(ReferenceEqualityComparer.Instance);
            List<Exception> exceptions = new();

            foreach (object service in _services.Values)
            {
                if (!disposedInstances.Add(service) || service is not IDisposable disposable)
                {
                    continue;
                }

                try
                {
                    disposable.Dispose();
                }
                catch (Exception exception)
                {
                    exceptions.Add(exception);
                }
            }

            _services.Clear();

            if (exceptions.Count > 0)
            {
                throw new AggregateException("Facet service disposal failed.", exceptions);
            }
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static ReferenceEqualityComparer Instance { get; } = new();

            public new bool Equals(object? x, object? y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(object obj)
            {
                return RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}
