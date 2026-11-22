# RRPG｜WP-C Battle Legacy Product-Path Cleanup｜Development Spec

> 日期：2026-09-18  
> 状态：**Development-time Binding / Ready for Repository Landing**  
> Work Package：Battle Legacy Product-Path Cleanup  
> 执行前置：WP-B Expedition Return Integrity 已 Technical Verified / Development Closed / Repository Closed  
> 执行基线：`5b8e37be6e6e361a059c6888dcbe0dd760963770`（`fix(exp): enforce expedition return transaction integrity`）  
> 建议落仓路径：`Docs/DevelopmentSpecs/2026-09-18_RRPG_WP-C_Battle-Legacy-Product-Path-Cleanup_Development-Spec.md`

---

# -1. Work Package 定位

本 WP 不是重新设计 Battle。

它只负责把 Current 产品已经明确替代的旧玩家操作链和旧 Item/TP 产品链，从**正式可达产品路径**中清除，并收敛 Current Runtime。

核心原则：

> **Remove Legacy Product Semantics, Preserve Current Technical Capabilities.**

必须区分：

## 需要退出的旧产品语义

- 独立 `Item Card`
- Item Deck / Item Hand
- Expedition Item Selection
- TP
- Player Personal AP
- Attack / Skill / Guard 旧玩家操作菜单
- `UsingItem` 旧目标流程
- Legacy Guard 玩家操作语义

## 仍必须保留的 Current 能力

- Shared Deck
- Shared AP
- Character Card
- Common Card
- Gate 0 Rule Pipeline
- `ApplyBattleDamage`
- Shield
- Status
- Current Skill/Trigger 底层能力中仍有真实消费者的部分
- Enemy Intent
- Enemy 当前行动次数/执行行为
- Current Card Cancel
- Battle Position / Dual Board
- Battle Result / Run Reward

本 WP 不允许因为“旧入口调用了某底层函数”，就把仍被 Current 使用的底层函数一并删除。

---

# 0. Current Authority

必须服从：

- `GameSpec/Battle/Battle-GameSpec-V0.1.md`
- `GameSpec/Battle/README.md`
- `GameDesign/06 卡牌系统.md`
- `GameSpec/README.md`
- `Docs/问题清单.md` 问题 53
- WP-A Dual-Board Spatial Authority 已关闭后的 Current Implementation
- WP-B Closure 后的最新 Return Implementation
- `.clinerules.md`
- `Docs/AI开发执行规则.md`
- `Docs/AI多Agent开发协作协议.md`

已冻结的产品事实：

1. Battle Card 顶层正式类别只有：
   - Character Card
   - Common Card
2. 不存在第三套正式 Item Card 产品系统。
3. 药剂、炸弹、工具等未来如以 Battle Card 存在，是 Common Card 内容表现。
4. 玩家行动由 Shared Deck + Shared AP 驱动。
5. Player Personal AP / TP / Attack-Skill-Guard 旧菜单不属于 Current Battle 核心。
6. Hero / Character 仍按 Current Battle Unit 规则参与战斗。
7. WP-A 双 Board / Range / Move / Swap / Adjacency 不得被本 WP 改写。

---

# 1. 本 WP 要关闭的 Legacy 垂直链

## L1｜Expedition Item Selection

旧链：

```text
PlayerCollection.OwnedItemCards
→ ExpeditionUI Item Tab
→ ExpeditionSystem.SelectedItems
→ ConfirmExpedition(out playerItems)
→ AdventureManager._playerItems
→ BattleUI.InitFromExpedition(items)
```

Current 目标：

> 正式 Expedition Prep 不再选择独立 Item Card，也不再向 Battle 传递独立 item list。

本 WP 只移除旧 Item 产品语义。

不得趁机重做：

- Person selection
- Hero selection
- Supply
- Starter Deck
- Common Card 来源

---

## L2｜Battle Item Hand / TP

旧链：

```text
CardSystem ItemCards
→ DrawPile / Hand / DiscardPile ItemCards
→ BattleUI itemCardRoot
→ RefreshItemCards
→ TPSystem
→ UsingItem
→ UseItem
→ CreateFakeSkill
→ SkillSystem
```

Current 目标：

> 正式 Battle Runtime 只剩 Shared Deck / Shared Hand / Shared AP 卡牌链。

不得保留一个：

> “不可交互但仍正式显示”的 Legacy Item Hand。

---

## L3｜旧 Player Action Menu

旧：

- Attack
- Skill
- Guard
- Cancel
- Player Personal AP

Current：

> Shared Hand → Card Target → Shared AP → Rule Resolution。

本 WP 必须移除：

- Attack 旧产品入口
-主动 Skill 旧产品入口
- Guard 旧产品入口
- `ActionMode` 中只服务上述 Legacy 的状态
- Player Personal AP 的产品展示与玩家操作依赖

