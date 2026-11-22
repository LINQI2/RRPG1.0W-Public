# RRPG｜WP-AL-3 Autonomous Selection & Arbitration（自主选择与仲裁）｜Development Spec V0.1

> 日期：2026-10-04
> 实现基线：`main@9c9a750999bcfcb04e9f01c9e2a0d1385f5c67c6`
> 输入：WP-AL-3 Design Checkpoint V0.2 Final
> 状态：**Ready for Authority Landing；Landing 完成后可明确授权 Runtime Implementation**
> 原则：Production Architecture + Prototype Content。

---

# 1. Work Package Goal

实现：

```text
Daily Activity Window
→ Decision Snapshot
→ Candidate Generation
→ Desire Evaluation
→ Plausibility
→ Weighted Candidate Order
→ Minimal Acceptance
→ Round-based Arbitration
→ WP-AL-2 Commit / Begin / Complete
```

并建立 Decision Round exactly-once、Explainability、Static Authoring。

不实现 Social Feedback / Event / Presentation / Personality Formation。

---

# 2. Existing Authorities Must Be Reused

必须复用：

- `PersonId`：长期人物身份
- `PersonDefinitionId`：静态配置 provenance，不是人物身份
- `Social.Runtime`：Relationship Authority
- `AutonomousActivity.Runtime`：Daily Window / Candidate / Claim / FSM / PersonActivityState
- `VillageTime`：唯一日时间 Authority
- `VillageRuntimeBridge.HandleDayTick`：每日链
- `RRPG.Deterministic.Runtime`：稳定 deterministic hash / random 原语
- 现有 XLSX → Compiler → Generated JSON → Unity Builder → StaticConfig Registry 配置链

禁止建立第二套同职责 Authority。

---

# 3. New Pure Runtime

建议新增：

```text
Assets/Scripts/AutonomousLife/Selection/Runtime/
```

asmdef：

```text
AutonomousSelection.Runtime
```

建议依赖：

```text
AutonomousActivity.Runtime
RRPG.Deterministic.Runtime
```

不直接依赖：

- Social.Runtime
- Village.Runtime
- Person.Runtime
- Event.Runtime
- UnityEngine
- UI

Social / Village / Person 数据通过 Host 构造 Snapshot 后传入。

---

# 4. Suggested Runtime Types

可由执行 AI 自主合理拆分，不要求机械一文件一类。

语义必须覆盖：

```text
BehaviorChannelDefinition
ResolvedBehaviorProfile
SpecificPreferenceView

DecisionRoundIdentity
DecisionSnapshot
DecisionPersonSnapshot
RelationshipSnapshot
ContextSignal

ActivityDesireProfile
BehaviorBinding
PreferenceBinding
RelationshipBinding
ContextBinding
RepetitionPolicy

CandidateEvaluationFrame
ContributionBreakdown
CandidateEvaluationResult

SelectionOption
NoMajorActivityOption
WeightedCandidateOrder

ParticipantAcceptanceResult
Proposal
ProposalResult

ArbitrationResult
DecisionPlan
DecisionTrace

AutonomousDecisionWorldState
AutonomousDecisionWorldSaveData
AutonomousDecisionWorldPersistence
```

---

# 5. Host / Product Adapter

建议 Assembly-CSharp 新增：

```text
Assets/Scripts/AutonomousLifeHost/AutonomousSelectionRuntimeBridge.cs
```

职责：

- 读取 ActivityRuntimeBridge 当前 Daily Window；
- 读取 PlayerCollection / PersonDefinitionId；
- 读取 SocialRuntimeBridge relationship query；
- 读取 StaticConfig；
- 构造 Context Snapshot；
- 调 pure Selection Runtime；
- 执行 Arbitration 结果；
- 调 WP-AL-2 lifecycle；
- Build / Restore DecisionWorld；
- 提供结构化 Decision Trace。

不得自己重新实现 Utility / Arbitration 算法。

---

# 6. Static Config Authoring

新增：

```text
Development/Config/RRPG_AutonomousLife.xlsx
```

复用现有 Config Compiler，不创建第二套 authoring path。

建议 Sheets：

```text
00_导航
BehaviorChannel
PersonBehavior
PersonPreference
Place
Activity
ActivityProvider
ActivityBehaviorBinding
ActivityPreferenceBinding
ActivityRelationshipBinding
ActivityContextBinding
ActivityRepetition
AutonomousLifeCalibration
```

对应生成 JSON / Unity Generated Database / Registry 访问入口。

Generated 文件不得人工编辑。

---

# 7. Behavior Value Rules

Behavior Value：

```text
0..1
Neutral = 0.5
```

