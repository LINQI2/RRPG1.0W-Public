# Product-Home-World-GameSpec-V0.1

> 版本：V0.1
> 日期：2026-09-07
> 状态：Current / FP-V0.1 / Product Home World Foundation
> 职责：Product Home World Foundation 的正式执行 Authority 与验收边界；定义 Existing Village Simulation Authority → Product Home Read Adapter → Product World Presentation / Interaction 的分层、Presentation State 边界、Selection / Inspector / Camera 最小规则，以及 Do Not Build / Deferred 边界。
> 历史编号：本文件最初使用的“WP-2”是已经停用的早期交付编号，不是当前 Milestone A WP-2；后续引用应使用 **Product Home World Foundation** 或本文件正式名称，不为其另行分配新 WP 编号。
> 设计意图：见 [GameDesign/07 Village.md](../../GameDesign/07%20Village.md) §7.13；本文件只负责“开发与验收时必须知道什么”。
> 上承接：`GameSpec/Village/Village-GameSpec-V0.1.md`（V1–V3、V4 Home↔Expedition 与第 11 章 Minimum Village World Integration 第一包边界）
> 概念证据：`Docs/DesignCheckpoints/2026-09-07_RRPG_Functional-Prototype-V0.1_Concept-Checkpoint.md`（默认不是 Current Authority）
> 注意：本文件描述 FP-V0.1 目标规则，不维护实时开发阶段；实现状态统一核对 `Docs/项目状态快照.md` 与 `Docs/开发验收交接.md`。

---

# 0. 系统定位

## 0.1 正式目标

> **Existing Village Simulation Authority → Product Home Read Adapter → Persistent Product World Presentation / Selection / Inspector / Camera**

- Home / Village 是真实 World，不是 Town Menu / 资源结算页 / UI 中转页；
- Product Home 只读复用既有 Simulation Authority，不为 Home 新建第二套 Village Simulation；
- 具体类名 / 接口名不冻结，由后续 Product Home World Foundation 的正式开发规格决定；本文件中的名称是职责角色名或对现有对象的引用。

## 0.2 负责

### 当前世界时间校准（2026-10-07 制作人验收后调整）

- 正常 1×：现实 24 分钟对应世界 1 天（1440 秒）；0.5× 为 48 分钟一天，2× 为 12 分钟、4× 为 6 分钟、8× 为 3 分钟、12× 为 2 分钟、24× 为 1 分钟一天。暂停仍不推进时间。
- 现有速度按钮循环 0.5× → 1× → 2× → 4× → 8× → 12× → 24× → 0.5×；显示读取 VillageTime 的实际倍率。速度只改变现实秒到世界分钟的换算，不改变路径、到岗、活动或结算规则。
- 人物行走表现跟随同一世界倍率，暂停时冻结，包括到场后尚未走完的表现路程；Simulation gating 关闭或世界 AdvanceSession 持有推进锁时不接收新的行走时间。表现不改变实际 Arrival / Presence，也不阻塞模拟推进。
- VillageTime 仍为唯一时间权威；旧存档的 5 秒日长仅用于 Legacy 时间迁移，不用于当前实时驱动。

- Product World Presentation（Person / Place 可见实体）；
- Person / Place Selection；
- Shared Context Inspector；
- Camera 最低浏览；
- 把 Simulation Facts 合成为产品可消费的只读 Snapshot。

## 0.3 不负责

- 决定 Person 是否在村 / 工作 / 远征 / 投入 Project / 可用；
- 决定 Workplace Capacity / Occupancy；
- 维护长期 Location truth；
- 运行完整 Authoritative Activity State Machine；
- 保存 Person / Place Presentation World Position、Movement / Camera 等表现状态；玩家放置的 Building Gameplay Position 按 [Building GameSpec](Village-Building-World-GameSpec-V0.1.md)持久化，不属于本条禁止范围。

---

# 1. Authority Inputs（Product Home 只读并复用）

## 1.1 复用清单

Product Home 只读并复用：

