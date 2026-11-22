# RRPG 日内时间基础与远征实际到达 — A Development Spec V0.2

日期：2026-10-05。状态：FDS Review PASS / A Implementation Authorized / Target / Not Implemented。

V0.2 已由制作人复核通过，授权按 §7 最小范围实施、§9 验收及 §10 Authority 同步。验证通过前不得登记 Current / Implemented；不启动 B / C / D / WP-AL-5，不提交、推送或发布。以下审计与修订核验按生成时历史语境保留；“未授权 / 仓库外”等旧阶段记录不撤销当前授权。本规格承担开发期 Binding，玩家规则仍按 GameSpec 分工归位。
<a id="baseline"></a>
## 1. 现场基线与审查证据

- 当前分支 main；HEAD：`1ee486289214dd0497a0d0b048da1c30592b0be7`，与交接一致。未回退，未创建分支。
- 暂存区为空。原有工作区为 3 个 tracked 修改、14 个 untracked 文件，包含两个旧 Review diff；没有阅读制作人资源的内容 diff。
- 已对 2902 个 Git tracked / 非忽略 untracked 文件建立本轮 SHA256 基线，结束时复核；既有 TMP exception 按本轮开始时的现有字节保护，不声称恢复了制作人原始版本。
- 已依次读取 Bootstrap 入口，再核对 Current Architecture、Expedition / Village / Resource / Save 规则与下述实际实现。引用检索覆盖 Scripts、Tests、Compiler 源码与测试、相关配置导航；配套消费者索引保存完整命中，不把引用存在当作逐条测试通过。
- T0 仅静态审查。没有运行 Compile、Unity Test Runner、Builder、Compiler 或体验测试。WP-AL-4 的 72 / 248 / 1349 / 93 是历史证据，不是本轮验证结果。
- 三份输入来自 Downloads，未复制入仓：Design Checkpoint、Interface Audit、T0 Development Handoff。

以下路径以 `UnityProject/RRPG1.0game/Assets/` 为前缀；行号对应本轮 HEAD。配套索引有完整本地链接。

| 实际入口 / 证据 | 当前契约 | 对 A 阶段的影响 |
|---|---|---|
| Scripts/Village/Runtime/VillageTime.cs:25,40,49,90,109,135 | 驱动秒数、Gregorian 日历，TickDay 先改日期、跨月先 MonthTicked 再 DayTicked | 不能只加小时显示；必须迁移推进入口和存档 |
| Scripts/Village/UI/VillageTimeDriver.cs:20 | IsSimulationActive 才每帧 Update | 远征期间仍暂停实时驱动，不把现实战斗耗时算世界耗时 |
| Scripts/UI/TownUI.cs:156,176,199 | 离开 Town 暂停，返回恢复 | 保留 gating；返程显式补算不是第二套 Clock |
| Scripts/Village/UI/VillageHUD.cs:41,57,152,165 | Inspector secondsPerDay=5；HUD Start 才写 Time 参数 | Restore 不可依赖 HUD 已 Start；配置初始化顺序要提前 |
| Scripts/Adventure/Progression/Runtime/ExpeditionNodeData.cs:26 | NodeElapsedDays 是整数 | 新的原生 DurationMinutes 与旧天数适配必须区分 |
| Scripts/Adventure/Progression/Runtime/ExpeditionProgressionResolver.cs:112,199–213 | Enter 预估成本；Resolve 合并 Event 天数后一次结算时间 / Supply / Resolved；失败 outcome 也结算 | 共享成本预览，不能独立改 UI 或 Event 公式 |
| Scripts/Adventure/Progression/Runtime/ExpeditionSupplyState.cs:79 | int Carry、PartySize × 天 × 日率 + ExtraCost | 子日内累计成本需要整数库存取整语义；现有乘法也需 checked long 防溢出 |
| Scripts/Adventure/Progression/LegacyMapGraphAdapter.cs:30；LegacyNodeElapsedDaysTestPolicy.cs:20 | 兼容图源；Battle/Elite/Boss=1 测试日，其他=0 | 保留旧测试内容为 1440 / 0 分钟适配，不冻结正式耗时 |
| Scripts/UI/ExpeditionUI.cs:531–545,1434,1471,1488 | 确认出征转移 Carry、绑定 elapsed provider；返回按 Phase 补算后返还 Carry、后果、Clear、Save | 捕获真实出发时刻须前移到确认；发布顺序不变 |
| Scripts/Village/VillageRuntimeBridge.cs:386,417,467,530 | BeginReturn 才捕获 Departure 日期；DaysSimulated 按完整 TickDay 增加 | 当前 Departure 字段并非真实出征时间；部分失败不能安全重播 |
| Scripts/UI/ExpeditionJourneyCoordinator.cs:282；ExpeditionJourneyView.cs:257 | HUD 只显示整数已过天数 | 改为只读累计时长 / 预计到达；UI 不算耗时 |
| Scripts/EventHost/ExpeditionEventFlow.cs:407–427；Adventure/ExpeditionEvent/ActiveExpeditionEventState.cs:49 | AdvanceExpeditionTime.IntValue 仍是天；PendingExtraElapsedDays 在 Resolve 合并 | 兼容输入 ×1440，不能把旧 IntValue 默读成分钟 |
| Scripts/Village/VillageRuntimeBridge.cs:719–783 | State→Projects→Assignments→Resources→Formation→Feedback Barrier→Window→Selection | A 保留每日唯一链；B/C 迁移后逐域停用旧消费者 |
| Scripts/Save/SaveSystem.cs:107–160；Village/Runtime/VillageSaveData.cs:67 | SaveVersion=1；Decision/Feedback preflight 后先恢复 Village/Event，再 Player、Social/Activity 等 | A 新时间 preflight 要发生在任何 World Restore 前；不能宣称旧全世界事务债已解决 |

## 2. 已确认产品边界与阶段目标

已确认：按实际到达时刻返村；VillageTime 唯一世界时间 Authority；PersonId 长期身份；节点世界耗时与现实动画 / Battle 操作秒数无关；正常、加速、跳过、返程使用相同模拟推进。午餐按当时在村人员、完整需求、不按半天折算，属于 B 阶段，不在 A 偷接。

