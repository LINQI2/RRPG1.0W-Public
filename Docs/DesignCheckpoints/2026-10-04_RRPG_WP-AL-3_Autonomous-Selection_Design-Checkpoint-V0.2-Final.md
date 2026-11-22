# RRPG｜WP-AL-3 Autonomous Selection & Arbitration（自主选择与仲裁）｜Design Checkpoint V0.2 Final

> 日期：2026-10-04
> 基线：`main@9c9a750999bcfcb04e9f01c9e2a0d1385f5c67c6`
> 状态：**Design Consolidation Final / Ready for Authority Landing**
> 性质：产品与算法设计检查点；不是 Runtime 实现授权本身。
> 原则：**Production Architecture + Prototype Content（正式生产架构 + 原型测试内容）**。
> 本版取代未落仓的 V0.1 工作稿。

---

# 1. 一句话定义

> WP-AL-3 让每个 Person（人物）在同一 Decision Snapshot（决策快照）下，以 Behavior Profile（行为画像）、Specific Preference（特定偏好）、Directional Relationship（有向关系）、Current Context（当前情境）和 Recent Activity（近期活动）形成**可解释但非机械**的 Candidate Order（候选顺序）；多人愿望再经 Minimal Acceptance（最小接受）与 Deterministic Fair Arbitration（确定性公平仲裁）决定哪些进入 WP-AL-2 Activity Lifecycle（活动生命周期）。

WP-AL-3 不负责人物长期塑造，也不接 Social Feedback（社会反馈）。

---

# 2. Product Rules（产品规则）

## 2.1 世界提供可能，人物进行选择

- Place（场所）、Person（人物）、Context（情境）提供 Activity Candidate（活动候选）。
- Provider（提供者）只回答“现在能做什么”。
- Decision System（决策系统）回答“这个人现在更想做什么”。
- Activity Lifecycle（活动生命周期）继续由 WP-AL-2 拥有。

## 2.2 Possibility / Eligibility / Plausibility 三层分离

```text
Possibility（可能性）
= 世界有没有提供

Eligibility（资格）
= 规则上允不允许

Plausibility（合理性）
= 这个具体的人现在是否真的可能想做
```

禁止用极低 Utility（效用）模拟非法行为。

## 2.3 稳定人格 + 动态情境

人物行为必须：

- 相似条件下有稳定倾向；
- Context（情境）足够强时可以覆盖普通习惯；
- 不能“最高分必选”；
- 不能退化为固定脚本。

## 2.4 决策输入按语义分层

V0.1 使用：

- Activity Base Desire（活动基础欲望）
- Behavior Profile（行为画像）
- Specific Preference（特定偏好）
- Directional Relationship（有向关系）
- Current Context（当前情境）
- Recent Activity State（近期活动状态）

禁止合成无语义万能 Modifier Dictionary（修正字典）。

## 2.5 History 不整本扫描进 Utility

History（历史）证明过去发生过什么；Decision（决策）优先读取历史已经形成的 Current State（当前状态）。

禁止同一经历同时通过：

```text
Fact + Relationship + Life Episode
```

重复计权。

## 2.6 Decision System 与 Formation System 分离

WP-AL-3 只读人物当前状态，不修改：

- Behavior Formation（行为塑造）
- Preference Formation（偏好塑造）
- Personality Evolution（人格演化）

## 2.7 NoMajorActivityOption（无主要活动选项）

它是正式决策选项，但不是 Activity。

语义：

> “今天没有特别想主动做的主要活动。”

它：

- 进入 Candidate Order；
- 可以成为冲突失败后的自然停止点；
- 不进入 WP-AL-2 FSM；
- 不产生 Fact；
- 不表示拒绝别人邀请。

## 2.8 Utility 与 Arbitration 分离

- Utility / Desire（效用 / 欲望）决定个人意愿顺序。
- Arbitration（仲裁）只解决意愿冲突。

仲裁不得再次使用 Utility 高低决定 Winner（胜者）。

## 2.9 单轮只生成一次 Candidate Order

冲突失败后：

```text
Candidate Pointer（候选指针）向后移动
```

禁止重新评价、重新随机、无限 reroll（重掷）。

## 2.10 Participant Acceptance（参与者接受）

