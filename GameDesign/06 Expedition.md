# 06 Expedition

> 状态：GameDesign V0.2 当前远征设计意图。节点、Battle 与结算的可执行规则仍由相应 GameSpec 管理。
> 2026-09-07 FP-V0.1 归位同步：远征推进结构升级为 **Node-based Progression**；本文件只表达设计意图，可执行规则见 [GameSpec/Expedition/Expedition-Progression-GameSpec-V0.1.md](../GameSpec/Expedition/Expedition-Progression-GameSpec-V0.1.md)。
> 2026-09-18 Journey Presentation 归位同步：玩家层默认表现 = 队伍向右旅行的 Journey Presentation（§6.2A）；Node-based Progression 仍是后台规则结构，Continue / Pause / Speed-up 只影响表现，不产生规则时间。
> 2026-09-19 Initial Expedition Party Capacity 归位同步：出征准备阶段最多 6 Character + optional Hero，Hero 独立可选、不占该 Character 上限（初始队伍最多 7 名成员），并与单场 Battle Active Roster 分离（§6.3）；最低出征人数不受影响；Mid-run Join Capacity 保持 Not Frozen。

## 6.1 远征在整体循环中的职责

远征把村庄的长期条件和具体人物带入不确定环境，并把风险、经历与暂存收益带回长期世界。

它负责形成：

- 本次 Run 的卡牌构筑；
- 人物 HP 跨战风险；
- 领悟与卡牌奖励节奏；
- 暂存收益与返程压力；
- 人物经历与新的村庄条件；
- 玩家是否继续深入或选择保全成果的取舍。

## 6.2 远征推进结构：Node-based Progression

> 选择人物与目标 → 进入远征推进（Node-based Progression）→ 承担 Battle / Situation / 事件 / Story / Discovery 等遭遇风险 → 获得 Run 构筑与暂存收益 → 继续深入 / Branch / Return → 按结束方式结算

2026-09-07 已确认：**Node-based Progression 作为 Expedition 的推进结构**，旧“节点式地图作为远征载体”不再作为当前表述。Node 是统一 Expedition Progression Unit，不等于 Battle（§6.3）；Branch 与 Return / Retreat 分离（§6.4 / §6.5）；Time 与抽象 Expedition Supply 只冻结职责（§6.6）；Legacy 点阵地图不再自动属于 FP-V0.1 Current Design（§6.9）。

## 6.2A Journey Presentation（当前确认的玩家层表现）

> Node-based Progression 继续作为远征规则结构；玩家层默认不显示完整节点图，而以队伍持续向右旅行的 Journey Presentation 表达推进。

当前确认：

- 远征准备确认的人物进入 Journey 后，以同一批具体人物组成可见队伍；
- 默认播放队伍向右行进、背景 / 环境相对移动的旅行表现；
- 抵达 Encounter 时，旅行自动中断并进入对应内容；
- Encounter 完成后回到 Journey，再继续下一段旅行；
- `继续前进`功能保留。在旅行播放期间，它的玩家语义是：**跳过当前旅行表现，抵达下一次需要玩家介入或进入内容的 Presentation 边界**；
- 如果下一次介入是 Branch，`继续前进`只跳到 Branch 表面，不能替玩家选择方向；
- Pause 只暂停 Journey Presentation；可选的 Speed-up 只加快 Presentation；二者都不改变远征规则状态；
- Journey 的现实播放秒数不等于游戏时间。行走期间不结算 elapsed days、Supply、HP、VillageTime 或其它规则事实；游戏时间与 Supply 仍由 Current Progression Resolver 在既有结算点处理；
- Supply、Party、Return / Retreat、无法推进原因与 Branch 功能继续保留，只从 Debug Node Panel 重新包装为 Journey HUD / Overlay；
- 第一版 Journey 可以继续使用当前有限 Graph 作为后台 Compatibility Progression 来源，玩家不需要看到完整 Graph；
- **一次 Journey Presentation Cycle 最多消费一个 Progression Transition。** 当前 Cycle 完成后，如果规则状态仍允许继续推进，必须重新读取更新后的 Current Progression State，再建立下一轮 Journey / Branch / Encounter Presentation；不得由上一轮 Travel / Encounter callback 在同一旧调用链中连续推进后续多个 Node。

长期产品方向：

