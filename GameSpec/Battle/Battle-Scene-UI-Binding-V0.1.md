# RRPG｜Battle Scene & UI Binding Spec V0.1
## Unity 场景对象 / UI对象 / 按钮 / 引用绑定规格

**版本日期：2026-08-28**  
**配套文档：Battle GameSpec V0.1**  

> 当前场景路径说明（2026-10-03）：原 BattleController 等界面宿主已收纳到纯 Transform 整理容器 SceneUIHosts。本文以 BattleController 开头的路径按宿主相对路径理解，完整场景路径增加 SceneUIHosts 前缀；原组件、Inspector / UnityEvent、内部层级和玩家规则保持不变。容器不作为 Canvas 或统一启停入口，详见[技术架构总览 §12.3](../../Docs/Architecture/技术架构总览.md)。
**用途：与 Battle GameSpec 同时交给 Cline / Partner，用于完成战斗系统的 Unity 场景、UI骨架、对象创建、功能绑定和开发侧验证。**

> **Binding 与阶段状态：**本文件仅负责长期 Scene/UI 对象、Inspector 与绑定要求，不得修改[Current Battle GameSpec](Battle-GameSpec-V0.1.md)的玩家规则。Stage 0–7 是已经执行的历史开发／验收计划，不是当前待办；实时实现与测试状态见[项目状态快照](../../Docs/项目状态快照.md)和[开发验收交接](../../Docs/开发验收交接.md)。对象、布局、视觉绑定要求及验收条件不因阶段结束而失效。

---

# 0. 本文档解决什么问题

Battle GameSpec 负责：

> **战斗应该怎么运行。**

本文档负责：

> **为了让玩家能够操作和观察这些规则，Unity中必须存在什么对象、按钮、Prefab、显示节点和引用。**

本文档**不负责最终视觉设计**。

不规定：

- 最终坐标；
- 最终尺寸；
- 最终字体；
- 最终颜色；
- 正式素材；
- 最终构图；
- 最终动画；
- 最终视觉层级。

这些由用户在 Unity 中最终调整。

---

# 1. 核心协作原则

## 1.1 Agent职责

Cline / Partner负责：

- 创建必要Scene对象；
- 创建必要UI对象；
- 创建必要Prefab；
- 创建按钮；
- 绑定按钮功能；
- 绑定数据；
- 绑定引用；
- 处理显示/隐藏；
- 处理可用/禁用状态；
- 创建动态对象生成逻辑；
- 保证Inspector引用完整；
- 保证运行时不会Missing Reference；
- 编译；
- Unity PlayMode验证；
- 输出对象映射表。

---

## 1.2 用户职责

用户负责：

- 最终对象坐标；
- 最终UI布局；
- 最终对象尺寸；
- 最终战场构图；
- 角色显示大小；
- 卡牌视觉；
- 字体；
- 配色；
- 美术资源；
- 动画表现；
- 视觉层级；
- 最终视觉验收。

---

## 1.3 已有产品宿主优先

本规格中的对象名首先表示：

> **功能身份 / 推荐名称。**

它们不是要求 Agent 无条件重新创建同名 Scene、Canvas、UI、Controller 或 Prefab 的指令。

如果当前项目已经存在承担相同产品职责的对象，例如 `BattleUI.handContainer` 可以承担 `Container_Hand`，Agent必须先建立：

> 规格功能身份 → 当前实际对象 / Hierarchy / Prefab路径

映射，并优先复用、扩展或替换其旧行为。只有 Existing Host Map / Stage 0 审计确认没有可合理复用的宿主时，才创建缺失对象。

不得为了隔离 Legacy、匹配规格名称或方便测试，在现有 Battle 产品路径旁边默认建立第二套正式 Canvas、Battle HUD、手牌容器、Controller 或初始化入口。确需新建同职能正式宿主时，必须先报告现有宿主无法复用的原因、新建必要性、对真实玩家路径的影响和旧宿主退出方案。

Debug/Test Harness、临时验证 Canvas、Mock 和独立 Runner 可以存在，但必须明确标记为 Debug/Test、默认不进入正常玩家流程，并且不能代替正式 Battle 产品路径的集成完成。

