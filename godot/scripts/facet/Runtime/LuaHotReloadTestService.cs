#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Sideline.Facet.Lua;

namespace Sideline.Facet.Runtime
{
    /// <summary>
    /// Lua 热重载开发测试服务。
    /// 通过对目标脚本执行一次无语义影响的文件改动，再主动轮询热重载协调器，
    /// 验证真实文件变更链路是否能够完成前向与回滚两次控制器重建。
    /// </summary>
    public sealed class LuaHotReloadTestService
    {
        private const string ProbePrefix = "-- facet_hot_reload_probe:";
        private static readonly UTF8Encoding Utf8NoBom = new(false);

        private readonly UIManager _uiManager;
        private readonly LuaReloadCoordinator _reloadCoordinator;
        private readonly ILuaScriptSource _scriptSource;
        private readonly IFacetLogger? _logger;
        private bool _isRunning;

        public LuaHotReloadTestService(
            UIManager uiManager,
            LuaReloadCoordinator reloadCoordinator,
            ILuaScriptSource scriptSource,
            IFacetLogger? logger = null)
        {
            ArgumentNullException.ThrowIfNull(uiManager);
            ArgumentNullException.ThrowIfNull(reloadCoordinator);
            ArgumentNullException.ThrowIfNull(scriptSource);

            _uiManager = uiManager;
            _reloadCoordinator = reloadCoordinator;
            _scriptSource = scriptSource;
            _logger = logger;
        }

        /// <summary>
        /// 当前是否已有正在执行中的热重载测试。
        /// </summary>
        public bool IsRunning => _isRunning;

        /// <summary>
        /// 主动执行一次 Lua 热重载往返测试。
        /// 默认优先选择当前页面运行时所使用的 Lua 控制器脚本。
        /// </summary>
        public bool TryRunRoundTripTest(string? scriptId = null, string reason = "manual")
        {
            if (_isRunning)
            {
                _logger?.Warning(
                    "Lua.HotReload.Test",
                    "Lua 热重载测试请求被忽略，已有测试正在执行。",
                    new Dictionary<string, object?>
                    {
                        ["requestedScriptId"] = scriptId,
                        ["reason"] = reason,
                    });
                return false;
            }

            _isRunning = true;
            try
            {
                return RunRoundTripTestCore(scriptId, reason);
            }
            finally
            {
                _isRunning = false;
            }
        }

