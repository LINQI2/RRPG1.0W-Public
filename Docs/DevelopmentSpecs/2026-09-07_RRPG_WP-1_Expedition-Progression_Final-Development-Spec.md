# RRPG WP-1 Expedition Progression — Final Development Spec

> 日期：2026-09-08
> 项目：RRPG
> 阶段：FP-V0.1
> 工作包：WP-1｜Expedition Progression Foundation
> 类型：Final Development Spec（FDS）
> 状态：**Executed / Development Closed**（Stage 1–3 已完成并通过验收；Final Completion Audit 已完成）。
> Original Issue Status：**制作人审查修订版 / Ready for Stage 1 Development Execution（仍需逐 Stage Gate）**。
> 当前职责：保留为 WP-1 的 Development-time Binding 与验收追溯资料；不再表示等待执行的新任务，也不升级为长期游戏规则 Authority。
> 历史编号边界：本 FDS 正文中的 Product Home World“WP-2”与 Village Food“WP-3”均为最初发布时的规划标签；其中 Product Home World“WP-2”不是当前 Milestone A WP-2。当前 WP-2 的核心闭环是 **Expedition Facts → Persistent Person State**。
> 前置：WP-1 / WP-2 / WP-3 Architecture Closure + Cross-document Closure 已 PASS
> 规则 Authority：
> - `GameSpec/Expedition/Expedition-Progression-GameSpec-V0.1.md`（WP-1 执行 Authority）
> - `GameSpec/Village/Village-GameSpec-V0.1.md`（VillageTime / Assignment / Occupancy 边界）
> - `GameSpec/Village/Village-Resource-Coupling-GameSpec-V0.1.md`（Supply / Carry 语义 cross-sync）
> - `GameSpec/Village/Product-Home-World-GameSpec-V0.1.md`（Product Home 只读边界）
> - `Docs/DesignCheckpoints/2026-09-07_RRPG_FP-V0.1_Cross-Document-Closure-Checkpoint.md`（Y1–Y5 制作人裁决）
> - `GameSpec/08-地图系统规格.md`（**LEGACY / SUPERSEDED**，仅 Compatibility / 历史参考）
> - V4F Return / Catch-up / Save 事实：`Docs/开发验收交接.md`、`Docs/项目状态快照.md` 与既有实现
> 本文件提出的类名 / 方法名 / 文件路径为 **WP-1 Development-time Binding**（由本 FDS 冻结供开发使用），不反向冻结 GameSpec 中明确的职责角色名；正式数值一律 Test / Not Frozen。

---

## 1. Scope

### 1.1 WP-1 负责（本 FDS 只覆盖此范围）

```text
Start Expedition
↓
Initialize Progression State
↓
Enter Node
↓
Resolve Node Outcome
↓
Complete Node exactly-once
↓
Accumulate integer elapsed Village Days
↓
Deduct Base Supply exactly-once
↓
Resolve Branch / Next Node
↓
Continue or Request Return
↓
Handoff to existing V4F Return transaction
```

### 1.2 不负责（严禁偷渡）

- Product Home World 实装（WP-2）；
- Farm / Kitchen / Ingredients / Hunger / Village Food Runtime（WP-3）；
- Run Save / Resume；
- Daily Schedule / Work Hours；
- Personality / Relationship；
- final Map UI / 正式 Node 内容池 / 正式节点生态；
- final Shop / Recruit / Rest ecology；
- final 2D / 2.5D / 3D；
- 多 Scene 重构；
- 任何 SaveData / SaveVersion 提升。

### 1.3 生成前提（本 FDS 依据的真实仓库事实）

- Git HEAD：`2f7c73c5e6a4ccef1f5d2011c6e5ace9acf167bb`（main，与 origin/main 同步）；工作区仅剩历史遗留 untracked `UnityProject/RRPG1.0game/TestResults/m6_playmode.log`（只报告、不纳入）。
- FP-V0.1 Architecture/Cross-Document Closure Commit（`docs(fp-v0.1): close WP1-WP3 architecture and alignment`）已存在于 main。
- 真实代码现状：Expedition 仍是 **Legacy Map runtime（4×3 点阵）**；Node-based Progression 尚未实现；V4F Return / Catch-up / Final Save 已落地并通过验收。

---

## 2. 真实仓库审计（仅列 WP-1 相关的代码级事实）

### 2.1 已核对实现与归属