---

# 2. 最高优先级规则：逻辑与视觉必须分离

> 当前宿主映射补充（2026-10-03，仅更新技术绑定）：战前人物/英雄准备卡使用 `BattleController/BattleCanvas/PrepCharacterHandRoot`，绑定 `BattleUI.prepCardContainer`；战中行动牌使用 `BattleController/BattleCanvas/BattleActionHandRoot`（原 PrepCardRoot 原位更名），绑定 `BattleUI.handContainer` 与 `BattleV1Runtime.handContainer`。原对象身份、组件与引用保留；准备卡容器为原 BattleCanvas 下的 full-stretch RectTransform，不增加第二套 Canvas 或战斗入口；不改变本规格的玩法与布局调节权。技术链见[技术架构总览 §12.2](../../Docs/Architecture/技术架构总览.md)。

任何影响规则的逻辑：

> 不得依赖最终屏幕坐标。

例如：

`PlayerSlot_01`

可以代表：

> 我方逻辑槽位01。

用户把它从：

> X=200

移动到：

> X=700

以后：

> 槽位身份仍然是01；
> 邻接关系不改变；
> 三排逻辑空间规则不改变；
> 目标规则不改变。

也就是说：

# Transform / RectTransform只负责视觉位置，不负责定义核心玩法关系。

逻辑关系必须通过：

- Slot ID；
- Row类型；
- Neighbor ID；
- Side；
- Target Scope；

等独立数据表达。

---

# 3. 禁止硬编码视觉参数

以下参数原则上必须允许用户在 Unity Editor 中直接调整：

- 战斗槽位位置；
- 角色显示位置；
- 敌人显示位置；
- 角色Scale；
- 敌人Scale；
- 手牌区域位置；
- 卡牌间距；
- 卡牌大小；
- 行动力显示位置；
- 结束回合按钮位置；
- Intent挂点位置；
- HP挂点位置；
- 名称挂点位置；
- 状态显示挂点；
- 战斗结果UI位置；
- 奖励UI位置；
- 所有重要UI容器的Position；
- 所有重要UI容器的Size；
- 文本、图片等最终视觉参数。

禁止：

> 用户想移动一个UI，必须让Cline改C#常量。

---

# 4. 不得覆盖用户手调布局

Cline首次创建对象时：

> 可以使用任何合理的临时位置保证对象可见。

但对象创建完成以后：

> 用户手动修改的Transform / RectTransform不得在后续功能更新中被无条件重置。

后续Agent修改逻辑时：

- 不得重新生成整个UI覆盖旧Scene；
- 不得因为Prefab更新重置用户布局；
- 不得因为新增字段覆盖已有Transform；
- 不得主动“优化排版”。

如果确实因为结构修改必须影响用户布局：

> 必须在执行前明确报告。

---

# 5. Scene对象必须可被用户找到

基础可调对象：

> 不允许全部隐藏在运行时代码中生成。

原则：

### 固定对象
应存在于：

- Scene Hierarchy；
- 或可编辑Prefab。

### 动态重复对象
例如：

- 卡牌；
- 角色卡；
- 敌人；
- 奖励卡；

可以运行时实例化。

但必须来自：

> 用户可直接找到并编辑的Prefab。

禁止：

> 用代码运行时临时创建一个无法在Editor中找到原型的完整UI。

---

# 6. 推荐对象命名规则

如果当前项目已有稳定命名规则：

> 优先沿用。

如果没有，则建议：

| 前缀 | 用途 |
|---|---|
| `Root_` | 大型功能根节点 |
| `Panel_` | UI面板 |
| `Container_` | 动态内容容器 |
| `Slot_` | 逻辑槽位 |
| `Anchor_` | 视觉挂点 |
| `Btn_` | Button |
| `Txt_` | 文本 |
| `Img_` | 图片 |
| `Prefab_` | Prefab概念名 |
| `Overlay_` | 覆盖层 |

最终实际Scene对象名可以与这里不同。

但Cline必须提交：

> **规格名 → 实际Hierarchy对象路径**

映射。

如果现有对象已经承担该功能，映射到现有对象即可；不得仅为匹配本规格名称而重命名或重复创建对象。

