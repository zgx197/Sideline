# Godot 发布方案草案

> 本文档定义 Sideline 第一版正式发布流程的目标范围、职责拆分和目录约定。当前只讨论 **Windows x64 -> GitHub Release**，不包含 Steam 分发，不直接落地 workflow 实现。

---

## 目标范围

本轮发布方案只覆盖以下内容：

- 平台：`Windows x64`
- 触发条件：`push tag`
- 版本规范：`v*` 形式的版本标签，例如 `v0.1.0`
- 发布目标：`GitHub Release`
- 产物形式：一个可下载的标准 zip 包

当前明确不做：

- Steam 发布
- 多平台导出矩阵
- 自动上传到 itch、Steam 或其他渠道
- 未经讨论直接落地 Godot release workflow

---

## 流程总览

第一版推荐的发布链路如下：

1. 维护者创建并推送版本标签，例如 `v0.1.0`。
2. GitHub Actions 被 tag 触发。
3. 发布流程校验 tag 格式、版本信息和导出前置条件。
4. 执行 Godot `Windows x64` 导出。
5. 整理导出目录，补齐需要保留的运行时文件。
6. 生成标准发布包 `Sideline-v0.1.0-win-x64.zip`。
7. 将 zip 先上传为 workflow artifact。
8. 创建或更新 GitHub Release。
9. 将同一个 zip 作为 Release asset 挂到对应版本页面。

这条链路的核心原则是：

- `artifact` 用于流程内部保存和排查；
- `Release asset` 用于外部下载；
- 发布流程独立于现有验证流程；
- tag 是唯一正式发布入口。

---

## 为什么用 Tag 触发

当前推荐使用 tag 触发，而不是 `push main` 或手动按钮，原因有三点：

- tag 天然表示“这是一个版本点”，比普通提交更适合作为发布入口；
- 能把“开发主线”和“正式版本”清楚地区分开；
- 以后接 GitHub Release、Steam 或 changelog 体系时，tag 仍然是最稳定的版本锚点。

推荐格式：

- `v0.1.0`
- `v0.1.1`
- `v0.2.0-alpha.1`

不推荐：

- `release-test`
- `latest`
- 没有语义版本含义的临时标签

---

## 发布产物定义

### 1. Workflow Artifact

artifact 是 GitHub Actions 在流程运行期间保存的构建产物，用于：

- 在 job 之间传递文件；
- 在发布失败时下载排查；
- 在正式挂 Release 之前保留中间结果；
- 后续人工验证构建内容。

当前方案里，artifact 至少包含：

- 未压缩的 Windows x64 导出目录
- 压缩后的发布包 `Sideline-v0.1.0-win-x64.zip`

### 2. GitHub Release Asset

Release asset 是正式挂在 GitHub Release 页面上的下载文件。

第一版只需要一个主要 asset：

- `Sideline-v0.1.0-win-x64.zip`

也就是说：

- artifact 面向流程内部；
- release asset 面向最终用户。

---

## 建议的工作流分层

虽然这一轮不直接写 workflow，但后续实现时建议按下面的职责拆分 job：

### 1. `prepare`

职责：

- 校验 tag 是否符合 `v*` 规范
- 解析版本号
- 生成统一文件名和输出路径
- 将版本信息写入 job output

输出示例：

- `version = v0.1.0`
- `package_name = Sideline-v0.1.0-win-x64.zip`

### 2. `export-win-x64`

职责：

- 安装或准备 Godot 导出环境
- 校验 `Windows Desktop` 导出预设是否存在
- 执行 Godot Windows x64 导出
- 检查导出目录结构是否完整

### 3. `package`

职责：

- 整理发布目录
- 压缩导出结果
- 生成 zip 包
- 上传 artifact

### 4. `release`

职责：

- 创建或更新 GitHub Release
- 上传 zip 作为 Release asset

这个拆分方式能保证：

- 导出失败时不会污染 release 步骤；
- 打包失败时仍可保留导出目录 artifact；
- 以后接多平台时可以扩展为矩阵，而不用重写整条链路。

---

## 目录约定

为了后续实现发布脚本和元数据，仓库中新增如下目录：

- `tools/release/shared/`
- `tools/release/windows-x64/`
- `tools/release/github-release/`

职责建议如下：

- `shared/`：放所有平台共用的版本解析、命名、校验逻辑
- `windows-x64/`：放 Godot Windows x64 导出与打包相关脚本
- `github-release/`：放 Release 创建、说明模板、asset 上传相关逻辑

构建输出不入库，后续统一落到被忽略的本地目录，例如：

- `artifacts/release/`

这和仓库里的受版本控制目录是两件事：

- `tools/release/` 存脚本与配置
- `artifacts/release/` 存运行时产物

---

## 推荐命名规范

### 标签

- `v0.1.0`

### Artifact

- `sideline-win-x64-export`
- `sideline-win-x64-package`

### Release Asset

- `Sideline-v0.1.0-win-x64.zip`

### Release 标题

- `Sideline v0.1.0`

---

## 实现前置条件

在真正写 workflow 之前，需要先确认以下几项：

1. Godot Mono / .NET 导出环境的准备方式
2. 当前仓库是否已经具备稳定的 `export_presets.cfg`
3. Windows x64 导出后所需的运行时文件清单
4. GitHub Release 是自动创建还是只在 tag 对应 release 已存在时更新
5. 发布说明是自动生成、模板生成，还是人工维护

这些问题没有定清之前，不建议直接写最终发布 workflow。

---

## 当前结论

当前已经明确的结论是：

- Sideline 第一版正式发布链路以 `tag -> Windows x64 -> artifact -> GitHub Release asset` 为主线。
- 现有 CI 验证流程不承担发布职责。
- 这套方案先落目录与文档，再进入 workflow 实现讨论。

下一步如果继续推进，应该优先讨论：

1. `export_presets.cfg` 的组织方式
2. Windows x64 导出命令与依赖准备
3. GitHub Release 的创建策略
