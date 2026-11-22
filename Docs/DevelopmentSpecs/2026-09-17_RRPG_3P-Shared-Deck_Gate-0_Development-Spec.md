# RRPG｜3P Shared Deck Buildcraft Prototype｜Gate 0 Development Spec

> **日期：2026-09-17**  
> **Work Package：3P Shared Deck Buildcraft Prototype｜Gate 0 Configurable Card Effect Architecture**  
> **状态：Development-time Binding / Ready for Execution**  
> **目标执行 Agent：Cline + Unity MCP**  
> **建议路径：`Docs/DevelopmentSpecs/2026-09-17_RRPG_3P-Shared-Deck_Gate-0_Development-Spec.md`**
>
> 本文件是当前 Work Package 的开发期 Binding Spec。  
> 它不替代 Current GameSpec，不把 Identity 候选内容提升为长期 Current Rule，也不冻结未明确要求的 C# 类名、设计模式、程序集拆分或具体技术实现。

---

# 0. Authority、证据与执行边界

## 0.1 开发流程 Authority

执行本 Work Package 前，Agent 必须遵守并按需读取：

1. `.clinerules.md`
2. `Docs/AI开发执行规则.md`
3. `Docs/AI多Agent开发协作协议.md`
4. `Docs/文档地图.md`

其中：

- `.clinerules.md`：不可跳过的硬规则；
- `AI开发执行规则.md`：Development / Verification / Acceptance / Final Closeout 的流程 Authority；
- `AI多Agent开发协作协议.md`：Work Package、文件所有权、并行冲突、Blocked / Awaiting Decision；
- 本文件：当前 Gate 0 的开发范围、玩家行为 Binding、Technical Checkpoint 与验收条件。

## 0.2 玩家规则 Authority

涉及现有 Battle / Person Current Rule 时：

1. 用户当前明确裁决；
2. `GameSpec/README.md` 指向的 Current Battle / Person GameSpec；
3. 当前仍有效的设计决策；
4. 本 Development Spec。

若本文件与 Current GameSpec 在**既有玩家规则**上出现冲突：

> 不得静默选择。停止固化该冲突项，报告证据并进入 `Awaiting Decision`；其它不受影响部分可继续。

## 0.3 Identity 内容输入

当前角色内容专题：

- `GameDesign/Identity/README.md`
- `GameDesign/Identity/Characters/Cook.md`
- `GameDesign/Identity/Characters/Worker.md`
- `GameDesign/Identity/Characters/Hunter.md`
- `GameDesign/Identity/卡牌与Trait设计工具箱.md`
- `GameDesign/Identity/Identity角色内容设计工作台.xlsx`

作为本 Prototype 的**角色内容设计输入**。

其当前 Freeze Level 仍为：

> `Concept / Not Fully Frozen`

因此：

- 可以作为 Gate 0 / 3P Prototype 的内容输入；
- 不得因为实现落地自动提升为 Current Rule；
- 不得反向冻结五 Identity 的全部未来卡池、Trait、数值或长期 Profession 内容；
- 正式长期规则仍服从 GameSpec。

---

# 1. Work Package 目标

Gate 0 只解决一个问题：

> **让当前 Battle 能以“有限、强类型、可配置的设计零件”表达 Cook + Worker + Hunter 的首轮 3P Proof Cards，而不是按 CardId 为每张牌硬编码专属逻辑。**

Gate 0 完成后，应具备进入 3P 人工产品验证的条件。

Gate 0 **不负责**证明：

> Shared Deck Buildcraft 已经好玩。

产品验证发生在 Gate 0 Technical Verification 完成之后。

---

# 2. 当前 Prototype Scope

首轮阵容：

- Cook（厨师）
- Worker（工人）
- Hunter（猎人）

首轮：

> **Trait OFF**

先验证：

- Card / Hand / Cost；
- Position / Adjacency；
- Enemy State / Timing；
- Shared AP；
- 多 Person Shared Deck 的跨角色重新估值。

Trait、Draw、Retain 等更高牌流干扰在 Core 3P 验证后再独立 A/B。

---

# 3. Binding｜必须满足的玩家行为

本章为当前 Work Package 的**行为 Binding**。

技术实现不得改变以下结果。

## 3.1 卡牌效果不得按 CardId 写死

禁止以以下方式作为九张 Proof Card 的正式执行结构：

```text
if CardId == ...
switch CardId
if Identity == Cook then ...
```

九张牌必须主要由可复用的有限规则零件 + 参数配置表达。