---

# 7. 战斗场景一级对象

Battle Scene至少应存在以下功能根节点。

## 7.1 `Root_Battle`

职责：

> 当前战斗所有功能对象的总根节点。

---

## 7.2 `Root_BattleField`

职责：

> 敌我战场实体区域。

子节点至少包含：

- `Root_PlayerSlots`
- `Root_EnemySlots`

---

## 7.3 `Root_BattleUI`

职责：

> 战斗HUD总根节点。

至少承载：

- 手牌；
- 行动力；
- 回合状态；
- 结束回合；
- 牌堆数量；
- 必要战斗信息。

---

## 7.4 `Panel_PreBattle`

职责：

> 战前选人 / 布阵阶段的功能UI根节点。

战斗正式开始以后隐藏。

---

## 7.5 `Panel_BattleHUD`

职责：

> 正式战斗期间HUD。

战前阶段可隐藏或部分隐藏。

---

## 7.6 `Panel_BattleResult`

职责：

> 胜利 / 失败结果阶段。

正常战斗中隐藏。

---

## 7.7 `Panel_CardReward`

职责：

> 战斗胜利后的卡牌奖励阶段。

正常战斗中隐藏。

---

# 8. 玩家战斗槽位

## 8.1 玩家槽位根节点

`Root_PlayerSlots`

职责：

> 所有我方逻辑战斗槽位的父节点。

---

## 8.2 玩家槽位数量

当前系统需要支持：

> 3排×3列，共9个逻辑位置；最多同时上阵6名我方单位。6名上阵单位不等于只有6个逻辑位置。

Scene / Prefab / 配置必须能够映射 Current 的9个逻辑位置，复用现有宿主，不为规格名称建立第二套战场对象。以下仅为可映射的对象命名示例，不要求重建或重新布局：

- `Slot_Player_01`
- `Slot_Player_02`
- `Slot_Player_03`
- `Slot_Player_04`
- `Slot_Player_05`
- `Slot_Player_06`
- `Slot_Player_07`
- `Slot_Player_08`
- `Slot_Player_09`

---

## 8.3 Slot必须具备的数据

每个Slot必须拥有可读取的逻辑身份。

至少包括：

- Slot ID；
- 我方/敌方Side；
- 三排 Row 与 Left / Center / Right 列身份（服从 Battle GameSpec R5.0 / R6）；
- 按 Battle GameSpec R6 读取的动态邻接关系，不以视觉距离代替；
- 当前占用角色；
- 是否有效；
- 必要Target信息。

这些关系不得由屏幕坐标推算。

---

## 8.4 Slot位置

每个Slot的Transform：

> 必须允许用户直接拖动修改。

用户改变Slot位置以后：

> 不需要修改代码。

---

# 9. 敌方槽位

## 9.1 根节点

`Root_EnemySlots`

---

## 9.2 槽位

当前基础系统需要：

> 支持多个敌人。

具体最大敌人数属于后续内容参数。

因此：

> Enemy Slot数量不应该被硬编码为某个敌人数。

可以：

- 预创建足够数量；
- 或根据配置生成；

但所有最终视觉坐标必须可调。

---

## 9.3 敌方逻辑

Enemy Slot和Player Slot使用同样的基础空间定义：

- 三排 Row 与 Left / Center / Right 列身份；
- Neighbor；
- Slot ID；
- Side。

不得用一套完全不同的空间规则。

---

# 10. 角色显示 Prefab

当前V0.1：

> **不要求正式角色实体。**

可以继续使用简单角色卡片 / 占位实体。

推荐Prefab：

`Prefab_CombatCharacterView`

---

## 10.1 当前必须显示

最小必须包括：

- 角色名称；
- 当前HP；
- 最大HP；
- 可识别的角色主体/占位图片；
- 是否已经失去战斗能力。

---

## 10.2 当前不要求正式完成

V0.1不要求：

- 正式人物立绘；
- 正式动作动画；
- 完整技能列表；
- 完整状态图标体系；
- 最终Buff UI；
- 最终Debuff UI；
- 正式人物卡框；
- 最终角色美术。

---

## 10.3 预留挂点

建议创建空节点：

