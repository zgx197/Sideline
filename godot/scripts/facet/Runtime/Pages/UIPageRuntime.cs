#nullable enable

using System;
using System.Collections.Generic;
using Godot;
using Sideline.Facet.Layout;
using Sideline.Facet.Lua;
using Sideline.Facet.UI;

namespace Sideline.Facet.Runtime
{
    /// <summary>
    /// 页面运行时壳子。
    /// 负责持有页面根节点、节点注册表、上下文、C# 生命周期对象、Lua 控制器和统一日志。
    /// </summary>
    public sealed class UIPageRuntime
    {
        private readonly IFacetLogger? _logger;
        private readonly IUIPageLifecycle? _pageLifecycle;
        private readonly ILuaRuntimeHost? _luaRuntimeHost;
        private LuaControllerHandle? _luaController;

        public UIPageRuntime(
            UIPageDefinition definition,
            UILayoutResult layoutResult,
            FacetRuntimeContext runtimeContext,
            IFacetLogger? logger = null)
        {
            ArgumentNullException.ThrowIfNull(definition);
            ArgumentNullException.ThrowIfNull(layoutResult);
            ArgumentNullException.ThrowIfNull(runtimeContext);

            Definition = definition;
            PageRoot = layoutResult.RootNode;
            NodeRegistry = layoutResult.NodeRegistry;
            NodeResolver = layoutResult.NodeResolver;
            Context = new UIContext(definition, runtimeContext);
            Context.AttachResolver(NodeResolver);
            _logger = logger;
            _pageLifecycle = PageRoot as IUIPageLifecycle;
            _luaRuntimeHost = ResolveLuaRuntimeHost(runtimeContext);
            State = UIPageState.Created;

            if (runtimeContext.Services.TryGet<UIBindingService>(out UIBindingService? bindingService) &&
                bindingService != null)
            {
                BindingScope = bindingService.CreateScope(definition.PageId, NodeResolver);
                Context.AttachBindings(BindingScope);
            }

            if (!string.IsNullOrWhiteSpace(definition.ControllerScript) &&
                _luaRuntimeHost != null &&
                _luaRuntimeHost.TryCreateController(Context, existingApi: null, out LuaControllerHandle? luaController) &&
                luaController != null)
            {
                _luaController = luaController;
            }

            LogLifecycle("Create", null, null);
        }

        public UIPageDefinition Definition { get; }

        public Control PageRoot { get; }

        public UINodeRegistry NodeRegistry { get; }

        public UINodeResolver NodeResolver { get; }

        public UIContext Context { get; }

        public IUIBindingScope? BindingScope { get; }

        public UIPageState State { get; private set; }

        public bool IsDisposed => State == UIPageState.Disposed;

        public bool HasLuaController => _luaController != null;

        public string? LuaControllerScript => _luaController?.ScriptId;

        public string? LuaControllerVersionToken => _luaController?.VersionToken;

        public void Initialize(IReadOnlyDictionary<string, object?>? arguments = null)
        {
            if (State != UIPageState.Created)
            {
                return;
            }

            Context.UpdateArguments(arguments);
            _pageLifecycle?.OnPageInitialize(Context);
            InvokeLuaController("OnInit", static controller => controller.OnInit());

            UIPageState previousState = State;
            State = UIPageState.Initialized;
            LogLifecycle("Initialize", previousState, arguments);
        }

        public void Show(IReadOnlyDictionary<string, object?>? arguments = null)
        {
            EnsureNotDisposed();
            Initialize(arguments);
            Context.UpdateArguments(arguments);

            PageRoot.Visible = true;
            _pageLifecycle?.OnPageShow(Context);
            InvokeLuaController("OnShow", static controller => controller.OnShow());

            UIPageState previousState = State;
            State = UIPageState.Shown;
            LogLifecycle("Show", previousState, arguments);
        }

        public void Refresh(IReadOnlyDictionary<string, object?>? arguments = null)
        {
            EnsureNotDisposed();
            if (State == UIPageState.Created)
            {
                Initialize(arguments);
            }

            if (State != UIPageState.Shown)
            {
                Show(arguments);
                return;
            }

            Context.UpdateArguments(arguments);
            _pageLifecycle?.OnPageRefresh(Context);
            InvokeLuaController("OnRefresh", static controller => controller.OnRefresh());
            LogLifecycle("Refresh", State, arguments);
        }

        public void Hide()
        {
            if (State != UIPageState.Shown)
            {
                return;
            }

            InvokeLuaController("OnHide", static controller => controller.OnHide());
            _pageLifecycle?.OnPageHide(Context);
            PageRoot.Visible = false;

            UIPageState previousState = State;
            State = UIPageState.Hidden;
            LogLifecycle("Hide", previousState, Context.Arguments);
        }