但：

> Cancel 的 Current 卡牌取消职责必须迁移/保留。

不得因为删除 Legacy Cancel Menu 而失去：

- pending card cancel
- pending target cancel
- rule marker cleanup

---

# 2. 明确保留边界

## 2.1 Damage

`BattleSystem.ApplyBattleDamage` 是 Current Damage Authority。

必须保留。

删除的是：

> Legacy `PlayerAttack` 产品操作路径。

不是 Damage System。

---

## 2.2 SkillSystem

`SkillSystem` 不能整体删除。

执行 Agent 必须先做当前消费者图，保留：

- TurnStart / Current Trigger
- Status-related Current capability
- Gate 0 / Current Runtime 真实依赖
- Enemy / other Current consumers

删除的只能是：

> 旧 Player主动 Skill 操作链中已经没有 Current consumer 的部分。

若 `SkillSystem.CastSkill` 仍被 Current 非 Item 路径使用：

> 保留。

不以文件名判断 Legacy。

---

## 2.3 Status

Status 属 Current 可复用 Battle capability。

不得因为旧 Item 使用 Status，就删除 Status。

---

## 2.4 Enemy 行动预算

当前敌人仍真实使用 `CurrentActionPoints` 等内部预算实现。

本 WP **不重新设计 Enemy AP**。

目标仅是：

> Player Personal AP 不再作为玩家资源、操作门槛或 HUD 信息。

允许为了最小改动暂时保留 BattleUnit 的 AP 字段供 Enemy 内部执行使用。

不得：

- 改为 Enemy Shared AP；
- 改变每回合敌人执行次数；
- 改变 Enemy Intent 玩家可观察行为。

如果后续需要清理 Enemy AP：

> 独立立项。

---

## 2.5 Guard

旧 Player Guard 产品操作正式退役。

若当前只有 Enemy redirect 读侧、没有 Current writer：

> 本 WP 可以完整移除 Legacy Guard runtime 语义。

未来如果重新出现“替友军承伤”：

> 通过 Current Rule / Status / Operation 独立设计，不恢复旧 Guard Menu。

---

# 3. Save Compatibility

旧 Save 中可能存在：

`OwnedItemCardIds`

本 WP 不做 Save migration，不提升 SaveVersion。

默认策略：

> **兼容字段可以保留为 Deprecated / Ignored。**

允许：

- DTO 字段继续存在；
- Current Save 写空值；
- Load 对旧值安全忽略；
- 加注释说明 Legacy / no-op。

禁止为了“彻底清爽”强行：

- 改 SaveVersion；
- 删除导致旧档反序列化风险的字段；
- 建 migration framework。

如果事实证明删除字段对现有序列化完全安全：

> 仍不在本 WP 主动扩大范围，默认保留兼容壳。

---

# 4. Legacy Asset / SO 边界

本 WP 的目标是：

> **Current 产品路径与 Runtime 不再依赖旧 Item 系统。**

不要求为了零死文件立即物理删除所有历史：

- Item SO
- Item image asset
- Legacy historical docs

只有在：

- 无 Current 引用；
- 删除不会造成 Missing Script / Scene / Prefab / Save 风险；
- 属于本 WP 明确消费者树；

时才可删除 runtime 文件。

否则：

> 标 `Legacy / Unreferenced / Future Asset Cleanup`，不扩大任务。

---

# 5. Existing Host Map｜执行前必须重做

根据前一轮只读证据，至少核对：

| 职责 | 已知宿主 |
|---|---|
| Expedition Item selection | `ExpeditionUI` |
| Selected items | `ExpeditionSystem.SelectedItems` |
| Item carry-through | `AdventureManager._playerItems` |
| Battle item input | `BattleUI.InitFromExpedition` / `_playerItems` |
| Item hand UI | `BattleUIItems` / `BattleUIHandLayout.RefreshItemCards` |
| Item drag | `ItemCardDragHandler` |
| Item runtime | `ItemCardData` / CardSystem legacy item collections |
| TP | `TPSystem` / `BattleSystem` |
| Legacy action menu | `BattleUIActionMenu` + BattleUI handlers |
| Legacy player actions | `PlayerAttack` / active `CastSkill` path / `PerformGuard` |
| Player AP display | `BattleUI.RefreshFieldUI` 等 |
| Enemy AP | `ExecuteTurnPart2` / `ExecuteEnemyIntent` |
| Current damage | `ApplyBattleDamage` |
| Current cancel | V1 pending card / marker cleanup |
| Save compatibility | `SaveData.OwnedItemCardIds` / SaveSystem |
| Long-term item storage | `PlayerCollection.OwnedItemCards` |

执行 Agent 必须以 WP-B Closure 后本地代码重新核对。

