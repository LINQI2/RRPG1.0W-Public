# RRPG Village Product Home + Building Foundation Development Spec V0.1

> 日期：2026-10-07  
> 基线：`main@49f9191168cabac08566c264f4c5bd030511fb84`  
> 状态：**Planned / Specified / Not Authorized for Implementation**  
> Workstream：开发16.0  
> 规划 Gate：`V16-P → V16-A → V16-B → V16-C1 → V16-C2 → V16-D`  
> 本文件是实施前总开发契约。  
> 当前只允许用于文档归位、交叉引用、一致性审查与后续实现交接；在制作人明确授权前，不得据此修改 Runtime / Scene / Prefab / SourceAssets / XLSX。

---

# 0. Executive Summary

Village 已完成大量 Simulation Foundation：

- Person / PersonId；
- VillageTime；
- Assignment / Workplace；
- Project；
- Resource；
- Expedition Bridge；
- Autonomous Activity；
- Social Feedback；
- Intraday Travel / Arrival / Work；
- Presence；
- Minimal Onsite inspection / interrupt；
- Save / Load。

正式产品缺口是：

1. 将真实 Simulation 映射为 Persistent Product World；
2. 建立 Home Shell / Selection / Inspector / Navigation；
3. 建立玩家主动 Placement 的长期 Building World Object；
4. 复用 Project / Person-Time 完成施工；
5. Completed Building 激活实例级 Workplace / Place；
6. 证明正式美术可经 Presentation / VisualAsset 替换，不返工 Gameplay。

---

# 1. Current Baseline / Do Not Rebuild

必须复用现有 Authority：

- `PersonId`；
- `PlayerCollection` 长期 Person；
- `VillageTime`；
- `VillageAssignmentSystem`；
- `VillageProjectSystem`；
- `VillageWorldPresence` / D 当前实际 Presence 查询；
- Intraday Work；
- Autonomous Activity Lifecycle；
- Social Feedback；
- `VillageResourceState`；
- `VillageWorldRegistry` 作为薄 Place Registry；
- SaveVersion 当前 1；
- D-019 Static Config / shared VisualAsset 链。

禁止建立：

- 第二套 Person；
- 第二套 Occupancy；
- 第二套 Village Clock；
- 第二套 Assignment；
- 第二套 Project Progress；
- 第二套 Place Registry；
- 第二套 History Authority；
- 第二套 VisualAsset Authority。

---

# 2. Existing Prototype / Legacy Surface Classification

## 2.1 VillageWorldLayer

状态：

> Debug / Functional Visualization

处理：

- 保留；
- 不直接美化为正式 Product World；
- 后续可作为 Debug Overlay / Diagnostics。

## 2.2 VillageWorldLayer.Onsite

状态：

> D 薄产品 / Debug Interaction Evidence

处理：

- Query / Command 能力可复用；
- 当前界面不作为正式 Home Inspector；
- 正式表面后续迁移到 Shared Inspector。

## 2.3 VillageHUD

状态：

> Functional HUD / Prototype

处理：

- Time API 可复用；
- Jump Month / Test State / 全员 Availability 不进入正式 Home。

## 2.4 TownUI

状态：

> 当前 Home / Return 等产品编排宿主之一

处理：

- 不因 V16-A/B 直接删除；
- 当前排版不作为新 Home Layout Authority；
- 逐 Gate 迁移，不大爆炸重写。

## 2.5 TownUI.infoText

状态：

> Local UI message sink

不得升级为 Village Feed / History Authority。

---

# 3. V16-P — Planning Publication Gate

目标：

> 把本轮规划沉淀到仓库，形成可由后续 Agent 从 Bootstrap 恢复的正式文档链。

本 Gate 必须是 **纯文档**。

禁止修改：

- `.cs`
- Scene
- Prefab
- PNG
- Font
- XLSX
- Generated Config
- SourceAssets

新增核心文件：

1. Design Checkpoint；
2. Building World GameSpec；
3. 本 Development Spec。

并同步：

- GameDesign；
- GameSpec Index；
- Product Home Spec；
- Architecture；
- Decision；
- Issue；
- Doc Map；
- DevelopmentSpecs Index；
- Current Status / Log / Handoff（在规划验收后）。

Gate P PASS 后仍然：

> **Implementation Not Started**

直到制作人明确授权 V16-A。

---

# 4. V16-A — Product World Foundation

## 4.1 Goal

把现有真实 Village Simulation 变成：