        public void DisposeRuntime()
        {
            if (State == UIPageState.Disposed)
            {
                return;
            }

            if (State == UIPageState.Shown)
            {
                Hide();
            }

            InvokeLuaController("OnDispose", static controller => controller.OnDispose());
            _pageLifecycle?.OnPageDispose(Context);
            BindingScope?.Dispose();

            if (GodotObject.IsInstanceValid(PageRoot))
            {
                if (Definition.LayoutType == UIPageLayoutType.PackedScene)
                {
                    PageRoot.QueueFree();
                }
                else
                {
                    PageRoot.Visible = false;
                }
            }

            UIPageState previousState = State;
            State = UIPageState.Disposed;
            LogLifecycle("Dispose", previousState, Context.Arguments);
        }

        /// <summary>
        /// 判断当前页面是否需要执行 Lua 热重载。
        /// </summary>
        public bool NeedsLuaHotReload()
        {
            if (State == UIPageState.Disposed || _luaRuntimeHost == null || _luaController == null)
            {
                return false;
            }

            return _luaRuntimeHost.NeedsReload(_luaController);
        }

        /// <summary>
        /// 尝试仅重建页面的 Lua 控制器，并复用现有页面上下文与状态袋。
        /// </summary>
        public bool TryReloadLuaController(string reason, out LuaReloadResult? result)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(reason);

            result = null;
            if (State == UIPageState.Disposed)
            {
                result = CreateReloadResult(false, null, null, reason, "页面已销毁，无法热重载。");
                return false;
            }

            if (_luaRuntimeHost == null || _luaController == null)
            {
                result = CreateReloadResult(false, null, null, reason, "页面当前没有可重载的 Lua 控制器。");
                return false;
            }

            LuaControllerHandle currentController = _luaController;
            if (!_luaRuntimeHost.NeedsReload(currentController))
            {
                result = CreateReloadResult(false, currentController.VersionToken, currentController.VersionToken, reason, "脚本版本未变化。");
                return false;
            }

            if (!_luaRuntimeHost.TryCreateController(Context, currentController.Api, out LuaControllerHandle? nextController) || nextController == null)
            {
                result = CreateReloadResult(false, currentController.VersionToken, null, reason, "新 Lua 控制器创建失败。");
                _logger?.Error(
                    "Lua.HotReload",
                    "Lua 热重载失败，新控制器未能创建。",
                    new Dictionary<string, object?>
                    {
                        ["pageId"] = Definition.PageId,
                        ["scriptId"] = currentController.ScriptId,
                        ["reason"] = reason,
                        ["oldVersionToken"] = currentController.VersionToken,
                        ["pageState"] = State.ToString(),
                    });
                return false;
            }

            try
            {
                currentController.Api.PrepareForControllerReload();
                RecoverLuaController(nextController);
            }
            catch (Exception exception)
            {
                Context.AttachLua(currentController.Api);
                currentController.Api.PrepareForControllerReload();
                TryRecoverPreviousController(currentController, reason);
                result = CreateReloadResult(false, currentController.VersionToken, nextController.VersionToken, reason, exception.Message);

                _logger?.Error(
                    "Lua.HotReload",
                    "Lua 热重载失败，新控制器恢复链路执行异常。",
                    new Dictionary<string, object?>
                    {
                        ["pageId"] = Definition.PageId,
                        ["scriptId"] = currentController.ScriptId,
                        ["reason"] = reason,
                        ["oldVersionToken"] = currentController.VersionToken,
                        ["newVersionToken"] = nextController.VersionToken,
                        ["pageState"] = State.ToString(),
                        ["exceptionType"] = exception.GetType().FullName,
                        ["message"] = exception.Message,
                    });
                return false;
            }

            _luaController = nextController;
            InvokeLuaControllerHandle(
                currentController,
                "OnDispose",
                static controller => controller.OnDispose(),
                "Lua.HotReload",
                "旧 Lua 控制器释放失败。");

            result = CreateReloadResult(true, currentController.VersionToken, nextController.VersionToken, reason, null);

            _logger?.Info(
                "Lua.HotReload",
                "页面 Lua 控制器已热重载。",
                new Dictionary<string, object?>
                {
                    ["pageId"] = Definition.PageId,
                    ["scriptId"] = nextController.ScriptId,
                    ["sourcePath"] = nextController.SourcePath,
                    ["reason"] = reason,
                    ["oldVersionToken"] = currentController.VersionToken,
                    ["newVersionToken"] = nextController.VersionToken,
                    ["pageState"] = State.ToString(),
                    ["controllerStateCount"] = Context.ControllerState.Count,
                    ["argumentCount"] = Context.Arguments.Count,
                    ["bindingCount"] = BindingScope?.Count,
                });
            return true;
        }

