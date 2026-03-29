# Godot 发布实现计划

> 本文档记录 Sideline 当前已经落地的 Godot Windows x64 发布方案、目录职责、workflow 入口与维护约束。当前范围只覆盖 `Windows x64 -> GitHub Actions artifact -> GitHub Release`，不包含 Steam 分发。

---

## 当前结论

Sideline 当前已经完成一套独立于现有 PR Gate / Main Validation / Benchmark 的 Godot 发布链路，正式边界如下：

- 平台：`Windows x64`
- 版本入口：`v*` tag
- 验证入口：手动触发 artifact-only workflow
- 正式发布入口：push tag 触发 GitHub Release workflow
- 产物形式：`Sideline-vX.Y.Z-win-x64.zip`
- 发布渠道：`GitHub Release`

这套发布链路已经与现有 CI 验证流程分离，不再让 benchmark、主干验证或 PR 门禁代偿导出与发布职责。

---

## 已完成状态

下表对应当前这轮发布实现的 5 个步骤，当前状态均已落地：

| 步骤 | 内容 | 当前状态 |
| --- | --- | --- |
| 1 | 修订发布方案文档，使其与仓库现状一致 | 已完成 |
| 2 | 冻结唯一的 CI Godot 导出环境准备方案 | 已完成 |
| 3 | 将本地发布脚本重构为 CI 友好的导出 / smoke test / 打包脚本体系 | 已完成 |
| 4 | 实现 artifact-only workflow，用于先验证导出、启动验证与打包链路 | 已完成 |
| 5 | 实现 tag 触发的 GitHub Release workflow 与维护文档 | 已完成 |

---

## 最终方案

### 1. 唯一工具链方案

当前固定采用以下方案准备 Godot 导出环境：

- workflow 运行时下载固定版本的 Godot Mono / .NET Windows 工具链
- workflow 运行时下载固定版本的 export templates
- 所有下载链接和版本拼装都由仓库内脚本生成
- GitHub runner 上使用隔离的 `APPDATA` 目录，避免与本机 Godot 编辑器状态耦合

当前固定参数：

- Godot 版本：`4.6.1`
- Release 状态：`stable`
- 产品变体：`mono`
- export templates 版本目录：`4.6.1.stable.mono`

### 2. 脚本职责拆分

当前发布脚本已经按职责固定为下面四层：

- `tools/release/shared/Resolve-ReleaseMetadata.ps1`
  负责版本号、产物命名、Release 标题等平台无关元数据
- `tools/release/shared/Resolve-GodotToolchainConfig.ps1`
  负责 Godot 下载地址、安装目录、模板目录与版本约束
- `tools/release/shared/Install-GodotToolchain.ps1`
  负责下载、解压并校验 Godot 工具链和 export templates
- `tools/release/windows-x64/Test-WindowsX64ReleasePrereqs.ps1`
  负责导出前置检查，包括 preset、模板版本、Lua 资源纳入与 x64 架构声明
- `tools/release/windows-x64/Export-WindowsX64Release.ps1`
  负责导出 Windows x64 包并校验核心产物
- `tools/release/windows-x64/Test-WindowsX64ReleaseSmoke.ps1`
  负责启动导出的 `Sideline.exe`，并通过结构化日志校验最小运行链路
- `tools/release/windows-x64/Package-WindowsX64Release.ps1`
  负责压缩打包并校验 zip 内部关键条目
- `tools/release/windows-x64/Invoke-WindowsX64LocalRelease.ps1`
  负责本地一键串联导出、smoke test 与打包
- `tools/release/github-release/New-GitHubReleaseNotes.ps1`
  负责生成 Release 说明文本
- `tools/release/github-release/Publish-GitHubRelease.ps1`
  负责创建 / 更新 GitHub Release 并上传 zip asset

### 3. Workflow 分层

当前仓库中发布相关 workflow 已固定为两条：

- `.github/workflows/godot-release-artifact.yml`
  手动触发，仅执行 `prepare -> install toolchain -> export -> smoke test -> package -> upload artifact`
