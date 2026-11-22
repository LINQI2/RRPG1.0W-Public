# 04 Person

> 状态：GameDesign V0.2 当前人物长期设计。Battle 行为统一以 [GameSpec/Battle/](../GameSpec/Battle/) 为准；Person / Profession / Growth 可执行规则以 [GameSpec/Person/](../GameSpec/Person/) 为准。

## 4.1 具体人物的设计职责

角色是共同体中的具体人物，不是可以无差别替换的抽象单位。长期成长应让玩家逐渐认识并塑造“这个人能带来什么可能”，而不是只把等级转换为更高的 HP 与 Attack。

`Base Trunk × Person Life Architecture Concept Checkpoint V0.1` 当前冻结的架构事实是：人物的长期人生最终需要形成某种稳定角色身份，并影响 Battle Expression。`Profession` 是当前最强工作模型，其 Formation 基础规则已于 2026-09-06 归位为 Current Rule（见 [GameSpec/Person/](../GameSpec/Person/Person-Profession-Growth-GameSpec-V0.1.md) §12）；但 Profession 最终命名、Formation 数值尺度与阈值、正式职业内容仍未冻结。

角色内容的专题设计入口见 [Identity 角色内容设计专题](Identity/README.md)。这里的 `Identity` 只表示角色内容设计专题和一个尚未冻结的产品设计术语，不是对 Profession 的 Current Rule 改名；它不新增 Identity Runtime、Person Identity Authority、Person-specific battle Trait 类别或第二套 Profession / Formation 规则。专题中的五个角色方向、卡牌、Trait、Build Seed 与阶段安排均为 **Concept / Not Fully Frozen** 的设计候选；具体结构化候选只在 [Identity角色内容设计工作台.xlsx](Identity/Identity角色内容设计工作台.xlsx) 主编辑，角色 Markdown 不维护费用、触发、目标、效果、数值等第二套参数。“可进入 Prototype”仅表示下一设计验证 Gate，Prototype 开发或实现仍需独立授权。

人物差异主要由以下结构承载：

- 成长阶段与主动升级；
- Starter Slots；
- Unlocked Pool；
- 当前职业的 Profession Trait（战斗机制表达）；
- 当前 Starter 的熟练；
- 人生经历与有向关系；完整人格形成仍为后续范围。

### Life History 与 Autonomous Life（Current Architecture）

Person 是长期连续的人物主体，也是自主行为与人生历史的主体之一。

当前确认：

- `PersonId（人物唯一身份）` 继续作为所有长期 Person 关联的稳定身份入口；Card / Battle 临时身份不承载长期社会状态。
- Person 在非高优先级 Occupancy / Commitment 阻断时，可以在世界提供的 Activity 可能性中自主选择行为；Primary Assignment（主要长期职责）不等于全天占用。
- Person 必须允许存在稳定、可配置、可解释的行为差异；正式 Personality 维度、字段和数值仍未冻结。
- Person 与另一具体 Person 的 Relationship 允许方向不对称：`A → B != B → A`。
- 真实互动可通过 `Semantic Fact → Relationship Interpreter → Relationship State` 改变未来自主行为。
- Profession 继续承载职业身份与 Battle Expression，不等于人物的全部 Personality / Relationship / Life History。
- `PersonLifeHistory（人物人生史）` 继续承担 Person-owned 客观人生事实；Shared Relational Fact 不复制成多份 PersonLifeHistory 真相。
- 高层 History View 可以组合 PersonLifeHistory 与其它合法历史 Source；View 不等于新的 Source of Truth。

