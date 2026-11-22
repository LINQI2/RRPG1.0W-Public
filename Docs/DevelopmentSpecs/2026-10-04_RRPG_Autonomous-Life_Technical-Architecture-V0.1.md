# RRPG Autonomous Life V0.1 — Technical Architecture

> 日期：2026-10-04
> 项目：RRPG
> 工程基线：`main@388d00db32a05c343c674308d3f6d74622181a5d`
> 类型：Technical Architecture Candidate（技术架构候选）
> 状态：Architecture Review PASS / Target Technical Architecture / Not Implemented（架构审阅通过 / 目标技术架构 / 尚未实现；不自动授权 Runtime 开发）
> 输入：Autonomous Life Vertical Slice Product Spec V0.2 + T1～T6 架构裁决
> 原则：**Production Architecture + Prototype Content（正式生产架构 + 原型测试内容）**

---

> WP-AL-4 Authority Landing更新：原始首页与早期章节保留完整目标的形成语境；WP-AL-1/2/3已Published Main，Current实现查[技术架构总览](../Architecture/技术架构总览.md)。§28对应Selection已实施；§29是本次Social Feedback **Target Architecture / Confirmed / Not Implemented**。本轮不启动Runtime，不创建或修改工作簿。

## 1. Architecture Goal（架构目标）

Autonomous Life V0.1 要证明：

> 具体 Person（人物）能够在 VillageTime（村庄时间）和现有 Responsibility / Occupancy（责任 / 占用）约束下，从世界提供的 Activity（活动）中自主选择；真实活动形成 Semantic Fact（语义事实），Fact 改变 Directional Relationship State（有方向关系状态），并进一步影响未来行为，同时可被 Event（事件）和 History Projection（历史投影）消费。

V0.1 不追求完整生活模拟，而是建立可直接扩展的正式骨架。

---

## 2. Non-negotiable Architecture Rules（不可破坏架构规则）

1. `PersonId（人物唯一身份）` 是长期人物身份；BattleUnitId / CardId / 临时实例不得承载长期社会状态。
2. `VillageTime（村庄时间）` 继续是唯一村庄时间 Authority（权威）。
3. 现有 `Temporary Occupancy（临时占用）` 继续负责 Expedition / Active Project / Unavailable 等高优先级占用。
4. Autonomous Life 不建立第二套 Village Clock（村庄时钟）或第二套 Occupancy（占用）真相源。
5. `Activity（活动）`、`Relational Fact（关系事实）`、`Relationship State（关系状态）`、`Recognition（认识）`、`Event（事件）`、`Life Episode Projection（人生经历片段投影）` 必须职责分离。
6. Read Must Not Create（读取不得产生持久副作用）。
7. Simulation Authority（模拟权威）与 Presentation（表现）分离。
8. 所有随机行为使用仓库共享 `RRPG.Deterministic（确定性随机）`；禁止新增独立随机实现。
9. Save / Load 使用 Replacement Restore（替换式恢复），不得跨 World（世界）残留社会状态。
10. Candidate / Algorithm（候选算法）不得因为本文件存在而自动升级为 Gameplay Rule（玩法规则）。

---

## 3. High-level Runtime Flow（高层运行流程）

```text
VillageTime
（村庄时间）
↓
Daily Responsibility / Hard Occupancy Snapshot
（每日责任 / 高优先级占用快照）
↓
Autonomous Activity Opportunity
（自主活动机会）
↓
Activity Providers
（活动提供者）
↓
Candidate Generation
（候选生成）
↓
Eligibility Gate
（资格门槛）
↓
Explainable Utility Evaluation
（可解释效用评价）
↓
Deterministic Weighted Candidate Ordering
（确定性加权候选排序）
↓
Central Conflict Arbitration
（统一冲突仲裁）
↓
Activity Commit + Participation Claims
（活动承诺 + 参与占用）
↓
Explicit Activity FSM
（显式活动有限状态机）
↓
Completed / Failed / Interrupted
（完成 / 失败 / 中断）
↓
Activity Result
（活动结果）
↓
Relational Fact Authority
（关系事实权威）
├─→ Relationship Interpreter（关系解释器）
│    └─→ Directed Relationship State（有向关系状态）
├─→ Recognition Projection（认识投影）
│    └─→ Unified Event Core（统一事件核心）
└─→ Historical Projection（历史投影）
     └─→ Life Episode View（人生经历片段视图）
```

---

## 4. Authority Map（权威映射）

### 4.1 Existing Authorities（既有权威）

| Authority（权威） | 当前正式职责 | Autonomous Life 边界 |
|---|---|---|
| `PersonId（人物唯一身份）` | 长期人物身份 | 直接复用 |
| `VillageTime（村庄时间）` | 日推进唯一时间真相 | 直接复用 |
| `Primary Assignment（主要长期职责）` | 长期 Responsibility（责任） | 不等于全天占用 |
| `Temporary Occupancy（临时占用）` | Expedition / Active Project / Unavailable | Autonomous Activity 的 Hard Gate（硬门槛） |
| `VillageSocialFactLedger（村庄社会事实账本）` | Project TemporaryCommitment Fact | 不扩成万能关系事实库 |
| `PersonLifeHistory（人物人生史）` | Person-owned 客观人生事实 | 不承担 Shared Relational Fact 权威 |
| `WorldEventHistory（世界事件历史）` | Event occurrence / repeat / cooldown | 不保存关系事实 |
| `SaveSystem（存档系统）` | Player / Village / EventWorld 等世界状态保存 | 新 Social State 必须接入统一 Save 权威 |

