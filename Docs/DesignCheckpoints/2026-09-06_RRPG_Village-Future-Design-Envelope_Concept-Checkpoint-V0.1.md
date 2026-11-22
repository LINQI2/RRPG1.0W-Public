# RRPG Village Future Design Envelope — Concept Checkpoint V0.1

> 日期：2026-09-06  
> 类型：Design Checkpoint  
> Freeze Level：Concept / Not Fully Frozen（混合包含若干 Confirmed Architecture 候选与 Supporting Candidate）  
> 作用：保存本轮 Future Village 设计包络、当前采用方向、候选方案、后置内容与 Milestone A 最小实现切片。  
> 注意：本文件不是 Current GameSpec，也不是实现完成声明；后续需按 `Checkpoint → Current Design → GameSpec → Development` 生命周期归位。

---

## 1. 本轮设计问题

本轮最初从 **Stable Identity 内容模板**开始。

讨论过程中发现：

> 身份内容不能脱离 Person 日常生活、场所、工作、自主活动和共同体结构来设计。

因此向上追溯并补齐了：

> Village 日常生活  
> → Person 行为  
> → Place / Activity  
> → Fact / Opportunity  
> → Stable Identity  
> → World Presentation

同时明确了 Milestone A 第一次可玩 Home 所需的最小子集。

---

## 2. 上游目标

Future Village 不是：

- Battle 的升级菜单；
- 一组静态管理面板；
- 纯粹的 Worker 调度器。

而应成为：

> **具体 Person 长期生活、承担责任、产生经历、形成身份，并让玩家产生“这里是家”感受的长期世界。**

Battle 与 Village 使用同一批具体 Person。

玩家离开 Village，不意味着 Village 在世界观上被冻结。

---

## 3. Person

### 3.1 当前方向

Person 首先是具体的人，而不是 Worker Template。

长期生活可以形成：

> Assignment / Activity  
> → Fact / Life History  
> → Stable Identity  
> → Battle 内容表达

Identity 不能反过来完全定义 Person 的性格。

### 3.2 内容原则

Stable Identity 提供：

- 共同生活经验；
- 长期社会位置；
- 可持续内容母题。

Person 个体差异仍主要来自：

- 具体经历；
- Life Fact；
- Event；
- 关系；
- 长期选择。

---

## 4. Village Control Model

Village 当前采用三层控制概念：

### 4.1 Direct Control

玩家直接决定责任与重大承诺。

例如：

- Assignment；
- Expedition；
- Project；
- 重大建设或公共资源决定。

### 4.2 Soft Influence

玩家通过制度、规则、优先级与环境影响 Person 行为。

例如未来可能包括：

- 工时；
- 分配规则；
- 场所开放；
- 公共优先级；
- 政策。

### 4.3 Autonomous Life

Person 自主决定大量日常生活与非关键行为。

核心原则：

> **玩家控制责任，不控制每一分钟。**

---

## 5. Autonomous Life

### 5.1 决策方式

不采用每帧全员持续复杂决策。

未来优先采用低频、离散触发：

- Activity 完成；
- 时间块变化；
- 状态变化；
- 目标失效；
- Opportunity 出现；
- 玩家规则改变；
- 世界重要变化。

大多数时间 Person 只执行已经决定好的 Activity。

### 5.2 当前未冻结

以下仍未冻结：

- 正式评分算法；
- 权重体系；
- Personality 对行为的影响尺度；
- Activity Block 时长；
- 具体优先级规则。

---

## 6. Activity Architecture

当前 Activity 概念暂分四类：

### 6.1 Commitment Activity

例如：

- FarmWork；
- ProjectWork；
- 值守；
- 远征准备。

### 6.2 Need / Life Activity

例如：

- Rest；
- Eat；
- Drink；
- 回家；
- 散步。

### 6.3 Social Activity

例如：

- Talk；
- 一起饮酒；
- 探望；
- 聚集。

### 6.4 Opportunity Response

例如：

- Help；
- 调解；
- 照顾；
- 响应公共事件。

### 6.5 Activity 最小设计语言

Activity 至少需要概念上表达：

- 谁参与；
- 在哪里；
- 做什么；
- 是否可执行；
- 需要多少参与者；
- 何时完成；
- 完成后产生什么 Fact / Effect。

