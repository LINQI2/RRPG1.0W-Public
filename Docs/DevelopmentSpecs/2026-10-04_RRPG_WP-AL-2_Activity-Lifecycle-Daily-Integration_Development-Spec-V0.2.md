# RRPG｜WP-AL-2 Activity Lifecycle & Daily Integration（活动生命周期与每日接入）｜Development Spec V0.2

> 日期：2026-10-04
> 项目：RRPG
> Work Package：WP-AL-2 Activity Lifecycle & Daily Integration（活动生命周期与每日接入）
> 唯一实现基线：`main@0c8722ecfbe353d0b2777360a9059516459138cb`
> 状态：**Architecture Review PASS / Ready for Authority Landing & Explicit Runtime Authorization（架构审阅通过 / 可进入落仓与明确 Runtime 授权）**
> 重要：**本文件不自动授权 Runtime 开发。**
> 输入 Authority：
> - `GameSpec/Village/Village-GameSpec-V0.1.md` 第 12 章
> - `Docs/DevelopmentSpecs/2026-10-04_RRPG_Autonomous-Life_Technical-Architecture-V0.1.md`
> - 已完成并发布的 WP-AL-1 Social Foundation
>
> 原则：**Production Architecture + Prototype Content（正式生产架构 + 原型测试内容）**

---

# 1. 本 WP 的目标

WP-AL-2 不负责让 NPC “聪明地选活动”。

它只建立：

> **人物什么时候拥有自主活动机会、一次 Activity（活动）怎样从 Candidate（候选）进入 Commitment（承诺）并完成生命周期、多人参与怎样互斥占用、每日参与状态怎样跨 Save 连续，以及正常 Day Tick / Jump Month / Expedition Return Catch-up 怎样共用同一套 Daily Activity Phase（每日活动阶段）。**

正式链：

```text
VillageTime.TickDay
↓
既有 Day-start Temporary Occupancy Snapshot
（日初临时占用快照）
↓
既有 Project / Assignment / Resource / Formation
（项目 / 长期责任 / 资源 / 职业形成）
↓
Autonomous Activity Opportunity
（自主活动机会）
↓
Activity Window Snapshot
（活动窗口快照）
↓
Future Selection / Arbitration
（后续 WP-AL-3）
↓
Activity Commit + Participant Claims
（活动承诺 + 参与占用）
↓
Explicit Activity Lifecycle
（显式活动生命周期）
↓
Completed / Failed / Interrupted
（完成 / 失败 / 中断）
```

WP-AL-2 **不实现**：

- Utility AI（效用型 AI）
- Weighted Candidate Ordering（加权候选排序）
- Multi-Person Proposal Arbitration（多人提案仲裁）
- Personality（人格）
- Relationship 对选择的影响
- Social Fact / Relationship Feedback 接线
- Recognition / Event
- Life Episode
- Navigation / Animation / Village World Presentation

---

# 2. 当前工程事实

基于 `main@0c8722e`：

## 2.1 VillageTime（村庄时间）

唯一 Village 时间 Authority。

所有合法跨日路径最终都使用：

```text
VillageTime.TickDay()
```

包括：

- 实时 `Update(deltaSeconds)`
- `JumpToNextMonth()`
- Expedition Return Catch-up 的逐日补算

因此 WP-AL-2 **禁止建立第二套 Activity Clock / Scheduler Time**。

## 2.2 `VillageRuntimeBridge.HandleDayTick`

当前真实顺序：

```text
VillageState
↓
capture occupiedAtStartOfDay
↓
Projects.OnDayTick
↓
Project completion → World / Capability
↓
Assignments.OnDayTick(occupiedAtStartOfDay)
↓
Resource Settlement
↓
Formation Resolver
```

其中：

> `occupiedAtStartOfDay（当日开始占用快照）`

已经保证：

- Project 当日完成；
- 当天仍视为整日被 Project 占用；
- Assignment 次日才恢复。

WP-AL-2 必须复用**同一份快照**决定该日 Autonomous Activity Hard Gate。

## 2.3 Return Catch-up

Return Catch-up 唯一逐日推进器已经调用：

```text
Time.TickDay()
```

因此 Activity Daily Phase 只要接入现有 `HandleDayTick`，就天然进入：

- live day tick
- Jump Month
- Return Catch-up

**禁止另写 Catch-up 专用 Activity 模拟循环。**

## 2.4 WP-AL-1 Social Foundation

