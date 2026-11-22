# Expedition-Progression-GameSpec-V0.1

> 版本：V0.1
> 日期：2026-09-07
> 状态：Current / FP-V0.1 / Expedition Progression Foundation
> 职责：WP-1｜Expedition Progression 的正式执行 Authority 与验收边界；定义 Node-based Progression、Node、Branch、Return / Retreat、Time、Expedition Supply、Authority 分层与 Do Not Break 边界。
> 设计意图：见 [GameDesign/06 Expedition.md](../../GameDesign/06%20Expedition.md)；本文件只负责“开发与验收时必须知道什么”。
> Pre-8/30 旧地图规格已标记 **LEGACY / SUPERSEDED FOR FP-V0.1 EXPEDITION PROGRESSION**，仅作历史 / Compatibility 参考；需要原文时查 Git History。
> 实现状态：WP-1 已完成 Stage 1–3、Final Completion Audit，当前为 **Development Closed**；本规则文件不维护实时开发阶段，后续统一核对 `Docs/项目状态快照.md` 与 `Docs/开发验收交接.md`。
> 2026-09-18 Journey Presentation Sync：新增 §17 Journey Presentation Contract（玩家层表现契约）。Progression 规则结构、Node / Branch / Return / Time / Supply 边界不变。
> 2026-09-19 Initial Expedition Party Capacity Sync：新增 §18 Party Capacity Contract（初始出征准备阶段的 Character 上限、Hero 是否计入该上限，以及与单场 Battle Active Roster 的分离）。Progression / Time / Supply / Return / Journey Presentation 章节语义不变。

---

# 0. 系统定位

## 0.1 负责

- Expedition 的路线 / Node 推进；
- Current Node；
- 合法后继；
- Node Enter / Resolve；
- Branch；
- elapsed time；
- Expedition Supply；
- Return 请求与 V4F Return 边界。

## 0.2 不负责

- Battle 内部规则；
- Run Deck / HP / Insight；
- Village 资源经济；
- Food 正式来源；
- Product Home；
- Run Save / Resume；
- Node 正式内容生态。

---

# 1. Authority 分层

> 本规格明确区分 **AdventureManager、RunState、Expedition Progression State、ExpeditionRecord** 四类职责，不得重新形成万能 State。具体类名 / 接口名不冻结，由 WP-1 Final Development Spec 决定；本文件中的名称是职责角色名。

## 1.1 AdventureManager = Expedition Orchestrator

- 负责协调 Expedition Prep、RunState、Expedition Progression、Battle Encounter 与 Return。
- 沿用现有顶层 Adventure / Expedition 流程宿主作为单一职责入口，**不得新建第二套顶层 Adventure / Expedition Manager 作为平行 Authority**。
- 现有 Legacy Adventure Manager（旧 Map Runtime 宿主）在本 WP 内收敛为上述 Orchestrator 职责边界；最终类名不冻结。

## 1.2 RunState

继续负责：

- Person CurrentHP；
- Run Deck；
- Insight；
- Pending Rewards；
- 既有 Run 构筑 / Battle 连续状态。

不得把 CurrentNode、Branch、elapsed time、Supply、Return transaction 全部塞入 RunState。

## 1.3 Expedition Progression State

独立 Runtime State，负责：

- Current Node；
- 已到达 / 已完成节点的路线推进状态；
- Node resolution state；
- Branch state；
- elapsed time；
- Expedition Supply；
- Expedition progression phase。

FP-V0.1：

> Expedition Progression State 只存在当前远征会话，**不持久化**（见 §12）。

具体类名不冻结。

## 1.4 ExpeditionRecord

继续仅负责 V4F Return：

- Return phase；
- Catch-up；
- DaysSimulated；
- Completed；
- exactly-once Return transaction。

不得扩张为整趟 Expedition Progression State。

---

# 2. Node Definition

Node Definition 至少可表达：

- NodeId；
- Content Type / Type；
- Connections；
- Presentation metadata（如需要）。