### 4.2 New Logical Authorities（新增逻辑权威）

#### A. `Relational Fact Authority（关系事实权威）`

职责：记录已经真实发生的 Person ↔ Person 关系语义事实。

正式语义要求：Canonical（唯一权威）、Append-only（追加式）、Immutable（不可变）；Actor / Target 使用 `PersonId`；同一 Source Occurrence（来源发生实例）具备 Idempotent Commit（幂等提交）。它不存当前 Relationship Score（关系数值）、Event occurrence（事件发生历史）或 Life Episode 的复制品。

具体类名 / DTO / FactType 枚举仍未冻结。

#### B. `Directed Relationship State Authority（有向关系状态权威）`

语义：`SourcePersonId（关系主体） → TargetPersonId（关系对象）`。

规则：
- `(A → B) != (B → A)`；
- `(A → A)` V0.1 不建立人际关系记录；
- Query Missing Record（查询缺失记录）→ 返回 Neutral Default（中性默认）；
- Query（查询）不得创建持久记录；
- 当前 Relationship State 必须正式持久化；
- Person 暂时不活跃 / 离开当前调度池，不自动删除历史关系。

Ownership（所有权）：world-social-domain-owned（世界社会域拥有），不是 Character-owned，也不是 Village-owned。

具体程序集 / Save DTO 名称仍未冻结。

### 4.3 Derived State（派生状态）

`Recent Activity State（近期活动状态）` 用于 Repeat Penalty（重复惩罚）与同日活动额度判断。它不是 Historical Authority（历史权威）。窗口长度、保存形式与字段仍未冻结。

---

## 5. Relationship Mutation Path（关系修改路径）

唯一合法长期变化路径：

```text
Activity / Event Consequence
↓
Semantic Relational Fact
↓
Relationship Interpreter
↓
Directed Relationship State
```

禁止 `Activity → Relationship += X` 或 `EventEffect → Affinity += X`。

`Fact（事实） = 过去发生了什么`；`Relationship State（关系状态） = 截止当前，这段关系已经形成了什么状态`。

---

## 6. Transaction Boundary（提交原子性边界）

一次关系行为成功完成后，`Relational Fact Commit（关系事实提交）` 与 `Relationship State Update（关系状态更新）` 必须具备逻辑原子性：不允许只有 Fact、没有 State，也不允许 State 改了但 Fact 丢失；重试不得重复加权。

Transaction（事务）、Staged Commit（分阶段提交）、Idempotent Retry（幂等重试）保持 Supporting Candidate（强候选）。

---

## 7. VillageTime / Responsibility / Occupancy Integration（村庄时间 / 责任 / 占用接合）

### 7.1 Primary Assignment（主要长期职责）

Primary Assignment 表示长期 Responsibility（责任），不是全天 Occupancy（占用）。有 Primary Assignment 的 Person 仍可在责任之外参与 Autonomous Activity（自主活动）。

### 7.2 Hard Temporary Occupancy（高优先级临时占用）

| 状态 | 可进入 Autonomous Activity Opportunity？ |
|---|---|
| Expedition（远征） | 否 |
| Active Project（进行中项目） | 否 |
| Unavailable（不可用） | 否 |
| 只有 Primary Assignment | 是 |
| 无 Assignment | 是 |

使用现有 Day-start Temporary Occupancy Snapshot（日初临时占用快照）。Project 在当日完成时，当日仍视为已占用，次日才恢复自主活动机会。

### 7.3 Autonomous Activity Opportunity（自主活动机会）

它是 VillageTime 上的 Scheduling Phase（调度阶段），不是新 Clock（时钟）。V0.1 每名 Eligible Person（合资格人物）每个 Activity Window（活动窗口）最多参与 1 个 Major Activity（主要活动）；具体时刻未冻结。正常 Update、Jump Month、Expedition Return Catch-up 必须经过同一 Daily Simulation（每日模拟）规则。

---

## 8. Activity Provider Architecture（活动提供架构）

正式采用 Provider-driven Candidate Generation（提供者驱动候选生成）。Place（场所）、Person（人物）、Context（情境）都可提供 Activity Offering（活动供给）。Provider 负责活动类型、Target / Place、参与者与基础可用条件；不负责 Personality（性格）、Relationship（关系）、Utility（效用）和最终选择。

具体接口名仍未冻结。

---

## 9. Candidate / Eligibility / Utility Pipeline（候选 / 资格 / 效用流水线）

### 9.1 Candidate Generation（候选生成）

只读、无副作用。候选失败 / 被过滤，不产生 Runtime Activity State（运行中活动状态）。

### 9.2 Eligibility Gate（资格门槛）

使用确定性 Hard Gate（硬门槛）。非法候选直接移除，而不是通过 `-999 Utility` 伪装。

### 9.3 Explainable Utility Evaluation（可解释效用评价）

只对 Eligible Candidate（合法候选）评价人物此刻的倾向。V0.1 正式输入类别：Base Desire（基础倾向）+ Resolved Behavior Profile（已解析行为画像）+ Specific Preference（特定偏好）+ Directional Relationship（有向关系）+ Current Context（当前情境）+ Recent Repetition Modifier（近期重复修正）。

