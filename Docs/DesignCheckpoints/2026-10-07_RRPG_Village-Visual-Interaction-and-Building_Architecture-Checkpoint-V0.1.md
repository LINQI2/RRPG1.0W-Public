# RRPG Village Visual / Interaction / Building Architecture Checkpoint V0.1

> 日期：2026-10-07  
> 基线：`main@49f9191168cabac08566c264f4c5bd030511fb84`  
> 状态：**Design Checkpoint / 已采用结论的日期化设计证据；不是单独 Runtime 授权**  
> 对应阶段：开发16.0 / `V16-P Planning Publication`  
> 作用：沉淀本轮关于 Village Product Home、集中界面、人物/历史/卡牌入口、世界信息密度、建造产品模型与视觉生产工作流的设计收口，避免后续对话重新从旧原型推导产品结构。  
> Authority 边界：Current 长期设计应归位 `GameDesign/`；可执行玩家规则归位 `GameSpec/`；技术实现与 Gate 见对应 `Development Spec`。本文件保留“为什么这样决定”和未冻结边界。

---

# 1. 本轮问题

开发16.0接手时，Village 已拥有大量真实 Simulation 能力，但正式玩家表面仍主要由 Debug / Functional / Prototype UI 承担。

已存在并必须复用的事实包括：

- `PersonId` 长期具体人物身份；
- `VillageTime` 唯一村庄时间 Authority；
- Assignment / Workplace；
- Project 临时承诺；
- 日内通勤、实际到场、实际劳动；
- Autonomous Activity Lifecycle；
- Social Feedback；
- Presence / 同地点查询；
- Home ↔ Expedition 已有连接；
- SaveVersion 当前保持 1；
- D 最小 Onsite 查看 / 中断能力。

问题不是“重新做村庄模拟”，而是：

> **如何把已存在的真实 Village Runtime 变成正式的、可观察、可点击、可管理、可扩展并能长期替换美术素材的产品世界。**

同时旧原型存在若干明显产品缺口：

- `VillageWorldLayer` 仍是 Debug / Functional Visualization；
- 旧 Project 启动 UI 更像开发原型，不像模拟经营中的主动建造；
- 人物、集中管理、历史、卡牌等入口尚未形成正式产品信息架构；
- Place / ProjectSite / Workplace 仍保留早期 Minimum World 的测试语义；
- 美术资源链已有 D-019 与共享 VisualAsset，但 Village 如何绑定与复用正式素材尚未收口。

---

# 2. 本轮 Confirmed Architecture

## 2.1 World-first，但不是 World-only

Village Product Home 的基础层是持续运行的村庄世界。

玩家应优先通过世界看到：

- 人物正在生活；
- 人物在哪里；
- 建筑与地点在哪里；
- 施工与世界变化在哪里；
- 当前有哪些值得关注的变化。

但 **World 不承担全部复杂管理**。

模拟经营仍需要统一、集中的功能界面用于：

- 全村比较；
- 批量调度；
- 深度人物查看；
- 卡牌体系浏览；
- 历史回看；
- 远征准备。

因此：

> **World-first ≠ 所有功能都塞进 World。**

---

## 2.2 Village 一级产品结构

当前收口的 Village / Home 产品结构为：

1. **World / 村庄世界**  
   负责观察正在发生什么、空间点击、快速 Inspector、世界模式。

2. **Person / 人物**  
   负责理解具体长期人物：概览、生活、职业成长、关系、人生经历。

3. **Management / 集中管理**  
   当前第一版推荐内部视图：
   - 人口；
   - 职责与场所；
   - 资源。

4. **Cards / 卡牌**  
   负责角色牌 / 通用牌体系与 Profession 理论牌池、具体 Person Unlocked Pool 的浏览。  
   不建立独立“职业卡”第三顶层卡牌类别。

5. **History / 历史**  
   当前推荐内部视图：
   - 编年史；
   - 人物档案；
   - 村庄日志 / 历史查询入口。  
   History View 不建立第二套 History Authority。

6. **Expedition / 远征**  
   离开 Home 的主要玩法循环入口。

7. **Build Mode / 建造模式**  
   是一级功能，但不是脱离世界的独立全屏管理页面。玩家在 World 内选择建筑并放置。

辅助层：

