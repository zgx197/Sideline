# Release Tools

本目录用于存放 Sideline 发布流程的脚本、模板和辅助配置。

当前只建立目录边界，不直接放入正式实现。后续发布功能建议按职责拆分为：

- `shared/`：公共逻辑
- `windows-x64/`：Windows x64 导出与打包
- `github-release/`：GitHub Release 相关逻辑

构建产物不进入本目录，也不入库，后续统一输出到被 `.gitignore` 忽略的本地目录，例如 `artifacts/release/`。