V0.1 保留正式 Acceptance 接口，但只做：

```text
ParticipantAvailable（参与可用）
+ no Hard Reject（无硬拒绝）
→ Accept
```

不做完整 Target Utility（目标效用）。

## 2.11 多人决策不依赖固定 Person 更新顺序

所有 Eligible Person（合资格人物）先读取同一 Snapshot，形成自己的候选顺序，再统一仲裁。

## 2.12 不做 Global Max-Utility Matching（全局最大效用匹配）

V0.1 采用：

> **Local Person Desire + Fair Conflict Resolution（人物局部意愿 + 公平冲突解决）**

而不是“全村总分最大”的世界优化器。

---

# 3. Person Decision Model（人物决策模型）

## 3.1 Person Continuity（人物连续性）

长期概念分层：

```text
Identity（身份）
Behavior Profile（行为画像）
Specific Preferences（特定偏好）
Relationship Network（关系网络）
Life History（人生历史）
Current Context（当前情境）
```

变化速度不同：

- Context：快
- Relationship / Recent Activity：中
- Preference / Behavior：慢
- Identity / History：极慢或持续累积

## 3.2 Behavior Profile（行为画像）

Decision Runtime 只读 `Resolved Behavior Profile（已解析行为画像）`。

上游未来可以来自：

```text
Base Disposition（基础倾向）
+ Trait / Background（特质 / 背景）
+ Profession Influence（职业影响）
+ Formation Modifier（人生塑造修正）
```

但这些不属于 WP-AL-3。

## 3.3 Behavior Channel（行为通道）

采用：

> **Data-driven but Governed（数据驱动但受治理）**

正式 Definition 至少含：

- `ChannelId（稳定语义ID）`
- `DisplayName（显示名）`
- `Description（语义说明）`
- `NeutralValue（中性值）`
- `Min / Max（合法范围）`

运行世界中禁止现场创造未定义 Channel。

## 3.4 V0.1 Behavior Domain（行为值范围）

本轮冻结为：

```text
[0, 1]
```

其中：

```text
0.0 = 极低
0.5 = Neutral（中性）
1.0 = 极高
```

Missing Channel：

```text
→ 0.5
→ 读取零副作用
```

原因：

- 对策划直观；
- “低”不等于逻辑上的负数状态；
- 运行时可围绕 0.5 中心映射到正负 Contribution（贡献）。

## 3.5 V0.1 正式测试 Behavior Channel

仅两个：

- `social.sociability（社交主动倾向）`
- `social.supportiveness（支持响应倾向）`

其它 Personality / Behavior 维度继续 Deferred（后置）。

## 3.6 Specific Preference（特定偏好）

独立于 Behavior Channel。

使用稀疏 Stable Semantic Key（稳定语义键），例如：

```text
leisure.drink（饮酒偏好）
```

V0.1 Preference Value（偏好值）使用：

```text
[-1, 1]
0 = Neutral
```

Missing Preference → 0，读取零副作用。

---

# 4. Decision Snapshot（决策快照）

同一 Decision Round（决策轮）所有人物读取同一只读 Snapshot。

至少包含：

- `DecisionRoundIdentity（决策轮身份）`
- `ActivityDayStamp（活动日期）`
- Resolved Behavior Profile
- Specific Preferences
- Directional Relationship View
- Context Signals
- Recent Activity State
- Actor Eligibility / Participant Availability
- Provider / Candidate 必要输入

它是 Derived Read Model（派生读模型），不是 Authority（权威）。

禁止复制整个 Save / World 数据库。

---

# 5. DecisionRoundIdentity（决策轮身份）

Village V0.1 每 Activity Day 只有一个主要自主决策轮。

稳定身份使用：

```text
"village.daily.major"
+ Year
+ Month
+ Day
```

它不依赖现实时间、Guid、遍历顺序。

PersonId / CandidateKey / ProposalIdentity 会继续参与 Selection / Arbitration 的独立随机身份。

---

# 6. Activity Desire Profile（活动欲望配置）

Activity 自己声明评价所需输入：

```text
Base Desire（基础欲望）
Behavior Bindings（行为绑定）
Preference Binding（偏好绑定）
Relationship Bindings（关系绑定）
Context Bindings（情境绑定）
Repetition Policy（重复策略）
```