        private bool RunRoundTripTestCore(string? requestedScriptId, string reason)
        {
            if (!TryResolveTarget(requestedScriptId, out TargetResolution? target) ||
                target == null)
            {
                return false;
            }

            UIPageRuntime runtime = target.Runtime;
            LuaScriptAsset scriptAsset = target.ScriptAsset;
            string scriptId = scriptAsset.ScriptId;
            string sourcePath = scriptAsset.SourcePath;
            string originalContent = scriptAsset.SourceCode;
            string originalVersion = runtime.LuaControllerVersionToken ?? scriptAsset.VersionToken;
            string forwardContent = CreateProbeVariant(originalContent);

            if (string.Equals(originalContent, forwardContent, StringComparison.Ordinal))
            {
                _logger?.Error(
                    "Lua.HotReload.Test",
                    "Lua 热重载测试无法生成有效的前向脚本变体。",
                    new Dictionary<string, object?>
                    {
                        ["pageId"] = runtime.Definition.PageId,
                        ["scriptId"] = scriptId,
                        ["sourcePath"] = sourcePath,
                        ["reason"] = reason,
                    });
                return false;
            }

            _logger?.Info(
                "Lua.HotReload.Test",
                "Lua 热重载往返测试开始。",
                new Dictionary<string, object?>
                {
                    ["pageId"] = runtime.Definition.PageId,
                    ["scriptId"] = scriptId,
                    ["sourcePath"] = sourcePath,
                    ["reason"] = reason,
                    ["originalVersionToken"] = originalVersion,
                    ["currentPageId"] = _uiManager.CurrentPageId,
                    ["currentPageState"] = runtime.State.ToString(),
                });

            LuaHotReloadTestPhaseResult? forward = null;
            LuaHotReloadTestPhaseResult? rollback = null;
            bool completed = false;

            try
            {
                forward = ExecutePhase(
                    runtime,
                    scriptId,
                    sourcePath,
                    forwardContent,
                    originalVersion,
                    $"{reason}.test.forward",
                    "forward");

                rollback = ExecutePhase(
                    runtime,
                    scriptId,
                    sourcePath,
                    originalContent,
                    forward.ActualVersion ?? runtime.LuaControllerVersionToken,
                    $"{reason}.test.rollback",
                    "rollback");

                completed = forward.Success && rollback.Success;
                LogRoundTripSummary(runtime, scriptId, sourcePath, reason, originalVersion, forward, rollback, completed);
                return completed;
            }
            catch (Exception exception)
            {
                _logger?.Error(
                    "Lua.HotReload.Test",
                    "Lua 热重载往返测试抛出异常。",
                    new Dictionary<string, object?>
                    {
                        ["pageId"] = runtime.Definition.PageId,
                        ["scriptId"] = scriptId,
                        ["sourcePath"] = sourcePath,
                        ["reason"] = reason,
                        ["originalVersionToken"] = originalVersion,
                        ["exceptionType"] = exception.GetType().FullName,
                        ["message"] = exception.Message,
                    });
                return false;
            }
            finally
            {
                EnsureOriginalState(runtime, scriptId, sourcePath, originalContent, originalVersion, $"{reason}.test.restore", completed);
                RestorePreviousPage(target, $"{reason}.test.restore_page");
            }
        }

        private LuaHotReloadTestPhaseResult ExecutePhase(
            UIPageRuntime runtime,
            string scriptId,
            string sourcePath,
            string nextContent,
            string? previousVersion,
            string pollReason,
            string phase)
        {
            File.WriteAllText(sourcePath, nextContent, Utf8NoBom);

            if (!_scriptSource.TryGetVersionToken(scriptId, out string? expectedVersion) ||
                string.IsNullOrWhiteSpace(expectedVersion))
            {
                LuaHotReloadTestPhaseResult missingVersionResult = new(
                    phase,
                    false,
                    previousVersion,
                    null,
                    runtime.LuaControllerVersionToken,
                    0,
                    "目标脚本版本标记读取失败。");

                LogPhaseResult(runtime, scriptId, sourcePath, pollReason, missingVersionResult);
                return missingVersionResult;
            }

            int reloadedCount = _reloadCoordinator.Poll(pollReason);
            string? actualVersion = runtime.LuaControllerVersionToken;
            bool versionChanged = !string.Equals(previousVersion, actualVersion, StringComparison.Ordinal);
            bool success = reloadedCount > 0 &&
                versionChanged &&
                string.Equals(expectedVersion, actualVersion, StringComparison.Ordinal);

            string? errorMessage = success
                ? null
                : "热重载轮询未能让运行时版本与脚本文件版本一致。";

            LuaHotReloadTestPhaseResult result = new(
                phase,
                success,
                previousVersion,
                expectedVersion,
                actualVersion,
                reloadedCount,
                errorMessage);

            LogPhaseResult(runtime, scriptId, sourcePath, pollReason, result);
            return result;
        }