- `.github/workflows/godot-windows-release.yml`
  由 `push tags: v*` 触发，执行 `prepare -> export -> smoke test -> package -> upload artifact -> publish release`

这两条 workflow 的边界已经固定：

- artifact workflow 用于验证导出链路，不创建 GitHub Release
- tag release workflow 用于正式版本发布，并始终先上传 artifact 再创建 / 更新 Release
- 两条 workflow 都必须在上传 artifact 前完成一次最小运行验证

---

## 当前目录约定

### 仓库内实现目录

- `tools/release/shared/`
- `tools/release/windows-x64/`
- `tools/release/github-release/`
- `.github/workflows/godot-release-artifact.yml`
- `.github/workflows/godot-windows-release.yml`

### 运行时产物目录

以下目录不入库，只用于本地或 CI 运行：

- `artifacts/toolchains/`
- `artifacts/release/windows-x64/`

---

## 当前验证结果

本轮实现已经完成以下本地验证：

- 发布前置检查脚本可正确识别：
  - Godot 4.6.1 stable mono 控制台可执行文件
  - `export_presets.cfg`
  - `Windows Desktop` 导出预设
  - `x86_64` 架构声明
  - `scripts/facet/LuaScripts/*.lua` 纳入导出
  - `4.6.1.stable.mono` export templates
- `Export-WindowsX64Release.ps1` 已在隔离 `APPDATA` 场景下完成本地导出验证
- `Test-WindowsX64ReleaseSmoke.ps1` 已成功启动导出包，并确认：
  - 运行时环境为 `runtime`
  - 使用打包资源 `usesPackagedResources=true`
  - 初始页面为 `client.idle`
  - Lua 控制器正常挂接到 `idle_runtime.lua`
- `Package-WindowsX64Release.ps1` 已成功产出并校验 zip 包
- `Invoke-WindowsX64LocalRelease.ps1` 已成功完成一键导出、启动验证与打包

本地验证产物示例：

- `artifacts/release/windows-x64-wrapper-proof/Sideline-v0.1.0-win-x64.zip`

---

## 使用方式

### 1. 本地验证 artifact 链路

先准备 Godot 可执行文件或本机已安装 Godot，再执行：

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\release\windows-x64\Invoke-WindowsX64LocalRelease.ps1 `
  -Version v0.1.0 `
  -GodotRoot D:\GodotCSharp\Godot_v4.6.1-stable_mono_win64
```

默认会执行：

- 导出
- smoke test
- 打包

如果希望更贴近 CI 场景，建议传入隔离的 `-AppDataRoot`，避免与本机 Godot 编辑器状态混用。

### 2. 验证 GitHub Actions artifact workflow

在 GitHub Actions 中手动触发：

- `Godot Release Artifact`

输入：

- `version = v0.1.0`

### 3. 正式发布

推送版本 tag，例如：

```powershell
git tag v0.1.0
git push origin v0.1.0
```

随后由 `Godot Windows x64 Release` workflow 自动完成：

- Windows x64 导出
- 启动 `Sideline.exe` 做最小运行验证
- zip 打包
- artifact 上传
- GitHub Release 创建或更新
- Release asset 上传

---

## 当前约束

当前仍然明确不做：

- Steam 发布
- 多平台导出矩阵
- benchmark / validation workflow 代偿 release 职责
- 在仓库里直接提交 Godot 二进制或 export templates 大文件

---

## 后续可继续优化的方向

在当前基线已经稳定的前提下，后续收益更高的方向包括：

1. 给 Godot 工具链下载增加缓存层，缩短 release workflow 耗时
2. 让 artifact workflow 在 `workflow_dispatch` 之外支持定时健康检查
3. 为 smoke test 增加更多关键页面或关键切换场景验证，但保持最小运行集职责清晰
4. 为 Release notes 接入更完整的版本摘要或 changelog 来源
5. 在 Windows x64 发布稳定后，再讨论 Steam 或更多平台矩阵