已经实现独立 `Social.Runtime`，但 WP-AL-2 **不依赖它**。

Activity Runtime 与 Social Runtime 在 WP-AL-2 保持解耦。

二者第一次正式接线属于 WP-AL-4。

---

# 3. 核心架构裁决

## 3.1 新增独立纯域：`AutonomousActivity.Runtime（自主活动运行域）`

建议路径：

```text
UnityProject/RRPG1.0game/Assets/Scripts/AutonomousLife/Activity/Runtime/
```

asmdef：

```text
AutonomousActivity.Runtime
```

要求：

```text
references = []
noEngineReferences = true
```

不得依赖：

- `Village.Runtime`
- `Social.Runtime`
- `Event.Runtime`
- `Person.Runtime`
- Battle
- UI
- UnityEngine

职责：

- Activity Candidate 数据契约
- Activity Provider 契约
- Daily Window / Eligibility Snapshot
- Participant Claims
- Activity Instance
- Lifecycle State Machine
- Daily Participation State
- Recent Initiated Activity State
- ActivityWorld Save / Restore

不负责：

- 谁应该被判定在 Village；
- VillageTime；
- Project / Assignment；
- Personality；
- Relationship；
- Utility；
- Event；
- 表现。

## 3.2 Assembly-CSharp Host

建议新增：

```text
UnityProject/RRPG1.0game/Assets/Scripts/ActivityHost/ActivityRuntimeBridge.cs
```

职责：

- 持有当前 `ActivityWorldState`
- 持有当前 transient Daily Window / Claims / Activity Instances
- Build / Restore / Reset
- 向 VillageRuntimeBridge 提供 Daily Phase 入口
- 向未来 WP-AL-3 暴露只读 Window + 正式 Commit / Lifecycle API

不允许：

> 把 Utility / Selection / Social Fact 逻辑写入 Bridge。

---

# 4. Daily Activity Phase（每日活动阶段）

## 4.1 插入位置

WP-AL-2 在现有 `VillageRuntimeBridge.HandleDayTick` 的**责任与日结链完成之后**进入 Activity Phase：

```text
VillageState
↓
Day-start Occupancy Snapshot
↓
Projects
↓
Assignments
↓
Resources
↓
Formation
↓
Autonomous Activity Opportunity
```

原因：

> Primary Assignment 表示当天长期 Responsibility（责任），Autonomous Activity 是责任之外的生活机会。

不得把 Activity Phase 插在 Project / Assignment 之前，否则会改变当前冻结的 Opportunity Cost（机会成本）语义。

## 4.2 必须复用同一 Day-start Occupancy Snapshot

当天 Activity Hard Gate 使用：

```text
occupiedAtStartOfDay
```

而不是 Project Tick 之后重新查询一次。

因此：

```text
Person 今天开始时在 Active Project
↓
Project 今天完成
↓
当天仍无 Autonomous Activity Opportunity
↓
次日才恢复
```

必须与 Assignment 当前语义一致。

---

# 5. Actor Eligibility 与 Participant Availability 分离

架构必须显式保留两个概念：

### Actor Eligibility（发起资格）

回答：

> 这个 Person 当前是否可以主动发起一个 Major Activity？

### Participant Availability（参与可用性）

回答：

> 这个 Person 当前是否可以作为另一个 Activity 的互斥参与者？

V0.1 当前 Village Adapter 可以暂时把二者构造成同一集合：

```text
InVillage
AND
not in Day-start Temporary Occupancy Snapshot
```

但 Runtime API **不得把二者合成一个永远相同的字段**。

原因：

未来：

- Injured（受伤）
- recovery context（恢复情境）
- prisoner / child / special role
- Target Acceptance

可能导致：

```text
ActorEligible = false
ParticipantAvailable = true
```

WP-AL-2 不实现上述内容，只保留正式扩展位。

---

# 6. Village Adapter 如何构建 Daily Window

VillageRuntimeBridge 在 Activity Phase：

1. 读取当前 `SimulationPersonSource`
2. 复用既有 Person / Availability 逻辑得到当前 InVillage Person
3. 过滤：
   - Expedition
   - Unavailable / dead
   - `occupiedAtStartOfDay`
4. **不**因 Primary Assignment 过滤
5. **不**因 PersonRest Recovery Window 过滤
6. 构造：
   - ActorEligiblePersonIds
   - ParticipantAvailablePersonIds
7. 用当前 `VillageTime.Year / Month / Day` 打开唯一 Daily Activity Window

