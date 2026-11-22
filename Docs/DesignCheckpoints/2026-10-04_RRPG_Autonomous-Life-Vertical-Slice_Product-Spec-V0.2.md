# RRPG Autonomous Life Vertical Slice V0.1 — Product Spec

> 日期：2026-10-04
> 项目：RRPG
> 类型：Vertical Slice Product Spec Candidate（纵切产品规格候选）
> 状态：Producer Approved（制作人已批准）
> 目的：定义 Autonomous Life（自主生活）第一代正式架构纵切的产品目标、架构边界、原型内容、验收要求、候选算法与现有工程兼容约束。
> 重要原则：**Production Architecture + Prototype Content（正式生产架构 + 原型测试内容）**。内容可以少、数值可以测试化、美术可以临时，但核心职责、数据流、生命周期、Authority（权威）与扩展方向不能采用未来要丢弃的一次性测试结构。
> 本文件不是 Active GameSpec；制作人批准后，确认项才可归位 Current Design，并将可执行规则同步至 GameSpec。

---

## 1. Product Goal（产品目标）

Autonomous Life V0.1 不追求完整《The Sims》级生活模拟。

它只验证一个核心幻想：

> **一个小村庄里的具体 Person（人物），即使玩家没有逐条下令，也会因为“自己是谁、和谁有怎样的关系、当前发生了什么”而选择不同活动；这些真实活动留下 Fact（事实），Fact 改变关系与未来行为，从而让人物的生活具有连续因果。**

第一代纵切只需要证明：

1. 人物存在稳定但不机械的行为差异；
2. 人物会区分不同 Target Person（目标人物）；
3. 当前情境可以覆盖一般偏好；
4. 过去互动能够改变未来自主行为；
5. 多人物互动在 Runtime 中可以稳定共存；
6. 复杂自主行为仍然可解释、可调试；
7. 重大事实可以被进一步表达为可理解的历史片段。

---

## 2. Vertical Slice Definition（纵切定义）

RRPG 的 Prototype（原型）不是“未来会全部丢弃的测试系统”。

本纵切采用：

> **Final Architecture Slice（正式架构的一小块）**

而不是：

> Test Architecture → Later Rewrite（测试架构 → 以后重做）

未来扩展应主要发生为内容与模型深化，而不是删除原型系统重做正式系统。

---

## 3. Success Gates（成功门槛）

### Gate A — Autonomous Behavior Feedback Loop（自主行为反馈闭环）

必须验证：

```text
Person Difference
+ Relationship
+ Context
↓
Activity Selection
↓
真实 Activity Execution
↓
Relational / Semantic Fact
↓
Relationship Interpreter
↓
Relationship State 变化
↓
Future Activity Selection 变化
```

Gate A 是 V0.1 的核心成功条件。

### Gate B — Historical Representation（历史表达）

独立验证：

```text
Significant Fact
↓
Life Episode（人生经历片段，候选）
↓
History View 可读取
```

Gate B 不允许成为 Gate A 的技术前置依赖。

---

## 4. Freeze Classification（冻结分级）

### 4.1 Proposed Confirmed Architecture（建议归位为 Confirmed Architecture）

1. **Production Architecture + Prototype Content**
2. **World Provides Possibilities, Person Chooses**
3. **Selection ≠ Execution**
4. **Unified Activity Lifecycle**
5. **Activity → Fact → Interpreter → Derived State**
6. **Directional Relationship Foundation**
7. **Simulation Authority ≠ World Presentation**
8. **Debug Explainability**
9. **Semantic History, Not Technical Logging**
10. **No Parallel Authority**

### 4.2 Vertical Slice Requirements（纵切必须满足）

- 人物选择必须同时受 Person-specific Behavioral Difference（人物级行为差异）、Relationship（关系）、Current Context（当前情境）影响。
- 同一人物应表现稳定倾向，但不能完全脚本化。
- 情境必须能够显著改变一般行为倾向。
- 过去互动必须真实改变未来 Activity Selection。
- 多人物争抢同一互动对象时必须存在正式冲突处理。
- Activity Failed / Interrupted 不得冒充 Completed。
- 随机选择必须可调试、可复现。
- Save / Load 后，能够影响未来行为的长期 Relationship State 必须继续成立。
- Debug 层必须能展示候选、被 Gate 排除的行为、主要评分原因、当前 Activity State 与冲突/预留情况。

