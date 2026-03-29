# Shared Release Helpers

本目录存放平台无关、渠道无关的发布公共逻辑。

当前已落地脚本：

- `Resolve-ReleaseMetadata.ps1`
  负责版本号校验、包名生成、artifact 命名、Release 标题生成
- `Resolve-GodotToolchainConfig.ps1`
  负责 Godot 下载地址、工具链目录、模板目录与版本约束
- `Install-GodotToolchain.ps1`
  负责在本地或 CI 中下载、解压并校验固定版本 Godot 工具链与 export templates

这些脚本不直接执行平台导出，也不直接发布 GitHub Release。