正式冻结 Factorized Explainability（分因素可解释性），不冻结具体数学公式、曲线、数值范围或 Personality / Relationship 正式字段。

---

## 10. Priority Model（优先级模型）

普通 Contextual Activity（情境活动）与 Daily Autonomous Activity（日常自主活动）不采用硬 `P2 > P3` 分层。Visit Sick（探病）等活动通过 Context Utility（情境效用）强烈提高倾向，而不是 100% 强制覆盖其它自主活动。

真正高层优先结构：Hard Responsibility / Occupancy（硬责任 / 占用）→ Future Critical Response（未来紧急响应，V0.1 不实现）→ Autonomous Life Selection（自主生活选择）。

---

## 11. Selection Algorithm（选择算法）

正式采用：`Plausibility Cut（合理性截断） + Deterministic Weighted Candidate Ordering（确定性加权候选排序）`。

```text
Eligible Candidates
↓
Utility Evaluation
↓
Plausibility Cut
↓
Weighted Candidate Ordering
↓
Proposal List
```

高 Utility 候选更常排前；不机械永远选最高分；明显不合理低分行为被截断；冲突失败后可稳定尝试下一候选。

### 11.1 Deterministic RNG（确定性随机）

唯一随机来源为 `RRPG.Deterministic.DeterministicRandom（仓库共享确定性随机）`。禁止 `UnityEngine.Random`、新 RNG、时间 Seed、Guid、`string.GetHashCode()` 与非稳定 Dictionary 顺序。

---

## 12. Decision Round / Conflict Arbitration（决策轮 / 冲突仲裁）

多人自主选择必须采用：Activity Window Start → Read World Snapshot → All Eligible Persons Generate Proposal Lists → Central Conflict Arbitration → Acquire Claims → Commit Activities。

要求：Deterministic（确定性）、Bounded（有界）、No infinite reroll（不无限重抽）、No fixed update-order bias（不因固定更新顺序永久偏置）、同输入可复现。

冲突失败者按已有候选顺序尝试下一候选，不重新随机整个决策。WP-AL-3 的目标优先级 / tie-break 已按 §28 选定；这是 Target Architecture，不是 Runtime 已存在或正式数值冻结。

---

## 13. Activity Commitment / Participation Claims（活动承诺 / 参与占用）

成功仲裁后才创建 Activity Instance（活动实例）。Activity Commitment（活动承诺）是当前 Activity Window 内的短期参与占用，不等同于现有 Temporary Occupancy。

双人 Activity 中，Actor（发起者）与 Target（目标人物）作为 Exclusive Participants（互斥参与者）时都需要 Claim。V0.1 每名 Person 每个 Activity Window 最多参与一个 Major Activity。

完整 Target Acceptance Utility（目标接受意愿）后置；WP-AL-3 必须保留正式 Participant Acceptance 接口，V0.1 目标为 Minimal Acceptance（合法 / 可用 / 无 HardReject），然后统一分轮仲裁，见 §28。

---

## 14. Activity Execution FSM（活动执行有限状态机）

V0.1 正式采用 Explicit FSM（显式有限状态机），不引入通用 BT / HFSM Framework。

概念生命周期：Committed → Preparing → Navigating → Executing → Completed；失败出口为 Failed / Interrupted。具体 Enum 名与是否保留 Preparing 未冻结。

正式规则：Commit 后不每帧重新计算 Utility；只有合法 Interrupt（中断）才取消；Failed / Interrupted 不产生成功 Fact；Completion（完成）是唯一长期副作用提交前提。

---

## 15. Live / Headless Execution（现场 / 无表现执行）

必须支持同一 Simulation Contract（模拟契约）的两种 Driver（驱动）：

- Live Driver（现场驱动）：Navigation（寻路）、Animation（动画）、Interaction Presentation（互动表现）；
- Headless Driver（无表现驱动）：用于 Jump Month、Expedition Return Catch-up、无 Village Scene 的日模拟。

两者必须使用同一 Eligibility、Selection、Activity Result、Fact 与 Relationship Mutation；禁止复制第二套简化 Gameplay Rule。

---

## 16. Presence Projection（在场投影）

现有 `VillageWorldPresence（村庄在场状态）` 不直接废弃。未来采用 `Base Presence（基础在场） + Activity Presence Overlay（活动位置覆盖） → Effective Presence（当前有效在场）` 的方向。具体 Overlay 数据结构未冻结。

---

## 17. Rest Naming Boundary（休整命名边界）

现有 `VillagePersonRestState（人物恢复期状态）` 与新 `Rest Activity（休息活动）` 是两个不同概念，不允许合并。

---

## 18. Save / Load / Reset（保存 / 加载 / 重置）

必须持久化或正式重建：Directed Relationship State（有向关系状态）、Relational Fact（关系事实）、Repeat Control / Daily Opportunity 所需的 Recent Activity State（近期活动状态）以及当天 Major Activity Participation 是否已消耗（若不能从持久事实确定性重建）。

V0.1 不要求保存路径进度、动画帧或进行中表现。长期副作用只在 Completed 后 Commit。Load 时清除旧 Runtime Activity Claims，不得残留 Ghost Activity（幽灵活动）或半 Fact / 半 Relationship Change。

NewGame / Load Other Save 使用 Clear / Replace，而不是 Merge（合并）。旧 Save 没有 Social 区块时初始化为空关系 / 空事实，未建立 Pair 默认 Neutral，不猜历史。SaveVersion 是否变化仍未冻结。

