# RRPG｜WP-AL-1 Social Foundation（社会域基础）｜Development Spec V0.2

> 日期：2026-10-04
> 项目：RRPG
> Work Package：WP-AL-1 Social Foundation（社会域基础）
> 工程基线：`main@388d00db32a05c343c674308d3f6d74622181a5d`
> 状态：**Architecture Review PASS / Awaiting explicit Runtime authorization（架构审阅通过 / 等待明确 Runtime 授权）**
> 重要：**本文件不自动授权 Runtime 开发。**只有在 Product / Current Design / Technical Architecture 正式落仓、制作人批准本 Development Spec 后，才允许执行。
> 原则：**Production Architecture + Prototype Content（正式生产架构 + 原型测试内容）**

---

# -1. 本 WP 的性质

WP-AL-1 **不是 Autonomous Life（自主生活）完整实现**。

它只建立后续所有人物自主生活必须依赖的长期社会状态地基：

```text
Relational Fact Authority
（关系事实权威）

+

Directed Relationship State
（有向关系当前状态）

+

Save / Load / NewGame World Isolation
（保存 / 加载 / 新游戏世界隔离）

+

Exactly-once Social Commit Boundary
（社会状态单次提交边界）
```

本 WP **不实现**：

- Activity Framework（活动框架）
- Utility AI（效用型 AI）
- Weighted Selection（加权选择）
- VillageTime Activity Window（村庄时间自主活动窗口）
- Navigation（寻路）
- Event Recognition（事件认识）
- Life Episode UI（人生经历片段界面）
- Personality（人格）
- 正式 Relationship 数值设计

本 WP 的目标不是“看见 NPC 自主生活”，而是：

> **先保证以后发生一次人际行为时，世界能以正式、唯一、可保存、可追溯、可幂等的方式记录“发生了什么”和“现在关系是什么”。**

---

# 0. Authority 与输入

执行时必须服从：

1. `main@388d00db32a05c343c674308d3f6d74622181a5d`
2. `README.md`
3. `AI_CONTEXT.md`
4. `Docs/文档地图.md`
5. `.clinerules.md`
6. `GameDesign/README.md`
7. `GameSpec/README.md`
8. `Docs/Architecture/技术架构总览.md`
9. `Docs/Architecture/核心对象与术语映射.md`
10. `Docs/DevelopmentSpecs/2026-10-04_RRPG_架构优化总结与验收清单.md`
11. 已批准的 Autonomous Life Product Spec V0.2
12. `2026-10-04_RRPG_Autonomous-Life_Technical-Architecture-V0.1.md`

若 Product Spec / Technical Architecture 尚未进入仓库：

> 执行员不得自行根据聊天摘要替代 Authority；应先完成设计 / 架构文件落仓，再启动本 WP。

---

# 1. 当前工程事实

基于 `388d00d`：

## 1.1 已存在且必须复用

- `PersonId（人物唯一身份）`：长期人物身份；
- `SaveSystem（存档系统）`：单槽 JSON Save Authority；
- `SaveData`：当前顶层含：
  - `Player`
  - `Village`
  - `EventWorld`
- `VillageSocialFactLedger（村庄社会事实账本）`：
  - 当前只拥有 Project TemporaryCommitment Fact；
  - 不扩展为万能关系事实库；
- `PersonLifeHistory（人物人生史）`：
  - Person-owned 人生事实 Source of Record；
  - 不承担 Shared Relational Fact；
- `WorldEventHistory（世界事件历史）`：
  - Event occurrence / repeat / cooldown；
  - 不承担 Relational Fact；
- `RRPG.Deterministic.Runtime（确定性随机运行域）`：
  - 本 WP 不需要随机，但后续 Social / Activity 系统只能复用它，不得另建随机源。

## 1.2 当前缺失

当前仓库没有：

- Canonical Relational Fact Authority（唯一关系事实权威）
- Directed Relationship Registry（有向关系注册表）
- Social World Save Block（社会世界存档区块）
- Relationship Interpreter Mutation Boundary（关系解释器修改边界）
- Social Runtime Reset / Replacement Restore（社会运行时替换式恢复）

因此本 WP 新增的是**缺失职责**，不是第二套已有 Authority。

---

# 2. 本 WP 核心不变量

## I-1｜关系事实与关系状态必须分离

```text
Relational Fact
= 过去发生了什么

Directed Relationship State
= 截止现在，这段关系是什么状态
```

不得把两者合并成：

> “关系记录 = 当前分数 + 最后一次事件”。

---

## I-2｜一个真实行为只允许一个 Canonical Fact

同一 Source Occurrence（来源发生实例）重复提交：

- 相同内容 → `Duplicate / No-op（重复 / 无副作用）`
- 不同内容复用同一 SourceOccurrenceId → `Conflict（冲突）`

不得：

