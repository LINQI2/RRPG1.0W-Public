# WP-AL-2 Activity Lifecycle & Daily Integration — Closeout

> 日期：2026-10-04
> 类型：实施 / 验证 / Architecture Review / Closeout 证据；不是产品规则或 Target 能力的实施授权。
> 状态：Runtime 已发布 review/wp-al-2，开发15.0 GitHub Real-Commit Architecture Review PASS；本次文档同步提交到同一分支。未合入 main，不宣称 Repository / 共享工作区 Closed。
> 实际基线：main@0c8722ecfbe353d0b2777360a9059516459138cb
> Runtime commit：70754649e9ad8b0186a0944a1bccd06c81d79e12
> Current Architecture：[总览](../Architecture/技术架构总览.md) / [对象映射](../Architecture/核心对象与术语映射.md)；输入：[Development Spec V0.2](2026-10-04_RRPG_WP-AL-2_Activity-Lifecycle-Daily-Integration_Development-Spec-V0.2.md)。日期化输入的 Ready 状态保留发布时语境。
> 文档 Commit SHA / 最终 Push 结果查 Git History；不回填自身未来 SHA。

## 1. 实际实现文件

Runtime commit 为 **53 文件，+2611 / −1**：3 个既有 Runtime 修改、22 个新 C# / asmdef（15 Runtime / Host、7 Tests）、27 个 Unity meta、1 份 Development Spec。27 个 meta 随对应文件 / 文件夹提交；发布前只删除标准空字段 81 处尾随 ASCII 空格，GUID / importer / 结构 / 编码 / 换行不变。Spec 行尾格式修正未改语义。

以下为该真实 Commit 的完整 name-status（A 新增，M 修改），不是本次文档提交清单：