| 真实文件（UnityProject/RRPG1.0game/Assets/…） | asmdef / 层 | 当前事实 |
|---|---|---|
| `Scripts/Adventure/AdventureManager.cs` | Assembly-CSharp | 顶层冒险宿主；ctor 创建 `RunState` + `MapGen = new MapGenerator()`；`StartAdventure(rows=4, columns=3)` 只调 `MapGen.Generate`；`ProcessNodeClick(MapNode)` 以 `node.Reachable && !node.Visited` 校验并用 `MapGen.SelectNode` 推进；`EndRun` / `SettleReturn`（纯结算、不含 Save）为 Return 入口；无任何 Node-based Progression |
| `Scripts/Adventure/MapGenerator.cs` | Assembly-CSharp | Legacy 点阵生成：`Nodes / CurrentNode / BossNode / IsAtBoss / Generate / SelectNode / GetNextNodes`；`Generate` 首节点即起点（Battle），末节点固定 Boss；`SelectNode` 维护 `Reachable / Visited` |
| `Scripts/Adventure/MapNode.cs` | Assembly-CSharp | `NodeId / Type(NodeType) / PosX / PosY / Connections / Visited / Reachable / Selected`；`NodeType` = Battle/Elite/Boss/Event/Shop/Rest/Recruit |
| `Scripts/Adventure/ExpeditionSystem.cs` | Assembly-CSharp | Prep 系统：选 1 英雄 + 角色卡 ≤6 + 道具卡 ≤6；`ConfirmExpedition` 读取统一 Temporary Occupancy 防线 |
| `Scripts/Adventure/RunConfig.cs` | Assembly-CSharp | Run 配置 SO（战后恢复 / 领悟阈值），全测试值未冻结 |
| `Scripts/Battle/Stage1/RunState.cs` | BattleStage1.Runtime | Run 连续态：人物 CurrentHP/MaxHP、Run Deck、Insight、Pending 收益（Gold/建材/GrowthExp/ProfessionExp）、Runtime Checkpoint（Test / Not Frozen）；**不含 CurrentNode / Branch / elapsed days / Supply**；`CommitPendingRewards` / `CommitPendingProfessionExp` / `DiscardPendingRewards` 供 SettleReturn 使用 |
| `Scripts/Village/Runtime/ExpeditionRecord.cs` | Village.Runtime | **只承载 V4F Return 会话**：Phase（Active→ReturnSettled→CatchUpComplete→OccupancyCleared→Completed/Failed）、`EffectiveElapsedDays`、`DaysSimulated`、`Completed`；不持久化 |
| `Scripts/Village/VillageRuntimeBridge.cs` | Assembly-CSharp | Village 运行时桥：`Time/State/Assignments/Projects/...` 单例；Expedition Occupancy 登记 `SetExpeditionPersonIds / ClearExpeditionPersonIds`；elapsed-day 注入 seam `SetExpeditionElapsedDaysProvider(Func<int>)` + `ResolveExpeditionElapsedDays()`（默认 provider 恒 0）；V4F `BeginExpeditionReturn / MarkExpeditionReturnSettled / RunReturnCatchUp / CompleteExpeditionReturnSave / DiscardExpeditionReturnRecord`；`HandleDayTick` 挂 `VillageTime.DayTicked` |
| `Scripts/UI/ExpeditionUI.cs` | Assembly-CSharp | 产品流程宿主：`OnConfirmExpedition` 创建 AdventureManager + `StartAdventure` + `SetExpeditionPersonIds(adventure.GetExpeditionPersonIds())` + MapUI.ShowMap 回调；`HandleNodeClick` → NodeAction → Battle（`BattleUI.SetRunState` + `InitFromExpedition` + `StartBattleWithExpedition`）或 Event 占位；`OnReturnToTown`（Retreat）、`OnReturnFromBattle(isVictory)`（Defeat / Boss SafeReturn 判断用 `MapGen.IsAtBoss`）、`FinalizeReturn(mode)` = V4F 冻结顺序统一入口 |
| `Scripts/UI/MapUI.cs` | Assembly-CSharp | 展示 `MapGenerator` 的 4×3 节点；`rows/columns` 默认 4/3；点击回调；`RefreshDisplayOnly` / `HideMap` |
| `Scripts/UI/BattleUI.cs` | Assembly-CSharp | Battle 宿主；`DoSettlement`（Settlement → RunState Pending 写入 → 领悟/奖励）→ `ShowBattleResult` → BattleResultUI 确认 → `ExpeditionUI.OnReturnFromBattle(isVictory)` |
| `Scripts/Village/Runtime/VillageTime.cs` | Village.Runtime | 唯一 Village 时间源：`Year/Month/Day/SecondsIntoDay`、`TickDay()/JumpToNextMonth()/Update()`、`DayTicked` 事件 |

### 2.2 asmdef 边界（真实）

- 纯逻辑运行库（仅引用 UnityEngine，autoReferenced=true）：`BattleStage1.Runtime`、`Village.Runtime`、`Growth.Runtime`、`Profession.Runtime`。
- 测试库引用方式：EditMode/PlayMode asmdef 强引用 `Village.Runtime` / `BattleStage1.Runtime`；Assembly-CSharp 对象经反射访问（团结引擎限制）。
- EditMode V4F 现有专项 15 项（5 文件 + V4FTestKit）；PlayMode V4F 3/3（Retreat / Defeat / SafeReturn 产品 Return 路径，v17 GUI+Unity MCP 通过）。

## 3. Authority-to-Code 映射（本 WP 的开发边界）

> 本节把 Cross-WP Authority Map 锁定到真实代码对象；命名仅为 FDS binding。每项同时标注：直接复用 / Compatibility-only / 新增 / 禁止修改。

### 3.1 AdventureManager = Expedition Orchestrator（复用 + 收敛）

- 现有 `AdventureManager` 继续作为顶层 Expedition 流程宿主（`ExpeditionUI` 创建它、Battle 用它回写 RunState、Return 走它 `SettleReturn`）。
- **收敛**：在本 WP 内把「地图点击规则引擎（`ProcessNodeClick`）」收敛为 Orchestrator 下的 Node Progression 驱动（不新建第二套顶层 Manager、不新建平行 Authority 单例）。
- 新增持有（会话内，不持久化）：`ExpeditionProgressionState`（§5）。
- **禁止**：把 ProgressionState / Node / Branch / elapsed days / Supply 塞进 `RunState`；禁止把 `AdventureManager` 变成万能状态容器（不得吞并 VillageReturnState / RunState / ExpeditionRecord 职责）。

### 3.2 RunState（不修改）

- 只继续承担 Run / Battle continuity（HP / Deck / Insight / Pending，见 §2.1）。
- **禁止**：增加 `CurrentNode / Branch / ExpeditionElapsedDays / Progression completion flags / Supply` 字段或方法；禁止让 Battle 层直接写 Progression。

### 3.3 Expedition Progression State（新增）

- 独立会话内状态（新 Runtime 纯逻辑对象，不进 SaveData、不提升 SaveVersion），至少表达：
  - current node identity；
  - entered / completed progression facts（节点进度事实）；
  - progression phase（含 route-terminal / return-requested）；
  - integer `ExpeditionElapsedDays`；
  - current expedition carry / supply state；
  - exactly-once node resolution guard；
  - branch / next-node availability。
- 类名 / 字段见 §7（FDS binding），数量以最小会话内状态为上限。

### 3.4 ExpeditionRecord / V4F Return Transaction（禁止修改）

- `ExpeditionRecord` + `VillageRuntimeBridge` 的 V4F 会话/推进方法只承担 Return transaction / exactly-once（Settlement → Keep Occupancy → Catch-up → Clear → Final Save → Town）。
- **禁止**：把 ProgressionState 塞入 ExpeditionRecord；禁止让 Progression 直接改写 `EffectiveElapsedDays / DaysSimulated / Completed / Phase` 或 RunReturnCatchUp 内部顺序。

### 3.5 Supply（抽象新增，不建第二套 Village 资源 Authority）

- 本 WP 只新增「Expedition 会话内抽象 Carry/Supply State」（消费侧），Village Food / Ingredients 长期 Authority 属于 WP-3，本 WP **不实现、不复制、不扣减 Village 库存**。
- FP-V0.1 产品映射（语义对齐，非本 WP 实现）：1 unit Carry = 1 unit Village Food；Departure 真实资源转移（`Village Food -= Carry; Expedition Carry += Carry`）由 **WP-3 Departure seam** 注入，WP-1 只预留读取入口。
- 数值（PartySize / PerPersonDailyRate / NodeElapsedDays 默认 / Carry 默认）全部 **Test / Not Frozen**，通过策略对象/注入 seam 提供。

### 3.6 V4F Return（只读复用，不改写 transaction）

