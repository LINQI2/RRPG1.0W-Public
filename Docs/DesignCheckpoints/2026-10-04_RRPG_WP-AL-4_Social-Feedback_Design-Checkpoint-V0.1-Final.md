# RRPG｜WP-AL-4 Social Feedback & Historical Reciprocity（社会反馈与历史互惠）｜Design Checkpoint V0.1 Final

> 日期：2026-10-04
> 基线：`main@7fcffe5b28be9156a45b95c3981b53ec8b9cac9b`
> 状态：**Design Consolidation Final / Ready for Authority Landing**
> 性质：产品与架构设计检查点；不是 Runtime 实现授权本身。
> 原则：**Production Architecture + Prototype Content（正式生产架构 + 原型测试内容）**。
> 前置：WP-AL-1 Social Foundation、WP-AL-2 Activity Lifecycle、WP-AL-3 Autonomous Selection 已发布 main。

---

# 1. 核心目标

WP-AL-4 只证明第一条完整历史反馈闭环：

```text
Activity Completed
↓
Semantic Relational Fact Projection
↓
Durable Social Feedback Outbox
↓
Social-owned Production Relationship Interpreter
↓
Directional Relationship State
↓
Save / Load
↓
下一次 WP-AL-3 Decision Snapshot
↓
Future Activity Selection Changes
```

一句话：

> **过去真实发生的社会行为，必须可靠地改变人物之间的有向关系，并在未来自主决策中产生可解释影响。**

---

# 2. Product Rules（产品规则）

## 2.1 Producer 只负责“发生了什么”

Activity / Event 等 Producer 只表达客观语义，不拥有长期关系数值解释权。

禁止：

```text
VisitSick → Affinity +10
Help → Affinity +15
```

直接存在 Activity / Event 配置中。

## 2.2 Production Relationship Interpreter 由 Social Domain 单一拥有

Social Domain 拥有唯一 Production Relationship Interpreter。

职责：

```text
Semantic Relational Fact
↓
Role-based Interpretation
↓
RelationshipMutation[]
```

现有 `IRelationshipInterpreter` 继续保留为 Social 纯域契约与测试注入接缝。

生产调用方不再自行选择 Interpreter。

## 2.3 Activity 不直接修改 Relationship

正式链：

```text
Activity Completed
↓
Fact Projection
↓
Social Commit
↓
Relationship Interpreter
↓
Directional Relationship State
```

Activity Runtime 不读取 / 写入 Relationship Mutation。

## 2.4 Fact 描述“发生了什么”，不是“奖励多少关系”

合法语义：

```text
relational.visited_sick_person
relational.helped_person
```

非法语义：

```text
increase_affinity_10
good_social_interaction_level_2
```

## 2.5 有向关系反馈必须保留方向意义

V0.1：

```text
Visit Sick:
Fact.Target → Fact.Actor
Affinity Positive

Help:
Fact.Target → Fact.Actor
Affinity Positive
```

不默认同步增加 `Actor → Target`。

## 2.6 Talk V0.1 不自动改变长期关系

普通 Talk 不进入持久 Relational Fact Authority，也不自动增加 Affinity。

## 2.7 Rest / Drink 不进入 Relational Fact Authority

V0.1：

```text
Rest  → no Relational Fact
Drink → no Relational Fact
Talk  → no Persistent Relational Fact
Visit Sick → Persistent Relational Fact
Help → Persistent Relational Fact
```

## 2.8 Ephemeral / Persistent / Significant 不建立三套 Ledger

第一代生命周期：

```text
ActivityCompletionSnapshot
= 已发生的短暂 Activity Occurrence

Persistent RelationalFactRecord
= 具有长期社会语义的 Canonical Fact

Significance / Life Episode
= 对 Canonical Fact 的后续派生视图 / 识别
```

不建立三套竞争性权威。

## 2.9 Historical Reciprocity 是 WP-AL-4 核心验收

必须证明：

```text
张三 Visit Sick 王五
↓
王五 → 张三 Relationship 上升
↓
Save / Load 后保持
↓
后来张三 NeedsHelp
↓
王五在 WP-AL-3 中对 Help 张三的 Relationship Contribution / Weight 提高
```

---

# 3. Fact Lifecycle（事实生命周期）

## 3.1 Activity Completion = Occurrence，不是 Canonical Social Fact

现有 `ActivityCompletionSnapshot` 只是 Activity 已完成的发生实例。

## 3.2 Projection 决定是否形成持久 Social Fact

```text
ActivityCompletionSnapshot
↓
ActivitySocialFactProjector
↓
0 or 1 ProjectedRelationalFact
```

V0.1：

| Activity | Persistent Relational Fact |
|---|---|
| Rest | 无 |
| Drink | 无 |
| Talk | 无 |
| VisitSick | `relational.visited_sick_person` |
| Help | `relational.helped_person` |