- 主模式倾向无限远征：需要下一段内容时再从未来 Encounter Pool / Generation Rule 产生下一段，而不是在远征开始时预生成完整无限地图；
- 该方向当前属于 **Strong Direction / Not Implemented / Generation Algorithm Not Frozen**；
- WP-E 不修改 Graph 生成、Route Terminal、Boss 终点或正式 Encounter Pool。

人物表现边界：

- Journey 中的人物身份必须来自本次远征准确的 `PersonId`；
- Hero 与普通 Character 可以共用同一种 **Presentation Visual Resolution Contract**，但不因此合并产品身份；
- Prep、Journey、Battle、Village 可以共享 Identity 与基础 Visual Asset 解析契约，不共享同一个 UI Prefab / GameObject；
- 视觉解析必须先由 `PersonId`定位到准确具体人物，再允许读取该具体人物可用的模板 / 定义键查询外观；
- `CharacterId`、`HeroId`、`PersonDefinitionId`不得反向猜测或替代 `PersonId`；
- Current 只冻结 `PersonId → exact Person → Presentation Visual` 的方向与身份边界；
- `PersonVisualCatalog`、`PersonVisualResolver`、`PersonVisualProfile`、Sprite 字段、Animator、Spine、换装、模块化身体与正式美术生产管线均属于实现 / Future Art Pipeline，不由本 Authority 提前冻结。

## 6.3 Node 语义与地图表现边界

### Expedition Party 与本场参战成员

Expedition Party 可以包含本场未上阵成员，统一称为“本场未上阵的远征成员”。当前不存在“后勤位”、`Logistics Slot`、后勤槽或独立后勤系统；未上阵不自动产生后勤加成、资源职责、职业职责或被动。通常上阵边界服从 [Battle GameSpec](../GameSpec/Battle/Battle-GameSpec-V0.1.md) §4；特殊 Encounter 是否覆盖通常上阵人数上限单独保持 **Deferred / Not Frozen**，不是当前开发任务。

**初始出征准备阶段的 Character 上限（2026-09-19 Current，Authority 见 [Expedition GameSpec](../GameSpec/Expedition/Expedition-Progression-GameSpec-V0.1.md) §18）：**

- 出征准备阶段普通 Character 最多选择 **6 人**（最多 6 Character + optional Hero）；
- Hero 是**独立可选**的远征成员，**不占**这 6 人上限；
- 因此选择 6 Character + Hero 时，初始远征队伍最多可达 **7 名成员**；
- 本决定只冻结该 Character 上限与 Hero 是否计入该上限两件事；**最低出征人数不由本决定修改**，当前既有最低人数行为继续保持，除非制作人以后另行裁决；不得从本决定推出 `0 Character + Hero` 已合法。

**Expedition Party 与单场 Battle 的分离：**

> **Expedition Party Size ≠ 单场 Battle Active Roster Size。**

- 单场 Battle 仍最多同时上阵 6 个我方单位（Battle Current Rule，不由本文件改写）；
- Hero 如果参加本场 Battle，占一个正常上阵位置；
- 远征队伍中未参加当前 Battle 的成员仍属于本次 Expedition，但本场不进入 Active Roster，其角色牌也不进入本场共享牌库；
- 不建立“战斗中途常规替补换人”规则。

**Mid-run Join Capacity（继续 Not Frozen）：** 出征后通过 Recruit / Event / Situation 等方式中途加入人物时的 Party 总人数上限、满员后的替换 / 拒绝 / 暂存或其它处理方式、以及是否受另一套数量限制，均未冻结；不得从“初始最多 6 Character + Hero”推导整个 Run 永久最多 7 人。Supply / Carry 的 Party Size 计数定义（Hero 是否计入）同样不在本轮定义范围。

### Node 与内容承载

- Node 是统一 Expedition Progression Unit：可承载 Battle、Event、Story、Discovery 以及未来其它内容；正式 Node 内容池仍未冻结。
- Node 不等于 Battle；“已经到达节点”不等于“节点内容已经完成”，二者是不同阶段。
- Shop、Recruit、Rest 如果未来存在，是某个 Node Content / Situation 中可能出现的服务、机会或内容，**不要求各自成为固定地图 Node Type**；Current 产品模型不要求固定 Shop / Recruit / Rest 节点生态。
- Node-based Progression 不等于必须存在《杀戮尖塔》式常驻完整地图 UI。当前不冻结完整地图视觉、点阵地图、树状地图、世界地图与路线 UI 呈现。