- Top HUD：时间 / 核心资源 / Attention；
- Shared Inspector；
- Village Feed；
- Tooltip / Modal；
- Event 表面；
- Notification / Attention Layer。

---

# 3. Person 产品结构

Person 是 RRPG 的长期核心价值对象，不是可替换 Worker。

统一 Person Detail Framework 推荐：

- **概览**
- **生活**
- **职业成长**
- **关系**
- **人生经历**

职责分配只是 Person“生活”的一个子模块，不是 Person 页本体。

普通 Person、Hero、未来 Historical Person 共用基础框架：

- Hero 可通过扩展模块承载未来 Hero-specific Talent / Common Card / 特殊成长；
- 死亡或离开的 Person 不从产品层消失；
- Historical Person 使用同一 Person Detail，但转为 Read-only / Historical 状态。

Person 页面回答：

> “这个人是谁、现在过着怎样的生活、成为了怎样的人、和谁发生了关系、留下了什么。”

Management 页面回答：

> “全村当前怎么调度。”

二者不得重复建立 Assignment Authority。

---

# 4. History / Feed / Attention 的边界

## 4.1 Person Life History

回答：

> “这个具体 Person 的一生发生了什么？”

属于长期人生历史表现。

## 4.2 Chronicle / 编年史

回答：

> “这个共同体为什么变成今天这样？”

只保留较高语义密度、真正值得长期回看的共同体历史。

## 4.3 Village Feed

回答：

> “刚才村里发生了什么？”

V0.1 推荐作为 Session-level Product Projection：

- 轻量；
- 高频；
- 可不持久化；
- 不成为 `WorldEventHistory`、`PersonLifeHistory`、`VillageSocialFactLedger` 的替代品；
- 不通过 UI 自行猜测并建立第二套历史事实。

## 4.4 Attention

回答：

> “现在有什么值得我处理或查看？”

Attention 是导航层，不是 Simulation Authority。

它只聚合已有 Domain 已经成立的可行动事实，例如：

- Growth Opportunity；
- Pending Event；
- 后续已存在的施工完成；
- 未来正式资源风险规则产生的风险。

V0.1 不通过 UI 自行发明：

- 缺员阈值；
- 资源预测阈值；
- 效率低于某值即报警等新玩法规则。

---

# 5. Home Surface V0.1

## 5.1 总原则

> **World 占绝对主体，UI 贴边。**

第一眼应先看到村庄、人和正在发生的行为，而不是模拟经营后台表格。

推荐白模结构：

- 顶部：日期、时刻、Pause / 1× / 4×、核心资源、Attention；
- 中央：Village World；
- 右侧：Selection 时出现 Shared Inspector；
- 左下：2～3 条轻量 Village Feed；
- 底部：人物 / 管理 / 卡牌 / 历史 / 远征 / 建造。

Bottom Navigation 是 V0.1 推荐布局，不冻结为永久 UI Layout。

---

## 5.2 Home 内部页面连续性

Person / Management / Cards / History 属于 Home 内部 Surface。

推荐行为：

- 打开后不自动停止 Village Simulation；
- Top Time Control 仍可用；
- 世界可作为弱化背景保持连续性；
- 玩家自己选择 Pause 或 Speed。

Expedition 是真正离开 Home 的主循环。

---

# 6. World Entity 信息密度

统一采用渐进披露：

- Default；
- Hover；
- Selected；
- Attention。

## 6.1 Person

正常游戏视距：

- 默认只常驻 **姓名**；
- 不常驻 Profession / Assignment / Activity / 多状态 Icon。

Hover：

- 姓名强化；
- 一句当前行为。

Selected：

- Selection Feedback；
- Shared Inspector。

Attention：

- 单独 Attention Marker。

原则：

> **Animation / Pose first，Icon second，Text third。**

Person 应先被理解为“张三”，再是“农夫”。

---

## 6.2 Place / Building

默认尽量不常驻名称和经营数据。

Hover / Selected 时再展示：

- 名称；
- 少量状态；
- Inspector。

轻微次优状态不自动升级成 Attention。

---

# 7. Shared Inspector

Inspector 是世界快捷理解层，不是第二套 Person Detail 或 Management。

Person Inspector V0.1：

- Portrait；
- DisplayName；
- Profession；
- Current Activity；
- Current Place；
- Assignment；
- 当前状态；
- “查看人物”。