- 重复追加 Fact；
- 重复修改 Relationship；
- 静默覆盖旧 Fact。

---

## I-3｜A→B 与 B→A 是独立关系

```text
Relationship(A → B)
!=
Relationship(B → A)
```

唯一身份：

```text
SourcePersonId（关系主体人物ID）
+
TargetPersonId（关系对象人物ID）
```

V0.1 不建立 `(A → A)` 自我关系。

---

## I-4｜Read Must Not Create（读取不得创建）

```text
Query(A, B)
```

如果不存在持久记录：

> 返回 Neutral Default（中性默认视图）

但：

> Registry（注册表）条目数不得变化。

不得因为：

- Debug
- UI
- Event Condition
- AI 查询

偷偷创建存档状态。

---

## I-5｜Relationship State 必须持久化

V0.1 不依赖：

> Load 时重放全部历史重新计算 Relationship。

必须：

```text
Save
→ Load
→ Directed Relationship State 保持
```

---

## I-6｜Restore = Replacement（恢复 = 替换）

```text
Current Social State
↓
Clear
↓
Restore Target Save
```

不得 Merge（合并）旧世界残留。

---

## I-7｜旧档不猜历史

旧 Save 缺 SocialWorld：

```text
RelationalFacts = empty
Relationships = empty
```

所有不存在的 Pair：

> Effective Relationship = Neutral

不得根据：

- Profession
- Assignment
- CharacterId
- DisplayName
- 同队次数

推断旧关系。

---

## I-8｜人物当前不活跃不等于删除历史

Relationship / Relational Fact 不以：

> 当前 PlayerCollection 是否还能找到该 Person

作为自动删除条件。

未来人物死亡、离开或暂时不可调度：

> 不自动 Cascade Delete（级联删除）历史关系。

---

# 3. 模块边界

## 3.1 新建纯域：`Social.Runtime（社会运行域）`

建议路径：

```text
UnityProject/RRPG1.0game/Assets/Scripts/Social/Runtime/
```

新 asmdef：

```text
Social.Runtime
```

要求：

```json
{
  "name": "Social.Runtime",
  "references": [],
  "noEngineReferences": true
}
```

可按 Unity 实际 asmdef 必要字段补全，但原则必须保持：

- 不依赖 `Village.Runtime`
- 不依赖 `Event.Runtime`
- 不依赖 `Person.Runtime`
- 不依赖 Battle
- 不依赖 UI
- 不依赖 UnityEngine

Social Runtime 只使用：

- `string PersonId`
- 纯 C# DTO / Domain Object
- 明确注入的 Interpreter Contract（解释器契约）

---

## 3.2 Assembly-CSharp Adapter（适配层）

建议新增：

```text
UnityProject/RRPG1.0game/Assets/Scripts/SocialHost/SocialRuntimeBridge.cs
```

职责：

- 持有当前世界 `SocialWorldState`
- 显式 Initialize / Restore / Reset
- 向 `SaveSystem` 暴露 Build / Restore 接缝
- 后续 WP 再增加 Activity / Event Adapter

禁止：

> 把 Social Domain 业务规则写进 Bridge。

---

# 4. WP-local Implementation Names（本 WP 实现名）

以下名字对本 WP 执行员视为实现期绑定，后续可通过 Architecture 决策演进；它们**不等于玩家规则术语永久冻结**。

建议新增：

```text
RRPG.Social.RelationalFactRecord
RRPG.Social.RelationalFactLedger
RRPG.Social.DirectedRelationshipState
RRPG.Social.DirectedRelationshipRegistry
RRPG.Social.RelationshipMutation
RRPG.Social.IRelationshipInterpreter
RRPG.Social.SocialCommitService
RRPG.Social.SocialWorldState
RRPG.Social.SocialWorldSaveData
RRPG.Social.RelationalFactSaveData
RRPG.Social.DirectedRelationshipSaveData
RRPG.Social.SocialWorldPersistence
```

Assembly-CSharp：

```text
SocialRuntimeBridge
```

若执行时发现 C# / Unity 序列化要求需要拆文件：

> 可以拆文件，但不得改变本 Spec 的职责边界。

---

# 5. Relational Fact Contract（关系事实契约）

## 5.1 `RelationalFactRecord`

V0.1 最小语义字段：

```text
SourceOccurrenceId（来源发生实例ID）
FactType（事实语义类型）
ActorPersonId（行为者人物ID）
TargetPersonId（目标人物ID）
Year（发生年份）
Month（发生月份）
Day（发生日期）
```

说明：

### `SourceOccurrenceId（来源发生实例ID）`

V0.1 直接承担：

> Canonical Idempotency Key（唯一幂等键）

本 WP **不要求单独再造 `FactId`**。

未来 Activity Runtime 可把稳定：

`ActivityInstanceId（活动实例ID）`

作为 SourceOccurrenceId 来源。

