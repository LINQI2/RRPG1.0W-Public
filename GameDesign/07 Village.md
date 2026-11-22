# 07 Village

> 状态：GameDesign V0.2 当前 Village / 村庄经营设计；文件名仅为领域导航，不据此冻结正式系统名称。
> 2026-09-07 FP-V0.1 归位同步：补充 Early Village 节奏与 Observe / Manage 上层原则（§7.12），该节只约束节奏与交互原则；FP-V0.1 最小生存资源链已由 WP-3 归位 Current（见 §7.11 / §7.14），不恢复旧四资源体系。
> 2026-09-07 Product Home World Foundation 归位同步（当时使用的“WP-2”为已停用历史交付编号，不是当前 Milestone A WP-2）：补充 Product Home World 长期产品原则（§7.13）：Home = Real World、Simulation ≠ Presentation、World-first ≠ World-only、Selection ≠ Action、Place 上层定义；不冻结 Anchor 数量 / 坐标结构 / NavMesh / Building 层级 / UI Layout / Camera 参数。
> 2026-09-07 WP-3 归位同步：升级 FP-V0.1 Minimal Survival Resource Loop（Ingredients → Food，§7.14）；补充 Person-centered Scarcity 与 Risk Communication 原则；不恢复旧四资源体系。

## 7.1 村庄的长期职责

村庄是玩家塑造共同体条件、承担社会承诺并观察长期后果的主要场所。它不是功能建筑列表，也不只是远征之间的资源结算页。

村庄主要负责：

- 呈现共同体当前趋势、压力与可能性；
- 让玩家选择优先级并作出有代价的承诺；
- 通过时间推进让后果显现；
- 扩展未来可能出现的人物成长、卡牌、遗物、装备、事件与 Run 内容；
- 提高训练和发展方向的控制程度；
- 让远征带回的经历与收益进入长期世界。

村庄扩大未来可能性，但不应以建筑等级粗暴硬锁普通人物成长。

### Home 与 Autonomous Life（Current Architecture）

Village 是 Person 长期生活、责任承担与互动发生的 Home。Autonomous Life 不把 Village 变成第二套人物 Authority，而是在现有 Person / VillageTime / Occupancy / Place 基础上增加正式自主生活能力。

当前确认：

- 玩家主要控制 Responsibility / Commitment（责任 / 承诺），不逐分钟控制所有 Person。
- `Primary Assignment（主要长期职责）` 是长期 Responsibility，不等于全天 Occupancy；拥有 Assignment 的 Person 仍可在责任之外参加 Autonomous Activity。
- `Temporary Occupancy（临时占用）` 继续负责 Expedition / Active Project / Unavailable 等高优先级占用，并作为自主活动资格的 Hard Gate。
- `VillageTime（村庄时间）` 继续是唯一时间 Authority；Autonomous Activity Window 是 VillageTime 上的 Scheduling Phase，不建立第二套 Clock。
- Place / Person / Context 可以提供 Activity 可能性；Provider 提供“能做什么”，Person Decision 负责“想做什么”。
- World Presentation 负责让玩家看见人物生活，但不拥有 Simulation Authority；Jump Month / Return Catch-up 等无现场表现路径也必须执行同一 Daily Simulation 规则。
- Autonomous Activity 可以产生 Semantic Fact；普通 Fact 不直接等于 Event，只有经合理 Recognition 后才进入 Unified Event Core。
- 社会 Fact / Person Life History / World Event History 可以保持不同职责；`No Parallel Authority（禁止平行权威） ≠ Force One Ledger（强行单一账本）`。

算法与数据承载的具体冻结见 Technical Architecture；Utility 数值、正式 Personality / Relationship 内容、24 小时 Schedule、正式地图 / Navigation 参数仍未冻结。

完整产品规格见 [Autonomous Life Vertical Slice Product Spec V0.2](../Docs/DesignCheckpoints/2026-10-04_RRPG_Autonomous-Life-Vertical-Slice_Product-Spec-V0.2.md)。

WP-AL-3 产品规则进一步区分 Possibility / Eligibility / Plausibility；NoMajorActivityOption 是“当前机会没有特别想主动做的主要活动”的正式决策项，不是 Activity，也不等于拒绝参与。所有人物同轮读取同一 Snapshot，Utility 与 Participant Acceptance / 分轮公平仲裁职责分离，不按固定更新顺序先选先占，不以全村 Utility 最大化匹配。候选顺序只生成一次，冲突后后移指针。规则见 [Village GameSpec §12](../GameSpec/Village/Village-GameSpec-V0.1.md)，依据见 [WP-AL-3 Final](../Docs/DesignCheckpoints/2026-10-04_RRPG_WP-AL-3_Autonomous-Selection_Design-Checkpoint-V0.2-Final.md)。Home / Tavern / Workshop 与五种活动只确认 Prototype 范围，不冻结正式建筑；WP-AL-1/2/3均已发布main；本次不改其Current事实。