### 4.3 Supporting Candidate（强候选，不冻结）

- `Utility AI（效用型 AI）`
- `Weighted Random（加权随机）`
- `HFSM（Hierarchical Finite State Machine，分层有限状态机）`
- `BT（Behavior Tree，行为树）`
- `Target Reservation（目标预留）` 的具体实现结构
- `Sociability（社交主动倾向）`
- `Supportiveness（支持响应倾向）`
- 单维 `Affinity（关系亲近倾向）`
- `Life Episode（人生经历片段）`
- `Directional Relationship State Persistence（方向关系状态直接持久化）`：当前技术强候选；V0.1 不倾向依赖全历史 Replay 重建当前关系
- `Life Episode as Derived Projection（Life Episode 作为派生投影）`：当前技术强候选；除非后续证明需要稳定独立身份或独立玩法权威，否则不默认新增持久化 Ledger
- Top-N / Candidate Cut / Response Curve 等具体效用选择算法细节

### 4.4 Deferred（主动后置）

GOAP、HTN、LLM 作为模拟权威、完整 Needs、完整 Mood、Habit、家庭、恋爱/婚姻、生育、年龄/完整生命周期、Personality Formation、多维 Relationship 正式模型、长期个人 Goal、大规模社会网络传播、Pattern → Life Episode 正式聚合算法、群体大型 Activity、完整 24 小时时间模拟。

### 4.5 Rejected / Replaced（否决）

- Prototype-only Gameplay Authority
- Case-specific Hardcode
- Activity 直接修改长期 Relationship State
- World Presentation / Animation 直接作为业务完成真相
- Permanent Fixed Decision Order 造成长期优先权偏差
- 以 BattleUnitId / CardId / 临时角色实例代替 PersonId 建立长期状态
- 为 Autonomous Life 新建第二套 Village Clock
- 为 Autonomous Life 新建第二套 Occupancy 真相源
- 为 Autonomous Life 新建独立第二套 Fact / History Ledger

---

## 5. V0.1 Prototype Content（原型测试内容）

### 5.1 Person（人物）

4 个测试 Person：

| Person | Sociability（社交） | Supportiveness（支持响应） | 测试偏好 |
|---|---|---|---|
| A / 张三 | 高 | 高 | 无 |
| B / 王五 | 高 | 低 | Drink / Leisure 较高 |
| C / 赵六 | 低 | 中 | 无 |
| D / 李四 | 中 | 中 | 无 |

这些值全部是 Prototype Content，不是正式 Personality Model。

### 5.2 Relationship（关系）

V0.1 使用一个薄层、方向性关系候选：

`Affinity(A → B)`

数值尺度、正式字段、关系等级均 Not Frozen。

### 5.3 Place（场所）

1. `Home（住宅 / 休息点）`
2. `Tavern / Common Place（酒馆 / 公共场所）`
3. `Rest / Medical Place（休息 / 医疗承载点）`

必须验证：
- Place 可提供 Activity Offering；
- Place 可提供 Activity Point；
- Place 可被 Navigation 解析为目的地；
- Place Availability / Capacity 有正式扩展入口。

### 5.4 Activity（活动）

1. `Rest（休息）`
2. `Drink / Leisure（饮酒 / 消遣测试活动）`
3. `Talk（交谈）`
4. `Visit Sick Person（探望伤员）`
5. `Help（帮助）`

---

## 6. Unified Activity Lifecycle（统一 Activity 生命周期）

```text
Available
↓
Selected
↓
Reserved（如果需要资源/Target）
↓
Committed
↓
Executing
↓
Completed / Failed / Interrupted
↓
Activity Result
```

禁止不同 Activity 各自定义互不兼容的生命周期。

---

## 7. Free Activity Block（自由活动时间块）

