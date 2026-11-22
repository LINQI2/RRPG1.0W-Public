# Village Building World GameSpec V0.1

> 版本：V0.1  
> 归位日期：2026-10-07  
> 状态：**Current Rule / Active GameSpec；Implementation Not Started**  
> 领域：Village / Building World  
> 目标：冻结 Village Building Foundation 的玩家规则、长期身份、Placement、Construction、Place / Workplace 连接与 Save 边界。  
> 非目标：不冻结正式建筑内容全集、正式资源成本、Grid 算法、NavMesh、建筑升级、最终视觉、完整经济。  
> 实施需要独立 Work Package 授权；本规格存在不代表 Runtime 已实现。

---

# 1. Authority 与适用范围

本规格只负责：

- BuildingDefinition；
- BuildingInstance；
- 玩家 Placement；
- Building Lifecycle；
- Construction 与既有 Project / Person-Time 的规则边界；
- Completed Building 激活功能 Place / Workplace 的身份规则；
- Building Save / Load；
- Building 与 Presentation 的隔离。

本规格不取代：

- `Village-GameSpec-V0.1.md` 的 VillageTime、Assignment、Project、Autonomous Life 等规则；
- `Product-Home-World-GameSpec-V0.1.md` 的 Product Home / Read-only World Presentation 规则；
- `Village-Resource-Coupling-GameSpec-V0.1.md` 的 Ingredients / Food Authority；
- `Save-GameSpec-V0.1.md` 的长期持久化总原则。

冲突时不得自行选边；应登记并由制作人裁决。

---

# 2. 顶层原则

## R1. Building 是长期世界对象

玩家放置后，世界中产生一个具有稳定身份的具体建筑实例。

```text
BuildingDefinition
→ BuildingInstance
```

同一个 Definition 可以产生多个 Instance。

例如：

```text
Farm Definition
├─ Farm #1
├─ Farm #2
└─ Farm #3
```

每个实例必须能够独立拥有：

- World Position；
- Lifecycle；
- 未来 Function；
- Assignment / Occupancy；
- Save；
- 历史引用。

---

## R2. BuildingDefinition ≠ BuildingInstance

`BuildingDefinition` 回答：

> “这类建筑是什么？”

`BuildingInstance` 回答：

> “世界里的这一座是什么？”

不得由：

- Sprite；
- World Position；
- DisplayName

推导 BuildingInstance Identity。

---

## R3. Building ≠ Place

Building 是长期物理世界对象。

Place 是可被：

- Presence；
- Activity；
- Assignment；
- Workplace；
- Interaction

消费的语义地点。

一个 Building 可以提供 0..N 个 Place。

一个 Place 可以不来自 Building。

禁止将长期规则写死为：

```text
BuildingInstanceId == PlaceId
```

---

## R4. Building ≠ Presentation

BuildingInstance 不拥有：

- VisualAssetId；
- Sprite；
- Scale；
- Selection Bounds；
- Anchor；
- Camera；
- UI State。

Presentation 只读取 Building Runtime 并决定如何显示。

---

# 3. BuildingDefinition

V0.1 Static Definition 至少表达：

- Stable `BuildingDefinitionId`；
- Display Name；
- Placement Footprint；
- Catalog Category（若当前 UI 需要）。

Function Binding 可以作为独立静态配置存在。

不得因为是 Building Definition 就复制：

- Workplace Capacity；
- Resource Production；
- Activity Rules；
- Runtime Position；
- Construction Participants；
- Runtime Progress。

这些属于其它 Domain / Runtime Authority。

---

# 4. BuildingInstance

V0.1 BuildingInstance 至少拥有：

- Stable `BuildingInstanceId`；
- `BuildingDefinitionId`；
- Village-local Gameplay World Position；
- Lifecycle State。

V0.1 最小 Lifecycle：

```text
UnderConstruction
Completed
```

第一版不要求 Rotation；若后续需要，应作为兼容增量扩展。

---

# 5. Player-authored World Position

玩家确认 Placement 后的建筑位置是：

> **Gameplay World State。**

必须与 Product Home 的 Presentation-only Person / Place Visual Position 区分。

```text
Person / transient Place visual position
= Presentation-only

Player-authored BuildingInstance position
= Persistent Gameplay State
```

Building Position 不得保存为：

- Screen Pixel；
- RectTransform Position；
- Camera Space；
- Sprite Offset。

应使用稳定的 Village-local Gameplay Coordinate。

---

# 6. Build Catalog

Build Catalog 读取正式 BuildingDefinition Registry。

V0.1 不建立：

- Building Tech Tree；
- Building Unlock Level；
- Research Gate；
- 隐式 UI-only Definition。

Catalog 不拥有 Placement legality、Runtime Instance 或 Construction Progress。

---

# 7. Placement Session

选择一个 BuildingDefinition 后，进入非持久化 Placement Session。

流程：

```text
Select Definition
→ Candidate Position
→ Placement Validator
→ Valid / Invalid
→ Confirm
→ Create BuildingInstance
```

Placement Session：