如果真实内容暴露出一个缺失的通用最小零件：

> 可以新增有限零件。

不得因此扩成万能脚本语言。

## 3.2 第一版卡牌表达能力

第一版只要求覆盖当前真实内容重复出现的设计结构：

- Trigger / Timing（触发 / 时机）
- Query（查询对象集合）
- Predicate（条件过滤）
- Selector / Aggregate（选择 / 聚合）
- Value Source / Value Expression（数值来源 / 数值表达）
- Operation（执行操作）
- Lifetime / Reset（生命周期 / 重置）
- Play Requirement（出牌前置）

这些是**设计职责**，不是冻结的 C# 类层级。

## 3.3 Primary Resolution 出牌合法性

基础规则：

> **一张主动 Card 如果其 Primary Resolution（主要结算）不存在合法结果，则当前不可打出。**

例如：

- 《补位》没有合法我方空格 → 不可打出；
- 《换岗》没有另一名合法可交换友方 Person → 不可打出；
- 《切配》手牌除自身外没有其它 Card → 不可打出。

但：

> Conditional Bonus（条件奖励）不成立，不等于卡牌不可打。

例如基础伤害可以正常结算，而邻接 Bonus 未触发：

> 卡牌仍可打。

默认禁止“支付 AP 后核心效果完全空结算”。

未来若某张牌明确把“空放”作为玩法，需要独立设计批准。

---

# 4. 《切配》当前 Prototype Binding

## 4.1 自动目标

打出《切配》后：

> 自动加工当前 Shared Hand（共享手牌）中，除正在结算的《切配》自身之外，`Actual Cost（实际费用）` 最高的 Card。

不限制：

- Owner；
- Identity；
- Attack / Support Tag；
- 是否具有 Damage。

无费用阈值。

## 4.2 并列规则

若多张 Card 的 Actual Cost 并列最高：

> 当前 Hand（手牌）从左到右，最左侧优先。

必须：

> 玩家在打出《切配》前可以预测系统将命中哪张 Card。

不得随机。

## 4.3 Operation Applicability

《切配》当前包含至少：

- Cost Modify（费用修改）；
- Damage Modify（伤害修改，如果目标具有 Damage）。

若目标没有 Damage：

> 目标仍然合法；Cost 修改正常执行；Damage 修改自然不适用。

必须区分：

> `Operation 不适用于目标`
>
> 与
>
> `目标本身非法`

两者不是同一件事。

## 4.4 临时修改

《切配》的修改是：

> Battle Runtime（单场战斗运行时）临时修改。

不得修改：

- 静态 Card Template；
- 长期 Person；
- Run 外永久卡牌状态；
- Save。

## 4.5 Current Deck Cycle 生命周期

《切配》的临时 Modifier（修正项）：

- 不因 Turn End（回合结束）自动清除；
- 不因进入 Discard Pile（弃牌堆）立即清除；
- 当该具体 Card 经弃牌堆重新进入新的 Draw Pile（抽牌堆）循环时重置。

也就是说：

```text
Hand
→ Play / Discard
→ Discard Pile
→ Reshuffle into Draw Pile
→ 进入下一 Deck Cycle
→ 清除 Current Deck Cycle Modifier
```

Retain（保留）在 Hand 中的 Card：

> 没有进入新 Deck Cycle，因此不因其它牌重洗而自动重置。

## 4.6 Modifier 叠加

同一 Card 在同一个 Deck Cycle 内：

> 允许多次受到《切配》。

当前只需要支持：

> Additive Modifier（加减型修正）可共存并累积。

边界：

- Cost ≥ 0；
- Damage ≥ 0。

未来的百分比、乘法、Replace、Highest Wins、Priority、Layer 全部不属于 Gate 0。

---

# 5. Worker 空间 Binding

## 5.1 《补位》

Worker 可以移动到：

> 任意合法我方空格。

首轮不限制为邻接移动。

合法目标至少要求：

- 属于当前合法我方战场空间；
- 当前为空；
- 不产生两个单位占同一位置；
- 不违反既有 Battle Current Rule。

## 5.2 《换岗》

Worker 可以与：

> 任意另一名仍具备战斗能力的我方 Person

交换位置。

首轮：

- 不要求邻接；
- 不能与自己交换；
- 不能与已经失去战斗能力的单位交换；
- 不能与敌方单位交换。

## 5.3 空间 Authority 不变量

无论技术上如何实现，必须保证：

> 战中 Move / Swap 后，整个 Battle 只有一份一致的 Position Authority。

必须满足：