## 6.4 Branch（分叉选择）

- Node 支持 0 / 1 / N 个合法后继；
- `N = 1` 时不制造无意义路线选择；
- `N > 1` 时形成 Branch Choice，且只能选择合法后继；
- Branch 只回答“下一步往哪里推进”，不回答“是否继续 / 结束本次 Expedition”（后者见 §6.5）。
- 具体分叉数量与生成算法仍未冻结。

## 6.5 Return / Retreat 是 Expedition 层独立选择

> Return / Retreat 是 Expedition 层独立选择，不是普通 Node Connection。

- Branch Choice 与 Return Choice 是两种不同决策。
- 禁止把 Return 设计成普通 `ReturnHomeNode`。
- 未来区分 Safe Return / Retreat / Failure；具体保留比例与损失仍未冻结（§6.12 与对应 GameSpec）。
- Return 后进入 Home↔Expedition Return / Catch-up 链（§6.7）。

## 6.6 Time + Expedition Supply

> Node 推进产生 elapsed-time cost；Supply 基础消耗由 elapsed time 推导，Explicit Extra Supply Cost 只用于特殊内容。

- Expedition Progression 在规则架构层继续使用抽象 **Expedition Supply**，不把 Supply 语义塞回某一具体资源，也不重新设计 Supply；
- FP-V0.1 标准 Carry / Supply 产品映射资源 = Village Food（真实资源转移）；该产品映射不改变 Progression 层抽象 Supply Authority；
- 原“Food 映射未冻结”表述已过时：FP-V0.1 标准产品映射见 [GameSpec/Village/Village-Resource-Coupling-GameSpec-V0.1.md](../GameSpec/Village/Village-Resource-Coupling-GameSpec-V0.1.md)；
- Progression 规则层仍只冻结职责，不冻结数值、单位、Supply 正式内容与每 Node 固定消耗；
- Expedition Runtime 维护累计 elapsed time、Expedition Supply、Party State 与收益；可执行规则见 [GameSpec/Expedition/Expedition-Progression-GameSpec-V0.1.md](../GameSpec/Expedition/Expedition-Progression-GameSpec-V0.1.md)。

## 6.7 Return 与 Village Time

> Expedition 期间累计 elapsed time；Return 时再进入 Home↔Expedition Return / Catch-up 链。

- 不把“每推进一个 Node 就直接修改 VillageTime”写成 Current。

## 6.8 Run Save / Resume：Deferred

- FP-V0.1 不要求 Expedition 中途 Save / Resume。
- 远征推进状态当前只需要会话内存在；Run Save / Resume 继续 Deferred。

## 6.9 Legacy Adventure Map 边界

- 历史 4×3 点阵地图、固定 Shop / Recruit / Rest / Boss 节点生态、固定节点比例与旧 MapGenerator runtime progression 不再自动属于 FP-V0.1 Current Design。
- FP-V0.1 的 Node 语义不等于恢复 Legacy 点阵地图或旧节点生态。
- 固定 Shop / Recruit / Rest 节点生态的退役不等于禁止未来交易、招募或休息内容：它们若存在，属于未来 Node Content 设计，产品形态不由旧规格决定。
- 旧实现可作为技术参考 / Legacy Compatibility 使用，但不得自动恢复成正式产品设计。
- Legacy 4×3 / Graph 继续作为 Compatibility 来源保留现状（后台 Progression 来源），不恢复其玩家层地图身份；Current 玩家层默认表现为 §6.2A Journey Presentation。

## 6.10 Run 构筑与领悟

人物以 Starter 提供起始角色牌，并从各自 Unlocked Pool 支持 Run 内候选。

领悟保留为当前正式的 Run 内成长机制方向，用于将远征过程中的积累转化为卡牌奖励机会。具体来源、阈值、数值、候选权重及最终命名均未冻结。

当前确认的方向：

- 领悟是 Run 内共享积累；
- 达到阈值后提供卡牌奖励机会；
- 扣除阈值后保留溢出；
- 一次积累可以跨越多个阈值并产生多次机会；
- 奖励采用多选一并允许跳过的方向。

领悟可以由未来规格确认的不同远征过程来源积累，不把 BattleVictory 固定为唯一来源。候选数量与具体表现同样未冻结，也不得把测试值写成正式设计值。

