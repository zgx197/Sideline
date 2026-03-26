# Lattice ECS 技术债清单（按优先级）

## 文档目的

本文档用于把当前 Lattice ECS 底座在 `D0 - D8` 文档收敛完成之后，仍然需要继续处理的技术债按优先级拆分出来。

这里的“技术债”特指三类问题：

- 当前主线已经可用，但实现硬化还不够
- 现有边界已经明确，但旧草案、旧抽象或旧路径还没有完成收口
- 某些能力虽然不是底座 bug，但如果不先处理，会显著放大后续运行时接入成本

本文档**不**把所有未来能力都算成技术债。

例如下面这些更接近后续功能建设，而不是当前技术债本身：

- 完整联机
- 完整 replay 工具链
- 并行 system runtime
- 完整 Godot 运行时 UI

---

## 阅读方式

当前技术债按四个优先级分组：

- `P0`：必须优先处理，否则会阻碍上层运行时安全接入
- `P1`：建议在开始大规模 runtime 接入前处理
- `P2`：可以与上层实现并行推进，但需要持续跟踪
- `P3`：中长期优化项，当前不应抢占主线资源

每条技术债都尽量回答五个问题：

- 债务是什么
- 影响哪些模块
- 为什么现在是债，而不是单纯未来功能
- 如果不处理会造成什么后果
- 什么时候可以认为它已经完成

---

## 当前总体判断

结合 [FoundationDocumentationRoadmap.md](/d:/UGit/Sideline/godot/scripts/lattice/ECS/FoundationDocumentationRoadmap.md) 和当前实现状态，Lattice 现在已经具备：

- 稳定的底座主链路
- 明确的模块边界
- 最小可运行的确定性闭环
- 明确的上层运行时分层

因此当前技术债的核心不再是“底座方向是否正确”，而是：

- 如何把当前第一版正式主线硬化到可持续承载运行时的程度
- 如何收口旧时代抽象，避免后续实现再次分叉
- 如何降低上层 Bridge / Runner / View / Debug 的接入风险

---

## P0：必须优先处理的技术债

## 1. `Frame / Storage` 销毁与恢复路径硬化

**涉及模块**

- [Frame.cs](/d:/UGit/Sideline/godot/scripts/lattice/ECS/Core/Frame.cs)
- [Storage.cs](/d:/UGit/Sideline/godot/scripts/lattice/ECS/Core/Storage.cs)
- [FrameStorageDesign.md](/d:/UGit/Sideline/godot/scripts/lattice/ECS/FrameStorageDesign.md)

**债务描述**

当前 `Frame / Storage` 主体方向是对的，但它仍然是整个底座里最关键、同时也最需要工程硬化的一层。

尤其是下面三条路径仍然属于高风险区：

- 实体销毁后 mask、版本、free list、storage 实例的一致性
- 高频 `CreateSnapshot / RestoreFromSnapshot / Clone` 的长期稳定性
- 存储恢复后内部版本、稀疏索引和 packed block 一致性

**为什么这是技术债**

因为：

- 这些能力已经进入正式主线
- 上层 `SimulationSession` 和未来 rollback 都建立在它们之上
- 但当前还没有被证明“足以长期高频稳定运行”

**如果不处理**

- 后续 session、runner、rollback 一旦负载变高，会把底层一致性问题放大
- 问题会表现成非常难排查的“偶发不同步”或“restore 后状态脏污”

**完成标准**

- 增补销毁/恢复/clone 的高频压力测试
- 明确 destroy 后 storage 清理的严格一致性
- 明确 restore 后所有关键索引与版本状态都可验证一致

---

## 2. `CommandBuffer` 复杂路径与错误路径硬化

**涉及模块**

- [CommandBuffer.cs](/d:/UGit/Sideline/godot/scripts/lattice/ECS/Framework/CommandBuffer.cs)
- [CommandBufferDesign.md](/d:/UGit/Sideline/godot/scripts/lattice/ECS/CommandBufferDesign.md)

**债务描述**

当前命令缓冲的主线语义已经清晰，但更复杂的组合路径还缺少足够硬化，例如：

- 更长命令流
- 多临时实体交错创建与操作
- 序列化 / 反序列化后的复杂回放
- 非法命令流的诊断质量

**为什么这是技术债**

因为 command buffer 已经不再是试验性 API，而是：

- 当前结构修改的正式落点

如果这层不够硬，上层系统和 session 再稳定都没有意义。

**如果不处理**

- 系统一多、结构修改一复杂，问题会集中爆在命令回放阶段
- 后续 debug tool 也很难解释结构修改到底发生了什么

**完成标准**

- 增加复杂命令流测试矩阵
- 补足非法输入与越界临时实体场景测试
- 明确更好的 command buffer 观测信息或统计接口

---