- PlayerCollection；
- PersonId；
- VillageRuntimeBridge；
- VillageAssignmentSystem；
- Village Availability；
- Project Occupancy；
- Expedition Occupancy；
- VillageWorldRegistry；
- VillageWorldPresence与ActivityWorld已发布的实际Presence/到场查询；
- Workplace Capacity / Occupancy。

## 1.2 禁止新增第二套 Authority

不得为 Product Home 新增第二套：

- Person state；
- Assignment；
- Occupancy；
- Capacity；
- Place registry；
- Long-term location truth。

任何 Second Source 一旦出现即视为偏离本规格。

## 1.3 只读约束

- Product Home 不得直接写入 Assignment / Occupancy / Availability 的 Simulation 判定；
- 高影响操作一律回到既有管理入口（Roster / Commitment / Expedition Prep 等）执行。

---

# 2. Product Home Read Adapter

必须存在一个只读 Adapter / Snapshot 层，把多个 Simulation Facts 合成为 Product World 与 Inspector 可消费的 Snapshot。

- 输入全部来自既有 Simulation Authority，不持有可写 Simulation 状态；
- 具体类名不冻结；
- Adapter 输出应为纯只读视图 / Snapshot，不反向写回 Simulation。

---

# 3. Person Snapshot

最低表达：

- PersonId；
- DisplayName；
- Simulation Presence（Away / Free / Workplace / ProjectSite 等派生结果）；
- Current Place；
- Assignment / Commitment；
- Availability；
- Occupancy reason；
- Product presentation intent。

Product presentation intent 只驱动表现（Idle / Traveling / Work / HiddenAtPlace 等候选），不拥有 Simulation Authority。

---

# 4. Place Snapshot

最低表达：

- PlaceId；
- DisplayName；
- Place kind / source；
- Capacity（若对应 Authority 存在）；
- Occupancy；
- Occupant PersonIds；
- Current stable / changed / needs-attention 状态。

> Product Home 不自行维护 Capacity / Occupancy。

---

# 5. 不新增 Authoritative Activity Runtime

FP-V0.1 不新建完整 Authoritative Activity State Machine。

现有Assignment / Project / Expedition / Availability与已发布Activity Runtime提供真实状态；Product Home只读这些事实，不复制Activity FSM、Clock或长期地点。

> 需要真实规则判定（如能否工作、能否出征）时，一律回到既有 Authority；Product Home 不做并行 Activity 裁决。

---

# 6. Presentation State

以下属于 Presentation-only：

- Idle；
- Traveling；
- Work；
- HiddenAtPlace；
- Visible / Hidden；
- Person / Place Presentation World Position（不含玩家放置的 Building Gameplay Position）；
- Animation State；
- Current Anchor；
- Selection Highlight。

这些状态不拥有 Simulation Authority，不得反向决定 Assignment / Occupancy / Availability / Workplace Capacity / Person Identity / Project State / Expedition State。

---

# 7. Traveling 语义

Commitment / Activity Commit可先形成出发计划，但计划目的地不等于实际到达。Simulation的分钟边界确认arrival / Executing后才可表达实际在场、接待与劳动；Presentation Traveling / 动画可滞后或跳过，不能反向推进Clock、提前宣告arrived或决定Claim/Fact。

D最小现场面板已接入实际Presence与查看/中断，是Current薄交互；本规格的完整Person/Place实体、共享Inspector、Camera与空间表现仍为后续目标，不因面板完成而宣称原WP-AL-5已实施。

# 8. Persistent World Entities

正式 Product World 不采用 Debug Layer 的“周期刷新 → 全量销毁 → 全量重建”作为主要产品表现方式。

Person / Place 使用 persistent world entities，只更新目标 / 显隐 / 动画 / 选择。

---

# 9. Debug VillageWorldLayer

`VillageWorldLayer` 继续保持：

> Debug Visualization / Diagnostics / Test / Not Frozen

不得直接升级为 Product World，不作为 Spatial Product Proof，不作为 World Interaction Authority。

---

# 10. Place Registry 边界

`VillageWorldRegistry` 保持薄，不要把 Capacity / Occupants / World Position / Nav / Building Simulation / Animation / Work Progress 全塞进 `VillagePlaceRecord`。