- `Anchor_Name`
- `Anchor_HP`
- `Anchor_Status`
- `Anchor_Effect`
- `Anchor_Visual`

用户后续可以移动这些挂点。

这些挂点当前即使没有完整内容：

> 也允许为空。

---

# 11. 敌人显示 Prefab

推荐：

`Prefab_CombatEnemyView`

必须至少显示：

- 敌人名称或可识别身份；
- 当前HP；
- 最大HP；
- 敌人占位视觉；
- Intent信息。

---

## 11.1 Intent挂点

建议：

`Anchor_Intent`

必须允许用户移动。

Intent具体最终表现：

> 图标、文字、数字等

由后续视觉设计决定。

当前只要求：

> 玩家能读懂下一步主要行为。

---

# 12. 手牌系统

## 12.1 手牌容器

`Container_Hand`

职责：

> 运行时生成当前手牌。

---

## 12.2 卡牌Prefab

`Prefab_BattleCard`

当前基础版不要求正式卡面。

最低信息：

- 卡牌名称；
- 行动力费用；
- 卡牌描述；
- 角色归属或通用牌身份；
- 是否当前可用；
- 必要目标类型。

---

## 12.3 角色牌身份必须可读

玩家必须能够区别：

> 这张牌属于哪个角色。

表现方法可以暂时非常简单：

- 角色名字；
- 角色头像；
- 标签；
- 卡框；
- 角标。

具体视觉由用户以后决定。

Agent只要求：

> 先存在一个明确的信息字段。

---

## 12.4 通用牌

通用牌必须：

> 可以明确区别于角色牌。

当前可以使用：

> `通用`

文字占位。

---

# 13. 卡牌视觉布局

手牌自动排列允许Agent提供：

> 临时可用方案。

但：

- 手牌Container位置必须可调；
- 卡牌Spacing必须可调；
- 卡牌Size必须可调；
- 自动排列参数不得写死；
- 用户后续能够替换Layout表现。

如果使用自动Layout组件：

> 不得因此限制用户最终调整方案。

---

# 14. 共享行动力UI

必须存在并完成绑定（优先映射/复用现有对象，确认缺失时才创建）：

`Txt_SharedActionPoint`

或功能等价对象。

职责：

> 显示全队唯一行动力。

---

## 14.1 禁止旧结构残留

不得继续显示：

> 每角色独立行动力。

如果旧角色卡上还有个人行动力文本：

> 必须停用或移除其功能意义。

---

## 14.2 数据绑定

行动力：

- 回合开始刷新；
- 使用牌后更新；
- 卡牌增加/减少行动力后更新。

必须即时刷新。

---

# 15. 回合UI

## 15.1 结束回合按钮

必须存在并完成绑定（优先映射/复用现有对象，确认缺失时才创建）：

`Btn_EndTurn`

点击：

> 结束玩家当前回合。

---

## 15.2 可用状态

仅当：

> 当前处于PlayerTurn；
> 战斗未结束；

按钮可用。

EnemyTurn：

> 按钮必须禁用或不可交互。

---

## 15.3 回合状态

创建：

`Txt_TurnState`

当前占位即可显示：

- 玩家回合；
- 敌人回合。

最终视觉形式以后替换。

---

# 16. 输入锁定

当系统：

- 正在EnemyTurn；
- 正在处理不可中断结算；
- 战斗已经结束；

玩家不得继续重复出牌。

Agent需要存在：

> 统一输入禁用机制。

可以表现为：

`Overlay_InputBlocker`

也可以通过系统控制所有交互。

具体技术方案由Agent决定。

---

# 17. 抽牌 / 弃牌 / 消耗显示

最低创建：

- `Txt_DrawPileCount`
- `Txt_DiscardPileCount`

如果当前实现消耗区：

- `Txt_ExhaustPileCount`

当前只要求：

> 数字正确。

不要求：

> 点击查看完整牌库。

后续如需要再扩展。

---

# 18. 卡牌选择与目标选择

基础原则：

> UI只负责表达GameSpec中的合法目标规则。

当玩家选中需要目标的牌：

- 合法目标必须可被识别；
- 非法目标不能成功使用；
- 玩家必须能够取消当前卡牌选择。