---

## 19. Event Integration（事件接合）

```text
Relational Fact
├─→ Relationship Interpreter
└─→ Recognition Projection
      ↓
Observer / Significance Gate
      ↓
VillageRecognitionRaised
      ↓
Unified Event Core
      ↓
WorldEventHistory
```

Recognition（认识）不是持久化真相源。合法来源包括 Direct Recognition（重大事实直接认识）与 Pattern Recognition（模式认识）。Observer Gate（观察者门槛）只约束 Social Response / Event，不阻止直接参与者的 Relationship Update。

Event Outcome（事件结果）若影响关系，生成新的 Semantic Relational Fact，而不是直接修改 Relationship State。

---

## 20. Historical Projection（历史投影）

V0.1 的 `Life Episode（人生经历片段）` 作为 Derived Historical Projection（派生历史投影），不新增 LifeEpisodeLedger / LifeEpisodeSaveData / Episode Lifecycle Runtime。

Gate B：`Significant Fact（重大事实） → Stable Historical Projection（稳定历史投影） → History View（历史视图）`。

`PersonLifeHistory（人物人生史）` 继续负责 Person-owned 客观人生事实。Shared Episode（共同经历片段）由多 Person 以不同 Role（角色）查看同一 Canonical Source，不复制多份原始事实。

只有未来出现稳定引用、玩家交互、Gameplay 消费、跨版本稳定要求或持续聚合需求时，才考虑 Materialize / Persist（物化 / 持久化）。

---

## 21. Debug Architecture（调试架构）

每次 Autonomous Decision（自主决策）必须可输出 Decision Trace（决策轨迹），至少包含：Decision Identity、ActorPersonId、Candidates、Provider、Eligibility、Invalid Reason、Base Desire、Behavior / Relationship / Context / Repetition Modifier、Final Desirability、Selection Weight、Weighted Candidate Order、Conflict Result、Committed Activity、Deterministic Random Identity。

Trace 默认不属于长期 Save Authority。

---

## 22. Algorithm / Framework Decisions（算法 / 框架裁决）

| 技术 | V0.1 状态 | 用途 |
|---|---|---|
| `Utility AI（效用型 AI）` | Adopted Principle（采用原则） | 合法自主候选的倾向评价 |
| `Weighted Random（加权随机）` | Adopted Principle（采用原则） | 非机械选择；必须确定性 |
| `Activity Provider（活动提供者）` | Adopted Architecture（采用架构） | 世界提供行为可能性 |
| `FSM（有限状态机）` | Adopted Architecture（采用架构） | Activity 生命周期 |
| `HFSM（分层有限状态机）` | Deferred（后置） | 未来更复杂长期行为 |
| `BT（行为树）` | Deferred | 未来复杂 Activity Executor |
| `GOAP（目标导向行动规划）` | Deferred | 尚无长期个人目标规划需求 |
| `HTN（分层任务网络）` | Deferred | 同上 |
| `LLM（大语言模型）` | Non-authoritative（非权威） | 可做文本表达，不做模拟决策 |
| `Smart Object（智能对象）` | Use Principle Only（只采用思想） | 不建立万能 Smart Object Framework |

---

## 23. Module / Assembly Direction（模块 / 程序集方向）

Confirmed（确认）：Relationship / Relational Fact 属于独立 Social Domain（社会域），不属于 Character / Hero / Village 某一具体对象。

Strong Candidate（强候选）：`Social.Runtime（社会运行程序集）`，可能职责为 Relational Fact Authority、Directed Relationship Registry、Relationship Interpreter、Social Query Surface。

要求尽量不依赖 UI、Battle、Event、Village Presentation。Activity Runtime（活动运行域）的最终 asmdef 归属仍未冻结。在 Development Work Package 前必须画清程序集依赖图，防止 Village ↔ Social ↔ Event 循环依赖。

---

## 24. Freeze Classification（冻结分级）

### 24.1 Confirmed Technical Architecture（已确认技术架构）

- 独立 Relational Fact Authority
- world-social-domain-owned Directed Relationship State
- Relationship State 正式持久化
- Query Missing → Neutral，读取无副作用
- Fact / Relationship State 职责分离
- Activity / Event 不直接修改 Relationship
- Primary Assignment ≠ 全天占用
- Autonomous Activity Opportunity 派生自 VillageTime
- Temporary Occupancy 是 Activity Hard Gate
- Provider-driven Candidate Generation
- Deterministic Eligibility
- Explainable Utility Evaluation
- Plausibility Cut + Deterministic Weighted Candidate Ordering
- Snapshot → Proposal → Arbitration → Commit
- Activity Commitment 与 Temporary Occupancy 分离
- Explicit Activity FSM
- Live / Headless 共用 Simulation Contract
- Relational Fact → Relationship / Recognition / Historical Projection 并行消费
- Unified Event Core 继续唯一 Event 出口
- Life Episode V0.1 采用 Derived Projection
- Save / Load Replacement Restore + World Isolation
- 正式 Decision Trace / Explainability

### 24.2 Supporting Candidate（强候选）

`Social.Runtime`、`SocialWorldSaveData`、`RelationalFactLedger`、`RelationshipRegistry`、`Affinity（亲近度）`、`Sociability（社交主动倾向）`、`Supportiveness（支持响应倾向）`、Utility 数值公式 / Curve、Candidate Cut 阈值、Weight 映射、SourceFactId / SourceOccurrenceId 字段、Recent Activity State 窗口长度、Activity Provider 接口名、Activity FSM Enum、Presence Overlay 结构、Life Episode 正式术语、SaveVersion 迁移策略。