如果 `SimulationPersonSource == null`：

> **不得把“缺失人物真相源”伪装成“今天没人”。** Village Adapter 返回 `MissingPersonSource（缺失人物源）` / 等价明确诊断，不打开或替换 Daily Window，也不抛出外部异常。

如果 `SimulationPersonSource` 已绑定但合法人物集合为空：

> 可以打开一个真实的空 Window。

正式产品链与 Return Catch-up 的集成测试必须证明需要 Activity Daily Phase 的路径已经绑定同一 PlayerCollection；WP-AL-2 不建立第二份人物列表作为兜底。

---

# 7. `ActivityDayStamp（活动日标识）`

纯域最小字段：

```text
Year
Month
Day
```

用途：

- Daily Window Identity
- Daily Participation
- Recent Activity
- Save / Load continuity

Activity.Runtime 不自行推进日期，也不计算 VillageTime。

日期合法性由 Host / Producer 保证；纯域只做基础结构验证。

---

# 8. Activity Provider Contract（活动提供者契约）

WP-AL-2 只建立契约，不建立正式生产 Provider 内容。

Provider 可以来自：

- Place（场所）
- Person（人物）
- Context（情境）

最小候选接口语义：

```text
IActivityProvider
→ Collect / Produce ActivityCandidate
```

Provider 负责：

- ActivityType（活动类型）
- ProviderId（提供者身份）
- PlaceId（可选）
- TargetPersonId（可选）
- Exclusive Participants（互斥参与者要求）
- 基础结构可用条件

Provider 不负责：

- Personality
- Relationship
- Utility
- Weighted Random
- 最终选择

---

# 9. `ActivityCandidate（活动候选）`

建议最小字段：

```text
CandidateKey（候选稳定键）
ActivityType（活动类型）
ProviderId（提供者ID）
ActorPersonId（发起人物）
TargetPersonId（可选目标人物）
PlaceId（可选场所）
ExclusiveParticipantPersonIds[]（除 Actor 外的互斥参与者）
```

要求：

- immutable（不可变）
- defensive copy（防御性复制）
- ActorPersonId 非空
- ActivityType 非空
- CandidateKey 非空
- Exclusive Participant 不允许重复
- Actor 不得再次出现在 ExclusiveParticipantPersonIds
- Target 如果属于互斥活动，应出现在 ExclusiveParticipantPersonIds
- Candidate 构造 / 查询无副作用

Candidate **不持久化**。

---

# 10. Stable Activity Instance Identity（稳定活动实例身份）

成功 Commit 时必须由调用方提供：

```text
ActivityInstanceId（活动实例ID）
```

WP-AL-2 不允许：

- `Guid.NewGuid()`
- timestamp
- `string.GetHashCode()`
- Unity random

ActivityInstanceId 必须：

> 由未来 Selection / Orchestration 层从稳定 Decision Identity 派生。

WP-AL-2 测试使用固定测试字符串。

---

# 11. Activity Window（活动窗口）

纯域概念：

```text
ActivityDailyWindow
```

至少持有：

- DayStamp
- ActorEligiblePersonIds
- ParticipantAvailablePersonIds

构造时必须：

- 去除空 PersonId；
- 去重；
- 使用 ordinal（序号）稳定排序；
- 对外返回 immutable snapshot / read-only view（不可变快照 / 只读视图）。

规则：

### 同一天重复 Open

只有在 DayStamp + ActorEligible 集合 + ParticipantAvailable 集合完全一致时才是幂等 no-op。

如果同一天再次 Open 但 Snapshot 不一致：

> 返回 `WindowConflict（窗口冲突）` / 等价明确结果并 Fail Closed（失败关闭）；不得静默刷新、合并或覆盖当前 Window。

这样 Window 一旦建立，就代表该 Decision Round（决策轮）的稳定世界快照。

### 打开新一天

必须：

- 结束上一天 transient window；
- 清除旧 Claims；
- 清除旧 in-flight Activity Instances；
- 未 Completed 的旧 Activity 不产生长期副作用；
- 不把跨日执行作为 V0.1 能力。

**V0.1 Major Activity 不跨 Day Window。**

这是 Development-time implementation boundary，不升级为长期产品规则。

---

# 12. Daily Participation（每日主要活动参与）

V0.1：

> 每个 Person 每个 Daily Activity Window 最多参与一个 Major Activity。

所有 Exclusive Participants 都受约束。

例如：

