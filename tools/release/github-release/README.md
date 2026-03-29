# GitHub Release Helpers

本目录存放 GitHub Release 渠道层脚本。

当前已落地脚本：

- `New-GitHubReleaseNotes.ps1`
  生成 Release 标题配套说明文本
- `Publish-GitHubRelease.ps1`
  负责创建 / 更新 GitHub Release，并上传 Windows x64 zip asset

它只负责 GitHub Release 渠道层，不负责 Godot 导出和打包。