具体程序接口、字段与数据结构 **Not Frozen**。

---

## 7. Place Architecture

### 7.1 定义方向

> **Place = 能承载 Person Activity、规则和世界状态的生活场域。**

Place 不等于 Building。

例如：

- Farm；
- Tavern；
- Kitchen；
- Home；
- Forest；
- Village Square；
- Project Site。

### 7.2 Building / Plot 与 Place 分离

- Building / Plot：物理空间、占地、建造对象；
- Place：玩法意义上的活动场域。

允许未来出现：

> 一个 Building 包含多个 Place。

例如：

- 公共建筑
  - Kitchen Place
  - Dining Place

### 7.3 Place 当前应概念上支持

- Activity Offering；
- Capacity；
- Availability；
- Activity Point；
- State；
- Result / Effect Hook。

### 7.4 约束

Place 不主动控制 Person。

原则：

> **Place 提供可能性，Person / Commitment 决定是否参与。**

---

## 8. Simulation Authority 与 World Presentation

这是本轮最重要的架构边界之一：

> **Simulation Authority ≠ World Presentation。**

例如 Farmer 当天是否产生 Work Fact / Food：

不能依赖：

> Sprite 是否真的走到了农田。

Simulation 根据：

- Assignment；
- Availability；
- Occupancy；
- Time；

决定真实结果。

World Scene 负责表现：

- Person 走到农田；
- 工作；
- 休息；
- 回家；
- 社交。

因此即使：

- Scene 没加载；
- 玩家正在 Battle；
- NPC 视觉寻路异常；

长期 Simulation 也不能因此停止。

该原则与 V4 Return Catch-up 方向一致。

---

## 9. World / Grid / Navigation

### 9.1 视觉目标

> **真实存在的 Village World，而不是 Town Menu。**

当前强候选：

> **2.5D Village**

但具体以下内容仍待视觉原型验证：

- 正交 / 透视；
- Camera angle；
- Sprite 方向数；
- 建筑纯 2D / 低模 3D；
- 遮挡与排序方式。

### 9.2 空间三层

#### World Space

玩家真正看到的村庄空间：

- 地面；
- 建筑；
- 农田；
- 道路；
- Person；
- 自然环境；
- 装饰物。

#### Placement Grid

负责：

- 建筑占地；
- 放置；
- 规则空间；
- 建造判断。

Grid 平时无需常驻显示。

#### Movement Space

Person 自然移动，不要求逐格行走。

当前候选：

- NavMesh；
- Waypoint / Path Graph；
- 其它轻量导航。

### 9.3 当前架构约束

> **Placement Grid 与 Person Movement 必须解耦。**

Person 不需要知道 Farm 占几格，只需要获得可到达的 Activity / Work Point。

---

## 10. Village 主要交互

### 10.1 Primary Interaction

Village 高频操作采用：

> **模拟经营式 Camera 浏览与 World Selection。**

包括：

- Pan；
- Zoom；
- 点击 Person；
- 点击 Place；
- 打开上下文信息；
- 下达管理操作。

### 10.2 Hero Free Walk

可以存在：

> Hero 场景自由行走 / 漫游模式。

但定位为：

> **沉浸体验层**

而不是主要管理方式。

核心管理功能不得依赖：

> Hero 必须走到某个建筑或 Person 附近才能操作。

---

## 11. World-first Interaction

当前强方向：

> **World-first Selection + Shared Context Inspector**

基本语言：

```text
World Object
→ Select
→ Shared Context Inspector
→ 查看
→ 玩家再决定是否 Action
```

核心原则：

> **Selection ≠ Action**

Person、Place、Project 不各自建立完全独立的交互体系。

未来 List 可以存在，但只作为：

> 快速定位 / 索引入口。

最终仍汇入：

> Selection → Context Inspector。

---

## 12. UI 信息层级

当前建议四层：

### L0｜World HUD

常驻、低信息密度。

例如：

- 日期；
- 时间；
- 少量全局资源 / 状态。

### L1｜Context Inspector

当前选中 Person / Place 的信息与操作入口。

### L2｜Light Overlay / Popover

轻量选择、小机会、小确认。

例如：

- Assignment 选择；
- 小型 Opportunity；
- 简单确认。

### L3｜Full Modal / Management Screen

复杂系统界面。

例如未来：