WP-AL-3 Decision 只读 Resolved Behavior Profile 与 Specific Preference；Behavior Channel 采用受治理定义，偏好独立稀疏语义键，不形成万能 Modifier Dictionary。PersonDefinitionId 只作静态画像 provenance，PersonId 仍是唯一人物身份。Decision 与 Behavior / Preference Formation 分离；Missing 使用 Neutral 且读取不创建记录。完整 Personality / Trait Runtime、形成与演化仍 Deferred。四人物 .8/.8、.5/.5、.8/.2 + leisure.drink +1、.2/.8 仅 Test / Calibration，不是正式角色平衡。规则 / 分级见 [WP-AL-3 Final](../Docs/DesignCheckpoints/2026-10-04_RRPG_WP-AL-3_Autonomous-Selection_Design-Checkpoint-V0.2-Final.md)与[当前设计状态](当前设计状态.md#wp-al-3-authority)，WP-AL-3已发布main，Current实现查[技术架构总览](../Docs/Architecture/技术架构总览.md#wp-al-3-current)，不由实现冻结具体数值。

WP-AL-4确认历史互惠：张三VisitSick王五 → Canonical Fact → 王五→张三正向关系 → Save / Load → 后续张三NeedsHelp时，王五Help张三的关系贡献、Desirability与Selection Weight提高；不要求必然选中Help。VisitSick / Help反馈方向为Fact.Target→Fact.Actor，不默认双向；Talk不自动增加长期关系。解释权归Social Domain，具体Affinity delta仍Calibration / Not Frozen；该反馈闭环已发布main，Current职责查[技术总览](../Docs/Architecture/技术架构总览.md#wp-al-4-current)。详见[Village规则](../GameSpec/Village/Village-GameSpec-V0.1.md#wp-al-4-feedback)与[WP-AL-4 Final](../Docs/DesignCheckpoints/2026-10-04_RRPG_WP-AL-4_Social-Feedback_Design-Checkpoint-V0.1-Final.md)。

未冻结：

- 正式 Personality Model；
- Relationship 正式维度、数值尺度与内容；
- Relationship Runtime 的具体类名 / Save DTO；
- Life Episode 正式名称、Pattern 聚合与 UI；
- 人物长期离开、死亡后的完整社会规则。

完整产品规格见 [Autonomous Life Vertical Slice Product Spec V0.2](../Docs/DesignCheckpoints/2026-10-04_RRPG_Autonomous-Life-Vertical-Slice_Product-Spec-V0.2.md)。本节不改变既有 Profession Formation GameSpec。

## 4.2 经验与主动升级

1. Run 内只累计 `Pending ProfessionExp`，不直接写长期经验。
2. Return 时统一提交已经产生的 ProfessionExp；Normal Return 包含主动撤退和有限路线完成后的返回，Expedition Failure 也完整保留已经产生的 ProfessionExp。
3. 经验达到升级条件时不自动升级，而是提示玩家可以主动升级。
4. 玩家每次确认提升一级；可以延后，也可以在经验允许时连续确认。
5. 等级表示成长阶段，不自动提高基础 HP、Attack 或 Range。
6. 每一级具体开放什么内容、经验曲线和等级上限均未冻结。

上述保留规则只覆盖 ProfessionExp；金币、材料及其他收益的保留或损失仍由对应规则负责，具体比例未冻结。完整经验规则见 [Person Current GameSpec](../GameSpec/Person/Person-Profession-Growth-GameSpec-V0.1.md) §10。

主动升级的意义是把成长变成可感知的长期决定入口，而不是在结算画面中自动完成数值膨胀。

## 4.3 Starter Slots

Starter 是具体人物每次 Run 的起始角色牌配置。

当前确认：

- 人物初始拥有 1 个 Starter Slot；
- 普通攻击是一张正常角色牌，初始可占用 Starter Slot，也允许在合法成长机会中被替换；
- 1 个 Slot 在每次 Run 开始时提供 1 张对应起始牌；
- 同一卡牌模板可以占用多个 Slot，从而提供多张起始副本；
- Starter 只能在长期成长机会中调整，不能在每次远征前无成本自由重构完整牌组。

Starter Slot 的最大数量、具体解锁等级和调整机会仍未冻结。

## 4.4 Unlocked Pool

Unlocked Pool 是某个具体人物未来可以出现的角色牌集合。

它同时服务于：

- 长期 Card Growth 的候选来源；
- Run 内角色牌奖励的候选来源；
- 村庄、事件或其他长期内容对人物可能性的扩展。

同一人物不应为不同来源维护互相割裂的平行解锁池。Unlocked Pool 扩展多少、如何分组、候选权重如何计算仍未冻结。

## 4.5 Trait（归位为 Profession Trait）

当前 Trait 归位为 **Profession Trait（职业特性）**：该 Person 当前职业身份的战斗机制表达。当前不存在正式的“独立 Person-specific battle Trait”类别。

当前规则（可执行规则见 [GameSpec/Person/](../GameSpec/Person/Person-Profession-Growth-GameSpec-V0.1.md)）：

- 当前 Trait 是该 Person 当前职业身份的专属被动；
- 职业变化 / 职业休眠时，对应 Trait 同步变化 / 休眠；
- Trait 不通过普通成长流程随意替换；
- Trait 不等同于可装备物、通用状态或一次 Run 获得的临时卡牌。

Trait 的具体效果、强化节点、数值与表现方式仍未冻结。

## 4.6 Starter 熟练

- 当前装入 Starter 的牌可以积累熟练。
- 牌被换出 Starter 后仍留在该人物的 Unlocked Pool。
- 换出后已经取得的高级熟练不永久保存。
- 熟练上限、成长速度和具体效果仍未冻结。

这条规则让玩家可以长期塑造常用起手能力，同时避免把每一次旧配置完整固化为永久 Build。

## 4.7 与 Battle 的边界

在 Battle 规则层，Hero 作为正常战斗单位参与上阵人数、位置、目标与战斗结算，可以上阵或不上阵。Battle 行为统一不等于产品身份统一：Hero 在产品定义上是独立产品层级、玩家自身 / 主角的载体，不因此被降级为普通 Person / Character。普通战斗中的 0HP、退出本场、牌库变化与胜负均由 GameSpec/Battle 定义。

旧个人行动力、战斗永久死亡、英雄不上阵仍提供旧基础光环等规则均不是 Current。本文也不为其他系统中的人物离开或死亡机制补充新裁决。

## 4.8 英雄：产品身份与后续专项设计入口

Hero 的**最低产品身份已经确认**：独立产品层级、玩家自身 / 主角载体。这不把 Hero 降级为普通 Person / Character，也不据此说明其完整机制。

仍未确认的是：

- Hero 完整成长；
- Hero 与 Profession 的最终关系（是否进入普通 Profession 体系、是否使用 Profession Formation）；
- Hero 独有资源；
- Hero 解锁；
- Hero 跨存档结构；
- Hero 的完整村庄职责；
- 其它正式 Hero 机制。

以下仅为候选或历史母题，不是 Current 规则：

- 上阵 / 下阵是否对应两种英雄姿态；
- 清路、青鹿等英雄是否分别承载团结、发展或统筹主题；
- 团结点、统筹点等正式资源；
- 英雄与共同体、职业化身之间的最终关系（“主角 / 玩家载体”已确认为最低产品身份，不再是候选）；
- 英雄声望与其它旧解锁承载。

Hero 的具体机制必须在后续专项设计中先明确“为什么存在”，再决定其战斗、村庄与成长承载；不得从“Hero 是独立产品层级”推导上述未冻结规则。

## 4.9 仍未冻结

以下内容仍是候选或后续入口：

- Personality 正式模型、Relationship 正式维度 / 数值 / 内容、Life Episode 聚合 / UI 等具体机制；Autonomous Life 的 Person / Relationship 长期职责边界已于 2026-10-04 归位 Current Architecture；
- 长期稳定角色身份的 Formation 机制细节（数值尺度、阈值、滞后量、Duration 等）与最终命名（Formation 基础规则已归位 Current，见 [GameSpec/Person/](../GameSpec/Person/Person-Profession-Growth-GameSpec-V0.1.md) §12）；
- Profession Trait 的具体效果、强化节点、数值与最终命名（当前 Trait 已归位 Profession Trait，无独立 Person-specific battle Trait）；
- 多职业 / Dormant 的正式结构；
- 装备正式体系；
- 人物长期离开、死亡或替补机制；
- 等级、经验、Starter、熟练和 Trait 的全部正式数值；
- 研究院、训练场等是否成为人物成长的正式村庄系统。

## 4.10 人物与风险的设计检查

RRPG 的长期价值对象是**具体 Person**，而非可随时替换的战斗槽位。设计风险机制时，应检查它是否深化玩家与这个人的长期连接，还是仅消耗一个可替换单位。这是设计原则，不替代各领域的可执行规则。

受伤、状态变化、经历后果、职业变化、年龄／生命阶段及关系后果属于 **Risk Design Directions / Not Fully Frozen**；列举方向不表示六类机制均已成为 Current Rule，也不从中推出普通 Battle 永久死亡、伤残或人物离场规则。具体后果必须由相应 Current Authority 决定。