- `VillagePlaceRecord` 维持最小 Place 事实（PlaceId / Kind / DisplayName / SourceRef），Capacity / Occupancy 由对应 Runtime Authority 提供；
- Product World 的视觉（Anchor / Sprite / 位置）属于 Presentation，不写入 `VillagePlaceRecord`。

---

# 11. Capacity / Occupancy Authority

对现有 Workplace：

> Capacity / Occupancy 继续由 Assignment / Workplace Runtime 提供。

Product Home 只读，不复制第二份权威状态。

---

# 12. Presentation Anchor / Slot

Place Presentation 可以提供一个或多个 Anchor / Slot，用于移动目标、站位、朝向、动画表现。

必须明确：

> Presentation Anchor 数量 ≠ Functional Capacity。

Anchor / Slot 数量是视觉承载，不等于该 Place 可以容纳多少正式 Occupant。

---

# 13. World Selection

必须支持：

- Person Selection；
- Place Selection。

规则：

- Selection 本身不修改 Simulation；
- World 点击与 List 点击应汇入同一 Selection Context；
- 同一时间一个共享 Selection Context 承载当前 Person / Place 选择。

---

# 14. Shared Context Inspector

Person / Place 共用统一 Inspector 体系。

最低 Person Snapshot：

- Identity；
- Current State；
- Current Place；
- Assignment / Commitment；
- Availability。

最低 Place Snapshot：

- Identity；
- Capacity；
- Occupancy；
- Occupants；
- Current Status。

不冻结最终视觉布局。

---

# 15. Camera Minimum

FP-V0.1 最低要求：

- Pan；
- Zoom；
- Focus Person；
- Focus Place。

不冻结输入方式、具体 zoom range、easing、orthographic / perspective、具体角度、Follow Hero。

---

# 16. Persistence

FP-V0.1 不要求保存 Product Home Presentation State。

不得为了 Product Home World Foundation 新增以下长期 Save Authority：

- Person world position；
- Traveling progress；
- Animation state；
- Selected object；
- Camera position；
- Current anchor。