- WP-1 只提供 Return **request**、`EffectiveElapsedDays`（= 当前会话 `ExpeditionElapsedDays`）、可读的 carry / participant 上下文、exactly-once handoff 输入。
- 既有 `ExpeditionUI.FinalizeReturn(RunEndMode)` 冻结顺序不动；elapsed-day 值来源由 WP-1 在出征登记时把 `_elapsedDaysProvider` 绑定到当前 Progression State（§9 接缝），`ResolveExpeditionElapsedDays()` 调用位置与 V4F transaction 内部逻辑不改。

---

## 4. Development Invariants & Do Not Break

以下边界在 WP-1 全程硬保护（违反即 Red）：

1. PersonId / PlayerCollection 长期人物真相源不动（M1 边界）。
2. Card → Person 显式绑定不动；模板 ID 不得当作 Person 身份。
3. VillageTime 唯一时间权威源：Progression **禁止每 Node 直接 Tick VillageTime**；只累计整数 elapsed days，Return 时才进入 Catch-up 逐日 TickDay。
4. Assignment / Occupancy（Primary + Temporary: Project / Expedition / Unavailable）不动；远征登记沿用 `VillageRuntimeBridge.SetExpeditionPersonIds`；Catch-up 期间保持 Occupancy。
5. V4F Return 冻结顺序与 exactly-once `Completed` 不动；`IsReturnCatchUpRunning` 防重入与 `Completed` 跨调用终态语义不互换。
6. Final Save 只发生一次（`FinalizeReturn` → `ReturnToTownWithFinalSave`）；Progression 不引入任何 Save 调用。
7. SaveData / SaveVersion 不动；ProgressionState / Supply / elapsed days **不持久化**；Run Save / Resume 维持 Deferred。
8. Product Home World 只读边界不动（本 WP 不写任何 World / Place / Camera 表现）。
9. WP-3 资源 Authority 不动：本 WP 不建 Village Food/Ingredient 长期数据、不做 Farm/Kitchen/库存。
10. Existing Battle flow / Battle Settlement / Growth / Pending Reward 不回退：WP-1 不改变 Battle 胜负、HP 回写、领悟、奖励结算与 `SettleReturn` 语义。
11. Legacy 历史代码不删除：本 WP 只做标记 + 兼容收敛；`GameSpec/08-地图系统规格.md` 不重新升级 Current。
12. 普通 Node 禁止「Time + fixed Supply 双扣」。

---

## 5. Y5：integer Village Days — 代码级锁定

> FP-V0.1 elapsed time Authority 是 **integer Village Days**，同一数值同时驱动 Supply 与 V4F Catch-up。

```text
NodeElapsedDays  = integer >= 0                      // 每个 Node 的推进耗时（成功完成且 exactly-once 才计入）
ExpeditionElapsedDays += NodeElapsedDays             // Node 成功完成且 exactly-once 时累加
BaseSupplyConsumption  = PartySize × NodeElapsedDays × PerPersonDailyConsumptionRate
EffectiveElapsedDays   = ExpeditionElapsedDays        // V4F Return Catch-up 输入
DaysSimulated          == EffectiveElapsedDays        // V4F 逐日 TickDay 最终一致性
```

约束：

- Progression 只维护**一套** elapsed days；禁止用 Node count 替代、禁止小时/小数天。
- NodeElapsedDays 由 Node 定义/策略提供（Test 默认值，Not Frozen；可注入 0 / 2 / N 测试值）。
- 每个 Node 成功完成且 exactly-once 时，elapsed 累加与 Base Supply 扣减发生在**同一次原子 complete 事务**内。
- Catch-up 逐日推进数量必须等于 `EffectiveElapsedDays`（既有 `DaysSimulated` 语义验证）。

## 6. Supply 边界与扣减模型

### 6.1 抽象 Expedition Supply（Progression 消费侧）

Progression 层只消费**当前远征会话内**的 carry/supply state：

```text
ExpeditionCarryState
  InitialCarryUnits     // 出发时转入的 Carry（真实转移由 WP-3 seam 注入；本 WP 会话默认 = 注入/测试值）
  CurrentCarryUnits     // 当前剩余（每次 complete 原子扣减后更新；Return 前不清空）
  PartySize             // 本次远征参与者数（= 出征登记 PersonId 数，RunState 人物数只读对齐）
  PerPersonDailyRate    // Test / Not Frozen；可注入
  ReservedExtra         // Explicit Extra Supply Cost 仅在特殊 Node 定义出现时使用
```

### 6.2 扣减公式（Y5 单一 Authority 派生）

```text
BaseSupplyConsumption
= PartySize
× NodeElapsedDays                    // 同一 Node 成功完成且 exactly-once 时的整数值
× PerPersonDailyConsumptionRate

TotalSupplyCost
= BaseSupplyConsumption
+ (ExplicitExtraSupplyCost if node defines one)

CurrentCarryUnits -= TotalSupplyCost   // 在同一个 complete 原子事务内
```

### 6.3 禁止

- 普通 Node 在产生 Time-derived Base Supply 消耗后再固定扣 Supply（无 Time + fixed 双扣）；
- 建立第二套长期 Village Food Authority；
- 让 WP-1 自行实现 WP-3 资源经济（Farm / Kitchen / Inventory / Hunger）；
- Return 提前清空 carry / Progression 自行向 Village 返还 Carry（返还 hook 归 WP-3 FDS）。

### 6.4 Insufficient Supply（FP-V0.1 无惩罚）

- 对 **进入前即可确定成本** 的普通 Node，必须在真正启动 Battle / Event / Situation 内容前做 affordability validation：
  `CanEnterNode / TryEnterNode` 同时检查预计 `NodeElapsedDays + ExplicitExtraSupplyCost` 对应的 Supply 是否可承担。
- Supply 不足时返回 `ProgressionResult.InsufficientSupply`，**不得启动 Node 内容、不得进入 Resolving、不得累计 elapsed days、不得扣减**。
- 不允许出现“玩家已经打完整场 Battle，结算时才发现补给不够，因此整场不算”的产品逻辑。
- 未来若存在“进入后才揭示的额外成本”，只能作为明确的 Outcome / Event Extra Cost 单独处理；FP-V0.1 普通 Node 不使用这种隐藏成本。
- FP-V0.1 不实现自动失败 / 自动返程 / 饥饿 / 伤害惩罚。
- Return / Retreat 是独立操作，**不受 Insufficient Supply 阻塞**。

---

## 7. Node Progression：真实 API / 方法职责（FDS binding）

