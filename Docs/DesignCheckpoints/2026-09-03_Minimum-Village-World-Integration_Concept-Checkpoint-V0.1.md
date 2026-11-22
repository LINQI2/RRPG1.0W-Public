# Minimum Village World Integration｜Concept Checkpoint V0.1

- **日期**：2026-09-03
- **项目**：RRPG
- **阶段**：Village V3 → Minimum Village World Integration → V4
- **状态**：Concept Near Closure
- **用途**：保存 Minimum Village World Integration 的阶段性设计事实，供后续系统策划专家审计、制作人裁决与 GameDesign / GameSpec 迁移使用
- **注意**：本文不是正式 GameSpec，不冻结具体地图尺寸、摄像机方案、Grid 尺寸、建筑 footprint、NPC 导航技术、Hero 控制方式、正式建筑数量、World Cue 资产数量或任何正式数值

---

## 1. 本阶段任务定义

Minimum Village World Integration 不是“给 Village 加一个好看的场景”。

它真正要解决的是：

> **如果把 Village World 删掉，只剩 Roster + Assignment + Project + Event / Log UI，RRPG 会失去什么？**

当前结论：

> **Village World 是具体人物、共同体、时间与历史的空间承载层，而不是 UI / 美术包装。**

它为 Person、Community、Assignment / Project、Social Response、Time / History、Departure / Return 提供一个可感知、可记忆、可重新进入的共同空间。

---

## 2. 核心职责

### 2.1 Person Presence
具体 Person 必须真实存在于世界中，而不只存在于 Roster。

World 至少让玩家看到：
- 这个人是否在村庄；
- 他大致在哪里；
- 他正在参与什么；
- Project / Expedition / Unavailable 时如何离开原生活位置；
- 返回后如何重新出现。

目标：
> **人物不只是列表项，而是反复在同一空间中被遇见的人。**

### 2.2 Place Memory
Village 必须让玩家形成：
> **“这是我的村庄。”**

玩家应能长期记住：
- 早期核心区域；
- 后来开发的土地；
- 稳定工作区；
- 重要 Project / 建筑留下的空间痕迹。

### 2.3 Spatial Anchoring
Assignment / Project 应与真实世界地点建立锚定。

当前强方向：
> **Semantic Binding + Visual Binding**
而不是：
> **Simulation Binding**

例如：
- Primary Assignment 指向具体 Workplace；
- 工作时 Person 出现在该 Workplace；
- Project 有 Project Site；
- Project 完成后世界产生可见变化。

但精确迟到、通勤距离等不直接决定生产数值。

### 2.4 Social Surface
V3 Social Response 需要自然世界承载面：
- Bark；
- 小规模聚集；
- 两人短暂停留 / 对话；
- 公共地点的社会信号；
- Recognition 的现场反馈。

但：
> **Social Signal ≠ Relationship Simulation**

### 2.5 Return Anchor
V4 的“归家”必须有一个真实空间对象。

目标：
> **我离开了这里，现在又回到了这里。**

---

## 3. 空间总体方向

当前强方向：
> **Small & Dense Village Core + Editable Grid + Expandable Territory**

不倾向：
- Darkest Dungeon 式完全固定功能 Hub；
- Banished 式完整随机大地图作为 Base 核心。

---

## 4. Editable Grid

Grid 的第一职责：
1. 建筑真的有位置；
2. 玩家能够塑造村庄形态；
3. Assignment / Project / Person 能引用真实空间；
4. 不同玩家的村庄长期可以长得不同。

因此：
> **Grid 是玩家塑造共同体空间的底层工具，不是空间优化题本身。**

当前不自动推出：
- 邻接 Buff；
- 道路效率；
- 通勤效率；
- 物流；
- 仓储链；
- 土地肥力；
- 复杂路径经济。

---

## 5. 空间规则深度

### 5.1 Spatial Expression
必须有，承担：
- Place Memory；
- World Anchor；
- 玩家规划感；
- 长期视觉历史。