```text
Talk
Actor = ZhangSan
Target = LiSi
```

Completed 后：

```text
ZhangSan consumed today
LiSi consumed today
```

因此当天都不能再 Completed 第二个 Major Activity。

---

# 13. Claim（活动参与占用）与 Temporary Occupancy 分离

Activity Claim 回答：

> 当前 Activity Window 中，这个人是否已经被一个已 Commit 但尚未结束的 Activity 独占？

它**不是** Village Temporary Occupancy。

不得：

- 写入 `_expeditionPersonIds`
- 写入 `_unavailablePersonIds`
- 写入 Active Project
- 暂停 / 删除 Primary Assignment
- 改 `VillageAvailability`

Claim 生命周期：

```text
Commit
→ acquire actor + exclusive participant claims

Completed / Failed / Interrupted
→ release claims
```

---

# 14. Commit Contract（活动承诺契约）

建议入口：

```text
TryCommitActivity(
    ActivityInstanceId,
    ActivityCandidate,
    CurrentWindow)
```

Commit 必须验证：

- Window 已打开
- Actor 当前 ActorEligible
- 所有 Exclusive Participants 当前 ParticipantAvailable
- Actor / Participant 未被其它 Active Activity Claim
- Actor / Participant 当天未完成另一个 Major Activity
- ActivityInstanceId 未被当前 Window 的另一 Activity 占用
- Candidate 结构合法

全部验证完成后才：

```text
create ActivityInstance
+
acquire all claims
```

不允许半 Claim。

---

# 15. Explicit Activity FSM（显式活动有限状态机）

WP-AL-2 采用最小 Simulation Lifecycle：

```text
Committed
↓
Executing
↓
Completed
```

失败出口：

```text
Failed
Interrupted
```

正式限制：

- 不使用多个 bool 表示状态；
- terminal state（终态）不可再次改变；
- duplicate Complete 不得重复消费 Daily Participation；
- Failed / Interrupted 不消费成功 Daily Participation；
- Failed / Interrupted 不产生成功 Result；
- Commit 后不重新执行选择。

### 为什么 V0.1 不包含 `Navigating`

`Navigating / Animation / Presentation` 属于未来 Live Driver / WP-AL-5。

Simulation FSM 不把表现子状态变成世界规则。

---

# 16. Lifecycle API（生命周期 API）

纯域建议提供：

```text
TryBeginExecution(activityInstanceId)
TryComplete(activityInstanceId)
TryFail(activityInstanceId)
TryInterrupt(activityInstanceId)
```

调用者：

- Headless Driver 可以立即 Begin → Complete
- Future Live Driver 可以异步推进后再 Complete

两个 Driver 最终都调用同一 Lifecycle Authority。

WP-AL-2 不实现 Navigation。

---

# 17. Completed Activity Result（完成活动结果）

`TryComplete` 成功时返回不可变：

```text
ActivityCompletionSnapshot
```

最少包含：

- ActivityInstanceId
- ActivityType
- ActorPersonId
- TargetPersonId
- PlaceId
- Participant PersonIds
- DayStamp

用途：

> 给未来 WP-AL-4 生成 Semantic Relational Fact。

WP-AL-2 **不生成 Fact**。

Completion Snapshot 不成为新长期历史 Authority。

---

# 18. Person Activity State（人物活动派生状态）

WP-AL-2 不建立 Activity History Ledger。

只建立每 Person 一个薄 Derived Gameplay State（派生玩法状态）：

```text
LastMajorParticipationDay
（最近完成主要活动的参与日期）

LastInitiatedActivityDay
（最近主动发起完成活动的日期）

LastInitiatedActivityType
（最近主动发起完成活动的类型）
```

### Participation

所有 Actor / Target / Exclusive Participant 都更新 `LastMajorParticipationDay`。

用途：

> 防同一日参与多个 Major Activity。

WP-AL-2 **不保存 `LastMajorParticipationActivityType`**，因为“被动参与过什么”当前没有已冻结 Gameplay Consumer（玩法消费者），不预建历史字段。

### Initiated

只更新 Actor：

```text
LastInitiatedActivityDay
LastInitiatedActivityType
```

用途：

> 给 WP-AL-3 提供最小 Recent Repetition（近期重复）输入。

WP-AL-2 **不保存 `InitiatedRepeatStreak（连续重复次数）`**。连续的定义（连续日 / 连续主动行为 / 最近 N 次）尚未冻结，应由 WP-AL-3 在正式 Repeat Rule（重复规则）确定后决定是否派生或新增缓存字段。

