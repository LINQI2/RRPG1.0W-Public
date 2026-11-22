# RRPG Functional Prototype V0.1 — Concept Checkpoint

> 日期：2026-09-07  
> 类型：Design Checkpoint  
> 建议简称：FP-V0.1  
> 状态：Concept / Design Closure Candidate  
> 作用：定义 RRPG 第一版可持续扩展的完整功能原型目标、完成 Gate、两次远征职责、前期 Village 节奏、Expedition Node 语义与当前 Supporting Candidate。  
> 注意：本文件不是 Current GameSpec，不代表对应内容已实现；需按 `Checkpoint → Current Design → GameSpec → Development` 生命周期归位。

---

## 1. 命名说明

仓库中已存在历史内部开发里程碑：

- Playable Milestone A
- Playable Milestone B
- Playable Milestone C
- Playable Milestone D

因此本轮不再继续使用“Milestone A”作为正式名称，避免与历史 Battle 内部开发里程碑混淆。

本轮统一使用：

> **RRPG Functional Prototype V0.1**

简称：

> **FP-V0.1**

其定义为：

> **RRPG 第一个可持续扩展的完整功能原型。**

它不是最终游戏，不等于 Steam Demo，也不等于第一章完成。

---

## 2. FP-V0.1 的开发意义

FP-V0.1 的目标不是继续增加零散 Foundation，而是第一次把现有核心系统装成完整可玩骨架：

> New Game  
> → Battle  
> → Run / Expedition  
> → Return  
> → Home / Village  
> → 再次 Prep  
> → 再次 Expedition  
> → Return  
> → Save / Load 后仍可继续

完成后，项目应从“持续造骨架”逐步转入：

- 内容扩展；
- 表现扩展；
- 体验优化；
- 第一章正式内容；
- Experience Vertical Slice。

---

## 3. FP-V0.1 Completion Gates

### G1｜Battle Core
必须证明玩家能够理解并完成当前核心 Battle：
- 基础出牌；
- Connection；
- Battle Reward；
- 新牌在后续 Battle 中实际使用；
- 不依赖 Debug 入口。

### G2｜Expedition Core
必须证明 Expedition 不只是连续 Battle：
- 正式组队；
- 携带当前远征所需 Supply；
- 推进产生 Time + Supply Cost；
- 统一 Node Progression；
- 多后继才出现 Branch Choice；
- Return / Retreat 为 Expedition 层独立退出；
- Safe Return / Retreat / Failure 可区分。

### G3｜Person Continuity
- Home 选中的 Person 进入 Expedition；
- Battle / Run 使用同一 PersonId；
- Return 后仍是同一 Person；
- 长期状态、成长与 Return 结果连续；
- 不创建第二套长期人物真相源。

### G4｜Home Exists
- 存在真实 Village World；
- 能识别具体 Person；
- 能识别基础 Place；
- Person 有最低世界行为表现；
- 玩家能通过 World 理解人物与职责；
- Home 不只是结算背景。

### G5｜Battle ↔ Village Coupling
- Expedition 能读取 Village 的具体 Person；
- Expedition 能从 Village 取得长期资源 / Supply；
- Expedition 会产生时间、人物状态、收益或其它长期变量；
- Return 能把这些结果带回 Village；
- Village 能承接这些变量。
- 不要求为了验收主动制造 Village 危机。

### G6｜Loop Sustainability
- 第一次 Return 后可以继续 Prep；
- 可以再次 Expedition；
- 第二次 Return 后仍可继续；
- 核心循环可重复；
- 不需要人工修改数据才能进入下一轮。

### G7｜End-to-End Product Path
最终至少应覆盖：

> New Game  
> → Tutorial Expedition  
> → Return Home  
> → Formal Expedition Prep  
> → Expedition  
> → Return  
> → Save / Load  
> → Continue

该 Gate 是最终产品集成验收，不等于“好玩验证”。

---

## 4. Onboarding 总体方向

### 4.1 Battle-first
当前强方向：

> **开局优先进入 Battle。**

