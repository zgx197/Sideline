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

## 当前仓库现状

基于当前仓库状态，先记录两个已经确认的事实：

- 仓库中目前**没有**可用的 `export_presets.cfg`，因此还不具备直接执行 `Windows Desktop` 导出的前置条件。
- 仓库中目前**没有**现成的 Godot 发布脚本，`tools/release/` 只有目录骨架和职责预留。

这意味着当前阶段不能直接进入“写完整 release workflow”，而应该先把导出预设、导出环境和打包约束补齐。

---

## 实现计划

下面这套计划按“先补前置，再落地实现”的顺序推进。它的目标不是一次性把所有发布能力写完，而是确保每一步完成后都能验证结果。

### 阶段 1：补齐 Windows x64 导出前置

目标：

- 让仓库具备最小可用的 Godot Windows x64 导出条件。

任务：

1. 在 `godot/` 目录下建立并提交 `export_presets.cfg`。
2. 新增一个明确面向 `Windows Desktop` 的导出预设，约束为 `x86_64`。
3. 约定第一版只导出 release 包，不同时兼容多平台或多配置。
4. 本地验证导出预设可被 Godot 识别。

完成标准：

- 仓库中存在可追踪的 `godot/export_presets.cfg`。
- 文档中明确写出第一版只支持 `Windows x64`。
- 本地可用 Godot 编辑器读取该预设。

### 阶段 2：确定 CI 中的 Godot 导出环境方案

目标：

- 选定 GitHub Actions 中准备 Godot 导出环境的唯一方案，避免后续 workflow 同时维护多种路径。

当前可选方案建议只保留三类进行比较：

1. 在 workflow 中下载并解压指定版本的 Godot Mono / .NET 发行包与 export templates。
2. 使用预先准备好的自托管 runner，本机已安装 Godot Mono 与导出模板。
3. 在仓库或缓存中维护固定版本的 Godot 工具链，再由 workflow 复用。

当前推荐优先评估方案 1，原因是：

- 最容易做到版本显式、流程可复现；
- 不依赖单台固定机器；
- 比仓库内直接提交大体积二进制更容易维护。

但在真正决定前，需要确认两件事：

- 你们后续是否接受 workflow 在发布时下载 Godot 工具链；
- Godot Mono / .NET 导出在 GitHub Hosted Windows Runner 上的可行性和稳定性。

完成标准：

- 文档中明确选定一种环境准备方案。
- 对应方案的目录入口和脚本位置已经约定好。
- 不再同时保留多套互斥方案。

### 阶段 3：实现本地发布脚本

目标：

- 先在本地或手动命令层面跑通 `Windows x64 -> 打包 zip`，再把同样逻辑搬进 GitHub Actions。

建议放置位置：

- `tools/release/windows-x64/`
- `tools/release/shared/`

任务：

1. 编写版本号解析与命名脚本。
2. 编写 Godot Windows x64 导出脚本。
3. 编写导出结果完整性检查脚本。
4. 编写 zip 打包脚本，输出 `Sideline-vX.Y.Z-win-x64.zip`。
5. 约定本地产物输出目录，例如 `artifacts/release/windows-x64/`。

完成标准：

- 不依赖 GitHub Actions，也能在本地手动执行完整导出与打包。
- 最终能产出一个命名规范的 zip 文件。
- 导出失败、缺文件、命名错误时能清晰报错。

### 阶段 4：实现 Tag 触发的 GitHub Actions Workflow

目标：

- 在本地脚本稳定后，再把流程迁移到 GitHub Actions。

任务：

1. 新增独立的 release workflow，不接入当前 CI 验证链路。
2. 使用 `push tags: [ 'v*' ]` 作为触发条件。
3. 先执行 `prepare`，再执行 `export-win-x64`、`package`、`release`。
4. 先上传 artifact，再创建或更新 GitHub Release。
5. 只上传一个正式 asset：`Sideline-vX.Y.Z-win-x64.zip`。

完成标准：

- 推送 `v*` tag 后，workflow 能被稳定触发。
- Release 页面可下载 Windows x64 zip。
- 发布失败时仍能从 artifact 获取中间产物。

### 阶段 5：补齐发布验证与维护文档

目标：

- 不让 release workflow 成为只能“看代码猜行为”的黑箱。

任务：

1. 补一份发布操作说明，说明如何打 tag、如何回滚、如何重发。
2. 补一份导出产物检查清单，至少覆盖启动、窗口模式、关键资源和运行时文件。
3. 记录 release workflow 使用到的环境变量、密钥和 GitHub 权限。

完成标准：

- 新人只看文档也能理解发布入口和产物结构。
- 发布异常时有明确排障入口。

---

## 当前建议的推进顺序

结合当前仓库现状，下一步建议按下面顺序推进：

1. 先创建 `godot/export_presets.cfg`，补齐 Windows Desktop 导出预设。
2. 明确 GitHub Actions 中 Godot 导出环境的准备方案。
3. 先写本地导出与打包脚本，再迁移到 workflow。
4. 最后再落 GitHub Release 自动发布。

这个顺序的核心原因是：

- 现在最大的阻塞点不是 GitHub Release，而是“仓库还不能稳定导出 Windows x64 包”；
- 如果导出预设和导出环境没有先定下来，后面的 workflow 只是把不确定性搬进 CI。

---

## 当前结论

当前已经明确的结论是：

- Sideline 第一版正式发布链路以 `tag -> Windows x64 -> artifact -> GitHub Release asset` 为主线。
- 现有 CI 验证流程不承担发布职责。
- 这套方案先落目录与文档，再进入 workflow 实现讨论。

下一步如果继续推进，应该优先讨论：

1. `export_presets.cfg` 的组织方式
2. GitHub Actions 中 Godot 导出环境的准备方式
3. Windows x64 本地导出与打包脚本的组织方式