### 24.3 Deferred（后置）

完整 Needs（需求）、Mood（情绪）、Habit（习惯）、Family / Romance / Marriage / Birth、Personality Formation、多维 Relationship 正式模型、Target Acceptance Utility、完整 24 小时 Schedule、Critical Response System、HFSM / BT 通用框架、GOAP / HTN、LLM Simulation Authority、Pattern Episode Runtime、Materialized Life Episode、大规模 Social Network Propagation（社会网络传播）。

### 24.4 Rejected（否决）

第二套 VillageTime、第二套 Occupancy Authority、万能 Universal Fact Database、关系事实写入 WorldEventHistory、Shared Fact 复制到多个 PersonLifeHistory、Activity / Event 直接 `Relationship += X`、Query 自动创建 Relationship Record、Load 时依赖完整历史 Replay 重算当前 Relationship、每条 Relational Fact 直接弹 Event、Legacy VillageSocialResponse 新写入、非确定性随机、固定 Person Update 顺序决定冲突胜负、冲突后无限 reroll、每帧重算 Utility、V0.1 建立万能 HFSM / BT / GOAP / HTN / Smart Object Framework、LLM 决定 Simulation Result。

---

## 25. Acceptance Architecture（架构验收）

第一代 Technical Architecture 至少支持：

1. Behavioral Difference（行为差异）
2. Relationship Differentiation（关系差异）
3. Context Override（情境覆盖）
4. Historical Reciprocity（历史互惠）
5. Repetition Control（重复控制）
6. Target Conflict（目标冲突）
7. Explainability（可解释性）
8. Persistence / World Isolation（持久化 / 世界隔离）
9. Live / Headless Consistency（现场 / 无表现一致性）
10. Event Integration Smoke Test（事件接合冒烟验证）
11. Exactly-once Social Commit（社会状态单次提交）
12. Deterministic Replay（确定性复现）

具体概率阈值 / 美术表现不在本 Technical Architecture 冻结。

---

## 26. Development Work Package Split（开发工作包切分）

### WP-AL-1 — Social Foundation（社会域基础）

建立 Relational Fact + Directed Relationship + Save / Load / Reset 的正式基础，不接 Autonomous Activity。

包括：Social Domain 纯逻辑边界、Relational Fact Authority、Directed Relationship Registry、Neutral Query、Relationship Interpreter 最小可扩展入口、Save / Restore / NewGame replacement reset、Exactly-once / idempotent commit、EditMode Tests。

不包括：Utility AI、Activity、Village World Presentation、Event、Life Episode UI。

### WP-AL-2 — Activity Lifecycle & Daily Integration（活动生命周期与每日接入）

包括 VillageTime Activity Opportunity、Day-start Occupancy Snapshot 接合、Activity Provider Contract、Activity Candidate、Claims / Commitment、Explicit Activity FSM、Live / Headless Simulation Contract、Daily Participation / Recent Activity State。

### WP-AL-3 — Autonomous Selection & Arbitration（自主选择与仲裁）

包括 Eligibility、Explainable Utility、Deterministic Candidate Ordering、Plausibility Cut、Multi-Person Proposal / Arbitration、Decision Trace，以及 4 Person / 3 Place / 5 Activity 测试内容。

### WP-AL-4 — Social Feedback Integration（社会反馈整合）

最终范围见§29：Activity Completion投影、Durable Outbox、Social-owned Interpreter、Historical Reciprocity、Save / Load与pre-next-decision barrier。Recognition / Unified Event / Derived LifeEpisode Runtime或Save不属于WP-AL-4，继续Deferred；旧全链分包设想不作为本包要求。

### WP-AL-5 — Village World Presentation（村庄世界表现）

在 Simulation Contract 稳定后进入：Place / Activity Point 现场表现、Navigation、Character Movement、Interaction Presentation、Base Presence + Activity Overlay、Debug View / Authoring Tooling、正式美术资源绑定入口。

---

## 27. Next Gate（下一闸门）

本 Technical Architecture 审阅通过后，不继续扩架构。下一步形成：

`WP-AL-1 Social Foundation Development Spec（社会域基础开发规格）`

必须明确 Write Scope（写入范围）、asmdef 依赖、Public API（公共接口）、Save Schema 候选、Migration / old-save 行为、Test Matrix（测试矩阵）、Non-goals（非目标）、Acceptance（验收）、Rollback / Compatibility（回退 / 兼容边界）。之后才授权第一个 Runtime Work Package。

<a id="wp-al-3-selection"></a>

## 28. WP-AL-3 Selection & Arbitration — Target Architecture / Confirmed

依据：[Design Checkpoint V0.2 Final](../DesignCheckpoints/2026-10-04_RRPG_WP-AL-3_Autonomous-Selection_Design-Checkpoint-V0.2-Final.md) 与 [Development Spec V0.1](2026-10-04_RRPG_WP-AL-3_Autonomous-Selection_Development-Spec-V0.1.md)。Product Rules 归 [Village GameSpec §12](../../GameSpec/Village/Village-GameSpec-V0.1.md)，本节只负责技术目标与扩展边界。唯一实施基线 main@9c9a750999bcfcb04e9f01c9e2a0d1385f5c67c6；本轮只 Landing，不授权 Runtime，不创建工作簿。