## 3.3 Significant 是 Consumer 判断

Producer 不设置 `Significant = true`。

未来：

```text
Canonical Fact
├─ Relationship Interpreter
├─ Recognition Projector
└─ History / Life Episode Projector
```

三个 Consumer 平行读取同一 Fact。

## 3.4 Life Episode V0.1 不建立 Runtime Authority

Life Episode 当前保持 **Derived View / Target Architecture**，不新增持久化 Episode Ledger。

---

# 4. Cross-domain Reliability（跨域可靠性）

## 4.1 不采用全局 Two-Phase Commit

明确否决 Activity + Social 全局 2PC / Universal Transaction Manager。

## 4.2 采用 Durable Outbox + Idempotent Social Consumer

```text
Activity Complete
↓
Persistent Social Feedback Outbox
↓
SocialCommitService
↓
SourceOccurrenceId exactly-once
```

## 4.3 Projection Plan 必须在 Activity mutation 前准备

正式顺序：

```text
Prepare Projection Plan
↓
TryComplete Activity
↓
Complete 成功
↓
立即 Enqueue 已验证 payload
↓
立即 Drain
```

可预见的配置 / 语义错误应尽量在 Complete 前发现。

## 4.4 Outbox 保存已经投影完成的 Fact Payload

Outbox Entry 保存：

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

不保存 ActivityCompletionSnapshot 等未来重解释。

## 4.5 Outbox 不是 Canonical Fact Authority

`Pending` = 应投递但尚未确认。
`Faulted` = 非重试型一致性 / 配置 / Interpreter 故障。

成功后删除，不保存 Delivered 历史副本。

---

# 5. SourceOccurrenceId（来源发生身份）

Activity 来源：

```text
activity:<ActivityInstanceId>
```

同一 Activity Completion 永远生成相同 SourceOccurrenceId。

---

# 6. Outbox Delivery Semantics

- `Success` → remove
- `Duplicate` → treat as delivered → remove
- `CommitInProgress` → keep Pending
- `SourceOccurrenceConflict` → Faulted
- `InvalidFact / InvalidMutation / DuplicatePairMutation / InterpreterFailure` → Faulted

不进行无限自动重试。

---

# 7. Daily Settlement Barrier（日推进反馈结算屏障）

真实 Village 顺序当前为：

```text
VillageState
→ Projects
→ Assignments
→ Resource Settlement
→ Formation
→ Open Activity Opportunity
→ WP-AL-3 Selection
```

WP-AL-4 最小插入点：

```text
VillageState
→ Projects
→ Assignments
→ Resource Settlement
→ Formation
→ Drain Social Feedback Barrier
→ Open Activity Opportunity
→ WP-AL-3 Decision Snapshot / Selection
```

冻结的语义是：

> **构建新的 WP-AL-3 Decision Snapshot 之前，所有更早发生且应影响 Relationship 的 Pending Feedback 必须 settled。**

Barrier 未 settled：
- 不打开新的 Autonomous Activity Window；
- 不运行当天 WP-AL-3 Selection；
- Village Day Tick 不向外抛异常；
- 记录结构化诊断；
- 不 replay 过去日期。

---

# 8. Current-day Completion Failure Boundary

如果：

```text
Activity 已成功 Complete
↓
Outbox 已 Enqueue
↓
立即 Drain 失败
```

则：
- Activity Completion 仍成立；
- 不伪装为 Activity Failed；
- Pending / Faulted 保留；
- 当前决策轮可完成；
- 下一次 Decision Snapshot 前 Barrier 必须处理或 fail closed。

---

# 9. Production Relationship Interpreter（生产关系解释器）

## 9.1 Social-owned

生产入口语义：

```text
SocialRuntimeBridge.CommitRelationalFact(fact)
```

调用方不传 Production Interpreter。

现有低层 `SocialCommitService.Commit(fact, interpreter)` 可继续作为纯域 / 测试接缝。

## 9.2 Rule-based

Production Interpreter 消费 Social-owned Rule Catalog。

概念规则：

```text
FactType
RelationshipSourceRole
RelationshipTargetRole
RelationshipChannel
Polarity
Strength
```

Prototype：

```text
relational.visited_sick_person
Source = Fact.Target
Target = Fact.Actor
Channel = Affinity
Polarity = Positive
Strength = Medium

relational.helped_person
Source = Fact.Target
Target = Fact.Actor
Channel = Affinity
Polarity = Positive
Strength = Medium
```

V0.1 不使用 current-affinity curve / diminishing returns。

---

# 10. Static Authoring Ownership（静态制作权威）

遵守“每个领域一本 Workbook”。

## 10.1 Activity → Fact Projection