WP-AL-4确认下一次Decision Snapshot前，所有更早发生且应影响关系的Social Feedback必须settled。Activity / Event Producer只表达发生语义，由Social单一解释；Rest / Drink / Talk不进入Persistent Relational Fact Ledger，VisitSick / Help可形成Canonical Fact，Significant / Episode是下游投影。Durable Outbox与新Decision Snapshot前的结算Barrier已发布main；日内机会复用唯一VillageTime边界，而非仅午夜开窗。不建立第二套Clock / Activity / Relationship Authority，不把本轮扩为Recognition / Event / LifeEpisode Runtime。规则见[Village GameSpec §12.6](../GameSpec/Village/Village-GameSpec-V0.1.md#wp-al-4-feedback)，依据见[WP-AL-4 Final](../Docs/DesignCheckpoints/2026-10-04_RRPG_WP-AL-4_Social-Feedback_Design-Checkpoint-V0.1-Final.md)。

## 7.2 村庄五步核心循环

> 观察 → 选择优先级 → 作出承诺 → 推进时间 → 后果显现

### 观察

玩家读取人物、群体、资源、趋势与外部环境正在发生什么。

### 选择优先级

玩家决定当前最值得处理的压力和最愿意争取的未来。

### 作出承诺

玩家投入时间、人员、资源、制度信用或其他仍待确认的承载，接受机会成本。

### 推进时间

项目、人物与趋势继续变化，未处理的问题也不会停住。

### 后果显现

结果以状态变化、事件、人物经历、新条件或被关闭的可能性呈现，并成为下一轮观察的基础。

## 7.3 时间模型

当前村庄时间方向是：

- 时间实时推进；
- 玩家可以暂停；
- 玩家可以加速；
- 玩家可以跳至下个月等重要时间节点；
- 日常变化可以按日积累；
- 较大事件与结构变化可以在日、月或其他节点显现。

时间倍率、日月长度、离线推进与跳月限制均未冻结。时间模型的目的，是让承诺、等待和后果成为可理解的关系，不是制造无意义的倒计时压力。

## 7.4 长期模式转化为可感知回应与事件

村庄应优先显示正在形成的长期倾向，而不是只在随机事件弹出后告知结果。

> 世界事实 / 状态变化 → 长期 Pattern → 合理感知与 Recognition → 在具有决策意义时形成 Signal / Event / Consequence

事件是长期事实与共同体的可感知结果之一，不是完全脱离村庄状态的随机题库。Pattern 的具体识别条件、事件内容和必要的数据承载由对应 Stage / GameSpec 决定；除非未来真实消费者证明需要，不预建统一 Trend Runtime 数据层。

## 7.5 与人物和远征的连接

村庄连接 Battle 的主要方式是扩大未来可能性，例如：

- 扩展人物 Unlocked Pool；
- 提供调整 Starter、强化 Trait 或其他长期成长的机会；
- 打开通用牌、遗物、装备或新的 Run 内容候选；
- 提高玩家对训练与发展方向的控制；
- 通过人物、事件与共同体状态改变远征条件。

研究院、训练场或其他建筑是否成为正式承载仍未确认。当前只冻结这些职责，不冻结载体。

## 7.6 八个社会决策维度

村庄内容可以从以下八个维度组织玩家选择：

1. 生存与安全：眼前生存、风险准备与安全边界。
2. 共同体关系：信任、团结、冲突、照顾与排斥。
3. 资源与分配：有限条件如何取得、分配与承担代价。
4. 发展与能力：把当前资源用于未来能力、知识和组织建设。
5. 治理与权力：谁决定、如何执行、如何质疑以及谁承担责任。
6. 文化与意义：共同体如何理解自己、保存传统并解释牺牲。
7. 外部关系：如何看待陌生人、邻近群体、交换、威胁与合作。
8. 人的生活与自主：共同体目标与具体人物生活、尊严和选择之间的张力。

这些是设计检查维度，不等同于八种资源、八条科技树或八个固定 UI 标签。

## 7.7 两个横切轴

每个社会选择还应从两个横切轴检查：

- 现在 ↔ 未来：当前缓解与长期能力之间如何取舍；
- 谁受益 ↔ 谁承担：收益与代价落在相同的人身上，还是被转移给其他人。

横切轴帮助事件和制度显示真实代价，不要求把它们做成正式点数。

## 7.8 五层社会复杂度阶梯

村庄复杂度按理解逐层积累：

| 层级 | 主要主题 | 玩家开始理解的关系 |
|---|---|---|
| 1. 共同体形成 | 团结 ↔ 发展 | 先照顾彼此，还是把有限条件投入未来能力 |
| 2. 内部秩序 | 冲突、规则与治理 | 谁来制止伤害，规则如何获得正当性 |
| 3. 共同体边界 | 警戒、外部关系 | 谁属于“我们”，安全与开放如何平衡 |
| 4. 共同价值 | 信仰、意义与文化 | 共同体为何坚持、牺牲和服从 |
| 5. 认知与挑战 | 知识、质疑与方法 | 如何检验共同理解，并允许新认识挑战既有秩序 |

这是一条社会复杂度与内容教学阶梯，不自动等于正式篇章数量、章节名或解锁表。Theme 的总体结构是 Tree / Branch，而不是线性文明塔；五层阶梯不得解释为 Theme 解锁顺序、DLC 前置链或唯一历史发展路线。后层继续使用前层已建立的关系，不做系统清零。

## 7.9 两组辩证主题

### 团结 ↔ 发展

团结保护当下的人与关系，发展争取未来能力。两者都可能必要，也都可能在失衡时产生代价。

### 信仰 ↔ 知识

信仰提供共同意义与行动力量，知识提供检验、积累和修正能力。两者不是善恶阵营，也不存在预设唯一正确路线。

清路、青鹿、团结点、统筹点、精神点、意识点、质疑点等具体承载均未冻结，不得从主题直接推导。

## 7.10 内容扩展原则

- 先确认主题、教学目的和玩家取舍，再决定资源、建筑、政策或事件形式。
- 初始村庄内容可以少，但五步循环必须完整可玩。
- 新内容应增加关系与可能性，不只是增加百分比增益。
- 服务型功能不自动等于战略建筑；是否需要建筑应由设计职责决定。
- 后续复杂度建立在玩家已经学会的关系上，不通过篇章清零重置。

## 7.11 Legacy 与未冻结承载

以下旧内容不再自动视为 V0.2 正式方案，仅作为早期候选或历史承载：

- 固定食物、木材、石材、金币四资源；
- 医疗室、仓库、伐木场、采石场等固定建筑表；
- 大厅、墓地、舒适度；
- 通过“下一天消耗食物”统一推进建造和治疗；
- 旧建筑升级表与固定数值。

FP-V0.1（2026-09-07 归位）：**正式采用 Ingredients → Food 作为最小生存资源链**（见 §7.14）；Ingredients / Food 在 FP-V0.1 资源链层为 Current。这是 FP-V0.1 Vertical Slice 的正式资源链，不代表最终游戏永久只保留 Ingredients / Food，也不恢复旧 Food / Wood / Stone / Gold 四资源体系。

仍未冻结：

- 村庄最终资源种类；
- 正式建筑列表；
- 政策与文化的正式结构；
- 人物工作、岗位与全局池结构；
- 研究院、训练场是否成为正式系统；
- 所有时间、趋势、事件、资源与建设数值；
- 正式篇章数量和章节边界。

## 7.12 FP-V0.1 Early Village 节奏与 Observe / Manage 原则

> 2026-09-07 归位：本节是 FP-V0.1 前期 Village 上层原则，只约束节奏与交互原则；FP-V0.1 最小生存资源链（Ingredients → Food）已归位 Current（见 §7.11 / §7.14），本节的 Observe / Manage 不把资源链层写成未冻结。

### Early Village 节奏

> Battle / Expedition 优先展开，Village 复杂度慢构筑。

含义：

- 开局不同时教学两套高复杂度系统；
- Village 前期可以先作为稳定 Home 被观察和理解；
- 不为了教学主动制造经营危机。

### Observe / Manage

> Stable = Observe
> Introduced Variable = Manage

含义：

- 稳定运转的 Village 内容优先让玩家看见和理解；
- 当 Expedition、玩家选择或 Village Event 真正引入变量后，相关内容才需要玩家管理；
- Battle / Expedition 是前期主要变量来源之一，但不是唯一来源。

### 资源候选边界

FP-V0.1 已归位 Current 的资源链为：Farm → Ingredients → Kitchen → Food → Village Consumption / Expedition Carry（生产、加工、消费与 Carry 的结构性职责见 §7.14 与 `GameSpec/Village/Village-Resource-Coupling-GameSpec-V0.1.md`）。仍保持 Supporting Candidate / Not Frozen 的是：Global Inventory 的具体实现结构、正式 Conversion Ratio 数值、Kitchen Processing Capacity 正式数值、正式 Demand / Hunger 数值、Daily Delta 正式公式。

## 7.13 Product Home：Home 是真实 World（长期产品原则）

> 2026-09-07 归位：本节是 Product Home World 的长期产品原则，只写原则、不写 C# 实现，也不冻结任何表现承载。

### Home = Real World

Village / Home 是可浏览、可识别具体 Person 与 Place 的共同体世界，不再只被定义为 Town Menu、资源结算页或 UI 中转页。最低方向：可浏览 Village World，至少能识别具体 Person 与基础 Place；World 不只是 UI 背景。

### Simulation ≠ Presentation

Person 是否在村、是否工作、是否远征、是否投入 Project、属于哪个 Workplace、是否可用等事实，由既有 Village Simulation Authority 决定。World Presentation 只负责 World Position、Movement、Animation、Anchor、Visibility、Selection Highlight、Camera Focus 等表现；这些表现不得反向决定 Assignment、Occupancy、Availability、Workplace Capacity、Person Identity、Project State 或 Expedition State。

> Person 是否完成工作，不由 Sprite 是否实际走到目标点决定。

### World-first ≠ World-only

World 是 Village 的主要认知入口，但不是唯一管理入口。允许 Person List、Commitment List、Management Overview、Expedition Prep、Search / Find Person 等其它入口；所有入口必须汇入同一 Person / Place、同一 Selection Context、同一 Simulation Authority。

### Selection ≠ Action

点击 World Object 首先建立 Selection / Context，不直接执行高影响操作。例如：点击 Person ≠ 自动加入 Expedition；点击 Farm ≠ 自动改变 Assignment；点击 Project ≠ 自动投入参与者。

### Place 上层定义

> Place = Village World 中可被 Person / Commitment / Activity 指向的功能地点。

Place 不等于建筑。FP-V0.1 不建立 Building → Room → Workstation 的正式多级 Simulation 层。

### 产品层只读复用

Product Home 只读并复用既有 Village Simulation Authority（Person / Assignment / Availability / Temporary Occupancy / Project / Expedition / Workplace Capacity / Place Registry / Presence），不新建第二套 Activity / Presence / Assignment / Occupancy / Capacity / Place Registry。

### 不冻结

本节不冻结：Anchor 数量、表现坐标结构、NavMesh、具体 Building 类层级、UI Layout、具体 Camera 参数；正式 2.5D / 2D / 3D 路线、正式 Place 内容、正式 Person 数量与最终 Inspector Layout 继续未冻结。2026-10-07 有限 Building Foundation 的身份、生命周期与玩家位置规则已归位 §7.16 及 Building GameSpec，不能继续笼统解释为建筑规则全部未定。具体只读表现规则见 [Product Home GameSpec](../GameSpec/Village/Product-Home-World-GameSpec-V0.1.md)。

## 7.14 FP-V0.1 Minimal Village Resource Coupling（最小生存资源链）

> 2026-09-07 归位：本节只写长期产品原则与 FP-V0.1 当前 Design，不写实现类名与数值。

### 最小生存资源链

> **Farm → Ingredients → Kitchen → Food → Village Consumption / Expedition Carry**

- 这是 FP-V0.1 Vertical Slice 的正式资源链；
- 不代表最终游戏永久只保留 Ingredients / Food；
- 不恢复旧 Food / Wood / Stone / Gold 四资源体系。

### Person 影响链

> Food Allocation → Equal Share → Person Daily Fulfillment → Consecutive Underfed Days → Hungry / Shortage State

核心原则：

> 资源后果优先落实到具体 Person，再汇总为 Village 状态（Person-centered Scarcity）。

### Stable = Observe / Introduced Variable = Manage

- Village 初始状态应允许稳定运行，不为了强迫经营主动制造危机；
- Expedition 可通过带走 Person、带走 Food、消耗时间等方式改变资源流；
- 当玩家选择真正引入变量后，相关 Village 内容才进入需要管理的状态。

### Risk Communication

> **Risk Visible → Impact Notify → Sustained Impact Requires Response → Persistent Neglect Creates Consequence**

含义：

- 负增长可被动可见（如 Food 趋势），不主动打扰；
- 尚无人受影响时不打扰；
- 实际状态开始影响具体 Person 时通知；
- 持续影响时提供响应空间；
- 持续忽视后才产生实际后果。

### Resource Time Authority

- Resource daily settlement 只依赖现有唯一 VillageTime / Day Tick；
- 不新建独立 ResourceTime / FoodClock。

> 生产、加工、消费、Person Fulfillment、Hunger、Expedition Carry 的详细执行规则见 [GameSpec/Village/Village-Resource-Coupling-GameSpec-V0.1.md](../GameSpec/Village/Village-Resource-Coupling-GameSpec-V0.1.md)；Expedition Supply 语义同步见 [GameSpec/Expedition/Expedition-Progression-GameSpec-V0.1.md](../GameSpec/Expedition/Expedition-Progression-GameSpec-V0.1.md) Supply 小节。

## 7.15 当前 Design Frontier

> Village Long-term Build → Living Tradition / Historical Legacy

当前讨论位置见 [Village Long-term Build Concept Checkpoint](../Docs/DesignCheckpoints/2026-09-04_RRPG_Village-Long-term-Build_Concept-Checkpoint.md)。下一设计问题是澄清 Living Tradition 何时仍在被具体人物、Place 与实践持续再生产，何时只成为 Historical Legacy。

该 Checkpoint 的 Freeze Level 为 **Concept / Not Fully Frozen**；本入口不确认具体 Opportunity、Tradition、Legacy 机制，不形成 GameSpec，也不授权进入功能开发。原“村庄系统依赖树 → Village V0.1 最小边界”不再作为当前 next-entry。

History × Autonomous Life 的横切引用见本文件 §7.1 与 [当前设计状态](当前设计状态.md#2026-10-04-history--autonomous-life--autonomous-life-vertical-slice-v01-归位)。Living Tradition / Historical Legacy 后续应优先读取 Community Pattern / Life Episode / Transition，复用已有历史真相源，不独立建立第二套历史来源；完整 Consumer 讨论见 [History × Autonomous Life Checkpoint](../Docs/DesignCheckpoints/2026-10-02_RRPG_History-Autonomous-Life_Architecture-Checkpoint-V0.1.md) §13 / §14.6。Life Episode 仅指候选高层人生片段，不改现行 Experience 的客观事实语义；该读取方向不冻结 Tradition / Legacy 的正式机制或算法。

## 7.16 Village Product World / Building Foundation（2026-10-07 归位）

Village Product Home = **World-first，但不是 World-only**。World 负责观察共同体生活、空间点击与快捷理解；Person / Management / Cards / History / Expedition 承担集中产品入口。Build 是 World Mode：玩家在世界中选择、放置并观察施工，不沿用旧 Project 勾人页面作为正式建设产品模型。底层 Project 仍可复用为临时人物时间承诺。

Person 是长期人物，职责分配只是其生活子模块。Person 页面理解具体人物；Management 当前方向为人口、职责与场所、资源，负责全村比较与调度，二者复用同一 Assignment Authority。Shared Inspector 保持快捷理解职责，不吞并完整人物或管理页面。

History 回看共同体变化；Person Life History 理解具体人物一生；Village Feed 表达近期发生的事情，第一版为 Session-level Projection；Attention 聚合已有 Domain 的可行动事实并导航。Feed / Attention 不创造第二套历史或 Simulation，不由 UI 发明缺员、资源预测或效率警报阈值。

Building 是具有稳定实例身份的长期世界对象。建筑种类与具体建筑分离，**Building ≠ Place ≠ Presentation**；Building 可提供多个语义 Place，Place 也可不来自 Building。Construction Site 是同一 BuildingInstance 的生命周期状态，施工前后身份连续；Project 的临时承诺身份不取代建筑身份。玩家放置的建筑位置属于持久 Gameplay State；Person / Place 的表现位置仍由 Presentation 重建。

Presentation 不拥有 Simulation、实际到场、劳动或施工进度；Gameplay / Save 与 Presentation / VisualAsset 分离。核心世界素材的 placeholder 与正式图走同一解析路径，使换图不改变长期世界事实。

规则分别见 [Product Home](../GameSpec/Village/Product-Home-World-GameSpec-V0.1.md)与 [Building World](../GameSpec/Village/Village-Building-World-GameSpec-V0.1.md)；采用理由、Rejected / Deferred 见 [2026-10-07 Checkpoint](../Docs/DesignCheckpoints/2026-10-07_RRPG_Village-Visual-Interaction-and-Building_Architecture-Checkpoint-V0.1.md)。本节不冻结底部导航位置、白模人数、具体类 / 字段或最终美术；Norland-like 仅为首版 Working Baseline，长期方向仍从 [ArtDirection](ArtDirection/README.md)进入。Implementation Not Started，实施须独立授权。