Content Type 是**泛化内容承载接口**，不是固定节点生态清单：

- Shop / Recruit / Rest 若未来存在，不要求成为固定 Node Type，可作为具体 Node Content / Situation 中提供的服务、机会或内容；
- 本规格不要求 Current 存在固定 Shop / Recruit / Rest 节点类别；内部是否使用 enum / type 属实现选择，不是产品要求；
- 正式 Node Content Pool、Event、Shop、Recruit、Rest、Boss 路线与生成概率仍 Not Frozen。

边界：

- Node Definition 不应继续作为 Runtime progression state 的唯一存储位置；
- 旧字段 `Visited` / `Reachable` / `Selected` 不再作为新的正式 Authority；
- Compatibility 期间可保留旧字段，但新逻辑真相源应位于 Progression State / Resolver。

---

# 3. Node 生命周期

业务语义至少区分：

```text
Not Entered
→ Entered / Resolving
→ Resolved
```

- 不要求实现为指定 enum；
- 必须保证：“已经到达节点” ≠ “节点内容已经完成”。

---

# 4. Progression Resolver

必须存在唯一规则入口负责：

- GetAvailableNextNodes；
- CanEnter；
- Enter；
- Resolve；
- Branch；
- Time cost；
- Supply cost；
- Current progression state 更新。

- 具体 API 名不冻结；
- 禁止 UI / Map View 直接修改 CurrentNode 作为正式规则执行方式。

---

# 5. Branch

- 0 后继：无普通路径后继，不自动解释为胜利；
- 1 后继：不要求 Branch Choice；
- N > 1：进入 Branch Choice，只能选择合法后继。

---

# 6. Invalid Entry

必须拒绝：

> 当前 Node 非法后继 / 不可达 Node 的直接进入。

---

# 7. Return / Retreat

Return / Retreat 属于 Expedition 层独立操作，不得表示为普通 Node Connection。

Return 请求最终接入既有 V4F：

```text
Settlement
→ Keep Occupancy
→ Catch-up
→ Clear
→ Final Save
→ Town
```

本 WP 不修改该顺序。

---

# 8. Time

- Expedition Progression 维护 accumulated elapsed time；
- Node 推进可以产生 Time Cost；
- 不允许：每 Node 直接 Tick VillageTime；
- Node DurationMinutes以整数累计；Return推进到DepartureInstant + 累计DurationMinutes，经过同一VillageTime分钟边界，保留实际到达时分；
- Supply整次累计ceil、节点扣新增差额；具体内容/分钟值及PartySize计数仍Not Frozen（见§19与Resource Coupling §16）。

---

# 9. Expedition Supply

> 2026-09-07 WP-3 cross-sync：Supply 语义与 Village Resource Coupling 对齐（见 `GameSpec/Village/Village-Resource-Coupling-GameSpec-V0.1.md` §16）。Expedition 标准 Carry / Supply 映射资源 = Village Food；Carry 是真实资源转移，不复制资源。

Progression Runtime 必须支持抽象 Supply：

- Initial Supply（出发时由 Village 真实转入的 Expedition Carry）；
- Current Supply；
- Insufficient Supply；
- Supply deduction exactly-once；
- Base Supply Consumption（由 Party Size × Expedition Elapsed Time × Per-Person Consumption Rate 推导）；
- Explicit Extra Supply Cost（仅特殊内容可提供）。

当前：

> **Base Expedition Supply Consumption = Party Size × Elapsed Time × Consumption Rate（具体数值 Not Frozen）。**

规则：

- Node / Progression 提供 elapsed-time cost；基础 Supply 消耗由 elapsed time 推导；
- **禁止普通 Node 因 Time Cost 已产生基础 Supply 消耗后，再次固定扣 Supply**；
- Explicit Extra Supply Cost 只用于特殊内容（绕路、丢失补给、特殊 Event、环境代价、额外安全消耗等）；
- Supply 不足时由 Resolver 拒绝普通推进（见 §11），本规格不冻结自动失败 / 自动返程 / 伤害 / 饥饿惩罚。