这样：

> 被别人拉去 Talk

不会覆盖这个 Person 自己最近主动选择了什么。

---

# 19. Completed / Failed / Interrupted 对 Person Activity State 的影响

### Completed

所有参与者：

```text
LastMajorParticipationDay = Current Day
```

Actor：

```text
LastInitiatedActivityType
LastInitiatedActivityDay
```

### Failed / Interrupted

- release Claims
- 不写 Completed Participation
- 不更新 Initiated Repeat State

是否允许同 Window 再规划：

> WP-AL-2 不裁决。

Runtime 只让该 Person 重新变为 unclaimed / not-consumed。

未来 WP-AL-3 Orchestrator 决定是否重选。

---

# 20. Persistence（持久化）

## 20.1 新增 top-level `ActivityWorld`

建议：

```text
SaveData
├ Player
├ Village
├ EventWorld
├ SocialWorld
└ ActivityWorld
```

原因：

- Activity Runtime 是独立纯域；
- Recent / Daily Participation 是 world gameplay state；
- 不把它硬塞进 Person / Village / Social Authority。

## 20.2 `ActivityWorldSaveData`

只保存：

```text
PersonActivityStates[]
```

每条：

```text
PersonId

LastParticipationYear
LastParticipationMonth
LastParticipationDay

LastInitiatedYear
LastInitiatedMonth
LastInitiatedDay
LastInitiatedActivityType
```

不保存：

- Current Window
- Candidate
- Provider
- Claim
- Activity Instance
- Executing state
- Completion Snapshot

因此：

> Save / Load 自动取消所有 in-flight Activity，但不会产生成功副作用。

---

# 21. Save / Load 语义

## Save

所有正式 Save 入口必须携带：

```text
ActivityWorld
```

## Load

只有：

- JSON / Version 合法；
- Player restore 成功；

之后才 Replacement Restore ActivityWorld。

失败 Load：

> 保留调用前 ActivityWorld。

## Restore

- Clear / Replace
- 不恢复 Claims
- 不恢复 in-flight Activity
- 不恢复 Window
- 不重新执行 Activity
- **Load 本身不重新打开当天 Daily Window，也不重放已经过去的 Activity Phase**
- 旧档缺 ActivityWorld → empty

如果测试或未来调度器在 Load 后显式尝试为同一天建立一个新 Window：

> 已持久化的 `LastMajorParticipationDay` 仍必须阻止已完成过 Major Activity 的人物再次 Commit。

“当天曾经 No Major Activity（无主要活动）是否需要额外 exactly-once 标记”留给 WP-AL-3 的 Decision Round 完整语义裁决；WP-AL-2 不通过隐式 Load replay 解决。

## NewGame

显式：

```text
ActivityRuntimeBridge.ResetForNewGame()
```

## SaveVersion

继续：

```text
1
```

如实现发现 additive nullable block 不安全：

> Stop Condition，禁止擅自升级。

---

# 22. 同日 Save / Load 防重复

必须验证：

```text
Person A 今天已 Completed Major Activity
↓
Save
↓
Load same day
↓
Activity Window 再打开
↓
A 仍然被视为今天已消费 Major Activity Opportunity
```

这是 PersonActivityState 持久化的核心理由。

注意：正式 Load 不自动“重新打开今天的活动机会”；上图中的“Activity Window 再打开”仅作为防御性契约测试，用于证明即使调用方错误/显式尝试重新建立同日 Window，已完成参与者也不会获得第二次完成额度。

---

# 23. VillageTime Integration（VillageTime 接入）

唯一接线位置：

```text
VillageRuntimeBridge.HandleDayTick
```

使用现有：

```text
occupiedAtStartOfDay
```

在：

```text
RunFormationDayEnd(...)
```

之后进入：

```text
OpenAutonomousActivityOpportunity(...)
```

不得：

- 订阅第二个 VillageTime
- 新建 MonoBehaviour Clock
- 在 Town UI Update 中单独跑 Activity day
- 给 Return Catch-up 写特例

---

# 24. Live / Jump / Catch-up 一致性

因为三者都最终进入：

```text
Time.TickDay()
→ HandleDayTick
```

所以 WP-AL-2 必须证明：

### Direct Day Tick

Activity Window 打开一次。

### JumpToNextMonth

每个完整 Day Tick 都经过 Activity Phase。

