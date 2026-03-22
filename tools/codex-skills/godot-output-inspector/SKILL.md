---
name: godot-output-inspector
description: Inspect and summarize Godot runtime or editor output logs for this project. Use when Codex needs the latest Godot Output window content, wants to read app_userdata project logs, inspect Facet structured logs, compare recent runs, filter by session or category, tail the newest run, or separate runtime/editor output from MSBuild Problems diagnostics.
---

# Godot Output Inspector

Use the bundled scripts to read the latest Godot runtime output log or the Facet structured log for the project without rebuilding the C# solution.

## Quick Start

Run one of these commands:

```powershell
# Read the current project's active Godot output log
powershell -ExecutionPolicy Bypass -File tools/codex-skills/godot-output-inspector/scripts/get_godot_output.ps1

# Show a longer tail from the plain Godot output log
powershell -ExecutionPolicy Bypass -File tools/codex-skills/godot-output-inspector/scripts/get_godot_output.ps1 -Tail 120

# Parse the Facet structured JSONL log and group by category and session
powershell -ExecutionPolicy Bypass -File tools/codex-skills/godot-output-inspector/scripts/get_facet_structured_logs.ps1

# Filter the Facet structured log by category prefix and minimum level
powershell -ExecutionPolicy Bypass -File tools/codex-skills/godot-output-inspector/scripts/get_facet_structured_logs.ps1 -Category Client.WindowManager -MinimumLevel Info

# Filter one specific runtime session
powershell -ExecutionPolicy Bypass -File tools/codex-skills/godot-output-inspector/scripts/get_facet_structured_logs.ps1 -SessionId 0123abcd
```

## Workflow

1. Prefer `get_godot_output.ps1` when the user asks for the current Godot Output contents.
2. Prefer `get_facet_structured_logs.ps1` when the goal is filtering, grouping, or diagnosing Facet and client-layer runtime behavior.
3. Read the summary first. The plain output report preserves chronology, while the structured report preserves session, event id, level, category, timestamp, and payload.
4. Use the tail excerpt to compare with the editor Output panel screenshot.
5. If the log is empty or missing, ask whether the user has actually run the project after the last clear.

## Difference From `godot-problems-inspector`

- `godot-problems-inspector` focuses on C# `Problems` style diagnostics, mainly from MSBuild or exported error logs.
- `godot-output-inspector` focuses on the Godot Output stream itself and Facet runtime logs: engine banner, runtime `GD.Print`, `GD.PushError`, resource or path errors, application logs, and Facet structured JSONL events.
- `godot-problems-inspector` is optimized for root-cause grouping by file and error code.
- `godot-output-inspector` is optimized for chronological runtime inspection and structured filtering by session, category, and level.

## Notes

- This skill does not scrape the editor Output panel UI directly.
- For the current Windows setup, the most reliable plain-text source is `%APPDATA%\Godot\app_userdata\<ProjectName>\logs\godot.log`.
- The Facet structured log defaults to `%APPDATA%\Godot\app_userdata\<ProjectName>\logs\facet-structured.jsonl`.
- `GodotSharpEditor` public APIs are useful for editor plugins and debugger integrations, but there is no obvious public `GetOutputPanelLines()` style API in the installed XML docs.
- If later we need true live capture inside the editor, build an `EditorPlugin` or runtime log sink that mirrors logs to a file or custom debugger channel at emission time.