抽象 Supply 概念继续保留为 Expedition Progression 规则语义（设计意图见 `GameDesign/06 Expedition.md` §6.6）；Expedition 内不复制 Village Food，不建立第二套长期资源 Authority。

---

# 10. Exactly-once

必须保证同一 Node 的 UI 重开、Battle 场景返回、重复查询、重复 Resolve 调用不会造成 Time Cost 或 Supply Cost 重复结算。

---

# 11. Insufficient Supply

Supply 不足时：

> Resolver 拒绝普通推进。

FP-V0.1 不在本规格冻结自动失败、自动返程、伤害、饥饿或其它惩罚；只返回明确不可推进结果。

---

# 12. Persistence：FP-V0.1 不支持 Expedition Run Save / Resume

FP-V0.1：

> **不支持 Expedition Run Save / Resume。**

因此：

- Progression State 不进入 SaveData；
- 不提升 SaveVersion；
- 不做 CurrentNode 持久化；
- 不做 Branch history 持久化；
- 不做 Supply mid-run 持久化。

Return 后长期状态继续走现有 Save 链（含 V4F Final Save），本 WP 不改变。

---

# 13. Legacy Compatibility

- 允许 `MapGenerator.Generate()` 暂时继续生成 Graph（Compatibility / Graph 来源候选）；
- 允许旧 PosX / PosY、Connections、Map UI 在兼容阶段继续使用；
- 但以下不得继续作为新 Progression Authority：
  - `MapGenerator.CurrentNode`
  - `MapGenerator.IsAtBoss`
  - `MapGenerator.SelectNode`
  - `MapNode.Reachable`
  - `MapNode.Visited`
  - `MapNode.Selected`
- 可以暂时同步 legacy view state，但新 Progression State 是唯一真相源。

---

# 14. Do Not Break

- PersonId Authority；
- Card → Person explicit binding；
- RunState 现有职责；
- Battle；
- Battle Settlement；
- Growth；
- Pending Reward；
- V4F Return；
- SaveVersion；
- SaveData；
- VillageRuntimeBridge Authority。

---

# 15. Explicitly Deferred

本 GameSpec 不定义：

- Food；
- Ingredients；
- Kitchen；
- Village resource economy；
- Product Home；
- World movement；
- Tutorial；
- Narrative；
- 正式 Shop；
- 正式 Recruit；
- 正式 Rest；
- 正式 Event pool；
- Boss 路线；
- 正式节点生成概率；
- 正式地图视觉；
- Run Save / Resume。

其中 Shop / Recruit / Rest 若未来存在，不要求成为固定 Node Type，可作为具体 Node Content / Situation 中的服务或机会；Node-based Progression 不要求恢复固定 Shop / Recruit / Rest / Boss 节点生态。本节只澄清承载形态，不新增正式 Shop / Recruit / Event 规则。

---

# 16. 未冻结与后续

- 具体类名 / 接口 / 数据结构、正式 Node 内容池、分叉数量与生成算法、正式Node时长与日内Calibration、Expedition Supply 正式内容与数值均未冻结；
- Node 正式内容形态（是否出现交易、招募机会或 Event，正式 Content Pool 与生成概率）属于未来 Node Content 设计；本规格只冻结泛化承载接口；
- 测试内容 / 测试值不得自动升级为正式设计；
- 本规格继续作为 WP-1 规则 Authority；开发完成状态与验收事实统一核对 `Docs/项目状态快照.md`、`Docs/开发验收交接.md` 及 WP-1 Final Development Spec，本文件不重复维护易过期的实时阶段状态。

---

# 17. Journey Presentation Contract（2026-09-18 Current Sync）

## 17.1 Authority 边界

Journey Presentation 负责把 Current Progression 翻译为玩家可见的旅行、Encounter 与 Branch 表现。

