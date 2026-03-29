# Godot 发布操作说明

> 本文档说明 Sideline 当前 Godot Windows x64 发布链路的实际操作方式，包括 artifact 验证、正式 tag 发布、重发、回滚和排障入口。

---

## 适用范围

当前只适用于：

- 平台：`Windows x64`
- 发布渠道：`GitHub Release`
- 版本入口：`v*` tag

不适用于：

- Steam 发布
- 多平台导出
- 非语义化版本标签

---

## 相关入口

### Workflow

- artifact 验证：`.github/workflows/godot-release-artifact.yml`
- 正式发布：`.github/workflows/godot-windows-release.yml`

### 脚本

- 工具链安装：`tools/release/shared/Install-GodotToolchain.ps1`
- 导出前置检查：`tools/release/windows-x64/Test-WindowsX64ReleasePrereqs.ps1`
- Windows x64 导出：`tools/release/windows-x64/Export-WindowsX64Release.ps1`
- Windows x64 启动验证：`tools/release/windows-x64/Test-WindowsX64ReleaseSmoke.ps1`
- Windows x64 打包：`tools/release/windows-x64/Package-WindowsX64Release.ps1`
- GitHub Release 发布：`tools/release/github-release/Publish-GitHubRelease.ps1`

---

## 正式发布前建议流程

推荐始终按下面顺序执行：

1. 先在本地或手动 workflow 验证 artifact 链路。
2. 确认当前提交就是要发布的提交。
3. 使用 `vX.Y.Z` 语义版本创建 tag。
4. 推送 tag，让正式发布 workflow 自动执行。

不要直接把“第一次验证导出链路”的动作放到正式 tag 上。

---

## 手动验证 artifact workflow

在 GitHub Actions 中手动触发：

- `Godot Release Artifact`

输入：

- `version`：例如 `v0.1.0`

这条 workflow 会完成：

- 准备版本元数据
- 下载并安装固定版本 Godot 工具链与 export templates
- 导出 Windows x64
- 启动 `Sideline.exe` 做最小运行验证
- 打包 zip
- 上传 publish 目录 artifact
- 上传 zip artifact

它不会创建 GitHub Release。

---

## 正式发布流程

### 1. 创建并推送 tag

```powershell
git tag v0.1.0
git push origin v0.1.0
```

### 2. GitHub Actions 自动执行

`Godot Windows x64 Release` workflow 会自动执行以下步骤：

- 准备 Release 元数据
- 下载并安装固定版本 Godot 工具链
- 导出 Windows x64
- 启动 `Sideline.exe` 做最小运行验证
- 打包为 `Sideline-v0.1.0-win-x64.zip`
- 上传 artifact
- 创建或更新 GitHub Release
- 上传同名 zip 作为 Release asset

### 3. 发布结果确认

发布完成后，至少确认：

- workflow 成功结束
- Release 页面存在对应版本
- Release asset 文件名正确
- artifact 可下载
- publish artifact 中包含 `logs/facet-console.log` 与 `logs/facet-structured.jsonl`

---

## 版本命名规则

当前只接受带 `v` 前缀的语义化版本：

- `v0.1.0`
- `v0.1.1`
- `v0.2.0-alpha.1`

不接受：

- `latest`
- `release-test`
- `0.1.0`

---

## 重发与覆盖策略

当前发布脚本使用以下策略：

- 如果同名 Release 不存在，则创建新 Release
- 如果同名 Release 已存在，则更新标题 / 说明，并用 `--clobber` 覆盖同名 asset

这意味着：

- 同一个 tag 对应的 Release 可以通过 workflow rerun 或再次执行发布步骤来覆盖 asset
- 不需要手动先删 asset 才能重传同名 zip

---

## 回滚方式

### 情况 1：tag 还不该发布

如果版本点本身错误，应删除 tag 与 Release：

```powershell
git tag -d v0.1.0
git push origin :refs/tags/v0.1.0
```

随后在 GitHub 上删除对应 Release。

### 情况 2：发布内容有问题，但版本号应保留

如果版本号仍有效，但产物有问题：

- 修复发布脚本或发布环境问题
- 重新触发对应 workflow，覆盖同名 asset

如果修复需要基于新的提交，则更稳妥的做法是发布新版本 tag，而不是强行重写旧 tag 指向。

---

## 权限与凭据

当前发布流程使用：

- GitHub Actions 默认 `GITHUB_TOKEN`
- `contents: write` 权限

当前不需要额外配置：

- 自定义 PAT
- Steam 凭据
- 外部存储服务密钥

---

## 排障入口

发布失败时建议按下面顺序排查：

1. 看 workflow summary，确认失败发生在 `prepare`、`export/package` 还是 `publish release`
2. 下载 publish artifact，优先查看 `logs/facet-console.log` 与 `logs/facet-structured.jsonl`
3. 检查 `export_presets.cfg` 是否仍包含：
   - `Windows Desktop`
   - `binary_format/architecture="x86_64"`
   - `include_filter="scripts/facet/LuaScripts/*.lua"`
4. 检查工具链脚本是否仍固定到 `4.6.1 stable mono`
5. 检查 Release job 是否有 `contents: write`

常见问题：

- 预设名写错：会直接导致 Godot 无法识别 `Windows Desktop`
- templates 版本不匹配：会导致导出失败或结果异常
- smoke test 找不到 `runtime` / `client.idle` / Lua 控制器证据：说明导出包可以生成，但运行链路没有真正打通
- 包内缺少 `Sideline.dll` / `Sideline.runtimeconfig.json`：说明导出或打包校验未通过
- 本地导出时 Godot 编辑器占用用户配置：建议使用隔离 `APPDATA` 验证

---

## 本地验证建议

如果要在本机验证更接近 CI 的发布场景，建议：

1. 准备隔离的 `APPDATA` 根目录
2. 将正确版本的 export templates 放入 `APPDATA\Godot\export_templates\4.6.1.stable.mono`
3. 再执行本地发布脚本

这样可以避免本机已打开的 Godot 编辑器与发布脚本共享配置目录。