        private static ILuaRuntimeHost? ResolveLuaRuntimeHost(FacetRuntimeContext runtimeContext)
        {
            return runtimeContext.Services.TryGet<ILuaRuntimeHost>(out ILuaRuntimeHost? luaRuntimeHost)
                ? luaRuntimeHost
                : null;
        }

        private void EnsureNotDisposed()
        {
            if (State == UIPageState.Disposed)
            {
                throw new InvalidOperationException($"Facet page runtime already disposed: {Definition.PageId}");
            }
        }

        private void InvokeLuaController(string action, Action<LuaControllerHandle> callback)
        {
            if (_luaController == null)
            {
                return;
            }

            InvokeLuaControllerHandle(_luaController, action, callback, "Lua.Runtime", "Lua 控制器生命周期执行失败。");
        }

        private void InvokeLuaControllerHandle(
            LuaControllerHandle controller,
            string action,
            Action<LuaControllerHandle> callback,
            string category,
            string failureMessage)
        {
            try
            {
                callback(controller);
            }
            catch (Exception exception)
            {
                _logger?.Error(
                    category,
                    failureMessage,
                    new Dictionary<string, object?>
                    {
                        ["pageId"] = Definition.PageId,
                        ["controllerScript"] = controller.ScriptId,
                        ["versionToken"] = controller.VersionToken,
                        ["action"] = action,
                        ["exceptionType"] = exception.GetType().FullName,
                        ["message"] = exception.Message,
                    });
            }
        }

        private void RecoverLuaController(LuaControllerHandle controller)
        {
            if (State == UIPageState.Created)
            {
                return;
            }

            controller.OnInit();

            if (State == UIPageState.Shown)
            {
                controller.OnShow();
                controller.OnRefresh();
            }
        }

        private void TryRecoverPreviousController(LuaControllerHandle controller, string reason)
        {
            try
            {
                RecoverLuaController(controller);
                _logger?.Warning(
                    "Lua.HotReload",
                    "Lua 热重载失败，已回退到旧控制器。",
                    new Dictionary<string, object?>
                    {
                        ["pageId"] = Definition.PageId,
                        ["scriptId"] = controller.ScriptId,
                        ["versionToken"] = controller.VersionToken,
                        ["reason"] = reason,
                        ["pageState"] = State.ToString(),
                    });
            }
            catch (Exception rollbackException)
            {
                _logger?.Error(
                    "Lua.HotReload",
                    "Lua 热重载失败，旧控制器回退链路也执行异常。",
                    new Dictionary<string, object?>
                    {
                        ["pageId"] = Definition.PageId,
                        ["scriptId"] = controller.ScriptId,
                        ["versionToken"] = controller.VersionToken,
                        ["reason"] = reason,
                        ["pageState"] = State.ToString(),
                        ["exceptionType"] = rollbackException.GetType().FullName,
                        ["message"] = rollbackException.Message,
                    });
            }
        }

        private LuaReloadResult CreateReloadResult(
            bool reloaded,
            string? oldVersionToken,
            string? newVersionToken,
            string reason,
            string? errorMessage)
        {
            return new LuaReloadResult(
                reloaded,
                Definition.PageId,
                _luaController?.ScriptId ?? Definition.ControllerScript ?? string.Empty,
                oldVersionToken,
                newVersionToken,
                reason,
                State.ToString(),
                errorMessage);
        }

        private void LogLifecycle(string action, UIPageState? previousState, IReadOnlyDictionary<string, object?>? arguments)
        {
            UIBindingDiagnosticsSnapshot? bindingDiagnostics = BindingScope?.GetDiagnosticsSnapshot();

            _logger?.Info(
                "UI.Page.Lifecycle",
                $"页面生命周期 {action}。",
                new Dictionary<string, object?>
                {
                    ["pageId"] = Definition.PageId,
                    ["layoutType"] = Definition.LayoutType.ToString(),
                    ["layer"] = Definition.Layer,
                    ["controllerScript"] = Definition.ControllerScript,
                    ["previousState"] = previousState?.ToString(),
                    ["currentState"] = State.ToString(),
                    ["argumentCount"] = arguments?.Count ?? 0,
                    ["hasPageLifecycle"] = _pageLifecycle != null,
                    ["hasLuaController"] = _luaController != null,
                    ["luaControllerVersion"] = _luaController?.VersionToken,
                    ["registeredNodes"] = NodeRegistry.Count,
                    ["bindingCount"] = bindingDiagnostics?.BindingCount ?? 0,
                    ["bindingRefreshCount"] = bindingDiagnostics?.RefreshCount ?? 0,
                    ["bindingScopeId"] = bindingDiagnostics?.ScopeId,
                    ["bindingLastReason"] = bindingDiagnostics?.LastRefreshReason,
                    ["hasBindings"] = BindingScope != null,
                    ["hasLuaBridge"] = Context.Lua != null,
                    ["nodePath"] = PageRoot.GetPath().ToString(),
                });
        }
    }
}