### 5.2 Lightweight Spatial Rules
强方向，但必须直觉透明。未来可能包括：
- 有限服务范围；
- 特定空间条件；
- 少量必要连接 / 邻接；
- 地形形成自然边界。

当前不冻结具体规则。

### 5.3 Full Spatial Efficiency Simulation
明确后置：
- 精确通勤；
- 距离效率公式；
- 物流运输；
- 逐格资源搬运；
- 精确班次；
- 完整道路网络效率；
- 精细仓储链。

总原则：
> **Spatial Decisions Should Be Legible Before They Are Optimizable.**

---

## 6. NPC Life Simulation 边界

当前强方向：
> **Presence + Lightweight Routine + Sparse Social Signals**

而不是完整生活模拟。

### Presence
- NPC 在世界中真实存在；
- 可被点击 / 识别；
- Assignment / Project / Expedition 状态变化能反映在空间存在上。

### Lightweight Routine
表现状态来自现有 Runtime，例如：
- Working
- Idle / Free
- Participating in Project
- Away
- Unavailable

World 负责：
> **把已有系统状态演出来。**

### Sparse Social Signals
可以有：
- 路过短暂停留；
- 公共区域小聚集；
- 简短 Bark；
- V3 Recognition 的少量现场回应。

不建立完整：
- Needs；
- Personality；
- Relationship；
- Opinion；
- Social Need；
- 完整 Schedule。

### NPC Locomotion
当前强方向：
> **NPC Visual Locomotion**

但：
> **Pathfinding-as-Economy 不是当前目标。**

---

## 7. Semantically Constrained Wandering

不倾向纯随机闲逛。

NPC 移动应大致服从状态与语义：
- 工作时围绕 Workplace；
- Project 时围绕 Project Site；
- 空闲时优先公共 / 居住 / 合理区域；
- Away 时离开 Village；
- Social Response 时可去相关地点。

目标：
> **玩家大致能理解“为什么这个人在这里”。**

---

## 8. Home Anchor / Housing 边界

“人物住在哪里”是高潜力空间锚点，但：

> **Home Anchor ≠ Housing Economy**

当前不冻结：
- 每人床位；
- 房屋容量；
- 家庭共享；
- 居住满意度；
- 房租；
- 温度；
- 正式 Housing System。

---

## 9. Project 的世界表现

Project 应从“进度条”变成：
> **他们真的在这里做这件事。**

最低世界表现：
- 未开始：普通世界状态；
- 进行中：通用施工 / 活动表现；
- 完成：正式世界变化。

多个 Project 可复用施工材料、脚手架、工具、人物活动等。

原则：
> **High Semantic Change, Low Asset Count**

---

## 10. 玩家交互范式：Hybrid Interaction

当前最有潜力方向：
> **World / Hero 负责“存在”，UI 负责“治理”**

### World / Embodied Interaction
主要承担：
- 认识具体人；
- 观察村庄；
- Workplace / Project；
- Social Signal；
- Departure / Return；
- 现场事件；
- 空间历史与变化。

### Governance Interaction
UI 主要承担：
- Roster；
- Assignment；
- Project；
- Expedition Preparation；
- Log；
- 精确数值；
- 搜索 / 筛选 / 批量管理。

管理行为不强迫玩家物理走到对象旁边。

判断标准：
- 价值主要来自效率 → UI；
- 价值主要来自亲历 / 空间存在 / 情感 → World。

---

## 11. Hero / Avatar

当前高优先级产品假设：
> **玩家在 Village World 中的主要具身代理可以是 Hero。**

原因：
- 已有 Hero，无需额外创建无名 Avatar；
- Departure / Return 身份统一；
- NPC 可合理称其为勇者；
- Village 身体与 Expedition 领队自然连接。

### Hero 身份边界
原则：
> **Hero 是特殊 Person：共享有意义的 Person 规则，对与玩家具身职责冲突的规则保留例外。**