---

## 18.1 合法目标反馈

当前允许使用极简占位反馈：

- 高亮；
- Outline；
- 简单颜色；
- 临时文字。

最终视觉以后替换。

---

## 18.2 取消选择

系统必须存在：

> 取消当前卡牌/目标选择的方法。

具体是：

- 再点一次牌；
- 点击取消按钮；
- 右键；
- ESC；

如果现有系统已有成熟方式：

> 沿用。

如果现有系统没有：

> Agent不得擅自决定最终交互方案；
> 应先实现可调用的Cancel接口，并在Stage报告中标记UX未决。

---

# 19. 战前选人与布阵

必须存在：

`Panel_PreBattle`

至少具有：

- 当前远征可用人物列表/容器；
- 可映射9个逻辑位置的 Player Slot（最多同时上阵6名我方单位）；
- 开始战斗按钮。

---

## 19.1 可用人物容器

建议：

`Container_AvailableCharacters`

用于展示：

> 当前远征中可参与本战的人物。

---

## 19.2 开始战斗按钮

`Btn_StartBattle`

只有当前阵容满足基础合法条件时：

> 可以进入Battle。

---

## 19.3 布阵最终交互

如果当前项目已经存在：

> 拖拽 / 点击选择 / 其他可工作的布阵方法；

优先复用。

如果不存在：

> Agent只负责建立：
> 人物数据 ↔ Slot绑定接口；
> Slot对象；
> 可视反馈；
> BattleStart读取结果。

最终布阵手势：

> 属于用户UX确认项。

不得因为缺最终手势阻塞其他Battle模块开发。

---

# 20. 战中位置变化

如果卡牌/特性改变角色位置：

UI必须：

1. 更新角色对应Slot；
2. 更新角色显示位置；
3. 重新计算邻接；
4. 更新必要视觉反馈。

禁止：

> 只移动图片但逻辑Slot没变。

也禁止：

> 逻辑Slot变了但画面角色没动。

---

# 21. HP显示

每个角色和敌人：

> 必须存在HP可读信息。

当前可以使用：

> `当前HP / 最大HP`

简单文本。

不要求正式血条。

---

## 21.1 HP变化

受到伤害/恢复：

> UI必须即时刷新。

---

# 22. 角色0HP表现

当角色HP归零：

逻辑由GameSpec执行。

UI至少必须：

1. 明确表现该角色已经不能继续战斗；
2. 不再作为正常目标；
3. 角色牌同步从手牌/牌库移除；
4. 邻接表现同步更新。

---

## 22.1 当前视觉方式

V0.1允许：

- 隐藏；
- 灰化；
- 简单倒地占位；
- 退出Slot。

最终正式表现：

> 用户以后决定。

Agent不得为了视觉效果重新加入：

> “倒地仍占位、仍吃Buff、仍参与邻接”

等Gameplay规则。

---

# 23. 敌人Intent

每个EnemyView必须有：

`Anchor_Intent`

并绑定：

> 当前Intent状态。

在PlayerTurn决策前：

> 必须可见。

---

## 23.1 Intent更新

敌人：

- 行动完成；
- 状态改变；
- Intent重新生成；

以后：

> UI同步刷新。

---

# 24. 战斗胜负面板

`Panel_BattleResult`

默认隐藏。

---

## 24.1 胜利

敌方全灭：

> 显示Victory状态。

最低可以是：

`Txt_BattleResult = 胜利`

---

## 24.2 失败

全员失去战斗能力：

> 显示Defeat状态。

---

## 24.3 结果后续

BattleResult必须能够：

> 进入战后结算/远征流程。

具体过渡动画：

> 当前不要求。

---

# 25. 战后HP反馈

战斗结束以后：

> 执行战后恢复。

UI必须至少能够让用户验证：

> 恢复前HP；
> 恢复后HP。

当前不要求正式动画。

可以：

> 直接数值刷新 + 临时日志。

---

# 26. 卡牌奖励UI

`Panel_CardReward`

默认隐藏。

胜利后：

> 根据GameSpec生成候选卡。

---

## 26.1 奖励卡容器

`Container_RewardCards`

运行时生成：