- 失败操作零部分写入；
- 成功后 Occupancy 与 Unit Position 一致；
- Range 立即读取新位置；
- Adjacency 立即读取新位置；
- 不得出现 Grid 与 Unit 自己记录的位置互相矛盾；
- 不得建立第二套正式空间真相源。

---

# 6. Shield（护盾）Binding

第一版引入轻量通用：

> `Shield（护盾）`

它只属于：

> 当前 Battle 的临时防御值。

受到 Damage（伤害）时：

```text
先由 Shield 吸收
→ Shield 不足部分再扣 HP
```

例如：

```text
Shield = 4
Incoming Damage = 7

结果：
Shield = 0
HP -3
```

首版：

> 下一玩家回合开始时清除剩余 Shield。

Gate 0 不建设：护盾类型、护盾穿透、破盾触发、护盾永久成长、多层复杂 Stack、独立 Shield Build Framework。

Shield 不进入：

- Run 长期状态；
- Person；
- Save；
- 下一场 Battle。

---

# 7. Hunter Prototype 需要读取的 Battle Fact

Hunter 首轮至少需要 Battle 能读取：

- Enemy Current HP；
- Enemy Max HP；
- Enemy Intent（敌人意图）；
- Card Owner / Source Person；
- Person Attack；
- Position；
- Adjacent Ally；
- 多个合法数值来源中的比较结果。

Gate 0 不要求新增 Hunter Focus、Prey、History 等未来专属资源。

若具体 Hunter 卡牌在内容绑定时暴露出**真实未定义且会改变玩家策略**的 Timing / Snapshot 问题：

> 仅暂停该局部卡牌绑定并进入 `Awaiting Decision`，不得因此阻塞其它 Technical Checkpoint。

不得由执行 Agent 根据代码方便程度补写设计。

---

# 8. Technical Constraints｜技术硬边界

## 8.1 必须复用现有正式 Battle 产品路径

必须先完成 Existing Host Map。

优先级：复用 → 扩展 → 原位替换 → 确认无法合理复用时才新增正式宿主。

禁止未经审计创建：

- 第二套 Battle Manager；
- 第二套 Shared Deck；
- 第二套 Person/Card Owner Authority；
- 第二套 Position Authority；
- 第二套正式玩家入口。

Debug / Test Harness 可以存在，但不得替代真实产品集成证明。

## 8.2 不改 Save 语义

Gate 0 默认：

- 不提升 SaveVersion；
- 不新增长期 Save Schema；
- 不将单场卡牌 Modifier、Shield、Position 临时状态持久化；
- 不改变 Battle → Return → Person 长期 Authority。

如果实现被证明必须触碰公共 Save Schema / SaveVersion：

> 属 Red，进入 `Awaiting Decision`。

## 8.3 不建设万能 DSL

禁止扩成：任意 IF / ELSE、文本表达式脚本、任意公式编辑器、Effect Graph、通用编程语言、完整 Modifier Priority Framework、未来所有卡牌机制的通用框架。

目标是：

> 当前真实卡牌逼出当前真实最小零件。

---

# 9. Technical Candidate｜推荐但不冻结的实现方向

本章全部为：

> **Technical Candidate / Recommended Implementation**

Cline 可以在完成本地 Audit、Existing Host Map 与 Impact Analysis 后调整。

只要玩家行为 Binding 不变、Authority 不变、Scope 不扩大、验证完整，无需回来请求人工批准。

当前推荐：

- 有限强类型配置，而非 CardId 专属逻辑或自由文本 DSL；
- Static Definition 与 Runtime State 分离；
- 区分 Base Cost / Actual Cost、Base Damage / Current Damage；
- 一次 Rule Block 结算时绑定已经选择出的目标，避免后续 Operation 因前一步数值变化误选另一对象；
- 将 Discard → Draw Reshuffle 作为 Current Deck Cycle Reset 的强候选接缝，但由执行 Agent 核对本地真实代码后决定；
- Shield 作为 Battle-only runtime state，具体承载位置由 Existing Host Map 决定；
- Move / Swap 通过唯一安全空间写入口实现，具体类名与方法名不冻结。

---

# 10. Legacy Compatibility

现有 Battle 已有旧最小 Damage / Heal 等路径。

Gate 0 应尽量增量迁移，不要求一次性把全部旧牌全部改成新效果架构。

允许的兼容方向：

- 新配置存在 → 新规则管线；
- 未迁移旧内容 → 保持现有路径。

但必须保证：

> 同一张 Card 不会新旧两套效果重复结算。