适合共享：
- Experience / Life Fact；
- Primary Assignment；
- Project Participation；
- Social Recognition；
- Expedition；
- 世界中的空间存在；
- 被村民观察 / 谈论。

可能未来特殊处理：
- Aging；
- Natural Death；
- 长期健康；
- Formation；
- Avatar 控制与 Occupancy 冲突。

### Hero 不老
可作为世界内部承认但暂不解释的事实。NPC 可以通过对白注意到 Hero 多年不老。

### Hero Assignment / Player Control
当前不要求物理完全同步。允许 Hero 有长期 Assignment / Project 事实，同时 Avatar 控制在表现层拥有一定自由。精确同步规则后置。

---

## 12. 地图生成范式

当前不倾向：
> 完整 Banished 式 Seeded Procedural World 作为 Base 核心。

当前强方向：
> **Authored Core + Free Grid + Progressive Expansion**

原因：
- 保留 Place Memory；
- 降低 PCG 公平 / 可建性 / 资源分布 / 寻路成本；
- 避免地图成为主要经营挑战；
- 更适合 Person / Community 核心。

---

## 13. Small & Dense Village Core

开局优先：
> **小而密的 Village Core**

优点：
- 容易认识具体人；
- Hero 移动不变成跑步劳动；
- Place Memory 强；
- 制作成本低；
- 更容易形成“家”。

---

## 14. Territory Expansion

当前最合理模型：
> **Expandable Boundary + Player-Initiated Development**

- Village 有明确物理边界；
- 当前可建设区有限；
- 外围可被开发；
- 何时扩张由玩家决定；
- 不依赖任意等级 Gate；
- 不要求剧情事件许可。

### 扩张压力来源
扩张本身不需要额外惩罚系统。自然压力来自：
- 资源；
- Person Time；
- Project Commitment；
- 新空间需要真实 Person 才有价值；
- 更大共同体自然增加维持需求。

### Expansion as Project
土地开发可成为 Project：
> **牺牲现在 → 改变未来共同体结构**

例如清理林地、整理新地块、开辟区域。

### Event 与扩张
Event 可改变特殊扩张机会，但不是常规扩张唯一许可。

### 地图最终边界
允许 Base Game 存在明确但可逐步开发的最大 Village Territory，用于保护 Person Recognizability、Place Memory、Avatar 距离、性能与制作量。

---

## 15. Controlled Procedural Variation

程序随机不被否决，但后置并受控。

未来可随机：
- 少量天然障碍；
- 林地位置；
- 边缘形状；
- 小型资源点；
- 外围区域特征。

原则：
> **先固定设计语义，再随机几何变化。**

---

## 16. Soft Zone

当前不做强 Zone System。

未来更适合：
> **Soft Zone**

即玩家布局自然形成空间聚类，系统 / UI 给予轻量识别、命名和导航。

职责优先是：
- Navigation；
- Memory；
- Information Compression。

例如 Return 时可说：
> “东部农作区灌溉扩建完成。”

而不是逐条报告十几个建筑变化。

当前不建立：
- Zone Efficiency；
- Zone Tax；
- Zone Mood；
- Zone Policy；
- Zone Leader；
- Zone Population Economy。

---

## 17. World State Visualization

Minimum Village World 的视觉语言是：
> **系统状态如何通过空间与视觉被玩家读懂。**

核心链：

```text
System Fact
→ World Cue
→ Player Perception
→ UI Explanation（需要时）
```

总原则：
> **世界先说话，UI 负责解释。**

World 负责：
- 第一层感知；
- 空间关系；
- 人物在场；
- 趋势；
- 长期变化；
- 历史痕迹；
- 重要社会信号；
- Departure / Return。

UI 负责：
- 精确数值；
- 比较；
- 搜索；
- 筛选；
- 规则；
- 长期详情；
- 批量管理；
- 复杂因果解释。

---

## 18. 优先世界化的高价值状态