> **Persistent、只读、可选择、可聚焦的正式 Product World。**

A 不新增 Building Gameplay。

---

## 4.2 Data Flow

```text
Existing Simulation Authority
→ Product Home Read Adapter
→ Home Snapshot
→ Persistent World Entity Coordinator
→ Person / Place World Entity
→ Selection Context
→ Inspector / Camera
```

另有：

```text
Identity
→ Presentation Resolver
→ Presentation Key
→ VisualAssetId
→ Shared VisualAsset
```

Snapshot 不直接拥有 Sprite。

---

## 4.3 Person Snapshot

最小目标：

- PersonId；
- DisplayName；
- Current Presence；
- CurrentPlaceId；
- Assignment；
- Availability / Occupancy summary；
- Current Activity summary；
- Presentation Intent。

Read Adapter 只组合已有 Authority 已给出的答案。

禁止自己重新写一套：

- Working Time；
- Activity State；
- Project Priority；
- Arrival；
- Expedition Presence。

---

## 4.4 Place Snapshot

最小目标：

- PlaceId；
- DisplayName；
- Source / Kind；
- Capacity；
- Occupancy；
- OccupantPersonIds；
- lightweight status。

所有 Capacity / Occupancy 来自真实 Runtime。

---

## 4.5 Persistent Entity

Person / Place 实体不得按固定刷新周期：

> destroy all → recreate all

Person 状态更新：

- same identity；
- update visual state；
- update target anchor；
- update label；
- update selection。

Away：

- world representation hidden / disabled；
- identity 不失效；
- Inspector 可以继续显示 Away。

---

## 4.6 Presentation Movement

V16-A 不引入 NavMesh。

Presentation 可以使用：

- simple linear；
- simple curved；
- anchor-to-anchor。

Simulation Arrival 是唯一事实。

Animation：

- 不得提前制造 Arrival；
- 不得阻塞 Simulation；
- 高倍速允许 chase / snap。

---

## 4.7 Selection Context

World Click 与 List / future Management 必须汇入同一个 Product Selection Context。

例如：

```text
SelectPerson(PersonId)
FocusPerson(PersonId)
OpenPerson(PersonId)
```

Inspector 不关心 Selection 来源。

Selection 不进 Save。

---

## 4.8 Inspector A

第一包只读：

Person：

- Name；
- Profession；
- Activity；
- Place；
- Assignment；
- status；
- Open Person。

Place：

- Name；
- Capacity；
- Occupancy；
- current occupants；
- Open Management。

暂不加入写操作。

---

## 4.9 Camera

V0.1：

- Pan；
- Zoom；
- Focus Person；
- Focus Place。

Norland-like 斜俯视作为首版 Working Profile。

Camera 不进 Save。

---

## 4.10 Debug coexistence

保留 `VillageWorldLayer` 作为 Debug Overlay / Diagnostics。

目标：

> Product World 看到错误时，可以对照真实 Authority / Debug 事实。

旧 Debug Layer 不成为 Product Authority。

---

## 4.11 A Acceptance Matrix

必须验证：

- Authority Isolation；
- Snapshot Correctness；
- Persistent Entity；
- Away / Return；
- Traveling Boundary；
- Selection；
- Inspector；
- Camera；
- Save / Load presentation rebuild；
- Diagnostics coexistence；
- Simulation Gating；
- 至少一个 World Entity 走正式 Presentation → VisualAsset。

---

# 5. V16-B — Home Product Whitebox

## 5.1 Goal

将 A 的正式 World 包装成第一版可玩的 Home Surface。

---

## 5.2 Top HUD

读取：

- `VillageTime`
- `VillageResourceState`
- Attention projection

V0.1 显示：

- Date；
- Time；
- Pause；
- 1×；
- 4×；
- Food；
- Ingredients；
- Attention。

不得继承：

- Jump Month；
- Test State；
- 全员 Availability 常驻。

---

## 5.3 Navigation

Home internal surfaces：

- World；
- Person；
- Management；
- Cards；
- History。

Exit Home：

- Expedition。

Future Mode：

- Build（C1 后接通）。

B 阶段 Build Button 可以显示但保持 Disabled / Not Available。

---

## 5.4 World Label

Person Default：

- name only。

Hover：

- name + one-line current action。

Selected：

- highlight + Inspector。

Attention：

- separate marker。

Place / Building 默认不常驻大量文字。

---

## 5.5 Attention Projection

只消费已有 Domain 可行动事实。