Place / Building Inspector V0.1：

- 名称；
- Capacity / Occupancy；
- 当前人物；
- 少量当前状态；
- “打开管理 / 查看详情”。

复杂修改后置到正式页面。

---

# 8. Build Mode 产品模型

旧原型式：

> `Project 面板 → 勾参与者 → 启动项目`

不再作为正式建设产品模型。

正式玩家体验方向：

```text
World
→ Build
→ 选择 Building Definition
→ 世界内 Placement Ghost
→ Valid / Invalid
→ 确认放置
→ BuildingInstance（UnderConstruction）
→ 人员/时间推进施工
→ Completed
→ 激活正式功能
```

底层可以复用 Project 作为有限 Person-Time Commitment 机制，但：

> **VillageProject ≠ Building。**

---

# 9. Building 是长期世界对象

本轮采用以下长期边界：

- `BuildingDefinition`：建筑种类；
- `BuildingInstance`：世界中的具体一座建筑；
- `Place`：可工作 / 活动 / Presence 的语义地点；
- `Presentation`：怎么显示。

硬边界：

```text
BuildingDefinition ≠ BuildingInstance
BuildingInstance ≠ Place
BuildingInstance ≠ Presentation
Place ≠ Presentation
```

`BuildingInstance` 从第一版开始拥有稳定身份。

这样未来可以支持：

- 同类多实例；
- 命名；
- 历史引用；
- 废弃 / 改造；
- 建筑经历。

本轮不实现上述后续内容，但身份模型不得阻止它们。

---

# 10. Construction Site 身份修正

不采用：

```text
ConstructionSite A
→ 完成
→ 销毁
→ Building B
```

采用：

```text
BuildingInstance #017
State = UnderConstruction
→
State = Completed
```

即：

> **Construction Site 是 BuildingInstance 的 Lifecycle / Presentation State，不是另一座对象。**

施工前后稳定 `BuildingInstanceId` 不变。

---

# 11. Building 与 Place

Place 的职责是：

> “这里可以发生什么？”

Building 的职责是：

> “世界里存在一座什么长期物理对象？”

一个 Building 可以提供：

- 一个 Workplace Place；
- 多个 Workplace / Activity Place；
- Home Place；
- Temporary / Social Place 等。

一个 Place 也可以不来自 Building，例如：

- Village Square；
- 自然区域；
- 其它世界语义位置。

因此不得使用：

```text
BuildingInstanceId == PlaceId
```

作为长期架构。

---

# 12. Construction 与 Project

现有 Project 可复用：

- Participant；
- Temporary Occupancy；
- Person-Time；
- Progress；
- 实际到场劳动；
- 完成与释放。

正式 Construction 推荐：

```text
BuildingInstance
↕
Construction Project
```

但 Project 仍可服务非建筑事务。

施工 Location 应由 BuildingInstance 派生：

```text
construction:<BuildingInstanceId>
```

而不是让临时 ProjectId 冒充正式世界地点身份。

---

# 13. Completed Building → Runtime Function

同类建筑实例必须拥有不同 Runtime Workplace Identity。

禁止：

```text
Farm #1 → ws_farm_01
Farm #2 → ws_farm_01
```

目标：

```text
Farm #1 → workplace:bld_00017:main
Farm #2 → workplace:bld_00023:main
```

两者可以共享静态 `Workplace Profile` / Function Profile，但 Runtime Instance 必须可独立：

- Capacity；
- Assignment；
- Occupancy；
- History。

---

# 14. Presentation / Visual Asset

D-019 已有共享 VisualAsset Authority，当前直接实证范围为 PNG / Sprite。

Village 不新建第二套资产库。

目标关系：

```text
Gameplay / Product Meaning
→ Presentation Key
→ VisualAssetId
→ Shared VisualAsset Catalog
→ Sprite
```

`RRPG_VisualAssets.xlsx` 负责：

> “这张图是什么。”

Village Presentation 负责：

> “谁使用它、以什么参数显示。”

当前推荐未来建立独立 Village Presentation Authoring 域，但实际工作簿与 Schema 在实施 Gate 创建前仍是 Target，不伪装为 Current。

---

# 15. Placeholder 原则

至少核心 World Asset 从第一版就必须走正式 VisualAsset / Presentation 路径。