禁止：

- Guid.NewGuid 作为不可复现业务身份
- 当前时间戳作为业务身份
- 随机字符串

Identity 比较采用 literal / ordinal semantics（字面序号语义）：

- 不自动大小写折叠；
- 不自动 Trim 后偷偷改变身份；
- 不根据 DisplayName / CharacterId 猜 SourceOccurrenceId；
- Producer 必须提供已经合法、稳定的业务身份。

如果未来一个 Activity / Event Source 需要产生多条 Relational Fact：

> 由 Producer 为每条 Fact 派生稳定且互不冲突的 SourceOccurrenceId；WP-AL-1 不假设“一次 Activity 永远只能有一条 Fact”。

### `FactType（事实语义类型）`

V0.1 建议使用：

> 非空 string semantic key（语义键）

原因：

- 不提前冻结正式 Fact enum；
- WP-AL-1 没有正式 Activity Content；
- 后续可由 GameSpec / Static Config / constants 收敛。

测试可使用：

```text
test_support
test_visit
```

这些测试键不得进入正式内容 Authority。

### 日期

Social.Runtime 只做结构性检查：

- Year > 0
- Month 1..12
- Day > 0

真正生产者未来必须从唯一 `VillageTime` 提供合法日期。

不得为了日期校验让 `Social.Runtime` 反向依赖 `Village.Runtime`。

---

## 5.2 Fact Validation（事实校验）

非法情况：

- SourceOccurrenceId 空
- FactType 空
- ActorPersonId 空
- TargetPersonId 空
- Actor == Target
- 日期明显非法

结果必须明确返回，不抛出业务异常作为正常控制流。

---

## 5.3 Idempotency（幂等）

同一 SourceOccurrenceId：

### 内容完全一致

结果：

```text
Duplicate
```

要求：

- 不追加第二条 Fact
- 不重复调用 Relationship Mutation
- 不改变 Save State

### 内容不一致

结果：

```text
SourceOccurrenceConflict
```

要求：

- Fail Closed（失败关闭）
- 不改 Fact
- 不改 Relationship
- 产生明确诊断信息 / Result

---

# 6. Relational Fact Ledger（关系事实账本）

`RelationalFactLedger`：

公开读取：

```text
IReadOnlyList<RelationalFactRecord> Facts
ContainsSourceOccurrence(string sourceOccurrenceId)
```

但 `Facts` 中的每个 Runtime Record 也必须不可变；`IReadOnlyList` 本身不足以构成历史不可变保证。

推荐：

> 追加 / Restore mutation API 保持 `internal` 或等价受控可见性。

外部 Runtime 不得获得可以直接 `Add / Remove / Replace / Edit` Fact 的引用。

原因：

> Future Activity / Event Adapter 不允许绕过 `SocialCommitService` 直接写 Ledger。

Ledger 不负责：

- Relationship 解释
- Event
- Pattern
- Life Episode
- UI
- Person 存在性验证
- 当前人物是否死亡 / 离村

---

# 7. Directed Relationship Contract（有向关系契约）

## 7.1 Identity（身份）

```text
SourcePersonId
TargetPersonId
```

构成唯一 Pair Key（人物对键）。

---

## 7.2 V0.1 Test Relationship Channel（测试关系通道）

为了验证：

- Directionality（方向性）
- Save / Load
- Exactly-once Commit
- Future Feedback Foundation

WP-AL-1 允许实现一个：

```text
Affinity（亲近倾向）
```

字段。

边界：

> `Affinity` 是 **Prototype / Test Relationship Channel（原型 / 测试关系通道）**，不是多维正式 Relationship Model 冻结。

推荐类型：

```text
float
```

本 WP 不冻结：

- 最小值 / 最大值
- 0~1 / -100~100
- UI 含义
- 正式 Relationship Dimension 数量

Neutral Default（中性默认）：

```text
Affinity = 0
```

仅作为 V0.1 技术默认值。

---

## 7.3 Registry Read API（注册表读取）

至少支持：

```text
TryGet(sourcePersonId, targetPersonId, out state)
GetOrNeutral(sourcePersonId, targetPersonId)
Count
```

`GetOrNeutral`：

> 返回 immutable Snapshot / read-only View（不可变快照 / 只读视图），不得写入持久 Registry。

禁止把内部可修改 `DirectedRelationshipState` 引用直接返回给调用方，否则会形成绕过 `SocialCommitService` 的第二写入口。

---

## 7.4 Mutation API（修改 API）

外部 Runtime 不得直接：

```text
registry.SetAffinity(...)
registry.AddAffinity(...)
```

正式外部变化入口必须经过：

```text
SocialCommitService
↓
IRelationshipInterpreter
↓
RelationshipMutation
↓
Registry
```

Registry 的底层 Apply / Restore 建议限制在 Social.Runtime 内部。