### Expedition Return Catch-up

每个补算 Day 都经过相同 Activity Phase。

Catch-up 中：

- Expedition Person 继续被 Day-start Occupancy 阻断；
- 留在 Village 的合资格 Person 正常拥有 Activity Opportunity。

---

# 25. Primary Assignment / Project / Rest 精确边界

必须验证：

### 有 Primary Assignment

```text
no Temporary Occupancy
→ Actor Eligible
```

### Active Project

```text
occupied at day start
→ not Actor Eligible
```

### Project 当天完成

```text
still blocked today
→ eligible tomorrow
```

### Expedition

```text
not eligible
```

### Unavailable / Dead

```text
not eligible
```

### PersonRest Recovery Window

**不作为 Autonomous Activity Hard Gate。**

现有 Recovery Window 只保留其已冻结职责：

> 阻止加入新的 Active Project。

它不是 `Rest Activity`，也不是全天 Occupancy。

---

# 26. Activity Provider 在 WP-AL-2 的范围

只实现 Contract + Test Provider。

不实现：

- Tavern Provider
- Home Provider
- Person Talk Provider
- Injured Context Provider

这些最小 Prototype Content 进入 WP-AL-3。

WP-AL-2 测试可使用：

```text
test_rest
test_talk
```

但这些 key 不进入正式配置 Authority。

---

# 27. Headless / Live Contract

WP-AL-2 不建立两套规则。

Activity Runtime 的正式 Lifecycle API 与 Window / Claim / Participation 状态就是唯一 Simulation Contract。

未来：

```text
Headless:
Commit → Begin → Complete

Live:
Commit → Begin → presentation/navigation → Complete
```

两者最终调用同一个 ActivityLifecycleService。

WP-AL-2 只实现 / 测试 Headless-style immediate lifecycle。

Live Presentation 后置 WP-AL-5。

---

# 28. No External Callback in Day Tick（本 WP 的日结安全边界）

WP-AL-2 的 `HandleDayTick` 接线只允许：

> 打开 / 更新 Activity Daily Window。

不允许在 WP-AL-2 中：

- 触发 Utility selection callback
- 调用外部 Provider 扫描
- 调用 Event
- 调用 Social Interpreter
- 自动执行 Activity Content

原因：

当前 Village Day Tick / Return Catch-up 不是数据库事务。

WP-AL-2 不应增加“责任结算已写完后，外部 callback 抛异常导致 PartialDayFailure”的新风险。

WP-AL-3 接自动 Selection 前必须单独设计 fail-closed execution boundary。

---

# 29. Public API Boundary

未来 WP-AL-3 可以使用：

## Read

```text
QueryCurrentWindow()
QueryPersonActivityState(personId)
QueryActivityInstance(instanceId)
QueryClaim(personId)
```

全部返回 immutable snapshot / read-only view。

`QueryPersonActivityState` 缺记录时返回 Neutral / Empty Snapshot（中性 / 空快照），**读取不得创建持久 PersonActivityState**。

## Write

只能经过：

```text
OpenDailyWindow(...)
TryCommitActivity(...)
TryBeginExecution(...)
TryComplete(...)
TryFail(...)
TryInterrupt(...)
Restore / Reset
```

禁止：

- 直接改 Claims
- 直接改 PersonActivityState
- 直接 Set Activity State enum
- 直接写 Save DTO

---

# 30. 建议实现文件

## 新增 Pure Runtime

```text
Assets/Scripts/AutonomousLife/Activity/Runtime/AutonomousActivity.Runtime.asmdef
Assets/Scripts/AutonomousLife/Activity/Runtime/ActivityDayStamp.cs
Assets/Scripts/AutonomousLife/Activity/Runtime/ActivityCandidate.cs
Assets/Scripts/AutonomousLife/Activity/Runtime/IActivityProvider.cs
Assets/Scripts/AutonomousLife/Activity/Runtime/ActivityDailyWindow.cs
Assets/Scripts/AutonomousLife/Activity/Runtime/ActivityLifecycleState.cs
Assets/Scripts/AutonomousLife/Activity/Runtime/ActivityInstance.cs
Assets/Scripts/AutonomousLife/Activity/Runtime/ActivityCompletionSnapshot.cs
Assets/Scripts/AutonomousLife/Activity/Runtime/ActivityClaimRegistry.cs
Assets/Scripts/AutonomousLife/Activity/Runtime/PersonActivityState.cs
Assets/Scripts/AutonomousLife/Activity/Runtime/ActivityWorldState.cs
Assets/Scripts/AutonomousLife/Activity/Runtime/ActivityLifecycleService.cs
Assets/Scripts/AutonomousLife/Activity/Runtime/ActivityWorldSaveData.cs
Assets/Scripts/AutonomousLife/Activity/Runtime/ActivityWorldPersistence.cs
```