不先展开完整 Village 教学。开场只提供最低限度情境和人物关系，让玩家尽快开始实际操作。

### 4.2 剧情与 Tutorial 交织
剧情碎片化穿插在多场 Battle 之间：
- 主角醒来 / 误入危险；
- Battle；
- 熟人找到主角；
- 对话介绍最低人物关系；
- 新 Person 加入；
- 下一场 Battle 引入新机制；
- 最后 Return Home。

原则：

> **剧情承担教程之间的自然节奏缓冲，同时逐步介绍人物与世界。**

---

## 5. 第一远征职责：Battle Tutorial Expedition

第一远征主要回答：

> **“这个游戏怎么打？”**

当前方向：
- Battle-first；
- 剧情碎片化；
- 人物逐步加入；
- 基础卡牌教学；
- Connection 教学；
- Reward；
- 新牌在后续 Battle 中实际使用；
- 最后 Return Home。

第一远征不承担完整 Expedition 风险管理教学。

---

## 6. 第一次 Home 职责

第一次 Home 主要回答：

> **“刚才和我一起战斗的人，原来都生活在这里。”**

当前目标：
- 看见真实 Village；
- 看见刚才同行的具体 Person；
- 看见他们在 Village 中的职责；
- 看见 Farm / Kitchen 等基础 Place；
- 让玩家理解这些人不是纯 Battle Unit。

前期不主动制造 Village 压力。

---

## 7. 前期 Village 节奏原则

当前确认方向：

> **Battle / Expedition 先展开，Village 慢构筑。**

第一章开局 Village 应是长期稳定、自洽、可以持续运转的老村庄。

不为了教学强行制造：
- Food 危机；
- 厨房停摆；
- 强制缺岗；
- 资源倒计时；
- 其它无玩家来源的开局危机。

核心原则：

> **Stable = Observe**  
> **Introduced Variable = Manage**

---

## 8. Battle / Expedition 作为变量注入器

当前强方向：

> **Battle / Expedition 是前期主要变量注入器，Village 是变量消化器。**

可能注入：
- 带走 Person；
- 带走 Food；
- 带回新人；
- 带回伤员；
- 带回新资源；
- 带回新发现；
- 带回人物状态；
- 带回外部事件线索；
- 带回新的社会关系或经历。

Village 不需要提前人为制造复杂度。

---

## 9. 第二远征职责：Expedition Tutorial

第二远征主要回答：

> **“怎么管理一趟远征？”**

当前方向：
- 正式组队；
- 查看同行 Person 的 Battle 价值；
- 查看同行 Person 的 Village 职责；
- 携带 Food / Supply；
- Expedition 推进；
- Node / Situation；
- Branch Choice；
- Time + Supply Cost；
- 风险累积；
- Return / Retreat；
- Safe Return / Retreat / Failure 的区别。

---

## 10. Formal Expedition Prep

Prep 核心同时回答：

> **带他出去，我在 Battle 得到什么？**  
> **带他出去，Village 少了什么？**

最小信息方向：
- Person 名字 / 身份；
- Battle 特点；
- Village Assignment / Responsibility；
- Availability；
- 离村后直接职责影响；
- 是否有替补；
- 当前 Village 资源趋势变化。

不做复杂未来 N 天精确预测。

推荐：

> **Current Stock + Expected Daily Delta**

例如：

> Food 42（+2/日）  
> 选择某 Person 后 → +1/日

---

## 11. Tutorial 与正式自由

正式玩法长期保持自由组队。

Tutorial 阶段优先通过：
- 剧情；
- 当前 Expedition 规模；
- 当前可用 Person；
- World Context；

自然缩小选择空间，而不是永久 UI 硬锁。

具体人数、具体同行 Person、Party Capacity 仍未冻结。

---

## 12. Expedition Node 语义

### 12.1 Node 定义
> **Node = 一次统一的 Expedition Progression Unit。**

Node 不等于 Battle。

Node 可以承载：
- Battle；
- Event；
- Story；
- Discovery；
- Rest；
- 其它未来内容。