---

# 8. Relationship Interpreter Boundary（关系解释器边界）

`IRelationshipInterpreter` 只定义：

> 给定一个已准备提交的 Relational Fact + **只读 Relationship Query Surface（关系查询面）**，产生零到多条 `RelationshipMutation（关系修改）`。

Interpreter 不得获得可修改 Registry / Ledger 引用，也不得在 `Interpret` 过程中直接写世界状态。

WP-AL-1：

> **不提供正式 Production Interpreter Rule（正式解释规则）**。

原因：

- `Talk / Visit / Help`
- Sociability
- Supportiveness
- Affinity 数值
- Relationship Curve

都尚未在 Active GameSpec 中冻结。

测试使用：

`Fake / Test Interpreter（测试解释器）`

例如：

```text
test_support
→ Target → Actor Affinity +10
```

此规则只存在测试代码。

禁止把测试 +10 写入 Runtime 产品逻辑。

---

# 9. `RelationshipMutation（关系修改）`

V0.1 最小测试字段：

```text
SourcePersonId
TargetPersonId
AffinityDelta
```

要求：

- Source / Target 非空
- Source != Target
- Delta 必须为有限数值
- 一次 Fact 可产生 0..N 条 Mutation
- **同一次 Commit 中，同一 Directed Pair（有向人物对）最多出现一条 Mutation**

如果 Interpreter 内部有多个因素都要修改同一 A→B：

> 必须先在 Interpreter 内聚合为一条最终 Mutation，再返回给 Commit Service。

这样未来一个事实可以：

```text
Actor → Target
Target → Actor
```

产生不同变化，同时避免 Commit 层因“同 Pair 多次 Delta 的顺序”形成隐藏语义。

---

# 10. Social Commit Transaction（社会提交事务）

## 10.1 唯一产品级修改入口

```text
SocialCommitService.Commit(fact)
```

逻辑顺序：

```text
Validate Fact
↓
Check SourceOccurrenceId
↓
如果 Duplicate → No-op
↓
如果 Conflict → Fail Closed
↓
Interpreter 生成全部 Mutations
↓
Prevalidate 全部 Mutations
↓
Append Fact
↓
Apply 全部 Relationship Mutations
↓
Commit Success
```

---

## 10.2 Atomicity Requirement（原子性要求）

如果发生以下任一情况：

- Fact Invalid
- SourceOccurrenceConflict
- Interpreter throws
- Interpreter returns invalid mutation
- Relationship mutation prevalidation fails

则：

```text
Fact Count 不变
Relationship State 不变
```

只有所有步骤可安全提交时：

> Fact + Relationship State 一起产生长期变化。

本 WP 不要求数据库级事务。

纯内存实现只需要：

> 所有可能失败的业务校验发生在持久内存 Mutation 之前。

Commit 阶段使用已预验证的 mutation plan（修改计划）；Commit 开始后不得再次调用可能产生业务失败的外部回调 / Interpreter。

---

## 10.3 Duplicate Commit（重复提交）

相同 SourceOccurrenceId + 相同 payload 再次提交：

```text
Result = Duplicate
```

并且：

> Interpreter 不得再次产生有效关系变化。

这是 Exactly-once Social Effect（社会效果单次生效）的核心验收。

---

# 11. Social World State（社会世界状态）

`SocialWorldState` 最小拥有：

```text
RelationalFactLedger Facts
DirectedRelationshipRegistry Relationships
```

不包含：

- Activity Runtime
- Event Pending
- Person objects
- Place
- Navigation
- Personality
- Recent Activity
- Life Episode

这些由后续 WP 分域加入。

---

# 12. Save Schema（存档结构）

## 12.1 Top-level Ownership（顶层所有权）

本 WP 采用：

```text
SaveData
├─ Player
├─ Village
├─ EventWorld
└─ SocialWorld
```

新增：

```text
public SocialWorldSaveData SocialWorld;
```

原因：

> Relationship / Relational Fact 是 world-social-domain state（世界社会域状态），不属于 Character/Hero 单体，也不属于 Village 单独所有。

---

## 12.2 `SocialWorldSaveData`

最小结构：

```text
RelationalFacts[]
Relationships[]
```

### `RelationalFactSaveData`

镜像：

- SourceOccurrenceId
- FactType
- ActorPersonId
- TargetPersonId
- Year
- Month
- Day

### `DirectedRelationshipSaveData`

镜像：

- SourcePersonId
- TargetPersonId
- Affinity

不得保存：

- Neutral missing pairs
- 派生 Episode
- Recognition
- Event
- Debug Trace

---

# 13. SaveVersion / Old Save Decision（存档版本 / 旧档裁决）

WP-AL-1 采用：

> **Additive Nullable Block（可空增量区块）**

当前 `SaveVersion`：

```text
1
```