V0.1 不实现完整 24 小时作息。

只需要一个明确的 `Free Activity Block（自由活动时间块）`：

- 每名 Eligible Person 在一次 Block 中最多进行 1 个主要自主 Activity；
- 被更高优先级 Commitment / Occupancy 占用的人不进入自主选择；
- Game Time 与现实动画播放时间分离；
- Block 结束后进入下一轮正式世界推进。

具体小时、分钟、Block 长度 Not Frozen。

---

## 8. Runtime Decision Flow（运行决策流程）

```text
Free Activity Block Start
↓
Check Person Availability
↓
Generate Activity Candidates
↓
Eligibility Gate
↓
Priority Resolution
↓
Evaluate Relative Desire / Utility
↓
Candidate Filtering
↓
Selection
↓
Target / Resource Conflict Resolution
↓
Commit Activity
↓
Navigation / Execution
↓
Completion Check
↓
Activity Result
↓
Fact
↓
Interpreter / Derived State
↓
Persist
↓
Block End
```

---

## 9. Activity Contract V0.1（活动契约）

| Activity | Provider | Priority | Eligibility | 主要行为输入 | Completion | Immediate Effect | History Output |
|---|---|---|---|---|---|---|---|
| Rest | Home / Rest Point | P3 | 有可用休息点；Person 可自主 | Base + Recent Repetition | 到达并完成逻辑活动段 | V0.1 不建立 Fatigue | Recent Activity |
| Drink / Leisure | Tavern / Common Place | P3 | 场所可用、有容量 | Base Preference + Sociability + Recent Repetition | 完成一次消遣 | 不建立醉酒/健康系统 | ActivityCompletionSnapshot（ephemeral；无持久关系Fact） |
| Talk | Target Person | P3 | Target 可互动、可到达 | Sociability + Actor→Target Relationship + Context + Repeat | 双方完成交谈段 | 无必须即时世界效果 | ActivityCompletionSnapshot（ephemeral；Talk无持久关系Fact） |
| Visit Sick | Injured Person / Place | P2 | Target Injured 且可探望 | Supportiveness + Relationship + Injury Context + Repeat | 到达并完成探望 | 不直接治疗 | Canonical RelationalFactRecord（Significance为下游投影） |
| Help | Person / Context | P2 | Target 存在 Needs Assistance 情境 | Supportiveness + Relationship + Context Urgency + Repeat | 成功解除/改善帮助情境 | 改变一个真实 World Context | Canonical RelationalFactRecord（Significance为下游投影） |

数值全部 Not Frozen。

---

## 10. Personality / Behavior Difference（人物行为差异）

V0.1 只要求：

> Person 存在稳定、可解释、可配置的行为偏置。

当前最小候选：
- `Sociability（社交主动倾向）`
- `Supportiveness（支持响应倾向）`

禁止把它们实现为具体 Person 名称判断 + Case Hardcode。

---

## 11. Directional Relationship（有方向关系）

```text
Relationship(A → B)
≠
Relationship(B → A)
```

Activity Selection 对某个 Target 的关系输入应读取 Actor → Target。

Relationship Interpreter 在关系行为完成后，可以依据 Role 产生不同变化。

---

## 12. Relationship Feedback Boundary（关系反馈边界）

```text
Activity Completed
↓
Relational Fact
↓
Relationship Interpreter
↓
Directional Relationship State
↓
Future Activity Selection
```

Activity 不直接承担长期关系解释职责。

---

## 13. Relationship Double-counting Rule（防重复计权）

V0.1 Activity Selection 不直接同时读取：
- 当前 Relationship State
- 同一条 Relational Fact
- 同一段 Shared Life Episode

V0.1 选择层主要读取当前 Directional Relationship State。

---

## 14. Fact Boundary（事实边界）

`Fact（事实）` 表示已经真实发生、具有世界语义、可被未来系统或玩家理解的事情。

不是导航距离、动画帧、Transform、技术日志。

WP-AL-4 Final对旧三类术语作生命周期归位：