```text
Expedition Progression State / Resolver（规则 Authority）
                    ↓ 只读状态 + 既有意图入口
Journey Presentation Coordinator（表现协调）
                    ↓
Journey Visual Hosts / HUD / Overlays（表现）
```

禁止 Journey Presentation：

- 决定合法下一 Node；
- 直接修改 CurrentNode / Branch / Supply / elapsed days；
- Tick VillageTime；
- 建立第二套 Party、Progression 或 Return Authority；
- 将 Traveling / Paused / Speed-up 写入 `ExpeditionProgressionPhase`；
- 以动画时间推导游戏时间或 Supply；
- 由上一轮 Presentation callback 连续消费多个 Progression Transition。

允许维护 Presentation-only 状态，例如 Traveling、PresentationPaused、AwaitingBranch、EncounterVisible；这些状态不得保存，也不得成为 Gameplay Authority。

## 17.2 0 / 1 / N 后继表现

- `N = 0`：显示 Current 无普通后继状态与 Return / Retreat；不得自动解释为胜利。
- `N = 1`：默认播放一段 Journey；表现完成后调用现有唯一 Node 进入产品入口。
- `N > 1`：默认播放至 Branch；到达后暂停表现并显示合法 Branch Choice；玩家选择后调用现有唯一 Branch / Node 进入入口。

自动完成与玩家跳过必须汇合到同一 Presentation completion 入口，并保证一次旅行最多触发一次 Node / Branch 表面转换。

## 17.3 Presentation Cycle 与 Progression Transition

定义：

> **一个 Presentation Cycle = 从一次 Current Progression State 读取开始，到本轮表现完成 / 中断 / 失效为止的单次表现调度周期。**

规则：

- 一个 Presentation Cycle 最多消费一个 Progression Transition；
- Travel 自动完成、Continue Skip、Branch Choice、Encounter 返回均不得在旧 Cycle 的 callback 调用链中继续消费第二个后继 Transition；
- 当前 Cycle 消费一个 Transition 后必须结束；
- 若更新后的 Progression State 仍可继续推进，Journey 必须重新读取 Current Progression State，并建立新的 Presentation Cycle；
- Return、Hide、Rebind、Battle、View Dispose 或新的 Cycle 建立，必须使旧 Cycle / token 失效；
- 连续 Placeholder / Compatibility Encounter 或连续 N=1 Node 不能因为同步 resolve 而在同一旧 callback 中被连续吞掉；
- 本节冻结行为契约，不冻结实现机制。下一帧调度、队列、generation token、cancellation、deferred dispatch 等属于 Development Spec / T0 后实现判断。

## 17.4 Continue / Pause / Speed-up

- `继续前进`保留；旅行播放期间等价于 Skip Current Travel Presentation；
- `N = 1`时跳过后进入同一个下一内容；
- `N > 1`时跳过后只显示 Branch Choice，不能自动选择；
- Pause 只暂停动画 / 背景 / 人物移动；
- Speed-up 如果实现，只改变表现播放速度；WP-E V0.1 不要求正式倍率系统；
- 以上操作不得改变规则结算次数、顺序和数值。

## 17.5 Encounter 与 Return

- Encounter 开始时 Journey Presentation 隐藏或冻结；
- Encounter 结果仍经 Current `TryResolveCurrentNode` 结算；
- 普通完成后返回 Journey，结束旧 Cycle，并从更新后的 Current Progression State 开始新一轮调度；
- Return / Retreat 在 Journey 表面继续可用，并只调用 Current Return 产品入口；
- 进入 Return 后必须取消未完成 Presentation callback，避免动画结束再次推进；
- V4F Return 顺序、exactly-once、Save 与恢复边界零修改。

## 17.6 时间语义

- 现实中的动画秒数不是 Expedition elapsed days；
- 行走、暂停、加速、跳过期间不产生新的规则事实；
- elapsed days 与基础 Supply 消耗仍只在 Current Resolver 的既有 Node resolution 路径结算；
- 同一 Node 不得因自动播放、Skip、UI 重开或 Encounter 返回重复扣除。