> 以下类与签名由本 FDS 为 WP-1 冻结（Development-time binding）；正文规则仍以 Expedition-Progression-GameSpec 为准。所有方法职责在下方逐条给出；每个返回结果均为结构化结果，便于 exactly-once / 失败语义测试。

### 7.1 核心不变量

- **Enter ≠ Complete**：进入节点与节点内容完成是两个阶段（对应 GameSpec §3 `Not Entered → Entered/Resolving → Resolved`）。
- exactly-once：同一 Node 的 UI 重开 / Battle 返回 / 重复查询 / 重复调用不产生第二次 elapsed 累加与第二次 Supply 扣减。
- 0 / 1 / N 后继：0 = progression terminal（只表示没有普通后继，**绝不自动等于成功完成远征**）；1 = 无意义选择不制造；N>1 = Branch Choice，只能选合法后继。成功完成路线必须由显式 `SuccessfulRouteTerminal / RouteTerminal` 语义决定。

### 7.2 建议新文件（纯逻辑 Runtime 层）

路径前缀 `UnityProject/RRPG1.0game/Assets/Scripts/Adventure/Progression/Runtime/`（新 asmdef `ExpeditionProgression.Runtime`，仅引用 UnityEngine）：

| 文件 | 职责 |
|---|---|
| `ExpeditionNodeKind.cs` | 内容种类枚举（Battle / Elite / Boss / Event / Story / Discovery / Rest / Terminal）。与 Legacy `NodeType` 保持 adapter 映射，二者不混为一个 Authority |
| `ExpeditionNodeData.cs` | Node 定义：`NodeId`、`NodeKind`、`Connections(string[])`、`NodeElapsedDays(int >= 0)`、`ExtraSupplyCost(int = 0)`、显式 `SuccessfulRouteTerminal(bool)`。不承载运行进度；**0 successor 本身不得推导 SuccessfulRouteTerminal** |
| `ExpeditionGraph.cs` | 会话内 Node 集合 + 根入口 NodeId 的只读定义容器（含 `TryGetNode`）；初始化时一次导入 |
| `ExpeditionProgressionState.cs` | 会话内真相：`CurrentNodeId`、`EnteredNodeIds`、`ResolvedNodeIds`、`CompletedNodeIds`、最小 phase enum（可内嵌）、`ExpeditionElapsedDays(int)`、`RouteCompleted(bool)`、graph 引用 |
| `ExpeditionSupplyState.cs` | §6 的 carry/supply state + `CanAfford / ApplyConsumption` |
| `ExpeditionProgressionResult.cs` | 结果枚举/结构：Success、InvalidEntry、AlreadyEntered、AlreadyResolved、AlreadyCompleted、NotEntered、InsufficientSupply、NoSuchNode、TerminalReached、FailedOutcome… |
| `ExpeditionProgressionResolver.cs` | 唯一规则入口：以下方法全部在此，禁止 UI/Map 直接改 state 作为规则执行 |

> **最小化约束**：Stage 1 不得仅为“未来可能扩展”新增无当前消费者的 Strategy / Policy 层。`NodeElapsedDays` 直接由 Node 定义提供；若未来出现真实第二种时间计算消费者，再单独引入 Policy。`ProgressionPhase` 若仅被 State 使用，可作为内嵌 enum，不强制独立文件。

### 7.3 Resolver 方法职责（真实 API）

- `InitializeProgression(ExpeditionGraph graph, int partySize, int initialCarryUnits, int perPersonDailyConsumptionRate)`：初始化 Progression State（根节点进入候选就绪、elapsed=0、carry=注入初值）。**不引入未定义 `PartyContext` 抽象。**
- `GetAvailableNextNodes()`：当前合法后继。NotStarted → 根节点；成功完成当前 Node 后 → 该 Node 后继（0/1/N）；其它状态 → 空。
- `CanEnterNode(nodeId)` / `TryEnterNode(nodeId)`：校验 ① node 存在；② 属于 `GetAvailableNextNodes()`；③ 当前允许进入；④ 对普通已知成本 Node，预计 Supply 足够。Supply 不足时直接 `InsufficientSupply`，**不得启动 Node 内容**。成功后仅更新 entered/current 事实。**只表示「到达」，不代表内容完成，也不发生 elapsed/supply 扣减。**
- `TryResolveCurrentNode(contentOutcome, out NodeResolutionResult result)`：**Node 内容结果的唯一 exactly-once 结算入口**。
  - guard：同一 `CurrentNodeId` 只能 Resolve 一次；重复调用 → `AlreadyResolved` no-op；
  - 无论 Battle Victory 还是 Defeat，只要 Node 内容确实已经发生并得到最终 outcome，本次经历的 `NodeElapsedDays` 与对应 Base Supply 都必须 exactly-once 结算；
  - `ExpeditionElapsedDays += NodeElapsedDays` 与 `CurrentCarryUnits -= TotalSupplyCost` 在同一原子 resolution 中发生；
  - **成功 outcome**：加入 `CompletedNodeIds`，允许后续 Branch / Next；若 Node 显式 `SuccessfulRouteTerminal=true`，才置 `RouteCompleted=true`；
  - **失败 outcome（例如 Battle Defeat）**：记录为 Resolved but not Completed，不开放普通后继；随后由 controller 以 `RunEndMode.Defeat` 走 Return handoff。失败仍消耗已经发生的时间与 Supply；
  - 0 successor 仅表示 terminal；若未显式 `SuccessfulRouteTerminal`，不得置 `RouteCompleted` / SafeReturn。
- `TryChooseBranch(nextNodeId)`：1 后继可由 controller 直接继续；N>1 校验合法后继后进入；0 后继只返回 TerminalReached，不推导胜利。
- `RequestReturn()`：**Expedition 层独立操作**，不属于任何 Node 后继；置 ReturnRequested 并向外暴露 handoff 输入（elapsed days / carry 只读 / participant context）。真正的 Settlement/Catch-up/Clear/Final Save/Town 由既有 V4F 执行。
- `TryAdvanceToNextOrReturn()`：供 controller 判断「存在普通后继 → 继续；成功路线终点 → 可 SafeReturn；失败 outcome → Defeat Return；玩家主动选择 → Retreat」。

### 7.4 AdventureManager 侧收敛方法（Assembly-CSharp）

在 `AdventureManager` 上收敛 Orchestrator 入口（签名 FDS binding，实现以保持既有 Battle/Return 兼容为前提）：