旧内容何时完全退役不属于 Gate 0。

---

# 11. Repository Work Package 与 Technical Checkpoints

本 Gate 0 定义为：

> **一个 Repository Work Package**

由同一个 Development Agent 在范围内连续完成。

内部 Technical Checkpoint：

```text
G0-T1
→ G0-T2
→ G0-T3
→ G0-T4
→ G0-T5
→ G0-T6
```

Technical Checkpoint 是机器验证节点：

- 不要求每个阶段用户验收；
- 不要求每阶段 Handoff；
- 不要求每阶段独立 Commit；
- Green / Yellow 技术问题由 Agent 自主处理；
- Red 才进入 Awaiting Decision。

---

# 12. G0-T1｜Rule Definition + Legacy Bridge

目标：建立足以表达首轮真实内容的最小可配置规则骨架，并与现有卡牌兼容。

至少需要能表达：Trigger / Timing、Query、Predicate、Selector / Aggregate、Value Source / Expression、Operation、Lifetime、Play Requirement。

验收：

1. 至少一个现有简单 Damage / Heal 行为可通过新结构表达；
2. Legacy 卡牌不因新结构接入而重复结算；
3. 不存在 CardId 专属执行；
4. 编译、Console、专项模块测试通过；
5. 无第二套正式 Battle/Deck/Owner Authority。

---

# 13. G0-T2｜Runtime Property + Modifier + Deck Cycle

目标：支撑 Actual Cost、临时 Damage 修改、Additive Modifier 与 Current Deck Cycle Reset。

必须验证：

```text
3 Cost / 8 Damage
→ 第一次加工
2 / 6
→ 第二次加工
1 / 4
→ Discard
临时状态仍存在
→ Reshuffle into new Draw Cycle
→ 恢复本次 Battle 应有基础状态
```

还必须证明：

- Cost Clamp 0；
- Damage Clamp 0；
- 多个 Additive Modifier 可共存；
- 不同 Card 实例不串状态；
- 不同 Person 拥有同模板 Card 时不串状态；
- 不修改静态模板；
- Retain 中的 Card 不因其它 Card 重洗被错误 Reset。

---

# 14. G0-T3｜Query / Selector / Play Requirement

目标：能正确找对象、判断可打、自动选择并稳定绑定本次结算对象。

重点验证《切配》：

- 使用 Actual Cost，不是 Base Cost；
- 排除 Source Card 自身；
- MaxBy 正确；
- 并列时 Hand 最左；
- 结果确定、非随机；
- 无第二张 Card 时不可打；
- 支付 AP 之前完成合法性判断；
- Cost 修改后，后续 Damage 修改仍作用于同一绑定目标。

同时验证：

- 《补位》无合法空格不可打；
- 《换岗》无合法可交换 Person 不可打；
- Bonus 条件不满足不影响基础出牌合法性。

---

# 15. G0-T4｜Operation Pack + Spatial Mutation + Shield

只实现当前真实内容需要的 Operation。

当前预计包括：Damage、Heal（若兼容层需要）、Shield、Draw（仅已有/必要能力）、Exhaust、ModifyCost、ModifyDamage、Move、Swap。

若实际九张 Proof Card 不需要某项：

> 不得只因为本列表出现就提前建设。

空间验证：

- Move 成功后位置唯一一致；
- Swap 原子；
- 非法操作零写入；
- Range 即时更新；
- Adjacency 即时更新；
- 失去战斗能力单位不能成为非法操作目标。

Shield 验证：

- Shield ≥ Damage → HP 不下降；
- Shield < Damage → 剩余 Damage 正确进入 HP；
- 下一玩家回合开始清除；
- Battle 结束不残留；
- 不进入 Run/Save。

---

# 16. G0-T5｜3P Proof Content Binding

首轮绑定：Cook、Worker、Hunter。

每个 Identity 使用当前首轮 Core Proof 内容。

具体 Card 名称、效果参数、Prototype Test Value：

> 读取当前 Identity 内容输入、工作台以及用户已经确认的本 Gate 行为规则。

注意：

- Prototype Test Value 不自动成为正式数值；
- Strong Candidate 不自动成为 Frozen Rule；
- Trait OFF；
- 不扩 Farmer / Herbalist；
- 不做完整 Profession Card Pool；
- 不把 DemoCardPool 提升为正式内容 Authority；
- 尽可能走现有真实 Person / Run Deck / Shared Deck 链。

九张卡目标：

> 0 个 CardId 专属执行分支。

---