```text
A	Docs/DevelopmentSpecs/2026-10-04_RRPG_WP-AL-2_Activity-Lifecycle-Daily-Integration_Development-Spec-V0.2.md
A	UnityProject/RRPG1.0game/Assets/Scripts/ActivityHost.meta
A	UnityProject/RRPG1.0game/Assets/Scripts/ActivityHost/ActivityRuntimeBridge.cs
A	UnityProject/RRPG1.0game/Assets/Scripts/ActivityHost/ActivityRuntimeBridge.cs.meta
A	UnityProject/RRPG1.0game/Assets/Scripts/AutonomousLife.meta
A	UnityProject/RRPG1.0game/Assets/Scripts/AutonomousLife/Activity.meta
A	UnityProject/RRPG1.0game/Assets/Scripts/AutonomousLife/Activity/Runtime.meta
A	UnityProject/RRPG1.0game/Assets/Scripts/AutonomousLife/Activity/Runtime/ActivityCandidate.cs
A	UnityProject/RRPG1.0game/Assets/Scripts/AutonomousLife/Activity/Runtime/ActivityCandidate.cs.meta
A	UnityProject/RRPG1.0game/Assets/Scripts/AutonomousLife/Activity/Runtime/ActivityClaimRegistry.cs
A	UnityProject/RRPG1.0game/Assets/Scripts/AutonomousLife/Activity/Runtime/ActivityClaimRegistry.cs.meta
A	UnityProject/RRPG1.0game/Assets/Scripts/AutonomousLife/Activity/Runtime/ActivityCompletionSnapshot.cs
A	UnityProject/RRPG1.0game/Assets/Scripts/AutonomousLife/Activity/Runtime/ActivityCompletionSnapshot.cs.meta
A	UnityProject/RRPG1.0game/Assets/Scripts/AutonomousLife/Activity/Runtime/ActivityDailyWindow.cs
A	UnityProject/RRPG1.0game/Assets/Scripts/AutonomousLife/Activity/Runtime/ActivityDailyWindow.cs.meta
A	UnityProject/RRPG1.0game/Assets/Scripts/AutonomousLife/Activity/Runtime/ActivityDayStamp.cs
A	UnityProject/RRPG1.0game/Assets/Scripts/AutonomousLife/Activity/Runtime/ActivityDayStamp.cs.meta
A	UnityProject/RRPG1.0game/Assets/Scripts/AutonomousLife/Activity/Runtime/ActivityInstance.cs
A	UnityProject/RRPG1.0game/Assets/Scripts/AutonomousLife/Activity/Runtime/ActivityInstance.cs.meta
A	UnityProject/RRPG1.0game/Assets/Scripts/AutonomousLife/Activity/Runtime/ActivityLifecycleService.cs
A	UnityProject/RRPG1.0game/Assets/Scripts/AutonomousLife/Activity/Runtime/ActivityLifecycleService.cs.meta
A	UnityProject/RRPG1.0game/Assets/Scripts/AutonomousLife/Activity/Runtime/ActivityLifecycleState.cs
A	UnityProject/RRPG1.0game/Assets/Scripts/AutonomousLife/Activity/Runtime/ActivityLifecycleState.cs.meta
A	UnityProject/RRPG1.0game/Assets/Scripts/AutonomousLife/Activity/Runtime/ActivityWorldPersistence.cs
A	UnityProject/RRPG1.0game/Assets/Scripts/AutonomousLife/Activity/Runtime/ActivityWorldPersistence.cs.meta
A	UnityProject/RRPG1.0game/Assets/Scripts/AutonomousLife/Activity/Runtime/ActivityWorldSaveData.cs
A	UnityProject/RRPG1.0game/Assets/Scripts/AutonomousLife/Activity/Runtime/ActivityWorldSaveData.cs.meta
A	UnityProject/RRPG1.0game/Assets/Scripts/AutonomousLife/Activity/Runtime/ActivityWorldState.cs
A	UnityProject/RRPG1.0game/Assets/Scripts/AutonomousLife/Activity/Runtime/ActivityWorldState.cs.meta
A	UnityProject/RRPG1.0game/Assets/Scripts/AutonomousLife/Activity/Runtime/AutonomousActivity.Runtime.asmdef
A	UnityProject/RRPG1.0game/Assets/Scripts/AutonomousLife/Activity/Runtime/AutonomousActivity.Runtime.asmdef.meta
A	UnityProject/RRPG1.0game/Assets/Scripts/AutonomousLife/Activity/Runtime/IActivityProvider.cs
A	UnityProject/RRPG1.0game/Assets/Scripts/AutonomousLife/Activity/Runtime/IActivityProvider.cs.meta
A	UnityProject/RRPG1.0game/Assets/Scripts/AutonomousLife/Activity/Runtime/PersonActivityState.cs
A	UnityProject/RRPG1.0game/Assets/Scripts/AutonomousLife/Activity/Runtime/PersonActivityState.cs.meta
M	UnityProject/RRPG1.0game/Assets/Scripts/Save/SaveData.cs
M	UnityProject/RRPG1.0game/Assets/Scripts/Save/SaveSystem.cs
M	UnityProject/RRPG1.0game/Assets/Scripts/Village/VillageRuntimeBridge.cs
A	UnityProject/RRPG1.0game/Assets/Tests/EditMode/AutonomousLife/ActivityLifecycle.meta
A	UnityProject/RRPG1.0game/Assets/Tests/EditMode/AutonomousLife/ActivityLifecycle/ActivityCrossDomainTests.cs
A	UnityProject/RRPG1.0game/Assets/Tests/EditMode/AutonomousLife/ActivityLifecycle/ActivityCrossDomainTests.cs.meta
A	UnityProject/RRPG1.0game/Assets/Tests/EditMode/AutonomousLife/ActivityLifecycle/ActivityIntegrationTestKit.cs
A	UnityProject/RRPG1.0game/Assets/Tests/EditMode/AutonomousLife/ActivityLifecycle/ActivityIntegrationTestKit.cs.meta
A	UnityProject/RRPG1.0game/Assets/Tests/EditMode/AutonomousLife/ActivityLifecycle/ActivityLifecycle.Tests.asmdef
A	UnityProject/RRPG1.0game/Assets/Tests/EditMode/AutonomousLife/ActivityLifecycle/ActivityLifecycle.Tests.asmdef.meta
A	UnityProject/RRPG1.0game/Assets/Tests/EditMode/AutonomousLife/ActivityLifecycle/ActivityLifecycleTests.cs
A	UnityProject/RRPG1.0game/Assets/Tests/EditMode/AutonomousLife/ActivityLifecycle/ActivityLifecycleTests.cs.meta
A	UnityProject/RRPG1.0game/Assets/Tests/EditMode/AutonomousLife/ActivityLifecycle/ActivityPersistenceTests.cs
A	UnityProject/RRPG1.0game/Assets/Tests/EditMode/AutonomousLife/ActivityLifecycle/ActivityPersistenceTests.cs.meta
A	UnityProject/RRPG1.0game/Assets/Tests/EditMode/AutonomousLife/ActivityLifecycle/ActivitySaveIntegrationTests.cs
A	UnityProject/RRPG1.0game/Assets/Tests/EditMode/AutonomousLife/ActivityLifecycle/ActivitySaveIntegrationTests.cs.meta
A	UnityProject/RRPG1.0game/Assets/Tests/EditMode/AutonomousLife/ActivityLifecycle/ActivityVillageIntegrationTests.cs
A	UnityProject/RRPG1.0game/Assets/Tests/EditMode/AutonomousLife/ActivityLifecycle/ActivityVillageIntegrationTests.cs.meta
```