Missing Channel → Definition Neutral，不创建记录。

V0.1 Channel：

```text
social.sociability
social.supportiveness
```

Specific Preference：

```text
-1..1
Neutral = 0
```

V0.1：

```text
leisure.drink
```

---

# 8. Prototype Profile Resolution

配置按：

```text
PersonDefinitionId
```

绑定静态 Behavior / Preference。

Runtime：

```text
PersonId
→ CharacterInstance.PersonDefinitionId
→ Static Profile
→ Resolved Profile
```

PersonId 始终是人物身份。

Missing / Legacy definition：
- neutral behavior；
- neutral preference；
- diagnostic；
- 不失败；
- 不创建长期状态。

WP-AL-3 不修改 Person Save Schema。

---

# 9. Decision Snapshot

Snapshot 必须 immutable / read-only。

同一 Round 所有人使用同一 Snapshot。

至少包含：

- DayStamp
- DecisionRoundIdentity
- eligible actors
- participant availability
- resolved profiles
- preferences
- relevant directed relationships
- context signals
- recent activity state
- provider state

Snapshot Build 后 Provider / Evaluation / Arbitration 不再实时读取 mutable world authority。

---

# 10. Decision Round Identity

使用 Canonical String：

```text
village.daily.major|Y|M|D
```

具体编码方式必须稳定、culture invariant。

---

# 11. Candidate Generation

V0.1 只实现最小 Prototype Provider 能力。

5 Activity：

```text
Rest
Drink
Talk
VisitSick
Help
```

3 Place：

```text
Home
Tavern
Workshop
```

Provider 只负责生成 Candidate，不负责评分。

Context Requirement：

- VisitSick：Target Injury Context > 0 才生成
- Help：Target HelpNeed Context > 0 才生成

HelpNeed 可来自 Test Context Fixture，不建立正式新 world state。

---

# 12. Desire Evaluation

使用 Additive Model。

每个 Binding 解析为 Family Contribution。

Strength Scalar：

```text
Weak   .35
Medium .65
Strong 1.0
```

Family Clamp：

```text
Behavior ±15
Preference ±20
Relationship ±25
Context ±40
Repetition 0..-25
```

每一 Family 内聚合后再 Clamp。

必须输出完整 Contribution Breakdown。

---

# 13. Relationship Adapter

不改变 Social Affinity。

V0.1：

```text
normalizedAffinity =
clamp(Affinity / 20, -1, 1)
```

`20` 来自 Calibration config，不硬编码在业务类中。

---

# 14. Context Signal

Context Magnitude：

```text
0..1
```

每个 Signal 必须携带语义：

```text
Key
SourceDomain
SubjectRole
Magnitude
```

禁止任意未定义字符串临时进入正式配置。

---

# 15. Plausibility

Calibration：

```text
Threshold = 15
```

Activity Desirability < Threshold：

```text
FilteredOut
```

NoMajor 不参与该过滤。

---

# 16. Weight Mapping

Calibration：

```text
x = clamp((score - threshold) / 60, 0, 1)
weight = 1 + 4 * pow(x, 1.5)
```

需要：

- finite validation
- monotonicity tests
- threshold boundary tests
- saturation tests

---

# 17. NoMajorActivityOption

Synthetic option：

```text
StableKey = special.no_major_activity
Weight = 0.75
```

始终加入 Actor 的 Selection Options。

如果排序走到它：

- actor resolved as NoMajor；
- 不生成 Proposal；
- 不 Claim；
- 不 Commit；
- 不更新 Activity PersonActivityState。

DecisionWorld 负责 round exactly-once。

---

# 18. Candidate-specific Deterministic Ordering

必须复用：

```text
DeterministicRandom.StableHash64
```

Selection identity：

```text
DecisionRoundIdentity|ActorPersonId|CandidateKey|selection
```

将 hash 稳定映射成 `(0,1)` 的 `u`。

V0.1 Weighted Exponential Race：

```text
orderKey = -ln(u) / weight
```

升序排列。

Tie-break：

```text
CandidateKey ordinal
```

测试必须证明：

- same input same order；
- shuffled source collection same order；
- unrelated candidate 不改变其它候选自身 random value；
- 不使用 `string.GetHashCode` / Unity Random / Guid / wall clock。

---

# 19. Minimal Acceptance

接口输出：

```text
Accepted
Rejected(reason)
```

Reject Reason 至少：

```text
ParticipantUnavailable
InvalidParticipant
HardReject
```

V0.1 Prototype 没有 production HardReject 规则。

---

# 20. Proposal Arbitration

每个 Actor 有稳定 Candidate Pointer。

Round：