- `StartProgression(int rows = 4, int columns = 3)`：替代/包装 `StartAdventure` 语义——经 `LegacyMapGraphAdapter` 导入 Graph → `InitializeProgression`（MapGen 作为 Compatibility Graph 源；默认 policy/seed 测试值注入）；同时保留 `MapGen` 引用供既有展示/回归。
- `GetProgressionElapsedDays()` / `ExpeditionElapsedDays` 只读属性（handoff 用）。
- `TryEnterNode` / `TryResolveCurrentNode` / `TryBranch` 转发 Resolver（**只供 ExpeditionUI / AdventureManager controller 层调用；MapUI 不得直接执行 Progression mutation**）。
- 保留 `ProcessNodeClick / StartAdventure / SetVictory / SetDefeat / SettleReturn / EndRun` 既有 API 与语义（既有 EditMode/PlayMode 回归零行为变化；新流程不依赖其作为规则）。

## 8. Branch / Return / Retreat 规则（产品侧绑定）

- Branch：
  - 0 successor：**progression terminal only**，只表示没有普通后继；不得自动推导 SafeReturn / `RouteCompleted`；
  - 1 successor：直接进入下一个 Node，**不制造 Branch Choice UI**；
  - N>1：进入 Branch Choice，只能选择合法后继（Resolver 校验）。
- 成功路线完成必须由 Node 定义显式 `SuccessfulRouteTerminal=true`（FP-V0.1 Legacy 映射可由 Boss Node adapter 赋值）决定。
- Return / Retreat：**Expedition 层独立操作**。
  - **禁止**设计 `ReturnHomeNode` / 普通 branch successor / Legacy map 特殊格。
  - `RequestReturn()` 只置位并 handoff 到既有 V4F；Progression **不得**自行实现 Catch-up / Clear / Final Save / Town restore。
- SafeReturn 资格（FP-V0.1 当前映射，保持与既有产品路径一致的语义）：
  - Boss 击败（route 终点完成）→ 玩家发起 Return 时按 `RunEndMode.SafeReturn`（既有 `ExpeditionUI.OnReturnFromBattle` 的 Boss 分支语义对齐）；
  - 主动撤退（任意时刻 Return 按钮）→ `RunEndMode.Retreat`；
  - 战斗全灭 → 当前 Node 先以失败 outcome 执行 `TryResolveCurrentNode(Defeat)`，**exactly-once 结算该段 elapsed days 与已发生的 Supply 消耗**，随后 `RunEndMode.Defeat`。
  - 本 WP 只把这些结果语义经 Return request 透传给既有 FinalizeReturn；**不冻结**奖励保留比例 / 失败损失（沿用 Not Frozen）。
- Return 前最后一个已经发生最终 outcome 的 Node 必须先执行 exactly-once resolution，再进行 Return handoff，确保 Victory / Defeat 都不会漏记 elapsed/supply；主动 Retreat 若发生在尚未启动任何 Node 内容前，则不额外结算未发生的 Node 成本。

## 9. Legacy Map Compatibility（逐项代码级审查结论）

> 规则 Authority：GameSpec Expedition §13 + `GameSpec/08-地图系统规格.md`（LEGACY/SUPERSEDED）。下列逐项标记来自真实实现审查。

| Legacy 成员 | 标记 | 处理 |
|---|---|---|
| `MapGenerator.Generate(rows, columns)` | **Compatibility-only（允许临时提供 Graph）** | Stage 2 经 `LegacyMapGraphAdapter` 一次性导入为 `ExpeditionGraph`（Node 定义 + Connections）。可作为 Compatibility Graph 源 / 默认临时来源；正式生成算法 / 内容池不在 WP-1 |
| `MapGenerator.CurrentNode` | **Do not use as new Authority（Can remove later / Must not break）** | 新 Progression 的当前节点真相 = `ExpeditionProgressionState.CurrentNodeId`；既有调用保留但只作 Legacy 展示/回归 |
| `MapGenerator.IsAtBoss` | **Do not use as new Authority（Must not break）** | SafeReturn 资格收敛到显式 `SuccessfulRouteTerminal` 成功 outcome 后的 `ProgressionState.RouteCompleted`；Legacy adapter 可把 Boss 映射为成功路线终点，但 **0 successor 不等于 RouteCompleted**；既有读取路径仅回归兼容 |
| `MapGenerator.SelectNode(node)` | **Do not use as new Authority（Must not break）** | 只作 Legacy 视图同步（镜像展示）；新规则推进一律走 Resolver Enter/Complete；禁止 UI 再直接以 SelectNode 作为规则执行 |
| `MapNode.Reachable` | **Do not use as new Authority** | 只作视图派生/镜像：由 Resolver `GetAvailableNextNodes()` 结果同步；不参与规则判定 |
| `MapNode.Visited` | **Do not use as new Authority** | 由 `ExpeditionProgressionState.CompletedNodeIds / EnteredNodeIds` 派生镜像 |
| `MapNode.Selected` | **Do not use as new Authority** | 表现层；无规则含义 |
| `MapNode.Type / NodeType`（Battle/Elite/Boss/Event/Shop/Rest/Recruit） | **Compatibility-only** | 经 adapter 映射到 `ExpeditionNodeKind`（Battle/Elite/Boss/Event/Story/Discovery/Rest/Terminal）；不把 Legacy NodeType 当新 Authority。Shop/Recruit/Rest 生态 FP-V0.1 不做 |
| `NodeAction`（Battle/Event/Shop/Rest/Recruit…） | **Compatibility-only** | 视图回调类型；新流程以 Resolver 结果驱动内容分派，不依赖 NodeAction 作为规则 |
| `MapUI` 4×3 展示 / rows / columns | **Compatibility-only（可保留，不做 final Map UI）** | 作为当前产品流程的最小可视化载体；展示可用节点改由 Progression 派生 |

原则：**Legacy Map flags 不再控制新的 Progression**；WP-1 允许暂时保留旧字段/旧 UI 兼容，但新逻辑真相源必须位于 Progression State / Resolver；任何「Legacy flags 重新成为 Progression Authority」均为偏离本 FDS。

## 10. V4F Return 接缝（WP-1 × V4F 契约）

### 10.1 WP-1 提供（只读 handoff 输入）

```text
Request Return
  → mode (SafeReturn / Retreat / Defeat)
  → EffectiveElapsedDays = ExpeditionProgressionState.ExpeditionElapsedDays   // integer Village Days
  → current carry state 可读（不参与 V4F 内部运算，供 WP-3 后续 hook）
  → expedition participant 上下文（沿用既有 SetExpeditionPersonIds 登记）
  → exactly-once handoff：重复 RequestReturn / 重复 FinalizeReturn 被既有 Bridge 守卫拒绝
```

### 10.2 冻结顺序（不改，原样保持）