Scene / Prefab 未修改；制作人已有 Config / TMP 字体 / QualitySettings / Battle / UnitHUD / ChatGPT 图片及 meta 未进入 Runtime commit，工作区增量哈希不变。两个旧本地 Review .diff 未提交；本 WP 不另生成 Runtime-Review.diff。

## 2. 模块与 Public API

AutonomousActivity.Runtime：references=[]、noEngineReferences=true、autoReferenced=true。纯域不依赖 Village / Social / Event / Person / Battle / UI / UnityEngine；ActivityHost 位于 Assembly-CSharp，通过自动引用接现有 Village / Save。ActivityLifecycle.Tests 是 Editor only 测试程序集，引用 AutonomousActivity.Runtime、Village.Runtime、Social.Runtime；测试依赖不等于 Activity 纯域依赖。

- 输入契约：ActivityDayStamp、ActivityDailyWindow、ActivityCandidate；IActivityProvider.CollectCandidates 是查询契约，没有 Production Provider。
- 业务入口：ActivityLifecycleService.OpenDailyWindow、TryCommitActivity、TryBeginExecution、TryComplete、TryFail、TryInterrupt。
- 只读输出：CurrentWindow、ActivityInstance / LifecycleState、ActivityClaimRegistry 查询、QueryPersonActivityState、ActivityCompletionSnapshot。
- 持久化：ActivityWorldPersistence.Build / Restore、ActivityWorldSaveData / PersonActivityStates DTO；Bridge 暴露同域窗口 / 生命周期查询与操作，以及 BuildSaveData / Restore / ResetForNewGame / ResetForTests。
- PersonId 为长期人物身份，ActivityInstanceId 由调用方提供稳定字符串；没有 Guid / timestamp / hash / Random 身份生成。

Window 的两个集合去空、去重、ordinal 稳定排序、不可变；同日完全相同 no-op，不同 WindowConflict / fail closed。Candidate defensive copy，actor 与参与者去重；非空 Target 视为实际参与者并 Claim。查询不创建人物长期记录，Missing 返回 Empty / Neutral。

## 3. Commit、FSM 与长期状态

TryCommitActivity 在任何正式 mutation 前验证 Window、actor eligibility、participant availability、既有 claim、当天 Completed participation、InstanceId 冲突和候选合法性；通过后一次性建立 instance + 全部 claims，拒绝无 half claim。Claim 不写 Village Temporary Occupancy、Project、Assignment、Expedition / Unavailable 集合。

成功路径：Committed → Executing → Completed。Committed / Executing 可进入 Failed 或 Interrupted；终态不可再改变。重复 Complete no-op、不重复消费、不再次产生成功 Snapshot。所有终态释放 claims；只有 Completed 返回不可变 Completion Snapshot，并更新长期状态。

