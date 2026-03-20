#nullable enable

using System;
using System.Collections.Generic;
using Sideline.Facet.Application;
using Sideline.Facet.Runtime;

namespace Sideline.Facet.Lua
{
    /// <summary>
    /// 提供给 Lua 页面控制器的受限 API 桥接。
    /// 只暴露节点解析、Binding 刷新、命令查询、页面路由、状态袋和红点占位能力。
    /// </summary>
    public sealed class LuaApiBridge
    {
        private readonly UIContext _context;
        private readonly ILuaRedDotBridge _redDotBridge;

        public LuaApiBridge(UIContext context, ILuaRedDotBridge redDotBridge)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(redDotBridge);

            _context = context;
            _redDotBridge = redDotBridge;
        }

        public string PageId => _context.PageId;

        public string Layer => _context.Layer;

        public string? ControllerScript => _context.Definition.ControllerScript;

        public IReadOnlyDictionary<string, object?> Arguments => _context.Arguments;

        public IUIObjectResolver? Resolver => _context.Resolver;

        public IUIBindingScope? Bindings => _context.Bindings;

        public bool HasResolver => Resolver != null;

        public bool HasBindings => Bindings != null;

        public bool CanGoBack
        {
            get
            {
                if (!TryGetNavigator(out IUIPageNavigator? navigator) || navigator == null)
                {
                    return false;
                }

                return navigator.CanGoBack;
            }
        }

        public int BackStackDepth
        {
            get
            {
                if (!TryGetNavigator(out IUIPageNavigator? navigator) || navigator == null)
                {
                    return 0;
                }

                return navigator.BackStackDepth;
            }
        }

        public bool SupportsRedDot => _redDotBridge.IsAvailable;

        public bool TryResolve<TObject>(string key, out TObject? value) where TObject : class
        {
            value = null;
            if (Resolver == null || !Resolver.TryResolve(key, out object? rawValue))
            {
                return false;
            }

            value = rawValue as TObject;
            return value != null;
        }

        public bool HasNode(string key)
        {
            return Resolver?.TryResolve(key, out object? _) == true;
        }

        public void RefreshBindings(string reason)
        {
            Bindings?.RefreshAll(reason);
        }

        public bool TryOpenPage(string pageId, IReadOnlyDictionary<string, object?>? arguments = null, bool pushHistory = true)
        {
            if (!TryGetNavigator(out IUIPageNavigator? navigator) || navigator == null)
            {
                return false;
            }

            navigator.Open(pageId, arguments, pushHistory);
            return true;
        }

        public bool TryGoBack()
        {
            return TryGetNavigator(out IUIPageNavigator? navigator) && navigator != null && navigator.GoBack();
        }

        public AppResult Send(ICommand command)
        {
            ArgumentNullException.ThrowIfNull(command);

            try
            {
                return _context.RuntimeContext.CommandBus
                    .SendAsync(command)
                    .GetAwaiter()
                    .GetResult();
            }
            catch (Exception exception)
            {
                return AppResult.Fail("lua.command.exception", exception.Message);
            }
        }

        public AppResult<TResult> Send<TResult>(ICommand<TResult> command)
        {
            ArgumentNullException.ThrowIfNull(command);

            try
            {
                return _context.RuntimeContext.CommandBus
                    .SendAsync(command)
                    .GetAwaiter()
                    .GetResult();
            }
            catch (Exception exception)
            {
                return AppResult<TResult>.Fail("lua.command.exception", exception.Message);
            }
        }

        public AppResult<TResult> Query<TResult>(IQuery<TResult> query)
        {
            ArgumentNullException.ThrowIfNull(query);

            try
            {
                return _context.RuntimeContext.QueryBus
                    .QueryAsync(query)
                    .GetAwaiter()
                    .GetResult();
            }
            catch (Exception exception)
            {
                return AppResult<TResult>.Fail("lua.query.exception", exception.Message);
            }
        }

        public bool TryGetRedDot(string path, out bool hasRedDot)
        {
            return _redDotBridge.TryGetState(path, out hasRedDot);
        }

        public void SetStateString(string key, string value)
        {
            _context.SetControllerState(key, value);
        }

        public string GetStateString(string key, string fallback = "")
        {
            if (_context.TryGetControllerState(key, out object? value) && value is string text)
            {
                return text;
            }

            return fallback;
        }

        public void SetStateNumber(string key, double value)
        {
            _context.SetControllerState(key, value);
        }

        public double GetStateNumber(string key, double fallback = 0)
        {
            if (_context.TryGetControllerState(key, out object? value))
            {
                return value switch
                {
                    double doubleValue => doubleValue,
                    float floatValue => floatValue,
                    int intValue => intValue,
                    long longValue => longValue,
                    _ => fallback,
                };
            }

            return fallback;
        }

        public void SetStateBoolean(string key, bool value)
        {
            _context.SetControllerState(key, value);
        }

        public bool GetStateBoolean(string key, bool fallback = false)
        {
            if (_context.TryGetControllerState(key, out object? value) && value is bool booleanValue)
            {
                return booleanValue;
            }

            return fallback;
        }

        public void ClearState(string key)
        {
            _context.RemoveControllerState(key);
        }

        public void LogInfoText(string message)
        {
            LogInfo(message, null);
        }

        public void LogWarningText(string message)
        {
            LogWarning(message, null);
        }

        public void LogErrorText(string message)
        {
            LogError(message, null);
        }

        public void LogInfo(string message, IReadOnlyDictionary<string, object?>? payload = null)
        {
            _context.Logger.Info("Lua.Controller", message, CreatePayload(payload));
        }

        public void LogWarning(string message, IReadOnlyDictionary<string, object?>? payload = null)
        {
            _context.Logger.Warning("Lua.Controller", message, CreatePayload(payload));
        }

        public void LogError(string message, IReadOnlyDictionary<string, object?>? payload = null)
        {
            _context.Logger.Error("Lua.Controller", message, CreatePayload(payload));
        }

        private bool TryGetNavigator(out IUIPageNavigator? navigator)
        {
            return _context.Services.TryGet(out navigator);
        }

        private Dictionary<string, object?> CreatePayload(IReadOnlyDictionary<string, object?>? payload)
        {
            Dictionary<string, object?> snapshot = new(StringComparer.OrdinalIgnoreCase)
            {
                ["pageId"] = PageId,
                ["layer"] = Layer,
                ["controllerScript"] = ControllerScript,
                ["argumentCount"] = Arguments.Count,
                ["controllerStateCount"] = _context.ControllerState.Count,
                ["hasResolver"] = HasResolver,
                ["hasBindings"] = HasBindings,
                ["canGoBack"] = CanGoBack,
                ["backStackDepth"] = BackStackDepth,
                ["supportsRedDot"] = SupportsRedDot,
            };

            if (payload == null)
            {
                return snapshot;
            }

            foreach (KeyValuePair<string, object?> pair in payload)
            {
                snapshot[pair.Key] = pair.Value;
            }

            return snapshot;
        }
    }
}