执行时可以合理合并小型 immutable records，但不得形成巨大 `ActivityManager.cs`。

## 新增 Host

```text
Assets/Scripts/ActivityHost/ActivityRuntimeBridge.cs
```

## 修改

```text
Assets/Scripts/Village/VillageRuntimeBridge.cs
Assets/Scripts/Save/SaveData.cs
Assets/Scripts/Save/SaveSystem.cs
```

## 新增 Tests

建议单独测试子程序集：

```text
Assets/Tests/EditMode/AutonomousLife/ActivityLifecycle/
```

避免修改 WP-AL-1 已有 `SocialFoundation.Tests.asmdef` 的职责。

---

# 31. 禁止修改

WP-AL-2 不允许修改：

- `Social.Runtime` 实现
- `SocialRuntimeBridge`
- `VillageSocialFactLedger`
- `WorldEventHistory`
- `PersonLifeHistory`
- Event Runtime / Event Host
- CharacterInstance / HeroInstance
- Battle
- Expedition 返乡顺序
- Scene
- Prefab
- UI
- Navigation
- Static Config
- GameDesign
- GameSpec

如果必须越界：

> Stop Condition，先报告影响面。

---

# 32. Test Matrix — Gate A：Pure Activity Lifecycle

至少验证：

- A1 Window Open
- A2 Same-day Open Idempotent（相同 Snapshot → no-op）
- A3 Same-day Different Snapshot → WindowConflict / no mutation
- A4 New-day Window 清旧 transient state
- A5 Window Person Sets stable ordinal + immutable
- A6 Candidate Immutable
- A7 Actor vs Participant Surface 独立
- A8 Commit Success
- A9 Claim Conflict
- A10 No Half Claim
- A11 Stable Instance Identity
- A12 FSM Legal Transition
- A13 Illegal Transition Rejected
- A14 Failed Releases Claims / 不消费 participation
- A15 Interrupted Releases Claims / 不消费 participation
- A16 Complete Consumes All Participants
- A17 Duplicate Complete No-op
- A18 Actor Initiated State 与 Target Participation 分离

---

# 33. Gate B：Persistence

至少验证：

- B1 Round Trip
- B2 Restore Replacement
- B3 Restore Null
- B4 In-flight Not Persisted
- B5 Load Clears Runtime Claims
- B6 Stable Ordering
- B7 Invalid Data Filtering
- B8 Old Save missing ActivityWorld → empty
- B9 Read Missing PersonActivityState = Empty + zero side effect
- B10 Restore / Load does not reopen or replay current-day Activity Window

---

# 34. Gate C：Village Daily Integration

至少验证：

- C1 Primary Assignment Does Not Block
- C2 Expedition Blocks
- C3 Unavailable Blocks
- C4 Active Project Blocks
- C5 Project Completion Day Still Blocks
- C6 Next Day Restores Opportunity
- C7 Recovery Window Is Not Hard Gate
- C8 Direct Tick Uses Daily Phase
- C9 Jump Month Uses Same Phase
- C10 Return Catch-up Uses Same Phase
- C11 Away Expedition Members Stay Blocked During Catch-up
- C12 Home Persons Still Eligible During Catch-up
- C13 Missing SimulationPersonSource Fails Closed
- C14 Bound-but-empty PlayerCollection Opens Real Empty Window

---

# 35. Gate D：SaveSystem Integration

至少验证：

- D1 Save / Load Round Trip
- D2 Same-day Participation Survives Load
- D3 Legacy Save Without ActivityWorld
- D4 NewGame Clears ActivityWorld
- D5 Load World B Replaces World A
- D6 Failed Load Retains Current ActivityWorld
- D7 SaveVersion Remains One
- D8 Load itself does not replay same-day Activity Phase

---

# 36. Gate E：No Cross-domain Side Effects

确认：

- Activity Commit / Complete 不生成 Relational Fact；
- 不修改 Relationship；
- 不创建 Event；
- 不修改 Person / Assignment / Project；
- 不创建 Scene / GameObject；
- 不运行 Utility；
- 不调用 RNG。