### Structural Change
- 新建筑；
- Project 完成；
- Territory 扩张。

应强可见。

### Person Presence Change
- 出征；
- Project；
- 回归；
- 长期 Unavailable。

应可见。

### Major Community Condition
未来如存在明显短缺、庆典、危机，应有整体或局部世界表现。

### Social Signal
V3 的少量 Recognition / Community Signal：
- Bark；
- 聚集；
- 现场反应。

低频、高语义。

### History Trace
重要共同体历史可在空间中留下长期痕迹。

目标：
> **Progression = Visual Accumulation of History**

---

## 19. Visual State Communication 原则

> **World Visualization 只承担“高语义、低频率、可直觉理解”的状态。**

不要求把所有 Food、Project、Social、Person、Formation 数值都视觉化。

精确、频繁、数字化信息继续交给 UI。

---

## 20. 当前空间与交互骨架

```text
Minimum Village World
│
├─ Small & Dense Village Core
├─ Editable Grid
│   └─ 玩家塑造空间
├─ Expandable Territory
│   └─ 资源 + Person Time + Project 主动开发
├─ Visible Persons
│   ├─ NPC Locomotion
│   ├─ Lightweight Routine
│   └─ Sparse Social Signals
├─ Spatial Anchoring
│   ├─ Workplace
│   └─ Project Site
├─ Hybrid Interaction
│   ├─ Hero / World = Presence
│   └─ UI = Governance
├─ Soft Zone
│   └─ Navigation / Memory / Information Compression
└─ World State Visualization
    └─ 高语义状态优先通过世界表达
```

---

## 21. 明确不进入 Minimum World 第一版的内容

- 完整物流；
- 精确通勤效率；
- 逐格资源运输；
- 复杂道路经济；
- 完整 Needs；
- 完整 Schedule；
- Relationship AI；
- Personality Simulation；
- Mood / Opinion 系统；
- Zone Economy；
- Zone Policy；
- Zone Leader；
- 完整 Housing Economy；
- 完整 Seeded Procedural World；
- 大规模城市人口；
- 每个 Project 多阶段专属资产；
- 强制 Avatar 跑腿式治理。

---

## 22. 当前已确认 / 强方向

- Village World 是共同体空间，不是包装；
- Person Presence / Place Memory / Spatial Anchoring / Social Surface / Return Anchor 是核心职责；
- Grid 自由布局是强方向；
- 不采用纯固定 Hub 作为长期最终形态；
- NPC 有物理存在并移动是强方向；
- NPC 移动服务语义与存在感，不进入完整物流效率；
- Presence + Lightweight Routine + Sparse Social Signals 是 Minimum NPC Life 适合层级；
- Hybrid Interaction 是当前最有潜力的交互方向；
- World / Hero 负责存在，UI 负责治理；
- Hero 作为玩家具身代理是高优先级假设；
- Hero 不老可作为世界承认但暂不解释的事实；
- Village 从小而密核心开始；
- Territory 可由玩家主动扩张；
- Expansion 可通过 Project 表达；
- 扩张压力来自资源、Person Time 与真实使用需求，而非额外惩罚系统；
- Soft Zone 更适合导航 / 记忆 / 信息压缩，而非强经济系统；
- World Visualization 遵循“世界先说话，UI解释”；
- World 只承担高语义、低频率、直觉透明的状态；
- High Semantic Change, Low Asset Count。

---

## 23. 强候选但未冻结

- Hero 最终作为可自由控制 Avatar；
- Hero 的具体移动方式；
- Hero 与 Assignment / Project 的精确同步；
- Home Anchor；
- 少量服务范围 / 地形 / 必要邻接；
- Controlled Procedural Variation；
- Soft Zone 自动识别 / 玩家命名；
- Return 时通过世界变化完成 Reorientation；
- Village 长期视觉历史痕迹。

---

## 24. 明确未冻结