> 候选卡牌Prefab。

---

## 26.2 必要信息

每张奖励牌至少显示：

- 卡名；
- 所属角色 / 通用；
- 费用；
- 描述。

---

## 26.3 放弃

必须存在：

> 放弃本次选牌的功能入口。

推荐对象名：

`Btn_SkipReward`

---

## 26.4 奖励确认方式

如果当前项目已有：

> 点击卡牌直接选取；
> 选中后确认；

等现成交互：

> 优先沿用。

如果没有：

> Cline先搭候选卡对象和选择接口；
> 不擅自决定最终正式交互。

---

# 27. 当前不制作的正式UI

Battle V0.1开发阶段不要求：

- 正式角色实体；
- 正式敌人实体；
- 最终HUD；
- 正式状态图标；
- 大量Buff/Debuff图标；
- 正式动画；
- 正式伤害飘字；
- 正式卡牌视觉；
- 正式Intent图标；
- 正式胜利界面；
- 正式奖励界面；
- 正式战场背景。

只要求：

> **功能完整、信息可读、对象可调。**

---

# 28. 必须暴露给用户的可调项

Agent完成开发后，用户必须无需改代码即可调整至少：

### 战场
- Player Slot Transform；
- Enemy Slot Transform；
- Character View偏移；
- Enemy View偏移；
- Character Scale；
- Enemy Scale。

### UI
- Hand Container；
- AP UI；
- Turn UI；
- EndTurn Button；
- Draw/Discard显示；
- Result Panel；
- Reward Panel。

### Prefab
- Character View视觉挂点；
- Enemy View视觉挂点；
- Card视觉；
- Intent挂点；
- HP挂点；
- Status挂点。

---

# 29. 用户对象地图

每个Stage完成后：

> Cline必须提交一次对象地图。

格式至少包括：

| 实际对象名 | Hierarchy / Prefab路径 | 用途 | 绑定 | 用户可调 |
|---|---|---|---|---|

例如：

| `Slot_Player_01` | `BattleRoot/BattleField/PlayerSlots/...` | 玩家槽位01 | PlayerSlot ID=1 | Transform |
| `Btn_EndTurn` | `BattleRoot/UI/...` | 结束回合 | EndPlayerTurn | RectTransform |
| `Txt_SharedAP` | `BattleRoot/UI/...` | 共享行动力 | SharedAP | RectTransform/Text视觉 |

这样用户不需要读代码。

只需知道：

> “我要调什么，就找到哪个对象。”

---

# 30. Stage开发顺序（已执行的历史开发／验收计划）

以下 Stage 0–7 已执行；保留原阶段内容和命令式措辞用于历史追溯，不要求重新从 Stage 0 开始。后续维护按当前任务范围进行，不以本序列生成新的开发任务。

Battle GameSpec与本文件：

> 不先后单独执行。

而是每个Stage同时读取对应功能与对应UI对象。

本节各 Stage 的“验收”默认指该 Technical Stage 的功能/自动验证目标，不表示每个小 Stage 完成后都要停止并请求用户人工验收。Technical Stage 可在机器验证通过后连续推进；是否请求用户验收由 `Docs/AI开发执行规则.md` 的 Playable Milestone 规则决定。

---

## Stage 0｜现状审计

Cline先检查：

- 当前Battle Scene；
- 现有角色卡；
- 现有按钮；
- 现有Slot；
- 现有Canvas；
- 现有Prefab；
- 现有拖拽/点击交互；
- 旧AP显示；
- 旧角色牌；
- 当前可复用UI。

输出：

### Scene/UI Coverage Map + Existing Host Map

先记录真实玩家入口和现有宿主链，例如：

> MainMenu → Town → Expedition → `ExpeditionUI.battleCanvas` → `BattleUI`

再按下表映射：

| 新规格职责 | 当前类 / 对象 / Prefab / 路径 | 当前处理方式 | 迁移处理 | 理由与产品路径影响 |
|---|---|---|---|---|
| 手牌容器 | 例如 `BattleUI.handContainer` | 旧手牌显示 | 复用 / 扩展 / 替换 / 新建 | 新逻辑如何进入真实 Battle |

分类：