本表不是允许机械删除的名单。

---

# 6. Technical Checkpoints

```text
WP-C-T1 Current Consumer Map
→ T2 Expedition Item Path Removal
→ T3 Battle Item/TP Removal
→ T4 Legacy Player Action Isolation/Removal
→ T5 Save/Data Compatibility Cleanup
→ T6 Scene/UI Binding Cleanup
→ T7 Regression + Product Integration
```

Green / Yellow 自主继续。

---

# 7. T1｜Current Consumer Map

修改前先证明每个候选符号属于：

- Current
- Legacy with Current technical dependency
- Legacy isolated
- Dead

重点：

- `ApplyBattleDamage`
- `SkillSystem`
- `StatusSystem`
- Enemy personal AP
- Cancel
- GuardedBy / GuardTarget
- ItemCardData
- `OwnedItemCards`
- Save item字段
- `DataManager` item loading
- Scene serialized item/action menu objects

不能仅凭：

> 类存在 / Scene序列化 / 测试引用

判 Current。

产品 runtime caller 才是关键证据。

---

# 8. T2｜Expedition Item Path Removal

目标：

正式 Prep 不再存在：

- Item Tab
- Item selection
- SelectedItems
- MaxItemCards
- AddItemCard / RemoveItemCard
- ConfirmExpedition 独立 item 输出
- `_playerItems`
- SyncPurchasedItem 等旧 item transport

必须保持：

- Character/Person selection 当前行为
- Hero selection
- Supply / Carry
- Progression
- Return
- WP-B 最新 Return Integrity

如果 WP-B 已改变 `ExpeditionUI` / `AdventureManager`：

> 必须以 WP-B Closure 版本为基线，不覆盖/回滚其事务修复。

---

# 9. T3｜Battle Item / TP Removal

目标：

删除正式运行链：

- itemCardRoot 的产品使用
- Item Hand refresh
- Item drag/use target flow
- `UsingItem`
- `UseItem`
- `TPSystem`
- Item-specific Draw/Hand/Discard runtime
- `CreateFakeSkill` 仅为 item 服务的桥

完成后：

> Battle Runtime 不再创建、显示、支付或结算独立 Item Card。

必须证明：

- Shared Hand 正常；
- Shared AP 正常；
- Character/Common Card 正常；
- Rule Program 正常；
- Skill / Status Current consumer 正常。

---

# 10. T4｜Legacy Player Action Removal

## Attack

移除：

- Legacy Attack button path
- `OnAttackOp`
- `ActionMode.Attacking`
- Legacy `PlayerAttack`

保留：

- Current Damage Authority
- Enemy damage
- Card damage

## Skill

移除：

- Legacy Skill button path
- `OnSkillOp`
- Player active skill action mode
- 仅服务旧菜单的 player AP gating

保留：

- Current SkillSystem capability
- TurnStart trigger
- Status / Current consumer

## Guard

移除：

- Legacy Guard button
- `OnGuardOp`
- `PerformGuard`
- Legacy Guard runtime fields/read redirect（若 consumer map 证明无 Current writer）

## Cancel

不得直接删除 Current cancel。

需要将：

> “Legacy ActionMenu Cancel”

与：

> “Current V1 Card/Target Cancel”

职责拆开。

最终 Current 产品必须仍能：

- 取消待出牌；
- 取消目标选择；
- 清理 Rule Cell Marker；
- 回到可继续选择的玩家回合。

---

# 11. Player Personal AP

完成后 Player 产品行为：

- 不读取 personal AP；
- 不支付 personal AP；
- 不显示 personal AP；
- 不通过 TurnSystem 给 Player 单位恢复/清零 personal AP 以驱动玩家行为。

Enemy 当前内部行为：

> 保持不变。

如果共用字段无法在不扩大 Scope 的情况下完全删除：

> 允许保留字段，但必须使其不再成为 Player 产品语义。

不得为了字段对称性修改 Enemy。

---

# 12. T5｜Save / Data Compatibility

## Save

- SaveVersion 保持不变。
- `OwnedItemCardIds` 默认保留兼容 no-op 字段。
- Current Save 不应再依赖 `OwnedItemCards` runtime container。
- Load 旧 item ids 不得导致异常。
- 不要求把旧 Item 数据转换成 Common Card。

禁止：

> 自动 migration：Item → Common Card。

因为 Common Card正式来源尚未冻结。

## Runtime Data

`PlayerCollection.OwnedItemCards` 若无 Current consumer：

> 可以移除 runtime依赖。

但若删除字段会引发大量非产品兼容风险：

> 可保留 deprecated no-op 容器，本 WP 的 PASS 标准是“Current产品不依赖”，不是“全仓零字符串”。

---

# 13. T6｜Scene / UI Binding

