# Problem Sources

## 1. Godot C# and MSBuild

Most entries shown in the Godot Mono editor `Problems` view come from the C# build pipeline rather than from scene validation.
Typical examples:

- `MSB4236` or `Godot.NET.Sdk` resolution failures
- duplicate attributes in `.godot\mono\temp\obj`
- xUnit symbols from `Tests` compiled into the main project
- missing `Tools\SwizzleGenerator` related symbols

## 2. Godot Editor and Runtime Output

Some issues only appear in Godot output, for example:

- `Cannot navigate to 'res://...'`
- resource import or script loading failures
- runtime node, resource, or path errors

When those are already visible in the editor and a rebuild is not desired, export the output text and use `-Mode LogFile`.

## 3. Why This Is Not MCP Yet

A full MCP server is only justified when live editor state, persistent subscriptions, or editor control APIs are needed.
For diagnostics collection, a local parsing script is lower maintenance and easier to trust.