Load 后根据当前 Simulation Facts 重建合理 Product World Presentation；单纯表现重建不要求提升 SaveVersion。后续 Building Gameplay 增量的旧档兼容与版本判定按 [Building GameSpec §22](Village-Building-World-GameSpec-V0.1.md#22-saveversion)实施前审计，不由本表现规格提前保证版本一定不变。

---

# 17. Scene / Host Strategy（Product Home World Foundation Development Strategy，非永久 Architecture）

以下属于 **Product Home World Foundation Development Strategy**，不是永久 Architecture：

> Single Scene Host + Separate Product Home World Layer

当前 FP-V0.1 推荐：在现有 Scene 内增加独立：

- ProductHomeWorldRoot；
- Product Home Camera；
- World Interaction；
- Person / Place Entity Root。

TownUI 继续作为 Home UI Orchestrator，而不是“村庄本体”。

> 禁止把“RRPG 永久必须 Single Scene”写入长期 GameDesign / Architecture。

---

# 18. Product Home World 与 TownUI 的边界

TownUI 可继续负责：

- PlayerCollection 绑定；
- Expedition 入口；
- Deck / Growth / Global Panel 入口；
- Return 后 UI 恢复；
- HUD refresh。

但：

> Product Home World 不应作为 TownCanvas 的子 UI 世界实现。

Product World 与 UI Layer 应职责分离。

---

# 19. FP-V0.1 明确不要求

- Daily Schedule；
- Needs System；
- Hunger / Fatigue；
- Eating Simulation；
- Sleeping Simulation；
- Social AI；
- Relationship AI；
- Building Interior；
- Room Simulation；
- Bed Assignment；
- House Ownership；
- Tavern System；
- Real Logistics；
- Warehouse；
- NavMesh 作为正式冻结方案；
- Follow Hero；
- Hero Manual Movement；
- 最终 2.5D；
- 最终动画集；
- 最终美术；
- 多 Scene 架构重构。

---

# 20. 只留 Checkpoint / Supporting Candidate（本轮不升级 Current）

本轮不升级 Current：

- 2.5D；
- Farm / Kitchen / Square / Tavern 具体组合；
- 4–6 Person；
- Idle / Move / Work 具体动画数量；
- Follow Hero；
- Manual Hero Movement；
- HiddenAtPlace 具体视觉实现；
- Snapshot → Details → Action 最终 UI 层级；
- 最终 Place 数量；
- 最终人物数量；
- 最终 Camera 形式。

以上内容继续留在 `Docs/DesignCheckpoints/2026-09-07_RRPG_Functional-Prototype-V0.1_Concept-Checkpoint.md` 或其它证据层的 Supporting Candidate / Not Frozen 状态，不得因实现候选而自动升级为 Current。

---

# 21. Do Not Break

- PlayerCollection / PersonId Authority；
- VillageRuntimeBridge；
- Assignment；
- Availability；
- Temporary Occupancy（Project / Expedition）；
- VillageWorldRegistry（薄 Place 记录 + ProjectSite 单向追加）；
- VillageWorldPresence 派生解析；
- Workplace Capacity / Occupancy；
- VillageTime 唯一时间权威；
- SaveVersion / SaveData 现有结构；
- 既有管理 UI（Roster / Commitment / Expedition Prep / Growth 等）；
- `VillageWorldLayer` 的 Debug 角色。

---

# 22. 未冻结与后续

- 正式 2.5D / 2D / 3D 路线、正式 Place 内容、正式 Person 数量、最终 Camera 参数、Pathfinding 实现、最终 Inspector Layout、Follow Hero / Manual Movement、Daily Schedule / Needs / Social AI 均未冻结；
- 具体类名 / 接口 / Snapshot 结构 / 具体 Anchor 与 Scene 布局由后续 Product Home World Foundation 的正式开发规格决定；
- 测试内容 / 测试值不得自动升级为正式设计；
- 本规则文件不维护实时开发阶段；Product Home World Foundation 的开发与验收应以对应正式开发规格、`Docs/项目状态快照.md`、`Docs/开发验收交接.md` 及既有验证规则为准。

<a id="v16-home-surfaces-position"></a>

# 23. 2026-10-07 Home Surfaces / Projection / Position 增量边界

Product Home 仍是现有 Simulation 的只读产品表现，继续复用 §1～§5 的 Authority / Adapter，不拥有 Building Runtime。World-first ≠ World-only：Person / Management / Cards / History 是 Home 内部 Surface，打开时默认不自动 Pause，顶部时间控制继续可用；玩家自行选择 Pause / Speed。Expedition 继续走既有离开 Home 与 Simulation gating，不通过内部页面重新建立 Clock。

Attention 是已有 Domain 可行动事实的导航 Projection；Village Feed 第一版是 Session-level Recent Occurrence Projection，可不持久化，必须来自明确 Domain lifecycle / occurrence output，不能逐帧比较 Snapshot 猜历史。二者不建立新的 Simulation、WorldEventHistory、PersonLifeHistory 或 VillageSocialFactLedger Authority，也不自行定义资源预测、缺员或效率警报阈值。

两种 Position 必须分开：

| Position | Authority / 持久化边界 |
|---|---|
| Person / Place Presentation World Position | Presentation-only，继续按 §6 / §16 不保存；Load 后从 Simulation Facts 重建 |
| Player-authored `BuildingInstance.WorldPosition` | Building Gameplay State，按 [Building GameSpec §5 / §21～§24](Village-Building-World-GameSpec-V0.1.md)保存稳定 Village-local 坐标，不能以屏幕像素 / Camera / Sprite Offset 替代 |

Building 的持久身份、Placement legality、Construction、Function activation 与 Restore 顺序由 Building GameSpec 决定；Product Home 只投影其状态与派生 Place，写操作委托领域 Command / Authority。内部 Surface 规则不冻结最终 UI Layout 或完整页面内容。实施分期见 [V16 总 Development Spec](../../Docs/DevelopmentSpecs/2026-10-07_RRPG_Village-Product-Home-and-Building-Foundation_Development-Spec-V0.1.md)，V16-A～D Not Authorized / Not Started。
