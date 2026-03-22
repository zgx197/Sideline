# Output Sources

## Primary Source

For this repository on Windows, the most stable source for Godot output is:

- `%APPDATA%\Godot\app_userdata\Sideline\logs\godot.log`
- `%APPDATA%\Godot\app_userdata\Sideline\logs\godotYYYY-MM-DDTHH.MM.SS.log`

These files match the runtime output stream closely enough for diagnostics and are easier to automate than scraping editor UI controls.

## Secondary Source

C# build diagnostics live elsewhere:

- `%APPDATA%\Godot\mono\build_logs\...\msbuild_log.txt`

That source belongs to `godot-problems-inspector`, not this skill.

## API Boundary

The installed `GodotSharpEditor.xml` exposes editor plugin, bottom panel, and debugger session APIs, but no clear public API for reading back the existing Output panel text buffer.

What the API can help with:

- add an editor dock or bottom panel
- add an `EditorDebuggerPlugin`
- send custom messages between game and editor through debugger channels
- emit logs with `GD.Print`, `GD.PushWarning`, and `GD.PushError`

What it does not obviously expose:

- enumerate existing Output panel lines
- subscribe to all built-in output messages from outside the producing process
- reconstruct old Output panel contents after the fact without reading log files