### 28.1 Snapshot / Profile / Desire 输入

Decision Snapshot 是同轮共用、不可变 / read-only 的 Derived Read Model，不是新人物 / 关系 / 世界 Authority。含稳定 DecisionRoundIdentity、DayStamp、Actor Eligibility / Participant Availability、resolved profiles、specific preferences、必要 directed relationships、Context Signals、Recent Activity 及 Provider 输入。构造后 Provider / Evaluation / Arbitration 不再实时查询 mutable World，也不复制整本 Save 数据库。

Resolved Behavior Profile 是只读输入；Governed Behavior Channel 必须有 ChannelId、DisplayName、Description、NeutralValue、Min / Max，禁止临时创造未定义通道。Specific Preference 是独立稀疏 Stable Semantic Key。静态 Prototype 由 PersonDefinitionId 解析画像，PersonId 仍是人物身份；missing / legacy definition 使用定义 Neutral + diagnostic，读取零副作用，不创建 Behavior Save State。完整 Trait / Background / Profession / Formation 来源后置。

Activity Desire Profile 声明 Base Desire、Behavior / Preference / Relationship / Context Bindings 和 Repetition Policy；Engine 不按 Activity 名写分支。Additive Contribution Model：Base + Behavior + Preference + Relationship + Context + Repetition；各 Family 先聚合再 Clamp，并输出 Contribution Breakdown。Context signal 有 Key / SourceDomain / SubjectRole / Magnitude；关系读取 Actor→Target，经 Calibration Adapter 标准化，不改变 Social Affinity Authority。

### 28.2 Plausibility / Ordering / NoMajor

Plausibility Cut 过滤低于阈值的真实活动，非法活动已由 Eligibility 拒绝。NoMajorActivityOption 为 Synthetic Decision Option，StableKey= special.no_major_activity，不经过 Plausibility 阈值过滤；到达它时 Actor 当轮停止主动提案，不 Claim / Commit / FSM / Fact，也不更新 Activity participation。

复用 RRPG.Deterministic.DeterministicRandom.StableHash64，不新建 hash / RNG。DecisionRoundIdentity 的 canonical string 为 village.daily.major|Y|M|D，culture invariant；Selection identity 为 Round|ActorPersonId|CandidateKey|selection。每候选独立映射 u∈(0,1)，Weighted Exponential Race：OrderKey=-ln(u)/Weight，升序，相同 key 用 CandidateKey ordinal。单轮只算一次；新增无关候选不污染其它候选自身随机值，失败后只推进指针。

Weight Response Curve 的具体参数属于 Calibration，不与已确认的 Weighted Ordering 算法分级混淆。

### 28.3 Acceptance / Deterministic Fair Arbitration

Minimal Acceptance 正式接口返回 Accepted / Rejected(reason)，至少 ParticipantUnavailable / InvalidParticipant / HardReject；原型没有 Production HardReject 内容。完整 Target Utility / Invitation / Negotiation Deferred。

所有 Actor 同 Snapshot 独立排序，分轮提交当前 Proposal；Accepted 提案统一按 Claim Conflict 仲裁，Loser / Reject 指针后移，Winner 使用既有 Lifecycle，直到每 Actor 成功、NoMajor、耗尽或显式失败。ProposalIdentity 含 Round / Actor / CandidateKey。独立 arbitration identity：Round|ProposalIdentity|arbitration，经 StableHash64，低 hash 优先，相同 hash 用 ProposalIdentity ordinal。

V0.1 Target 采用 Deterministic Greedy：按该公平优先级接纳全部互斥参与者尚未被 Winner 使用的 Proposal。仲裁不读取 Utility，不按固定人物顺序决定胜者，不做 maximum matching / global optimization，不 reroll。Selection 与 Arbitration 随机身份域隔离。

### 28.4 Daily exactly-once / Fail-closed / Headless

目标新增最小 AutonomousDecisionWorld Authority，只保存 HasLastResolvedDay、LastResolvedYear / Month / Day，表示 Village Daily Major Round 完整处理到哪日，补齐 NoMajor / 无活动结果的同日防重复；不是 Activity History。目标 SaveVersion=1 additive nullable block；Replacement、old save empty、failed Load 保留旧状态、NewGame reset，不保存 Order / Trace / Proposal / RNG / Instance，不 replay 已 resolved 日。

仅有效 Open 后调用未来 Selection Coordinator：必须 Window.Day == 当前 Village Day 且 Daily Phase 有效；MissingPersonSource / stale window / 当日 resolved 不执行。Planning（Snapshot / Provider / Evaluation / Ordering / Acceptance / Arbitration）全部先完成；异常 / 非法结构 catch + structured diagnostic，在任何 Activity Commit 前 fail closed，不标记 resolved，不写 Social，不向 Village Day Tick 外抛，也不隐式补算旧日。

Headless Driver 只调用 WP-AL-2 TryCommitActivity → TryBeginExecution → TryComplete，不拥有第二套 FSM / Claim。Planning 成功后的单个 CommitFailure 必须诊断、不 reroll、不得改变已成功提交历史；继续互不冲突 Winner 或停止剩余 Winner 的确定策略由未来 Implementation Owner 按 Spec §25 选择并测试，不在本次 Landing 擅自新增跨域事务语义。所有 Actor 进入明确终局后才标 resolved。