# 17. G0-T6｜Verification + Product Integration

必须同时完成：

## 17.1 Module Verification

按最小范围执行：

- EditMode；
- 必要 PlayMode；
- 编译；
- Console；
- 规则专项回归。

## 17.2 Product Integration Verification

必须从真实玩家产品路径进入现有 Battle 宿主。

需要证明：

- 真实 Person Owner；
- 真实 Shared Deck；
- 真实 Shared AP；
- 真实 Battle UI；
- 真实 Position / Range / Intent；
- 新配置卡牌真实结算；
- 上下游没有被 Debug/Test Harness 替代。

独立 Runner / Mock / Harness 只能证明模块，不得代替产品集成。

---

# 18. Prototype UI 最低要求

Gate 0 不是正式视觉工作包。

只要求测试者能够识别：

- Card Owner；
- Actual Cost；
- 当前 Damage（如果该 Card 有 Damage）；
- 临时加工状态；
- Shield；
- Worker 合法 Move 目标；
- Worker 合法 Swap 目标；
- 《切配》当前自动目标。

具体表现：

> Test / Not Frozen。

可以使用低成本 Debug Label、Highlight、Outline、临时状态文字。

不得因为 UI 美术不完整而扩大成本。

如果最终玩家无法预测《切配》目标：

> Product Integration 不通过。

---

# 19. Verification Matrix

至少覆盖以下契约。

## Card Runtime

- Base 与 Actual 分离；
- Modifier 不写静态模板；
- Additive Modifier 可重复；
- Cost ≥ 0；
- Damage ≥ 0；
- 不同实例不串状态；
- 不同 Owner 不串状态。

## Operation Applicability

- 无 Damage Card 仍可被《切配》选中；
- Cost Modify 生效；
- ModifyDamage 对不存在 Damage 的 Card 为 No-op / Not Applicable；
- 不因此让整个 Card Resolution 失败。

## Deck Cycle

- Discard 不 Reset；
- Retain 不 Reset；
- Discard → Draw Reshuffle Reset；
- 只 Reset 真正进入下一 Cycle 的具体 Card；
- Exhaust 不错误回流。

## Selector

- Actual Cost；
- MaxBy；
- Leftmost Tie Break；
- 非随机；
- Target Binding 稳定。

## Play Requirement

- Primary Resolution 无合法结果 → 不可打；
- Bonus 未触发 → 仍可打；
- AP 不足 → 不可打；
- 不出现“先扣 AP，再发现核心无目标”。

## Spatial

- Move 原子；
- Swap 原子；
- Grid / Unit Position 一致；
- Range 即时刷新；
- Adjacency 即时刷新；
- 非法操作无副作用。

## Shield

- 吸收顺序正确；
- 剩余 Damage 正确；
- Turn Start 清理；
- Battle 结束清理；
- 不进 Save。

## Existing Battle Regression

不得破坏：Shared Deck、Shared AP、Draw / Discard / Reshuffle、Retain、Exhaust、Person Ownership、Range、Enemy Intent、失去战斗能力后人物牌移除、Run Deck、Battle → Return → Person 既有长期链。

---

# 20. Disposable Tests by Default

开发期间允许创建 Gate Test、临时 Test Runner、Acceptance Test、临时日志、临时诊断脚本。

Final Closeout 前必须分类：

## Long-term Regression

只保留真正保护已确认核心契约的最小代表测试。

## Disposable

默认清理：

- 为定位问题建立的一次性测试；
- 重复覆盖同一契约的测试；
- 临时 Runner；
- TestResults；
- 临时日志；
- 临时诊断脚本。

“以后也许有用”不是长期保留理由。

---

# 21. Risk Level 与执行自主权

本任务属于：

> L3 玩法功能 + 局部 L4 Battle 核心流程 / 公共结构风险。

开始实施前必须：

- 读取 Local Git 实时现场；
- 记录 Console 基线；
- Existing Host Map；
- Impact Analysis；
- 文件所有权检查。

然后按三档：

- Green：当前 Work Package 内、复用现有宿主、无新设计、可回滚 → 自主执行；
- Yellow：必要局部适配、测试调整、少量新文件、可修复编译问题 → 自主执行并在报告说明；
- Red：跨 Work Package、必须改变未授权 Current Rule、SaveVersion / 高风险 Save Schema、必须建立第二套正式产品路径、必须改变已冻结 Architecture / Authority、Scope 无法在当前 Gate 0 内完成 → `Awaiting Decision`。

普通技术实现问题不得升级成用户审批 Gate。

