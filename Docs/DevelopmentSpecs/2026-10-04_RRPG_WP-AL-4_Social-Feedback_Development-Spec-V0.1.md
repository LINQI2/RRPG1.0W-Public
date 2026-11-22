# RRPG｜WP-AL-4 Social Feedback & Historical Reciprocity（社会反馈与历史互惠）｜Development Spec V0.1

> 日期：2026-10-04
> 实现基线：`main@7fcffe5b28be9156a45b95c3981b53ec8b9cac9b`
> 输入：WP-AL-4 Social Feedback Design Checkpoint V0.1 Final
> 状态：**Ready for Authority Landing；Landing 通过后可授权 Runtime Implementation**
> 原则：Production Architecture + Prototype Content。

---

# 1. Work Package Goal

实现：

```text
Completed Activity
↓
Prepared Social Fact Projection
↓
Durable Social Feedback Outbox
↓
Social-owned Production Relationship Interpreter
↓
Canonical Relational Fact + Directed Relationship Mutation
↓
Save / Load
↓
pre-next-decision settlement barrier
↓
WP-AL-3 future behavior changes
```

不实现 Recognition / Event / LifeEpisode Runtime。

---

# 2. Existing Authorities Must Be Reused

必须复用：
- `PersonId`
- WP-AL-1 `Social.Runtime`
- `RelationalFactRecord`
- `RelationalFactLedger`
- `DirectedRelationshipRegistry`
- `IRelationshipInterpreter`
- `SocialCommitService`
- `SocialRuntimeBridge`
- WP-AL-2 Activity Runtime / `ActivityCompletionSnapshot`
- WP-AL-3 Selection / Decision Snapshot
- `VillageTime`
- `VillageRuntimeBridge.HandleDayTick`
- 现有 XLSX → Compiler → JSON → Builder → StaticConfigRegistry
- `SaveSystem`

禁止建立竞争性 Fact / Relationship / Activity / Clock Authority。

---

# 3. New Integration Pure Domain

建议新增：

```text
Assets/Scripts/AutonomousLife/SocialFeedback/Runtime/
```

asmdef：

```text
AutonomousSocialFeedback.Runtime
```

引用：

```text
AutonomousActivity.Runtime
Social.Runtime
```

`noEngineReferences=true`。

不引用 Village / Person / Event / UI / UnityEngine。

---

# 4. Social-owned Production Interpreter

在 Social Domain 中新增 configured production interpreter，例如：

```text
ConfiguredRelationshipInterpreter : IRelationshipInterpreter
```

它只消费纯规则数据。

Production bridge 应提供：

```text
CommitRelationalFact(RelationalFactRecord fact)
```

由 Bridge 内部使用唯一 Production Interpreter。

现有低层：

```text
SocialCommitService.Commit(fact, interpreter)
```

继续作为纯域 / 测试入口。

生产 callsite 不允许自行选择 interpreter。

---

# 5. Social Rule Model

最小 rule：

```text
RuleId
FactType
SourceRole
TargetRole
RelationshipChannel
Polarity
Strength
```

V0.1：
```text
SourceRole / TargetRole: Actor | Target
RelationshipChannel: Affinity
Polarity: Positive | Negative
Strength: Weak | Medium | Strong
```

Prototype rules：
```text
relational.visited_sick_person
SourceRole = Target
TargetRole = Actor
Channel = Affinity
Polarity = Positive
Strength = Medium

relational.helped_person
SourceRole = Target
TargetRole = Actor
Channel = Affinity
Polarity = Positive
Strength = Medium
```

---

# 6. Social Calibration

新增：
```text
AffinityDeltaWeak   = 4
AffinityDeltaMedium = 8
AffinityDeltaStrong = 12
```

Not Frozen。

必须校验 finite、>0、required keys、duplicate key。

---

# 7. Config Authoring

## 7.1 修改现有 Autonomous workbook

`Development/Config/RRPG_AutonomousLife.xlsx`

新增 Sheet：
```text
ActivitySocialProjection
```

字段至少：
```text
ActivityType
PersistentRelationalFactType
```

Prototype：
```text
Rest      → empty
Drink     → empty
Talk      → empty
VisitSick → relational.visited_sick_person
Help      → relational.helped_person
```

## 7.2 新增 Social workbook

`Development/Config/RRPG_Social.xlsx`

Sheets：
```text
00_导航
RelationalFactType
RelationshipInterpretationRule
SocialCalibration
```

复用现有 cross-workbook ref validation。

---

# 8. Projection Prepare