```text
Settlement（AdventureManager.SettleReturn，纯结算、不含 Save）
→ Keep Expedition Occupancy
→ Return Catch-up（VillageRuntimeBridge.RunReturnCatchUp：仅 Time.TickDay，逐日 DaysSimulated++）
→ Clear Expedition Occupancy
→ Unique Final Return Save（唯一一次）
→ Town / Home
```

### 10.3 elapsed 值来源接缝

- 出征登记点（`ExpeditionUI.OnConfirmExpedition` 成功分支，已有 `VillageRuntimeBridge.SetExpeditionPersonIds(...)`）同时执行：
  `VillageRuntimeBridge.SetExpeditionElapsedDaysProvider(() => adventure.ProgressionState.ExpeditionElapsedDays)`
- 既有无 Progression 的链路（旧测试/纯 Battle 场景）不注册 provider → 默认恒 0（Catch-up 空跑），零回归。
- 测试仍可用既有 `SetExpeditionElapsedDaysProvider` seam 覆盖注入值（不改 V4F transaction）。
- 约束：`DaysSimulated == EffectiveElapsedDays` 由既有 RunReturnCatchUp 逐日循环天然保证；WP-1 测试必须在产品 path 断言。

### 10.4 Carry Return 边界

- WP-1 只保证：elapsed days 可交付、carry state 可读取、Return **不提前清空 carry**、**不新增第二次 Final Save**。
- 「未消耗 Carry 返还 Village」（SafeReturn/Retreat）由 WP-3 FDS 在既有冻结顺序「Catch-up 完成之后、Clear 之前」插入，本 FDS **不实现该 hook**（Y3 交由 WP-3）。

## 11. File-level Plan（代码开发时的白名单）

> 路径根 = `UnityProject/RRPG1.0game/Assets/`。修改只允许出现在本白名单；未列出文件一律视为 Explicitly untouched。

### 11.1 Existing files to modify（仅 3 个产品文件，全部在 Assembly-CSharp 层）

**A. `Scripts/Adventure/AdventureManager.cs`**
- 当前职责：顶层冒险宿主；MapGen + RunState 持有；地图点击规则；EndRun/SettleReturn。
- 为什么修改：WP-1 §1.1 需要把 Orchestrator 收敛到既有宿主，且 Progression State / Supply / elapsed 需要会话宿主（不进 RunState）。
- 修改内容：新增只读属性/字段持有 Progression（`StartProgression(...)` 初始化）；新增 Resolver 转发方法（§7.4）；保留并复用既有 `MapGen / RunState / SettleReturn / EndRun`。
- 禁止改什么：SettleReturn/EndRun 的 Save 语义；RunState 职责；`ProcessNodeClick/SetVictory/SetDefeat` 既有签名与既有行为（回归兼容）。

**B. `Scripts/UI/ExpeditionUI.cs`**
- 当前职责：产品流程宿主（Prep → 创建 AdventureManager → Map/Battle glue → Return 冻结链）。
- 为什么修改：WP-1 的 Start/Enter/Complete/Branch 需要接进真实产品流程；elapsed provider 需在出征登记点绑定。
- 修改内容：
  1. `OnConfirmExpedition` 成功分支：把 `adventure.StartAdventure(...)` 替换/包装为 `adventure.StartProgression(...)`；`SetExpeditionPersonIds(...)` 之后绑定 `SetExpeditionElapsedDaysProvider(() => adventure.ProgressionState.ExpeditionElapsedDays)`；
  2. Map 点击只由 `MapUI` 上报 `NodeId` 给 `ExpeditionUI`；`ExpeditionUI → AdventureManager → Resolver` 执行 affordability validation 与 `TryEnterNode`，MapUI 本身不得推进 State；
  3. `OnReturnFromBattle` 对 Victory / Defeat 都先调用 `TryResolveCurrentNode(outcome)`：Victory 成功完成并开放后继/显式成功终点；Defeat 记录 Resolved-not-Completed，但同样 exactly-once 结算 elapsed/supply，随后进入既有 `FinalizeReturn(Defeat)`；
  4. Boss/显式 SuccessfulRouteTerminal 胜利后才允许 SafeReturn；普通胜利 → 回图继续。
- 禁止改什么：`FinalizeReturn` 内部冻结顺序与 Save 时机；Bridge 守卫；Boss/Retreat/Defeat 的 mode 语义映射；M1 防重入 guard。

**C. `Scripts/UI/MapUI.cs`**
- 当前职责：Legacy 4×3 节点展示与点击回调。
- 为什么修改：可用/不可用、点击后推进的判定真相要从 Progression 派生（Legacy flags 不再当规则）；但展示可以继续沿用现有格子。
- 修改内容：展示刷新读取 Progression 派生的可用节点镜像；**点击只上报 NodeId / click intent 给 ExpeditionUI，不直接调用 `TryEnterNode`、不修改 Progression State**。合法性与 affordability validation 由 `ExpeditionUI → AdventureManager → Resolver` 执行；`MapNode.Reachable` 仅为显示镜像。保留 rows/columns 等布局字段与既有产品入口。
- 禁止改什么：Prefab/Scene 绑定字段名；rows/columns 视觉布局；既有 RefreshDisplayOnly 等外部调用签名（回归兼容）。

### 11.2 New files

**A. 纯逻辑 Runtime（新 asmdef，`autoReferenced=true`，仅引用 UnityEngine）**

```
Scripts/Adventure/Progression/Runtime/
  ExpeditionProgression.Runtime.asmdef
  ExpeditionNodeKind.cs
  ExpeditionNodeData.cs
  ExpeditionGraph.cs
  ExpeditionProgressionState.cs
  ExpeditionSupplyState.cs
  ExpeditionProgressionResult.cs
  ExpeditionProgressionResolver.cs
```
Authority ownership：Node 定义（只读数据）、Progression State（会话真相）、Supply State（会话消费侧）、Resolver（唯一规则入口）。不引用 Assembly-CSharp / Village.Runtime / BattleStage1.Runtime / RunState / ExpeditionRecord。

**B. Assembly-CSharp 适配/收敛层**

```
Scripts/Adventure/Progression/LegacyMapGraphAdapter.cs
```
职责：把 `MapGenerator` 输出（Compatibility Graph 源）映射为 `ExpeditionGraph`（NodeKind / Connections / `NodeElapsedDays` 测试配置 / 显式 `SuccessfulRouteTerminal`），一次性导入；未来正式 Node 生成替换时仅替换本 adapter。
Authority ownership：只做定义导入与类型映射；不产生运行规则、不持久化。

**C. 测试**