不得自行建立：

- food prediction；
- vacancy threshold；
- efficiency threshold。

Attention 主要保存：

- Subject；
- Target；
- Navigation Intent。

它是 Product Projection，不是 Simulation / History。

---

## 5.6 Village Feed

V0.1：

> Session-level Recent Occurrence Projection

来源必须是明确的 Domain lifecycle / occurrence output。

不得：

- 逐帧 diff Snapshot 猜事件；
- 升级成新的 History Authority。

V0.1 可不持久化。

---

## 5.7 Shared Inspector

继续保持轻量，不把 Person / Management 页面塞进 Inspector。

---

## 5.8 Home Internal Surface Time

Person / Management / Cards / History 打开：

> 默认不自动 Pause Village Simulation。

顶部时间控件继续工作。

Expedition 继续使用现有离开 Home / Simulation gating。

---

## 5.9 Whitebox Pressure Fixture

允许测试专用 Snapshot Fixture：

- 约 12 Person；
- multiple places；
- Work / Travel / Talk / Idle；
- Selected；
- Attention。

Fixture 只能用于 Visual QA / Test。

Production Home 永远使用真实 Read Adapter。

---

## 5.10 B Product Acceptance

制作人至少确认：

1. 第一眼是 Village，不是 UI；
2. 12 Person 不形成文字墙；
3. Default / Selected / Attention 易区分；
4. HUD 可读但不抢 World；
5. Inspector 足够理解当前对象；
6. Navigation 清楚；
7. Feed 有生活感但不噪；
8. Home internal Surface 保持世界连续性。

---

# 6. V16-C1 — Building Placement Foundation

## 6.1 Goal

新增第一个正式玩家创作的 Village Gameplay World State：

> BuildingInstance。

只验证：

- Definition；
- Catalog；
- Placement；
- Identity；
- Position；
- Save / Load。

不做施工劳动。

---

## 6.2 Static Config Target

推荐创建：

```text
Development/Config/RRPG_Buildings.xlsx
```

创建时间：

> C1 实施时。

V16-P 不提前伪造该文件。

推荐最小 Definition：

- BuildingDefinitionId；
- DisplayName；
- Category；
- FootprintWidth；
- FootprintDepth。

Schema 以实施 Preflight 为准。

---

## 6.3 Runtime

目标对象：

- BuildingDefinition；
- BuildingInstance；
- Building Registry / Query；
- Placement Session；
- Placement Validator。

BuildingInstance：

- stable identity；
- definition；
- world position；
- state = UnderConstruction。

---

## 6.4 Placement Validator

Preview / Confirm 共用唯一 Validator。

V0.1：

- Buildable bounds；
- footprint overlap。

不冻结：

- Grid；
- Road；
- Terrain；
- Nav；
- Cost；
- Rotation。

---

## 6.5 Save

新增 additive Building block。

保存不可派生长期实例状态。

实施前必须做旧档兼容 Audit。

优先保持 SaveVersion=1，但不能先承诺。

---

## 6.6 Unknown Definition

不得 silent drop。

---

## 6.7 C1 Acceptance

- Catalog 从正式 Definition 读取；
- Placement Ghost；
- same validator；
- bounds invalid；
- overlap invalid；
- stable identity；
- multi-instance；
- save/load；
- presentation separation；
- no fake construction / production / cost。

---

# 7. V16-C2 — Construction + Function Activation

## 7.1 Goal

让 C1 的 `UnderConstruction BuildingInstance`：

> 通过真实 Person-Time 完成施工，并激活属于该 BuildingInstance 的 Runtime Workplace / Place。

---

## 7.2 Construction Binding

目标：

```text
BuildingInstanceId
↕
Construction ProjectId
```

Project 继续拥有：

- participants；
- progress；
- actual labor；
- temporary occupancy。

Building 不复制 Project Progress。

---

## 7.3 Construction Place

UnderConstruction Building 派生：

```text
construction:<BuildingInstanceId>
```

D / Product World Presence 指向具体 Building。

旧非 Building Project 可以继续兼容 legacy site logic。

---

## 7.4 Add / Remove Worker

补正式 Construction Worker Command。

Remove Worker：

- release temporary occupancy；
- keep Primary Assignment；
- keep progress。

0 Worker：

- construction paused；
- project/building remains。

---

## 7.5 Actual Labor

只由真实 arrival + actual work interval 推进。

---

## 7.6 Finalize

完成点必须幂等并一次收口：

