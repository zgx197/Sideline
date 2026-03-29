#nullable enable

using System;
using System.Collections.Generic;
using Sideline.Facet.Lua;

namespace Sideline.Facet.Runtime
{
    /// <summary>
    /// Lua 鐑噸杞藉紑鍙戞祴璇曟湇鍔°€?    /// 閫氳繃瀵圭洰鏍囪剼鏈墽琛屼竴娆℃棤璇箟褰卞搷鐨勬枃浠舵敼鍔紝鍐嶄富鍔ㄨ疆璇㈢儹閲嶈浇鍗忚皟鍣紝
    /// 楠岃瘉鐪熷疄鏂囦欢鍙樻洿閾捐矾鏄惁鑳藉瀹屾垚鍓嶅悜涓庡洖婊氫袱娆℃帶鍒跺櫒閲嶅缓銆?    /// </summary>
    public sealed class LuaHotReloadTestService
    {
        private const string ProbePrefix = "-- facet_hot_reload_probe:";

        private readonly UIManager _uiManager;
        private readonly LuaReloadCoordinator _reloadCoordinator;
        private readonly ILuaScriptSource _scriptSource;
        private readonly ILuaWritableScriptSource? _writableScriptSource;
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
            _writableScriptSource = scriptSource as ILuaWritableScriptSource;
            _logger = logger;
        }

        /// <summary>
        /// 褰撳墠鏄惁宸叉湁姝ｅ湪鎵ц涓殑鐑噸杞芥祴璇曘€?        /// </summary>
        public bool IsRunning => _isRunning;

        /// <summary>
        /// 涓诲姩鎵ц涓€娆?Lua 鐑噸杞藉線杩旀祴璇曘€?        /// 榛樿浼樺厛閫夋嫨褰撳墠椤甸潰杩愯鏃舵墍浣跨敤鐨?Lua 鎺у埗鍣ㄨ剼鏈€?        /// </summary>
        public bool TryRunRoundTripTest(string? scriptId = null, string reason = "manual")
        {
            if (_isRunning)
            {
                _logger?.Warning(
                    "Lua.HotReload.Test",
                    "Lua hot reload test request ignored because another test is already running.",
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
            if (!string.IsNullOrWhiteSpace(requestedScriptId) &&
                (_writableScriptSource == null || !_writableScriptSource.CanWriteScript(requestedScriptId)))
            {
                _logger?.Warning(
                    "Lua.HotReload.Test",
                    "Current script source cannot write the requested hot reload target.",
                    new Dictionary<string, object?>
                    {
                        ["requestedScriptId"] = requestedScriptId,
                        ["reason"] = reason,
                        ["currentPageId"] = _uiManager.CurrentPageId,
                        ["scriptSourceType"] = _scriptSource.GetType().Name,
                    });
                return false;
            }
            if (!TryResolveTarget(requestedScriptId, out TargetResolution? target) ||
                target == null)
            {
                return false;
            }
            try
            {
                UIPageRuntime runtime = target.Runtime;
                LuaScriptAsset scriptAsset = target.ScriptAsset;
                string scriptId = scriptAsset.ScriptId;
                string sourcePath = scriptAsset.SourcePath;
                string originalContent = scriptAsset.SourceCode;
                string originalVersion = runtime.LuaControllerVersionToken ?? scriptAsset.VersionToken;
                if (_writableScriptSource == null || !_writableScriptSource.CanWriteScript(scriptId))
                {
                    _logger?.Warning(
                        "Lua.HotReload.Test",
                        "Current script source cannot write the resolved hot reload target.",
                        new Dictionary<string, object?>
                        {
                            ["pageId"] = runtime.Definition.PageId,
                            ["scriptId"] = scriptId,
                            ["sourcePath"] = sourcePath,
                            ["reason"] = reason,
                            ["scriptSourceType"] = _scriptSource.GetType().Name,
                        });
                    return false;
                }
                string forwardContent = CreateProbeVariant(originalContent);
                if (string.Equals(originalContent, forwardContent, StringComparison.Ordinal))
                {
                    _logger?.Error(
                        "Lua.HotReload.Test",
                        "Unable to create a valid forward Lua script variant for hot reload testing.",
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
                    "Lua hot reload round-trip test started.",
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
                        "Lua hot reload round-trip test threw an exception.",
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
                }
            }
            finally
            {
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
            if (_writableScriptSource == null ||
                !_writableScriptSource.TryWriteScript(scriptId, nextContent, out LuaScriptAsset? updatedScriptAsset) ||
                updatedScriptAsset == null)
            {
                LuaHotReloadTestPhaseResult writeFailedResult = new(
                    phase,
                    false,
                    previousVersion,
                    null,
                    runtime.LuaControllerVersionToken,
                    0,
                    "Failed to write the target Lua script during hot reload testing.");
                LogPhaseResult(runtime, scriptId, sourcePath, pollReason, writeFailedResult);
                return writeFailedResult;
            }
            string expectedVersion = updatedScriptAsset.VersionToken;
            int reloadedCount = _reloadCoordinator.Poll(pollReason);
            string? actualVersion = runtime.LuaControllerVersionToken;
            bool versionChanged = !string.Equals(previousVersion, actualVersion, StringComparison.Ordinal);
            bool success = reloadedCount > 0 &&
                versionChanged &&
                string.Equals(expectedVersion, actualVersion, StringComparison.Ordinal);
            string? errorMessage = success
                ? null
                : "Hot reload polling did not align runtime version with script version.";
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
            if (_writableScriptSource == null ||
                !_scriptSource.TryGetVersionToken(scriptId, out string? currentFileVersion) ||
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
            if (!_writableScriptSource.TryWriteScript(scriptId, originalContent, out _))
            {
                _logger?.Warning(
                    "Lua.HotReload.Test",
                    "Lua hot reload test failed to restore the original script content.",
                    new Dictionary<string, object?>
                    {
                        ["pageId"] = runtime.Definition.PageId,
                        ["scriptId"] = scriptId,
                        ["sourcePath"] = sourcePath,
                        ["reason"] = restoreReason,
                    });
                return;
            }
            int reloadedCount = _reloadCoordinator.Poll(restoreReason);
            _logger?.Info(
                "Lua.HotReload.Test",
                "Lua hot reload test applied a restore correction.",
                new Dictionary<string, object?>
                {
                    ["pageId"] = runtime.Definition.PageId,
                    ["scriptId"] = scriptId,
                    ["sourcePath"] = sourcePath,
                    ["reason"] = restoreReason,
                    ["restoredVersionToken"] = originalVersion,
                    ["runtimeVersionToken"] = runtime.LuaControllerVersionToken,
                    ["reloadedRuntimeCount"] = reloadedCount,
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
                    "No suitable page runtime with a Lua controller was found for hot reload testing.",
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
                    "Failed to read the target Lua script for hot reload testing.",
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
                        "Restored the page that was active before the hot reload test.",
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
                    "Closed the page that was opened temporarily for the hot reload test.",
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
                    "An exception occurred while restoring the page opened for hot reload testing.",
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
                _logger?.Info("Lua.HotReload.Test", "Lua hot reload test phase passed.", payload);
                return;
            }

            _logger?.Warning("Lua.HotReload.Test", "Lua hot reload test phase failed.", payload);
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
                _logger?.Info("Lua.HotReload.Test", "Lua hot reload round-trip test completed.", payload);
                return;
            }

            _logger?.Warning("Lua.HotReload.Test", "Lua hot reload round-trip test did not fully pass.", payload);
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