- 建造；
- 政策；
- 大型 Event；
- Expedition Prep；
- 完整人物履历。

Milestone A 只需要：

> **L0 + L1 + 极少 L2。**

---

## 13. World 信息语言

当前形成三层分工：

> **World 表现“正在发生什么”。**

> **Selection 表现“这个对象和谁 / 什么有关”。**

> **Inspector 解释“为什么、具体情况、如何操作”。**

例如：

- Person 在农田工作，应从世界表现直接可读；
- 点击 Person 后再查看 Assignment；
- 点击 Farm 后相关 Worker 可轻量高亮；
- 复杂关系线不常驻。

---

## 14. Person Context Inspector

Person 被选中后，信息优先级建议为：

### 第一层：即时状态

- 名字；
- 基础身份；
- 当前行为；
- 当前所在位置；
- Assignment；
- Availability / Occupancy。

### 第二层：长期社会位置

- Stable Identity / Profession；
- 长期职责；
- 重要成长状态；
- 席位型身份。

### 第三层：人生与关系

未来可展开：

- Life History；
- 重要经历；
- 关系；
- 家庭；
- 重大事件痕迹。

### 第四层：操作

例如：

- 调整 Assignment；
- 查看 Workplace；
- 远征相关；
- 查看 Battle 内容。

原则：

> **当前处境优先于数值属性。**

---

## 15. Place Context Inspector

Place 被选中后，信息优先级建议为：

### 第一层：当前状态

- 场所名称；
- 当前功能；
- 是否工作中；
- 当前 Person 数；
- Capacity；
- 关键状态提示。

### 第二层：职责与参与者

- Assignment Slots；
- 当前分配 Person；
- 当前基础产出；
- 当前缺口。

### 第三层：长期信息

未来可包括：

- 扩建；
- 历史；
- 长期效率；
- 特殊设施状态。

### 第四层：操作

例如：

- 添加 / 移除 Assignment；
- 查看相关 Person；
- 聚焦相关对象。

Person 与 Place 应支持双向跳转。

---

## 16. Fact / Opportunity / Event

### 16.1 定义

> **Fact = 已经发生的事实。**

> **Opportunity = 当前存在一个玩家或 Person 可以介入的窗口。**

不是所有 Fact 都升级为 Opportunity。

### 16.2 建议链路

```text
Simulation / Activity
→ Fact
→ Rule 判断
→ Opportunity
→ Response / Ignore / Expire
→ 新 Fact / State
```

### 16.3 UI 打断强度

事件重要性决定 UI 打断强度：

- 小事 → World Cue；
- 中等 → Light Popover；
- 重大决策 → Modal。

核心原则：

> **社会模拟持续产生 Fact，只有少量值得介入的 Fact 转成 Opportunity。**

避免 Village 变成弹窗游戏。

---

## 17. 第一章 Stable Identity 内容模板

当前策划模板：

1. 身份名称；
2. 世界存在理由；
3. 主要形成经历；
4. 身份特有机会 / 特殊响应；
5. 三关键词概念母题。

当前用于模板验证的第一章候选样例：

- Farmer；
- Cook；
- Hunter；
- Laborer；
- Carpenter；
- Herbalist；
- Drunkard；
- Tavern Owner；
- Village Chief。

这些目前主要承担：

> **模板验证与第一章候选池**

不代表九个全部已经冻结为 Milestone A 正式内容。

第二、第三章 Identity 内容主动 Deferred。

---

## 18. Future Village 内容方向

当前讨论过、需要保留的未来内容包括：

- 农田；
- 森林；
- 仓储；
- 公共厨房 / 用餐；
- 住宅；
- 酒馆；
- 公共空间；
- 基础疾病 / Health；
- 草药；
- 工作制；
- Autonomous Life；
- 社交；
- Help / Opportunity；
- Project；
- Identity Formation；
- 席位 / 继承型身份。

这些都**没有因为 Milestone A 缩小而被取消**。

---

## 19. 第一章世界基础方向

当前第一章世界概念倾向：

> 一个已经长期存在并维持多年微妙平衡的小村庄。

玩家不是从零建村。

开局已有成熟社会结构与具体 Person。

玩家通过：

- 带人远征；
- 带回新人；
- 带回资源；
- 带回发现；
- 建设选择；
- 抽调原有劳动力；