本 WP **不提升 SaveVersion**。

依据当前仓库既有兼容模式：

- Village 区块 additive
- EventWorld 区块 additive
- Growth / Formation / LifeHistory additive

旧档缺 `SocialWorld`：

> `SocialWorldPersistence.Restore(world, null)` → empty state

如果实施中发现 JsonUtility / Schema 行为无法安全满足这一契约：

> 停止实现并回报，不得擅自提升 SaveVersion。

---

# 14. SocialWorldPersistence（社会世界持久化）

纯 `Social.Runtime`：

```text
Build(SocialWorldState world)
Restore(SocialWorldState world, SocialWorldSaveData data)
```

## Build

要求：

- Deep Snapshot（深快照）
- 不返回 Runtime 对象引用
- Facts 保持稳定历史顺序
- Relationships 输出稳定排序：
  - SourcePersonId ordinal
  - TargetPersonId ordinal

目的：

> Save JSON / Test Diff 可稳定复现，不依赖 Dictionary 遍历顺序。

---

## Restore

Restore 是 **exact state restore（精确状态恢复）**，不是重新解释历史。

第一步必须：

```text
Clear Facts
Clear Relationships
```

然后直接恢复 DTO 中的 Fact 与 Relationship State。

禁止：

```text
Restore Facts
→ 再跑 Relationship Interpreter
→ 重算 Relationship
```

否则算法版本变化会重写旧世界关系。

`data == null`：

> 保持空状态。

防御规则：

### Fact

丢弃：

- 空 SourceOccurrenceId
- 空 FactType
- 空 Actor / Target
- Actor == Target
- 明显非法日期

重复 SourceOccurrenceId：

- 相同 payload → 只保留首条
- 冲突 payload → 只保留首条 + Diagnostic / 可测试结果

### Relationship

丢弃：

- 空 Source / Target
- Source == Target
- 非有限 Affinity

重复 Pair：

> 只保留首条。

重要：

> 不因当前 PlayerCollection 找不到 PersonId 而丢弃历史 Social Data。

---

# 15. `SocialRuntimeBridge` Contract（社会运行桥契约）

Assembly-CSharp 新增：

```text
SocialRuntimeBridge
```

最小生命周期 API 建议：

```text
EnsureInitialized()
BuildSaveData()
Restore(SocialWorldSaveData data)
ResetForNewGame()
ResetForTests()
```

业务公开 API 建议：

```text
QueryRelationship(...)
QueryFacts(...)
CommitRelationalFact(...)
```

### Raw Current State Boundary（原始当前状态边界）

**不建议向一般产品代码公开可修改的 `SocialWorldState Current`。**

如果测试 / 同程序集适配确实需要 Current：

- 限制为 internal / test seam（测试接缝），或
- 返回只读 facade（只读门面）。

Getter 不得自动初始化，也不得给上层第二个直接修改 Ledger / Registry 的入口。

## `EnsureInitialized`

显式：

> current == null 时创建空 `SocialWorldState`。

## `BuildSaveData`

不得偷偷改变业务状态。

若 Runtime 尚未显式初始化：

> 可以对“空世界”构造 empty DTO，但不得为了保存而创建 Fact / Relationship Record，也不得通过 getter 产生隐藏业务状态。

是否缓存一个空 `SocialWorldState` 属实现细节；对外观察必须与“空 Social World”一致。

## `Restore`

显式 Replacement Restore。

## `ResetForNewGame`

必须形成：

```text
empty facts
empty relationships
```

不得保留上一世界状态。

---

# 16. SaveSystem Integration（存档系统接入）

允许修改：

```text
Assets/Scripts/Save/SaveData.cs
Assets/Scripts/Save/SaveSystem.cs
```

## Save

最终 `BuildSaveData`：

```text
SocialWorld = SocialRuntimeBridge.BuildSaveData()
```

所有真实 Save 入口：

> 必须携带当前 SocialWorld snapshot。

不得只让某个特殊 Save path 才保存 Social。

---

## Load

Social Restore 必须延迟到：

- JSON 成功解析；
- SaveVersion 合法；
- Player 区块存在；
- `RestorePlayerCollection(...)` 已成功返回新的 PlayerCollection；

之后才正式：

```text
SocialRuntimeBridge.Restore(saveData.SocialWorld)
```

因此：

> 如果 Player Restore 抛异常 / Load 最终返回 null，Social Runtime 必须保持调用前状态，不得被提前 Clear。

注意：

> 当前 Village / Event Restore 的既有顺序不在 WP-AL-1 重构范围；本 WP 只保证新 Social Domain 不扩大失败 Load 的世界状态破坏。

---

## NewGame

`SaveSystem.NewGame()` 必须显式：

```text
SocialRuntimeBridge.ResetForNewGame()
```

保证同会话：

```text
World A
→ NewGame
→ World B
```

