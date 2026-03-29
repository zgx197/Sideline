#nullable enable

using System;
using System.Collections.Generic;
using Godot;
using Sideline.Facet.Layout;

namespace Sideline.Facet.Runtime
{
    /// <summary>
    /// 妞ょ敻娼版潻鎰攽閺冨墎顓搁悶鍡楁珤閵?    /// 鐠愮喕鐭楁い鐢告桨閸掓稑缂撻妴浣规▔缁€鎭掆偓渚€娈ｉ挊蹇嬧偓渚€鏀㈠В浣碘偓浣界箲閸ョ偞鐖ら崪宀€绱︾€涙鐡ラ悾銉ｂ偓?    /// </summary>
    public sealed class UIManager : IUIPageNavigator, IDisposable
    {
        private readonly UIPageRegistry _pageRegistry;
        private readonly UIPageLoader _pageLoader;
        private readonly UIRouteService _routeService;
        private readonly FacetRuntimeContext _runtimeContext;
        private readonly IFacetLogger? _logger;
        private readonly Dictionary<string, UIPageRuntime> _pageRuntimes = new(StringComparer.OrdinalIgnoreCase);

        private Node? _mountRoot;
        private UIPageRuntime? _currentRuntime;

        public UIManager(
            UIPageRegistry pageRegistry,
            UIPageLoader pageLoader,
            UIRouteService routeService,
            FacetRuntimeContext runtimeContext,
            IFacetLogger? logger = null)
        {
            _pageRegistry = pageRegistry;
            _pageLoader = pageLoader;
            _routeService = routeService;
            _runtimeContext = runtimeContext;
            _logger = logger;
        }

        public string? CurrentPageId => _currentRuntime?.Definition.PageId;

        public UIPageRuntime? CurrentRuntime => _currentRuntime;

        public bool CanGoBack => _routeService.CanGoBack;

        public int BackStackDepth => _routeService.Count;

        public void AttachMountRoot(Node mountRoot)
        {
            ArgumentNullException.ThrowIfNull(mountRoot);
            _mountRoot = mountRoot;

            _logger?.Info(
                "UI.Page",
                "妞ょ敻娼伴幐鍌濇祰閺嶇濡悙鐟板嚒缂佹垵鐣鹃妴?",
                new Dictionary<string, object?>
                {
                    ["mountRootPath"] = mountRoot.GetPath().ToString(),
                    ["registeredPages"] = _pageRegistry.Count,
                });
        }

        /// <summary>
        /// 閼惧嘲褰囪ぐ鎾冲瀹告彃鍨卞娲€夐棃銏ｇ箥鐞涘本妞傞惃鍕彥閻撗佲偓?        /// 閻戭參鍣告潪钘夊礂鐠嬪啫娅掓担璺ㄦ暏鐠囥儱鎻╅悡褑鐤嗙拠銏ｅ壖閺堫剛澧楅張顒€褰夐崠鏍モ偓?        /// </summary>
        public IReadOnlyList<UIPageRuntime> GetPageRuntimesSnapshot()
        {
            List<UIPageRuntime> snapshot = new();
            HashSet<string> pageIds = new(StringComparer.OrdinalIgnoreCase);

            if (_currentRuntime != null && pageIds.Add(_currentRuntime.Definition.PageId))
            {
                snapshot.Add(_currentRuntime);
            }

            foreach (UIPageRuntime runtime in _pageRuntimes.Values)
            {
                if (pageIds.Add(runtime.Definition.PageId))
                {
                    snapshot.Add(runtime);
                }
            }

            return snapshot;
        }

        public UIPageRuntime Open(
            string pageId,
            IReadOnlyDictionary<string, object?>? arguments = null,
            bool pushHistory = true)
        {
            if (_mountRoot == null)
            {
                throw new InvalidOperationException("Facet UIManager mount root has not been attached.");
            }

            UIPageDefinition definition = _pageRegistry.GetRequired(pageId);
            if (_currentRuntime != null &&
                string.Equals(_currentRuntime.Definition.PageId, definition.PageId, StringComparison.OrdinalIgnoreCase))
            {
                if (_currentRuntime.State != UIPageState.Shown)
                {
                    _currentRuntime.Show(arguments);
                }

                _currentRuntime.Refresh(arguments);
                LogOpen(definition, _currentRuntime, loadedFromCache: true, pushHistory: false, reusedCurrent: true);
                return _currentRuntime;
            }

            if (_currentRuntime != null)
            {
                if (pushHistory)
                {
                    _routeService.Push(_currentRuntime.Definition.PageId, _currentRuntime.Context.Arguments);
                }

                HideOrDisposeRuntime(_currentRuntime);
            }

            UILayoutResult layoutResult = _pageLoader.Load(definition, _mountRoot!);
            UIPageRuntime runtime = GetOrCreateRuntime(definition, layoutResult, out bool loadedFromCache);
            runtime.Show(arguments);
            runtime.Refresh(arguments);
            _currentRuntime = runtime;

            LogOpen(definition, runtime, loadedFromCache, pushHistory, reusedCurrent: false);
            return runtime;
        }