如需修改 Scene / Prefab：

> 必须通过 Unity MCP / Editor；不可直接编辑 YAML。

目标：

- 旧 Attack / Skill / Guard UI 不再进入产品；
- Item hand root 不再进入产品；
- Player AP legacy显示移除；
- TP显示移除；
- Current Shared Hand / Shared AP / End Turn / Start Battle 保持；
- Current Cancel 保持。

若可以只通过脚本安全解除旧绑定而无需序列化修改：

> 优先最小变更。

但不得留下会误导未来 Agent 的正式可见旧 UI。

---

# 14. T7｜Product Integration

必须走真实产品路径验证。

至少：

## Case A｜Expedition Prep

- 可正常选择当前合法人物/Hero；
- 不再存在独立 Item selection；
- Confirm Expedition 正常；
- Supply / Progression 不回归；
- WP-B Return 路径不回归。

## Case B｜Battle Player Turn

- Shared Hand；
- Shared AP；
- Card Target；
- Move/Swap；
- Damage；
- Shield；
- Intent；
- End Turn；

全部正常。

同时：

- 没有旧 Attack/Skill/Guard 玩家入口；
- 没有 TP；
- 没有 Item Hand；
- 没有 Player Personal AP HUD。

## Case C｜Enemy Turn

证明：

> 清 Player Personal AP / old menu 不改变 Enemy 当前执行行为和 Intent。

---

# 15. Tests

长期最小契约至少覆盖：

- `ExpeditionPrep_HasNoLegacyItemSelection`
- `BattleRuntime_HasNoIndependentItemHand`
- `BattlePlayerTurn_UsesSharedAPOnly`
- `LegacyAttackMenu_NotProductReachableOrRemoved`
- `LegacySkillMenu_NotProductReachableOrRemoved`
- `LegacyGuardMenu_NotProductReachableOrRemoved`
- `CurrentCancel_StillCancelsPendingCard`
- `ApplyBattleDamage_RemainsCurrentAuthority`
- `TurnStartSkillTrigger_StillWorks`
- `EnemyIntentExecution_Unchanged`
- `LegacyItemSaveData_LoadDoesNotBreak`
- `SharedDeckCharacterCommon_Regression`

优先复用现有测试。

Disposable Tests by Default。

---

# 16. 明确不做

本 WP 不处理：

- EventUI / ShopUI / RecruitUI 物理删除；
- Legacy Map cleanup；
- Common Card 正式内容；
- Common Card 来源；
- Item → Common 自动迁移；
- Hero新设计；
- Enemy AP重设计；
- Enemy Formation；
- Return Integrity（必须由WP-B先关闭）；
- Supply Preview P2；
- Save migration；
- SaveVersion；
- UI整体布局；
- 美术；
- Battle Product Acceptance。

---

# 17. Hard Stop

以下为 Red：

- 当前仓库不包含 WP-B Closure 基线 `5b8e37b`，或本地工作区不是基于该基线的 clean 状态；
- 需要回滚/覆盖 WP-B Return修改；
- 删除 Legacy Item 必须改变 SaveVersion；
- 必须决定 Common Card正式来源；
- 必须改变 Enemy行动次数；
- 必须重做 SkillSystem / StatusSystem；
- 必须创建新 Item/Common平行系统；
- 必须重做 BattleUI；
- Current Authority存在无法解释的冲突。

普通引用清理、类拆分、deprecated字段、测试适配：

> Green / Yellow，自主处理。

---

# 18. Final Closeout

完成后：

- Asset Refresh
- 编译
- Console
- 定向 EditMode / PlayMode
- 全量回归
- Product Integration
- Handoff
- 项目状态快照
- 项目日志
- 问题 53 生命周期
- Legacy状态同步
- Disposable Cleanup
- Final Diff / Workspace Review
- Closure Commit Staging Manifest

本 Spec 不授权 Commit / Push。

默认停在：

> **WP-C Battle Legacy Product-Path Cleanup Technical Verified / Ready for Commit / Push**

---

# 19. Technical PASS

只有全部成立：

- Expedition Current路径不再选择/传递独立 Item Card；
- Battle不再存在正式 Item Hand / TP / UsingItem；
- 正式 Battle Card 顶层行为只走 Character/Common + Shared Deck/AP；
- Player Attack/Skill/Guard旧菜单不再存在于产品路径；
- Player Personal AP不再是玩家资源或HUD信息；
- Current Cancel未丢失；
- Damage Authority未破坏；
- Skill/Status Current能力未破坏；
- Enemy Intent / 行动次数未改变；
- WP-A双Board无回归；
- WP-B Return Integrity无回归；
- Legacy Save item字段安全兼容；
- SaveVersion不变；
- 没有建立新平行 Item系统。

才可标记：

> **WP-C Technical Verified**