### 12.2 Progression
- 线性推进为主；
- Node 支持 0 / 1 / N 个合法后继；
- `N = 1` 时不制造无意义选择；
- `N > 1` 时触发 Branch Choice；
- 分叉数量属于配置层。

### 12.3 Branch 与 Return 分离
> **Branch Choice 回答“接下来往哪走”。**  
> **Return / Retreat 回答“还要不要继续这次 Expedition”。**

Return 不作为普通 Node 后继。

### 12.4 Legacy 防误解
FP-V0.1 的 Node 语义：

> **不等于恢复 Legacy Adventure Map。**

禁止自动恢复：
- 旧树状点阵地图；
- 旧 Shop；
- 旧 Recruit；
- 旧 Rest；
- 旧节点生态；
- 旧路线结构。

---

## 13. Node 推进成本

当前确认：

> **Expedition Node 推进同时产生 Time Cost + Supply Cost。**

职责边界：
- Node 提供 / 产生本次推进的 Time / Supply 变化；
- Expedition Runtime 维护累计 elapsed time、Supply、Party State 与收益；
- Return 时累计时间接入 Village Return Catch-up；
- 不写死“每 Node = 1天 + 1 Food”。

具体耗时、Supply、Risk、Reward 属于配置层。

---

## 14. Retreat / Return

当前确认：

> Return / Retreat 是核心 Expedition 决策，不是失败按钮。

教学应建立：

> 主动撤退 = 风险管理 / 见好就收

未来区分：
- Safe Return；
- Retreat；
- Failure。

具体奖励保留比例、失败损失、伤病后果仍 Not Frozen。

---

## 15. Situation / Branch 信息透明度

不采用两个极端：
- 完全精确公式透明；
- 完全无信息随机猜测。

当前方向：

> 给玩家足够世界迹象和部分后果信息，但不把结果完全算死。

信息来源可以包括：
- World Clue；
- Party Interpretation；
- 极轻系统标签。

---

## 16. Person 对 Expedition 信息的潜在作用

Supporting Candidate：

> 同行 Person 可以在特定 Situation 中提供额外信息、解释或特殊选项。

暂不冻结正式侦察数值、固定职业技能或情报系统。

---

## 17. Home World 最低产品目标

FP-V0.1 的 Home 必须是真实 World，而不是纯菜单。

当前最低方向：
- 可浏览 Village World；
- 至少能识别具体 Person；
- 至少能识别基础 Place；
- Person 至少有最低 `Idle / Move / Work` 表现；
- Camera 支持最低浏览能力；
- World 不只是 UI 背景。

---

## 18. World Interaction

当前强方向：

> **World-first Selection + Shared Context Inspector**

核心：

> Selection ≠ Action

未来 List 可以作为快速索引，但不建立第二套独立管理逻辑。

---

## 19. Simulation 与 Presentation

当前 Confirmed Architecture 候选：

> **Simulation Authority ≠ World Presentation**

Person 是否完成工作不能由 Sprite 是否实际走到目标点决定。

---

## 20. 基础 Village 生存链

本轮形成新的 Strong Supporting Candidate：

> **Farm / Gathering → Ingredients → Kitchen → Food → Village Consumption / Expedition Carry**

---

## 21. Expedition Food / Supply

当前 Supporting Candidate：

> Expedition 不额外做 `Food → Expedition Ration` 二次转换。

而是：

> 从 Village Food 库存直接携带 Food。

系统层可继续使用 `Expedition Supply` 作为职责概念，但 FP-V0.1 当前内容候选只需要 Food。

---

## 22. Village Inventory

当前 Strong Supporting Candidate：

> Ingredients / Food 在 FP-V0.1 使用 Village Global Inventory。

即：

> Farm → Village Ingredients → Kitchen → Village Food → Village Consumption / Expedition Carry

FP-V0.1 不做局部库存、Warehouse 搬运权威或真实物流链。

---

## 23. Freeze 分类