A 交付目标：08:00 出发，已发生节点累计 30 小时，次日 14:00 到达；经过的跨日与已接入日内边界有序处理；同一节点和返程重试不重复成本或模拟。A 不声称工作已经按小时推进，也不声称 Activity 已持续执行。

A 暂时保留 Assignment / Project / Resource / Formation / WP-AL-2/3/4 的既有日级行为。项目完成日仍按原 occupiedAtStartOfDay 阻止；Primary Assignment 与 PersonRest 不阻止自主资格。现有 Event / Recognition 保留并回归。

不足食物的现行行为是列表顺序分配整份，非比例 Equal Share；这是规格 / 实现差异，按最新交接暂保留，既不静默修复也不改称已符合旧规格。WP-AL-1～4 Published Main 的能力和 Prototype / Calibration Not Frozen 不变。

<a id="time-contract"></a>
## 3. 自主收口的时间技术方案

1. 最小粒度选整数世界分钟。`WorldInstant` 为 Gregorian 日期可换算的 checked long 绝对分钟，epoch 为 Year 1 / Month 1 / Day 1 / 00:00；一天 1440 分钟。闰年规则沿用 VillageTime。小时、日期只是派生视图；不选现实 timestamp。
2. 类型放在现有 Village.Runtime 内（无新 asmdef、无新增跨域引用）。ExpeditionProgression.Runtime 只持有 checked long `ElapsedMinutes` / `DurationMinutes`，不引用 VillageTime 或 Village.Runtime；Host 将时长与出发时刻相加。重复使用的日到分钟转换为明确单位适配，不复制 Clock。
3. `VillageTime.CurrentInstant` 是唯一可写时间状态。Year / Month / Day / MinuteOfDay 改为从它读取；必要旧 API 经兼容方法委托，禁止调用方直接写日期。修改公开字段为只读前须完成全仓消费者编译迁移，不以旧测试赋值保留第二份状态。
4. 唯一推进入口拟为 `AdvanceTo(target, sink)`：拒绝倒退 / 非法 / 溢出 / 重入，返回结构化结果。`sink` 是 Host 的最小推进接口，不是脚本语言或另一个 Scheduler。由 VillageTime 选择下一个跨日 / 跨月 / Host 提供的最近必要边界，按时间顺序执行区间与边界，再继续至 target；不每分钟扫描全体人物。
5. `Update(deltaSeconds)` 只把有限、非负驱动秒数与倍率转换为世界分钟请求；保存不足一分钟的驱动余量，不按每帧独立取整丢余量。驱动分区不能改变已跨过的世界边界。Pause 阻止自动 Update；明确的 catch-up / jump 可以绕过用户倍率暂停，但不能绕过推进会话的 Waiting / Failed / AwaitingFinalSave 锁。secondsPerDay 为正且有限，必须在恢复前初始化；不再由 HUD 生命周期决定迁移参数。正在推进时驱动请求的接纳规则统一见 §4.1。
6. `TickDay()` 兼容入口表示显式推进 1440 分钟，由 AdvanceTo 穿过边界，保持调用开始的时分；`JumpToNextMonth()` 推进到下月首日 00:00。这两个新时间含义要在 GameSpec 最小同步，旧消费者不直接改日历。完整整日样例保持旧调用次数。
7. A 的正式生产边界只有已有日 / 月链；12:00 等测试边界用于证明推进器不会跳过边界，不产生正式午餐。B 才接生产 / 午餐，C 才接机会 / 活动执行，D 才接到场互动。
8. 在同一瞬间的顺序明确：完成此前区间；跨月时保留旧 MonthTicked→DayTicked 顺序；执行该时刻原日链；到达终点后才做 Return Carry / Consequence / Clear / Save。A 保持“占用持续到补算完成”的旧保障，包括恰在午夜抵达的情形；不额外开一次当日 Selection。
9. 推进必须有预算（边界次数 / 区间数量），超限返回 Yielded 和精确游标，保留原 target，可续跑；禁止类似旧 1095 次上限后丢失余量或继续假装已到达。

普通类拆分、接口名与预算大小由实现负责人决定，不列产品裁决。程序集现状 Village.Runtime / ExpeditionProgression.Runtime 均有 UnityEngine 引用且 noEngineReferences=false；A 不借机全域纯化。Activity、Selection、Social、Feedback 原有纯域依赖方向不变。

## 4. 推进失败、交互暂停与返程协议

AdvanceResult 至少包含 Completed / Yielded / WaitingForInteraction / InvalidTarget / Reentrant / RetryableBoundaryFailure / PartialBoundaryFailure，LastCompletedInstant、TargetInstant、BoundaryKind、Diagnostic。结果必须区分“时间已到达某边界”与“该边界的消费者完整成功”。

- 在无副作用的预校验处失败：保留最后完整游标，原 target 重试，不重抽结果。
- 旧日链已部分写入后异常：标记推进会话 Failed；禁止自动重放该边界，禁止仅依据时间值跳过剩余消费者。沿用最后成功存档重开的现有恢复边界，不宣称通用回滚或跨进程续跑。
- 需要玩家处理的已出现 Pending Event：在产生它的完整边界停止继续推进，返回 WaitingForInteraction；使用既有 Pending / Choice UI，处理后续同一 target，不重跑已成功边界。不能因为 VillageTime.Speed=Paused 就把整个 catch-up 判定成完成，也不能绕过必须选择的内容。
- A 不实现 Expedition mid-run Save / Resume，也不保存尚未结束的推进请求。即使游标位于完整边界，Yield / Waiting 也不允许普通 Save；唯一保存许可与 FinalSave 特许入口见 §4.1–4.3。相关 UI 自动 Save 一律经过相同许可检查，不能偷偷保存缺失 Return 会话的中间世界。

出发：在确认、配置 / 人物 / 补给预校验全部成功后，从 VillageTime 读取 DepartureInstant，构造唯一会话；与 Carry 转移及 Expedition 占用一起完成可验证的确认流程，失败不能留半转移或半登记。出发前已进入 Prep 的实时 gating 保持，默认立即确认即出发，不引入晨间预约。