不会保留：

- Relational Fact
- Relationship State

本 WP 不借机重写 Village / Event World Reset。

若执行中发现现有 NewGame 的其它 World Host 污染会阻断 Social 验收：

> 登记独立问题，不扩大 WP-AL-1。

---

# 17. Public API Boundary（公共 API 边界）

本 WP 结束时，未来 Activity / Event 层应只能依赖以下概念接口：

## Read

```text
Query Relationship
Query Relational Facts
```

## Write

```text
Commit Semantic Relational Fact
```

未来上层不允许：

```text
直接 Set Relationship
直接 Append Ledger
直接改 Save DTO
```

---

# 18. Exact Write Scope（精确写入范围）

## 18.1 新增 Runtime

建议新增：

```text
UnityProject/RRPG1.0game/Assets/Scripts/Social/Runtime/Social.Runtime.asmdef
UnityProject/RRPG1.0game/Assets/Scripts/Social/Runtime/RelationalFactRecord.cs
UnityProject/RRPG1.0game/Assets/Scripts/Social/Runtime/RelationalFactLedger.cs
UnityProject/RRPG1.0game/Assets/Scripts/Social/Runtime/DirectedRelationshipState.cs
UnityProject/RRPG1.0game/Assets/Scripts/Social/Runtime/DirectedRelationshipRegistry.cs
UnityProject/RRPG1.0game/Assets/Scripts/Social/Runtime/RelationshipMutation.cs
UnityProject/RRPG1.0game/Assets/Scripts/Social/Runtime/IRelationshipInterpreter.cs
UnityProject/RRPG1.0game/Assets/Scripts/Social/Runtime/SocialCommitService.cs
UnityProject/RRPG1.0game/Assets/Scripts/Social/Runtime/SocialWorldState.cs
UnityProject/RRPG1.0game/Assets/Scripts/Social/Runtime/SocialWorldSaveData.cs
UnityProject/RRPG1.0game/Assets/Scripts/Social/Runtime/SocialWorldPersistence.cs
UnityProject/RRPG1.0game/Assets/Scripts/SocialHost/SocialRuntimeBridge.cs
```

执行 Agent 可合理合并小型 record / enum 文件，但：

> 不得把职责混成一个巨大 `SocialManager.cs`。

---

## 18.2 修改

```text
UnityProject/RRPG1.0game/Assets/Scripts/Save/SaveData.cs
UnityProject/RRPG1.0game/Assets/Scripts/Save/SaveSystem.cs
```

---

## 18.3 新增 Tests

建议：

```text
UnityProject/RRPG1.0game/Assets/Tests/EditMode/AutonomousLife/SocialFoundation.Tests.asmdef
UnityProject/RRPG1.0game/Assets/Tests/EditMode/AutonomousLife/RelationalFactLedgerTests.cs
UnityProject/RRPG1.0game/Assets/Tests/EditMode/AutonomousLife/DirectedRelationshipRegistryTests.cs
UnityProject/RRPG1.0game/Assets/Tests/EditMode/AutonomousLife/SocialCommitServiceTests.cs
UnityProject/RRPG1.0game/Assets/Tests/EditMode/AutonomousLife/SocialWorldPersistenceTests.cs
UnityProject/RRPG1.0game/Assets/Tests/EditMode/AutonomousLife/SocialSaveIntegrationTests.cs
```

Test asmdef：

- reference `Social.Runtime`
- Editor only
- `UNITY_INCLUDE_TESTS`
- `TestAssemblies`

SaveSystem / Bridge（Assembly-CSharp）集成测试：

> 按仓库现有模式通过 Reflection（反射）访问，不让 asmdef 反向引用预定义程序集。

---

# 19. Forbidden Write Scope（禁止写入）

WP-AL-1 不允许修改：

- `Assets/Scripts/Village/Runtime/*` 的玩法逻辑
- `VillageRuntimeBridge`
- `EventRuntimeBridge`
- `WorldEventHistory`
- `VillageSocialFactLedger`
- `PersonLifeHistory`
- `CharacterInstance`
- `HeroInstance`
- Battle
- Expedition
- UI
- Scene
- Prefab
- Static Config
- GameDesign 玩法正文
- GameSpec 规则正文

例外：

> 如果编译因 namespace / asmdef 结构必须做极小引用调整，先停并回报，不自行扩大白名单。

---

# 20. Test Matrix（测试矩阵）

## Gate A｜Pure Domain Core（纯域核心）

### A1 Missing Query Is Neutral And Side-effect Free
- Query 不存在 A→B
- 返回 Neutral
- Registry Count 不变

### A2 Directionality
- 修改 A→B
- B→A 不变

### A3 Self Pair Rejected
- A→A 不建立记录

### A4 Fact Append
- 合法 Fact 成功
- Facts Count +1