### 28.5 Module / Config 目标

建议纯域 AutonomousSelection.Runtime 仅依赖 AutonomousActivity.Runtime 与 RRPG.Deterministic.Runtime，无 UnityEngine；不直接依赖 mutable Social / Village / Person / Event / UI。Assembly-CSharp Host 从既有 Authority / StaticConfig 构造 Snapshot、调纯域、调 Lifecycle、接 DecisionWorld / Trace；不另写算法或人物来源。

目标 RRPG_AutonomousLife.xlsx 复用现有 XLSX → RRPG.ConfigCompiler → Generated StaticConfig JSON → Unity Builder → Registry / Generated Database；Generated 不人工编辑。本轮没有创建工作簿或改 Config pipeline。原型 Sheet 与 Gate A–H / causal tests / 10–30日 headless scenario 要求见 Development Spec，属于未来授权实现的验收输入，不是本轮测试结果。

### 28.6 Calibration / Prototype / Deferred

**Calibration / Not Frozen**：Behavior Domain 0..1 / Neutral .5；Weak / Medium / Strong=.35/.65/1；Family Budgets（Behavior ±15、Preference ±20、Relationship ±25、Context ±40、Repetition 0..−25）；Threshold=15；Weight Response x=clamp((score−threshold)/60,0,1)，weight=1+4*x^1.5；NoMajor Weight=.75；RelationshipReferenceMagnitude=20。所有具体映射是调参种子，不是长期产品常量。Final §3.4 的“本轮冻结”按其最终 §25 与制作人本轮明确分级理解，不能提升为正式产品 Freeze。

**Prototype / Test 范围已确认**：张三 .8/.8、李四 .5/.5、王五 .8/.2 + leisure.drink +1、赵六 .2/.8；Home / Tavern / Workshop；Rest / Drink / Talk / VisitSick / Help；Base Desire=22/28/30/18/18。人物值与 Base Desire 仍 Calibration / Not Frozen，不是正式角色平衡 / 村庄建筑。VisitSick 需 Injury Context>0，Help 需 HelpNeed Context>0；HelpNeed 可以 Test Context Fixture，不新建正式 World State、不保存 fixture。

**Deferred**：完整 Personality / Trait、Behavior / Preference Formation、Evolution、Needs / Mood / Habit、更多通道、完整 Target Acceptance Utility、Negotiation / Invitation、Reciprocal Proposal Coalescing、Global optimization / maximum matching、RepeatStreak / 最近N次历史、Life History直接Utility、Activity→Social Fact、Relationship Feedback、Event / Recognition、Life Episode Runtime、Navigation / Animation / World Presentation。

WP-AL-1/2/3均已Published Main；本节记录Selection技术契约，实际实现及Integrity Repair查Current Architecture / WP-AL-3 Closeout。Target / Participant未来扩展与Village / Event世界级Load transaction债务仍保留；Interpreter ownership已由WP-AL-4裁决为Social-owned，但Runtime待实施，见§29。

<a id="wp-al-4-feedback-target"></a>

## 29. WP-AL-4 Social Feedback & Historical Reciprocity — Target Architecture / Confirmed / Not Implemented