Decision Engine 不为具体 Activity 写：

```text
if (activity == Talk) ...
```

---

# 7. Contribution Model（贡献模型）

## 7.1 Additive Desire（加法欲望）

```text
Final Desirability（最终欲望）
=
Base Desire
+ Behavior Contribution
+ Specific Preference Contribution
+ Relationship Contribution
+ Context Contribution
+ Repetition Contribution
```

## 7.2 Designer Strength（策划影响强度）

V0.1 语义层：

```text
Weak（弱）
Medium（中）
Strong（强）
```

映射系数冻结为第一版 Calibration Seed（调参种子）：

```text
Weak   = 0.35
Medium = 0.65
Strong = 1.00
```

这些仍属于 Calibration（调参）参数，不是长期产品含义。

## 7.3 Family Budget（类别影响预算）

V0.1 调参起点：

| Family | Clamp Budget |
|---|---:|
| Behavior（行为） | ±15 |
| Specific Preference（特定偏好） | ±20 |
| Relationship（关系） | ±25 |
| Context（情境） | ±40 |
| Repetition（重复） | 0 ～ -25 |

同一 Family 内先聚合，再 Clamp（钳制），防止 Context / Modifier 无限堆叠。

相对职责冻结：

- Context：最大短期覆盖能力；
- Relationship：主要区分“更想和谁”；
- Behavior：稳定人物倾向；
- Preference：少量鲜明个性；
- Repetition：扰动机械重复。

## 7.4 Relationship Normalization（关系标准化）

WP-AL-1 `Affinity` 当前没有长期冻结的最终量纲。

WP-AL-3 不改变 Social Authority。

V0.1 通过 Calibration Adapter（调参适配器）把 Affinity 映射为 `[-1,1]`：

```text
normalized = clamp(Affinity / RelationshipReferenceMagnitude, -1, 1)
```

第一代 `RelationshipReferenceMagnitude = 20` 只作为测试 Calibration Seed。

未来 Social 量纲变化只替换 Mapping，不改变 Decision Contract。

## 7.5 Context Magnitude（情境强度）

Context Signal V0.1 统一提供：

```text
[0,1]
```

0 = 无该情境，1 = 当前该信号的强端。

Context Signal 必须声明 SourceDomain / SubjectRole / ValueSemantic。

---

# 8. Plausibility（合理性）

V0.1 使用统一门槛：

```text
PlausibilityThreshold = 15
```

仅作为 Calibration Seed。

规则：

```text
Desirability < Threshold
→ 不进入真实 Activity Candidate Order
```

`NoMajorActivityOption` 不接受该门槛过滤。

---

# 9. Weight Mapping（权重映射）

Desirability ≠ Probability（欲望分数不等于概率）。

V0.1 使用温和饱和的 Power-like（类幂）响应：

```text
x = clamp((Desirability - Threshold) / 60, 0, 1)
Weight = 1 + 4 * x^1.5
```

因此：

```text
刚过门槛 → Weight ≈ 1
高欲望     → Weight 逐渐接近 5
```

高分更常排前，但不会变成最高分必选。

这是 Calibration Seed，可后续调参，不升级为产品规则。

---

# 10. NoMajorActivity Weight（无主要活动权重）

V0.1：

```text
Weight = 0.75
StableKey = "special.no_major_activity"
```

它始终作为一个 Synthetic Decision Option（合成决策选项）存在。

如果排序走到它：

> Actor 当轮停止主动提案。

它不 Claim、不 Commit、不进入 FSM。

---

# 11. Deterministic Weighted Candidate Ordering（确定性加权候选排序）

## 11.1 不使用共享顺序 RNG

禁止：

```text
Random.Next()
Random.Next()
...
```

否则新增无关 Candidate 会污染后续随机值。

## 11.2 复用现有仓库稳定哈希

复用：

```text
RRPG.Deterministic.DeterministicRandom.StableHash64
```

不建立第二套 hash。

## 11.3 Candidate-specific Random Identity

```text
DecisionRoundIdentity
| ActorPersonId
| CandidateKey
| "selection"
```

→ `StableHash64`

→ Candidate-specific `u ∈ (0,1)`。

## 11.4 Weighted Ordering Algorithm

V0.1 采用独立 Weighted Exponential Race（加权指数竞速）：