---

# 22. 文件所有权与本地现场

Cline 开始时必须记录：

- Owner；
- Local branch；
- Local HEAD；
- Modified；
- Untracked；
- Staged；
- 当前任务预计写入范围；
- 其它 Agent 正在占用的文件。

工作区不干净不自动停止。

只有本任务会覆盖、混淆或无法隔离他人修改时才暂停重叠部分。

同一文件不得多个 Agent 并行写。

---

# 23. Git 权限

本 Development Spec：

> **不包含 Commit / Push 授权。**

开发、验证、必要 Acceptance、Final Closeout 准备完成后：

> 停在 `Ready for Commit / Push`

并输出：

> `Closure Commit Staging Manifest`

必须先完成 Disposable Cleanup、Final Diff / Workspace Review、文件所有权核对、任务外变化隔离。

未经本任务单独明确授权：

> 不 Commit，不 Push。

---

# 24. Final Closeout

当技术开发和必要 Acceptance 已通过后，由同一个 Development Agent 在授权范围内完成：

1. Handoff 实际落盘；
2. 回读 Handoff；
3. 必要 Current 状态同步；
4. Disposable Cleanup；
5. Final Diff / Workspace Review；
6. Closure Commit Staging Manifest；
7. 按当前任务 Git 授权执行或停在 `Ready for Commit / Push`。

不得把聊天总结当作 Handoff 文件。

---

# 25. Gate 0 Technical PASS

只有同时满足以下条件，才能写：

> `Gate 0 Technical Verified`

- 九张 Core Proof 内容能在真实 Battle 路径运行；
- 无 CardId 专属效果实现；
- 没有第二套正式 Battle / Deck / Person / Position Authority；
- Primary Resolution 合法性正确；
- 《切配》自动目标正确、稳定、可预测；
- Modifier 生命周期正确；
- Current Deck Cycle Reset 正确；
- Move / Swap 空间 Authority 一致；
- Shield 基础生命周期正确；
- Existing Battle Regression 无破坏；
- Product Integration Verification 通过；
- 没有把 Test Harness 冒充产品集成。

---

# 26. Gate 0 不等于 Product PASS

Gate 0 Technical Verified 之后进入：

```text
Space Test
→ Card Economy Test
→ Timing Test
→ Mixed Freeform
→ 3P Product Gate Review
```

人工产品 Gate 关注：

- Cross-Person Revaluation；
- Order Sensitivity；
- Shared AP Competition；
- Identity Visibility；
- Shared Deck 是否出现真正构筑乘法，而不是三套小卡组混合。

这些结果不能由自动测试替代。

---

# 27. 明确不进入的范围

Gate 0 不进入：

- Farmer Growth；
- Herbalist 完整 Status；
- Hunter Focus；
- Worker History / 接力；
- Cook 全部未来加工体系；
- 完整 Trait 系统；
- 4P / 6P；
- 正式数值平衡；
- 正式 UI 美术；
- 完整卡牌编辑器；
- 万能 Effect DSL；
- 完整 Modifier Stack Framework；
- Legacy Battle 大清理；
- Save Schema 重构；
- Identity / Profession 全工程重命名；
- 第六 Identity；
- FP-V0.1 最终整体体验验收。

---

# 28. NEXT ACTION

本 Spec 落仓并确认后：

> 由一个 Cline Development Agent 接手整个 Gate 0 Repository Work Package。

启动顺序：

```text
Local Workspace Audit
→ Authority Read
→ Existing Host Map
→ Impact Analysis
→ G0-T1
→ G0-T2
→ G0-T3
→ G0-T4
→ G0-T5
→ G0-T6
→ Technical Report
→ 必要 Product Acceptance
→ Final Closeout
→ Ready for Commit / Push（若尚未获得 Git 授权）
```

不需要：

- 每个 Technical Checkpoint 重开一个对话；
- 每阶段人工批准；
- 每阶段独立 Handoff；
- 每阶段独立 Commit。

只有 Green / Yellow 以外的 Red 问题才回到协调层。

---

# 29. 状态声明

截至本 Spec 生成时：

- 角色内容设计阶段已收口，可进入 Prototype；
- Identity 内容仍为 Concept / Not Fully Frozen；
- Gate 0 设计输入已明确；
- Gate 0 Technical Architecture 尚未由本地 Cline 实际 Audit；
- 自动化验证尚未执行；
- Product Prototype 尚未执行。

因此当前 Work Package 状态：

> **Ready for Development Audit / Not Yet Implemented**