累计：节点预估与 Resolve 使用同一单位和成本函数；Node + Event 额外时间一起预校验 overflow / affordability。Success、Defeat 等已发生 outcome 均结算，内容未开始的拒绝不结算。全部验证后再写 Resolved、Elapsed、Supply；重复 Resolve no-op。旧 Event AdvanceExpeditionTime(int days) 保持“天”，Host checked ×1440，不修改 RRPG_Events.xlsx。

返回：计算 `ArrivalTarget = DepartureInstant + SettledElapsedMinutes`，以 ReturnSessionId 提交一次不可变推进命令。命令被接受后，原目标和完整游标只由 VillageTime 的 AdvanceSession 持有；Return Record 保有 SessionIdentity、DepartureInstant、冻结的 SettledElapsedMinutes、原 mode、Return Phase 与已有 Carry / Consequence / FinalSave ack，通过只读投影查询目标 / 游标 / 诊断，不另存可写副本。BeginReturn 不再冒充出发时刻；重复 safe/retreat/defeat 入口只能 Resume 同一命令，不能重新以当前时刻加 elapsed。

顺序维持：Return Settlement 成功 → AdvanceTo(ArrivalTarget) 完整成功 → remaining Carry → Person Consequence → Clear Expedition Occupancy → Final Save 成功 → Completed。between-boundary 续跑只补未完成部分；Final Save false 只重试保存。零耗时目标等于出发时刻，不新造一天。若当前 Clock 超过冻结 target，明确 InvalidReturnTimeline，不倒退也不再多加一次 elapsed。

<a id="advance-ownership"></a>
### 4.1 唯一推进会话、驱动请求、完整游标与 Save 许可

最小 `WorldAdvanceSession` 归 VillageTime 拥有，属于其推进状态，不是第二 Clock。VillageRuntimeBridge 是唯一产品编排入口；Driver、HUD、Return/UI 都只提交或查询，不自行保存剩余时间 / 续跑目标。没有通用任务队列或第二 Scheduler。

| 状态 / 数据 | 唯一可写归属 | 消费者权限 |
|---|---|---|
| CurrentInstant、LastCompletedInstant、已处理日/月边界标记 | VillageTime | Host / UI 只读；日期先进入边界不等于边界完成 |
| AcceptedRequestId、Origin（Realtime / Jump / Direct / Return）、OriginalTarget、推进状态、阻塞 EventInstanceId | VillageTime.AdvanceSession | Return Record / UI 只关联 RequestId；重试传同 ID，不传新 target |
| Accepted driver minutes 与 fractional DriverRemainder | VillageTime | Driver 提供 delta；接纳后完整分钟全部计入不可变 OriginalTarget，余量保留；Driver 无自己的 backlog |
| Return Phase、Carry / Consequence ack、Return mode | 现有 ExpeditionRecord，由 Bridge 写 | 不允许 UI / Clock 重做已 ack 的阶段 |
| Pending Event / Option 执行结果、History | EventRuntimeBridge / VillageEventFlow | AdvanceSession 只保存等待关联 ID，不复制 Pending DTO 或重抽 Outcome |
| SavePermission | VillageTime 的会话状态只读判定，结合 Bridge 验证的 Return Phase | SaveSystem 每次写盘前检查；UI 禁用按钮不是充分保障 |

接受请求仅在无活跃请求时发生；目标合法、配置齐备后才能写 AcceptedRequestId / OriginalTarget。同一 ID + 同一原命令是查询/续跑，同 ID 不同命令是 conflict，其他 origin 在活跃期间返回 Busy、零 mutation。RequestId 用会话内单调序列 / 既有 ReturnSessionId，不用 Guid、wall clock 或新随机源。

`Yielded` 只能发生在**完整区间 / 完整边界后**，保留原目标、余量与完整游标；由同一 Host 的后续帧以 Resume(ID, budget) 继续，绝不重送原 delta 或再加全部 elapsed。`WaitingForInteraction` 保留相同信息以及阻塞实例 ID，只在明确成功处理并结束结果展示后转为可续跑。目标已到但事件未处理时仍 Waiting，不能提前转 Return Completed。

推进会话非 Idle 时，不接纳新的自动 delta：Driver 返回 Busy / Blocked，不更新目标或余量，也不累积等待期间现实秒数。之前**已接纳**的分钟仍全部包含在原目标中，没有丢失；本来未接纳的帧不作为迟后 catch-up 债务。Pause/Speed/Jump/Direct Tick 也不得越过会话锁。测试比较的是相同已接纳请求与倍率，不把暂停期间 wall-clock 时间强行追补。Yield 以预算分帧执行一个已采纳命令，不开另一套 Update 模拟路径。

完整游标只在整段及其全部同刻消费者成功后提交。LastCompletedInstant 不等于“最后某回调已成功”；如果日界内部分失败，CurrentInstant 可已显示该时刻，但游标仍在上个完整点，状态 Failed，禁止跳过/重播/Save。对已到终点的无时间流逝 Event Option，成功效果通过既有 Event transaction 发生，不重跑该点日链。

| 推进 / 返程状态 | 普通 Save（含 UI 自动保存） | Return FinalSave | 允许的推进操作 |
|---|---|---|---|
| Idle，无未完成 Return、无进行中远征 | 允许，须完整时间 / World preflight | 不适用 | 可以接纳新请求 |
| Running / Yielded / Waiting（Realtime、Jump 或 Return） | 拒绝，保留原文件、返回结构化原因 | 拒绝 | Running/Yield 仅 Resume 当前 ID；Waiting 仅处理当前 Event |
| Failed / PartialBoundaryFailure | 拒绝 | 拒绝 | 不自动续跑；只允许明确丢弃当前会话并从最后成功档重新恢复 |
| 原返程 motion 完成，但 Carry/Consequence/Clear 未全部 ack | 拒绝 | 拒绝 | 只补当前 Return Phase |
| AwaitingFinalSave：原 target 已完整完成且 Return=OccupancyCleared | 拒绝 | 允许匹配 ReturnSessionId 的专用保存意图 | 时间锁定，只有 FinalSave retry |
| FinalSave success、Return Completed，随后释放会话 | 允许 | 后续相同 Return 成功 no-op | 按返回后的原用户暂停/倍率与 Town gating 正常推进 |