### Confirmed Architecture 候选
- Battle-first onboarding；
- 第一远征主要教 Battle；
- 第二远征主要教 Expedition；
- 前期 Village 慢构筑；
- Stable = Observe；
- Introduced Variable = Manage；
- Battle / Expedition 是前期主要变量注入器；
- Village 是变量消化器；
- Node 是统一 Expedition Progression Unit；
- Branch 与 Return 分离；
- Node 推进产生 Time + Supply Cost；
- Person Continuity 保持同一实例；
- Home 是真实 World；
- Simulation Authority ≠ World Presentation；
- 不恢复 Legacy Adventure Map 作为 Current。

### Strong Supporting Candidate
- Farm / Gathering → Ingredients → Kitchen → Food；
- Ingredients / Food 使用 Village Global Inventory；
- Expedition 直接携带 Food；
- 2.5D Village；
- Person 最低 Idle / Move / Work；
- World-first Selection；
- Shared Context Inspector；
- 同行 Person 在 Situation 中提供额外信息；
- 第二次远征用剧情限制 + 少量自由位降低教学复杂度。

### Not Frozen
- 资源正式产消数值；
- 主厨 / 帮厨效率；
- Food Shortage 后果；
- 第二次远征具体 Party 人数；
- 具体剧情角色；
- 分叉数量；
- Node 正式内容池；
- Retreat 奖励保留；
- Failure 损失；
- Run HP → Village Health；
- Expedition 正式 Time / Food 公式；
- 正式剧情台词；
- Camera 具体形式；
- NavMesh / Waypoint；
- 最终 UI Layout；
- 最终视觉技术方案。

---

## 24. Deferred

FP-V0.1 不要求：
- 第二章 / 第三章；
- 完整 Tavern；
- 完整 Autonomous Life；
- 完整 Disease；
- 完整 Family；
- 完整 Relationship；
- 大量 Stable Identity 内容；
- 大量随机 Village Event；
- 完整 Expedition Shop / Recruit / Rest 生态；
- 多种 Ingredients；
- Recipe；
- Meal 品质；
- 真实物流；
- 完整 Policy；
- 完整 Society Simulation；
- 最终数值平衡；
- 最终美术品质。

---

## 25. 已否决 / 替代

### 已否决
- 开局强制 Village 危机；
- 为教学主动制造 Food 失衡；
- Battle 与 Village 从开场同时高复杂度展开；
- 每次 Node 都强制 Continue / Return；
- 把 Return 当普通 Branch；
- 自动恢复旧杀戮尖塔式点阵地图；
- 纯 UI 教学锁长期正式自由；
- 精确 N 天资源预测作为 Prototype 必需；
- 真实物流作为 FP-V0.1 Simulation Authority。

### 已替代
旧候选：

> Farm → Food

当前 Strong Supporting Candidate：

> Farm / Gathering → Ingredients → Kitchen → Food

---

## 26. 与 2026-09-06 Village Future Design Envelope 的关系

2026-09-06 的 `Village Future Design Envelope` 继续作为 Future Village 长期设计包络。

本文件不覆盖它，而是：

> 从 Future Village 包络中切出 FP-V0.1 需要的第一版产品骨架。

---

## 27. 后续归位要求

本 Checkpoint 不自动成为 Current Authority。

下一步：

1. 执行 FP-V0.1 Readiness Audit；
2. 对照真实仓库确认已有 / Foundation / Missing；
3. 决定哪些 Confirmed Architecture 候选升级 Current Design；
4. 对影响开发与验收的规则同步 GameSpec；
5. 再拆 Development Work Package；
6. 不允许开发 Agent 直接把 Supporting Candidate 当冻结规则。

---

## 28. Closure 声明

本轮 Design Closure 的核心结论：

> **FP-V0.1 不再继续扩展新玩法。**

当前目标从：

> “还能做什么？”

转换为：

> **“为了完成第一版可持续扩展的完整功能原型，真实仓库还缺什么？”**

下一阶段：

> **FP-V0.1 Readiness Audit → Current/GameSpec 归位 → Development WP**