- Building → Completed；
- clear binding；
- retire construction place；
- activate completed presentation；
- register building functions；
- release temporary occupancy。

---

## 7.7 Building Function Binding

推荐 Static Config 概念：

- BuildingDefinitionId；
- FunctionSlotId；
- FunctionType；
- FunctionProfileId。

V0.1 只实现：

> Workplace。

---

## 7.8 Runtime Workplace Instance

目标身份：

```text
workplace:<BuildingInstanceId>:<FunctionSlotId>
```

不允许同类多 BuildingInstance 共用一个具体 WorkplaceId。

---

## 7.9 Assignment Restore Order

必须确保：

```text
restore building
→ register completed runtime workplace
→ restore assignment
```

防止长期 Assignment 因 Workplace 尚未存在而被丢弃。

---

## 7.10 Resource Workplace

若动态资源绑定改动极小，可在 C2 Gate B 接入。

如果发现当前 `_resourceWorkplaceDefs` 初始化结构耦合较深：

> Assignment / Workplace activation 先 PASS；
> Resource dynamic binding 作为紧随 C2 的受控小包。

不得为了 C2 大改整个 Resource Domain。

---

## 7.11 C2 Acceptance

- unique construction binding；
- add/remove workers；
- actual labor；
- construction place identity；
- zero worker pause；
- same BuildingInstance completion；
- atomic finalize；
- multi-instance workplaces；
- separate assignment；
- save/load construction；
- compatibility regressions pass。

---

# 8. V16-D — Asset Replacement Proof

## 8.1 Goal

证明：

> Placeholder → Formal Art 不要求 Gameplay / Save / Building / Assignment 返工。

---

## 8.2 Test A — Same AssetId Source Replacement

例如：

```text
va_village_farm_basic
→ placeholder farm PNG
```

运行完整链。

随后只换 Source PNG：

```text
va_village_farm_basic
→ formal / semi-formal farm PNG
```

重新 Compiler / Builder。

要求所有共用此资源的 Farm Instance 更新。

---

## 8.3 Test B — Presentation Binding Replacement

不覆盖原 PNG。

改：

```text
village.building.farm.basic
→ va_village_farm_visual_b
```

只重建配置 / Presentation。

Gameplay 不改。

---

## 8.4 Test C — Multi-instance Propagation

至少两个 Farm Instance。

替换视觉后全部更新，但：

- instance identity；
- position；
- worker；
- assignment；
- construction；
- save

各自保持。

---

## 8.5 D Pass Criteria

必须做到：

- zero Gameplay code change；
- zero Save migration caused solely by art change；
- zero Building identity change；
- zero Assignment change。

D 不验最终美术质量。

---

# 9. Presentation / Visual Production Architecture

## 9.1 Existing Asset Authority

继续复用共享：

```text
RRPG_VisualAssets.xlsx
```

当前直接实证边界：

> PNG → Sprite。

不得在 V16-P 把 Prefab / Material / Spine 写成已验证 Current。

---

## 9.2 Village Presentation Target

推荐未来建立独立 Village Presentation Authoring Domain，用于：

- Place / Building visual binding；
- Person world presentation；
- anchor；
- scale；
- offset；
- selection bounds。

它只引用共享 VisualAsset。

实际工作簿名称 / Schema 在 A/C1 实施时 Preflight 后创建。

---

## 9.3 UI Presentation

V0.1 UI：

> 结构正式，Skin 临时。

可以用：

- Unity Image；
- color block；
- TMP。

暂不建立万能 UI Presentation XLSX。

---

## 9.4 Production Tracking

`Missing / Draft / Approved / Deprecated`

属于：

> Art Production Tracking。

不得强行进入 Runtime VisualAsset Definition。

---

# 10. Save / Persistence

新增 Building 长期状态时必须核对：

- Save GameSpec；
- `VillageSaveData`；
-旧档；
- invalid data；
- NewGame reset；
- failed Load isolation；
- Return / FinalSave interaction。

V16 不借 Building 机会解决整个 Village / Event 世界级事务隔离债。

---

# 11. Tests

## 11.1 Unit / EditMode

至少覆盖：

- Snapshot resolver；
- placement validator；
- building identity；
- save conversion；
- construction binding；
- dynamic workplace registration；
- restore order；
- unknown definition；
- asset binding resolver。

## 11.2 PlayMode / Product Path

至少覆盖：