许可由 Bridge/Time 提供不可绕过的内部入口或校验凭证，SaveSystem.Save / SaveWithVillage / SaveCore 均检查；FinalSave 不是任意调用方可置 true 的 bool。Retry 不扩大许可。NewGame、Load、DeleteSave 等世界替换在活跃推进期间默认拒绝；错误恢复若需要丢弃，必须经显式 Abort-and-Load 路径、成功后清整套会话，不能普通 UI 随意覆盖。退出进程不拦截，但不自动保存中间世界，说明只能恢复上次成功档；不新增跨进程 RunResume。

所有活跃推进状态同时禁止无关的手动世界写入；日链内部消费者及当前阻塞 Event transaction 按会话许可继续。入口区分玩家操作与当前受控编排，不让操作锁反过来阻止 Projects.OnDayTick 等既有内部结算。仅保存入口拒绝不够：例如 CharacterGrowthUI 是先改变人物再调用 Save，必须在改变前检查锁。该接入只补已有 Host / UI 的写前守卫，不重构成长、项目或资源算法，不建设通用事务框架。

### 4.2 FinalSave 失败：世界暂停与操作限制

实际代码证据：`ExpeditionUI.FinalizeReturn` L1548 检查保存 bool；但 `TownUI.OnReturnFromExpedition` L197–199 无论保存成功与否均 SetSimulationActive(true)。`ReturnToTownWithFinalSave` L1623 还会在成功确认前切 UI。当前实现已有“只 retry Save”标记，**尚没有本稿要求的完整冻结保障**，必须在 A 最小接线修复，不能把它写成已实现。

进入 OccupancyCleared 前，推进会话已经持有独占权；motion 完成后保留会话并置 AwaitingFinalSave 锁。FrozenInstant 必须等于原 ArrivalTarget，Driver、TickDay、Jump Month、HUD Resume/Fast 全部不能再推进。不能只用 Speed=Paused 或 IsSimulationActive=false，因为其它显式入口和 UI 恢复速度可绕过它们。

FinalSave false：保持 OccupancyCleared / Completed=false、所有阶段 ack 与最后完整游标，冻结同一已结算世界；**不恢复 Expedition Occupancy、不撤销 Carry/后果、不重新 Settlement**。展示保存失败诊断和既有返程重试入口，可读当前结果；禁止新出征、职责/项目/资源/人物成长等世界写操作、其他 Event 选项、普通保存、Jump、世界替换。各正式写入口由同一 Bridge 会话状态校验；模态遮罩只是表现保障。

最小实现方案是“保存与进入 Town 的显示 / 恢复模拟分离”：先完成专用 FinalSave，再标记 Return Completed，最后清会话、恢复正常 Town 控制与原用户速度。若错误提示需要显示 Town 背景，只做只读展示，不能调用带 Save / sim=true 的 OnReturnFromExpedition。非 Town 路径同样冻结；不能以准备 UI 可见为理由重新选队伍/转移 Carry。

成功释放顺序须一次性可重试：Save success→CompleteExpeditionReturnSave→完成态清理 / 会话释放→Town正常展示与控制。UI 后续显示失败不能重新保存或重跑世界阶段，保留已完成事实并只修显示。玩家原本 Paused 则仍 Paused，不能强制 Normal。失败等待十秒/切面板/重复重试不改变时刻、库存、人物状态、DecisionWorld 或事实数；修复存储后保存相同权威世界，成功仅释放一次。

### 4.3 返程 Pending Event：实际 UI、处理与续跑接线

实际宿主是 `SceneUIHosts/TownUI/TownCanvas/VillageSocialEventUI`（场景查找指南 L68/122；源码类 VillageSocialEventUI），不是 ExpeditionJourneyRoot/EncounterOverlay 的远征节点事件卡。VillageSocialEventUI.Update L85 依赖激活组件轮询，ShowEventPending L168 使用现有配置/模板生成按钮；OnEventOptionClicked L280 经 VillageEventFlow.TryExecutePendingOption，成功清 Pending，失败保留；OnCloseClicked L396 和 OnDisable L413 会恢复旧 Speed。源码与导航证实接缝，但本轮没有启动 Editor 核验可见性 / Raycast。

因此**不能假设 TownCanvas 隐藏时 UI 会自行弹出**。A 最小方案复用这一个现有组件与 PanelRoot，不新造 EventUI：在返程 Waiting 时由产品 Host 主动找到 inactive 宿主、幂等初始化绑定，把整个现有 Event UI 子树临时挂到已激活的 SceneUIHosts UI 宿主下，保留原 parent / sibling / layout；使用现有 PanelRoot 的子 Canvas / 遮罩显示，不激活整套 Town 流程，不调用 Initialize / OnReturnFromExpedition / Save。结果关闭后还原挂载。临时挂载只属运行时表现，不修改 Scene/Prefab，也不复制 Event 状态。若实际场景的 Canvas/绑定无法支持该方案，报告最小宿主兼容问题，不能偷偷建立第二正式窗口或放开所有 Town 操作。

生产接线约定：