## 3. `SimulationSession` 历史模型缺位

**涉及模块**

- [SimulationSession.cs](/d:/UGit/Sideline/godot/scripts/lattice/ECS/Session/SimulationSession.cs)
- [SimulationSessionDesign.md](/d:/UGit/Sideline/godot/scripts/lattice/ECS/SimulationSessionDesign.md)
- [DeterministicLoopDesign.md](/d:/UGit/Sideline/godot/scripts/lattice/ECS/DeterministicLoopDesign.md)

**债务描述**

当前 session 已具备最小 verified / predicted 闭环，但仍缺少真正可供 rollback 接入的历史模型：

- 没有正式快照历史窗口
- 没有 verified 输入回填后的 rollback 起点选择
- 没有正式的 resim 窗口管理

**为什么这是技术债**

因为从架构上看，`SimulationSession` 已经是底座顶层。

如果它长期停留在“只有当前帧和预测帧”的状态，那么：

- Runner 能接，但只能做最轻量本地驱动
- 联机和 replay 迟早要再回来大改 session

这就不是单纯的未来功能，而是明显的“最小闭环之后必须补的主链路债”。

**如果不处理**

- 后续网络与 replay 接入只能绕着 session 做自己的历史管理
- 架构会重新分叉

**完成标准**

- 为 session 定义最小快照历史与淘汰策略
- 明确 verified 回填后的 rollback / resim 流程
- 不要求一次做到完整联机，但至少要把历史模型补成正式能力

---

## 4. 旧运行时抽象收口

**涉及模块**

- [IEngineAdapter.cs](/d:/UGit/Sideline/godot/scripts/lattice/ECS/Abstractions/IEngineAdapter.cs)
- [ENGINE_ADAPTER_DESIGN.md](/d:/UGit/Sideline/godot/scripts/lattice/ECS/Abstractions/ENGINE_ADAPTER_DESIGN.md)
- [RuntimeLayerDesign.md](/d:/UGit/Sideline/godot/scripts/lattice/ECS/RuntimeLayerDesign.md)

**债务描述**

当前已经有新的 D8 文档口径，但旧的 engine adapter 抽象仍保留在仓库里。

它们有参考价值，但如果不明确收口，后续很容易出现：

- 一部分实现按旧 adapter 走
- 另一部分实现按新 runtime layer 走

**为什么这是技术债**

因为这会直接制造双主线，而不是单纯的文档问题。

**如果不处理**

- Bridge / Runner 实现阶段很容易出现 API 分叉
- 评审时也会不断争论“到底以哪个版本为准”

**完成标准**

- 明确旧文件是 `legacy/reference` 还是继续保留但降级
- 新 runtime 实现统一以 D8 文档为准
- 避免新代码继续直接扩张旧 `IEngineAdapter`

---

## P1：建议在大规模 runtime 接入前处理的技术债

## 5. `SystemScheduler` 真实负载验证不足

**涉及模块**

- [SystemScheduler.cs](/d:/UGit/Sideline/godot/scripts/lattice/ECS/Framework/SystemScheduler.cs)
- [SystemDesign.md](/d:/UGit/Sideline/godot/scripts/lattice/ECS/SystemDesign.md)

**债务描述**

当前调度器的生命周期、启停、排序和 trace 都已经进入正式主线，但验证规模仍然偏“小而精”：

- 系统数量不多
- 分组深度不深
- 与真正 runner 的联动还没形成

**为什么这是技术债**

因为它不是设计缺失，而是“主线可用但负载覆盖不足”。

**如果不处理**

- 后续一旦挂上更多系统、更多组和更多运行时观察器，调度器的边角行为才会暴露

**完成标准**

- 引入更贴近真实项目规模的 system 组合测试
- 验证更复杂的组级启停、动态添加、trace 消费场景

---

## 6. `Query` 热点路径收益尚未和真实业务绑定

**涉及模块**

- [QueryDesign.md](/d:/UGit/Sideline/godot/scripts/lattice/ECS/QueryDesign.md)
- `Filter<T...>()`
- `ComponentBlockIterator<T>`

**债务描述**

当前 Query 分层设计是合理的，但“哪些系统应该停留在 Filter，哪些值得下沉到 block iterator，哪些值得 owning group”仍然主要停留在框架层判断。

**为什么这是技术债**

因为 Query 现在已经足够正式，会被真实系统依赖。

如果没有业务绑定下的 benchmark 和规则，后续团队会在使用方式上重新分叉。

**如果不处理**

- 普通系统可能被过早推向低层 API
- 热点系统也可能因为缺少标准而一直停留在默认路径

**完成标准**

- 基于 1 到 3 个真实系统场景形成 Query 使用基线
- 明确哪些系统保留 Filter，哪些允许显式热点路径

---

## 7. snapshot / checksum 仍缺“长期运行型”验证