依据：[WP-AL-4 Final](../DesignCheckpoints/2026-10-04_RRPG_WP-AL-4_Social-Feedback_Design-Checkpoint-V0.1-Final.md)与[Development Spec V0.1](2026-10-04_RRPG_WP-AL-4_Social-Feedback_Development-Spec-V0.1.md)；产品规则归[Village GameSpec §12.6](../../GameSpec/Village/Village-GameSpec-V0.1.md#wp-al-4-feedback)。唯一Landing基线main@7fcffe5b28be9156a45b95c3981b53ec8b9cac9b；WP-AL-1/2/3已Published Main，本节仅确认后续目标，不改Current实现或开启Runtime。

### 29.1 Ownership与生命周期

Activity / Event只生产客观发生语义，Social Domain单一拥有Production Relationship Interpreter；生产入口目标为SocialRuntimeBridge.CommitRelationalFact(fact)，调用方不选解释器。低层SocialCommitService.Commit(fact, interpreter)仍作纯域 / 测试接缝；Social纯域不引用StaticConfig，Host将Social-owned规则配置投影成纯Rule Catalog。

ActivityCompletionSnapshot是短暂occurrence / projection input；ActivitySocialFactProjector准备0或1持久Fact。RelationalFactRecord / RelationalFactLedger继续唯一canonical persistent关系事实Authority，DirectedRelationshipRegistry继续当前关系Authority。Significant由下游Consumer判断，LifeEpisode为未来Derived View；无Ephemeral Ledger / Significant Ledger / LifeEpisode Ledger。Pending Outbox不是Fact历史，成功删除且不留Delivered副本。

### 29.2 Prepare → Complete → Enqueue → Drain

在Activity mutation前准备投影：查ActivityType mapping / FactType与matching rule存在性，验证Actor / Target，构造已验证payload及稳定SourceOccurrenceId=`activity:<ActivityInstanceId>`（culture invariant）。无persistent mapping为NoFeedback；有映射但非法则Failure，不得Complete。

复用Host lifecycle gateway decorator / equivalent adapter：Prepare成功 → WP-AL-2 TryComplete → Complete成功立即Enqueue已准备payload → Attempt immediate Drain。Failed / Interrupted / NoMajor不形成成功反馈。Complete之后Drain失败不把成功活动改成Failed；Pending / Faulted保留，当前决策轮可完成，下次Snapshot前Barrier必须处理或fail closed。不建立全局2PC / Universal Transaction Manager，不另建Claim / FSM。

### 29.3 Durable Outbox与Delivery

目标Entry仅SourceOccurrenceId、FactType、ActorPersonId、TargetPersonId、Year / Month / Day、Status、FaultCode；Status=Pending | Faulted。保存已投影payload，不保存Completion以供Load后重新解释。

Enqueue先验证：同occurrence同payload幂等existing / no-op；不同payload显式Conflict；非法payload正式mutation前拒绝。Drain调用既有SocialCommitService，Success / Duplicate均ack并remove；CommitInProgress保留Pending；SourceOccurrenceConflict、InvalidFact、InvalidMutation、DuplicatePairMutation、InterpreterFailure转Faulted。Faulted不无限自动retry，提供只读结构化诊断 / Trace，不写Canonical History。

### 29.4 配置与依赖方向

- RRPG_AutonomousLife.xlsx新增ActivitySocialProjection，只表达ActivityType→PersistentRelationalFactType，不存Relationship delta。
- 独立Social Domain工作簿RRPG_Social.xlsx，目标Sheets=00_导航 / RelationalFactType / RelationshipInterpretationRule / SocialCalibration；复用现有XLSX→Compiler→Generated JSON→Unity Builder→StaticConfigRegistry及跨工作簿引用校验。本次不创建任何Workbook、Generated或资产。
- Social规则含RuleId / FactType / SourceRole / TargetRole / RelationshipChannel / Polarity / Strength；V0.1角色Actor | Target，通道Affinity、Positive | Negative、Weak | Medium | Strong。解释器只消费纯规则数据，不按Producer名或Activity名硬编码奖励。
- 目标AutonomousSocialFeedback.Runtime仅依赖AutonomousActivity.Runtime与Social.Runtime，noEngineReferences=true；不依赖Village / Person / Event / UI / UnityEngine。Activity和Social彼此不新增引用，由独立integration domain接合；Host负责Village / Save / StaticConfig接线。

V0.1 Policy：Rest / Drink / Talk无持久Fact；VisitSick→relational.visited_sick_person；Help→relational.helped_person。两规则SourceRole=Fact.Target、TargetRole=Fact.Actor、Affinity Positive；不默认双向。Positive Medium及Weak / Medium / Strong=4/8/12全为Prototype / Calibration / Not Frozen；required / duplicate / finite / >0校验不冻结具体delta。V0.1不做current-affinity curve / diminishing returns。

### 29.5 Save / Load / NewGame目标

SaveData.SocialFeedbackWorld为目标additive nullable顶层区块，只保存Pending / Faulted entries；SocialWorld继续保存Canonical Facts + Relationships，ActivityWorld / AutonomousDecisionWorld原职责不变，SaveVersion=1。所有正式Save带Feedback snapshot，Build deep-copy / stable ordering。

Restore=Replacement，null / legacy missing→empty；invalid或duplicate occurrence DTO preflight失败且保持旧世界；validation先于mutation。SaveSystem新增Feedback preflight并保留已有DecisionWorld preflight，失败不提前清当前Feedback。成功Restore不自动Drain、不replay活动；NewGame清空。此处不解决Village / Event世界级Load transaction，不建立全局2PC。

### 29.6 唯一Daily Barrier与历史互惠验收

目标唯一顺序：VillageState → capture occupiedAtStartOfDay → Projects / completion → Assignments（原快照）→ Resource Settlement → Formation → Drain Social Feedback Barrier → WP-AL-2 Open Window → WP-AL-3 Snapshot / Selection。

任何更早且应影响关系的Feedback在新Snapshot前必须settled。Pending可尝试Drain；未settled / Faulted不打开新Window、不运行Selection，结构化诊断、不向外抛Day Tick异常、不replay旧日期。仍沿VillageTime.TickDay覆盖direct / Update / Jump / Return Catch-up，不新增Clock或特殊Catch-up模拟。

Historical Reciprocity：张三VisitSick王五Completed → Canonical Fact / Social Interpreter → 王五→张三Affinity正向 → Save / Load → 张三NeedsHelp。World A无反馈、World B有反馈，其它输入相同，王五Help张三的Relationship Contribution / Final Desirability / Selection Weight满足B>A；不要求必然选中，不双计raw Fact与Relationship。+8仅Calibration证据，不是产品Rule。

### 29.7 分级、Pending与Deferred

Product职责已归Village GameSpec；以上Projector / Outbox / Interpreter / Config / Save / Barrier / Integration是Confirmed Target，Runtime Not Implemented。Production Interpreter ownership=Decision Resolved / Implementation Pending；现有调用方传Interpreter的Current API尚未改变。

Recognition为未来System / Narrative Recognition，不等于Character Knowledge；Recognition Runtime、Event Integration、LifeEpisode Runtime / Save、History UI、Knowledge / Witness / Rumor、complex significance、saturation / diminishing return、richer Relationship channels、production fault recovery UI均Deferred。未来Canonical Fact可平行提供Relationship Interpreter、Recognition Projector、History Projector→LifeEpisodeView，不新建历史Authority。Village / Event全世界统一Load transaction债务继续保留；WP-AL-5未启动。