- 已存在，可直接复用；
- 已存在，但逻辑需改；
- 已存在，但应该停用；
- 确认缺失，需要创建。

禁止：

> 未审计就整套重建UI。

---

## Stage 1｜共享牌库 + 行动力 + 最小手牌UI

同时完成：

### 功能
- Shared Deck；
- Draw；
- Hand；
- Discard；
- Shared AP。

### UI
- `Container_Hand`
- `Txt_SharedActionPoint`
- `Txt_DrawPileCount`
- `Txt_DiscardPileCount`
- BattleCard Prefab

### Stage 功能/自动验证目标
目标：

> 牌能显示、能抽、AP正确、牌堆数字正确。

不验收位置。

如果当前项目已有 Battle 产品入口，本 Stage 的 Product Integration Verification 必须从该真实入口进入现有 Battle 宿主并看到上述行为。独立 Stage HUD 或 Test Harness 通过只计为 Module Verification，不能据此宣称 Stage 1 已接入产品。

---

## Stage 2｜回合生命周期

### 功能
- PlayerTurn；
- EnemyTurn；
- 回合切换。

### UI
- `Btn_EndTurn`
- `Txt_TurnState`
- 必要输入锁。

### Stage 功能/自动验证目标
目标：

> 回合操作正确。

---

## Stage 3｜上阵 / 布阵 / 空间

### 功能
- 最多6人；
- Player Slot；
- Enemy Slot；
- 三排×三列逻辑空间（9个位置，上阵上限6人）；
- 邻接；
- 战前布阵；
- 战中锁位。

### Scene对象
- `Root_PlayerSlots`
- `Slot_Player_01～09`（Current 映射示例；阶段历史不构成重建对象或修改视觉布局的指令）
- `Root_EnemySlots`
- `Panel_PreBattle`
- `Container_AvailableCharacters`
- `Btn_StartBattle`
- Character/Enemy View Prefab

### Stage 功能/自动验证目标
目标：

> 人物和逻辑Slot对应正确；
> 布阵结果能进入战斗；
> 邻接正确。

然后：

> 用户可以开始手调Slot视觉位置。

---

## Stage 4｜HP / 失去战斗能力

### 功能
- 独立HP；
- 0HP退出；
- 角色牌移除。

### UI
- Character HP；
- Enemy HP；
- 失能占位反馈。

### Stage 功能/自动验证目标
目标：

> 一人0HP后他的牌消失，其余人继续。

---

## Stage 5｜敌人 / Intent / 胜负

### 功能
- Intent；
- EnemyTurn；
- 胜负。

### UI
- `Anchor_Intent`
- `Panel_BattleResult`
- `Txt_BattleResult`

### Stage 功能/自动验证目标
目标：

> Intent与行为一致；
> 胜负正常。

---

## Stage 6｜战后 / 卡牌奖励

### 功能
- HP跨战斗；
- 战后恢复；
- 战斗奖励；
- Run新牌。

### UI
- `Panel_CardReward`
- `Container_RewardCards`
- `Btn_SkipReward`
- 必要Result继续入口。

### Stage 功能/自动验证目标
目标：

> 战后恢复正确；
> 新牌进入下一战。

---

## Stage 7｜完整纵向Play验证

完整流程：

> 战前选人  
> → 布阵  
> → 开战  
> → 抽牌  
> → 出牌  
> → 结束回合  
> → Enemy Intent  
> → 敌人行动  
> → 角色0HP  
> → 角色退出  
> → 战斗胜利  
> → 战后回血  
> → 奖励卡  
> → 下一战。

---

# 31. 三层验证与里程碑验收制度

## 第一层｜Agent自动验收

Cline必须自己检查：

- Compile；
- Console；
- EditMode；
- PlayMode；
- Missing Reference；
- Button Binding；
- Prefab引用；
- Scene引用；
- 数据更新；
- 基础流程。

这些：

> 不应该交给用户发现。

---

## 第二层｜Playable Milestone 用户功能验收

若干 Technical Stage 形成用户可以通过正常游戏操作直接判断对错的 Playable Milestone 后：

用户只判断：

> 功能是不是自己要的。

例如：