## 17.7 Journey Party 与人物视觉解析

- Journey Party 成员来源 = 当前 `AdventureManager` 的准确远征人物集合；身份键必须是 `PersonId`；
- 不建立第二份成员真相、不复制长期 Person 实例、不以卡名 / DisplayName / CharacterId / HeroId 猜身份；
- Visual Resolution 必须先由 `PersonId`解析到准确 Character / Hero / Person，再读取该具体人物已有的定义 / 模板 / Presentation 数据获得视觉；
- 给定准确 Person 后，可读取该人物的 `PersonDefinitionId` / `CharacterId` / `HeroId`作为外观配置候选键；查询外观不改变身份 Authority；
- Hero 可共用同一种 Visual Resolution Contract，但继续保留独立产品身份；
- Journey 使用独立 View Prefab；不得直接复用 Prep Card 或 Battle `UnitCard.prefab`；
- 素材缺失时使用 default player placeholder 并给出可诊断 warning；
- Current 规则不要求存在 `PersonVisualCatalog` / `PersonVisualProfile` / Sprite 字段；具体数据结构由 WP-E T0 基于现有仓库与未来替换成本决定；
- 不得为了本 WP 把临时视觉映射升级成长期美术生产 Authority。

## 17.8 第一版兼容与长期边界

- WP-E V0.1 允许 Current 有限 Graph、LegacyMapGraphAdapter 与既有 0/1/N Resolver 继续作为后台规则来源；
- Legacy 4×3 Map UI 不作为 Current 玩家层默认表现；
- 长期无限远征是 Strong Direction，不在本节冻结 Encounter Pool、生成算法、Biome、Boss、终止条件或 Route Terminal 重构；
- WP-E 不删除 Graph，不修改生成算法，不修改 Node 正式内容生态。

## 17.9 明确未冻结

- 最终 Journey 构图、队伍间距、人物层级、环境滚动速度；
- 正式 HUD、正式 Encounter / Branch 视觉；
- Person Visual Catalog / Profile / Sprite 字段等长期视觉数据架构；
- 正式人物 Sprite、Animator、Spine、换装与美术生产管线；
- 正式 Speed-up 倍率；
- 正式环境、Biome 与 Encounter 内容；
- 无限远征生成与结束规则。

---

# 18. Party Capacity Contract（2026-09-19 Initial Expedition Party Capacity Sync）

> 本节只冻结**初始出征准备阶段的 Character 上限与 Hero 是否计入该上限**，以及与单场 Battle Active Roster 的分离关系；最低出征人数不由本节修改。
> 本节不修改其它章节语义：Node / Branch / Progression Resolver、Time（§8）、Expedition Supply（§9–§11）、Persistence（§12）、Return / Retreat（§7）与 Journey Presentation Contract（§17）全部保持不变。

## 18.1 初始出征准备阶段的 Character 上限（Current Rule）

Expedition 准备阶段的初始队伍容量：

```text
最多 6 Character
+
optional Hero
```

- 普通 Character 初始最多选择 **6 人**；
- Hero 是**独立可选**的远征成员：可以选择，也可以不选择；
- Hero **不占**这 6 人上限；
- 因此选择 6 Character + Hero 时，初始远征队伍最多可以达到 **7 名成员**。

冻结范围（本决定只冻结以下两件事）：

- 初始出征准备阶段的 **Character 上限 = 6**；
- **Hero 是否计入该上限**：不计入（Hero 为独立可选项）。

边界：

- 本节只描述**初始出征准备**的上限，不是整个 Run 或 Expedition 的永久人数上限（见 §18.3）；
- **最低出征人数不由 D-018 / 本节修改**：当前既有最低人数行为继续保持，除非制作人以后另行裁决；
- **不得从本节推出 `0 Character + Hero` 已合法**：本节只冻结 Character 上限与 Hero 是否计入该上限，不定义最低人数。

## 18.2 与单场 Battle 的分离（Battle Current Rule 不变）