1. Bridge 在开始推进前、以及每个完整边界后（包括原目标终点）查询既有 Pending。阻塞条件复用当前 `VillageTestSocialPatterns.ShouldPauseOnOpen`：Unified Event 采用 EventPresentation.Urgency / PauseOnOpen；Legacy Pending 使用现有 pattern 与已绑定 UI 配置。Ignorable 不自动变强制；无配置/无法判断时返回明确 Diagnostic/WaitingForInteraction，不伪装无 Pending，不虚构新选项或忽略规则。
2. 旧日/月链在同一边界先完整执行，再检查等待，不中途切成半条 DayTick；若已有阻塞 Pending，则推进前就等待，不先跳到下一天。后续若产品要求 Option 先于同边界 Selection，需要独立子相位契约，不在本 A 静默改变日链。
3. Waiting 关联 RequestId + ReturnSessionId + EventInstanceId；Host 显式 ShowEventPending 同一 instance，不调用 Resolver、Recognition 或 Outcome reroll。首次UI缺失/无法渲染保持等待与诊断，可修复后重开；不 advance、不 Clear、不 Save、不自动选 Option。Legacy 兼容复用 ShowPending / 原处理桥，不升级旧 Pending 为新随机实例。
4. 点击前验证窗口 instance == 当前 Pending instance == 会话阻塞 instance。随后调用既有 VillageEventFlow.TryExecutePendingOption，效果 / History 仍由 Event 域 transaction 提交；UI 只反馈 success / diagnostic。失败仍 Waiting；关诊断只是关闭展示，可重新打开原 Pending，没有 success ack，不能恢复推进。双击 / stale 按钮不能执行新 Pending 或另一次 Outcome。
5. 成功后 Pending 按既有入口清除，展示原结果；记录同一阻塞 ID 的成功处理确认。玩家“知道了”关闭结果后 Host 请求 Resume 原 RequestId，在下一正常 Host 调度点继续同一 target；不在选项回调中嵌套 Tick/Return，不重跑刚完成边界。不需要重新点击 Retreat 来推进，也不会因为再点 Return 而新建会话。
6. UI 自己 RestoreAfterClose / OnDisable 不拥有推进恢复权；允许还原表现层暂停意图，但会话锁优先，关闭未解决窗口、禁用组件或恢复 Speed 都不能解除 Waiting/FinalSave锁。只有上述成功确认+结果关闭才允许 Bridge Resume；不把“Pending被任意清空”或窗口消失当作已执行成功。
7. Resume 后在下一个完整点再次检查；新的真正阻塞 Event 按同一路径处理。原目标已达到则只完成 motion，继续已有 Carry / Consequence / Clear / FinalSave，绝不再走一天。headless 测试通过显式 fixture处理同一 Option executor并确认结果关闭，不做生产自动选择或另建模拟器。

Waiting/Yield 期间不转移余下 Carry、不解除远征占用、不发布返回完成、不保存中间 World。已有 Event effects 可以改变允许的资源/恢复窗口，那是玩家正在处理的当前阻塞 transaction；其余手动世界操作被锁定。Event 处理失败、UI 显示失败与 PartialBoundaryFailure 是不同状态，不把配置显示失败伪装成回滚成功。

## 5. 子日内补给：Confirmed / Awaiting Implementation

制作人在 V0.2 修订请求中已明确裁决：基础 Supply 随累计时长推导，**整次远征累计向上取整、节点只扣增量**。不再 Awaiting Gameplay Decision；当前 Runtime 尚未迁移，正式 GameSpec 归位待文档入仓授权。消费率等数值仍 Calibration / Not Frozen。

**Confirmed：按整次远征累计精确需求向上取整；每个节点只扣累计总额的增量。**

```
N = 累计 Σ(PartySize × PerPersonDailyRate × durationMinutes)
baseRequired = ceil(N / 1440)
thisBaseCost = newBaseRequired - priorBaseRequired
thisTotalCost = thisBaseCost + explicitExtraCost
```

使用 checked long 有界运算；整日成本保持旧公式。N 与 priorBaseRequired 为 Supply 会话状态，不依赖节点个数。Preflight 对原 N 做只读预测，Resolve 一次提交 N / 费用 / elapsed；重复解析不增加 N。实际 PartySize 继续复用现有 PersonId 集合口径，不顺手裁决 Hero 或中途招募供给规则；如会话已有合法规模变化，以实际区间人数累积 N，不能对过去重算。

在 PartySize>0、消费率>0 时，正短程需要至少 1 整份；消费率=0 则基础成本为0。基础整份不得每个短节点重收。初始出发 preflight 必须复用相同预测器，余下 Carry 按已扣整份值返还，不退掉累计向上取整的份额；无额外“返程四舍五入”费用。

具体验收断言（以下 1 Food/日、人数为测试参数，Not Frozen）：

| 输入 | 基础成本 / 节点基础扣减断言 |
|---|---|
| 1人，4×360分钟 vs 1×1440分钟 | 总成本均1；四段增量为[1,0,0,0] |
| 1人，2×900分钟 vs 1×1800分钟 | 总成本均2；两段增量为[1,1] |
| 1人，1分钟后再1439分钟 | 增量[1,0]；再加1分钟则增量1、累计2 |
| 2人，3×240分钟 vs 1×720分钟 | 总成本均1；三段增量[1,0,0] |
| 2人，1440分钟 | 总成本2，保持旧整日公式 |
| 1人，消费率0，1800分钟 | 基础0；显式extra仍独立扣减 |
| 0分钟，ExtraCost=2 | 基础0、总成本2、时间不增加 |
| 4×360分钟且四段Extra=[2,0,3,0] | 基础1、Extra5、总6；不能把Extra计入取整分子 |
| Resolve同一节点两次、Return重试/FinalSave重试 | 第二次不改变N、priorBaseRequired、Carry或elapsed |
| Carry=1，先结算900分钟，再进入另900分钟 | 第一步扣1；第二步预测新增1、返回InsufficientSupply，零mutation |
| Event旧额外1天，叠加节点360分钟，1人1/日 | 时长1800分钟、基础2；预览和Resolve完全一致 |
| 已发生Defeat，节点360分钟 | 基础1、elapsed360、Resolved-not-Completed；重试不双扣 |
| 分子 / 累计elapsed / Extra+Base溢出 | 明确失败，全部相关状态保持原样 |

计算 ceil 使用 quotient + 非零 remainder 判定，避免 `(N+1439)` 本身溢出。取整在人数×日率×累计分钟合并后发生，不对每个人或每个节点分别 ceil；未来合法规模变化按区间加权，不追溯重算。无需 Supply 策略框架。

<a id="save"></a>
## 6. Save 草案与旧档兼容

保持顶层 SaveVersion=1 的技术方案是 **additive nullable 时间语义区块 + 显式 SchemaVersion**，不是靠字段默认值猜新旧。拟在 `VillageTimeSaveData` 新增 `IntradayTime`，内容：

| 字段 | 语义 |
|---|---|
| SchemaVersion=1 | 该子区块时间语义版本 |
| CurrentWorldMinute: long | 唯一权威时刻 |
| DriverRemainder | 已积累但未变成完整世界分钟的驱动余量；有限、规范范围 |
| LastCompletedDailyBoundaryMinute | 当前已完整处理的日界标记，避免旧消费者重放 |