## 6.11 HP 风险与连续性

人物 HP 在同一次远征的不同战斗间继承，不在每场战斗后自动完全恢复。这个连续性让路线选择、战后恢复和继续深入产生真实压力。

单场 Battle 的 0HP 与胜负行为由 GameSpec/Battle 定义。旧“伤残 → 普通战斗永久死亡 → 招募补员”不再是当前远征核心循环。

## 6.12 暂存收益与 Return 归置

远征中的资源、材料、成长经验或其他长期收益先作为暂存结果，在 Return 后转换为人物或村庄的长期进度。

当前只冻结设计职责，不冻结数值：

- Return（含无限远征主动撤退与有限关卡完成两类 Normal Return）应是长期消化的清晰节点；
- 人物成长经验（ProfessionExp）：Run 内只累计 Pending，Return 时统一写入长期；Expedition Failure 也完整保留已产生经验；第一版不额外给予 Expedition Completion 经验（完整规则见 [GameSpec/Person/](../GameSpec/Person/Person-Profession-Growth-GameSpec-V0.1.md)）；
- 主动结束、被迫撤退和全队失败对**其它暂存收益**（金币、材料等）应有不同的保留与损失结果，具体损失比例仍未冻结；
- 实际 Build 默认随 Run 结束重置；
- 人物经历可以作为长期输出，但正式机制尚未确定。

## 6.13 与村庄的双向连接

村庄不只是远征外的商店或升级页。它通过人物成长机会、牌池扩展、训练控制、事件条件和共同体状态改变下一次远征可能出现什么；远征则把资源、经历、风险和新的社会条件带回村庄。

完整总体循环见 [02 核心循环与系统关系.md](02%20核心循环与系统关系.md)，村庄职责见 [07 Village.md](07%20Village.md)。

## 6.14 保留与未冻结边界

继续保留的方向（FP-V0.1）：

- Node-based Progression：Node 是统一 Expedition Progression Unit；
- Branch Choice 与 Return / Retreat 分离；
- Battle / Situation / Event / Story / Discovery 等不同 Node 内容职责（正式内容池未冻结）；
- Time Cost + Expedition Supply Cost 作为 Node 推进的职责；
- 远征内暂存、返程结算与长期世界之间的分层；
- Journey Presentation 作为 Current 玩家层表现契约：队伍向右旅行、Encounter 自动中断、`继续前进`跳过当前旅行表现、Pause / Speed-up 只影响表现、一次 Presentation Cycle 最多消费一个 Progression Transition（见 §6.2A）。

本轮不冻结：

- Node 正式内容池与节点种类、数量、概率；
- 具体分叉数量与生成算法；
- 完整地图不作为 Current 玩家层默认表现；Journey Presentation Contract 已确认，但最终画面、动画、HUD 布局、Biome 与正式环境内容仍未冻结；
- 长期无限远征的 Encounter Pool、生成算法、Biome、终止条件与正式内容（**Strong Direction / Not Implemented / Not Frozen**）；
- 正式人物视觉数据架构（`PersonVisualCatalog` / `PersonVisualProfile` / Sprite 字段 / Animator / Spine / 换装 / 模块化身体与美术生产管线）；
- Expedition Supply 的正式内容与数值（FP-V0.1 标准 Carry 映射资源 = Village Food 已由 `GameSpec/Village/Village-Resource-Coupling-GameSpec-V0.1.md` 确认；Progression 层抽象 Supply 仍不冻结具体内容、每 Node 固定消耗与未来其它映射）；
- 正式 elapsed-days 换算公式；
- 商店、招募和事件的正式内容（若存在，作为某个 Node Content / Situation 中的服务或机会，不要求成为固定地图节点类别）；
- 自愿返程、强制返程、全队失败的具体损失；
- 金币、材料、经验、领悟和卡牌奖励的全部正式数值；
- 出征后中途加入人物时的 Expedition Party 人数上限、满员后的替换 / 拒绝 / 暂存处理，以及是否受另一套数量限制；
- Supply / Carry 的 Party Size 计数定义（Hero 是否计入）；
- 人物经历的正式数据结构与玩法效果。

> FP-V0.1 相关可执行规则见 [GameSpec/Expedition/Expedition-Progression-GameSpec-V0.1.md](../GameSpec/Expedition/Expedition-Progression-GameSpec-V0.1.md)。