### A5 Exact Duplicate
- 同 SourceOccurrenceId + 同 payload
- Duplicate
- Count 不变

### A6 Source Identity Conflict
- 同 SourceOccurrenceId + 不同 payload
- Conflict
- Count / Relationship 均不变

### A7 Same Pair Different Occurrences
- 不同 SourceOccurrenceId
- 可记录多次真实行为

### A8 Runtime Fact Immutability
- 外部读取 Fact 后不能修改 Ledger 内历史事实

### A9 Relationship Query Is Snapshot / Read-only
- Query 返回值不能绕过 Commit Service 修改 Registry

---

## Gate B｜Exactly-once Social Commit（社会效果单次提交）

使用 Test Interpreter：

```text
test_support
→ Target → Actor Affinity +10
```

### B1 Success
- Fact +1
- Relationship 只变化一次

### B2 Duplicate Retry
- Fact 不增加
- Relationship 不增加第二次
- Interpreter 不产生第二次有效 mutation

### B3 Interpreter Failure
- Interpreter 抛异常 / 明确失败
- Fact 0 mutation
- Relationship 0 mutation

### B4 Invalid Mutation
- Fact 不提交
- Relationship 不提交

### B5 Multi-Mutation
- 同一 Fact 可对多个 Directed Pair 产生 mutation
- 一次性全部成功

### B6 Duplicate Pair Mutation Rejected
- 同一 Commit Plan 返回两条 A→B mutation
- 整次 Commit Fail Closed
- Fact / Relationship 均不变化

---

## Gate C｜Persistence（持久化）

### C1 Round Trip
- Facts 完整
- Relationship 完整
- A→B / B→A 保持

### C2 Restore Replaces
预置旧状态：

```text
old facts
old relationships
```

Restore 新 DTO 后：

> old 全部消失，只保留 target save。

### C3 Restore Null
- 清空全部 Social State

### C4 Invalid Data Filtering
- invalid fact
- self relationship
- duplicate pair
- duplicate/conflicting occurrence

按 Spec 防御性处理。

### C5 Stable Relationship Ordering
同一 Runtime State 重复 Build：

> Relationships 序列顺序一致。

### C6 Unknown / Inactive PersonId Preservation
PersonId 当前不在 PlayerCollection：

> 仍可字面 Restore，不猜、不删除历史。

### C7 Restore Does Not Reinterpret Facts
使用一个与保存时不同的 Test Interpreter：

> Restore 后 Relationship 仍等于存档值，不因当前 Interpreter 改变而重算。

---

## Gate D｜SaveSystem Integration（存档系统接合）

### D1 Save / Load Round Trip
经真实 SaveSystem：

```text
Social State
→ Save
→ Reset
→ Load
```

保持一致。

### D2 Legacy Save Without SocialWorld
旧 JSON 无 SocialWorld：

> Load Success
> Social Facts = 0
> Relationships = 0

### D3 NewGame Clears Social World
World A 有关系 / 事实：

```text
SaveSystem.NewGame()
```

之后：

> Current Social State 为空。

### D4 Load Save B Replaces World A
World A 当前有数据，加载 B：

> A 不残留。

### D5 SaveVersion Compatibility
本 WP 完成后：

```text
CurrentSaveVersion == 1
```

旧 Version 1 Save 仍可加载。

### D6 Failed Load Does Not Clear Current Social World
先建立 World A Social State，再制造一个 Player Restore 失败 / 非法 Load：

> Load 返回失败时，World A Social State 保持调用前值。

---

# 21. Full Regression（全量回归）

因为本 WP 修改：

- `SaveData`
- `SaveSystem`

属于跨域高风险入口。

实施完成后必须执行：

## Compile

```text
0 compile errors
```

## Targeted EditMode

WP-AL-1 全部新测试：

```text
PASS
```

## Full EditMode

基线：

> `1064 / 1064 PASS`

本 WP 新增测试后总数应增加，但：

> **原有测试 0 regression / 全套 0 failure**

## Full PlayMode

基线：

> `93 / 93 PASS`

要求：

> 全套 0 failure。

## Static

- `git diff --check`
- Missing Script = 0（本 WP 原则上不改 Scene）
- 不新增 Scene / Prefab 序列化变更

---

# 22. Non-goals（明确非目标）

WP-AL-1 不做：

- 正式 Activity
- ActivityInstanceId
- Utility AI
- WeightedPermutation 接入
- Provider
- Reservation / Arbitration
- Recent Activity State
- Personality
- Relationship UI
- Relationship Debug Inspector
- Navigation
- Village World Actor
- Recognition / Event Adapter
- Life Episode
- Pattern
- Social Graph Visualization
- Romance / Family
- 关系衰减
- 关系多维模型

如果执行 Agent 发现上述需求：

> 登记为下一 WP 输入，不扩大 WP-AL-1。

---