逐步向旧平衡注入变量。

后续第二章 / 第三章的内部秩序与共同体边界，只保留为顶层方向，本 Checkpoint 不继续细化。

---

## 20. Milestone A 定位

Milestone A 当前定义为：

> **第一次完整可玩的 Functional Vertical Slice。**

不等同于：

> 核心乐趣已经被验证。

真正体验验证需要后续更完整 Demo，包括：

- Battle；
- Village；
- UI；
- 美术；
- 场景；
- 摄像机；
- Person 表现；
- 文案；
- 节奏。

---

## 21. Milestone A 玩家流程

当前建议流程：

> Battle Tutorial  
> → Connection  
> → Card Reward  
> → 后续 Battle 使用新牌  
> → Return  
> → Home  
> → 看见同一 Person  
> → 看见最小长期状态  
> → 做一次最小 Village 决策。

首次远征当前建议：

> **Hero + 1 Person**

正式队伍上限仍保留更大规模。

首次战斗结构当前建议：

> Battle 1：基础卡牌  
> → Reward  
> → Battle 2：Connection  
> → Reward  
> → Battle 3：首次自主组合  
> → Return

复杂地图、商店、营地、随机路线等不属于 Milestone A 必需内容。

---

## 22. Milestone A 最小 Home

当前建议最小 Home：

- 一个可见的小 Village Scene；
- 少量具体 Person；
- 一个 Farm；
- 一个最小 Assignment 入口；
- 一个 Food 长期状态；
- Return 时间反馈；
- Person continuity。

Village 场景目标：

> 真实可见、可浏览，不是纯菜单。

Milestone A 不要求完整 Hero 漫游，但未来保留该沉浸层。

---

## 23. Milestone A 最小 Simulation Consequence

当前优先使用确定性的 Simulation Consequence，而不是随机 Event。

示例：

```text
出征前：
Farm：张三 + 李四
Food：20

Hero 带张三离村 3 天

Away：
张三因 Expedition Occupancy 不执行 Farm Assignment
李四正常工作
在村人口继续消耗 Food

Return：
Food 发生可解释变化
张三重新回村
Farm 恢复可用
```

该最小闭环可以同时验证：

- 时间连续；
- Occupancy；
- Assignment；
- Food 长期状态；
- Person continuity；
- Return；
- 双系统机会成本。

---

## 24. Milestone A 最小 Food 模型

当前只服务纵切验证：

> 每个在村 Person 每天消耗固定 Food。

> 每个 Farm 有效 Worker 每天产生固定 Food。

概念式：

```text
Daily Food Delta
= Farm 有效 Worker 产出
- 在村人口消耗
```

Milestone A 中 Away Person 暂不消耗 Village 每日 Food。

该口径只服务最小纵切，不代表最终远征口粮规则已冻结。

Milestone A 暂不要求：

- Kitchen；
- Meal；
- 营养；
- 饥饿层级；
- 完整仓储玩法。

---

## 25. Milestone A 最小 World / UI

最低目标：

- 2.5D 小村庄方向；
- 可平移 / 缩放 Camera；
- Person 可见；
- Person 能去 Farm WorkPoint；
- Farm 可选中；
- Person 可选中；
- Shared Context Inspector；
- 顶部最小 HUD；
- 少量轻量 Overlay。

Milestone A 不要求：

- 完整自主生活；
- 酒馆；
- 厨房；
- 疾病；
- 完整 Opportunity；
- 完整社会关系；
- 完整 Identity 内容；
- 大规模 NPC AI。

---

## 26. 当前 Freeze 分类建议

### 26.1 Confirmed Architecture 候选

建议后续归位 Current Design：

- Village 是具体 Person 长期生活的 Home，不是 Battle 菜单；
- Battle / Village 使用同一具体 Person；
- Village 长期 Simulation 与 World Presentation 分离；
- Player 主要管理 Responsibility，而不是逐分钟控制 Person；
- Milestone A 是 Future Village 的最小 Slice，不代表完整 Village 被砍掉；
- World Scene 是正式产品体验的一部分，而不是装饰背景；
- Placement Grid 与 Person Movement 必须解耦；
- Selection 与 Action 分离。

### 26.2 Supporting Candidate

当前较强候选：