        private void EnsureOriginalState(
            UIPageRuntime runtime,
            string scriptId,
            string sourcePath,
            string originalContent,
            string originalVersion,
            string restoreReason,
            bool alreadyRestored)
        {
            if (!_scriptSource.TryGetVersionToken(scriptId, out string? currentFileVersion) ||
                string.IsNullOrWhiteSpace(currentFileVersion))
            {
                return;
            }

            if (alreadyRestored &&
                string.Equals(currentFileVersion, originalVersion, StringComparison.Ordinal) &&
                string.Equals(runtime.LuaControllerVersionToken, originalVersion, StringComparison.Ordinal))
            {
                return;
            }

            File.WriteAllText(sourcePath, originalContent, Utf8NoBom);
            int reloadedCount = _reloadCoordinator.Poll(restoreReason);

            _logger?.Info(
                "Lua.HotReload.Test",
                "Lua 热重载测试已执行文件恢复校正。",
                new Dictionary<string, object?>
                {
                    ["pageId"] = runtime.Definition.PageId,
                    ["scriptId"] = scriptId,
                    ["sourcePath"] = sourcePath,
                    ["reason"] = restoreReason,
                    ["restoredVersionToken"] = originalVersion,
                    ["runtimeVersionToken"] = runtime.LuaControllerVersionToken,
                    ["reloadedRuntimeCount"] = reloadedCount,
                    ["restoreSucceeded"] = string.Equals(runtime.LuaControllerVersionToken, originalVersion, StringComparison.Ordinal),
                });
        }

        private bool TryResolveTarget(
            string? requestedScriptId,
            out TargetResolution? target)
        {
            target = null;

            string? scriptId = requestedScriptId;
            string? previousPageId = _uiManager.CurrentPageId;
            IReadOnlyDictionary<string, object?>? previousArguments = CloneArguments(_uiManager.CurrentRuntime?.Context.Arguments);
            bool temporarilyOpened = false;
            UIPageRuntime? runtime = null;
            LuaScriptAsset? scriptAsset = null;

            if (string.IsNullOrWhiteSpace(scriptId))
            {
                runtime = _uiManager.CurrentRuntime;
                scriptId = runtime?.LuaControllerScript;
            }
            else
            {
                foreach (UIPageRuntime candidate in _uiManager.GetPageRuntimesSnapshot())
                {
                    if (!candidate.HasLuaController)
                    {
                        continue;
                    }

                    if (string.Equals(candidate.LuaControllerScript, scriptId, StringComparison.OrdinalIgnoreCase))
                    {
                        runtime = candidate;
                        break;
                    }
                }
            }

            if ((runtime == null || !runtime.HasLuaController) &&
                !string.IsNullOrWhiteSpace(scriptId) &&
                TryGetPageIdForScript(scriptId, out string? pageId))
            {
                runtime = _uiManager.Open(pageId, arguments: null, pushHistory: false);
                temporarilyOpened = !string.Equals(previousPageId, runtime.Definition.PageId, StringComparison.OrdinalIgnoreCase);
            }

            if (runtime == null ||
                !runtime.HasLuaController ||
                string.IsNullOrWhiteSpace(scriptId))
            {
                _logger?.Warning(
                    "Lua.HotReload.Test",
                    "Lua 热重载测试未找到可用的目标页面运行时。",
                    new Dictionary<string, object?>
                    {
                        ["requestedScriptId"] = requestedScriptId,
                        ["currentPageId"] = _uiManager.CurrentPageId,
                        ["currentHasLuaController"] = _uiManager.CurrentRuntime?.HasLuaController,
                    });
                return false;
            }

            if (!_scriptSource.TryGetScript(scriptId, out scriptAsset) || scriptAsset == null)
            {
                _logger?.Error(
                    "Lua.HotReload.Test",
                    "Lua 热重载测试未能读取目标脚本源文件。",
                    new Dictionary<string, object?>
                    {
                        ["pageId"] = runtime.Definition.PageId,
                        ["scriptId"] = scriptId,
                    });
                return false;
            }

            target = new TargetResolution(runtime, scriptAsset, previousPageId, previousArguments, temporarilyOpened);
            return true;
        }

