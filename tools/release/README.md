# Release Tools

本目录用于存放 Sideline 当前正式发布链路的脚本、模板和辅助配置。

当前已经落地的职责拆分如下：

- `shared/`：版本元数据、Godot 工具链配置、工具链安装
- `windows-x64/`：Windows x64 导出、前置检查、启动 smoke test、打包、本地一键入口
- `github-release/`：Release 说明生成、GitHub Release 发布

对应 workflow：

- `.github/workflows/godot-release-artifact.yml`
- `.github/workflows/godot-windows-release.yml`

当前发布链路的固定顺序为：

- `prepare`
- `install toolchain`
- `export`
- `smoke test`
- `package`
- `upload artifact / publish release`

构建产物不进入本目录，也不入库。运行时输出统一进入被 `.gitignore` 忽略的本地目录，例如：

- `artifacts/toolchains/`
- `artifacts/release/`
