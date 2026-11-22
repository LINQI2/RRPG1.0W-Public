# 03 Battle

> 状态：GameDesign V0.2 的 Battle 设计意图。Current 可执行规则唯一入口为 [GameSpec/Battle/](../GameSpec/Battle/)。

## 3.1 Battle 在整体体验中的职责

Battle 是远征中最集中的风险与组合决策场所。它应让玩家感受到：

- 出发人物的长期差异确实改变本次可形成的组合；
- Run 中获得的卡牌让同一批人物出现新的打法；
- 人物 HP 与状态产生跨战压力，而不是每场战斗完全独立；
- 站位、邻接与范围提供轻量空间判断，但不把游戏转变为完整战棋；
- 胜负与消耗会被带回远征和村庄循环，形成可承担的后果。

## 3.2 Run 随机构筑的职责

Battle 使用共享牌库承载队伍组合。长期塑形决定人物能带来哪些 Starter 与可能进入奖励候选的角色牌；远征中的随机获得决定本次 Run 实际形成什么构筑。

因此：

- 长期成长负责扩大和塑造未来可能；
- Run 构筑负责制造临场适应与意外组合；
- Run 结束后实际 Build 默认重置；
- 当前一次远征的强组合不自动变成人物永久数值或永久完整牌组。

角色牌、通用牌、Starter、Unlocked Pool 与 Run Copies 的设计分工见 [05 Cards.md](05%20Cards.md)。

## 3.3 人物风险与辨识度

人物不是抽象战斗单位。角色牌读取具体人物属性与状态，人物退出本场战斗会改变牌库和队伍组合。Battle 风险应让玩家关心“是谁在承担”，同时避免把普通战斗直接变成永久死亡判定。

人物长期成长与 Battle 行为的职责边界见 [04 Person.md](04%20Person.md)。

## 3.4 规则边界

以下内容均不在 GameDesign 重复定义：

- 上阵人数、战前布阵与阵型锁定；
- 共享行动力、抽牌数与牌库循环；
- 角色 HP、0HP、退出牌库与胜负条件；
- 距离、邻接、目标、Intent、状态和结算顺序；
- Scene、UI、Slot 与 Binding 行为。

这些规则必须从 GameSpec/Battle 读取。GameDesign 只说明它们服务的体验目标，不得根据旧章节或旧实现补齐规则。

## 3.5 Legacy 追溯

旧固定 Slot 容器及配套空间行为、角色卡上阵凭证、个人行动力 + TP、Death's Door、普通战斗永久死亡与旧英雄例外均已被 Battle V0.1 替代。Current 使用敌我各自独立的 3×3 逻辑 Board：两边共享同一种空间语言，但 Occupancy 与 Adjacency 各自独立；具体 Range、占位、失能与目标规则读取 Battle GameSpec R5–R6，同为 3×3 不表示旧单 Grid 规则整体恢复。历史规则仍可从旧 GameSpec、Git 历史与归档资料追溯，但不再保留为本文件的 Current 章节。

## 3.6 Enemy 设计原则与候选边界

**Current Design Principle：**Enemy 应为人物、卡牌和空间构筑制造新的决策。设计敌人时优先判断它让玩家产生什么新的决策，不能只把敌人作为血量或数值检查。

具体 Intent、空间、目标和胜负规则仍由 [Battle GameSpec](../GameSpec/Battle/Battle-GameSpec-V0.1.md)负责。本原则不冻结具体敌人、数值、Boss 内容或 AI 架构。

以下均为 **Deferred / Not Frozen**，不是当前开发任务：

- 普通敌人分类、精英敌人；
- Boss 阶段与机制；
- 敌方是否使用牌、是否拥有行动力；
- 特殊的不完全可预测机制；
- 敌人组合逻辑；
- 难度增长方式与属性曲线。

## 3.7 队伍选择便利功能

上次队伍快捷选择、推荐阵容仅为 **Deferred UX Candidate**，不代表固定小队，也不是当前开发任务。

## 3.8 Status 的设计原则与跨系统边界

Status 用于表达持续发生的变化，帮助玩家理解后果并作出应对，不是数值 Buff / Debuff 清单。一个状态应说明其 **source、target、duration 与 ending**；当玩家能据此决策时，应在发生前或发生时得到足够反馈。同一 Gameplay 概念不得因 UI 名称不同而产生第二套规则，视觉位置也不决定 Gameplay relationship。

Battle 状态的具体层数、触发、结算与时序由 [Battle GameSpec](../GameSpec/Battle/Battle-GameSpec-V0.1.md) 决定；跨战或 Return 后是否保留，由 Battle、Expedition、Person 等相应 Authority 确认。Personality、Relationship、Experience 不因叙事关联自动成为 Buff / Debuff。这里不恢复旧倒地、Death's Door 或历史接口。