继续放在：

```text
Development/Config/RRPG_AutonomousLife.xlsx
```

新增 Sheet：

```text
ActivitySocialProjection
```

至少表达：

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

## 10.2 Social Domain Rules

新增：

```text
Development/Config/RRPG_Social.xlsx
```

最小 Sheets：

```text
00_导航
RelationalFactType
RelationshipInterpretationRule
SocialCalibration
```

所有生成物继续走现有 Config Compiler / Builder / StaticConfigRegistry。

---

# 11. Calibration Candidate（调参候选 / Not Frozen）

V0.1 测试映射：

```text
Weak   → |AffinityDelta| 4
Medium → |AffinityDelta| 8
Strong → |AffinityDelta| 12
```

Prototype：

```text
Visit Sick → Positive Medium → +8
Help       → Positive Medium → +8
```

这些数值只用于验证反馈链强度。

---

# 12. Social Feedback Integration Layer（社会反馈接合层）

建议新增独立纯逻辑 Integration Domain：

```text
AutonomousSocialFeedback.Runtime
```

职责：
- Projection Plan
- Pending / Faulted Outbox
- Delivery / Drain
- Persistence DTO
- Result / Diagnostic

依赖：
```text
AutonomousActivity.Runtime
Social.Runtime
```

不依赖 Village / Person / Event / UI / UnityEngine。

---

# 13. Save / Load Authority

新增顶层 Save block：

```text
SocialFeedbackWorld
```

只保存 Pending / Faulted Outbox entries。

`SocialWorld` 继续保存 Canonical Facts + Relationships。

`SaveVersion` 保持 1。

Restore：
- replacement；
- old save missing → empty；
- invalid DTO preflight 失败；
- failed Load 保留当前 SocialFeedbackWorld；
- NewGame → empty；
- Load 时不自动 Drain。

---

# 14. Recognition / Life Episode Scope

Recognition 定义为 **System / Narrative Recognition**，不等于 Character Knowledge。

WP-AL-4 V0.1 Runtime 不实现 Recognition。

Life Episode 保持 Target Architecture / Derived View：
```text
Canonical Fact
↓
History Projector
↓
LifeEpisodeView
```

继续 Deferred：
- Event UI
- History UI
- Biography
- Witness / Rumor
- Character Knowledge
- complex significance
- Episode materialization

---

# 15. Vertical Slice Test（纵切测试）

## Historical Reciprocity E2E

Day A：
```text
王五 injured
↓
张三 selects VisitSick 王五
↓
Activity Completed
↓
Projected Fact = relational.visited_sick_person
↓
Outbox
↓
Social Interpreter
↓
王五 → 张三 Affinity +8（Calibration）
```

Save / Load 后保持。

Day B：
```text
张三 NeedsHelp
```

比较：
```text
World A：没有历史反馈
World B：有王五 → 张三 feedback
```

其它输入一致。

必须验证 World B 中：
- Relationship Contribution 更高
- Final Desirability 更高
- Selection Weight 更高

不要求 Weighted Ordering 必然选中 Help。

---

# 16. Freeze Classification

## Product Rule / Confirmed
- Producer 只生产客观 Fact 语义
- Social Domain 单一拥有 Production Relationship Interpreter
- Activity 不直接修改 Relationship
- Visit / Help：Target → Actor 正向
- Talk V0.1 不自动增加长期 Relationship
- Rest / Drink / Talk 不进入 Persistent Relational Fact Ledger
- Canonical Fact 与 Significant Projection 分离
- Historical Reciprocity 为核心验收
- 下一次 Decision Snapshot 前必须结算 earlier feedback

## Target Architecture / Confirmed
- Activity Social Fact Projector
- Prepare → Complete → Enqueue → Drain
- Durable Social Feedback Outbox
- `activity:<ActivityInstanceId>` SourceOccurrenceId
- Social-owned configured interpreter
- `RRPG_AutonomousLife.xlsx / ActivitySocialProjection`
- `RRPG_Social.xlsx`
- SocialFeedbackWorld Save block
- Village daily settlement barrier
- AutonomousSocialFeedback integration domain
- Life Episode as future Derived View

## Calibration Candidate / Not Frozen
- Weak / Medium / Strong → 4 / 8 / 12
- Visit Sick = Positive Medium
- Help = Positive Medium
- all exact Affinity delta values

## Deferred
- Recognition Runtime
- Event integration
- LifeEpisode Runtime / persistence
- Character Knowledge
- Witness / Rumor
- significance algorithm
- richer Relationship channels
- current-affinity curves
- production recovery UI for Faulted entries

---

# 17. Next Step

本文件与 WP-AL-4 Development Spec V0.1 一起进入 Authority Landing。

Landing 通过后才授权 Runtime Implementation。