1. current option = NoMajor → actor finished；
2. activity option → Proposal；
3. participant acceptance；
4. accepted proposals 进入 arbitration；
5. rejected / loser pointer++；
6. winner commit；
7. repeat until all actors resolved or exhausted。

禁止 reroll / re-evaluate desire。

---

# 21. Arbitration Priority

Identity：

```text
DecisionRoundIdentity|ProposalIdentity|arbitration
```

→ StableHash64。

低 hash 优先。

Tie-break：

```text
ProposalIdentity ordinal
```

V0.1 Deterministic Greedy：

按优先序遍历 Proposal；
若其全部 Exclusive Participants 尚未被本轮 Winner 使用 → Winner；
否则 ConflictLoss。

不最大化全局成功数或 Utility。

---

# 22. Decision World Persistence

新增顶层 Save block：

```text
AutonomousDecisionWorld
```

最小 Schema：

```text
HasLastResolvedDay
LastResolvedYear
LastResolvedMonth
LastResolvedDay
```

用途：

> 防止同日已经完成 NoMajor / 全轮决策后再次重跑。

SaveVersion 保持 1。

Build / Restore：

- replacement；
- old save missing → empty；
- failed Load 保留旧 state；
- NewGame reset；
- 不保存 Candidate / Proposal / Trace / Order / RNG。

---

# 23. Daily Integration

WP-AL-2 当前：

```text
HandleDayTick
→ Formation
→ Open ActivityDailyWindow
```

WP-AL-3 在有效 Open 后调用 Selection Coordinator。

硬要求：

- 必须检查 Window Day == current Village Day；
- MissingPersonSource / stale window 不执行；
- DecisionWorld 已 resolved same day 不执行；
- 不凭 `CurrentWindow != null` 就消费。

---

# 24. Fail-closed Planning Boundary

Snapshot / Provider / Evaluation / Ordering / Acceptance / Arbitration 先完成 Planning。

Planning 出错：

- catch；
- structured diagnostic；
- no Activity commit；
- no DecisionWorld resolved；
- no Social mutation；
- Village day tick 不向外抛。

不 replay 已过去日期。

---

# 25. Headless Execution Driver

成功 Winner：

```text
TryCommitActivity
→ TryBeginExecution
→ TryComplete
```

调用 WP-AL-2 Runtime。

Selection Runtime 不写 Activity FSM 内部状态。

如果单个 Commit 因 runtime invariant 意外失败：

- 记录 CommitFailure；
- 不动态重新 Utility / reroll；
- 不生成 Social Fact；
- 继续按明确实现策略完成或停止本轮，但必须结构化诊断，禁止静默 fallback。

具体是“继续其它互不冲突 Winner”还是“停止剩余 Winner”，执行 AI可选**一种确定策略**并测试；不得让异常改变已成功 Commit 的历史。

---

# 26. Decision Round Completion

所有 Actor 最终进入：

- Activity success / terminal execution
- NoMajor
- Candidate exhausted
- explicit failed result

之后才：

```text
DecisionWorld.LastResolvedDay = current day
```

如果 Planning 阶段整体失败，不写 resolved。

---

# 27. No Social Feedback in WP-AL-3

禁止：

- ActivityCompletion → RelationalFact
- Relationship Mutation
- Event Recognition
- Life Episode

这些属于 WP-AL-4。

Selection 只读取 Relationship Snapshot。

---

# 28. Prototype Calibration Content

四人物：

```text
张三  Sociability .8  Supportiveness .8
李四  Sociability .5  Supportiveness .5
王五  Sociability .8  Supportiveness .2  leisure.drink +1
赵六  Sociability .2  Supportiveness .8
```

三场所：

```text
Home
Tavern
Workshop
```

五 Activity base seed：

```text
Rest      22
Drink     28
Talk      30
VisitSick 18
Help      18
```

全部 Test / Not Frozen。

---

# 29. Required Mechanism Tests

至少覆盖：

## Gate A — Config / Profile
- Channel definition validation
- missing channel neutral
- missing preference neutral
- PersonDefinitionId profile resolution
- legacy / missing definition neutral
- no PersonId identity substitution
- invalid duplicate config rejected

## Gate B — Snapshot
- immutable
- stable ordering
- same round same snapshot
- relationship frozen
- context frozen
- no mutable world query during evaluation

## Gate C — Desire
- additive breakdown exact
- family clamp
- strength mapping
- relationship normalization
- context mapping
- repetition mapping
- threshold
- weight mapping monotonic / finite / saturation
- explainability trace

## Gate D — Deterministic Ordering
- same input same order
- source order invariant
- candidate-specific random identity
- selection domain isolated from arbitration
- NoMajor participates in ordering
- no reroll

