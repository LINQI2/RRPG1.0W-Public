# Battle 规格入口

> 状态：**Current / Active**
> 切版日期：2026-08-29
> 适用版本：Battle V0.1 目标设计

## 阅读顺序与文件分类

Battle 相关任务必须先读取：

1. **Current 玩家规则**：`GameSpec/Battle/Battle-GameSpec-V0.1.md`，是玩家行为、核心规则与验收标准的 Authority。
2. **Scene/UI Binding**：`GameSpec/Battle/Battle-Scene-UI-Binding-V0.1.md`，只负责 Scene/UI 对象、绑定、可调项与协作边界，不得修改玩法。
3. **设计意图与实现核对**：[GameDesign/03 Battle](../../GameDesign/03%20Battle.md) 说明设计目的；当前 Architecture 与代码说明技术现状。Pre-8/30 旧规格仅通过 Git 历史按需追溯。

两份文件共同组成当前 Battle V0.1 规格。第一份决定玩法行为；第二份不得改变第一份的玩法规则。

用户随后明确采用并授权的设计修改须同步回 Current GameSpec，不以临时对话长期替代仓库 Authority。当前代码、Git History 中的旧规格和历史辅助资料不能自行覆盖 Current 玩家规则。

### 历史辅助资料迁出状态

Battle 目录长期只保留本 README、`Battle-GameSpec-V0.1.md` 与 `Battle-Scene-UI-Binding-V0.1.md` 三份文件，本 README 是唯一入口。18 份 `RRPG_Battle_*` 历史辅助资料已于 2026-09-11 移出活动仓库；迁出前内容归位基线为 `7b6abdbc1da8bf070af5910cbec246e1ae9dddb1`（`docs(battle): preserve auxiliary design content before migration`），通过 Git 历史或仓库外个人归档追溯，不再作为当前阅读入口。

已迁出资料不得因 V0.2/V0.3 编号、较新日期或历史“当前/确认”措辞覆盖 Current GameSpec。英雄是否进入普通 Profession 仍 Not Frozen；固定金币／经验等由 `Docs/问题清单.md` 问题50承载；重伤、Village 资源变化与更广泛 World Fact 仍为未来跨系统候选。现有设计与规则落点不因迁出而改变，见 `GameDesign/当前设计状态.md` 与相应 Current GameSpec。

## Current 与 Legacy 边界

Current 玩家规则读取 [Battle GameSpec](Battle-GameSpec-V0.1.md)，Scene/UI 对象与绑定读取 [Binding Spec](Battle-Scene-UI-Binding-V0.1.md)，设计意图读取 [GameDesign/03 Battle](../../GameDesign/03%20Battle.md)；技术现状再核对 [Current Architecture](../../Docs/Architecture/技术架构总览.md) 与代码。

Pre-8/30 Battle-related specs are historical / implementation migration evidence, not Current Authority. 需要复盘旧规则、旧接口或迁移起点时通过 Git History 查原文，不再在本 Current 入口逐文件维护 Legacy 导航。

仍可复用的工程概念包括逻辑位置与视觉坐标分离、配置与运行时分离、Effect / Trigger / Target / Status / Duration 等执行语言；是否复用具体旧接口以当前实现审计为准。它们不恢复旧共享 Grid、固定 Slot、个人 AP + TP、独立 Item Card、Death's Door、普通 Battle 永久死亡/伤残、满血入场或旧结算规则。Player / Enemy 各有独立 3×3 Board，实际空间与行为以 Battle GameSpec 为准。

**未冻结**：旧固定金币/经验是否与新卡牌奖励并存、具体 Enemy / Boss 内容、部分跨系统长期后果仍按 [当前设计状态](../../GameDesign/当前设计状态.md)与[问题清单](../../Docs/问题清单.md)读取，不从旧规格自动补全。