- 属 Product / Presentation；
- 不进 Save；
- 不产生第二套 Building Runtime。

---

# 8. Single Placement Validator

Preview 与正式确认必须使用同一套 Validator 规则。

不得存在：

```text
Preview says Valid
Runtime says Invalid
```

而来自两套不同实现。

V0.1 最小规则：

1. 位于 Buildable Area；
2. Footprint 不与已存在 BuildingInstance 重叠。

当前不冻结：

- Grid Snap；
- Terrain Type；
- Road；
- Slope；
- Nav；
- Adjacency Bonus；
- Resource Cost；
- Building Limit。

---

# 9. Placement Result

成功确认后立即产生稳定 BuildingInstance：

```text
BuildingInstanceId = stable
BuildingDefinitionId = selected definition
WorldPosition = confirmed position
State = UnderConstruction
```

不先建立临时“Placement Object”再在施工开始时更换长期身份。

---

# 10. Construction Identity

Construction Site 不是新的长期对象。

采用：

```text
BuildingInstance #017
UnderConstruction
→
BuildingInstance #017
Completed
```

`BuildingInstanceId` 从放置到完工保持不变。

---

# 11. Construction 与 Project

V0.1 Construction 可以复用现有 Project 作为：

- Temporary Commitment；
- Participant；
- Person-Time；
- Progress；
- actual-arrival / actual-work execution；
- completion / release。

但：

```text
VillageProject ≠ BuildingInstance
```

Project 仍可服务非建筑事务。

Construction 与 Project 应通过显式 Binding 关联。

---

# 12. Construction Place

UnderConstruction Building 应派生一个可供：

- Work Destination；
- Presence；
- Inspector

使用的 Construction Place。

推荐身份语义：

```text
construction:<BuildingInstanceId>
```

正式 Construction Place 不应以临时 ProjectId 冒充 Building Identity。

该 Place 可由 BuildingInstance State 派生，不要求独立长期 Save。

---

# 13. Construction Workers

正式 Construction 至少支持：

- Add Worker；
- Remove Worker；
- 0 Worker 暂停。

加入施工：

- 是 Temporary Occupancy；
- 不删除 Primary Assignment。

退出施工：

- 解除 Temporary Occupancy；
- 保留既有 Primary Assignment；
- 不清空已完成 Construction Progress。

---

# 14. Actual Person-Time

Construction Progress 只可由：

> 实际到达、实际执行的 Person-Time

推进。

不得仅根据：

- Participant 名单；
- Assignment；
- UI Checkbox；
- 人物逻辑上“应该在工作”

推进正式施工进度。

Presentation Animation 不拥有 Construction Progress。

---

# 15. Construction Cost

V0.1 施工只验证 Person-Time。

当前不冻结：

- Wood；
- Stone；
- BuildingMaterial；
- Ingredients；
- Food；
- Gold

是否作为 Construction Cost。

禁止为了让建筑“看起来正式”临时把当前 Ingredients / Food 解释为建材。

---

# 16. Completion

Project / Construction 达成完成条件时，必须产生一次语义完整、幂等的 Finalization。

完成结果至少包括：

1. 同一个 BuildingInstance：
   `UnderConstruction → Completed`
2. Construction Binding 结束；
3. Construction Place 退役；
4. Temporary Construction Occupancy 解除；
5. Completed Presentation 生效；
6. Building Functions 激活。

不得长期留下：

> Project Completed，但 Building 仍 UnderConstruction

等半完成 Authority 状态。

---

# 17. Building Function Binding

BuildingDefinition 可以声明 0..N 个 Function Slot。

概念结构：

```text
BuildingDefinition
→ FunctionSlot
→ FunctionType
→ FunctionProfile
```

V0.1 实施只要求：

> `Workplace` FunctionType

其它如：

- ActivityPlace；
- Home；
- Storage；
- Social；

继续 Deferred。

---

# 18. Runtime Workplace Instance

多个同类 BuildingInstance 完成后，必须产生不同 Runtime Workplace Identity。

禁止：

```text
Farm #1 → ws_farm_01
Farm #2 → ws_farm_01
```

要求：

```text
Farm #1 → workplace:bld_00017:main
Farm #2 → workplace:bld_00023:main
```

它们可以共享静态 Workplace Function Profile，但必须能够独立：

- Capacity；
- Assignment；
- Occupancy；
- Query；
- Save reference。

---

# 19. Assignment Authority

现有 Assignment Authority 继续拥有：

```text
PersonId → WorkplaceId
```

Building Domain 不建立第二套 Person Assignment。

完成 Building 后只负责：

> 激活 / 注册属于该 BuildingInstance 的 Runtime Workplace。

人员分配仍走现有 Assignment 规则和 Command / Authority。

---

# 20. Place Registry

VillageWorldRegistry / Place Registry 继续保持薄 Query / Registration 职责。

不得扩展为：

- Building Runtime；
- Building Position；
- Construction Progress；
- VisualAsset；
- Navigation；
- Grid；
- Workplace Capacity Authority。

Building 派生 Place 可以注册 / 退役，但 Registry 不成为这些 Domain 的 Owner。