**涉及模块**

- [Frame.cs](/d:/UGit/Sideline/godot/scripts/lattice/ECS/Core/Frame.cs)
- [DeterministicLoopDesign.md](/d:/UGit/Sideline/godot/scripts/lattice/ECS/DeterministicLoopDesign.md)

**债务描述**

当前 snapshot / checksum 的正确性已有单测证明，但长期、高频、多轮 restore 下的稳定性还缺少更系统的验证。

**为什么这是技术债**

因为 checksum 已经被放到了确定性循环的正式位置上。

**如果不处理**

- 后续 replay / rollback 接入时，问题会表现成间歇性 hash 不一致，排查成本极高

**完成标准**

- 增加长轮次 clone / restore / checksum repeatability 测试
- 覆盖更多实体生命周期与结构修改场景

---

## P2：可以并行推进，但要持续跟踪的技术债

## 8. 调试观察面还不完整

**涉及模块**

- `SystemScheduler.Trace`
- `SystemNodeInfo`
- `SimulationSession`
- `CommandBuffer`

**债务描述**

当前底座已经暴露了一部分观察面，但仍然缺少一套相对完整、稳定、可供 D8 调试工具直接消费的数据面。

例如当前还缺：

- 更明确的 command buffer 统计
- session 级运行摘要
- snapshot / checksum 变化追踪视图

**为什么这是技术债**

因为 D8 已经允许做 debug observer 了，但底层公开观察面还没有收敛完整。

**如果不处理**

- 后续调试工具会倾向于直接穿透内部实现细节

**完成标准**

- 形成一组稳定 runtime observation API 或快照结构

---

## 9. 文档链虽然完整，但“实现跟随更新”机制还未制度化

**涉及模块**

- 全部 `ECS/*.md`

**债务描述**

现在文档链是完整的，这是优势；但它还没有形成明确机制来保证后续实现变更时同步更新文档。

**为什么这是技术债**

因为一旦后续 runner、bridge、debug tool 开始推进，文档极容易再次落后。

**如果不处理**

- 1 到 2 个迭代后，文档和实现又会重新分离

**完成标准**

- 对核心底座变更建立“文档同步更新”约束
- 至少把设计变更纳入 PR 检查清单

---

## P3：中长期优化项

## 10. 并行 system 与高级调度仍只是预留

**涉及模块**

- `SystemExecutionKind.ParallelReserved`
- `JobSystem`
- `SystemScheduler`

**债务描述**

当前并行能力已经被识别，但仍然只停留在预留边界，而不是正式主线。

**为什么这是 P3**

因为当前它不是主链路阻塞项。

在 runner、bridge、view、debug 这些更靠前的运行时接入还没完成前，不应优先投入。

**完成标准**

- 等主线 runtime 跑通后，再决定是否需要真正的并行调度模型

---

## 11. 高级 Query 布局优化仍应后置

**涉及模块**

- owning group
- zipped fast path
- 热 bundle 布局

**债务描述**

这些方向已经被识别，但目前仍属于热点优化预留，而不是当前主线阻塞项。

**为什么这是 P3**

因为当前最缺的不是再压榨最后一点性能，而是先把底座主线和 runtime 接入打稳。

---

## 当前不应误判成技术债的事项

为了避免排期失真，下面这些事项当前不应直接归类为“技术债必须先还”：

- 完整 Godot bridge 实现
- 本地 runner 完整功能化
- replay 文件格式
- lockstep 网络协议
- 调试 UI 面板
- 多引擎统一适配

这些更准确地说是：

- 未来运行时实现工作

它们可以建立在当前技术债清单之上排期，但不应和 P0 / P1 的底座硬化债混为一谈。

---

## 当前建议的处理顺序

如果从工程管理角度只选最值得先处理的 5 项，我建议按下面顺序：

1. `Frame / Storage` 销毁与恢复路径硬化
2. `CommandBuffer` 复杂路径与错误路径硬化
3. `SimulationSession` 历史模型补齐
4. 旧运行时抽象收口
5. `SystemScheduler` 与 `Query` 的真实负载验证

这个顺序的原因是：

- 前三项直接关系到底座主链路是否能长期承载 runtime
- 第四项直接关系到后续实现会不会重新分叉
- 第五项决定上层接入时会不会出现“主线可用但规模一上来就变脆”的问题

---

## 最终结论

当前 Lattice 的技术债结构可以概括为：

**底座主链路的方向已经稳定，当前最需要优先偿还的不是“重新设计底座”，而是“把 `Frame / CommandBuffer / SimulationSession` 这条主链做工程硬化，并尽快收口旧运行时抽象，避免上层 Bridge / Runner / View / Debug 接入时再次分叉”。**

这份清单将作为后续工程排期、任务拆分和提交节奏的优先级依据。