禁止：

```text
if placeholder:
    DrawGreenRectangle()
else:
    LoadFormalAsset()
```

目标：

```text
Placeholder PNG
→ 正式 VisualAsset 路径
→ 后续替换正式 PNG
```

Runtime 不知道它是 Placeholder 还是真实正式图。

UI V0.1 可以使用程序色块 / TMP，先冻结组件结构，不强行把全部 UI Skin 提前塞入共享 Sprite 配置链。

---

# 16. 第一版视觉工作基线

Village 首版空间表现直接采用：

> **Norland-like 斜俯视共同体世界**

作为快速推进和验证空间信息架构的工作基线。

这不是：

> “RRPG 最终美术 = Norland。”

永久美术方向继续服从：

`GameDesign/ArtDirection/`

当前本轮只确认：

- 斜俯视；
- 多 Person 共同生活；
- Building / Ground / Person 有真实空间关系；
- 清晰、可读、世界优先；
- 不为首版反复重新裁决最终画风。

---

# 17. 白模验证场景

推荐第一轮统一使用同一 Village Scene 的两个状态：

1. Normal + Selected Person；
2. Build Mode。

压力测试内容：

- 约 12 Person；
- 住宅 / 农田 / 厨房 / 工坊 / 公共空间等若干测试 Place；
- Work / Travel / Talk / Idle；
- Selected；
- Attention；
- World Label；
- Inspector；
- Feed；
- HUD；
- Build Ghost。

白模以信息密度、交互结构和 Authority 正确性验收，不以最终审美质量验收。

---

# 18. 当前 Rejected / Replaced

以下方向不得从旧原型恢复为正式产品默认：

- 将 Project 选择面板作为正式“建建筑”的主要产品入口；
- 让 World 承担全部集中管理；
- 把所有复杂管理塞进 Inspector；
- Person 默认头顶常驻 Profession / Assignment / Activity / 多状态；
- 将 Village Feed 升级为新的 History Authority；
- Attention 自行发明资源/岗位规则；
- Construction Site 完成后销毁并生成无关新 Building 身份；
- 多个 BuildingInstance 共用一个具体 Runtime WorkplaceId；
- 把 `VillageWorldRegistry` 扩成 Building / Grid / Nav / Presentation 万能 Authority；
- 为 Placeholder 和正式图建立两套 Runtime 路径。

---

# 19. 当前 Deferred / Not Frozen

本轮不冻结：

- 最终 UI Layout；
- 最终摄像机参数；
- Grid Snap vs Free Placement；
- Building Rotation；
- 正式建筑内容全集；
- 正式 Building Cost / Wood / Stone；
- 建筑升级；
- 拆除 / 维修 / 损坏；
- 道路 / NavMesh；
- 房间系统；
- 家具；
- 物流；
- 电网；
- 建筑历史 UI；
- ActivityProvider 型 Building Function；
- 正式资源风险阈值；
- 最终 Norland-like 视觉是否保留；
- Spine / Animator / Sprite 最终人物技术路线；
- UI Skin 的长期静态配置 Schema。

---

# 20. 后续开发顺序

本轮规划采用：

```text
V16-P Planning Publication
→
V16-A Product World Foundation
→
V16-B Home Product Whitebox
→
V16-C1 Building Placement Foundation
→
V16-C2 Construction + Function Activation
→
V16-D Asset Replacement Proof
```

在 V16-P 完成、文档一致性检查通过、制作人确认前：

> **不得开始 Runtime / Scene / Asset / XLSX 实现。**

---

# 21. 本 Checkpoint 的归位要求

长期产品结论应同步到：

- `GameDesign/07 Village.md`
- `GameDesign/当前设计状态.md`

可执行 Building 规则应同步到：

- `GameSpec/Village/Village-Building-World-GameSpec-V0.1.md`

Product Home 现有规则的增量边界同步到：

- `GameSpec/Village/Product-Home-World-GameSpec-V0.1.md`

技术目标与实施 Gate 见：

- `Docs/DevelopmentSpecs/2026-10-07_RRPG_Village-Product-Home-and-Building-Foundation_Development-Spec-V0.1.md`

本文件之后只作为：

> **讨论过程、采用理由、Rejected / Deferred 以及阶段性设计证据。**