- AP共享了吗？
- 抽牌对吗？
- 角色0HP规则对吗？
- 邻接对吗？
- Intent对吗？

只把已经形成可玩闭环的项目放入一页以内、原则上不超过 10 项的 Milestone Acceptance Card；每项使用“操作 → 正确表现”。机器可可靠判断或需要理解 Legacy、Placeholder、内部 Stage 的内容不交给用户。

用户此阶段：

> 不需要调整正式美术。

---

## 第三层｜用户视觉验收

当Battle核心功能完成以后：

用户集中调整：

- Slot位置；
- Character大小；
- Enemy大小；
- Hand位置；
- UI位置；
- UI尺寸；
- 视觉素材；
- 状态挂点；
- Intent位置；
- 卡牌表现；
- 整体构图。

然后：

> 做最终视觉Play验收。

---

# 32. 用户视觉调整保护

当用户已经手动调整一个Scene/Prefab视觉参数以后：

后续Cline修改逻辑时：

> 默认必须保留。

只有用户明确要求：

> “重新排版 / 重置这个对象”

才允许主动改变。

特别保护：

- Slot Transform；
- UI RectTransform；
- Scale；
- Visual Anchor；
- 用户替换的Sprite；
- 用户设置的字体/颜色；
- 用户调整的Prefab视觉结构。

---

# 33. 完成定义

Battle Scene/UI V0.1只有满足以下条件才算完成：

### 所有必须对象存在
Scene/Prefab可找到。

### 所有必须按钮已绑定
不存在占位按钮无功能。

### 所有动态信息正确刷新
HP/AP/Intent/牌堆等。

### 所有功能可以实际操作
不是只在代码测试中成立。

### 用户可以自由移动重要视觉对象
不改代码。

### 用户移动对象不会破坏Gameplay
视觉与逻辑分离。

### 不覆盖用户视觉调整
后续逻辑修改安全。

### Object Map完整
用户知道每个对象是什么。

### PlayMode完整流程通过
战前→战斗→战后→奖励能够跑通。

### 真实产品路径已经接入
从当前游戏真实入口使用正式流程数据进入现有 Battle 宿主；新逻辑已经替换/接入旧行为，上下游流程没有被破坏。

### 不存在平行第二套正式 Battle 流程
Debug/Test Harness 已明确隔离且默认不进入玩家路径；若存在新正式宿主，已有审计和旧宿主退出方案。

---

# 34. 当前明确留给用户最终决定的内容

以下项目不属于Cline自主权：

- 最终战场构图；
- 角色卡最终长什么样；
- 是否改成完整角色实体；
- 状态最终怎么表现；
- Intent最终表现形式；
- 手牌最终视觉；
- 按钮最终位置；
- 按钮最终样式；
- 卡牌最终大小；
- 角色/敌人最终Scale；
- 战前布阵最终视觉交互方式；
- 奖励选牌最终确认交互；
- 视觉动画与节奏。

Cline可以提供：

> 功能占位版本。

但不能因为占位版本可运行：

> 将其视为最终视觉设计。

---

# 35. Battle开发包最终组成

以下保留原开发包分工；Stage 序列是已执行的历史流程，非下一步开发指令：

## 文档A
`Battle GameSpec V0.1`

负责：

> 游戏必须怎么运行。

## 文档B
`Battle Scene & UI Binding Spec V0.1`

负责：

> Unity中必须有什么对象以及怎么连接。

## 当前项目
负责提供：

> 已有代码 / Scene / Prefab / 功能。

当时的 Cline 开发流程：

> 三者对照 → Stage 0审计 → 最小迁移方案 → 分阶段开发。

---

# 36. 最终开发原则

> **功能和最小可操作界面同步开发。**

不是：

> 全部功能做完再接UI。

也不是：

> 每做一个功能就完成正式视觉。

而是：

> 功能  
> + 最低限度可操作对象  
> + 数据绑定  
> + Technical Stage机器验证
> + 形成Playable Milestone时的用户功能验收

依次推进。

全部功能稳定以后：

> 用户统一完成最终视觉布局。

目标是：

> **AI负责把机器组装并接好线；用户负责最终把它摆成真正的游戏。**