---

# 21. Save

V0.1 Building Save 至少保存不可派生的长期实例事实：

- `BuildingInstanceId`
- `BuildingDefinitionId`
- World Position
- Lifecycle State
- 必需的 Construction Binding / Restore 信息（若仍在施工）

不得保存：

- VisualAssetId；
- Sprite；
- Scale；
- Presentation Anchor；
- Selection；
- Camera；
- Build Catalog State；
- Ghost；
- 可从 Definition + Instance 派生的 Place；
- UI。

---

# 22. SaveVersion

目标优先：

> 使用 additive / nullable block 保持旧档兼容。

当前项目长期方向仍优先保持 `SaveVersion = 1`。

但本规格 **不提前保证** Building 增量一定无需提升 SaveVersion。

实施前必须通过真实 Save Compatibility Audit。

不得为了“保持版本号”牺牲正确性，也不得无必要自行提升版本号。

---

# 23. Load 顺序

如果 Completed Building 会生成 Runtime Workplace，则 Load 必须先保证：

```text
Static Definition
→ Restore BuildingInstance
→ Register Completed Building Functions / Runtime Workplace
→ Restore Assignment
```

否则 Assignment 不得因 Workplace 尚未注册而静默丢失长期绑定。

---

# 24. Unknown BuildingDefinition

Load 遇到存档中存在的 `BuildingDefinitionId`，但当前静态 Definition 缺失时：

> **不得静默删除长期 BuildingInstance。**

必须：

- fail closed；
- 或显式 fallback / diagnostic。

具体产品错误表现可在实施阶段确定，但“不能无声丢失长期世界对象”是 Current Rule。

---

# 25. Multi-instance

V0.1 必须支持：

> 同一个 BuildingDefinition 放置多个独立 BuildingInstance。

这些实例可以共用 Presentation / VisualAsset，但必须拥有独立：

- BuildingInstanceId；
- Position；
- Lifecycle；
- Runtime Function；
- Assignment；
- Save。

---

# 26. Presentation Binding

Building Display 使用：

```text
Building Identity / State
→ Presentation Resolver
→ Presentation Key
→ VisualAssetId
→ Shared VisualAsset Catalog
```

Gameplay 不直接依赖具体 PNG。

VisualAsset 不得进入 Building Save。

---

# 27. Asset Replacement Acceptance

至少验证：

1. 同一 VisualAsset Source 替换；
2. Presentation Binding 改指向另一 VisualAsset；
3. 多 BuildingInstance 共用同一 Presentation。

上述替换后：

- BuildingInstanceId 不变；
- Position 不变；
- Lifecycle 不变；
- Construction 不变；
- Workplace / Assignment 不变；
- Save 不变；
- Gameplay Code 不需要修改。

---

# 28. V0.1 Do Not Build

本规格不授权：

- Grid System；
- NavMesh / Pathfinding；
- Building Rotation；
- 完整建筑升级；
- 拆除；
- 维修；
- 损坏；
- Building Ownership；
- Room；
- Furniture；
- Warehouse / Logistics；
- Electricity；
- Road Network；
- 完整建筑历史 UI；
- 正式建筑材料体系；
- 正式资源经济重构；
- ActivityProvider Building Function；
- 完整建筑动画；
- 正式 Building Content 全集；
- 最终 Art Bible。

---

# 29. Product Acceptance

V0.1 至少必须证明：

1. 玩家从正式 Build Catalog 选择 Definition；
2. Preview 与 Confirm 共用唯一 Validator；
3. 可以放置多个同类 BuildingInstance；
4. Save / Exit / Load 后实例身份与位置正确；
5. UnderConstruction Building 可以分配施工者；
6. 只有真实到场劳动推进进度；
7. Worker 可以撤出且 Primary Assignment 不丢；
8. 完成后同一 BuildingInstance 变为 Completed；
9. Completed Building 激活实例级 Runtime Workplace；
10. 两座同类建筑可分别分配不同 Person；
11. Placeholder → 正式图不需要 Gameplay 返工；
12. 现有 Person / VillageTime / Assignment / Project / Activity / Social / Resource Authority 不被复制。

---

# 30. Historical Stage Boundary Clarification

`Village-GameSpec-V0.1.md` 早期章节曾明确：

- V1 / V2 不提前实现 Building；
- Minimum Village World 第一包禁止 Grid / 自由摆放 / 正式 Building。

这些条目属于当时 Stage 的开发边界。

本规格是后续制作人确认的新阶段 Building Foundation，不表示历史条目当时错误。

当前 Building 规则以：

> **本 `Village Building World GameSpec V0.1`**

为该领域 Current Authority。

---

# 31. Implementation Status

截至本规格归位：

> **Implementation Not Started。**

当前已有：

- Test Workplace；
- Project；
- ProjectSite / World 痕迹；
- VillageWorldLayer Debug / Functional Visualization；
- 日内 Presence / Work / Activity；
- VisualAsset Sprite production chain。

这些是复用基础或 Legacy / Prototype evidence，不能被解释为本规格已经实现。