## Gate E — Acceptance / Arbitration
- unavailable reject
- invalid reject
- disjoint proposals coexist
- overlapping proposals single winner
- source person order invariant
- utility does not affect arbitration winner
- loser advances fixed pointer
- candidate exhaustion terminates
- NoMajor stops actor
- deterministic greedy bounded

## Gate F — DecisionWorld
- round once per day
- NoMajor survives save/load via resolved day
- old save empty
- failed load retains
- NewGame clears
- SaveVersion = 1

## Gate G — Village Integration
- valid window current day executes
- stale window blocked
- MissingPersonSource blocked
- direct tick
- Update tick
- Jump Month
- Return Catch-up
- no replay same day
- planning failure fail closed

## Gate H — Cross-domain
- no Social Fact
- no Relationship write
- no Event
- no Person core mutation
- no Scene / Prefab
- no Unity Random / wall clock / Guid
- no second VillageTime / Activity FSM / Claim authority

---

# 30. Vertical Slice Causal Tests

必须验证：

1. 张三 Talk 倾向 > 赵六 Talk（人物差异）
2. 张三对高 Affinity Target 的 Talk contribution 更高（关系差异）
3. Severe Injury 让 Visit 可以覆盖普通 Talk（情境覆盖）
4. 王五 Drink 第二天受到 repetition penalty（重复控制）
5. 张三 / 赵六争夺同一受伤 Target，仲裁唯一且可复现
6. 冲突后 Actor 可落到 NoMajor
7. same save + same round identity → same candidate order
8. Decision Trace 能回答“为什么”

---

# 31. Experience Validation

在 Headless Scenario Runner（无表现情景运行器）中观察 10～30 Activity Days。

该 Runner 属 Editor / Tests 工具，不是游戏 Runtime Authority。

观察：

- 人物差异能否被描述；
- 行为是否稳定但不机械；
- Context 是否产生可理解偏离；
- 是否存在过度重复；
- 冲突结果是否显得公平；
- Trace 是否解释结果。

WP-AL-3 不要求历史互惠闭环；该项留 WP-AL-4。

---

# 32. Suggested Write Scope

预计：

```text
Development/Config/RRPG_AutonomousLife.xlsx
Tools/RRPG.ConfigCompiler/...
Development/Generated/StaticConfig/...（生成）
Assets/Generated/StaticConfig/...（Unity 生成，按仓库规则）
Assets/Scripts/StaticConfig/...（Registry / DTO 接入）
Assets/Scripts/AutonomousLife/Selection/Runtime/...
Assets/Scripts/AutonomousLifeHost/...
Assets/Scripts/Save/SaveData.cs
Assets/Scripts/Save/SaveSystem.cs
Assets/Scripts/Village/VillageRuntimeBridge.cs
Assets/Tests/EditMode/AutonomousLife/Selection/...
必要的 Config Compiler / Static Config Tests
```

执行前由 Codex 基于真实仓库列精确白名单。

不得把候选列表机械当全部写入文件。

---

# 33. Forbidden / Stop Conditions

发现以下任一项应停止并上报：

- 需要修改 PersonId 身份语义；
- 需要把 PersonDefinitionId 当 PersonId；
- 需要新建第二套 config authoring 系统；
- Selection Runtime 需要直接依赖 mutable Social / Village / Person runtime；
- 需要第二套 Activity Claim / FSM / Clock；
- 需要 SaveVersion 升级；
- 需要完整 Target Utility；
- 需要 Personality Formation；
- 需要 Social Fact / Relationship Feedback；
- 需要 Event / Life Episode；
- 需要 Scene / Prefab / Navigation；
- 需要改变 WP-AL-2 Activity lifecycle 语义；
- 需要对过去日期自动 replay decision；
- Spec / Current Authority 出现真实冲突且无法通过 Adapter 解决。

普通低风险工程实现、格式、private API、测试辅助结构由 Codex自主判断，不作为 Stop Condition。

---

# 34. Validation / Delivery

实现完成：

- compile 0 errors
- targeted tests PASS
- full EditMode 0 regression
- full PlayMode 0 regression
- config compiler tests PASS
- generated config reproducible
- `git diff --check`
- Scene / Prefab unchanged
- protected workspace resources unchanged
- Semantic Self-Review
- Push `review/wp-al-3`
- 不直接 push main

开发15.0随后直接读取 GitHub real commit review。

---

# 35. Deferred

不做：

- Personality / Trait Runtime
- Formation
- Social Feedback
- Event Recognition
- Life Episode
- Navigation / Animation
- Target Acceptance Utility
- maximum matching
- Habit / Mood / Needs
- additional behavior channels beyond prototype