在 Activity mutation 前完成：
- ActivityType mapping lookup
- FactType existence validation
- Actor / Target validation
- SourceOccurrenceId build
- matching interpretation rule existence validation
- projected fact payload construction

无 persistent mapping：
```text
Prepare = NoFeedback
```

需要 feedback 但规则非法：
```text
Prepare = Failure
```

此时不得 Complete Activity。

---

# 9. SourceOccurrenceId

```text
activity:<ActivityInstanceId>
```

要求 stable、culture invariant、同一 Activity instance 唯一一致。

---

# 10. Completion Integration

推荐使用现有 Host lifecycle gateway 的 decorator / equivalent adapter。

```text
Prepare feedback plan
↓
ActivityRuntimeBridge.TryComplete
↓
if Complete Success:
    Enqueue prepared payload
    Attempt immediate Drain
```

Activity Completed 后 Social drain failure 不得伪装成 Activity Failed。

---

# 11. Outbox Semantics

Entry：
```text
SourceOccurrenceId
FactType
ActorPersonId
TargetPersonId
Year
Month
Day
Status
FaultCode
```

Status：
```text
Pending
Faulted
```

Enqueue：
- same occurrence + same payload → idempotent no-op / existing
- same occurrence + different payload → explicit conflict
- invalid payload rejected before mutation

---

# 12. Drain Semantics

- Success → remove
- Duplicate → remove
- CommitInProgress → keep Pending
- SourceOccurrenceConflict → Faulted
- InvalidFact / InvalidMutation / DuplicatePairMutation / InterpreterFailure → Faulted

Faulted 不自动无限 retry。

---

# 13. Social Feedback Save

新增顶层 Save block：

```text
SocialFeedbackWorld
```

保存 Pending / Faulted entries。

`SaveVersion=1`。

Persistence：
- deep-copy build
- replacement restore
- null / legacy empty
- invalid DTO → false
- validation before mutation
- failed restore preserves old world
- stable ordering
- duplicate occurrence validation
- NewGame clear

---

# 14. SaveSystem Integration

所有 Save 入口携带：
```text
SocialWorld
ActivityWorld
AutonomousDecisionWorld
SocialFeedbackWorld
```

Load：
1. preflight SocialFeedbackWorld
2. 不在 preflight 前 mutate
3. 保持现有其它 world preflight
4. restore Social / Activity / Decision / Feedback
5. Load 时不自动 Drain
6. failed Load 保留当前 feedback world

不要顺手解决 Village / Event 全局事务债务。

---

# 15. Daily Barrier Integration

当前：
```text
State
→ Projects
→ Assignments
→ Resource
→ Formation
→ OpenAutonomousActivityOpportunity
→ Selection
```

改为：
```text
State
→ Projects
→ Assignments
→ Resource
→ Formation
→ SocialFeedbackRuntimeBridge.DrainBarrier
→ OpenAutonomousActivityOpportunity
→ Selection
```

Barrier 非 Settled：
- 不开 Activity Window
- 不运行 Selection
- 结构化 diagnostic
- DayTick 不向外抛异常
- 不 replay 旧日期

---

# 16. Static Config Runtime Access

Social Runtime 不直接依赖 StaticConfig assemblies。

Host 将 Social Config 投影为纯 rule catalog 给 configured interpreter。

---

# 17. Prototype Fact Policy

Persistent：
```text
VisitSick → relational.visited_sick_person
Help      → relational.helped_person
```

No persistent relation fact：
```text
Rest
Drink
Talk
```

Failed / Interrupted Activity 永不产生 success fact。

---

# 18. Historical Reciprocity E2E

World A：
```text
no prior feedback
```

World B：
```text
张三 VisitSick 王五 completed
→ 王五→张三 Affinity +8 calibration
```

Save World B → Load。

随后给张三相同 NeedsHelp context。

比较王五对 `Help 张三` 的：
- Relationship Contribution
- Final Desirability
- Selection Weight

必须：
```text
World B > World A
```

其它输入保持一致。

---

# 19. Required Tests

## Gate A — Projection
- Rest no fact
- Drink no fact
- Talk no persistent fact
- Visit fact exact
- Help fact exact
- failed / interrupted no success fact
- missing mapping legal no feedback
- mapped fact missing definition fails prepare
- missing relationship rule fails prepare
- invalid actor / target fails prepare
- stable source occurrence id

## Gate B — Interpreter
- production interpreter Social-owned
- production caller cannot select arbitrary interpreter
- Target→Actor direction
- Visit / Help positive medium
- calibration lookup
- finite / missing / duplicate config validation
- same fact deterministic mutation
- no current-affinity curve V0.1
- multiple mutation contract remains legal