**WP-D 实施事实（2026-09-18）**：`WP-D Battle Visual Foundation V0.1` 把 Current Battle 白模升级为可替换视觉宿主原型，不改任何玩家规则。实施事实：UnitCard Prefab 增加 `Anchor_Visual/Body_Visual`（完整小人占位）、`Anchor_Shield`（独立 Shield 宿主，不再只拼进 HP 字符串）、`Anchor_Intent`（敌方 Intent 稳定宿主，运行时不再只依赖临时 `new Text`）、`Anchor_Status` / `Anchor_Effect`（可为空），矩形卡片底板弱化为透明功能容器；`BattleCard.prefab` 增加 `Image_Art` / `Text_Description` 视觉槽并绑定 `BattleCardView`；SampleScene 的 Battle Canvas 增加 `BattleBackground` 宿主、六个 Row Rect 增加 `RowVisual` 视觉子对象；新增唯一 `Formation Presentation` 入口（1 人居中 / 2 人聚拢 / 3 人展开，参数 Inspector 可调，`BattleUnit.Row/Column` 仍是 Position Authority）；WP-C 已证明 Dead 的场景对象（`BtnAttackOp` / `BtnSkillOp` / `BtnGuardOp` / `ItemCardRoot`）与其 BattleUI 序列化引用已物理清理，Current Card Cancel（`BtnCancelOp`）保留。占位素材仅位于 `Assets/Art/Prototype/Battle/`，不代表最终画风；Position Target UX 仍为 Future Design，未把内部 Column 做成正式可见 Slot。

**WP-C 实施事实（2026-09-18）**：§D 清单中属于正式产品路径的部分已由 `WP-C Battle Legacy Product-Path Cleanup` 移除——`TPSystem` / 独立道具抽牌堆与弃牌堆 / `Hand.ItemCards` / `BattleUI` 旧玩家操作菜单（Attack / Skill / Guard）与独立道具手牌（Item Hand）/ `ExpeditionSystem.SelectedItems` 与 `ConfirmExpedition` 的独立 item 输出 / `AdventureManager` 道具列表 / 旧玩家援护（Guard）写侧与读侧运行时字段 / 玩家个人 AP 的回合恢复与 HUD 显示均已退出。仍保留的 Legacy 兼容内容为：`ItemCardData` 内容资产与 `ItemCard.prefab`（供尚未清理的 Legacy UI 使用）、`PlayerCollection.OwnedItemCards` deprecated no-op 容器、以及场景中已不被产品代码使用的旧 UI 对象（运行时清除，物理清理另行任务）。Current 玩家路径只有共享牌库（角色牌 / 通用牌）+ 共享行动力 + `ApplyBattleDamage` + TurnStart 技能触发 + Enemy Intent。

## 今后内容写入规则

| 内容 | 修改位置 |
|---|---|
| Battle 体验目标、Enemy 与 Status 设计原则、未冻结方向 | [GameDesign/03 Battle](../../GameDesign/03%20Battle.md) |
| 人物与英雄长期设计 | [GameDesign/04 Person](../../GameDesign/04%20Person.md) |
| 卡牌构筑设计意图 | [GameDesign/05 Cards](../../GameDesign/05%20Cards.md) |
| Battle 玩家行为、规则边界和验收条件 | [Battle-GameSpec-V0.1.md](Battle-GameSpec-V0.1.md) |
| Scene、UI、Inspector 和对象绑定 | [Battle-Scene-UI-Binding-V0.1.md](Battle-Scene-UI-Binding-V0.1.md) |
| Person、Profession、Formation 和长期经验 | [Person Current GameSpec](../Person/Person-Profession-Growth-GameSpec-V0.1.md) |
| Expedition、Return 和 Supply | [Expedition Current GameSpec](../Expedition/Expedition-Progression-GameSpec-V0.1.md) |
| 实现状态与验收结果 | [项目状态快照](../../Docs/项目状态快照.md)和[开发验收交接](../../Docs/开发验收交接.md) |

- 优先修改已有 Authority，不再创建并列的 Battle Overview、Design Bible、AI Context、Documentation Map 或规则副本。
- 确需新增文件时，必须说明现有文件不能承载的原因、新文件唯一职责、Authority / Freeze 状态及入口。
- 新文件不能因版本号或日期较新获得更高 Authority；已迁出的18份辅助资料仅供历史追溯，不恢复为并列 Authority。

## 设计与实现状态

Current GameSpec 负责玩家规则，Binding 负责对象、场景、Inspector 与 UI 绑定；两者不维护实时实现进度。Battle 产品实现已完成并持续受回归保护，保留的 Legacy 兼容路径与未来未冻结内容须单独区分。

Stage 0–7 属于已经执行的历史开发／验收计划，不是下一步开发指令，也不要求重新从 Stage 0 开始。实时实现与测试状态统一查看[项目状态快照](../../Docs/项目状态快照.md)和[开发验收交接](../../Docs/开发验收交接.md)，必要时核对已提交代码；不得把历史阶段说明当作当前待办。