```text
OrderKey = -ln(u) / Weight
```

按 `OrderKey` 升序形成完整 Candidate Order。

完全相同 Key 时：

```text
CandidateKey ordinal
```

作为稳定 tie-break。

语义：

- Weight 越大越容易排前；
- 每个 Candidate 的随机值独立；
- 不 reroll；
- 同输入可复现；
- 新增无关 Candidate 不会改变其它 Candidate 自己的随机值。

---

# 12. Proposal / Acceptance / Arbitration（提案 / 接受 / 仲裁）

## 12.1 ProposalIdentity（提案身份）

稳定身份至少包含：

```text
DecisionRoundIdentity
ActorPersonId
CandidateKey
```

## 12.2 Minimal Acceptance（最小接受）

返回：

```text
Accepted / Rejected
```

最小 Reject Reason（拒绝原因）：

- `ParticipantUnavailable（参与者不可用）`
- `InvalidParticipant（非法参与者）`
- `HardReject（硬拒绝）`

V0.1 Prototype 没有正式 HardReject 内容；多数社交活动只要参与者可用即接受。

## 12.3 Round-based Arbitration（分轮仲裁）

每一轮：

1. 未解决 Actor 提交当前 Candidate；
2. `NoMajorActivity` 直接结束该 Actor；
3. Social Proposal 先过 Acceptance；
4. Accepted Proposals 构建 Claim Conflict；
5. 使用独立公平优先级；
6. Winner 进入 Commit；
7. Loser / Reject 的 Actor 指针 +1；
8. 下一轮继续，直到成功、NoMajor、或候选耗尽。

## 12.4 Arbitration Priority（仲裁优先级）

不读取 Utility。

使用：

```text
DecisionRoundIdentity
| ProposalIdentity
| "arbitration"
```

→ `StableHash64`

Hash 越小优先级越高。

Hash 相同：

```text
ProposalIdentity ordinal
```

tie-break。

同一 Round 内稳定，不同日期自然变化。

## 12.5 Arbitration Strategy（仲裁策略）

V0.1 使用 Deterministic Greedy（确定性贪心）：

- 按 ArbitrationPriority 排序；
- Proposal 所需 Exclusive Participants 均未被本轮已接受 Proposal 占用 → 接受；
- 否则冲突失败。

不做 maximum matching / global optimization（最大匹配 / 全局优化）。

---

# 13. Decision Round Exactly-once（决策轮只结算一次）

WP-AL-2 已保证 Completed Participant 同日不能再次完成 Activity，但它不能表达：

> “这个人今天已经选择了 NoMajorActivity。”

因此 WP-AL-3 新增最小 Selection Authority：

```text
AutonomousDecisionWorld
```

只持久化：

```text
HasLastResolvedDay
LastResolvedYear
LastResolvedMonth
LastResolvedDay
```

语义：

> Village Daily Major Decision Round 已经完整处理到哪一天。

它不保存：

- Candidate Order
- Utility Trace
- Proposal
- Arbitration
- Runtime RNG
- Activity Instance

SaveVersion 保持 1（additive nullable block）。

Load：
- Replacement；
- failed Load 保留旧 DecisionWorld；
- old save missing → empty；
- 不 replay 当天已经 resolved 的 Decision Round。

NewGame → reset。

这是 WP-AL-3 为 `NoMajorActivity` / 无 Activity 结果补齐 exactly-once 的必要状态，不是 Activity History。

---

# 14. Fail-closed Daily Integration（每日失败关闭）

WP-AL-3 接到 WP-AL-2 Daily Window 后：

```text
Open Window Success
↓
Build Decision Snapshot
↓
Generate Candidates
↓
Evaluate / Order
↓
Acceptance / Arbitration
↓
Decision Plan
↓
Commit / Execute
```

Planning（规划）阶段在任何 Runtime mutation 前完成。

如果 Snapshot / Provider / Evaluation / Arbitration 抛异常或结构非法：

- Catch；
- 返回结构化诊断；
- 不 Commit Activity；
- 不生成 Social Fact；
- 不改变 Relationship；
- 不标记 DecisionWorld resolved；
- Village Day Tick 本身不向外抛异常。

不进行隐式旧日补算。

---