- ActivityCompletionSnapshot = ephemeral occurrence / projection input；不建立Ephemeral Ledger。
- RelationalFactRecord = canonical persistent relational fact；只保留唯一RelationalFactLedger。
- Significant / Life Episode = 下游Consumer投影，不是Producer字段或第三套Fact Authority。

Rest / Drink / Talk不进入持久关系Fact Ledger；VisitSick / Help分别映射relational.visited_sick_person / relational.helped_person。Talk V0.1不自动增加长期关系；两类支持行为反馈为Fact.Target→Fact.Actor正向。具体Affinity delta为Calibration / Not Frozen；旧活动矩阵中的Significant不是生产时重大标记或Episode实现要求。现行规则见[Village GameSpec §12.6](../../GameSpec/Village/Village-GameSpec-V0.1.md#wp-al-4-feedback)，裁决见[WP-AL-4 Final](2026-10-04_RRPG_WP-AL-4_Social-Feedback_Design-Checkpoint-V0.1-Final.md)。

---

## 15. Life Episode（人生经历片段）候选

`Life Episode` 继续是 Supporting Candidate / Not Frozen。

V0.1 只验证：

```text
Significant Relational Fact
↓
Shared Life Episode
↓
History View 可读取
```

要求：
- 不覆盖 V3 当前 `Experience（客观经历记录）` 语义；
- 若建立 Shared Life Episode，必须支持同一 Canonical Episode 被多 Person 以不同 Role 引用；
- 不建立每人一份互相独立的平行真相；
- 不要求 Pattern 聚合进入 V0.1 核心反馈链。

---

## 16. Conflict / Reservation Requirement（多人冲突要求）

V0.1 必须证明：
- 不产生非法重复占用；
- 失败者可以重新决策；
- 不永久卡死；
- 不无限重试；
- 不因为固定 Update 顺序导致同一 Person 永远先抢到 Target。

具体 `Reservation（预留）` 结构仍为 Supporting Candidate。

---

## 17. Decision Order（决策顺序）

产品要求：

> 决策顺序不能产生长期固定优先权偏差。

候选实现：
- Round-robin（轮换）
- Seeded Shuffle（带随机种子的洗牌）
- Proposal / Arbitration（提案 / 仲裁）

---

## 18. Navigation / Execution Boundary（寻路 / 执行边界）

V0.1 必须验证：
- 人物可到达正式 Activity Point；
- 不穿不可通过障碍；
- 无法到达时能够 Fail；
- Target / Place 失效时能够终止；
- 不允许用直接瞬移替代主验证链。

后置高级拥堵、道路偏好、复杂避让、交通模拟。

---

## 19. Completion Authority（完成权威）

Activity 是否 Completed 必须由 Activity 自己的逻辑条件决定。

动画播完 ≠ 一定 Completed。

只有 Completed Activity 才产生其成功 Fact。

---

## 20. World Presentation（世界表现）

V0.1 必须实际让玩家看到：
- 4 个可辨认 Person；
- 3 个不同 Place；
- Person 在世界中移动；
- Person 到达 Activity Point；
- 基础互动表现；
- 受伤 / 需要帮助等 Context 可识别；
- 谁正在和谁互动可以理解。

允许临时资源，但必须通过正式资源绑定 / 配置入口进入。

---

## 21. Debug Explainability（行为可解释性）

至少能够查看：
- 当前 Person
- 当前 Selected Activity
- Available Candidates
- 被 Eligibility Gate 排除的候选与原因
- 主要行为输入 / Modifier
- 最终相对分值 / 优先结果
- 当前 Target / Reservation
- Activity Lifecycle State
- Recent Repetition 信息
- 决策随机 Seed / 可复现标识（若实现采用随机）

---

## 22. Product Test Cases（产品测试案例）

### Test 1 — Personality Differentiation
相同世界/关系条件下，不同 Person 的稳定行为偏置必须产生可观察差异。

### Test 2 — Relationship Differentiation
同一 Actor、不同 Target、不同 Actor→Target Relationship 下，关系更深的 Target 应有更高互动倾向。

### Test 3 — Context Override
低社交 Person 面对关系亲近且 Injured 的 Target，Visit / Help 倾向应显著高于普通状态。

### Test 4 — Historical Reciprocity
李四受伤 → 张三探望 → Canonical Relational Fact（Significance下游投影）→ 李四→张三关系改善；之后张三受伤/需要帮助时，李四的 Visit / Help 倾向显著提高。

### Test 5 — Repetition Control
高 Drink Preference 的 Person 仍应最常去 Common Place，但不能每个 Block 永远重复同一活动。

### Test 6 — Target Conflict
多个 Person 同时争抢同一 Target 时，不重复占用、不死锁、不无限重试、不长期固定同一 Winner。

### Test 7 — Explainability
Developer / AI 能回答“为什么这个 Person 选择了这个 Activity”。

### Test 8 — Persistence / World Isolation（持久化 / 世界隔离）
验证：
- 同一世界 Save → Load 后，Relationship Feedback 仍然成立；
- NewGame 或切换到不含该状态的存档后，不残留上一世界的 Relationship / Fact / Active Activity。

---

## 23. Validation Model（验证方式）

### Deterministic Debug Test（确定性调试）
验证 Candidate、Modifier、关系变化、Activity Lifecycle。

### Batch Simulation Test（批量模拟）
验证人物差异、关系反馈、重复控制、Context 影响。

V0.1 暂不冻结正式概率阈值。

---

## 24. Existing Runtime Compatibility Constraints（现有 Runtime 兼容约束）

### 24.1 Person Identity（人物身份）
现有长期人物身份以 `PersonId` 为入口。Actor、Target、Relationship Pair、Fact Participant、Life History Owner 不得以 BattleUnitId / CardId / 临时角色实例代替。

### 24.2 Village Time（村庄时间）
现有 `VillageTime` 是村庄日推进真相源。Free Activity Block 必须接入 / 扩展既有正式 Village 时间语义，禁止另建第二套时钟。

### 24.3 Presence / Occupancy（在场 / 占用）
Autonomous Life 必须先读取正式 Occupancy / Availability。Activity 的 Reserved / Committed / Executing 状态应与现有占用模型合法集成或成为其下级状态，不建立竞争性的 `AutonomousBusy` 真相源。

### 24.4 Existing History Authorities（现有历史权威）
2026-10-04 只读调查已经确认：当前三个 History / Ledger 的语义、所有者、键与保存接缝不同，不能按名字自动合并，也不能任意选择一个作为 Autonomous Life 通用 Fact Ledger。

#### `VillageSocialFactLedger（村庄社会事实账本）`
当前只记录 Person 成功投入 Active Project 后产生的 `TemporaryCommitment（临时承诺）`事实；Identity Key 为 `(PersonId, ProjectId)`；被 V3 Social Response 用于年内计数 → Recognition / Pending；保存于 `SaveData.Village.Social.Facts[]`。它是当前 TemporaryCommitment Fact 的 Source of Record，但不是全部自主生活社会事实的统一 Source of Truth。

因此，不得直接假设 `Relational Fact（关系事实）` 应写入该 Ledger。若未来扩展其 Authority，必须显式设计新的 Fact 语义、键、查询面、保存兼容与消费者迁移。

#### `WorldEventHistory（世界事件历史）`
当前按 `EventId` 保存累计发生次数、最近 Village 日序、最近 EventInstanceId 等事件发生汇总，被 Event 条件、OncePerWorld / 冷却、执行 Trace 使用，并已真实进入 `SaveData.EventWorld.History[]` 持久化。它是 Event occurrence / repeat / cooldown 的正式 Authority，但不是逐次完整世界事实日志，也不是通用 Fact Ledger。

因此，Autonomous Life 的普通 Relational Fact 不应因为“也属于 History”而直接进入 `WorldEventHistory`。旧 `WorldEventHistory.cs` 中“只读 + 测试种子 / 不进 Save”的 1B 注释已落后于当前真实 Runtime / Save 链。

#### `PersonLifeHistory（人物人生历史）`
当前是 Person-owned 的长期人生事实 Source of Record，正式类型为 `ProfessionFormed（职业形成）` / `ExpeditionReturned（远征归来）`，保存于 `SaveData.Player.Characters[].LifeHistory.Events[]`。当前没有跨人物共享的 FactId / SharedEpisodeId，也不能假定已覆盖 Hero、Relationship 或 Shared Episode。

因此，`PersonLifeHistory` 可以成为未来人物历史的查询 / 投影入口候选之一，但不得在没有共享身份契约时把同一关系事实复制成多份互不关联的平行真相。

### 24.5 History Authority Mapping Gate（历史权威映射闸门）
在实现任何新的 Relational Fact / Life Episode Runtime 之前，必须完成技术裁决：
1. 新 Relational Fact 的 Canonical Source of Truth 由谁承担；
2. 是扩展现有 Authority、正式迁移 Authority，还是新增一个职责明确且不竞争的上层结构；
3. Producer / Consumer / Identity Key / Lifetime / Query Surface / Save Owner / Save Container 是什么；
4. 与 `VillageSocialFactLedger`、`WorldEventHistory`、`PersonLifeHistory` 的关系是什么；
5. 如何避免同一事实在多个容器中成为互相无法关联的独立权威。

> **No Parallel Authority（禁止平行权威） ≠ Force One Ledger（强行单一账本）。**

允许多个不同职责的 Authority 长期共存，只要语义、所有权、数据流和跨层引用清楚。

### 24.6 Event（事件）
Activity Fact 与 Event 内容可以连接，但不能因为共用 History 就合并为同一 Authority。产品边界继续保持：

```text
Fact / Pattern / Recognition / State
↓
Event Rule
↓
Event
```

Event 是历史的消费者 / 显化出口之一，不自动成为所有底层事实的保存位置。

### 24.7 Save（存档）
长期 Relationship 若会改变未来行为，必须满足 Save / Load 连续性。

V0.1 技术强候选：当前 Directional Relationship State 应作为长期 Gameplay State 被直接持久化，或拥有等价的正式重建保证；V0.1 不优先依赖“重放所有 History”恢复当前关系。

具体 Save Container / DTO / SaveVersion / Migration 仍 Not Frozen。

### 24.8 Life Episode Persistence（人生经历片段持久化）
当前优先候选：`Life Episode（人生经历片段）` 作为 Canonical Fact / Existing History 上的 Derived Projection（派生投影），而不是默认新增一个独立持久化 Source of Truth。

只有后续证明需要稳定 EpisodeId、独立玩法状态、共享引用或重建成本不可接受时，才考虑升为独立持久化 Authority。

### 24.9 World-State Reset / Load Isolation（世界状态重置 / 加载隔离）
第十三批静态调查发现两个生命周期风险线索：
- 整个 Village Save 区块缺失时，已初始化会话内加载旧档是否可能保留已有 `VillageSocialFactLedger`，尚未动态验证；
- NewGame 当前只明确新建 PlayerCollection，World Host 的统一重置责任尚未完成动态验证。

Autonomous Life 引入长期 Relationship / Fact / Recent State 后，正式验收必须覆盖：

```text
World A 产生长期关系 / Fact
↓
New Game 或 Load 另一份世界状态
↓
不得残留不属于目标世界的 Relationship / Fact / Activity 状态
```

### 24.10 Town / World / Scene（世界展示）
新 Autonomous Life World Presentation 不得依赖已退休历史 Stage 写入器，不得假设旧场景根路径，必须基于实施时当前 Scene / Architecture 事实接入。


## 25. Persistence Requirement（持久化要求）

必须长期保存或具有正式、可验证的重建保证：
- 对未来 Activity Selection 有影响的 Directional Relationship；
- 对其因果成立所必需的世界事实 / 派生状态；
- Gate B 若验证 Life Episode，则其可读历史必须跨 Save / Load 保持一致。

V0.1 还必须验证两类相反方向：
1. **Continuity（连续性）**：同一世界 Save → Load 后，过去形成的关系反馈仍影响未来行为；
2. **Isolation（隔离性）**：NewGame / 切换到不含这些状态的存档后，不得残留上一世界的关系、事实或 Activity 状态。

具体存储位置、DTO、版本迁移与是否缓存 Life Episode 不在 Product Spec 冻结。

---

## 26. Anti-overengineering Rule（反过度设计）

禁止因为未来“可能需要”而提前创建：
- 12 个 Personality 维度；
- 10 个 Relationship 维度；
- 家庭数据；
- 情绪图谱；
- Goal Planner；
- LLM Agent；
- 社会网络传播；
- 未验证的大型抽象 Framework。

正式架构只要求当前职责边界正确，并且未来合理扩展不要求推翻现有核心职责。

---

## 27. Parallel Development Boundary（并行开发边界）

### 开发15.0
负责 Autonomous Life 产品设计、Freeze 分级、Product Spec、后续 Current Design / GameSpec 规则准备。

### 本地工程优化 Codex
当前优先：
- A：拟改对象引用清单审计
- B：Battle 内部对象用途 / 命名只读调查

暂缓 C–E 实际写入，直到新 Product Spec 与共享接口影响面进一步明确。

### Shared Danger Zones（共享危险区）
- Person
- Village
- Event
- Save
- GameDesign/当前设计状态.md
- GameSpec/Person
- GameSpec/Village
- Architecture / Status / Handoff 中央文档
- SampleScene / Prefab / Unity Editor

共享 Authority 同一时段必须 Single Writer（单写入者）。

---

## 28. Implementation Gate（实施闸门）

Autonomous Life Implementation Codex 启动前必须同时满足：

### Product Gate
制作人批准本 Product Spec。

### Design Authority Gate
确认项归位 Current Design；涉及可执行规则的内容同步 Active GameSpec。

### History Authority Mapping Gate
在新建 / 扩展任何 Relational Fact、Relationship Persistence 或 Life Episode Runtime 前，完成现有三套 History Authority 的技术裁决与唯一权威边界设计。不得因为名字相似直接合并，也不得直接创建第二套通用 Ledger。

### Engineering Baseline Gate
当前本地工程优化完成审计、验证、Commit、Push，形成新的干净可复现基线 SHA。

### Worktree / Ownership Gate
多执行 Agent 并行时采用独立 Worktree / Branch、明确 Write Scope、Unity Editor / Test Runner 串行、共享文件 Single Writer。

---

## 29. Prototype Scope + Acceptance Matrix（原型范围 + 验收矩阵）

| 模块 | 正式架构要求 | V0.1 内容 | 验收 | 后置 |
|---|---|---|---|---|
| Person | 稳定 PersonId / Activity 接口 | 4 Person | 跨 Block 身份连续 | 正式人口生成 |
| Behavior Difference | 可配置行为偏置输入 | 2 候选轴 | 批量行为差异明显 | 完整 Personality |
| Relationship | Directional Foundation + Interpreter | 单维 Affinity 候选 | A→B / B→A 不同且影响未来 | 多维关系 |
| Place | Activity Provider + Point | 3 Place | 世界提供活动 | 正式建筑系统 |
| Activity | 统一 Contract / 生命周期 | 5 Activity | 无 Case 旁路 | 更多 Activity |
| Candidate | 统一候选生成职责 | Place / Person / Context | 世界变化改变候选 | 复杂 Opportunity |
| Selection | 可解释、非机械、可复现 | Utility 候选模型 | 8 个 Test Cases | 正式曲线 |
| Conflict | 正式冲突处理职责 | Person Target | 不冲突/不死锁 | 群体 Activity |
| Navigation | Activity→Destination→Arrival | 简单地图 | 可到达/Fail/Interrupt | 高级避障 |
| Fact | 统一语义入口 | 少量 Fact | 可追踪来源 | 完整 Fact Family |
| Relationship Update | Canonical Fact→Social-owned Interpreter | VisitSick / Help正向；Talk无持久反馈 | Target→Actor及历史互惠；delta Not Frozen | 复杂解释 |
| History | 候选高层表达 | 1 Shared Life Episode | History View 可读 | Pattern 聚合 |
| Debug | 正式 Explainability 接口 | 粗糙面板 | 能回答 why | 正式工具 UI |
| Art | 正式资源绑定 | 临时资源 | 可直接替换正式素材 | 正式生产 |
| Save | 唯一 Save Authority | 最小长期状态 | Load 后反馈连续；NewGame / 切档无世界状态残留 | 最终 Schema / Migration |

---

## 30. Open Questions（未冻结问题）

- Utility 具体数学公式
- Response Curve
- Weighted Random 具体形式
- Candidate Cut 算法
- Decision Order 具体实现
- Reservation 数据结构
- HFSM / BT / 混合执行方式
- Relationship 数值尺度
- Personality 正式维度
- Activity 逻辑时长
- Free Activity Block 正式时间长度
- Relationship Ownership / Save Container
- Relational Fact Canonical Authority 最终归属
- `VillageSocialFactLedger` 是否扩展职责、迁移或保持窄域
- Fact / History 现有 Authority 的最终接入与跨容器引用方式
- Life Episode 是否保留当前命名
- Life Episode 是否保持 Derived Projection，何时需要稳定 EpisodeId
- History UI
- Place 正式内容
- 正式 Village World Art
- 更复杂 Pattern / Tradition / Event Consumer

---

## 31. Producer Approval Checklist（制作人批准检查）

- [ ] 原型必须采用正式可扩展架构，而不是未来丢弃的 Test Runtime。
- [ ] World 提供 Activity 可能性，Person 进行选择。
- [ ] Activity Selection / Execution / Result 职责分离。
- [ ] Activity 必须经过统一生命周期。
- [ ] Activity 完成先产生 Fact，再由 Interpreter 更新长期关系状态。
- [ ] Relationship Foundation 支持方向性。
- [ ] 过去互动必须能够改变未来行为。
- [ ] Debug Explainability 是纵切必要能力。
- [ ] V0.1 只使用 4 Person / 3 Place / 5 Activity 的最小内容。
- [ ] Utility AI / Weighted Random / HFSM / BT 等仍是 Supporting Candidate，不作为固定实现。
- [ ] Life Episode 是 Gate B 候选，不是 Gate A 技术依赖。
- [ ] 不提前实现 Needs / Mood / Family / GOAP / HTN / LLM Agent 等 Deferred 内容。
- [ ] 新功能不得创建 Person / VillageTime / Occupancy / Fact-History / Save 的第二套 Authority。
- [ ] Product Gate + Engineering Baseline Gate 同时通过后才进入 Runtime Implementation。

---

## 32. Next Step（下一步）

若制作人批准本 Product Spec：

1. 将长期架构确认项归位 `GameDesign` / `当前设计状态`；
2. 将真正可执行的 Vertical Slice Requirement 同步进对应 Active GameSpec；
3. 形成 `Autonomous Life Technical Architecture Investigation`，优先顺序为：
   - T1 `Relational Fact Authority Decision（关系事实权威裁决）`
   - T2 `Relationship Ownership & Persistence（关系所有权与持久化）`
   - T3 `Activity × VillageTime × Presence / Occupancy`
   - T4 `Fact → Social Response / Event` 接缝
   - T5 `Life Episode Projection（人生片段投影）`
   - T6 Utility / Reservation / HFSM / BT 等算法与执行承载
4. 对 World-State Reset / Load Isolation 做动态验证规划；
5. 等本地工程优化完成 Engineering Baseline Closure；
6. 基于新的干净 Git SHA 建立 Autonomous Life 实现 Worktree；
7. 执行 Vertical Slice；
8. 按 8 个 Product Test Case + Save / Load / NewGame Isolation + Explainability 验收。

---

## 33. One-line Product Definition（一句话产品定义）

> **Autonomous Life V0.1 用正式可扩展架构证明：具体 Person 会在世界提供的可能性中，根据自身差异、关系与当前情境自主选择活动；真实活动留下语义 Fact，Fact 改变长期关系并反过来改变未来选择，使人物生活形成可解释、可保存、可继续生长的因果历史。**