A 不序列化 AdvanceSession、OriginalTarget、AcceptedRequestId、阻塞Event关联或Return会话：只允许 Idle 完整快照，以及已完整完成返程前置阶段的专用 FinalSave。Yield / Waiting 虽保有完整游标，也不满足此持久化许可；不能仅写游标而省掉原请求造成 Load 后剩余推进丢失。DriverRemainder 仅代表已接纳且未构成整分钟的余量，不是等待期间 wall-clock backlog。以后若需要在 Jump / Waiting 中 Save-and-Resume，应另行设计请求 DTO / Event 关联恢复，本 A 不假称已支持。

继续输出旧 Year / Month / Day 为一致的派生兼容字段；SecondsIntoDay 可作为兼容驱动投影，但新读者只认新区块。**不承诺旧二进制能无损读取新档**。Time 与日期字段矛盾、标记晚于当前时刻、非 midnight 标记、未知 schema / 溢出 / 非有限余量明确失败，不先清状态。不保存 Boundary 正在执行到一半的快照。

Legacy：实际旧档只有日期、Speed、SecondsIntoDay，没有保存 SecondsPerDay；所以不能保证精确还原任意历史 Inspector 校准。推荐使用集中初始化的 LegacySecondsPerDay（本基线代码默认 5，Test / Not Frozen），显式记录 LegacyApproximateMigration 诊断：合法旧余量按该基准转为日内进度，保留小于一分钟余量；非法 / 越界余量拒绝时间迁移，不暗中补造日结。只转换、不跑过去日链、不重开活动、不 Drain。

迁移时将旧日期日界视为已处理，以防已领取日奖励重复；这保持现有 Load 不 replay 语义。无法从旧档重建最后一日真实劳动、午餐、出发或活动完成时分，不伪造它们。未知旧校准仅影响近似时分，不得重新计算旧资源 / Social / Formation。

SaveSystem 在任何 World Restore 前只读验证 / 构造待恢复时间，与 DecisionWorld / FeedbackWorld 现有 preflight并列。Player 准备成功后，提交新增时间状态；针对非法新增区块的 Load 必须失败且旧时间 / Decision / Feedback 原样。保持已登记 Village/Event 先写后失败债务，**不扩大保证为任何错误都全世界未变**；实现时若无法在现有桥中分离时间恢复而保证这一窄范围，须报告最小依赖改动，不偷偷改全球事务。

new-schema null / legacy missing 与 invalid 要明确区分，验证 JsonUtility 实际 absent/null materialization、long round-trip、未知字段与根成员重复，不假设 CLR 默认就是缺块。可采用现有 root member 检查模式，避免引入第二 JSON 系统。

NewGame 显式重置新时间 / 游标 / 余量；Load Other World 为 Replacement，不继承游标。普通 Save / Load 日内时间 round-trip；Load 不前进 Clock、不触发 DayTick / MonthTick，不运行 Selection。Return Record、Progression、Carry 会话仍不入 Save，无新增远征 checkpoint；B/C 的持久化关联不留到最后才补。

## 7. A 的精确建议写入范围（尚未授权实现）

前缀 `UnityProject/RRPG1.0game/Assets/`。本列表按消费者闭包组织，不是让开发机械修改所有文件；未需要的文件保持不动。

- 时间：Scripts/Village/Runtime/VillageTime.cs、VillageSaveData.cs、ExpeditionRecord.cs；可在同目录新增 WorldInstant.cs / WorldTimeAdvanceResult.cs / 时间推进辅助文件及对应 Unity meta。
- 编排：Scripts/Village/VillageRuntimeBridge.cs；Scripts/Village/UI/VillageTimeDriver.cs、VillageHUD.cs；初始化 / 时分显示 / 统一请求、Resume、操作锁；Scene / Prefab 不改。
- 远征纯逻辑：Scripts/Adventure/Progression/Runtime/ExpeditionNodeData.cs、ExpeditionProgressionState.cs、ExpeditionSupplyState.cs、ExpeditionProgressionResolver.cs、ExpeditionProgressionResult.cs。
- 兼容接线：Scripts/Adventure/Progression/LegacyMapGraphAdapter.cs、LegacyNodeElapsedDaysTestPolicy.cs；Scripts/Adventure/AdventureManager.cs；Scripts/UI/ExpeditionUI.cs、ExpeditionJourneyCoordinator.cs、ExpeditionJourneyView.cs；旧 ProgressionPanel 只有出现显示 / 编译依赖才最小适配。
- Event兼容：Scripts/Adventure/ExpeditionEvent/AdventureManager.ExpeditionEvent.cs、ActiveExpeditionEventState.cs；Scripts/EventHost/ExpeditionEventFlow.cs、ExpeditionEventEffectGateway.cs，预估成本 / pending-time适配；Scripts/EventHost/VillageEventFlow.cs 仅既有阻塞查询 / 执行结果接缝；Scripts/Village/UI/VillageSocialEventUI.cs 仅绑定初始化、现有窗口临时挂载/还原、实例匹配、结果关闭确认与暂停锁接线，不改 Option / Outcome / Effect 算法、通用 Event Effect 的“天”语义或 Event 工作簿。
- 保存/玩家入口：Scripts/Save/SaveSystem.cs；SaveData.cs 只有新增顶层字段确需时改，默认时间留 Village 区块；Scripts/UI/TownUI.cs 的 FinalSave / 展示 / gating分离；Scripts/UI/CharacterGrowthUI.cs、MainMenuUI.cs 仅现有写操作在改变前的 Busy拒绝 / 明确失败反馈，必要时其它检索已确认的 UI 写入口同样最小补守卫并登记。不得改变成长/新游戏内容、资产或产品算法。
- 测试：Assets/Tests/EditMode/Village、ExpeditionProgression、ExpeditionV4F 现有相关测试；可新增 Assets/Tests/EditMode/IntradayTime；Assets/Tests/PlayMode/ExpeditionV4F 真实 Return 产品测试。相关夹具的旧字段赋值 / 天单位迁移须在引用清单内精确登记；AutonomousLife 原测试优先原样回归。

A 不需要新 Compiler / Builder 或工作簿。参数通过现有时间初始化 seam、旧 Test lookup / 测试 fixture 提供；不人工编辑 Generated，不给测试耗时冒充正式静态内容。未来正式节点 authoring 接现有 XLSX→Compiler→Generated→Builder→Registry，不新建平行系统。