# 15. Headless Execution（无表现执行）

WP-AL-3 V0.1 为了真正更新 WP-AL-2 Recent Activity State，成功 Proposal 使用：

```text
TryCommitActivity
→ TryBeginExecution
→ TryComplete
```

由独立 Headless Driver（无表现驱动器）调用既有 WP-AL-2 Lifecycle。

Selection Runtime 不自己拥有 FSM。

WP-AL-5 可把 Headless Driver 替换为 Live Presentation Driver（实时表现驱动器），但最终仍调用同一 Lifecycle。

WP-AL-3 不生成 Social Fact；该接线属于 WP-AL-4。

---

# 16. Static Authoring（静态制作链）

不新建第二套配置系统。

新增一本：

```text
Development/Config/RRPG_AutonomousLife.xlsx
```

继续走现有：

```text
XLSX 人工 Authority
→ RRPG.ConfigCompiler
→ Development/Generated/StaticConfig JSON
→ Unity Builder
→ StaticConfigRegistry / Generated Database
```

建议最小 Sheets：

- `00_导航`
- `BehaviorChannel`
- `PersonBehavior`
- `PersonPreference`
- `Place`
- `Activity`
- `ActivityProvider`
- `ActivityBehaviorBinding`
- `ActivityPreferenceBinding`
- `ActivityRelationshipBinding`
- `ActivityContextBinding`
- `ActivityRepetition`
- `AutonomousLifeCalibration`

生成物不人工编辑。

---

# 17. Prototype Profile Source（原型人物画像来源）

WP-AL-3 不修改 Person 核心 Authority。

静态人物画像使用：

```text
PersonDefinitionId
```

作为配置 provenance（来源）键：

```text
PersonDefinitionId
→ PersonBehavior rows
→ PersonPreference rows
→ Resolved Behavior Profile / Preferences
```

Runtime Person 仍以 `PersonId` 为唯一人物身份。

`PersonDefinitionId` 不替代 `PersonId`。

Missing / Legacy PersonDefinition：

- Behavior → Channel Neutral；
- Preference → Neutral；
- 结构化 diagnostic；
- 不创建运行时持久状态。

V0.1 Behavior / Preference 是静态派生内容，因此**不新增人物 Behavior Save Schema**。

未来 Formation 修改 Behavior / Preference 时再建立正式长期 Person State Authority。

---

# 18. Prototype Content（原型内容）

## 18.1 四人物

采用：

| Person | Sociability | Supportiveness | Preference |
|---|---:|---:|---|
| 张三 | 0.8 | 0.8 | 无 |
| 李四 | 0.5 | 0.5 | 无 |
| 王五 | 0.8 | 0.2 | `leisure.drink = +1.0` |
| 赵六 | 0.2 | 0.8 | 无 |

**本版确认赵六 Supportiveness 从旧候选“中”调整为“高”。**

## 18.2 三场所

确认：

1. `Home（住所）`
2. `Tavern（酒馆 / 公共场所）`
3. `Workshop（工作场所）`

**第三 Place 从旧 Rest / Medical 候选调整为 Workshop。**

## 18.3 五活动

- Rest（休息）
- Drink / Leisure（饮酒 / 消遣）
- Talk（交谈）
- Visit Sick（探望伤员）
- Help（帮助）

可用第一版 Calibration Seed：

| Activity | Base |
|---|---:|
| Rest | 22 |
| Drink | 28 |
| Talk | 30 |
| Visit Sick | 18 |
| Help | 18 |

仅为调试起点。

---

# 19. Prototype Bindings（原型绑定）

## Rest
- Base 为主
- Repetition = Weak Negative

## Drink
- Sociability = Weak Positive
- `leisure.drink` = Strong Positive
- Repetition = Medium / Strong Negative

## Talk
- Sociability = Medium Positive
- Actor→Target Affinity = Medium Positive
- Repetition = Medium Negative

## Visit Sick
- Supportiveness = Medium Positive
- Actor→Target Affinity = Weak / Medium Positive
- Target Injury Severity = Strong Positive
- Repetition = Weak Negative

## Help
- Supportiveness = Medium Positive
- Actor→Target Affinity = Medium Positive
- Target HelpNeed Severity = Strong Positive

---

# 20. Prototype Context 边界