- Town/Home → world；
- actual person presence；
- selection / inspector；
- save/load；
- building placement；
- construction worker；
- actual work；
- completion；
- runtime workplace；
- assignment；
- reload；
- asset replacement smoke。

## 11.3 Manual Producer Acceptance

A/B：

- Village feels like a living world；
- not spreadsheet wall；
- information density；
- selection；
- home continuity。

C：

- “我真的在世界里建了一座东西”；
- person truly travels / builds / works there。

D：

- visual replacement without gameplay rework。

---

# 12. Deferred / Anti-scope

V16 不做：

- final building catalog；
- formal economy；
- wood / stone；
- grid system；
- navmesh；
- building rotation；
- roads；
- upgrades；
- destruction；
- rooms；
- furniture；
- logistics；
- full History implementation；
- full Person Detail implementation；
- final Management page；
- final Cards page；
- final Art Bible；
- Spine pipeline；
- weather；
- day/night lighting；
- seasons。

---

# 13. Known Existing Issues Receiving This Plan

与本 Workstream 直接相关：

- Issue 84：Commons pseudo Place；
- Issue 85：Active Project / ProjectSite type inconsistency；
- Issue 86：runtime Workplace seed；
- Issue 88：formal Product Home World Surface missing。

V16 规划是这些问题的未来解决路径，不表示 P Gate 已经关闭问题。

---

# 14. Documentation Landing

V16-P 需要将长期结论归位：

- `GameDesign/07 Village.md`
- `GameDesign/当前设计状态.md`
- `GameSpec/README.md`
- `Village-GameSpec-V0.1.md` historical stage clarification
- `Product-Home-World-GameSpec-V0.1.md`
- `Docs/Architecture/技术架构总览.md`
- `Docs/Architecture/核心对象与术语映射.md`
- `Docs/决策记录.md`（建议 D-022）
- `Docs/问题清单.md`
- `Docs/文档地图.md`
- Checkpoint / DevelopmentSpec indexes

P Gate producer acceptance 后再同步：

- `Docs/项目状态快照.md`
- `Docs/项目日志.md`
- `Docs/开发验收交接.md`

---

# 15. V16-P Exit Criteria

必须：

1. root `main` baseline 核对；
2. 三份新增规划文档落盘；
3. GameDesign / GameSpec / Architecture 交叉引用一致；
4. Building 历史阶段禁止语句有 Current override；
5. Product Home Position 规则与 Building Gameplay Position 不冲突；
6. Decision / Issues 有明确指针；
7. Development Spec 状态仍是 Not Authorized；
8. 全部修改仅 Markdown；
9. Git diff 无 Runtime / Scene / Asset / XLSX；
10. 制作人阅读 diff 后明确确认。

只有之后才允许：

> V16-A Implementation。

---

# 16. Recommended Execution Order After Authorization

```text
A1 Read Adapter
A2 Persistent Entities
A3 Selection / Inspector / Camera
A4 Presentation binding proof
→ Producer A Gate

B1 Home HUD
B2 Navigation
B3 World label / attention
B4 Village Feed
B5 whitebox QA
→ Producer B Gate

C1-1 Building config / definition
C1-2 placement validator
C1-3 runtime instance
C1-4 save/load
→ Producer C1 Gate

C2-1 construction binding
C2-2 worker add/remove
C2-3 actual labor
C2-4 finalize
C2-5 runtime workplace
C2-6 restore order
→ Producer C2 Gate

D1 placeholder asset
D2 same-id replacement
D3 binding replacement
D4 multi-instance proof
→ Architecture PASS
```

---

# 17. Stop Conditions

遇到以下任一情况停止相关 Gate 并上报：

- 需要新 Person / Occupancy / Time Authority；
- 需要改变 Autonomous Activity 规则；
- 需要改变 Village Project 语义才能完成 Construction；
- 需要把 Building 与 Place 强行同一身份；
- 旧档兼容必须静默丢 Building；
- SaveVersion 是否改变无法在现有规则下判断；
- 正式建筑资源体系成为 C2 前置；
- Product Home 需要接管 Simulation Arrival；
- VisualAsset 当前链无法满足 Sprite Replacement 且需要扩大 D-019 已验证对象类型；
- 发现 Current GameSpec 与本规划直接冲突且无法通过历史阶段边界解释。

---

# 18. Current Status

截至 2026-10-07：

> **Planning only。**

A～D 均未授权实现。

允许的下一动作仅为：

> `V16-P Repository Documentation Publication / Cross-reference Audit`。