        public bool GoBack()
        {
            if (!_routeService.TryPop(out UIRouteEntry? entry) || entry == null)
            {
                _logger?.Warning("UI.Page", "妞ょ敻娼版潻鏂挎礀鐠囬攱鐪扮悮顐㈡嫹閻ｃ儻绱濊ぐ鎾冲鏉╂柨娲栭弽鍫滆礋缁屾亽鈧?, null");
                return false;
            }

            UIPageRuntime runtime = Open(entry.PageId, entry.Arguments, pushHistory: false);
            _logger?.Info(
                "UI.Page",
                "妞ょ敻娼版潻鏂挎礀鐎瑰本鍨氶妴?",
                new Dictionary<string, object?>
                {
                    ["pageId"] = runtime.Definition.PageId,
                    ["backStackDepth"] = BackStackDepth,
                    ["currentState"] = runtime.State.ToString(),
                    ["hasLuaController"] = runtime.HasLuaController,
                });
            return true;
        }

        public bool CloseCurrent()
        {
            if (_currentRuntime == null)
            {
                return false;
            }

            UIPageRuntime runtime = _currentRuntime;
            HideOrDisposeRuntime(runtime);
            _currentRuntime = null;

            _logger?.Info(
                "UI.Page",
                "瑜版挸澧犳い鐢告桨瀹告彃鍙ч梻顓溾偓?",
                new Dictionary<string, object?>
                {
                    ["pageId"] = runtime.Definition.PageId,
                    ["backStackDepth"] = BackStackDepth,
                    ["state"] = runtime.State.ToString(),
                    ["hasLuaController"] = runtime.HasLuaController,
                });
            return true;
        }

        void IUIPageNavigator.Open(string pageId, IReadOnlyDictionary<string, object?>? arguments, bool pushHistory)
        {
            Open(pageId, arguments, pushHistory);
        }

        bool IUIPageNavigator.GoBack()
        {
            return GoBack();
        }

        public void Dispose()
        {
            HashSet<UIPageRuntime> disposedRuntimes = new();

            if (_currentRuntime != null)
            {
                DisposeRuntime(_currentRuntime, disposedRuntimes);
                _currentRuntime = null;
            }

            foreach (UIPageRuntime runtime in _pageRuntimes.Values)
            {
                DisposeRuntime(runtime, disposedRuntimes);
            }

            _pageRuntimes.Clear();
            _routeService.Clear();
            _mountRoot = null;
        }

        private UIPageRuntime GetOrCreateRuntime(UIPageDefinition definition, UILayoutResult layoutResult, out bool loadedFromCache)
        {
            loadedFromCache = _pageRuntimes.TryGetValue(definition.PageId, out UIPageRuntime? runtime) &&
                runtime != null &&
                !runtime.IsDisposed &&
                GodotObject.IsInstanceValid(runtime.PageRoot);

            if (loadedFromCache && runtime != null)
            {
                return runtime;
            }

            if (_pageRuntimes.ContainsKey(definition.PageId))
            {
                _pageRuntimes.Remove(definition.PageId);
            }

            runtime = new UIPageRuntime(definition, layoutResult, _runtimeContext, _logger);

            if (definition.CachePolicy == UIPageCachePolicy.Reuse || definition.LayoutType == UIPageLayoutType.ExistingNode)
            {
                _pageRuntimes[definition.PageId] = runtime;
            }

            return runtime;
        }

        private void HideOrDisposeRuntime(UIPageRuntime runtime)
        {
            if (runtime.Definition.CachePolicy == UIPageCachePolicy.Reuse ||
                runtime.Definition.LayoutType == UIPageLayoutType.ExistingNode)
            {
                runtime.Hide();
                return;
            }

            runtime.DisposeRuntime();
            _pageRuntimes.Remove(runtime.Definition.PageId);
        }

        private static void DisposeRuntime(UIPageRuntime runtime, ISet<UIPageRuntime> disposedRuntimes)
        {
            if (!disposedRuntimes.Add(runtime))
            {
                return;
            }

            runtime.DisposeRuntime();
        }

        private void LogOpen(
            UIPageDefinition definition,
            UIPageRuntime runtime,
            bool loadedFromCache,
            bool pushHistory,
            bool reusedCurrent)
        {
            _logger?.Info(
                "UI.Page",
                "妞ょ敻娼伴幍鎾崇磻鐎瑰本鍨氶妴?",
                new Dictionary<string, object?>
                {
                    ["pageId"] = definition.PageId,
                    ["layoutType"] = definition.LayoutType.ToString(),
                    ["layoutPath"] = definition.LayoutPath,
                    ["layer"] = definition.Layer,
                    ["cachePolicy"] = definition.CachePolicy.ToString(),
                    ["controllerScript"] = definition.ControllerScript,
                    ["loadedFromCache"] = loadedFromCache,
                    ["pushHistory"] = pushHistory,
                    ["reusedCurrent"] = reusedCurrent,
                    ["backStackDepth"] = BackStackDepth,
                    ["currentState"] = runtime.State.ToString(),
                    ["registeredNodes"] = runtime.NodeRegistry.Count,
                    ["currentPagePath"] = runtime.PageRoot.GetPath().ToString(),
                    ["hasLuaController"] = runtime.HasLuaController,
                    ["luaControllerScript"] = runtime.LuaControllerScript,
                    ["luaControllerVersion"] = runtime.LuaControllerVersionToken,
                });
        }
    }
}