## Gate C — Outbox
- enqueue
- idempotent same payload
- conflict different payload
- Success ack
- Duplicate ack
- CommitInProgress pending
- non-retry results Faulted
- no infinite retry
- stable ordering
- structured diagnostics

## Gate D — Persistence
- round trip
- replacement
- null / legacy empty
- invalid preserves old state
- duplicate occurrence rejected
- DTO isolation
- SaveVersion 1
- NewGame clear
- failed load preserves feedback world

## Gate E — Completion Integration
- prepare failure prevents Complete
- complete success enqueues
- immediate drain success
- drain failure does not revert Activity Completed
- pending survives
- NoMajor produces no feedback
- Activity FSM semantics unchanged

## Gate F — Daily Barrier
- settled continues
- pending retry settled then continues
- unresolved pending blocks window
- Faulted blocks selection
- barrier before Decision Snapshot
- direct tick
- Update tick
- Jump
- Return catch-up
- no replay old date

## Gate G — Cross-domain
- no Activity Runtime dependency on Social
- no Social Runtime dependency on Activity
- integration domain only
- no Event write
- no Recognition / Episode write
- no Person identity substitution
- no second clock / claim / relationship authority
- no Scene / Prefab

## Gate H — Historical Reciprocity
- feedback changes Target→Actor relationship
- save/load preserves
- future relationship contribution increases
- future desirability increases
- future selection weight increases
- no double-counting raw fact in WP-AL-3 utility

---

# 20. Public Debug / Trace

至少提供只读：
```text
Projection result
SourceOccurrenceId
Outbox status
Drain result
SocialCommitResult
Relationship mutation(s)
Barrier result
```

Trace 不进入 Canonical History。

---

# 21. Recognition / Life Episode

本 Work Package 不实现：
```text
RecognitionCandidate
LifeEpisodeView
Event Trigger
History UI
```

只在 Architecture 文档记录 Target：
```text
Canonical Fact
├─ Relationship Interpreter
├─ Recognition Projector（Future）
└─ History Projector → LifeEpisodeView（Future）
```

---

# 22. Suggested Write Scope

预计候选：
```text
Development/Config/RRPG_AutonomousLife.xlsx
Development/Config/RRPG_Social.xlsx
Tools/RRPG.ConfigCompiler/...
Development/Generated/StaticConfig/...
Assets/Scripts/Social/Runtime/...
Assets/Scripts/SocialHost/...
Assets/Scripts/AutonomousLife/SocialFeedback/Runtime/...
Assets/Scripts/AutonomousLifeHost/...
Assets/Scripts/StaticConfig/...
Assets/Editor/StaticConfig/...
Assets/Scripts/Save/SaveData.cs
Assets/Scripts/Save/SaveSystem.cs
Assets/Scripts/Village/VillageRuntimeBridge.cs
Assets/Tests/EditMode/AutonomousLife/SocialFeedback/...
Compiler tests
```

执行前由 Codex 基于真实仓库列精确白名单。

---

# 23. Stop Conditions

以下情况停止并上报：
- 需要修改 PersonId identity
- 需要 Activity Runtime 直接依赖 Social
- 需要 Social Runtime 直接依赖 Activity
- 需要 Activity config 保存 Relationship delta
- 需要 Producer 自己选择 production interpreter
- 需要第二套 Relational Fact Ledger
- 需要 SaveVersion 升级
- 需要全局 2PC
- 需要修改 WP-AL-3 selection semantics
- 需要 Recognition / Event / Life Episode 进入本轮产品链
- 需要先解决 Village / Event 全局 Load transaction
- 需要 Scene / Prefab / Presentation
- Authority / Current docs 与真实实现冲突且无法 Adapter 兼容

---

# 24. Validation / Delivery

实现完成要求：
- compile 0 errors
- targeted tests PASS
- Compiler tests PASS
- full EditMode PASS
- full PlayMode PASS
- config generation reproducible
- Builder repeat no-op
- `git diff --check`
- Scene / Prefab unchanged
- protected producer resources unchanged
- Semantic Self-Review

先推：
```text
review/wp-al-4
```

不直接 push main。

---

# 25. Deferred

继续后置：
- Recognition Runtime
- LifeEpisode Runtime / Save
- Event integration
- History UI
- Character Knowledge
- Witness / Rumor
- multi-dimensional relationship model
- relationship saturation curve
- long-term pattern aggregation
- production fault recovery UI