# 23. Implementation Risks（实施风险）

## R1｜Affinity 误冻结为最终关系模型

控制：

- Runtime 注释明确 `V0.1 Test / Not Frozen`
- 不写 GameSpec “正式关系只有 Affinity”
- Registry / Authority 结构不得依赖“永远只有一个维度”

---

## R2｜SaveSystem NewGame 世界状态职责扩大

控制：

- 本 WP 只负责 Social reset；
- 不顺手重构 Village / Event reset；
- 其它 World Host 问题独立登记。

---

## R3｜Fact 与 State 半提交

控制：

- Interpreter / Mutation 全部先验证；
- 再进入无业务失败点的内存 commit；
- Duplicate 不再次 interpret。

---

## R4｜Social.Runtime 反向依赖 Village / Event

控制：

- asmdef `references = []`
- 日期由 adapter 提供
- PersonId 只当稳定 string identity
- Event / Village 以后通过 Host Adapter 接入。

---

## R5｜旧存档污染 / 合并

控制：

- Restore 先 Clear
- NewGame explicit reset
- 旧档缺区块 → empty
- 集成测试强制覆盖同会话 World A → World B。

---

# 24. Acceptance（验收）

WP-AL-1 只有满足以下条件才算完成：

1. `Social.Runtime` 纯域成立，无 Village / Event / Person / Unity 反向依赖；
2. Relational Fact 具备稳定 SourceOccurrence 幂等语义；
3. Directed Relationship A→B / B→A 独立；
4. Missing Query = Neutral 且零写入；
5. Relationship 只通过 Social Commit Path 修改；
6. 同一 Fact retry 不重复改关系；
7. Fact + Relationship 无业务半提交；
8. SocialWorld Save / Load Round Trip 成立；
9. Restore 是 Replacement，不是 Merge；
10. NewGame 清空 Social Runtime；
11. 旧 Save 无 SocialWorld 可加载为空状态；
12. SaveVersion 保持 1；
13. 不修改既有三个 History Authority 的职责；
14. Targeted / Full EditMode / Full PlayMode 全通过；
15. Runtime Fact / Relationship Query 不暴露可绕过 Authority 的 mutable reference（可变引用）；
16. Failed Load 不提前破坏当前 Social World；
17. Restore 不重新运行 Interpreter；
18. 没有引入 Activity / Utility / Event / UI 等越界内容。

---

# 25. Stop Conditions（停止条件）

执行期间遇到以下情况，必须停并回报：

- 需要修改 `VillageSocialFactLedger` 才能实现；
- 需要修改 `PersonLifeHistory` 才能实现；
- 需要修改 `WorldEventHistory` 才能实现；
- 需要让 Social.Runtime 引用 Village.Runtime / Event.Runtime；
- 需要提高 SaveVersion；
- 发现旧 Save 缺 SocialWorld 不能安全兼容；
- 发现 SaveSystem NewGame / Load 生命周期无法在当前白名单内保证 Social World Isolation；
- 需要引入正式 Relationship 数值规则；
- 需要新增 Scene / Prefab / UI；
- 需要创建第二套 Time / Occupancy / History Authority。

---

# 26. Repository Landing / Commit Boundary（落仓 / 提交边界）

执行前：

- Product Spec / Technical Architecture 必须先成为仓库可引用材料；
- 本 Development Spec 进入 `Docs/DevelopmentSpecs/`；
- 由单一执行 Agent 从最新 `main` 开工。

建议实现 Commit 独立：

```text
feat: add autonomous life social foundation
```

不得把：

- GameDesign 重写
- Scene 整理
- UI 美术
- 其它架构清理

混入同一个 implementation commit。

完成后另立：

`WP-AL-1 Closeout Report（收口报告）`

记录：

- 实际文件
- API
- Save schema
- 测试数量
- 偏离 Spec 的原因
- Commit SHA
- 后续债务

---

# 27. 下一 Work Package

WP-AL-1 通过后才进入：

> **WP-AL-2 Activity Lifecycle & Daily Integration（活动生命周期与每日接入）**

WP-AL-2 才处理：

```text
VillageTime
+
Day-start Occupancy Snapshot
+
Activity Opportunity
+
Activity Provider
+
Activity Commitment / Claims
+
Explicit Activity FSM
+
Live / Headless Contract
```

Utility AI 仍不在 WP-AL-2；它属于：

> WP-AL-3 Autonomous Selection & Arbitration（自主选择与仲裁）。

---

# 28. 一句话开发定义

> **WP-AL-1 用一个纯 Social Domain（社会域）建立“关系事实唯一权威 + 有向关系当前状态 + 单次提交 + 世界级持久化”的正式基础，让后续任何 Activity / Event 都只能通过可追溯的 Semantic Fact（语义事实）改变长期人物关系，而不创建第二套 History、Person、Village 或 Save Authority。**