- 2.5D；
- Placement Grid + 自然 Movement；
- NavMesh / Waypoint；
- Place / Activity 统一语言；
- 四层 UI；
- World-first Selection + Shared Inspector 的具体承载方式；
- Autonomous Life 低频决策；
- Hero Free Walk 作为可选沉浸层。

### 26.3 Concept / Not Fully Frozen

继续保留讨论空间：

- 正式日程算法；
- Food → Kitchen → Meal → Person 完整链；
- 酒馆具体玩法；
- 疾病深度；
- Activity 正式分类与字段；
- Place 正式数据结构；
- 九个 Identity 最终正式池；
- Hero 漫游具体玩法；
- 最终 Camera / 视觉技术方案；
- 正式 Food / Expedition 口粮关系；
- 正式远征耗时公式。

### 26.4 Deferred

当前主动后置：

- 第二章身份池；
- 第二章制度；
- 第三章边界内容；
- 派系；
- 完整医疗；
- 完整家庭系统；
- 完整社会关系系统；
- 大规模 NPC AI；
- 完整 Kitchen / Tavern 内容实现；
- 完整 Policy 系统。

---

## 27. Rejected / Replaced

本轮明确不采用或不作为当前方向：

- 用纯 Town Menu 代替未来 Village World；
- 把 Person Movement 写死成逐格移动；
- 用 Assignment 直接驱动完整导航 / 表现 / 生产所有职责；
- 让 Farm / Tavern / Kitchen 各自主动“抢 Person”；
- 让 World Presentation 成为 Simulation 是否发生的唯一条件；
- 每帧对全部 Person 做复杂自主决策；
- 所有 Fact 都直接升级为玩家弹窗；
- Milestone A 为了最小化而取消 Future Village 的长期内容；
- 继续提前细化第二章、第三章正式内容。

---

## 28. 开发架构约束

后续开发在实现 Milestone A 时，应避免以下写死：

- Person 行为只支持 Farm；
- Person 只能由 Assignment 驱动；
- Building 与 Place 永久一一绑定；
- Grid 与导航强绑定；
- World Scene 激活状态决定 Simulation 是否推进；
- Person 必须走到目标点才产生 Simulation 结果；
- 所有管理操作必须通过 Hero 近距离触发；
- 每种 Place 各建一套完全独立 Selection / Inspector 系统；
- Activity / Fact / Opportunity 被混成同一个事件系统。

Milestone A 应遵循 YAGNI：

> 只实现 Farm 最小子集，但不能破坏 Future Village 的扩展方向。

---

## 29. 本轮尚未解决的问题

当前未决主要包括：

- 最终 2.5D 视觉技术路线；
- Camera 形式；
- Person Sprite 方向与动画规格；
- NavMesh 与 Waypoint 的正式选择；
- Place / Activity 是否需要显式通用 Runtime Framework；
- Milestone A Farm 的正式产品表现；
- Battle Tutorial 产品流程与现有实现之间的差距；
- Milestone A 完整 UI/Scene/Productization 缺口；
- V4F 完成后下一正式 Development WP 的拆分；
- 何时进入 Experience Vertical Slice；
- 美术与视觉生产何时正式接入。

---

## 30. 下一 Design Frontier

本轮 Village Future Design Envelope 到此 Closure。

下一节点不继续向 Village 微观生活深入。

下一步转向：

> **Playable Milestone A 的剩余顶层设计与开发排程。**

重点包括：

- 当前仓库距离 Milestone A 还缺什么；
- V4F 完成后下一 Development WP 怎么拆；
- Battle Tutorial / Reward / Home Productization / UI / Scene 哪些先做；
- Functional Vertical Slice 与 Experience Demo 的边界；
- 美术 / 视觉生产接入时机。

---

## 31. Checkpoint Closure 声明

本文件用于保存：

- 本轮完整讨论成果；
- 当前采用方向；
- Supporting Candidate；
- Deferred；
- Rejected / Replaced；
- Future Architecture Constraints；
- Milestone A Implementation Slice。

本文件不自动升级为 Current Authority。

正式提升链仍为：

> **Design Discussion / Checkpoint  
> → 用户明确采用  
> → `GameDesign/当前设计状态.md` 归位 Current Design + Freeze Level  
> → 若影响可执行规则或验收边界，同步对应 GameSpec  
> → Development / Validation**