        private void RestorePreviousPage(TargetResolution target, string reason)
        {
            if (!target.TemporarilyOpened)
            {
                return;
            }

            try
            {
                if (!string.IsNullOrWhiteSpace(target.PreviousPageId))
                {
                    UIPageRuntime restoredRuntime = _uiManager.Open(target.PreviousPageId, target.PreviousArguments, pushHistory: false);
                    _logger?.Info(
                        "Lua.HotReload.Test",
                        "Lua 热重载测试已恢复先前页面。",
                        new Dictionary<string, object?>
                        {
                            ["reason"] = reason,
                            ["restoredPageId"] = restoredRuntime.Definition.PageId,
                            ["currentPageId"] = _uiManager.CurrentPageId,
                            ["currentPageState"] = restoredRuntime.State.ToString(),
                        });
                    return;
                }

                bool closed = _uiManager.CloseCurrent();
                _logger?.Info(
                    "Lua.HotReload.Test",
                    "Lua 热重载测试已关闭临时打开的页面。",
                    new Dictionary<string, object?>
                    {
                        ["reason"] = reason,
                        ["closed"] = closed,
                        ["currentPageId"] = _uiManager.CurrentPageId,
                    });
            }
            catch (Exception exception)
            {
                _logger?.Warning(
                    "Lua.HotReload.Test",
                    "Lua 热重载测试恢复先前页面时出现异常。",
                    new Dictionary<string, object?>
                    {
                        ["reason"] = reason,
                        ["previousPageId"] = target.PreviousPageId,
                        ["currentPageId"] = _uiManager.CurrentPageId,
                        ["exceptionType"] = exception.GetType().FullName,
                        ["message"] = exception.Message,
                    });
            }
        }

        private void LogPhaseResult(
            UIPageRuntime runtime,
            string scriptId,
            string sourcePath,
            string reason,
            LuaHotReloadTestPhaseResult result)
        {
            IReadOnlyDictionary<string, object?> payload = new Dictionary<string, object?>
            {
                ["pageId"] = runtime.Definition.PageId,
                ["scriptId"] = scriptId,
                ["sourcePath"] = sourcePath,
                ["phase"] = result.Phase,
                ["reason"] = reason,
                ["previousVersionToken"] = result.PreviousVersion,
                ["expectedVersionToken"] = result.ExpectedVersion,
                ["actualVersionToken"] = result.ActualVersion,
                ["reloadedRuntimeCount"] = result.ReloadedRuntimeCount,
                ["pageState"] = runtime.State.ToString(),
                ["success"] = result.Success,
                ["errorMessage"] = result.ErrorMessage,
            };

            if (result.Success)
            {
                _logger?.Info("Lua.HotReload.Test", "Lua 热重载测试阶段已通过。", payload);
                return;
            }

            _logger?.Warning("Lua.HotReload.Test", "Lua 热重载测试阶段未通过。", payload);
        }

        private void LogRoundTripSummary(
            UIPageRuntime runtime,
            string scriptId,
            string sourcePath,
            string reason,
            string originalVersion,
            LuaHotReloadTestPhaseResult forward,
            LuaHotReloadTestPhaseResult rollback,
            bool success)
        {
            IReadOnlyDictionary<string, object?> payload = new Dictionary<string, object?>
            {
                ["pageId"] = runtime.Definition.PageId,
                ["scriptId"] = scriptId,
                ["sourcePath"] = sourcePath,
                ["reason"] = reason,
                ["originalVersionToken"] = originalVersion,
                ["forwardSuccess"] = forward.Success,
                ["forwardExpectedVersionToken"] = forward.ExpectedVersion,
                ["forwardActualVersionToken"] = forward.ActualVersion,
                ["rollbackSuccess"] = rollback.Success,
                ["rollbackExpectedVersionToken"] = rollback.ExpectedVersion,
                ["rollbackActualVersionToken"] = rollback.ActualVersion,
                ["runtimeVersionToken"] = runtime.LuaControllerVersionToken,
                ["pageState"] = runtime.State.ToString(),
                ["success"] = success,
            };

            if (success)
            {
                _logger?.Info("Lua.HotReload.Test", "Lua 热重载往返测试已完成。", payload);
                return;
            }

            _logger?.Warning("Lua.HotReload.Test", "Lua 热重载往返测试未完全通过。", payload);
        }