- `Target.InjurySeverity` 可以通过测试 Adapter / 后续真实 Person Injury Adapter 提供；
- `Target.HelpNeedSeverity` V0.1 可以由 Test Context Fixture（测试情境夹具）提供；
- 不为 HelpNeed 提前建立新的正式世界状态 Authority；
- Context Fixture 不进入正式 Save。

---

# 21. Validation Scenario（纵切验证）

必须覆盖：

1. Person Difference（人物差异）
2. Relationship Differentiation（关系差异）
3. Context Override（情境覆盖）
4. Repetition Control（重复控制）
5. Conflict / Arbitration（冲突 / 仲裁）
6. NoMajorActivity（无主要活动）
7. Save / Load Decision exactly-once
8. Deterministic Ordering（确定性排序）
9. Selection / Arbitration Random Domain 隔离
10. Explainability（可解释性）

WP-AL-4 再补：

11. Historical Reciprocity（历史互惠）

---

# 22. Decision Trace（决策轨迹）

至少可追踪：

- Actor
- Candidate
- Provider
- Eligibility
- Base Desire
- Behavior Contribution
- Preference Contribution
- Relationship Contribution
- Context Contribution
- Repetition Contribution
- Final Desirability
- Plausibility Result
- Selection Weight
- Candidate-specific Random Identity
- Candidate Order
- Acceptance Result / Reason
- Arbitration Priority / Result
- Commit Result
- Final Activity Result / NoMajor

Trace 是 Debug 派生数据，不保存为世界历史。

---

# 23. Deferred（后置）

不进入 WP-AL-3：

- 完整 Personality / Trait Runtime
- Behavior / Preference Formation
- Personality Evolution
- Needs / Mood / Habit
- RiskTaking / Curiosity 等更多正式 Behavior Channels
- Target Acceptance Utility
- Negotiation / Invitation
- Reciprocal Proposal Coalescing
- Global Max Utility / Maximum Matching
- RepeatStreak / 最近 N 次行为历史
- Life History 直接参与 Utility
- Activity → Social Fact
- Relationship Feedback
- Recognition / Event
- Life Episode Runtime
- Navigation / Animation / Village World Presentation

---

# 24. Autonomous Life Design Manual（自主生活设计手册）

在 WP-AL-4 主闭环稳定后制作。

它不是 Authority，而是给制作人 / 策划 / 新团队成员使用的解释手册。

按问题组织：

- 怎么新增 Activity？
- 怎么让人物更喜欢某类活动？
- 怎么让关系影响“找谁”？
- 怎么让重伤触发探望？
- 为什么人物总重复？
- 为什么张三没去看王五？
- 为什么第一愿望失败后去做了第二件事？
- 一次经历怎样影响未来行为？

---

# 25. Final Freeze Classification（最终分级）

## Product Rule / Confirmed
- Possibility / Eligibility / Plausibility 分层
- Person 行为稳定但非机械
- NoMajorActivity 正式存在
- Utility 与 Arbitration 分离
- 单轮不 reroll
- Minimal Acceptance 接口
- Round-based Arbitration
- 不使用固定 Person 顺序
- 不做全局最大效用匹配
- Decision / Formation 分离
- History 不整本直接参与 Utility

## Target Architecture / Confirmed
- Decision Snapshot
- Resolved Behavior Profile
- Governed Behavior Channel
- Activity Desire Profile
- Additive Contribution Breakdown
- Candidate-specific deterministic random
- Selection / Arbitration random domain 分离
- DecisionWorld exactly-once
- Static Config authoring chain
- Headless Driver → WP-AL-2 Lifecycle

## Calibration Candidate
- `[0,1]` Behavior Domain
- Weak/Medium/Strong = .35/.65/1
- Family Budgets
- Threshold = 15
- Power response formula
- NoMajor Weight = .75
- RelationshipReferenceMagnitude = 20
- Base Desire seeds
- 4 Person concrete numbers

## Deferred
见 §23。

---

# 26. 下一步

本文件与 WP-AL-3 Development Spec V0.1 一起落仓。

之后：

```text
Authority Landing
→ review/wp-al-3
→ Runtime Implementation
→ Local Semantic Self-Review
→ Push Review Branch
→ GitHub Real Commit Review
→ main Publication
```