```
Tests/EditMode/ExpeditionProgression/ExpeditionProgression.Tests.asmdef（引用 ExpeditionProgression.Runtime）
  ExpeditionProgressionInitTests.cs
  ExpeditionNodeEnterCompleteTests.cs
  ExpeditionElapsedDaysExactlyOnceTests.cs
  ExpeditionSupplyConsumptionTests.cs
  ExpeditionBranchReturnIndependenceTests.cs
Tests/PlayMode/ExpeditionProgression/ExpeditionProgression.PlayModeTests.asmdef（引用 ExpeditionProgression.Runtime / Village.Runtime / BattleStage1.Runtime；Assembly-CSharp 经反射，同既有 ExpeditionV4F.PlayModeTests 模式）
  ExpeditionProgressionProductFlowPlayTests.cs
```
测试细目见 §13。

### 11.3 Explicitly untouched（高风险清单）

- `Scripts/Battle/Stage1/RunState.cs` 与 BattleStage1.Runtime（Run 连续态；不新增任何 Node/elapsed/supply 字段）；
- `Scripts/Village/Runtime/ExpeditionRecord.cs` 与 V4F 会话状态机（只读复用）；
- `Scripts/Village/VillageRuntimeBridge.cs`（V4F transaction / RunReturnCatchUp / 守卫不修改；仅复用既有 provider seam；如实现确需暴露 getter 时走增量小改并回归 V4F 15 项 + PlayMode 3/3）；
- `Scripts/Village/Runtime/VillageTime.cs` / VillageState / Assignment / Project / Social / World / SaveData（不动）；
- `Scripts/Adventure/MapGenerator.cs` / `MapNode.cs` / `ExpeditionSystem.cs` / `RunConfig.cs` / `PlayerRunRewardSink.cs` / `BattleSettlement.cs`（Compatibility / 复用；WP-1 不修改功能）；
- 所有 SaveSystem / SaveData / SaveVersion / PlayerCollection / PersonIdentity / Growth / Profession / Battle Stage1 runtime 文件；
- GameSpec / GameDesign / Docs 状态管理员文档 / Scene / Prefab / ScriptableObject；
- Product Home World（WP-2）相关任何文件。

## 12. Stage 拆分（每 Stage 独立 Gate，禁止一次全做）

### Stage 1 — Pure Progression Domain（只加新文件，不动产品代码）
- 交付：最小 `ExpeditionProgression.Runtime` 纯逻辑类；Resolver 的 enter / outcome resolution / branch / elapsed / supply / exactly-once；NodeData 直接持有 integer `NodeElapsedDays`（Test 值）；自建小型测试 Graph。**不创建无当前消费者的 ElapsedPolicy / Strategy 抽象。**
- Gate 1：新增 EditMode 全绿；零产品文件改动；编译干净；Legacy flags 未参与。
- 关键断言：Enter≠Resolve/Complete；Victory/Defeat outcome 都只 Resolve 一次；Defeat 也消耗已发生的 elapsed/supply；0 successor ≠ successful route completion；0/1/N 后继；可预知成本不足在内容启动前被拒；无双扣。

### Stage 2 — AdventureManager / UI Integration
- 交付：`LegacyMapGraphAdapter`；`AdventureManager.StartProgression`；`ExpeditionUI` 出征登记绑定 provider + 点击/回图走 Resolver；`MapUI` 展示镜像。
- Gate 2：真实产品入口 Start Expedition → 初始化 Progression → MapUI click intent → ExpeditionUI/AdventureManager validation → 首个 Node（Battle）→ 胜利 Resolve+Complete → 回图 → 下一 Node；Branch 0/1/N 产品可走；Retreat 按钮仍直接 Return；既有 M1/M5/M6/Battle 相关 EditMode 与 PlayMode 回归不回归（含 V4F 3/3）。
- 关键断言：Node completion 才累计 elapsed；Map 点击非法后继被拒；Legacy flags 不再决定推进。

### Stage 3 — Supply + Return Contract
- 交付：Supply 初值注入 seam（测试 seed）/ WP-3 预留读取入口；complete 原子扣减；insufficient 结果接线（UI 提示只读）；elapsed provider 真实返回 Progression elapsed；产品 Return 路径断言 `DaysSimulated == EffectiveElapsedDays`。
- Gate 3：Supply 专项 EditMode；产品 Defeat/SafeReturn/Retreat 三路径 elapsed>0 场景下 `ExpeditionRecord.EffectiveElapsedDays == Progression.ExpeditionElapsedDays`；**Defeat 的最后一场已发生 Battle 必须计入 elapsed/supply**；Catch-up 期间 Occupancy 保持；唯一 Final Save；重复 Return no-op；V4F 15 项 + 3/3 全回归。
- 关键断言：无普通 Node 双扣；Return 不提前清空 carry；无第二次 Save。

### Stage 4 — Product Flow Proof + Regression
- 交付：最小真实流程（Battle/Situation → complete node → next/branch/return）产品验证剧本；SafeReturn / Retreat / Defeat 全路径回归；性能/稳定性（无死锁、无重复结算、无新增 Console Error）。
- Gate 4：产品试玩剧本（GUI + Unity MCP）通过；既有 V4F 3/3 与全量 EditMode 回归通过；Cross-check §14 全绿。
- 说明：实际 Stage 细节允许在保持本 FDS 边界与 Gate 的前提下按真实代码微调，但每个 Stage 必须有独立可验证 Gate。

## 13. Test / Validation Plan

### 13.1 新增 EditMode / Pure Logic（至少覆盖 WP-1 规则）

| 测试文件（新增） | 必须覆盖 |
|---|---|
| `ExpeditionProgressionInitTests` | initialize state（phase / elapsed=0 / carry=seed / 根节点进入候选）；无 graph / 重复 initialize |
| `ExpeditionNodeEnterCompleteTests` | 合法进入 / 非法后继被拒；可预知成本不足在内容启动前被拒；Enter ≠ Resolve/Complete；Victory Resolve 后 Completed；Defeat Resolve 后 Resolved-not-Completed；重复 Resolve no-op |
| `ExpeditionElapsedDaysExactlyOnceTests` | NodeElapsedDays=0/1/N；Victory 与 Defeat 的最终 outcome 均 exactly-once 累加已发生 elapsed；重复 Resolve 不重复累计；Node count ≠ elapsed（用 0 天节点验证） |
| `ExpeditionSupplyConsumptionTests` | Base = PartySize×NodeElapsedDays×Rate；Explicit Extra Supply Cost（仅特殊 Node）；**无普通 Node 双扣**；普通已知成本不足 → Enter/内容启动前原子拒绝；Victory/Defeat outcome 均 exactly-once 扣除已发生成本；Return 不受阻塞且不清空 carry |
| `ExpeditionBranchReturnIndependenceTests` | branch 0/1/N（0=terminal **不自动胜利且不自动 RouteCompleted**；1 无选择；N 只能选合法后继）；仅显式 SuccessfulRouteTerminal 的成功 outcome 置 RouteCompleted；Return 独立于 branch；legacy flags 非真相 |

