# Worker｜劳工 / 帮工 当前设计

**内容基线：2026-09-16；导航修订：2026-09-17**  
**Freeze Level：Concept / Not Fully Frozen**  
**阶段标签：第一产品阶段角色内容设计已收口 / 待 Prototype**  
**内容置信度：本文件记录当前强候选与候选，不构成 Current Rule**

> 可执行 Profession / Starter / Trait / Full Card Pool / Battle 规则统一服从 [GameSpec/Person/](../../../GameSpec/Person/README.md) 与 [GameSpec/Battle/](../../../GameSpec/Battle/README.md)。具体卡牌配置统一查看 [../Identity角色内容设计工作台.xlsx](../Identity角色内容设计工作台.xlsx)。

## 1. 身份锚点（Identity Anchor）

Worker 来自村庄中大量跨场所、低专业门槛但必须有人承担的公共劳动：搬运、整理、送货、临时补位、Project 缺人时顶上。它不绑定一个固定 Workplace，而是共同体里的“连接人”。

## 2. 战斗身份（Battle Identity）

> **让共同作业不断档，通过主动重构关系补上结构缺口。**

Worker 不是传统 Aura Support。玩家真正反复判断的是：

- 哪里出现了结构断档；
- 我能否移动 / 换位让关系重新成立；
- 当前空间关系改变后，哪些 Card / Trait 会被重新估值；
- 后期，前一个人的行动结束后怎样让共同作业继续。

## 3. 母题 / 子题候选地图

### 补位
**子题：空间补缺 / 关系重构**  
玩家问题：哪里出现空间断档，我如何通过移动 / 换位让关系重新成立？

### 接力
**子题：行动承接**  
玩家问题：前一个人的行动结束后，怎样让共同作业继续而不断档？

“接力”当前仍属于后续候选；Previous Owner、Acted This Turn 等 History Fact 不应在初期高密度出现。

## 4. 起始牌 / Trait / 当前内容候选

- **Starter 强候选：**《搭把手》
- **Trait：**开放
- **第一批身份特色 候选：**《补位》《换岗》

Starter 的职责是保证单 Person / 单 Card 时仍能完成最低战斗闭环，并只露出一层“协作增值”；Worker 的核心移动 / 重构体验在多人环境成熟后再展开。

具体 AP 与《搭把手》邻接额外收益在工作台记录为候选；移动合法性服从 Battle GameSpec，原型验证具体候选是否满足规则。

## 5. 构筑接口

Worker 当前最重要的构筑接口：

- Position；
- Adjacency；
- 独狼；
- 群羊；
- 其它依赖空间关系的 Card / Trait。

它的价值不是自己叠高数值，而是作为**结构编辑器**改变整个 Shared Deck 的条件成立情况。

## 6. 构筑种子与体验候选

### 结构杠杆 / 空间重构（涌现 Seed）

体验骨架：

> 移动一个 Person → Position / Adjacency / 独狼 / 群羊同时改变 → 多张 Card / Trait 重新估值 → 原本卡住的行动线突然被盘活

Worker 的爽感承载主体可以是整个系统，而不是 Worker 自身数值。

关键成型感：

> “我现在每动一次位置，手里好几张牌都会一起变好用。”

### 创造机会 → 捕获机会（与 Hunter 的涌现 Seed）

Worker 可以主动改变空间 Fact，制造 Hunter 原本需要等待的机会；Hunter 再负责是否捕获。

该 Seed 不属于任何单一 Identity 的固定流派。

## 7. 内容阶段原则

建议认知梯度：

1. **基本行动：**Starter 能独立推进战斗；
2. **人与人协作：**邻接等当前可见关系；
3. **空间重构：**补位 / 换岗，开始主动改变关系；
4. **行动接力：**Previous Owner / Acted This Turn 等极短 History。

Worker 特别受“环境成熟度”影响：如果首阶段还没有多人 / 空间关系价值，就不应强行提前发移动牌。

## 8. 组合观察

Worker 对外接口广，但不应因此成为所有空间 Build 的必带钥匙。

- × Farmer：维护 / 重构空间成长条件；
- × Hunter：创造机会 → 捕获机会；
- × Cook：空间结构整理 × Hand / 资源结构整理；
- × Herbalist：位置变化可能重新分配身体风险与处理目标。

组合必须保持各 Identity 独立成立。

## 9. 原型验证 / 开放问题

1. **最关键环境 Gate：**整个 Battle 的 Position / Adjacency / 独狼 / 群羊引用密度是否足够？
2. 如果空间引用太少，应优先修正 Battle 内容环境，而不是继续给 Worker 塞普通 Buff。
3. 《搭把手》邻接时的轻量协作收益具体是什么；不能退化成高额条件攻击。
4. 《补位》《换岗》的 AP 与合法移动细节需要和正式 Battle Rule 对齐。
5. 接力类 History 是否值得显式系统承载，必须等更多独立强内容共同需要。

## 10. 当前边界

- Current：补位 / 接力两母题方向；Starter《搭把手》；移动 / 换岗作为第一批身份特色；结构杠杆体验。
- 开放：Trait、接力实现、Starter协作收益、移动经济。
- 禁止误解：Worker ≠ 普通邻接 Buff 职业；移动本身不是爽点，**移动造成的系统重新估值**才是核心体验。


## 使用与维护入口

[专题首页](../README.md) · [设计流程](../README.md#日常操作流程) · [零件目录](../卡牌与Trait设计工具箱.md#零件查询目录) · [内容工作台](../Identity角色内容设计工作台.xlsx)

工作台按本角色英文ID筛选。效果、费用、阶段和问题详情以对应表为主；本文只维护身份语义与设计边界。正文的 Current 指当前设计候选，不代表已冻结规则。
