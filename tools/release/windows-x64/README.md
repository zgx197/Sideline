# Windows x64 Release Helpers

本目录存放 Sideline 当前 `Windows x64` 发布链路的脚本。

当前已落地脚本：

- `Test-WindowsX64ReleasePrereqs.ps1`
  检查导出预设、x64 架构、Lua 资源纳入、模板版本和 Godot 可执行文件
- `Export-WindowsX64Release.ps1`
  负责执行 Godot Windows x64 导出并校验核心产物
- `Test-WindowsX64ReleaseSmoke.ps1`
  负责启动导出的 `Sideline.exe`，并通过结构化日志校验运行时、初始页面和 Lua 控制器是否正常接入
- `Package-WindowsX64Release.ps1`
  负责压缩 publish 目录，并校验 zip 中的关键条目
- `Invoke-WindowsX64LocalRelease.ps1`
  负责在本地串联导出、smoke test 与打包，作为一键入口

它只负责 `Windows x64` 平台，不处理 GitHub Release 创建。