PersonActivityState 仅 LastMajorParticipationDay、LastInitiatedActivityDay、LastInitiatedActivityType。所有成功参与者更新 participation day，只有 actor 更新 initiated day / type；Failed / Interrupted 不更新。没有 LastMajorParticipationActivityType、RepeatStreak 或最近 N 次历史。Snapshot 含 instance / type / actor / target / place / participants / day，不落档，不生成 Social Fact / Event。

## 4. Social / Activity 与时间 Authority

Social 的 Fact / Relationship Authority 不变；Activity 与 Social 没有生产反馈链，不解释关系、不调用 Event / RNG，不创建 Scene / GameObject。已验证跨域边界不修改 Assignment / Project。

唯一日链：VillageTime.TickDay → HandleDayTick：
State → capture occupiedAtStartOfDay → Projects / completion → Assignments（原快照）→ Resource Settlement → Formation → Activity Open Window。

direct Tick、实时 Update、Jump Month、Return Catch-up 共用同一 Phase；不新增 Clock、订阅、MonoBehaviour Tick 或 Catch-up simulator，不改变 Return 顺序。日结只 Open，不扫 Provider / Utility / Commit / 自动执行 / Event / Social。

Eligibility 与 Availability 独立字段，首版同取 InVillage 且不在原 occupiedAtStartOfDay；Primary Assignment / PersonRest Recovery Window 不阻止，Expedition / Unavailable / Dead / 日初 Active Project 阻止。Project 完成当日仍 blocked，次日恢复。SimulationPersonSource=null 明确 MissingPersonSource 且保留旧 Window，不伪装为零人，不建立第二人物来源；bound-empty 是真实空 Window。

## 5. ActivityWorld Save Schema 与恢复

SaveData.ActivityWorld → PersonActivityStates[]：

```text
PersonId
LastParticipationYear / LastParticipationMonth / LastParticipationDay
LastInitiatedYear / LastInitiatedMonth / LastInitiatedDay
LastInitiatedActivityType
```

SaveVersion 仍为 **1**，additive nullable 区块。不保存 Current Window、Candidate、Provider、Claim、Instance、Executing / in-flight 或 Completion Snapshot。

Build 按 PersonId ordinal 排序；Restore 准备有效数据后 Replacement，清 Window / claims / instances 并精确恢复长期人物状态，不重执行、replay 或 reopen。非法数据过滤、重复 PersonId 保留首条有效记录；unknown / inactive PersonId 保留。null / 旧档无 ActivityWorld → empty。

所有正式 Save 入口携带 Activity snapshot；Player Restore 成功后才 Restore Social / Activity；failed Load 保留原 ActivityWorld；successful NewGame 显式 reset。Load 不自动打开 / 重放当天 Phase；当天成功参与在 Save / Load 后保留，调用方再显式 Open 同日窗口也不能第二次 Commit Major Activity。不宣称 Village / Event 全世界 Load transaction 已解决。

## 6. 验证证据（Runtime 实施轮；本次文档任务未重跑）

| 检查 | 真实结果 |
|---|---|
| Compile | 0 error |
| Gate A / pure Activity | 26/26 PASS：窗口幂等 / conflict / ordinal / immutable、原子 Claim、FSM、成功参与状态 |
| Gate B / Persistence | 12/12 PASS：replacement / null / transient 不保存、清 claims、稳定排序 / 过滤、missing query、无 replay |
| Gate C / Village Integration | 14/14 PASS：Assignment / PersonRest、Expedition / unavailable / dead / 日初 Project、完成日 / 次日、direct / Jump / catch-up、missing / bound-empty source |
| Gate D / SaveSystem | 14/14 PASS：真实 round trip、同日防重、legacy / NewGame / replacement / failed Load、SaveVersion=1、Load 不 reopen / replay |
| Gate E / cross-domain | 3/3 PASS：Social / Relationship / Event / Assignment / Project / RNG / GameObject 边界 |
| Targeted EditMode | 69/69 PASS |
| Full EditMode | 1181/1181 PASS（此前 1112 原有用例 0 regression） |
| Full PlayMode 最终 | 93/93 PASS |
| Runtime 发布前 diff / cached check | PASS |
| Runtime Semantic Self-Review | PASS |
| 开发15.0 GitHub Real-Commit Architecture Review | PASS（制作人本轮明确确认，对 70754649 的真实代码审阅） |
| Scene / Prefab / 制作人资源 | Scene / Prefab unchanged；受保护资源哈希不变，未进入 commit |
| 原实际存档 | 测试后按字节恢复，哈希一致 |