禁止写 Social / Activity / Selection 的产品算法、Formation算法、Assignment / Project玩法、资源算法、Person核心、Battle算法、Scene / Prefab / 图片 / 字体 / Config工作簿。Unity 自动生成新代码meta仅在后续Runtime授权内使用正常生命周期。asmdef默认无变化。必要编译依赖适配只能按实际消费者清单最小修改，不能扩成无关重构。

## 8. 后续 B / C / D 的接口与依赖 Gate

| 阶段 | 建议修改的现有归属（非本次授权） | 保留 / 替换 | 必须随阶段验证 |
|---|---|---|---|
| B 职责 / 资源 / 午餐 | VillageAssignmentSystem、VillageProjectSystem、VillageResourceSettlement、VillageResourceState、VillageSaveData、VillageRuntimeBridge；FormationEvidence / mapper接口与Person Fulfillment适配，仅必要劳动输入 | Assignment长期职责、Project唯一完成点、库存Authority、FormationResolver日算法保留；成员人数给每日进度、SettleDay整体发放、MapWorkplaceDay全日输入按迁移逐项停用 | 无人项目不丢档；材料开工保留；换人不双产；午餐一次；不在村无份；真实劳动日聚合一次；正式项目加入的Recognition/Event不因复工重复 |
| C 持续活动 | Activity Runtime / Host、Selection DecisionWorld / Coordinator / Host、Village推进Host、Save集成 | 快照/Desire/Ordering/Arbitration保留；当天一轮与提交即Complete替换；唯一Lifecycle保留 | 新机会稳定ID；安排≠完成；刷新不清in-flight；日内/跨日恢复必要活动进度与重建Claim；NoMajor机会也去重；stale guard与Feedback Barrier仍成立 |
| D 现场互动 / 表现 | Activity唯一生命周期与最小成员结果；Feedback Projector / Outbox适配；Host Presence / view投影 | Social-owned Interpreter、Canonical Ledger、Durable Outbox保留；不新增Recognition / LifeEpisodeAuthority | 到场再验接待；扑空无success；共享场景协调个人结果；早退/跨日稳定成员resultId；Prepare在有效完成前；不全员两两奖励；表现不决定到达 |

顺序为 A→B→C→D 的依赖计划，不强行新立四个 Work Package，不把原 WP-AL-5 静默改号。世界表现最终验收必须用产品路径，纯域测试不能代替人工生活体验。

后续最小产品建议（**不阻塞 A**）：

- 午后回村：不追补已过去午餐、不按半天折算。此前合法远征补给作为其本日远征供养证据；不能把“不在村午餐快照”直接写成 Underfed。B 必须决定部分补给 / 原先 Hungry / 同日午前回村怎样更新 streak，防双扣或双惩罚；A 不新增营养写回。
- 项目出征：B 推荐保留项目承诺、暂离时不贡献，其他人在场继续、无人则Paused并保存，返村恢复优先安排；不自动清承诺或重复正式Join。是否预约容量属于玩法管理语义，B 实施前裁决。A 保持现有 ActiveProject / Expedition互斥，不先放宽Hard Gate。
- 动态多人：优先评估“每次个人访问有个人稳定结果，共享场景协调共处”的最小方案；有真实共享动作再扩成员生命周期。有效参与门槛、接待兼容为 D 玩法裁决；不给任意两条独占Claim。

## 9. A 的验收 Gate（全部待 Runtime 实施后执行）

| Gate | 必须覆盖 |
|---|---|
| A1 时间纯逻辑 | 08:00+30h=次日14:00；跨月/闰年/跨年；0与非法/overflow；Pause/Fast；不同驱动切片不丢余量；同目标no-op；Yield游标；TickDay和Jump语义 |
| A2 日链与边界 | direct/Update/Fast/Jump/Catchup相同目标、输入、seed下状态一致；中间测试12:00不漏；旧日/月消费者每边界一次、顺序保留；Feedback Barrier在新Snapshot前；MissingPersonSourcefail closed |
| A3 节点/补给 | Enter只读不扣；Success/Defeat已发生cost；重复Resolve；完整执行§5所有具体成本/增量断言；zeroDuration+Extra；Event旧天转换及SupplyDelta组合；checked溢出零mutation；costpreview与真实一致 |
| A4 返程完整性 | departure捕获而非Return时捕获；Retreat/Defeat/SafeReturn同target；重试不改mode/target；between边界失败可续、partial失败禁止续；carry仅catchup后；健康/经历一次；FinalSave冻结/重试与Pending UI续跑按下列专项断言 |
| A5 Save兼容 | 新日内roundtrip；旧字段显式迁移诊断；absent/null/newinvalid；long/余量合法性；invalid时间preflight保留旧状态；Idle的NewGame/WorldA→B replacement；活跃请求所有保存/替换入口拒绝；专用FinalSave许可；不Loadreplay；全局债务不冒充解决；SaveVersion=1实证 |
| A6 产品回归/范围 | 用真实UI确认→Progression→Return→Town读到实际时分，headless同结果；既有Return/Resource/Project/Formation/Event/AL1～4回归；Compile0；targeted/fullEdit/fullPlay；git diff --check；受保护hash与Scene/Prefab不变 |

专项验收（尚未执行；必须包含真实产品路径，不止Mock）：