---

# 37. Full Regression

实施完成必须：

- Compile = 0 error
- WP-AL-2 targeted EditMode PASS
- Full EditMode 0 regression
- Full PlayMode 0 regression
- `git diff --check`
- Scene / Prefab unchanged
- 制作人已有工作区增量未修改

当前已发布基线最近验证：

```text
EditMode 1112 / 1112
PlayMode 93 / 93
```

新增测试后总数应增加。

---

# 38. Stop Conditions

遇到以下任一情况必须停止：

- 需要新建第二套 VillageTime
- 需要改变 Return Catch-up 的既有顺序
- 需要把 Activity Claim 写进 Temporary Occupancy Authority
- 需要让 `AutonomousActivity.Runtime` 引用 Village / Social / Event / Person
- 需要修改 Social Runtime
- 需要提升 SaveVersion
- 需要正式冻结 Utility / Personality / Affinity 数值
- 需要 Scene / Prefab / Navigation
- 需要在 WP-AL-2 自动生成 Social Fact / Event
- 需要保存 in-flight Activity 才能满足本 WP 验收
- 无法在同一 `HandleDayTick` 路径覆盖 direct / Jump / Catch-up
- 需要在 `SimulationPersonSource == null` 时猜测 / 复制第二份 Person 列表作为 Activity 输入
- 需要为 Repeat Penalty 提前冻结 streak / recent-window 规则

---

# 39. 当前不解决的已知问题

## A. Production Relationship Interpreter ownership

来自 WP-AL-1，WP-AL-2 不处理。

## B. Village / Event 世界级统一 Save transaction debt

既有遗留风险。

WP-AL-2 只保证新增 ActivityWorld 自身：

- failed Load 不被破坏
- NewGame reset
- replacement restore

不扩大到重构所有 World Host。

## C. Selection Fail Boundary

WP-AL-2 不在 Day Tick 中调用外部 Selection。

WP-AL-3 开始自动选择前必须明确：

> Selection / Provider / Arbitration 异常是否可以影响 Village Day Tick。

---

# 40. Acceptance（验收）

WP-AL-2 完成必须满足：

1. `AutonomousActivity.Runtime` 为纯域；
2. Activity 不建立第二套 Time / Occupancy；
3. Activity Phase 只接现有 `HandleDayTick`；
4. Project completion-day occupancy 与 Assignment 语义一致；
5. 有 Assignment 的人物仍可自主；
6. Actor Eligibility / Participant Availability 概念分离；
7. Claim 与 Temporary Occupancy 分离；
8. Activity FSM 显式且单一；
9. Failed / Interrupted 不产生成功长期状态；
10. Completed 才消费 Daily Participation；
11. Actor initiated recency 与 target participation 分离；
12. in-flight Activity 不保存；
13. same-day participation Save / Load 连续；
14. direct / Jump / Catch-up 共用同一 Daily Phase；
15. WP-AL-2 不接 Social / Event / Utility；
16. SaveVersion 保持 1；
17. 同日 Window snapshot 不得被静默刷新；冲突必须 fail closed；
18. Person Activity State 读取无副作用；
19. 缺失 SimulationPersonSource 不得伪装为空世界；
20. Load 不自动重放当前日 Activity Phase；
21. Repeat Streak / 最近 N 次规则仍未冻结；
22. 全量 EditMode / PlayMode 0 regression。

---

# 41. 下一 Work Package

WP-AL-2 Closeout 后才进入：

> **WP-AL-3 Autonomous Selection & Arbitration（自主选择与仲裁）**

WP-AL-3 才增加：

- 实际 Test Activity Providers
- Personality test axes
- Relationship query input
- Eligibility evaluation
- Explainable Utility
- Deterministic Weighted Candidate Ordering
- Plausibility Cut
- Proposal Lists
- Conflict Arbitration
- Decision Trace
- 4 Person / 3 Place / 5 Activity vertical slice test content

---

# 42. 一句话开发定义

> **WP-AL-2 把 Autonomous Life 接进现有唯一 Village Day Tick：复用日初 Temporary Occupancy Snapshot 生成每日 Activity Opportunity，用独立纯 Activity Domain 管理 Candidate / Claim / Lifecycle / Daily Participation，并保证 direct Tick、Jump Month、Return Catch-up、Save / Load / NewGame 全部共享同一正式生命周期；但不提前实现 Utility、选择算法、Social Feedback 或世界表现。**