- Grid 单格尺寸；
- 地图总尺寸；
- Village Core 大小；
- 最大 Territory；
- 建筑 footprint；
- 摄像机方案；
- Hero 控制方式；
- NPC 导航技术；
- NPC 动画数量；
- 正式 Workplace 数量；
- 正式建筑数量；
- 正式 Project Site 表现；
- Territory 扩张成本 / 耗时；
- 是否存在道路；
- 是否存在服务范围；
- 是否存在正式邻接规则；
- 是否存在正式 Zone；
- World Cue 资产数量；
- 正式美术风格；
- 正式昼夜 / 天气；
- 正式 Housing；
- 正式随机地图参数。

---

## 25. 当前禁止误读

不得从本文推导：
- Minimum World = RimWorld 式生活模拟；
- 有 Grid 就必须有道路 / 物流 / 邻接 Buff；
- NPC 会走路就必须精确模拟通勤；
- Hero 必须和普通 Person 全盘一致；
- Hero 被 Assignment 后玩家不能自由移动；
- 所有 Social Response 都必须现场动画；
- 所有状态都必须视觉化；
- 扩张必须由剧情 / 等级 Gate；
- 程序地图生成是 Base 必需；
- Zone 必须拥有独立经济规则；
- Village 必须发展成巨大城市。

---

## 26. 产品验收方向

未来 Minimum Village World Integration 不应主要验：
> 是否有一个场景。

而应问：

### Person Recognition
> 我能不能在世界里认出具体 Person，而不只在 Roster 里认识他？

### Place Memory
> 我能不能形成“这个村庄是我的、我知道哪里是什么”的空间记忆？

### Spatial Legibility
> Assignment / Project 是否真的看起来发生在具体地点？

### Social Surface
> V3 Social Response 是否有自然世界表达，而不是全靠弹窗？

### World State
> 重要结构变化是否能在不打开 UI 的情况下被感知？

### Return Anchor
> 离开又回来以后，我是否真的感觉回到了同一个持续变化的地方？

### Interaction Friction
> Hero / Avatar 的存在有没有增强“我在这里”，同时没有把管理变成跑腿？

最终核心：
> **这个 Village 是否已经从“管理后台”变成“一个我认识、塑造、离开、又会想回来的地方”？**

---

## 27. 当前阶段状态

> **Minimum Village World Integration｜Concept Near Closure**

概念层已经解决：
- 为什么必须有 Village World；
- World 的不可替代职责；
- Grid 是否需要；
- 空间是否成为玩法；
- 空间规则应做到多深；
- NPC 要“活到什么程度”；
- NPC 是否需要移动；
- Assignment / Project 如何空间锚定；
- Hero / Avatar 与 UI 如何分工；
- Hero 与 Person 的关系边界；
- 地图生成范式；
- Territory 扩张方式；
- 扩张压力来源；
- Zone 是否需要；
- World State Visualization 的视觉语言；
- 单人开发资产成本边界。

继续往下讨论地图尺寸、Grid 参数、建筑数量、NPC 导航、摄像机、Avatar 键位、扩张成本、正式道路 / 邻接、具体 World Cue，已经进入 GameSpec / implementation / content / art production 层，应后置。

---

## 28. 推荐后续流程

本文当前只作为阶段性 Design Checkpoint。

建议：

```text
V2 Repository Closed
  ↓
将 V3 / Minimum World / V4 / Base Completeness 等 Checkpoint 放入 Docs/DesignCheckpoints
  ↓
RRPG 游戏系统策划专家整组审计
  ↓
制作人裁决
  ↓
Codex 最小迁移到 Current GameDesign / 必要 GameSpec
  ↓
V3 开发
  ↓
Minimum Village World Integration 规格与开发
  ↓
V4 开发
  ↓
Village Playable Milestone A
```

本文不替代：
- `GameDesign/当前设计状态.md`
- 正式 `GameDesign`
- `GameSpec`
- UI / 美术规范
- 正式开发执行规则
