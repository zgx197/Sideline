---
name: godot-problems-inspector
description: Collect and summarize Godot C# editor problems, MSBuild diagnostics, and exported runtime or editor logs for this project. Use when Codex needs a fast, repeatable way to inspect Godot Problems output, group diagnostics by file, identify common root-cause categories, or parse an exported Godot/MSBuild log.
---

# Godot Problems Inspector

Use the bundled script to turn noisy Godot or MSBuild diagnostics into a grouped summary.

## Quick Start

Run the script in one of these modes:

```powershell
# Trigger dotnet build for the Godot C# project and summarize Problems
powershell -ExecutionPolicy Bypass -File tools/codex-skills/godot-problems-inspector/scripts/get_godot_problems.ps1

# Include warnings
powershell -ExecutionPolicy Bypass -File tools/codex-skills/godot-problems-inspector/scripts/get_godot_problems.ps1 -IncludeWarnings

# Parse an exported Godot or MSBuild log file
powershell -ExecutionPolicy Bypass -File tools/codex-skills/godot-problems-inspector/scripts/get_godot_problems.ps1 -Mode LogFile -LogPath path\to\godot-output.log
```

## Workflow

1. Prefer `Auto` or `Build` mode when the goal is to reproduce the current C# `Problems` set.
2. Use `LogFile` mode when the Godot editor already shows the errors but the current environment should not rebuild, or when only exported output text is available.
3. Read the grouped summary first. It classifies several common buckets:
   - `godot-sdk-resolution-failed`
   - `duplicate-generated-assembly-attributes`
   - `test-sources-compiled-into-main-project`
   - `lattice-generator-missing`
   - `godot-resource-path-invalid`
4. If the summary still mixes multiple root causes, drill into the top files first instead of reading the whole raw log.

## Notes

- This skill does not read Godot's in-memory Problems panel directly. It reconstructs the same class of diagnostics from build output or exported logs.
- In sandboxed environments, `dotnet build` may fail early on SDK restore or network access. The script still captures and summarizes those failures.
- The raw transcript path is printed in the summary so you can inspect untouched output when needed.