### 13.2 新增 Integration / PlayMode（产品路径）

`ExpeditionProgressionProductFlowPlayTests`（GUI / Unity MCP）至少覆盖：
- Start Expedition → Initialize Progression → 首个 Node（Battle）→ 胜利 → Complete Node exactly-once → 回图 → 下一 Node；
- Branch N>1 选择 / 1 后继自动继续 / 0 后继 terminal；
- Retreat（主动撤退）、SafeReturn（显式 SuccessfulRouteTerminal 成功）、Defeat（全灭且最后已发生 Battle 的 elapsed/supply 已结算）产品 Return 三路径；
- Occupancy 在 Catch-up 期间保持（既有 V4F 语义回归）；
- 既有 V4F 3/3（ExpeditionV4F.PlayModeTests）仍然 PASS；
- Final Save 仍 exactly-once；无新增产品流程 Error。

### 13.3 复用回归基线（既有测试，不新增同名重复）

- EditMode：当前全量基线 423/423（含 ExpeditionV4F 专项 15 项）——以真实 runner 结果文件为准，WP-1 每 Stage 全量复跑；
- PlayMode：ExpeditionV4F 3/3（v17 GUI+Unity MCP 通过记录）；M1 基线 31/31 仅作历史基线；
- 说明：新增测试明确列出为「新增」，其余为「复用既有 V4F / Battle / Village / Growth / Profession 回归」，不得伪造为新增覆盖。

## 14. Unity 验证策略

- Pure Logic / 编译可先非 GUI（新增 asmdef EditMode 可在 Editor batch 内执行）；但 **headless / -nographics 不是可信唯一通道**。
- 正式 PlayMode / 产品流程（含 §13.2 与 V4F 3/3 回归）**优先 Unity Editor + Unity MCP / GUI interactive**；PlayMode 执行前先确认 Editor / MCP 可用。
- 不得把已知停滞的 headless 通道作为唯一 Gate（历史根因：Agent 未使用已连接 Unity MCP）。
- 保留 timeout / anti-hang（沿用既有测试运行规范）。

## 15. 生成后只读 Cross-check（本 FDS 自检 12 项）

1. Current GameSpec vs 本 FDS 无冲突：Node/Return/Branch/Time/Supply/Authority 分层均取自 GameSpec Expedition §1–13（Pass）；
2. 未偷渡 WP-3 Food economy：§6.3 明令禁止，Supply 为会话抽象 + seam（Pass）；
3. Legacy Map flags 未重新成为 Authority：§9 每项标记 Do not use as new Authority（Pass）；
4. RunState 未变万能 State：§3.2 + §11.3 禁止增字段（Pass）；
5. 未引入 Run Save / Resume：§1.2 / §4.7（Pass）；
6. 未提升 SaveVersion：§4.7（Pass）；
7. 未改变 V4F Return frozen order：§10.2 原样（Pass）；
8. Y5 integer elapsed-day 契约完整：§5（Pass）；
9. 无 Base Supply double-charge：§6.3 + Stage 1/3 Gate（Pass）；
10. 所有文件路径来自真实仓库审查：§2 审计 + §11 白名单均真实存在（Pass）；
11. 每个 Stage 有独立验证 Gate：§12（Pass）；
12. 包含 Unity MCP / GUI PlayMode 验证策略：§14（Pass）。

## 15.1 制作人 Pre-development Review 修订（2026-09-08）

本节覆盖本 FDS 早期草案中对应冲突表述，开发时以本节及已同步正文为准：

1. **0 successor ≠ successful route completion**：0 successor 只代表 progression terminal；SafeReturn / RouteCompleted 只能由显式 `SuccessfulRouteTerminal` 的成功 outcome 产生。
2. **Supply affordability 前置**：普通、成本可预知 Node 必须在启动 Battle/Event 前校验 Supply；禁止“打完后才发现补给不足所以不算”。
3. **Defeat 也消耗已经发生的时间与补给**：Battle Defeat 是最终 outcome，必须 exactly-once 结算该 Node 的 `NodeElapsedDays` 与 Supply，再进入 Defeat Return；但不标记 Node Completed、不开放普通后继。
4. **不使用未定义 PartyContext**：Stage 1 初始化只传最小必要值 `partySize / initialCarryUnits / perPersonDailyConsumptionRate`。
5. **最小抽象优先**：不创建无当前消费者的 `ExpeditionElapsedPolicy`；`NodeElapsedDays` 直接由 NodeData 配置。Phase enum 可内嵌 State。
6. **MapUI 只负责 View/Input**：MapUI 只上报 `NodeId`；状态 mutation 必须走 `ExpeditionUI → AdventureManager → Resolver`。

---

## 16. Development Closure Gate（WP-1 开发完成后验收清单）

- [ ] Node-based Progression 成为真实 Runtime Authority（Progression State / Resolver）；
- [ ] Legacy map flags 不再控制新 progression；
- [ ] Enter ≠ Complete；
- [ ] Node completion exactly-once（elapsed / supply 同事务）；
- [ ] integer ExpeditionElapsedDays exactly-once；
- [ ] Base Supply 按 Party × elapsed days 扣除；
- [ ] 无普通 Node Supply 双扣；
- [ ] Branch 0/1/N 正确；
- [ ] Return 独立于 Branch；
- [ ] Run Save / Resume 未实现；
- [ ] SaveVersion 未提升；
- [ ] Return handoff 保持 V4F frozen order；
- [ ] Existing V4F Return regression PASS（EditMode 15 项 + PlayMode 3/3）；
- [ ] 无第二次 Final Save；
- [ ] 无新增产品流程 Error；
- [ ] 修改均在 §11 白名单；
- [ ] Docs / Tests / Runtime 一致。

## 17. 结束条件与本文件效力

- 本 FDS 生成即本轮结束；**不自动进入 WP-1 代码开发**。
- 代码开发只在制作人审核本 FDS 后开始；开发时本文件为修改白名单与 Stage/Gate 的唯一执行依据。
- 与既有规则一致：GameSpec 数值 / 测试值不得自动升级为正式设计；发现 Current Authority 与真实代码无法对齐或出现 Red 时停止并回报，不自行实现、不猜测处理。

---

> 文档结束。