```text
Expedition Party Size  ≠  单场 Battle Active Roster Size
```

- 单场 Battle 仍最多同时上阵 **6 个我方单位**（服从 Battle GameSpec R4.1，本规格不改写、不覆盖）；
- Hero 如果参加本场 Battle，占一个**正常 Battle 上阵位置**；
- 远征队伍中未参加当前 Battle 的成员**仍属于本次 Expedition**；
- 本场未上阵的远征成员**不进入本场 Active Roster**，其角色牌**也不进入本场共享牌库**；
- 本场未上阵成员不因此获得后勤职责、资源职责、额外收益或身份变化（沿用 `GameDesign/06 Expedition.md` §6.3 既有边界）；
- **不建立“战斗中途常规替补换人”规则**（与 Battle GameSpec R4.2 一致）。

## 18.3 明确未冻结（Not Frozen）

以下各项**继续 Not Frozen**，均**不**由本轮冻结，也不得从 §18.1 推导：

- 出征后通过 Recruit / Event / Situation 等方式**中途加入人物**时，Expedition Party 的总人数上限；
- 满员后的替换 / 拒绝 / 暂存或其它处理方式；
- 中途加入人物是否受另一套数量限制；
- 特殊 Encounter 是否覆盖通常上阵人数上限（保持既有 Deferred / Not Frozen）；
- Supply / Carry 的 **Party Size 计数定义**（Hero 是否计入）：既有规格（§9 与 `GameSpec/Village/Village-Resource-Coupling-GameSpec-V0.1.md` §16.3）与既有实现保持不变，本轮不定义、不改动。

禁止从“最多 6 Character + Hero”推导：整个 Run 永久最多 7 人；Expedition Party 的永久上限为 7；出征后中途加入人数已确定；`0 Character + Hero` 已合法；最低出征人数已被本决定修改。

## 18.4 实现备注（不是规则）

当前产品实现以“远征人物集合（Character + Hero）”计数计算 Carry / Supply 的 party size 与 Carry preflight。该计数语义属于 §18.3 的未冻结范围，未在本轮改动，也不因本节获得正式规则地位。

<a id="intraday-arrival"></a>
# 19. 日内时间与真实返程（Confirmed / Published Main / Runtime Verified）

本节收口 §8/9 的时间单位与补给取整；实施和验收见 [A Spec V0.2](../../Docs/DevelopmentSpecs/2026-10-05_RRPG_Intraday-Time-Expedition-Arrival_Development-Spec-V0.2.md)。A 已完成本地实现与真实产品验收，详见 [实施记录](../../Docs/DevelopmentSpecs/2026-10-05_RRPG_Intraday-AD_Execution-Record.md)；已于 2026-10-06 随 A～D 发布 main，发布点 `93a9e8cd6e0409ce335753221276bf88fe35f53f`。

- Progression 只累计已发生节点的整数分钟，不逐 Node Tick 世界。VillageTime 仍唯一 Clock；出发捕获 DepartureInstant，Return 推进至 DepartureInstant + 冻结累计时长；08:00 + 30h = 次日14:00。
- 整次基础补给为 ceil(累计 PartySize × DailyRate × DurationMinutes / 1440)，节点只扣基础需求新增差额及 Explicit Extra Cost。不可逐节点独立向上取整；具体数值和 Party Size 测试口径仍 Not Frozen。
- 已发生 Success / Defeat 均结算成本和时间；未开始的拒绝、重复 Resolve 零新增。旧 Event 输入保持天，在 Host checked ×1440。
- Return 保持 Settlement→Catch-up→Carry→Consequence→Clear Occupancy→FinalSave→Completed。Yield / Waiting 保留唯一原请求、原目标和完整游标，禁止保存与跨进程续跑。
- FinalSave 失败冻结世界和手动写操作，只重试保存，不重跑阶段或提前恢复 Town。阻塞 Pending 处理成功且关闭结果后只续原请求，不重抽或重播日期。