1. **FinalSave failure freeze**：目标到达后注入Save=false，等待10秒、多帧Update、HUD Resume/Fast/Jump/显式TickDay、重复Return。CurrentInstant=ArrivalTarget；完整游标、DayTick次数、DecisionWorld、资源、Person状态、Facts均不变化，Completed=false、OccupancyCleared及ack保留；普通Save/新出征/项目/成长/世界替换均在改变前拒绝。移除失败注入，专用retry只保存一次成功、释放一次，无阶段重跑；原本Paused仍Paused。领地与非领地两条收尾路径都覆盖。
2. **Pending actual UI**：从Town发起真实远征、TownCanvas隐藏，补算在完整日界产生Timely+PauseOnOpen Pending；已有VillageSocialEventUI确实可见且可点击，非另一个Event卡。停在该日界，远征占用及remaining Carry仍未Clear/返还，原目标仍次日14:00；默认TimeDriver无任何推进，所有Save入口拒绝且原存档字节不变。
3. **Option failure/close**：执行不可行选项→Pending仍是同一ID、History/效果零新增；关闭错误显示或禁用UI不能解除Waiting，重新打开仍同一Outcome来源，不能自动改选/重抽。stale按钮与双击不会执行后来的Pending。
4. **Option success/resume**：成功Option effects及History只提交一次；结果展示关闭前仍Waiting；关闭后下一Host调度Resume同一ID。刚处理的DayTick不重跑，继续至原14:00；若等待点就是目标，到达后不额外Tick。Window/Selection及反馈Barrier按原日链执行一次；最后只在完整Return后保存。Ignorable/PauseOnOpen=false沿已有分类不强制等待；缺配置/宿主以诊断fail closed。
5. **Yield/request conservation**：同一原命令跨至少3个预算Yield；各次OriginalTarget不变，游标严格前进；期间Update/Jump/第二Return命令Busy、时间/余量零新增。所有已采纳分钟最终完成，等待现实秒数不形成新backlog；Driver与UI没有剩余时长副本。Yield完整点也禁止Save，不生成缺request的可加载中间档。
6. **Permission bypass**：Save、SaveWithVillage、UI自动Save、普通Finalize与非Town路径全部覆盖；错误FinalSave身份拒绝写盘；进入PartialBoundaryFailure不允许普通保存/恢复推进；不存在“关弹窗→Speed恢复→直接Tick”旁路。

建议复用：VillageTimeTests；ExpeditionElapsedDaysExactlyOnceTests / ExpeditionSupplyConsumptionTests；ExpeditionCatchUpDayTests / CatchUpFailureTests / ReturnIntegrityTests / ReturnOrderSaveTests；ExpeditionStage3ReturnContractPlayTests / ExpeditionReturnIntegrityProductPlayTests / VillageResourceCarryE2EPlayTests；增加VillageSocialEventUI产品接线专项。旧整数测试可继续以 1440 分钟验证，不删除已通过契约来让测试变绿。

停止条件：必须第二Clock、内容随机重抽、改Battle耗时规则、改通用Event单位或dirty工作簿、放宽项目出征语义、改变午餐/营养、必须跨进程RunResume或全世界2PC、必须Scene/Prefab结构修改、SaveVersion=1无法证明安全兼容。普通helper/命名/格式/编译夹具不作为裁决点。

## 10. 必要 Authority 同步清单（后续授权落仓时）

- GameSpec/Expedition/Expedition-Progression-GameSpec-V0.1.md §8/9：世界分钟累计、真实arrival、已确认的整次累计向上取整/节点扣增量；FinalSave锁和返程Waiting接线；保留禁止每Node直接TickWorld与Return守卫。
- GameSpec/Village/Village-GameSpec-V0.1.md 时间章节：唯一Clock、兼容TickDay/Jump契约；§12仍保留每日自主Current，标明C迁移尚未实施。
- GameSpec/Village/Village-Resource-Coupling-GameSpec-V0.1.md：先登记现行整份列表行为与EqualShare差异，具体午餐迁移在B授权时归位；不能在A把旧供餐和新供餐一起发放。
- GameSpec/Save/Save-GameSpec-V0.1.md：新时间区块兼容边界、旧档近似解释与invalid失败保证；不升级全球事务承诺。
- Current Architecture两份：实施前只能新增Target迁移段，实施验证后才登记Current；旧WP发布事实保留。项目状态、DevelopmentSpecs导航、地图、Handoff按阶段准确同步；历史日志/日期化规格不伪装重写。

本轮没有修改以上仓库文档，没有生成Runtime-Review.diff。A阶段补给取整已由制作人裁决；三项完整性接缝已形成具体契约与验收断言。本次没有新增必须裁决的A玩法问题。B营养/项目、D有效参与继续是未来Gate，不拉回阻塞本次修订，也未被本文偷偷冻结。

## 11. V0.1 T0 完成核验（保留历史语境）

本轮开始 / 结束 2902 个仓库文件 SHA256 全部相同，新增文件只在仓库外输出目录。main HEAD 未变，暂存区为空，git status 仍为原有 3 tracked 修改 + 14 untracked。TMP 相对于本轮开始不变，先前 Runtime 回归引起的 integrity exception 仍保留、未恢复、未提交。

`git diff --check` exit 0；Git 对原 TMP / QualitySettings 的 LF→CRLF 提示为已有工作区换行提示，本轮未触碰这些文件。草案 / 消费者索引均无 trailing whitespace；索引本地文件链接逐条检查。没有编译、测试、Builder、stage、commit、push 或新 Work Package 启动。

阅读配套消费者索引时，可按“时间与远征耗时/成本/返程”“日期身份与活动/决策/反馈”“配置与Compiler时间契约”定位来源。该索引保留匹配行，不复制完整实现，不替代此规格的语义审查结论。

## 12. V0.2 修订完成核验与变更摘要

- §4.1 收口唯一 AdvanceSession：原请求/目标、完整游标、驱动余量、等待实例关联与保存许可；Return/UI没有另一份可写进度。明确活跃会话拒绝新delta、只续原请求，Yield/Waiting不持久化、不允许普通Save。
- §4.2 补齐 FinalSave冻结：实际Town收尾无条件恢复模拟是待修接缝。失败仍OccupancyCleared，锁定世界写操作与推进，只允许专用retry；成功保存后才正常进入Town并释放，不用Paused倍率替代互斥锁。
- §4.3 指定真实Pending UI：复用VillageSocialEventUI并处理远征期间inactive父级，既有Option executor与History不变；失败关窗不放行，成功处理+结果关闭后续原ID / 原target。临时宿主方案需后续产品验证，不写成本轮已验证场景事实。
- §5 将累计向上取整改为Confirmed，新增13组具体断言；数值仍测试校准。§7同步最低必要接缝，§9新增6组暂停/权限/UI/续跑产品验证要求。
- main仍 `1ee486289214dd0497a0d0b048da1c30592b0be7`。本次独立建立的2902文件hash基线结束复核0变化；git status仍为原有3 tracked修改+14 untracked，暂存区为空；TMP原有exception保留、本次不变。
- `git diff --check` exit0。修订稿4个显式锚点无重复，无trailing whitespace；未改V0.1历史草案或原消费者索引，修订稿新增在仓库外。未运行Unity/测试/Builder/Compiler，未stage/commit/push或发布。