        private static string CreateProbeVariant(string originalContent)
        {
            string newline = originalContent.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            string currentProbeToken = TryReadProbeToken(originalContent);
            string nextProbeToken = string.Equals(currentProbeToken, "a", StringComparison.Ordinal) ? "b" : "a";
            string probeLine = $"{ProbePrefix}{nextProbeToken}";

            int firstNewlineIndex = originalContent.IndexOf(newline, StringComparison.Ordinal);
            if (firstNewlineIndex >= 0)
            {
                string firstLine = originalContent[..firstNewlineIndex];
                string rest = originalContent[firstNewlineIndex..];
                if (firstLine.StartsWith(ProbePrefix, StringComparison.Ordinal))
                {
                    return probeLine + rest;
                }
            }
            else if (originalContent.StartsWith(ProbePrefix, StringComparison.Ordinal))
            {
                return probeLine;
            }

            return probeLine + newline + originalContent;
        }

        private static string TryReadProbeToken(string content)
        {
            int newlineIndex = content.IndexOf('\n');
            string firstLine = newlineIndex >= 0
                ? content[..newlineIndex].TrimEnd('\r')
                : content;

            if (!firstLine.StartsWith(ProbePrefix, StringComparison.Ordinal))
            {
                return string.Empty;
            }

            return firstLine[ProbePrefix.Length..].Trim();
        }

        private static IReadOnlyDictionary<string, object?>? CloneArguments(IReadOnlyDictionary<string, object?>? arguments)
        {
            if (arguments == null || arguments.Count == 0)
            {
                return null;
            }

            return new Dictionary<string, object?>(arguments);
        }

        private static bool TryGetPageIdForScript(string scriptId, out string pageId)
        {
            switch (scriptId)
            {
                case FacetLuaScriptIds.IdleRuntimeController:
                    pageId = UIPageIds.Idle;
                    return true;
                case FacetLuaScriptIds.DungeonRuntimeController:
                    pageId = UIPageIds.Dungeon;
                    return true;
                default:
                    pageId = string.Empty;
                    return false;
            }
        }

        private sealed class LuaHotReloadTestPhaseResult
        {
            public LuaHotReloadTestPhaseResult(
                string phase,
                bool success,
                string? previousVersion,
                string? expectedVersion,
                string? actualVersion,
                int reloadedRuntimeCount,
                string? errorMessage)
            {
                Phase = phase;
                Success = success;
                PreviousVersion = previousVersion;
                ExpectedVersion = expectedVersion;
                ActualVersion = actualVersion;
                ReloadedRuntimeCount = reloadedRuntimeCount;
                ErrorMessage = errorMessage;
            }

            public string Phase { get; }

            public bool Success { get; }

            public string? PreviousVersion { get; }

            public string? ExpectedVersion { get; }

            public string? ActualVersion { get; }

            public int ReloadedRuntimeCount { get; }

            public string? ErrorMessage { get; }
        }

        private sealed class TargetResolution
        {
            public TargetResolution(
                UIPageRuntime runtime,
                LuaScriptAsset scriptAsset,
                string? previousPageId,
                IReadOnlyDictionary<string, object?>? previousArguments,
                bool temporarilyOpened)
            {
                Runtime = runtime;
                ScriptAsset = scriptAsset;
                PreviousPageId = previousPageId;
                PreviousArguments = previousArguments;
                TemporarilyOpened = temporarilyOpened;
            }

            public UIPageRuntime Runtime { get; }

            public LuaScriptAsset ScriptAsset { get; }

            public string? PreviousPageId { get; }

            public IReadOnlyDictionary<string, object?>? PreviousArguments { get; }

            public bool TemporarilyOpened { get; }
        }
    }
}