**首次 Full PlayMode 为 92/93**：既有随机地图测试
`VillageV4ProgressionClosurePlayTests.Progression_InsufficientSupply_ShowsVisibleReason_AndRetreatReturnsToTown`
未取得所需 expensive successor；地图生成器使用既有随机源，该用例不是固定地图。未修改代码，单项复跑 **1/1 PASS**，随后完整复跑 **93/93 PASS**。保留第一次失败事实；不误记为 WP-AL-2 缺陷，不由它新增 Runtime 返工。

## 7. Current / Target 与后续边界

Production Activity Providers / 正式活动内容、Utility AI、Personality、Relationship 参与选择、Plausibility Cut、Deterministic Weighted Candidate Ordering、Proposal / Conflict Arbitration、Decision Trace、自动选择 / 自动执行、Activity → Social Fact、Relationship Feedback、Recognition / Event Integration、Life Episode Runtime、Navigation / Animation / World Presentation、RepeatStreak / 最近 N 次规则仍为 Target / Deferred；WP-AL-3/4/5 未启动。

1. WP-AL-3 stale-window guard：MissingPersonSource 保留旧 Window；未来自动 Selection 必须核对当前 Village Day / 有效 Daily Phase，不能只凭非空 Window 消费。
2. Target vs Exclusive Participant：当前非空 Target 是参与者并 Claim；未来 semantic-only Target 出现再扩展，当前不返工。
3. WP-AL-3 自动 Selection 进入 Day Tick 前须设计 fail-closed execution boundary，避免外部 callback 失败造成 PartialDayFailure。
4. 首个真实 Social Producer 前裁决 Production Relationship Interpreter ownership；当前由调用方注入 Interpreter 是 WP-AL-1 允许接缝。
5. Village / Event 全世界统一 Load transaction 是既有债务，Social / Activity 自身隔离不等于其它 Host 已修复。

登记见 [问题清单](../问题清单.md#wp-al-2-boundaries)。无本轮必须先裁决才能完成的真正产品 / 架构冲突；无 Stop Condition，无本次 Runtime 返工。Current 文档不改变 GameDesign / GameSpec / Freeze / Save Schema 或模块 Ownership，不启动 WP-AL-3。

## 8. 本次文档 Closure Staging Manifest

仅以下 **9 个 Markdown** 进入本次文档提交：

- `Docs/Architecture/技术架构总览.md`
- `Docs/Architecture/核心对象与术语映射.md`
- `Docs/DevelopmentSpecs/README.md`
- `Docs/开发验收交接.md`
- `Docs/问题清单.md`
- `Docs/项目状态快照.md`
- `Docs/文档地图.md`
- `Docs/项目日志.md`
- `Docs/DevelopmentSpecs/2026-10-04_RRPG_WP-AL-2_Activity-Lifecycle-Daily-Integration_Closeout.md`

本次不重复提交 Runtime / Tests / meta / Spec，不修改 Scene / Prefab / Config / 美术。制作人 3 个 tracked dirty 资源、12 个图片 / meta、两个旧 Review .diff 保留，不暂存。本次文档相对链接220处、anchor11处检查0错误；git diff --check与git diff --cached --check exit0；Current / Target自检PASS。提交前81个受保护文件（Runtime commit全范围、制作人资源、Scene / Prefab）哈希无变化，暂存区恰好9份上述Markdown。未用旧Runtime PASS替代文档检查。

本轮发布目标只有 origin review/wp-al-2；main 保持 0c8722e，未授权 merge / push main。最终文档 SHA 属 Git History，不以生成时 HEAD 冒充本次文档 Commit。
