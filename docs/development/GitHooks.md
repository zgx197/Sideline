# Git Hooks 配置指南

本文档说明如何配置本地 Git Hooks，在提交前自动检查代码格式。

## 快速开始

### 1. 配置 Hooks 路径（只需执行一次）

```bash
git config core.hooksPath .githooks
```

### 2. 验证配置

```bash
git config core.hooksPath
# 应该输出: .githooks
```

## 功能说明

配置完成后，每次执行 `git commit` 时会自动：

1. 运行 `dotnet format --verify-no-changes` 检查代码格式
2. 如果格式有问题，提交会被阻止，并提示修复命令
3. 修复后重新 `git add` 和 `git commit` 即可

## 常见问题

### 提交被阻止怎么办？

如果看到错误提示：

```
❌ 代码格式检查失败！

请运行以下命令修复格式问题：
  cd godot/scripts/lattice
  dotnet format

然后重新添加文件并提交：
  git add .
  git commit -m '你的提交信息'
```

按提示执行即可。

### 如何临时跳过检查？

紧急情况下可以使用 `--no-verify`：

```bash
git commit -m "hotfix: xxx" --no-verify
```

> ⚠️ 不推荐常规使用，这会导致 CI 检查失败。

### Windows 行尾问题

如果遇到行尾（CRLF/LF）相关的格式错误，确保本地 Git 配置：

```bash
# 在项目根目录执行
 git config --local core.autocrlf false
git config --local core.eol lf
```

项目已配置 `.gitattributes` 自动处理行尾，通常无需额外操作。

## 技术细节

- **Hook 位置**: `.githooks/pre-commit`
- **检查范围**: `godot/scripts/lattice` 目录下的所有 C# 代码
- **规则来源**: `.editorconfig` 文件

## 禁用 Hooks

如果需要完全禁用：

```bash
git config --unset core.hooksPath
```
