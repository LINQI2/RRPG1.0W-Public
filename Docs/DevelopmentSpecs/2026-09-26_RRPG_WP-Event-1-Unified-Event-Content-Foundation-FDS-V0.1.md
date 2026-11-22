# WP-Event-1 Unified Event Content Foundation — Formal Development Spec V0.1

> 阅读状态说明（2026-10-04）：WP-Event-1 已完成 D3、Producer Acceptance Repair 1 / 2 并发布关闭，见[Closeout Report §N](2026-09-26_RRPG_WP-Event-1-Unified-Event-Content-Foundation-Closeout-Report-V0.1.md)与[当前状态](../项目状态快照.md)。下方首页的 Ready for Gate D3、未获 Git 授权及各 1A / 1B 阶段限制为当时记录，不是新的任务队列或本轮授权。当前 History 保存边界查[技术架构总览 §21.3](../Architecture/技术架构总览.md)；不从旧阶段备注推导当前持久化能力。

> **文件性质：FORMAL DEVELOPMENT SPEC / Implementation Contract（开发期 Binding）**
> 它约束本 Work Package 的实现范围、Schema、诊断与验证方式；
> **不是**长期玩家规则 Authority（事件玩法规则仍服从 `GameSpec/` 与 `GameDesign/`），
> **不是**新的架构决定（静态配置生产架构服从 D-019；遭遇生成服从 D-020，本轮不修改二者）。
>
> - Work Package：**WP-Event-1｜Unified Event Content Foundation V0.1**
> - 已执行技术检查点：**1A｜Static Content Foundation**（§17/§18）、**1B｜Resolver Foundation**（§19）、
>   **1C｜Option / Outcome / Effect Foundation**（§20）、**1D｜Product Cutover —— Gate D0**（§21.1–21.4）、
>   **Gate D1｜Village Product Cutover（Phase A + Phase B）**（§21.5–21.9）、
>   **Gate D1-R｜PlayMode Regression Baseline Repair**（§21.10）、
>   **Gate D2｜Expedition Event Product Cutover**（§21.11）
> - 发布日期：2026-09-26
> - Target Branch：`main`（本地工作区）
> - 当前状态：**`WP-Event-1D Gate D2 Technical Verified / Ready for Gate D3`**
>   （D1 = §21.9、D1-R = §21.10、D2 = §21.11；Full EditMode 999/999 + Full PlayMode 80/80 +
>   Compiler 208/208 + `RRPG_检查配置.cmd` PASS + `error CS = 0`）；
>   **Gate D3 Presentation / Producer Acceptance 未开始**
> - **无 Commit / Push 授权**：本文件不隐含任何 Git 写权限。

---

## 1. WP 总目标

统一 Village / Expedition 两套产品 Context 的**事件内容定义形态**，使事件成为可被配置、可被校验、
可被确定性重建的内容对象，覆盖：

| 概念 | 含义 |
|---|---|
| Event Definition | 事件身份、启用状态、抽取权重、重复规则、冷却 |
| Trigger | 事件在什么时机被考虑 |
| Role | 事件里的参与者（ContextPerson / VillagePerson / ExpeditionPartyMember / FixedPerson） |
| Condition | 事件成立条件（Context / History / Person / Village / Expedition） |
| Presentation | 事件如何被展示（JourneyCard / VillageModal / 标题 / 正文 / 插图 / 紧迫性） |
| Option | 玩家可选择的选项与可用条件 |
| Outcome | 选项的确定或加权结果 |
| Effect | 结果造成的世界变化（Flag / Village / Expedition） |

### 1.1 Event Core 的边界（WP 级硬约束）

> **Event Core 不拥有 Village Simulation、Expedition Progression、Encounter、Person Identity、Resource Authority。**

- 事件表**描述**“什么时候、对谁、发生什么”，但：
  * Village 时间 / 项目 / 调度规则仍由 Village 领域拥有；
  * 远征深度 / Supply / 推进规则仍由 Expedition 领域拥有；
  * 敌方阵容仍由 Encounter 领域拥有（`StartBattle` 只允许**引用** `EncounterPool.PoolId`）；
  * Person 身份 / 长期状态仍由 Person 领域拥有；
  * `Ingredients` / `Food` 的存量与结算仍由 Village Resource Authority 拥有。
- 事件配置**不得**成为第二套规则 Authority：它承载 Gameplay Parameter，其语义仍服从 GameSpec。

---

## 2. 四个技术检查点

| 检查点 | 内容 | 状态 |
|---|---|---|
| **1A｜Static Content Foundation** | 正式 `RRPG_Events.xlsx` Authoring Authority + 共享 `RRPG_VisualAssets.xlsx` + Compiler / Validator / Canonical Mirror / Unity Builder / StaticConfig Registry 的**只读读取入口** | **已完成**（§17 / §18） |
| 1B｜Resolver Foundation | Event 求解（Trigger 过滤 → Repeat / Cooldown → Role Binding → Condition → Weight 抽取）、`ResolvedEventInstance`、只读 World View 契约、World / Expedition 双层 History 读模型、Resolution Trace | **已完成**（§19；不含 Template Renderer 与 History 写入） |
| 1C｜Option / Outcome / Effect | Option 可用性、Outcome 抽取、Effect Runtime Handler、`StartBattle` 调用 Encounter 领域、History 写入 | Planned（未开始） |
| 1D｜Product Cutover | Journey Event Card / Village Social Response 切换到 Event Core；Encounter Pool 由内容驱动；产品 Adapter（只读桥接） | Planned（未开始） |

**1A 不允许改变任何玩家运行行为。** 1A 明确**不进入**：Event Resolver、`ResolvedEventInstance`、
Event History / Save、Option 执行、Effect Runtime、Village Social Response Cutover、
Journey Event Cutover、Battle / Encounter 调用、UI 修改。

---

## 3. 已确认、未来仍 Not Frozen 的边界

以下边界本轮确认，并明确**不是**长期冻结结论：

1. **Event Core 统一，但一条 Event Definition 不必须跨两个产品 Context 使用**：
   V0.1 一个 Enabled Event 的正式 Trigger 只能属于同一个 Context Domain（Village 或 Expedition）。
2. **V0.1 不支持 Group Role**：一个 Role 最终只绑定一个 Person；“不同 Role 默认绑定不同 Person”
   属于 1B Runtime 规则，1A 只做静态表达与形状校验。
3. **V0.1 不支持 `QueueEvent`**：事件不得通过 Effect 排队另一个事件。
4. **一个 Event 必须且只能有一条 Presentation**（第一版）。
5. **所有 Option 必须经过 Outcome**；V0.1 不支持 Outcome Condition（Outcome 不参与条件判断）。
6. **文本模板语法 V0.1 只允许** `{RoleId.DisplayName}` 与 `{Context.Key}`；
   1A **不实现** Template Renderer，只做最小结构检查（见 §8.7）。
7. **VisualAsset Authority 独立化**（本轮执行，见 §5）：通用 `VisualAsset` 只有**一份**人工 Authority。
8. `StartBattle` 的 `TargetKey` 现在**必须**引用 `EncounterPool.PoolId`；正式 Node → Pool 内容绑定
   仍未冻结（沿用 D-020 边界）。
9. 本轮写入 `RRPG_Events.xlsx` 的全部事件内容都是 **Test / Not Frozen**：
   它们只用于验证 Schema，不代表 Event Core 已经接管现有 Village Social Response 或 Journey Event。
10. V0.1 Village Resource 合法 TargetKey 只有 `Ingredients` / `Food`；
    **不得**借事件表或事件效果恢复旧 Wood / Stone / Gold Authority。
11. 事件插图未来来源是 `Development/SourceAssets/Event/*.png`；
    **当前仓库没有正式 Event PNG 时不得凭空生成美术图**（测试事件允许 `IllustrationAssetId` 为空）。

---

## 4. Authority 与依赖（本轮不修改）

| 领域 | 当前 Authority | 本轮动作 |
|---|---|---|
| 静态配置生产架构 | D-019（XLSX + SourceAssets → Compiler → Canonical Mirror → Unity Builder → Registry → Consumer） | 复用，不扩权 |
| Encounter / 敌方阵容生成 | D-020 + `RRPG_Encounters.xlsx` + Monster 工作簿两张 Encounter 子表 | 只被 `StartBattle` **引用**；不修改 |
| Monster Definition | `RRPG_Monsters.xlsx`（M2） | 不改 |
| Enemy Presentation | `RRPG_EnemyPresentation.xlsx`（M3） | 只移除已迁出的 `VisualAsset` Sheet（见 §5） |
| 通用 VisualAsset | **本轮新增** `RRPG_VisualAssets.xlsx / VisualAsset` | Authority 独立化 |
| Event 内容 | **本轮新增** `RRPG_Events.xlsx` | 新领域 |

---

## 5. VisualAsset Authority 独立化

### 5.1 问题

`RRPG_EnemyPresentation.xlsx` 当前同时承载两张表：

- `EnemyPresentation`（`MonsterId → VisualAssetId`）—— 这是**敌方领域**的绑定；
- `VisualAsset`（`VisualAssetId + SourceFile + ImportPreset → Sprite`）—— 这是**通用资产域**的事实。

Compiler 的 Canonical 输出身份直接来自 Sheet 名（`VisualAsset → VisualAsset.json`），
因此如果在 `RRPG_Events.xlsx` 里再建一张 `VisualAsset`，会产生：

- `WB006_DUPLICATE_ENTITY_SHEET`（同一实体名跨 Workbook 重复）；
- 或者（若改名规避）第二套 VisualAsset Authoring Authority 与第二份 `AssetManifest`。

两者都不可接受。

### 5.2 本轮迁移

```text
新增  Development/Config/RRPG_VisualAssets.xlsx
        ├─ 00_导航   （中文说明）
        └─ VisualAsset（原 RRPG_EnemyPresentation.xlsx / VisualAsset 的 6 行**无损迁移**）

RRPG_EnemyPresentation.xlsx
        ├─ 00_导航
        └─ EnemyPresentation（MonsterId → VisualAssetId，引用共享 VisualAsset 实体）
        （VisualAsset Sheet 退出数据 Authority）
```

迁移要求（全部为验收项）：

- Enemy 当前 6 怪的 `VisualAssetId` 全部保持原值；
- `SourceFile` 全部保持原值；
- `SourceSha / AssetManifest` 语义保持（`AssetManifest.json` 仍由 `SourceArtValidator` 生成，
  `managedRootRelative` 仍为 `Assets/Generated/StaticConfig/Assets`）；
- Enemy Presentation Runtime 行为零变化（`MonsterId → EnemyPresentation → VisualAsset → Sprite`）；
- **不建立第二套 VisualAsset Registry**；
- 迁移后 `VisualAsset.json` 的 `source.workbook` 允许由
  `Config/RRPG_EnemyPresentation.xlsx` 变为 `Config/RRPG_VisualAssets.xlsx`（允许的 metadata 变化）；
  业务行必须等价。

### 5.3 跨 Workbook Entity Ref

`CrossReferenceValidator` 的 ref 目标是**全局实体名**（跨 Workbook 构建索引），
因此 `EnemyPresentation.VisualAssetId (ref:VisualAsset)` 与
`EventPresentation.IllustrationAssetId → VisualAsset` 都能直接复用既有机制，无需第二套链。

### 5.4 边界声明（避免过度承诺）

导航里只允许写「所有进入现有 PNG → Managed Sprite 管线的共享静态图片都在此登记」；
**不得**写「所有未来 UI / Prefab / Material / Spine 都必须走这张表」。
当前**已实证**的仍然只是 PNG Sprite。

---

## 6. Authoring Authority：工作簿格式

`Development/Config/RRPG_Events.xlsx` 与 `RRPG_VisualAssets.xlsx` 沿用 D-019 已冻结格式：

| 位置 | 用途 | 能否改 |
|---|---|---|
| A 列 | 制作人中文说明列（A1=`机器字段` / A2=`数据类型` / A3=`规则` / A4=`中文说明` / A5+ 行备注），**不导出** | 可以改 |
| 行 1 | 机器字段（字段身份） | **不要改** |
| 行 2 | 数据类型（`id` / `int` / `float` / `bool` / `string` / `ref:<实体>`） | **不要改** |
| 行 3 | 规则（`optional` / `key` / `default=` / `>=` / `<=` / `allowed=A\|B`） | **不要改** |
| 行 4 | 中文字段名 | 可以改（不导出） |
| 行 5+ | 数据 | 可以改 |

`00_导航` Sheet（被 Compiler 按命名约定跳过）必须用中文解释：每张 Sheet 做什么、所有英文字段的中文含义、
枚举值的中文含义、什么属于 Current、什么属于 Test / Not Frozen、V0.1 不支持什么。
**制作人不应该需要自己逐字段翻译英文。**

---

## 7. 十张业务表

### 7.1 `Event`（事件主表；身份 = `EventId`）

| 字段 | 类型 | 规则 | 说明 |
|---|---|---|---|
| `EventId` | `id` | | 事件ID |
| `DisplayName` | `string` | | 制作人识别名称 |
| `Enabled` | `bool` | `default=false` | 是否启用 |
| `Weight` | `int` | `>=1` | 候选抽取权重（Test / Not Frozen 数值） |
| `RepeatPolicy` | `string` | `allowed=Repeatable\|OncePerExpedition\|OncePerWorld` | 重复规则 |
| `CooldownType` | `string` | `allowed=None\|VillageDays\|ExpeditionNodes` | 冷却类型 |
| `CooldownValue` | `int` | `>=0` | 冷却值 |

`RepeatPolicy`：`Repeatable`（可重复）/ `OncePerExpedition`（每次远征最多一次）/ `OncePerWorld`（整个世界最多一次）。
`CooldownType`：`None`（无）/ `VillageDays`（村庄天数）/ `ExpeditionNodes`（远征节点数）。

校验（DAT033）：

- `Weight > 0`（由 Row 3 `>=1` 声明式保证）；
- `CooldownType = None` → `CooldownValue = 0`；
- `CooldownType != None` → `CooldownValue > 0`；
- `RepeatPolicy = OncePerWorld` → `CooldownType` 必须 `None`；
- `RepeatPolicy = OncePerExpedition` → `CooldownType` 必须 `None`。

Once 策略已经限制一次，不再叠加无意义 Cooldown。

### 7.2 `EventTrigger`（事件触发表；身份 = `TriggerId`）

| 字段 | 类型 | 规则 |
|---|---|---|
| `TriggerId` | `id` | |
| `EventId` | `ref:Event` | |
| `TriggerType` | `string` | `allowed=VillageDaySettled\|VillageRecognitionRaised\|VillageProjectCompleted\|ExpeditionEventNodeReached\|ExpeditionBattleResolved\|ExpeditionReturned\|ManualDebug` |

规则：

- Enabled Event 至少一个 Trigger（DAT031）；
- 除 `ManualDebug` 外，同一个 Event 的正式 Trigger 必须属于同一 Context Domain（DAT034）：
  Village = `VillageDaySettled` / `VillageRecognitionRaised` / `VillageProjectCompleted`；
  Expedition = `ExpeditionEventNodeReached` / `ExpeditionBattleResolved` / `ExpeditionReturned`。
- V0.1 不支持同一个正式 Event 同时既是 Village Event 又是 Expedition Event。

### 7.3 `EventRole`（事件角色表；身份 = `RoleId`，全局唯一）

| 字段 | 类型 | 规则 |
|---|---|---|
| `RoleId` | `id` | |
| `EventId` | `ref:Event` | |
| `RoleName` | `string` | 中文角色名 |
| `SelectorType` | `string` | `allowed=ContextPerson\|VillagePerson\|ExpeditionPartyMember\|FixedPerson` |
| `SourceKey` | `string` | `optional` |
| `FixedPersonId` | `string` | `optional` |

结构校验（DAT036）：

| SelectorType | SourceKey | FixedPersonId | Domain 约束 |
|---|---|---|---|
| `ContextPerson` | 必填 | 空 | 无 |
| `VillagePerson` | 空 | 空 | Event Domain 必须 Village |
| `ExpeditionPartyMember` | 空 | 空 | Event Domain 必须 Expedition |
| `FixedPerson` | 空 | 必填 | 无 |

V0.1：一个 Role 最终只绑定一个 Person；Group Role 不支持。

### 7.4 `EventRoleCondition`（事件角色筛选条件表；身份 = `ConditionId`）

| 字段 | 类型 | 规则 |
|---|---|---|
| `ConditionId` | `id` | |
| `RoleId` | `ref:EventRole` | |
| `ConditionType` | `string` | `allowed=PersonInVillage\|PersonOnExpedition\|PersonAvailable` |
| `TargetKey` | `string` | `optional` |
| `IntValue` | `int` | `optional` |
| `StringValue` | `string` | `optional` |

同一个 Role 下：**全部条件 = AND**。1A 只建立静态表达与字段形状验证，**不实现 Runtime Handler**。

### 7.5 `EventCondition`（事件成立条件表；身份 = `ConditionId`）

| 字段 | 类型 | 规则 |
|---|---|---|
| `ConditionId` | `id` | |
| `EventId` | `ref:Event` | |
| `GroupId` | `int` | `>=1` |
| `ConditionType` | `string` | `allowed=<见下表>` |
| `SubjectRoleId` | `string` | `optional`（条件引用 `EventRole`） |
| `TargetKey` | `string` | `optional` |
| `IntValue` | `int` | `optional` |
| `StringValue` | `string` | `optional` |

逻辑语义：**同 GroupId 内 = AND；不同 GroupId = OR**；没有 EventCondition → Event 没有额外成立条件。

V0.1 `ConditionType` 词表：

| 分类 | ConditionType |
|---|---|
| Context | `ContextStringEquals` / `ContextIntAtLeast` / `ContextIntAtMost` |
| Event World / History | `FlagSet` / `FlagNotSet` / `EventNotSeen` / `EventSeenCountAtLeast` |
| Person | `PersonExists` / `PersonInVillage` / `PersonOnExpedition` / `PersonAvailable` |
| Village | `VillageResourceAtLeast` / `VillageResourceAtMost` |
| Expedition | `ExpeditionDepthAtLeast` / `ExpeditionDepthAtMost` / `SupplyAtLeast` / `SupplyAtMost` / `PartySizeAtLeast` / `PartySizeAtMost` |

Village Resource 的合法 `TargetKey` 只有 `Ingredients` / `Food`。

### 7.6 `EventPresentation`（事件表现表；身份 = `EventId`）

一个 Event 必须且只能有一条 Presentation（身份 = `EventId` 天然保证唯一性）。

| 字段 | 类型 | 规则 |
|---|---|---|
| `EventId` | `id` | |
| `PresentationChannel` | `string` | `allowed=JourneyCard\|VillageModal` |
| `TitleTemplate` | `string` | |
| `BodyTemplate` | `string` | |
| `IllustrationAssetId` | `string` | `optional`（→ 共享 `VisualAsset`） |
| `Urgency` | `string` | `allowed=Timely\|Ignorable` |
| `PauseOnOpen` | `bool` | `default=false` |

`PresentationChannel`：`JourneyCard`（旅途事件卡）/ `VillageModal`（村庄弹窗）。
`Urgency`：`Timely`（需要及时处理）/ `Ignorable`（可忽略）。

规则：

- Village Event → `VillageModal`；Expedition Event → `JourneyCard`；ManualDebug-only Event 可任选合法 Channel（DAT035）；
- `IllustrationAssetId` 非空 → 必须引用共享 `VisualAsset`（DAT042）；
- `TitleTemplate` / `BodyTemplate` 必填（由 required 列保证）。

### 7.7 `EventOption`（事件选项表；身份 = `OptionId`）

| 字段 | 类型 | 规则 |
|---|---|---|
| `OptionId` | `id` | |
| `EventId` | `ref:Event` | |
| `SortOrder` | `int` | `>=1` |
| `LabelTemplate` | `string` | |
| `SubLabelTemplate` | `string` | `optional` |
| `UnavailableMode` | `string` | `allowed=Hide\|Disable` |

规则：Enabled Event 至少 1 个 Option（DAT031）；同一 Event 的 `SortOrder` 不得重复（DAT045）；
`LabelTemplate` 必填。

### 7.8 `EventOptionCondition`（选项条件表；身份 = `ConditionId`）

字段与 `EventCondition` 对应（`OptionId` 替代 `EventId`，类型 `ref:EventOption`）。
逻辑：Group 内 AND / Group 间 OR；没有条件 → Option 默认可用。
`ConditionType` 与 `EventCondition` **共用 V0.1 词表和参数规则**。

### 7.9 `EventOutcome`（事件结果表；身份 = `OutcomeId`）

| 字段 | 类型 | 规则 |
|---|---|---|
| `OutcomeId` | `id` | |
| `OptionId` | `ref:EventOption` | |
| `Weight` | `int` | `>=1` |
| `ResultTextTemplate` | `string` | `optional` |

规则：每个 Option 至少 1 个 Outcome（DAT032）；Weight > 0；
一个 Outcome = 确定结果；多 Outcome = 未来 Runtime 按 Weight 随机；V0.1 不支持 Outcome Condition。

### 7.10 `EventEffect`（事件效果表；身份 = `EffectId`）

| 字段 | 类型 | 规则 |
|---|---|---|
| `EffectId` | `id` | |
| `OutcomeId` | `ref:EventOutcome` | |
| `Order` | `int` | `>=1` |
| `EffectType` | `string` | `allowed=<见下表>` |
| `TargetRoleId` | `string` | `optional`（条件引用 `EventRole`） |
| `TargetKey` | `string` | `optional` |
| `IntValue` | `int` | `optional` |
| `StringValue` | `string` | `optional` |

V0.1 静态词表：通用 `SetFlag` / `ClearFlag`；Village `VillageResourceDelta` / `GrantPersonRest`；
Expedition `SupplyDelta` / `AdvanceExpeditionTime` / `StartBattle`。
**`QueueEvent` 不属于 V0.1。**

结构规则（DAT040 / DAT041 / DAT042）：

| EffectType | 规则 |
|---|---|
| `SetFlag` / `ClearFlag` | `TargetKey` 必填；`TargetRoleId` 空 |
| `VillageResourceDelta` | `TargetKey` ∈ {`Ingredients`,`Food`}；`IntValue != 0` |
| `GrantPersonRest` | `TargetRoleId` 必填；其它无关参数为空（只表达“调用正式 Rest Authority”；具体 Rest 窗口仍服从当前 Village Rule，不在 Event 表另造第二套时间参数） |
| `SupplyDelta` | `IntValue != 0` |
| `AdvanceExpeditionTime` | `IntValue > 0` |
| `StartBattle` | `TargetKey` 必须引用 `EncounterPool.PoolId`；必须是同一 Outcome 下 `Order` 最大的 Effect；一个 Outcome 最多一个 `StartBattle` |

同一 Outcome 的 `Order` 必须唯一。一个 Outcome 可以没有任何 Effect（合法的“只记录玩家选择 / 无额外世界变化”）。

---

## 8. 领域校验规则

### 8.1 领域完整性与空态（DAT030）

- 十张表**全部不存在** = 该领域尚未建立（合法空态，不产生诊断）；
- 只存在其中一部分 = 领域不完整（**Hard Error**）。

### 8.2 Enabled 事件结构完整性（DAT031）

`Enabled = true` 必须：≥1 Trigger、exactly 1 Presentation、≥1 Option、每个 Option ≥1 Outcome。
允许：0 Role、0 EventCondition、0 OptionCondition、Outcome 0 Effect。
`Enabled = false` 允许作为制作人草稿不完整；但**已有行的引用本身仍必须合法**，不允许引用不存在对象。

### 8.3 Repeat / Cooldown 形状（DAT033）

见 §7.1。

### 8.4 Domain / Context 约束（DAT034 / DAT035 / DAT043）

从非 `ManualDebug` Trigger 推导 Event Domain；同一个 Event 不允许同时拥有两种正式 Domain Trigger。
该约束与 §8.2 一样只作用于**已经写出的行**：Disable 的草稿也必须语义自洽（草稿只豁免“结构完整性”）。
完全没有正式 Trigger（例如只有 `ManualDebug`）时**不推导 Domain**，因此不做 Role / Channel / Cooldown / Effect
的域约束。

| Domain | PresentationChannel | 禁止的 Role Selector | 允许的 CooldownType | 禁止的 Effect |
|---|---|---|---|---|
| Village | `VillageModal` | `ExpeditionPartyMember` | `None` / `VillageDays` | `SupplyDelta` / `AdvanceExpeditionTime` / `StartBattle` |
| Expedition | `JourneyCard` | `VillagePerson` | `None` / `ExpeditionNodes` | `VillageResourceDelta` / `GrantPersonRest` |

`SetFlag` / `ClearFlag` 两域通用。这是 V0.1 边界，不代表未来永远不能有跨域复杂事件。

### 8.5 Role 引用与跨 Event 约束（DAT037 / DAT039）

`SubjectRoleId` / `TargetRoleId` 必须属于该 Condition / Option / Event 所在的**同一个 Event**；
不能引用别的 Event 的 Role。引用不存在 → DAT039。

### 8.6 条件与效果的参数形状（DAT038 / DAT040）

对每个条件 / 效果按 `ConditionType` / `EffectType` 校验参数形状：

- 未知 `TriggerType` / `SelectorType` / `ConditionType` / `PresentationChannel` / `RepeatPolicy` /
  `CooldownType` / `UnavailableMode` / `EffectType` **全部 Hard Error**，不得静默接受；
- `VillageResource*` 的 `TargetKey` 只能是 `Ingredients` / `Food`；
- 参数形状错误（该有参数的没有、不该有的却填了）→ Hard Error。

### 8.7 模板最小结构检查（DAT044）

V0.1 只允许未来 Runtime 解析 `{RoleId.DisplayName}` 与 `{Context.Key}`。
1A **不实现** Template Renderer；Validator 只做最小结构检查：

- 大括号必须成对、不得嵌套；
- 花括号内不得为空；
- 花括号内不得出现 `(` / `)` / `;` / `=` / `{` / `}`（明显不是 `{RoleId.DisplayName}` / `{Context.Key}` 形状）；
- 不得出现 `if` / 函数调用 / C# / Lua / 任意表达式脚本痕迹（如 `$(`, `function`, `return `, `=>`）。

完整 Template Validation（键必须解析到真实 RoleId / Context Key）标记为 **1B 完成**。

### 8.8 其他结构规则

- 见 §7（每张表的跨列 / 条件引用 / 取值域规则）；
- 所有引用失败必须能定位 **Workbook / Sheet / Row / Column** 并给出**可操作中文原因**。

---

## 9. Static Cross Reference

| 引用 | 目标 | 机制 |
|---|---|---|
| `EventTrigger.EventId` | `Event` | 声明式 `ref:Event` |
| `EventRole.EventId` | `Event` | 声明式 `ref:Event` |
| `EventRoleCondition.RoleId` | `EventRole` | 声明式 `ref:EventRole` |
| `EventCondition.EventId` | `Event` | 声明式 `ref:Event` |
| `EventCondition.SubjectRoleId` | `EventRole` | **条件性**（可选列 + `EventValidator`） |
| `EventPresentation.EventId` | `Event` | 声明式（`id` 列 = 天然唯一） |
| `EventPresentation.IllustrationAssetId` | `VisualAsset`（共享 Authority） | **条件性**（`EventValidator`） |
| `EventOption.EventId` | `Event` | 声明式 `ref:Event` |
| `EventOptionCondition.OptionId` | `EventOption` | 声明式 `ref:EventOption` |
| `EventOptionCondition.SubjectRoleId` | `EventRole` | **条件性** |
| `EventOutcome.OptionId` | `EventOption` | 声明式 `ref:EventOption` |
| `EventEffect.OutcomeId` | `EventOutcome` | 声明式 `ref:EventOutcome` |
| `EventEffect.TargetRoleId` | `EventRole` | **条件性** + 同 Event 约束 |
| `StartBattle.TargetKey` | `EncounterPool` | **条件性**（`EventValidator`） |

额外约束：`SubjectRoleId` / `TargetRoleId` 必须属于同一个 Event（见 §8.5）。

> 说明：为什么条件性引用不用 `ref:`？RuleParser 明确禁止 `ref` + `optional` 组合
> （两种声明叠加属 Hard Error）。因此沿用 Encounter 领域已验证的模式：
> 该列声明为 `string` + `optional`，由领域 Validator 负责“值是否存在 + 形状”校验。

---

## 10. Compiler / Validator

沿用现有模式：Workbook discovery → Schema → Data validation → Cross ref → Domain Validator →
Canonical Mirror → transactional 0-update on failure。

**新增 `EventValidator`**（事件专用规则不塞进通用 `DataCompiler`），并新增诊断码：

| 诊断码 | 含义 |
|---|---|
| `DAT030_EVENT_DOMAIN_SHAPE` | 领域不完整（缺 Sheet） |
| `DAT031_EVENT_ENABLED_STRUCTURE` | Enabled 事件结构不完整（Trigger / Presentation / Option） |
| `DAT032_EVENT_OPTION_WITHOUT_OUTCOME` | Option 没有任何 Outcome |
| `DAT033_EVENT_REPEAT_COOLDOWN_SHAPE` | RepeatPolicy / CooldownType / CooldownValue 组合非法 |
| `DAT034_EVENT_TRIGGER_RULE` | TriggerType 未知取值；同一 Event 混用 Village 与 Expedition 正式 Trigger |
| `DAT035_EVENT_PRESENTATION_CHANNEL_MISMATCH` | PresentationChannel 未知取值 / 与 Event Domain 不匹配；Urgency 未知取值 |
| `DAT036_EVENT_ROLE_SHAPE` | Role SelectorType / SourceKey / FixedPersonId / Domain 形状非法 |
| `DAT037_EVENT_ROLE_CROSS_EVENT_REF` | Role 引用跨越了别的 Event |
| `DAT038_EVENT_CONDITION_SHAPE` | 条件类型未知 / 参数形状错误 / VillageResource target 非法 |
| `DAT039_EVENT_ROLE_REF_UNRESOLVED` | 条件性 Role 引用不存在 |
| `DAT040_EVENT_EFFECT_SHAPE` | 效果类型未知 / 参数形状错误 |
| `DAT041_EVENT_EFFECT_ORDER` | 同 Outcome 内 Order 重复 / StartBattle 顺序非法 / 多个 StartBattle |
| `DAT042_EVENT_ASSET_REF_UNRESOLVED` | StartBattle 引用不存在的 EncounterPool，或 Illustration 引用不存在的 VisualAsset |
| `DAT043_EVENT_EFFECT_DOMAIN_MISMATCH` | Village / Expedition 专属 Effect 用错域 |
| `DAT044_EVENT_TEMPLATE_SYNTAX` | 文本模板大括号 / 表达式痕迹非法 |
| `DAT045_EVENT_OPTION_SORT_ORDER` | 同一 Event 的 Option SortOrder 重复；UnavailableMode 未知取值 |
| `DAT046_EVENT_CONDITION_TARGET_REF` | 事件历史条件（`EventNotSeen` / `EventSeenCountAtLeast`）的 TargetKey 不是真实 EventId；或 `EventSeenCountAtLeast.IntValue < 1`（WP-Event-1C 补强，§20.8） |

`DAT042` 同时承载 `IllustrationAssetId → VisualAsset` 与 `StartBattle.TargetKey → EncounterPool`
的解析失败；诊断文案分别指明具体引用目标。

---

## 11. Canonical Mirror

新增（`Development/Generated/StaticConfig/`）：

```text
Event.json / EventTrigger.json / EventRole.json / EventRoleCondition.json / EventCondition.json /
EventPresentation.json / EventOption.json / EventOptionCondition.json / EventOutcome.json / EventEffect.json
```

`VisualAsset.json` **仍只有一份**（Authority 搬迁后 `source.workbook` metadata 允许变化，
业务行必须等价）。`AssetManifest.json` 仍使用同一正式输出。

---

## 12. Unity Static Config

### 12.1 Runtime

- 新增 `EventDefinitions.cs`：十张表的只读记录类型（字段名 = Mirror 字段名，一一对应）。
  可选整数参数（`IntValue`）用「值 + `HasIntValue` 是否存在」两个成员表达：Unity 不能序列化 `int?`，
  而「Mirror 里是 null」与「值是 0」是两件不同的事（例如 `VillageResourceDelta` 要求 `IntValue != 0`，
  0 是非法值而不是“没有参数”）。
- 新增 `EventConfigDatabase`（`ScriptableObject`，生成物）：聚合十张表。
- `RRPGStaticConfigRoot`：新增 `eventDatabase` 字段并纳入 `ApplyGeneratedContent`。
- `StaticConfigRegistry`：新增 **lazy / 幂等**的 `TryGetEventConfig` / `EventConfig` / `EventConfigDiagnostic`，
  给出明确诊断（Root 缺失 / Event 库缺失 / 空领域）。
- **无 Editor / AssetDatabase / Scene 依赖。**

1A Runtime 只能“读到 Event Config”。**不得**开始：Resolve Event、绑定 Person、判断 Condition、执行 Effect。

### 12.2 Editor Builder

- 新增 `EventMirrorReader`：读取 10 个 Mirror 文件，做 Builder 自洽性校验
  （结构必须同时存在 / 列集合精确匹配 / rowCount 一致 / 标识不重复 / 必填非空 / 领域引用完整），
  **失败前不写任何产物**；
  枚举取值域**不在 Builder 重复实现**：1A 的 Builder 不按取值分支（只做静态装载），
  若未来 Builder 开始按枚举分支，必须同时把取值域带过来（避免第二份词表漂移）。
- 缺失语义：10 个文件都不存在 = 合法空态（不生成库、不报错）；只存在一部分 = Hard Error；
- `StaticConfigBuilder`：新增 `EventConfigDatabase.asset` 生成根、加入 stale keep 集合、
  纳入 content-signature 与 no-op 判定。

---

## 13. 1A 测试矩阵

### 13.1 Compiler（xUnit，`dotnet test`）

| 组 | 用例 |
|---|---|
| Workbook / Schema | Enabled 缺 Trigger → FAIL；缺 Presentation → FAIL；多 Presentation → FAIL；缺 Option → FAIL；Option 缺 Outcome → FAIL；Disabled 草稿允许不完整 → PASS |
| Repeat / Cooldown | None + 非 0 → FAIL；非 None + <=0 → FAIL；OncePerWorld + Cooldown → FAIL；OncePerExpedition + Cooldown → FAIL |
| Domain | Village + Expedition 正式 Trigger 混用 → FAIL；Village + JourneyCard → FAIL；Expedition + VillageModal → FAIL；Village 用 ExpeditionPartyMember → FAIL；Expedition 用 VillagePerson → FAIL；Village 用 SupplyDelta / StartBattle → FAIL；Expedition 用 VillageResourceDelta → FAIL |
| Role | ContextPerson 缺 SourceKey → FAIL；FixedPerson 缺 FixedPersonId → FAIL；Role ref 跨 Event → FAIL |
| Condition | 未知 ConditionType → FAIL；VillageResource target 非 Ingredients / Food → FAIL；参数形状错误 → FAIL |
| Outcome / Effect | Weight <= 0 → FAIL；同 Outcome Order 重复 → FAIL；StartBattle 不是最后 → FAIL；一个 Outcome 两个 StartBattle → FAIL；StartBattle 引用不存在 EncounterPool → FAIL；无 Effect Outcome → PASS |
| Visual | Event Illustration 引用不存在 VisualAsset → FAIL；Illustration 空 → PASS；迁移后 EnemyPresentation 仍能引用共享 VisualAsset → PASS |
| Template | 大括号不闭合 / 嵌套 → FAIL；`if` / 函数调用痕迹 → FAIL |
| 确定性 | 同输入重复 build = 字节相同 + mtime 不变；`validate` PASS |
| Transaction | 任一 Event Hard Error → Canonical Mirror **0 更新** |

### 13.2 Unity（EditMode）

- Event Builder 定向测试：Mirror → `EventConfigDatabase` 生成、条数与 Mirror 一致、
  no-op 路径、缺失诊断、`StaticConfigRegistry` 只读入口；
- EditMode 全量：Failed = 0；
- **Enemy Regression**：因为 VisualAsset Authority 被迁移，必须验证当前 6 只怪仍通过
  `MonsterId → EnemyPresentation → VisualAsset → Sprite` 正常解析
  （`EnemyPresentationResolverTests` / `EnemyPresentationBattleWiringTests` 保持绿）。
- 1A **不要求** PlayMode / 产品 Play（产品行为不应变化）。

---

## 14. 本轮严禁修改的内容

不得修改：JourneyEventCard gameplay wiring、`JourneyEventPresentationCatalog` 正式消费、
`VillageSocialResponseLog` 运行逻辑、`VillageSocialEventUI` 产品行为、Expedition node event behavior、
Battle / Encounter、Event Save、Village Save、Progression State、Person、Scene / Prefab。

如为 StaticConfig 编译引用需要少量 namespace / asmdef 接线，按 Yellow 处理；但不得产生玩家可见行为变化。

---

## 15. 真实验证（本轮执行顺序）

```text
1) dotnet test（Tools/RRPG.ConfigCompiler/tests）→ 全绿
2) RRPG_检查配置.cmd（rrpg-config validate）→ PASS
3) 关闭 Unity 后 RRPG_一键导表.cmd（build + Unity Builder + verify）→ PASS
4) Unity Refresh / compile / StaticConfig 定向 EditMode / EditMode 全量 → Failed = 0
5) Enemy Regression（MonsterId → EnemyPresentation → VisualAsset → Sprite）
```

---

## 16. Test / Not Frozen 内容

`RRPG_Events.xlsx` 内的第一批内容**只用于验证 Schema**：

- `evt_test_village_temp_draft`（Village Recognition Test）；
- `evt_test_exp_ruined_shrine`（Expedition Shrine Test）。

它们**不代表** Event Core 已经接管现有 Village Social Response 或 Journey Event；
1A **不得**让它们在真实游戏中出现（没有任何 Runtime Consumer）。

---

## 17. 交付状态与结论边界

本轮完成后标记：**`WP-Event-1A Technical Verified / Ready for Coordinator Review`**。

不得：自动进入 1B、Commit、Push、修改产品 UI、宣布 `WP-Event-1 Closed`。

1B / 1C / 1D 的内容一律写为 **Planned Next Checkpoint**，不是 Bug。

## 18. 本轮验证结果与变更文件（执行记录）

### 18.1 真实验证数字（2026-09-26）

| 验证 | 命令 / 方式 | 结果 |
|---|---|---|
| Compiler 单测 | `dotnet test tests/RRPG.ConfigCompiler.Tests/RRPG.ConfigCompiler.Tests.csproj -c Release` | **196 passed / 0 failed**（其中新增 `EventDomainTests` 57 条） |
| 正式配置检查 | `RRPG_检查配置.cmd`（= `rrpg-config validate`） | **PASS**（errors=0；warnings 仅为 5 条「跳过 00_导航」既有提示） |
| 一键导表 | `RRPG_一键导表.cmd`（Compiler build → Unity Builder → verify） | **RESULT: PASS**（Builder stage=`build` result=PASS；`event: events=2 triggers=2 roles=2 roleConditions=1 conditions=2 presentations=2 options=4 optionConditions=2 outcomes=5 effects=6`） |
| Unity EditMode（定向） | `-runTests -testPlatform EditMode -testFilter RRPG.StaticConfig.Tests` | **61 passed / 0 failed**（含新增 `EventConfigBuilderTests` 5 条与 Enemy Regression） |
| Unity EditMode（全量） | `-runTests -testPlatform EditMode` | **815 passed / 0 failed / 0 skipped** |
| Enemy Regression | 由 `EnemyPresentationResolverTests` / `EnemyPresentationBattleWiringTests` / `EventConfigBuilderTests.E5` 覆盖 | 绿：`MonsterId → EnemyPresentation → VisualAsset → Sprite` 仍成立 |
| PlayMode / 产品 Play | — | **本轮未执行**（1A 不允许改变玩家行为，无需产品 Play） |

### 18.2 VisualAsset 迁移等价性证据

* `git diff` 显示 `Development/Generated/StaticConfig/VisualAsset.json` 只变化 **2 行**：
  `_generated.sourceSha256` 与 `source.workbook`（`RRPG_EnemyPresentation.xlsx` → `RRPG_VisualAssets.xlsx`）；
  **6 行业务数据（VisualAssetId / SourceFile / ImportPreset）逐字节未变**；
* `AssetManifest.json` 完全未变（源图内容哈希与 managedRootRelative 不变）；
* `EnemyPresentation.json` 完全未变（绑定数据未动）。

### 18.3 变更文件（精确列表）

**新增**

* `Docs/DevelopmentSpecs/2026-09-26_RRPG_WP-Event-1-Unified-Event-Content-Foundation-FDS-V0.1.md`（本文件）
* `Development/Config/RRPG_Events.xlsx`（事件内容人工 Authority：10 张表 + 中文导航）
* `Development/Config/RRPG_VisualAssets.xlsx`（共享 VisualAsset 人工 Authority：无损迁移 6 行）
* `Development/Generated/StaticConfig/Event.json` 等 **10 个** `Event*.json`（Canonical Mirror）
* `Tools/RRPG.ConfigCompiler/src/RRPG.ConfigCompiler/Core/EventValidator.cs`
* `Tools/RRPG.ConfigCompiler/tests/RRPG.ConfigCompiler.Tests/EventDomainTests.cs`
* `UnityProject/RRPG1.0game/Assets/Scripts/StaticConfig/EventDefinitions.cs`（+`.meta`）
* `UnityProject/RRPG1.0game/Assets/Scripts/StaticConfig/EventConfigDatabase.cs`（+`.meta`）
* `UnityProject/RRPG1.0game/Assets/Editor/StaticConfig/EventMirrorReader.cs`（+`.meta`）
* `UnityProject/RRPG1.0game/Assets/Tests/EditMode/StaticConfig/EventConfigBuilderTests.cs`（+`.meta`）

**修改**

* `Development/Config/RRPG_EnemyPresentation.xlsx`（移除 `VisualAsset` Sheet；重写中文导航）
* `Development/Config/README.md`、`Development/Generated/StaticConfig/README.md`、`Docs/DevelopmentSpecs/README.md`
* `Development/Generated/StaticConfig/VisualAsset.json`（仅 `source.workbook` + 指纹）
* `Tools/RRPG.ConfigCompiler/src/RRPG.ConfigCompiler/Core/{CompilerRunner,DiagnosticCodes,EntityRequirements}.cs`
* `Tools/RRPG.ConfigCompiler/src/RRPG.ConfigCompiler/Program.cs`（authority 提示文案）
* `UnityProject/RRPG1.0game/Assets/Editor/StaticConfig/StaticConfigBuilder.cs`
* `UnityProject/RRPG1.0game/Assets/Scripts/StaticConfig/{RRPGStaticConfigRoot,StaticConfigRegistry}.cs`

**未修改（刻意）**：JourneyEventCard / JourneyEventPresentationCatalog / JourneyEventIllustrationResolver、
VillageSocialResponseLog / VillageSocialEventUI / VillageRuntimeBridge、Expedition 节点行为、
Battle / Encounter 运行逻辑、Save、Progression State、Person、Scene / Prefab、`.gitignore`。

**一次性工具**：创建工作簿用的一次性 CLI 动词 `author-draft-workbooks`（`DisposableWorkbookAuthoring.cs`）
已按 `.clinerules.md` §5「Disposable Tests by Default」在验证完成后删除；
工作簿内容的追溯由 Git 历史承担。

### 18.4 Open Issues / Planned Next Checkpoint

**Planned Next Checkpoint（不是 Bug）**

1. **1B｜Resolver / Runtime Instance / History**：Event 求解、`ResolvedEventInstance`、条件求值、
   角色绑定（含「不同 Role 默认绑定不同 Person」）、`Template Renderer` 与完整模板键校验、Event History / Save。
2. **1C｜Option / Outcome / Effect**：Option 可用性求解、Outcome 按 Weight 抽取、
   Effect Runtime Handler（`StartBattle` 调用 Encounter 领域）。
3. **1D｜Product Cutover**：Journey Event Card 与 Village Social Response 切换到 Event Core；
   Encounter Pool 由内容 / 深度驱动（正式 Node → Pool 绑定仍未冻结）。

**Open Issues（真实观察，不影响 1A 验收）**

1. 事件插图目前**没有正式 Source PNG**，两条测试事件的 `IllustrationAssetId` 为空；
   因此「事件插图 → 共享 VisualAsset → Sprite」这条链目前只在**测试夹具**中被证明（
   `EventDomainTests.Visual_IllustrationReferencingSharedAsset_Passes` 与 `E4`），
   尚未在真实内容上落地。正式 Event 美术到位后需要重新 build。
2. `RRPG_一键导表.cmd` 在 Tuanjie batchmode 下会打印两行
   `.build-state is not a valid directory name. ...`（编辑器自身的路径提示噪音）：
   stage 文件与生成根均正常，属**既有工具噪音**，本轮未修改 Producer 入口。
3. 「一个事件恰好一条 Presentation」目前由两层保证：身份（`EventPresentation.EventId` 的 id 唯一性 → DAT005）
   与 `EventValidator` 的 DAT031（防御性第二道防线）。后者在正常情况下不会被触发，
   属刻意的 defense-in-depth，而不是未使用的死代码。

---

## 19. Technical Checkpoint 1B｜Resolver Foundation（实现契约 + 执行记录）

### 19.1 定位与边界（WP 级硬约束的 1B 展开）

1B 只回答一个问题：**给定 `Trigger + EventContext + ResolutionSeed + 只读世界状态`，
确定性地说清「事件为什么能出现、出现的是谁」。**

**1B 明确不做**（属 Planned Next Checkpoint，不是缺陷）：

| 不做的事 | 归属 |
|---|---|
| 执行玩家 Option | 1C |
| 执行 Outcome / Effect（含 `StartBattle`） | 1C |
| 写 Event History（`RecordOccurrence` / `SetFlag` / `ClearFlag`） | 1C（Option → Outcome → Effect 全部成功之后） |
| Template Renderer（`{Requester.DisplayName}` / `{Context.X}`） | 1D 表现接入前 |
| 产品 Adapter（真实 Village / Expedition / Person 只读桥接） | 1D |
| 修改 Save / SaveVersion / SaveSystem / 任何玩家可见行为 | 不在本 WP 的 1B 范围 |

> 1B 的 Resolver **只读**：解析出事件 ≠ 玩家已选择并执行结果，因此解析**绝不**记录 occurrence。

### 19.2 Resolver 输入契约

`EventResolutionRequest` 恰好四项，且不含时间 / Guid / 随机源：

| 字段 | 语义 |
|---|---|
| `TriggerType` | 触发时机（取值域 = `EventTriggerTypes` 词表，与 `EventTrigger.TriggerType` 一致） |
| `TriggerKey` | 本次触发的**稳定身份**（如 `recognition:TemporaryDraftBuildup:person_031:year_2`）；空值 = InvalidRuntimeConfig |
| `ResolutionSeed` | 唯一随机来源 |
| `Context`（`EventContext`） | **本次为什么触发**的轻量事实集合：String / Int / Person 三类键值 |

`EventContext` **不是**第二套 World State：禁止复制全村资源、全 Person 数据、整个 Progression State。
动态状态（Food / Supply / Person Available …）一律在求解时经只读 World View 读取。

### 19.3 只读 World View 契约（`IEventWorldView`）

1B 建立**纯逻辑只读查询契约**，而不是让 Event Runtime 直接持有 `VillageState` / `AdventureManager`：

| 域 | 查询 |
|---|---|
| Person | `PersonExists` / `PersonInVillage` / `PersonOnExpedition` / `PersonAvailable` |
| Person 候选 | `VillagePersonIds`（村庄候选池）、`ExpeditionPartyPersonIds`（远征队伍候选池） |
| Village | `Ingredients` / `Food` / `VillageDayOrdinal`（当前村庄**绝对日序**） |
| Expedition | `ExpeditionDepth` / `ExpeditionSupply` / `ExpeditionPartySize` / `ExpeditionNodeOrdinal` |
| Event World State | Flags + World Event History（见 §19.4） |

- `VillageDayOrdinal` / `ExpeditionNodeOrdinal` 是**单调序数**：Event Core **自己不算 Gregorian 日期**，
  未来由 1D Adapter 从正式 `VillageTime` 提供；
- 契约要求实现方：只读、无副作用、无随机、无时间依赖，候选池中的 PersonId 必须真实存在；
- 1B 只用 `EventWorldViewSnapshot`（纯数据夹具）验证；**不接正式产品 Adapter**。

### 19.4 双层 Event History 模型与只读边界

| 模型 | 语义支持 | 数据结构 | 本轮 Save |
|---|---|---|---|
| **World Event History** | `OncePerWorld`、`VillageDays` 冷却、`EventNotSeen`、`EventSeenCountAtLeast` | `EventId` + `OccurrenceCount` + `LastOccurredVillageDayOrdinal`（+ `HasLastVillageDay`） | **1B 当时不进 Save**；后续 1D 已接入 `SaveData.EventWorld.History`，当前保存链见技术架构总览 §21.3 |
| **Expedition Event Session State** | `OncePerExpedition`、`ExpeditionNodes` 冷却 | `EventId` + `OccurrenceCount` + `LastOccurredNodeOrdinal`（+ `HasLastNodeOrdinal`） | Runtime Only，**不进 Save** |
| World Flags | `FlagSet` / `FlagNotSet` | `EventWorldState`（Flag 集合） | **不进 Save** |

**V0.1 冻结语义**：`EventNotSeen` / `EventSeenCountAtLeast` **统一查询 World Event History**；
不允许同一个 ConditionType 根据 Context 偷换语义（若未来需要「本次远征没见过」，必须新增明确的 Session ConditionType）。

**只读性**：`WorldEventHistory.SeedOccurrenceForTesting` / `ExpeditionEventSessionState.SeedOccurrenceForTesting` /
`EventWorldState.SeedFlagForTesting` 命名与文档都明确标注为**测试 / 未来 1C 的种子入口**；
Resolver 代码路径**不调用**任何写方法（由 `History_IsReadOnly_DuringResolution` 用例守住）。

本轮**未修改** `SaveData` / `VillageSaveData` / `SaveVersion` / `SaveSystem` / `VillageSocialResponseSaveData`。

### 19.5 运行时语义（正式冻结项）

| 主题 | 冻结语义 |
|---|---|
| 候选生成顺序 | Trigger → 监听该 TriggerType 的 **Enabled** Event → Repeat / Cooldown 预筛选 → 完整 Role Binding → EventCondition → Candidate → 按 `Event.Weight` 抽取一个 → 冻结 `ResolvedEventInstance` |
| **一个 Event 一张票** | 同一 Event 即使有 20 套合法 Person Binding，也只进入候选池 **1 次**（权重不被 Binding / Condition 行数放大） |
| Role Selector | `ContextPerson`（经 `SourceKey` 取 Context 人物，取不到 = 该 Role 无候选）、`FixedPerson`（仍要求 `PersonExists`）、`VillagePerson`、`ExpeditionPartyMember` |
| Role 条件 | 同一 Role 的全部条件 = **AND**；未知条件类型 = 显式拒绝，不静默忽略 |
| **角色唯一性** | 同一个 `ResolvedEventInstance` 内，不同 Role 必须绑定**不同** PersonId；需要同一人时改为引用同一个 RoleId（V0.1 不引入 `AllowSamePerson`） |
| **求解而非抽一次** | Role Solver 在完整候选空间中**回溯**求解：候选顺序由 `(Seed, EventId, RoleId)` 确定性派生，但绝不因第一组失败而宣告无解；评估次数受 `EventResolverOptions.MaxSearchNodes`（默认 20000）保护 |
| 条件组 | 同 `GroupId` 内 **AND**；不同 `GroupId` 之间 **OR**；无 EventCondition = **true** |
| Repeat | `Repeatable` 直通；`OncePerWorld` 查 World History；`OncePerExpedition` 查 Session |
| Cooldown | 只用序数差：`currentOrdinal - lastOrdinal >= CooldownValue` 才 Ready；**绝不使用现实时间 / Unity Time**；从未发生过 = Ready |
| 权重选择 | 仅「已通过 Repeat / Cooldown / Role Binding / Condition」的 Event 进入最终池，按配置 `Weight` 确定性抽取 |
| 确定性 | 禁止 `UnityEngine.Random` / 时间 / Guid / 字典遍历顺序；候选列表进入随机过程前一律**规范排序**（EventId / TriggerId / RoleId / ConditionId / GroupId） |

**判定的边界写法**：`VillageDays` / `ExpeditionNodes` 冷却使用**闭区间**（差值等于冷却值即 Ready）；
`ContextIntAtLeast/AtMost`、`VillageResourceAtLeast/AtMost`、`Expedition*AtLeast/AtMost` 均**包含相等值**。

### 19.6 结果码与 Resolution Trace

**结果码（`EventResolutionCode`）**——「这次没有事件发生」是**合法 Gameplay 结果**，不是异常：

| 码 | 含义 |
|---|---|
| `Success` | 已冻结 `ResolvedEventInstance` |
| `ConfigUnavailable` | 未提供静态事件配置（环境问题） |
| `UnknownTrigger` | 请求的 `TriggerType` 不是 V0.1 已知触发时机 |
| `NoCandidateEvent` | 触发成立但没有合法候选（**正常结果**） |
| `NoLegalRoleBinding` | 有事件命中触发，但全部因「无法求出合法角色绑定」被排除 |
| `ConditionUnsatisfied` | EventCondition 未成立（V0.1 只作为**逐事件 Trace 拒绝阶段**使用，顶层归入 `NoCandidateEvent`） |
| `SearchLimitExhausted` | 搜索上限耗尽（**不是**「无解」） |
| `InvalidRuntimeConfig` | 运行时不变量被破坏（未知枚举 / 悬空引用 / 重复身份 / 缺参数）：显式失败，绝不静默继续 |

**结构化 Trace（`EventResolutionTrace`）**——V0.1 必做，不靠 Console 刷屏：

- 逐事件：`TriggerMatched?` / `RepeatPassed?` / `CooldownPassed?` / `RoleBindingPassed?` /
  `ConditionPassed?` / `IsCandidate?` / `Weight` / `RejectStage` / `RejectReason` / `SearchNodes`；
- 汇总：`EvaluatedEventCount` / `CandidateCount` / `TotalSearchNodes` / `SelectedEventId`；
- `ToText()` 输出形如：

```text
[Event Resolution] trigger=VillageRecognitionRaised key=recognition:... seed=2026 evaluated=2 candidates=1 nodes=3 selected=evt_A
  evt_A OK Candidate Weight=100 roles=1 nodes=3
  evt_B x Cooldown: VillageDays 冷却未满足：3/10 天（当前村庄日序 100，最近发生 97）。
```

Trace 边界：只用于 EditMode / Debug，**不是** Player UI、不写长期 Save、不默认打印完整日志。

### 19.7 程序集边界与确定性随机源

| 程序集 | 角色 | 关键约束 |
|---|---|---|
| `Event.Runtime`（新增，`Assets/Scripts/Event/`） | 纯逻辑 Event 求解核心 | `noEngineReferences = true`；只引用 `StaticConfig.Runtime`（1A 的纯数据定义）与共享随机源；**不引用** UI / Editor / AssetDatabase / Scene / MonoBehaviour / SaveSystem / Village.Runtime / ExpeditionProgression.Runtime / Encounter.Runtime |
| `RRPG.Deterministic.Runtime`（新增，`Assets/Scripts/Shared/Deterministic/`） | **唯一**确定性随机算法实现（SplitMix64 + FNV-1a 稳定哈希 + 排列 / 加权排列 / 子流派生） | 纯逻辑；`Encounter.Runtime` 与 `Event.Runtime` 共同依赖它 |
| `Encounter.Runtime`（修改 asmdef 一行） | 原 RNG 归位 | `RRPG.Encounter.DeterministicRandom` 变为共享实现的**兼容子类**（算法只有一份，Encounter 调用点零改动；由 `DeterministicRandomSharedTests` 守住） |

**为什么不复用 `RRPG.Encounter.DeterministicRandom`**：架构硬约束禁止 Event 依赖 Encounter Gameplay 层。
抽取共享程序集是唯一同时满足「不重复实现」与「不跨层依赖」的做法；`VillageTime` / `VillageResourceState` /
`ExpeditionProgressionState` 等产品真相源则**只通过 1D Adapter 的只读投影**进入 Event Core，Event.Runtime 不直接引用它们。

### 19.8 验证结果与变更文件（执行记录）

#### 19.8.1 真实验证数字（2026-09-26，1B）

| 验证 | 命令 / 方式 | 结果 |
|---|---|---|
| Compiler 单测（未改 Compiler） | `dotnet test tools/RRPG.ConfigCompiler/tests/RRPG.ConfigCompiler.Tests/RRPG.ConfigCompiler.Tests.csproj -c Release -v q` | **196 passed / 0 failed** |
| 正式配置检查 | `RRPG_检查配置.cmd` | **PASS，errors=0 / warnings=5**（与 1A 同数） |
| Unity EditMode（定向：Event） | `-batchmode -runTests -testPlatform EditMode -testFilter RRPG.Event.Tests` | **65 passed / 0 failed / 0 skipped** |
| Unity EditMode（全量） | `-batchmode -runTests -testPlatform EditMode` | **880 passed / 0 failed / 0 skipped**（1A 基线 815 + 1B 新增 65） |
| Unity Compile | 同批 batchmode 日志检查 `error CS` | **0 × error CS** |
| PlayMode | — | **本轮未执行**（1B 不接产品、不改变玩家行为） |

> 环境事实（如实记录）：本机 EditMode 必须**不带** `-quit`（带 `-quit` 时编辑器会在测试开始前退出，
> 只留下 "Batchmode quit successfully now!"）；结果文件写在仓库外，不影响仓库状态。

#### 19.8.2 变更文件（精确列表）

**新增 — Runtime（`Assets/Scripts/Event/`，程序集 `Event.Runtime`，`noEngineReferences = true`）**

* `Event.Runtime.asmdef`
* 输入 / 上下文：`EventResolutionRequest.cs`、`EventContext.cs`、`EventContextSnapshot.cs`
* 只读世界契约：`IEventWorldView.cs`、`EventWorldViewSnapshot.cs`
* 历史读模型：`WorldEventHistory.cs`、`ExpeditionEventSessionState.cs`（内含 `EventWorldState`）
* 结果 / 追踪：`EventRejectStage.cs`、`EventResolutionTrace.cs`、`EventResolutionResult.cs`、`ResolvedEventInstance.cs`
* 静态配置投影：`EventStaticCatalog.cs`、`EventStaticCatalog.Indexing.cs`、`EventStaticCatalog.TriggerIndex.cs`、`EventStaticCatalog.Validation.cs`
* 词表：`EventTriggerTypes.cs`、`EventVocabularies.cs`、`EventConditionTypes.cs`
* 求解器：`EventResolver.cs`、`EventResolver.Selection.cs`、`EventResolver.Validation.cs`、`EventResolver.Pipeline.cs`、
  `EventResolver.Repeat.cs`、`EventResolver.RoleSolver.cs`、`EventResolver.RoleCandidates.cs`、`EventResolver.Conditions.cs`、
  `EventResolver.ConditionEvaluator.cs`、`EventResolver.ConditionWorld.cs`、`EventResolver.ConditionNumeric.cs`
* `EventResolverOptions.cs`

**新增 — 共享确定性随机源（`Assets/Scripts/Shared/Deterministic/`，程序集 `RRPG.Deterministic.Runtime`）**

* `DeterministicRandom.cs`、`RRPG.Deterministic.Runtime.asmdef`

**新增 — 测试（`Assets/Tests/EditMode/Event/`，程序集 `Event.Tests`）**

* `Event.Tests.asmdef`
* 夹具：`EventResolverTestKit.cs`、`EventResolverTestWorld.cs`、`EventResolverSmokeFixture.cs`
* 用例（R1–R26 + 边界）：`EventResolverCoreTests.cs`、`EventResolverEmptyStateTests.cs`、`EventResolverInputTests.cs`、
  `EventResolverRepeatTests.cs`、`EventResolverCooldownTests.cs`、`EventResolverRoleSelectorTests.cs`、
  `EventResolverRolePoolTests.cs`、`EventResolverRoleSolverTests.cs`、`EventResolverSearchLimitTests.cs`、
  `EventResolverConditionGroupTests.cs`、`EventResolverConditionContextTests.cs`、
  `EventResolverConditionFlagHistoryTests.cs`、`EventResolverConditionWorldStateTests.cs`、
  `EventResolverTraceAndWeightTests.cs`、`EventResolverAuthoritySmokeTests.cs`、`DeterministicRandomSharedTests.cs`

**修改**

* `Assets/Scripts/Encounter/DeterministicRandom.cs`（→ 共享实现的兼容子类，算法只有一份）
* `Assets/Scripts/Encounter/Encounter.Runtime.asmdef`（依赖 +1：`RRPG.Deterministic.Runtime`）
* `Docs/DevelopmentSpecs/2026-09-26_RRPG_WP-Event-1-Unified-Event-Content-Foundation-FDS-V0.1.md`（本文件 §19、头部与 §2 状态）
* `Docs/DevelopmentSpecs/README.md`、`Development/Config/README.md`（状态与运行时机语义）

**未修改（刻意）**：全部产品运行时代码（Journey Event Card / Village Social Response / VillageRuntimeBridge /
Expedition 节点行为 / Battle / Encounter 生成 / UI）、Compiler 源码与诊断码、`RRPG_Events.xlsx` /
`RRPG_VisualAssets.xlsx`、Canonical Mirror 与生成根、`SaveData` / `VillageSaveData` / `SaveVersion` / `SaveSystem`、
Person 真相源、Scene / Prefab、`.gitignore`。

**无一次性工具遗留**：1B 未引入任何 Disposable 工具（修复测试文件编码用的脚本写在仓库外 `D:\BBB\_rrpg_runs\`）。

---

## 20. Technical Checkpoint 1C｜Option / Outcome / Effect Foundation（实现契约 + 执行记录）

### 20.1 定位与边界

1C 回答第二个问题：**对一个已经冻结的 `ResolvedEventInstance`，
「玩家现在能不能选这个 Option → 会得到哪个 Outcome → 效果能不能整笔执行 → 执行成功后世界怎么变」。**

正式时序（**冻结**，不得调换）：

```text
OptionCondition 重检（点击此刻的世界）
  ↓ 确定 Outcome（EventInstanceId + OptionId 确定性派生）
  ↓ 构建完整 Effect Plan（按 Order）
  ↓ 对整个有序序列做 Preflight（projected state，含前序效果对后序的影响）
  ↓ 全部通过 → Commit（唯一真实 mutation 点）
  ↓ Record World / Session History
  ↓ Mark EventInstance Completed
  ↓ Success
```

**1C 明确不做**（属 1D 或后续）：

| 不做的事 | 归属 |
|---|---|
| Template Renderer（Title / Body / Result / Option Label 渲染） | 1D（UI 接入前） |
| 真实产品 Adapter（Village Resource / Rest / Supply / Expedition Time / Battle 的真实映射） | 1D |
| Event Node 产品流程、Journey / Village UI 接线 | 1D |
| Save / SaveVersion / World History 持久化 / Legacy SocialResponse 迁移 | 1D（协调层裁决） |

**1C 不修改任何玩家可见行为**：没有产品消费者，没有 UI 变化，没有 Save 变化。

### 20.2 Option 可用性（`EventOptionEvaluator`）

| 结果 | 条件 | 含义 |
|---|---|---|
| `Available` | 条件成立（或该 Option 没有 OptionCondition） | 可被玩家选择 |
| `Hidden` | 条件不成立 + `UnavailableMode = Hide` | UI 应隐藏该选项 |
| `Disabled` | 条件不成立 + `UnavailableMode = Disable` | UI 应显示但不可选 |
| `Invalid` | 未知 ConditionType / 悬空 SubjectRoleId / 未知 UnavailableMode / 配置缺失 | 显式失败，不猜 |

硬约束：

- **动态性**：Option 可用性用**点击此刻**的 World View 判断，**绝不**使用事件创建时的世界快照；
  事件实例只冻结「为什么触发」（Context 快照）与「出现的是谁」（Role 绑定）；
- **单一解释器**：OptionCondition 与 EventCondition 共用同一套语义（1C 把 1B 的条件求值重构成一个
  形状归一（`EventConditionView`）+ 唯一解释器（`EventConditionGroupEvaluator` / `EventConditionEvaluator`））；
- **纯查询**：评估可用性绝不 mutation（不写 Flag / History / 资源）；
- **UI 只消费结果**：不得自己重新实现条件判断。

### 20.3 Outcome 确定性选择（`EventOutcomeSelector`）

- 随机来源**只**派生自 `EventInstanceId + OptionId`（固定 domain salt `event-outcome`，FNV-1a → Seed）；
- 因此同一个实例的同一个 Option，**无论 UI 重开 / 反复预览 / 失败后重试**，永远得到同一个 Outcome
  —— 玩家不能靠开关事件卡重抽；
- 不同实例可以抽到不同（但都合法）的 Outcome；候选按 OutcomeId 规范排序后按 Weight 无放回加权抽取；
- 单一 Outcome 也走同一条统一流程（不做特例）；
- `PreviewOutcome` 与 `Execute` 使用**同一**选择函数：预览结果 = 正式结果，且预览不写任何状态。

### 20.4 Effect Transaction（Plan → Preflight → Commit）

| 阶段 | 语义 |
|---|---|
| **Plan** | 取该 Outcome 的全部 Effects，按 `Order` 规范排序；运行时做**形状第二道防线**（未知 EffectType / 缺参数 / 目标非法 / StartBattle 不是最后 → `InvalidRuntimeConfig`） |
| **Preflight** | 在 **projected state** 上按顺序预计执行整条序列：数值效果（资源 / 补给）的起点来自 Gateway 只读读取，之后由 Event Core 自己累加 —— 因此前序效果会真实影响后序判定；任一效果预计违反业务不变量（变负）→ 整笔失败 |
| **Commit** | 唯一真实 mutation 点。按顺序执行：Flag（Event Core 自己拥有）直接写入 `EventWorldState`；外部效果经 Gateway 的 `Apply*/GrantRest/AdvanceExpeditionTime/StartBattle` |

冻结规则：

1. **Preflight 失败 = 零 mutation**：资源 / 补给 / Flag / History / ExecutionState 全部保持不变；
2. **Preflight 必须考虑顺序**：`+20 Food` 之后的 `-30 Food` 在初始 Food=20 时整笔合法
   （不能只看初始值就拒绝）；
3. **Terminal Effect**：V0.1 唯一 `StartBattle`，必须是 Outcome 的最后一个效果且最多一个
   （编译期 `DAT041` + 运行时 Plan 校验两层）；Terminal Effect 成功开始，整笔 Commit 才算成功；
4. **Commit 真实失败**：返回 `EffectCommitFailed`，**不**写 History、**不**标 Completed、**不**谎报成功；
   1C **不承诺**跨系统万能 rollback —— 前序已提交的效果保持原样并如实报告；
   产品 Gateway 必须在 1D 用「Preflight 通过 ⇒ Commit 应成功」的 Authority 接口兑现该契约；
5. **SetFlag / ClearFlag 幂等**，且其 mutation 只发生在 Commit 阶段（Preflight 绝不提前改）。

### 20.5 外部 Effect Gateway 契约（`IEventEffectGateway`）

Event Core **不拥有** Village Resource / Rest / Supply / Expedition Time / Battle 这些 Authority，
也**禁止**直接引用 Village.Runtime / AdventureManager / ExpeditionProgressionResolver / Battle / Encounter。
因此外部效果只能以契约表达（三组方法：只读投影输入 / 纯查询 / 正式提交）：

| Effect | 1C 语义（Fixtures） | 1D 真实 Adapter 待办 |
|---|---|---|
| `VillageResourceDelta` | `TryGetVillageResource` 读起点 → projected 判定 `>= 0` → `ApplyVillageResourceDelta` | 正值 → `AddIngredients/AddFood`；负值 → `TryConsumeIngredients/TryConsumeFood` |
| `GrantPersonRest` | 经 TargetRoleId 解析到具体人物 → `CanGrantRest(personId)` / `GrantRest(personId)` | 正式 Rest Authority（月末 RestUntil）；**不**直连旧 `VillageSocialResponseLog`（迁移属 1D） |
| `SupplyDelta` | projected `supply + delta >= 0` → `ApplySupplyDelta` | 需先决定是否给 `ExpeditionSupplyState` 增加正式 Add 入口（当前只有 `CanAfford` / `TryConsume`） |
| `AdvanceExpeditionTime` | `IntValue > 0` 语义 + `CanAdvanceExpeditionTime` / `AdvanceExpeditionTime` | 需与 `ExpeditionProgressionState.ExpeditionElapsedDays` 的 exactly-once Node 结算正式组合（1C 绝不直接写它） |
| `StartBattle` | `CanStartBattle(poolId)` / `StartBattle(poolId)`：**不**打开 Battle UI、**不**创建 ActiveEncounter、**不** Enter Node | Event → Encounter → Battle → 回到 Node resolution 的产品生命周期 |

### 20.6 History / Flags / Exactly-once

| 主题 | 1C 冻结语义 |
|---|---|
| **World Event History** | 每次 Option 完整成功后 **+1**（Village 与 Expedition 事件**都**计数，便于 `EventNotSeen` / `EventSeenCountAtLeast` 跨 Context 查询）；记录 `LastEventInstanceId` 与当前村庄日序（取自 World View，**不 invent** 时间） |
| **Expedition Session History** | **只有 Expedition Domain 事件**额外 +1（记录当前远征节点序）；Village 事件**不写** Session |
| **Flags** | Event Core 自己拥有；`SetFlag` / `ClearFlag` 幂等；mutation 只在 Commit 阶段 |
| **Completed Instance** | `EventExecutionState` 以 **EventInstanceId** 为边界（不是 OptionId）：成功一次后任何 Option 都返回 `InstanceAlreadyResolved`；失败**不**标完成，修复条件后可重试 |
| **写入协作** | `RecordOccurrence` / `SetFlag` / `ClearFlag` 是 1C 新增的正式写入口；由 Execution 层调用，**Resolver 绝不调用**（1B 的只读性质不变） |
| **Save** | 1C **不修改** `SaveData` / `VillageSaveData` / `SaveVersion` / `SaveSystem` / `VillageSocialResponseSaveData`；World History / Flags / Completed 仍只存在 Runtime（持久化策略属 1D） |

### 20.7 结果码与 Execution Trace

`EventExecutionCode`：`Success` / `InstanceAlreadyResolved` / `OptionNotFound` / `OptionHidden` /
`OptionDisabled` / `NoOutcome` / `EffectPreflightFailed` / `EffectCommitFailed` / `InvalidRuntimeConfig`。

成功结果携带：`EventInstanceId` / `EventId` / `OptionId` / `OutcomeId` / 每个效果的 preflight 结论与是否已提交 /
`AppliedEffectCount` / `HasTerminalEffect`。

`EventExecutionTrace`（结构化，不刷 Console）示例：

```text
[Event Execution] instance=evtinst_1a2b… event=evt_test_exp_ruined_shrine option=opt_test_exp_investigate stage=Instance
  OptionCondition = Available
  Outcome = out_test_exp_investigate_ok (weight 70, seed 1234)
  Effects = 2 条（Order 升序）
  Effect 10 SupplyDelta +3 : Preflight OK（projected 23）
  Effect 20 StartBattle pool_test_general : Preflight OK
  Commit = Success（2 条效果）
  History = Recorded (world count=1, session count=1)
  Instance = Completed (evtinst_1a2b…)
```

失败示例（Preflight）：

```text
  Effect 10 VillageResourceDelta Food -30 : Preflight FAIL（Food 预计变为 -10（不允许 < 0；expected >= 0 actual projected = -10）：当前 20，本效果 -30。）
  Result = EffectPreflightFailed：Effect Preflight 失败（本次零 mutation：…）
```

### 20.8 编译期补强（DAT046，不改 Workbook Schema）

1B 冻结了「`EventNotSeen` / `EventSeenCountAtLeast` 的 `TargetKey` = 目标 EventId，查询世界长期历史」；
1C 把这条约定**正式加入 `EventValidator`**：

| 规则 | 结果 |
|---|---|
| `EventNotSeen` / `EventSeenCountAtLeast` 的 `TargetKey` 必须指向真实 `Event.EventId` | 否则 Hard Error `DAT046_EVENT_CONDITION_TARGET_REF`（Workbook / Sheet / Row / Column + 可操作中文原因） |
| `EventSeenCountAtLeast.IntValue >= 1` | 否则 Hard Error `DAT046`（0 次与「从未见过」同义，应写 `EventNotSeen`） |
| 覆盖范围 | `EventCondition` 与 `EventOptionCondition` **两张表**（共用同一套 ConditionType 语义） |

保持：Hard Error → Canonical Mirror **0 更新**（由新增 Compiler 测试断言）。
本规则**不新增任何 Schema 列**，只补强既有字段语义。

### 20.9 验证结果与变更文件（执行记录）

#### 20.9.1 真实验证数字（2026-09-26，1C）

| 验证 | 命令 / 方式 | 结果 |
|---|---|---|
| Compiler 单测（含 DAT046 新增 6 条） | `dotnet test tools/RRPG.ConfigCompiler/tests/RRPG.ConfigCompiler.Tests/… -c Release -v q` | **202 passed / 0 failed** |
| 正式配置检查 | `RRPG_检查配置.cmd` | **PASS，errors=0 / warnings=5**（Mirror 未写入 → 1C 未触碰内容） |
| Unity EditMode（定向：Event 1B + 1C） | `-batchmode -runTests -testPlatform EditMode -testFilter RRPG.Event.Tests` | **105 passed / 0 failed**（1B 65 + 1C 新增 40） |
| Unity EditMode（全量） | `-batchmode -runTests -testPlatform EditMode` | **920 passed / 0 failed / 0 skipped**（1B 基线 880 + 1C 新增 40） |
| Unity Compile | 同批 batchmode 日志检查 `error CS` | **0 × error CS** |
| PlayMode | — | **本轮未执行**（1C 不接产品、不改变玩家行为） |

> 环境事实（如实记录，续 1B）：EditMode 必须**不带** `-quit`；
> 删除 `Assets/Scripts/Event/*.cs` 后首次编译会遇到 `Library/Bee` 增量缓存引用旧文件的
> `error CS2001`（既有已知瞬时错误）→ 清 `Library/Bee` 后复跑即消失。

#### 20.9.2 变更文件（精确列表）

**新增 — Runtime 1C（`Assets/Scripts/Event/`，仍为 `Event.Runtime` 纯逻辑程序集）**

* Effect / Gateway：`EventEffectTypes.cs`、`IEventEffectGateway.cs`
* 条件解释器（唯一实现，1B 与 1C 共用）：`EventConditionView.cs`、`EventConditionGroupEvaluator.cs`、
  `EventConditionEvaluator.cs`、`EventConditionEvaluator.World.cs`、`EventConditionEvaluator.Numeric.cs`
* Option / Outcome：`EventOptionAvailability.cs`、`EventOptionEvaluator.cs`、`EventOutcomeSelector.cs`
* 事务 / 执行：`EventEffectPlan.cs`、`EventExecutionState.cs`、`EventExecutionTrace.cs`、
  `EventOptionExecutionResult.cs`、`EventExecutionOptions.cs`
* 执行器分片：`EventOptionExecutor.cs`、`.Context.cs`、`.Validation.cs`、`.Outcome.cs`、`.Plan.cs`、
  `.EffectShape.cs`、`.Preflight.cs`、`.PreflightState.cs`、`.Commit.cs`
* Catalog 扩展：`EventStaticCatalog.Options.cs`、`EventStaticCatalog.OptionIndexing.cs`、
  `EventStaticCatalog.OptionValidation.cs`

**修改 — Runtime**

* `EventStaticCatalog.cs`（新增 9 参数构造重载；5 参数重载保留 → 1B 测试零改动）
* `EventStaticCatalog.Validation.cs`（`Validate()` 增加 Option / Outcome / Effect 表校验）
* `WorldEventHistory.cs` / `ExpeditionEventSessionState.cs`（新增正式写入口 `RecordOccurrence`；
  `EventWorldState` 新增 `SetFlag` / `ClearFlag`；旧 `*ForTesting` 保留为等价别名）
* `EventContext.cs` / `EventContextSnapshot.cs`（实现 `IEventContextReader`）
* `EventResolver.Conditions.cs`（改为委托唯一解释器，**语义与文案不变**）

**删除 — Runtime（被唯一解释器取代）**

* `EventResolver.ConditionEvaluator.cs`、`EventResolver.ConditionWorld.cs`、`EventResolver.ConditionNumeric.cs`（含 `.meta`）

**新增 — Tests（`Assets/Tests/EditMode/Event/`）**

* 夹具：`EventExecutionTestKit.cs`、`EventEffectGatewayFixture.cs`（+`.Queries.cs` +`.Commit.cs`）
* 用例：`EventOptionAvailabilityTests.cs`（C1/C2）、`EventOutcomeSelectionTests.cs`（C3）、
  `EventOutcomeCoverageTests.cs`（C4）、`EventEffectTransactionTests.cs`（C5/C6/C7）、
  `EventEffectProjectionTests.cs`（C12/C13）、`EventEffectPreflightFailureTests.cs`（C11/C17）、
  `EventEffectCommitFailureTests.cs`（C14）、`EventExecutionHistoryTests.cs`（C8/C9/C10）、
  `EventExecutionHistoryFeedbackTests.cs`（C18/C19）、`EventExecutionExactlyOnceTests.cs`（C15/C16）、
  `EventExecutionTraceTests.cs` + `EventExecutionTraceFailureTests.cs`（C20）、
  `EventExecutionAuthoritySmokeTests.cs`（+`.SmokeHelpers.cs` +`.ShapeTests.cs` +`.BehaviorTests.cs`
  +`.BattleTests.cs`，§27）、`EventRuntimeBoundaryTests.cs`（§9/§30 依赖边界守卫）

**修改 — Compiler**

* `Tools/RRPG.ConfigCompiler/src/RRPG.ConfigCompiler/Core/DiagnosticCodes.cs`（新增 `DAT046_EVENT_CONDITION_TARGET_REF`）
* `Tools/RRPG.ConfigCompiler/src/RRPG.ConfigCompiler/Core/EventValidator.cs`（新增事件历史 TargetKey 校验）
* `Tools/RRPG.ConfigCompiler/tests/RRPG.ConfigCompiler.Tests/EventDomainTests.cs`（新增 6 条：未知 EventId / 合法 EventId / 未知 EventId（SeenCount）/ IntValue=0 / OptionCondition 两张表）

**修改 — Docs**

* `Docs/DevelopmentSpecs/2026-09-26_…FDS-V0.1.md`（新增 §20；头部状态；§8 诊断码表补 DAT046）
* `Docs/DevelopmentSpecs/README.md`、`Development/Config/README.md`（1C 状态与内容侧新校验）

**未修改（刻意）**：Journey Event Card / Village Social Response / VillageSocialResponseLog /
VillageRuntimeBridge / ExpeditionUI / AdventureManager / Battle / Encounter 运行逻辑、Save 全链
（`SaveData` / `VillageSaveData` / `SaveVersion` / `SaveSystem`）、`RRPG_Events.xlsx` /
`RRPG_VisualAssets.xlsx`、Canonical Mirror、Person 真相源、Scene / Prefab、`.gitignore`。

---

## 21. Technical Checkpoint 1D｜Product Cutover —— **Gate D0 执行记录**（D1–D3 未执行）

> **状态：`WP-Event-1D Gate D0 Technical Verified`；D1（Village Cutover）/ D2（Expedition Cutover）/
> D3（Presentation + Product Validation）**未执行**，不得宣称 1D 完成或进入 Producer Play Acceptance。**

### 21.1 为什么停在 Gate D0（执行判断，如实记录）

1D 要求把 Village / Expedition 的**玩家可见路径**切换为 Unified Event Core，并跑真实 PlayMode 产品链。
其中：

- **Gate D0 是纯加性（additive）改造**：新 Authority / 新区块 / 新 Host 都在旧路径之外新增，
  旧产品路径逐字不变 → 在任何时刻停止都不会留下半切换状态；
- **D1 / D2 / D3 是同一批产品文件的原地替换**（`VillageRuntimeBridge` 的 Recognition 入口、
  `VillageSocialEventUI` 的动态选项渲染、`ExpeditionUI` / `AdventureManager` 的 Event Node 生命周期、
  Template Renderer / Illustration 切换 + PlayMode P1–P3）。
  这些必须先设计好「一次性切换 + 回退点」，否则会出现「新 Pending 已建立但 UI 仍读旧 Pending」
  这类**玩家可见的半状态**——正是 §39 `Fail Closed` 禁止的情形；
- 本会话预算不足以在**同一会话内**完成 D1–D3 并跑完 3 条 PlayMode 产品链；
  因此按「每个 Gate 验证通过才能继续」的顺序纪律，**在 D0 边界停止**。

### 21.2 Gate D0 已交付（全部为加性、向后兼容）

| § | 交付 | 文件 |
|---|---|---|
| §7 | **VillagePersonRestState**（正式 Rest Authority：Grant / Gate 查询 / 只追加不缩短 / Save+Restore / 幂等旧档迁移） | `Assets/Scripts/Village/Runtime/VillagePersonRestState.cs`（新） |
| §7 | Bridge 接线：`PersonRest` 字段 + `EnsureInitialized` 创建 + Save 区块写出 + Load 恢复与迁移 + `GrantPersonRestUntilMonthEnd`（Event Adapter 用）+ Rest Gate 三处读点改为「新 Authority 优先，旧 Rests 兼容（OR）」 | `Village/VillageRuntimeBridge.cs`（改） |
| §8 | Legacy `VillageSocialResponseSaveData.Rests` → 新 Authority 的**幂等迁移**（不猜 Person、不缩短） | 同上 + `VillageSaveData.PersonRest`（新字段） |
| §3 | **EventWorld 存档区块**：Flags / World History / 至多一条 Pending Village Event（完整 `ResolvedEventInstance`） | `Save/SaveData.cs`（新字段）、`Event/EventWorldSaveData.cs`（新 DTO） |
| §3 | 纯逻辑双向映射（精确写回、身份/绑定/Context 不变、Outcome 不重抽） | `Event/EventWorldPersistence.cs` + `.Restore.cs`（新） |
| §3 | 恢复专用显式写入口（`ClearAll` / `RestoreRecord` / `CreateRecord` / `ClearFlags`） | `Event/EventWorldRestoreApis.cs`（新） |
| §2 | **EventRuntimeBridge**（产品层唯一 Event World Host）：World State + Pending + ExecutionState；`SetPendingVillageEvent` 强制 §12「至多一条 + PendingExists 诊断」；Load 后校验 Pending 事件仍在当前 Static Config | `Assets/Scripts/EventHost/EventRuntimeBridge.cs`（新，Assembly-CSharp） |
| §3/§6 | SaveSystem 接线：`BuildSaveData` 写入 `EventWorld`；Load 恢复（**SaveVersion 保持 1**） | `Save/SaveSystem.cs`（改） |
| §21 | `ExpeditionSupplyState.CanApplyDelta` / `TryApplyDelta`（正负 / 越界 / 溢出零 mutation；Supply 仍是唯一 Authority；旧 `TryConsume` 保持） | `Adventure/Progression/Runtime/ExpeditionSupplyState.cs`（改） |
| §22/§23 | `TryResolveCurrentNode(outcome, additionalElapsedDays, out data)`：时间与补给**同一次 exactly-once 结算**；负数额外时间明确拒绝；旧入口等价 `additionalElapsedDays = 0`；新增 `InvalidAdditionalElapsedDays` 与结算明细字段 `AdditionalElapsedDays` / `TotalElapsedDaysSettled` | `ExpeditionProgressionResolver.cs`、`ExpeditionProgressionResult.cs`（改） |
| §4/§5 | 结构保证：Completed Instance 与 Expedition Session History **不进入**存档结构（由测试断言） | `Assets/Tests/EditMode/EventProduct/EventWorldPersistencePendingTests.cs` |

### 21.3 Gate D0 验证数字（真实）

| 验证 | 结果 |
|---|---|
| Compiler 单测 | **202 passed / 0 failed** |
| `RRPG_检查配置.cmd` | **PASS，errors=0 / warnings=5**（Mirror 未写入 → 内容未改动） |
| Unity EditMode 定向（`RRPG.EventProduct.Tests`，Gate D0 新用例） | **23 passed / 0 failed**（Rest 7 / EventWorld 往返 6 / Supply 5 / Event 时间 5） |
| Unity EditMode 全量 | **943 passed / 0 failed / 0 skipped**（1C 基线 920 + 23；**Village / Expedition / Save / Encounter 回归全绿**） |
| PlayMode | **未执行**（D0 不改变玩家行为；PlayMode 属于 D1–D3 的验收） |

> 环境事实（续 1B/1C）：EditMode 必须**不带** `-quit`；结果文件写在仓库外。
> 一处自测捕获的实现缺陷（已修复）：`ExpeditionResolveResult` 追加新枚举值时误覆盖了既有
> `InsufficientSupply` 成员 → 编译期即被现有代码引用暴露，已恢复并重新验证全绿。

### 21.4 D1–D3 执行计划（下一步，未开始）

| Gate | 计划要点（按 §10–§31） | 关键风险 / 回退点 |
|---|---|---|
| **D1 Village Cutover** | Recognition（`EvaluateDraftBuildupResponse`）不再建 Legacy Pending → 构建 `VillageRecognitionRaised` Trigger（Context: Observer/RecognitionType/ObservedCount，TriggerKey 稳定派生）→ Resolver → Pending；Village World View Adapter（PlayerCollection/occupancy/Resources/VillageTime→单调日序）；Village Effect Gateway（ResourceDelta→Add/TryConsume；GrantPersonRest→新 Rest Authority；Flag→EventWorldState）；`VillageSocialEventUI` 改为 **Event Consumer**（标题/正文/选项数量全部来自配置，动态生成选项按钮，禁用/隐藏按 `UnavailableMode`）；结果文本走 `ResultTextTemplate`；成功后清 Pending → 显示结果；失败保留 Pending + 可操作诊断 | 一次性切换「Pending 建立 + UI 读取」两处；旧 Legacy Pending 只服务旧档收敛（§40 禁止新运行时再建 Legacy Pending） |
| **D2 Expedition Cutover** | Event Node **原子进入**（Validate → Context → Resolver 预解析 → TryEnter → Active Expedition Event）；`ActiveExpeditionEventState`（NodeId / ResolvedEventInstance / PendingExtraElapsedDays / Prepared Roster）；`AdvanceExpeditionTime` 只累积 → Node Resolve 时 `TryResolveCurrentNode(..., extra, ...)` 一次结算；`StartBattle` = 当前 Event Node 的内容结果（Preflight 生成 Roster + Commit 用同一份；Battle Victory/Defeat → Node Success/Defeat + 同一 extra 结算）；投影式 Preflight 必须包含 Node 结算可行性（§25 防软锁）；扫描默认 Graph 所有可达 Event Node 的 projected depth 并保证有候选（§31） | 需先读 `AdventureManager` / `ExpeditionUI` / `JourneyPresentationCycle` 的 Placeholder 路径并给出精确 diff；**不允许**保留 Placeholder fallback |
| **D3 Presentation + Validation** | `EventTemplateRenderer`（仅 `{RoleId.DisplayName}` / `{Context.Key}`，缺失值 Fail + 诊断）；Illustration 走 `EventId → EventPresentation.IllustrationAssetId → VisualAsset`（Formal Event 不再读 NodeId binding）；`JourneyEventCardView` Adapter（ResolvedEventInstance+Config → CardConfig）；Urgency/PauseOnOpen 保持；V1–V10 / E1–E14 集成测试 + Save 回归 + PlayMode P1–P3 | PlayMode 三条产品链是本轮最终验收门槛 |

> **注意（2026-09-26 协调层纠正）**：D0 期间**没有**新增第二个 Legacy Pending 创建入口，但
> **旧产品 Recognition 路径仍然会创建 Legacy `VillagePendingResponse`**（`EvaluateDraftBuildupResponse`
> → `SocialResponse.OpenBuildupResponse` 未改）。
> 「新运行时不再创建新的 Legacy Pending」是 **Gate D1 完成后的目标**，不是 D0 的既成事实。
> D0 只保证：新事件路径（EventRuntimeBridge）不会写入 Legacy Pending。

**注意**：D0 期间没有修改任何玩家可见行为，因此 1D 目前**不能**进入 Producer Play Acceptance。

### 21.5 Gate D1（Village Product Cutover）执行状态 —— **未完成，仅交付前置件**

**给协调层的如实状态（2026-09-26 本会话）**：

| D1 项 | 状态 | 事实 |
|---|---|---|
| §0 D0 措辞纠正 | **已完成** | 见本文件 §21.4 末尾纠正说明：D0 未新增第二个 Legacy Pending 创建入口，但旧 Recognition 路径**仍在**创建 Legacy Pending |
| §10/§11/§12 **Template Renderer** | **已完成并验证** | `Assets/Scripts/Event/EventTemplateRenderer.cs` + `.Resolve.cs` + `EventTemplateRenderResult.cs`（含 `EventPersonDisplayNameResolver` 委托）；只支持 `{RoleId.DisplayName}` / `{Context.Key}`；失败 Fail Closed（MalformedPlaceholder / UnsupportedPlaceholder / RoleNotFound / RoleNotBound / PersonNameUnresolved / ContextKeyMissing）；显示名必须由产品侧提供（Event Core 不知道 Person Authority），解析不到即失败 |
| §7 **Village 绝对日序** | **已完成并验证** | `Assets/Scripts/Village/Runtime/VillageDayOrdinal.cs`（`1年1月1日` = 1；天数 / 闰年全部委托 `VillageTime.DaysInMonth` / `IsLeapYear`；非法 → 0；不使用现实世界时间） |
| §4 Recognition → Event Core 切点 | **未执行** | `EvaluateDraftBuildupResponse` → `SocialResponse.OpenBuildupResponse` 未改 |
| §7 Village World View Adapter / §8 Village Effect Gateway | **未执行** | 未新增产品 Adapter |
| §13–§19 `VillageSocialEventUI` 通用化 / 动态 Option / Result Mode | **未执行** | UI 仍为 Legacy 硬编码 |
| §21–§24 Legacy Pending 收敛迁移 | **未执行** | 旧档 Pending 仍走 Legacy 兼容路径 |
| §29 V1–V18 EditMode 契约测试 | **未执行** | 需要切点先落地才有意义 |
| §30 Targeted PlayMode | **未执行** | 产品链未切换 |

**因此本轮不得写 `WP-Event-1D Gate D1 Technical Verified`，更不得进入 Producer Acceptance。**

**D1 开工前必须先定稿的两件事**：

1. **测试通道**：`VillageRuntimeBridge` / `VillageSocialEventUI` 属 Assembly-CSharp，asmdef 测试程序集**无法直接引用** → V1–V18 必须走既有「反射访问产品入口」模式（Village 测试已有先例），PlayMode 只做 §30 的真实产品链；两者边界必须在写测试前定稿。
2. **一次性切换 + 回退点清单**：Recognition 建立 Event Pending 与 UI 改读 Event Config 必须**同一次**落地，否则会出现「Pending 出现但 UI 不显示」的玩家可见半状态。建议顺序 = Gateway / Adapter（纯加性、可反射测试）→ Recognition → UI → Legacy 收敛迁移 → §20 守卫。

**本轮前置件验证数字（真实）**：

| 验证 | 结果 |
|---|---|
| Compiler `dotnet test` | **202 passed / 0 failed**（未改 Compiler） |
| `RRPG_检查配置.cmd` | **PASS，errors=0 / warnings=5**（Mirror 未写入） |
| Unity EditMode 全量 | **961 passed / 0 failed / 0 skipped**（D0 基线 943 + 新用例 18：Template Renderer 11 / VillageDayOrdinal 7） |
| Unity Compile | **0 × `error CS`** |
| PlayMode | **未执行** |

**自测捕获的两处用例缺陷（已修正；均为用例预期错误，非实现缺陷）**：

1. `{Person.DisplayName}` 被实现**正确**判为 `RoleNotFound`（"Person" 只是一个未定义的 RoleId），原用例误期望 `UnsupportedPlaceholder` → 已拆为两条（真·不支持形状 + 未定义 RoleId 必须明确报错，不得静默放过）；
2. `1年12月30日 + 3 天 = 2年1月2日`，原用例误写成 `3年1月1日` → 已更正（跨月 + 跨年一并覆盖）。

### 21.6 Gate D1-Cutover：旧 SocialResponse 测试清单（§1 强制第一步）

#### 21.6.1 依赖旧 SocialResponse 面的文件（findstr 全仓实测）

**产品代码（Scripts / Editor）**

| 文件 | 角色 |
|---|---|
| `Village/Runtime/VillageSocialResponseLog.cs` | Legacy Authority（fired / pending / ChoiceHistory / Rests） |
| `Village/Runtime/VillageSaveData.cs` | Legacy 存档 DTO（含 D0 新增 `PersonRest`） |
| `Village/Runtime/VillageCommitmentView.cs` | 只读展示（Rest / urgency 文案） |
| `Village/Runtime/VillageTestSocialPatterns.cs` | Test / Not Frozen 数值目录（PatternKey / 阈值 / Urgency / Pause 策略） |
| `Village/Runtime/VillagePersonRestState.cs` | D0 新 Rest Authority（Legacy Rests 的替代者） |
| `Village/UI/VillageSocialEventUI.cs` | Legacy 事件弹窗（Scene Host；两个按钮固定产品语义） |
| `Village/UI/VillageCommitmentUI.cs` | 调度 UI（读 Rest Gate 显示） |
| `Village/VillageRuntimeBridge.cs` | Recognition 入口 + `ResolveSocialResponse` + Rest Gate |
| `Editor/VillageSocialEventUIAuthoring.cs` | Scene 内事件 UI 的 Authoring 辅助 |

**测试分类（A 保留 / B 迁移 / C 改契约）**

| 测试 | 分类 | D1 处置 |
|---|---|---|
| `Tests/EditMode/Village/VillageSocialResponseCoreTests.cs` | **A** | 保留；语义注释改为「Legacy SocialResponse compatibility core」，只保护旧档收敛，**不再**作为正式产品行为证明 |
| `Tests/EditMode/Village/VillageSocialResponseSaveTests.cs` | **A + B** | 旧档 Save 收敛用例保留；凡断言「第三次 Project → Legacy Pending → ResolveSocialResponse」的用例**迁移为 Event Product Contract** |
| `Tests/EditMode/Village/VillageV3CSocialHistoryViewTests.cs` | **A + C** | 只读 View 语义保留；依赖「旧两按钮 / fired 产品 Authority」的断言改为新 Event 契约（不机械改常量） |
| `Tests/EditMode/Event/EventRuntimeBoundaryTests.cs` | **A** | 保留（保护 Event Core 不反向依赖 Legacy） |
| `Tests/PlayMode/Village/VillageSocialEventProductIntegrationPlayTests.cs` | **B** | **必须迁移**为正式 Event Core PlayMode 产品链（§28） |
| `Tests/PlayMode/Village/VillageV3CProductChainPlayTests.cs` | **B** | 经 Legacy Pending 的产品链断言迁移为 Event Pending + History + Rest Authority |

> 原则：**A 保留、B 迁移、C 改契约**；不得为了让旧测试保持绿色而让正式产品路径继续写 Legacy Pending。

#### 21.6.2 **Red / Hard Stop：正式 Event 内容无法渲染（阻断 D1 §27 与 §28）**

**实测内容事实（`Development/Generated/StaticConfig/`，人工 Authority = `Development/Config/RRPG_Events.xlsx`）**：

| 表 | 事实 |
|---|---|
| `EventRole` | 村庄事件 `evt_test_village_temp_draft` 的角色 = **`role_test_village_requester`**（SelectorType `ContextPerson`，**SourceKey `RecognitionPerson`**）；远征事件角色 = `role_test_exp_scout` |
| `EventPresentation` | 村庄 BodyTemplate = **`{RoleId.DisplayName} 已经连续多次承担临时任务，想谈谈接下来的节奏。`**；远征 BodyTemplate 同为 `{RoleId.DisplayName} …`；两事件 TitleTemplate 为纯文本；`PauseOnOpen` = true / false，`Urgency` = Timely / Ignorable |
| `EventOption` | 村庄：`opt_test_village_grant_rest`（SortOrder 1）、`opt_test_village_continue`（SortOrder 2），`UnavailableMode=Hide` |
| `EventOutcome` | 5 条，`ResultTextTemplate` **全部为 null**（→ UI 走 §19 fallback） |
| `EventEffect` | 村庄 GrantRest 效果（Order 1）；远征 4 条（SupplyDelta / AdvanceExpeditionTime / StartBattle） |
| `EventCondition` | 村庄：`ContextStringEquals`，TargetKey `RecognitionType` = `TemporaryDraftBuildup` |
| `EventTrigger` | 村庄：`VillageRecognitionRaised` |

**问题**：模板中的 `RoleId` 是**文档元变量名字面量**，并非真实 RoleId。`EventTemplateRenderer` 会把它当 RoleId `"RoleId"` 查询绑定 → 未命中 → **`RoleNotFound` → Fail Closed（§11/§12）**。后果：

1. **§27（Role 引用提升为 Compiler 硬校验）一旦落地，真实配置立即 Hard Error**（`RoleId` 既不存在、也不属于该 Event）→ `RRPG_检查配置.cmd` 从 PASS 变 RED、Mirror 更新被阻断 —— 这是「落地即红」的中间状态，不允许。
2. **§28 P-D1-1 要求的「Body 含真实 Person DisplayName」在今天的真实内容下不可能成立**：Body 渲染失败，按 Fail Closed 应不显示该卡面 → 产品链无法验收。

**修复不在 Gate D1 代码 Scope 内**：必须变更 **Event Content Authoring Authority**（`RRPG_Events.xlsx` 的 `TitleTemplate/BodyTemplate` 等，把 `{RoleId.DisplayName}` 改为真实 RoleId，如 `{role_test_village_requester.DisplayName}`），并重新生成 Canonical Mirror（内容 SHA 变化）。该变更需明确授权后才能执行。

**另需协调层裁决（§24 要求的真实内容差异）**：新内容 `evt_test_village_temp_draft` 的 `RepeatPolicy` / `CooldownType` 与旧 V3 的「同人同自然年只 fired 一次」语义**不是同一套表达**；D1 正式路径只查 Event History（旧 fired 不再是 Authority）。是否要求「一年一次」由 Event Config 决定，需明确，而不应在产品代码里加 `if LegacyFired then block`。

#### 21.6.3 Red 解除：Event Authoring 修正 + Compiler Hardening（本轮实测）

**授权**：协调层正式授权修改 `Development/Config/RRPG_Events.xlsx`（Event Content Authoring Authority）+ 其 Canonical Mirror + Compiler。

**修正内容（XLSX 单元格级，经 Excel COM 写入并 `Save`）**：

| Sheet | 单元格 | 修正前 | 修正后 |
|---|---|---|---|
| `EventPresentation` | E5 | `{RoleId.DisplayName} 已经连续多次承担临时任务…` | `{role_test_village_requester.DisplayName} 已经连续多次承担临时任务…` |
| `EventPresentation` | E6 | `{RoleId.DisplayName} 注意到半塌的石龛里…` | `{role_test_exp_scout.DisplayName} 注意到半塌的石龛里…` |
| `Event` | G5 / H5 | `VillageDays` / `30` | **`None` / `0`**（§6：不得用全局 VillageDays 冷却，个体「一年一次」由 `ObservedCount == 3` 表达） |
| `EventCondition` | 新增 2 行 | — | `cond_test_village_count_min`（`ContextIntAtLeast` / `ObservedCount` / 3）与 `cond_test_village_count_max`（`ContextIntAtMost` / `ObservedCount` / 3），与既有 `cond_test_village_recognition` **同 GroupId=1 → AND ⇒ `ObservedCount == 3`**（§5） |
| `00_导航` + `EventPresentation.A5` | 文档单元格 | `{RoleId.DisplayName}` / `{Context.Key}` | **`{<RoleId>.DisplayName}` / `{<Context.Key>}`**（§3：不再提供看起来可直接复制的数据示例） |

**Mirror 再生证据**（`RRPG_一键导表.cmd` → build **PASS**，errors=0 / warnings=5）：

- `updated Generated/Event.json`、`Generated/EventCondition.json`、`Generated/EventPresentation.json`（其余 unchanged）
- 复核：`EventPresentation.json` 两条 BodyTemplate 均为真实 RoleId；`Event.json` 村庄事件 `RepeatPolicy=Repeatable` / `CooldownType=None` / `CooldownValue=0`；`EventCondition.json` 两组 `ObservedCount` 条件存在

**Compiler Hardening（§27/§8/§9）**：

- 新诊断 `DAT047_EVENT_TEMPLATE_ROLE_REF`（`DiagnosticCodes.DatEventTemplateRoleRef`）
- `EventValidator.CheckTemplateRoleRefs`：作用于 **5 处**模板字段（Presentation.Title / Presentation.Body / Option.Label / Option.SubLabel / Outcome.ResultText）；`{X.DisplayName}` 的 X 必须是真实 `EventRole.RoleId` **且属于同一 Event**；报错含 Workbook / Sheet / Row / Column / 错误 RoleId / 所属 EventId / 可操作中文原因（并给出本事件真实 Role 示例）
- `{Context.X}` **不**做静态 Key 存在性证明（Key 由 Trigger Adapter 提供），缺失由 Runtime Renderer Fail Close 兜底（§9）
- 6 条新 Compiler 用例：同 Event 真实 Role PASS / 不存在 Role FAIL / 元变量名字面量 FAIL / 跨 Event Role FAIL / 五字段全覆盖 FAIL / `{Context.ObservedCount}` PASS（Mirror 0 更新断言沿用 `AssertNoEventMirror`）
- 既有语料缺陷一并修正：`EventDomainTests` 基线 Presentation 模板由元变量名字面量改为 `{role_village.DisplayName}` / `{role_exp.DisplayName}`

**验证数字（本轮实测）**：

| 验证 | 结果 |
|---|---|
| Compiler `dotnet test` | **208 passed / 0 failed**（1C 基线 202 + §27 新用例 6） |
| `RRPG_一键导表.cmd` | **PASS**，errors=0 / warnings=5，Mirror 3 文件更新 |
| `RRPG_检查配置.cmd`（DAT047 生效后复跑） | **PASS**，errors=0 / warnings=5，Mirror 与 Authoring 一致 |

**自测捕获的用例缺陷（已修正）**：`TemplateRoleRef_LiteralMetavariableName_Fails` 首版把占位符错列到 `IllustrationAssetId`（Fixture 列序误用），先触发 DAT042 而非 DAT047 → 已修正为 Body 列并重跑全绿。

### 21.7 Gate D1-Cutover · Phase A（Additive Preparation）—— 落地记录

**授权与阶段**：协调层本轮授权「Phase A 加性准备 → Phase B 原子产品切换」。本节只记录 **Phase A**。

**已落地的新增产品件（纯加性；不改变任何现有产品行为）**：

| 文件 | 内容 |
|---|---|
| `Assets/Scripts/EventHost/VillageEventWorldView.cs`（新，Assembly-CSharp） | `IEventWorldView` 的 Village 产品 Adapter：Person 四问 + 村庄候选池读取 `PlayerCollection` + `VillageRuntimeBridge.GetPersonAvailability / IsPersonTemporarilyOccupied / ExpeditionPersonIds`；资源读取 `VillageRuntimeBridge.Resources`；日序走 `VillageDayOrdinal.FromVillageTime(VillageRuntimeBridge.Time)`；**Expedition-only 字段返回明确非法哨兵 `-1`**（`UnknownExpeditionValue`），不伪造 |
| `Assets/Scripts/EventHost/VillageEventEffectGateway.cs`（新，Assembly-CSharp） | `IEventEffectGateway` 的 Village 产品 Adapter：`VillageResourceDelta` → `AddIngredients/AddFood` / `TryConsume*`（不足则失败且零部分扣除）；`GrantRest` → `VillageRuntimeBridge.GrantPersonRestUntilMonthEnd`（**不写 Legacy `SocialResponse.Rests`**）；`SupplyDelta` / `AdvanceExpeditionTime` / `StartBattle` 与 `TryGetExpeditionSupply` 一律 **Fail Closed**（跨域拒绝，含明确中文原因） |
| `Assets/Tests/EditMode/EventProduct/VillageEventAdapterTests.cs`（新，9 用例） | 以仓库既有模式（`Type.GetType("X, Assembly-CSharp")` + 公共接口断言）覆盖：Person 四问 / 村庄候选池随远征登记变化 / 资源与日序来自 Authority / Expedition 字段非伪造哨兵 / 资源增量正负与失败语义 / GrantRest 写新 Rest Authority 且跨月恢复且 Legacy Rests=0 / 未知人物 Fail Closed / Expedition-only 效果 Fail Closed |

**边界**：`RRPG_检查配置.cmd` 与 Compiler 不受影响（未改 Compiler/内容）；Village 产品现有路径（Legacy `SocialResponse`）**未被改动**，因此不存在半切换状态。

**Phase A 验证状态（如实）**：EditMode 运行已提交（`D:\BBB\_rrpg_runs\d1c\` → `full3b.log` / `full3.xml` / `summary3.txt`），但**结果未在本次会话内观测到**（文件尚未落地）→ **Phase A 不得视为已验证**。上一次会话内实测的 EditMode 全量为 **961 / 961 PASS**，那是新增这 3 个文件**之前**的基线。

### 21.8 `World == null` Fail Closed 裁决落地（Gate D1 · Phase A 补充）

**协调层裁决**：`IEventWorldView` 不可用时，任何**依赖 WorldView 的** Condition 绝不能成立；不得把 `World == null` 当作 `0` 参与比较。

**落地**（唯一解释器，最小修改）：

| 位置 | 变更 |
|---|---|
| `EventConditionEvaluator.Numeric`（VillageResource*） | 新增 `World == null` → `EventConditionOutcome.Fail` + 「IEventWorldView 不可用，无法求值 …（Fail Closed，不按 0 猜测）」；随后直接读 `World.Ingredients/Food`（删除 `?: 0`） |
| `EventConditionEvaluator.Numeric`（Expedition*：Depth / Supply / PartySize） | 同上；另保留上一轮加入的 **负数哨兵 guard**（`actual < 0` → Fail） |
| `EventConditionEvaluator.World`（Person*） | **审计结论：无需修改** —— 既有实现已是 `context.World != null && …`，天然 Fail Closed |
| `EventConditionEvaluator.World`（Flag* / EventNotSeen / EventSeenCountAtLeast） | **不改**（它们读 `EventWorldState / History`，与 WorldView 无关；`WorldState == null` 时 `OccurrenceCount` 返回 0 属既有语义） |
| Workbook / Schema | **未改**（符合裁决：不是 Schema 变化） |

**新增用例**（`EventConditionUnavailableWorldValueTests`）：World=null 时 `VillageResourceAtMost/AtLeast`、`ExpeditionDepthAtMost`、`SupplyAtMost`、`PartySizeAtMost` 一律 **不成立**（顶层码 `InvalidRuntimeConfig`）；Context-only 条件在 World 正常时仍按原语义成立（反向对照）。

**协调层最终裁决（2026-09-26，本节结论）**：

1. **1B 顶层输入契约维持不变**：`IEventWorldView == null` → `EventResolutionCode.InvalidRuntimeConfig`，**不放宽**。
   ⇒ 此前「`World == null` 时 Context-only 条件仍须在 Resolver 中正常求值」的要求**正式撤销**（属协调层过宽要求，不再是产品契约）。
2. **Condition 层保留纵深 Fail-Closed**（本文档上一段所述）：World 缺失时 VillageResource* / ExpeditionDepth* / Supply* / PartySize* / Person* 一律不成立；Person 条件既有 `context.World != null && …` 已天然满足，无需重复改动；Expedition `-1` 哨兵 guard 继续保留。
3. **Context-only 条件不冻结 null-world 行为**：`ContextStringEquals` / `ContextIntAtLeast` / `ContextIntAtMost` 在正式产品路径中始终运行在「WorldView 已通过顶层输入校验」之后；解释器被单独直接调用时的行为**不构成公共契约**，也不得为测试制造「Resolver + World=null + Context 条件成功」语义。
4. **History / Flag 语义不变**：`FlagSet` / `FlagNotSet` / `EventNotSeen` / `EventSeenCountAtLeast` 读 `EventWorldState` / `WorldEventHistory`，本裁决不涉及。

**验证状态**：上述改动**尚未运行验证**（本会话内的 EditMode 运行结果未落地）；不得视为 Phase A Verified。
**（承 §21.7）Phase A 之后追加的运行时防御**：`EventConditionEvaluator.Numeric` 的 Expedition 数值分支新增集中式 **unavailable guard** —— WorldView 数值 `< 0`（例如 Village `UnknownExpeditionValue = -1`）时条件一律**不成立**（`EventConditionOutcome.Fail`），绝不参与比较，避免 `ExpeditionDepthAtMost 3` 因 `-1 <= 3` 变成 **Fail Open**；各 Adapter 不得自行发明比较语义。新增 5 条用例（`EventConditionUnavailableWorldValueTests`：Depth / Supply / PartySize 的哨兵在 AtMost 与 AtLeast 下均不得成立，且合法数值语义不变）。

---

### 21.9 Gate D1-Cutover · Phase B（原子产品切换）—— 落地记录

> **本节记录 Gate D1 Phase B（B1–B8）的真实落地与验证事实。**
> §21.8 末尾的「尚未运行验证」是当时截面；Phase A 已于本次会话完成验证（见 §21.9.4）。

#### 21.9.1 正式产品路径切换（B1 Recognition Cutover / B5 Legacy Authority Exit）

真实时序（**已切换**，冻结）：

```text
VillageSocialFactLedger.CountTemporaryCommitmentsInYear  （真实成功抽调事实）
  ↓ VillageRuntimeBridge.EvaluateDraftBuildupResponse
VillageEventFlow.RaiseRecognition(personId, RecognitionType, ObservedCount, Year)
  ↓ EventResolutionRequest(TriggerType=VillageRecognitionRaised,
      TriggerKey = "village-recognition|<RecognitionType>|<PersonId>|year:<Year>",
      ResolutionSeed = FNV-1a(TriggerKey)，Context = RecognitionPerson / RecognitionType / ObservedCount)
  ↓ EventResolver.Resolve(catalog, VillageEventWorldView, EventRuntimeBridge.World, sessionState)
  ↓ EventRuntimeBridge.SetPendingVillageEvent（V0.1 至多一条；已存在 → PendingExists，不排队不覆盖）
```

* **`SocialResponse.OpenBuildupResponse` 已退出正式产品路径**：新产品路径**不创建 Legacy Pending、不写 Legacy `fired`**（§40）。
* `EventResolver` 仍是**唯一**求解入口：Trigger → Repeat/Cooldown → Role Binding → EventCondition → 确定性抽取。
* 「没有候选事件」= 正常 Gameplay 结果（例如 `ObservedCount == 3` 未成立），**不是错误**，也不产生 Pending。
* 配置不可用 / 运行时不合法 → **Fail Closed**（不建 Pending；诊断落在 `VillageEventFlow.LastDiagnostic`）。
* Legacy `fired` 不再是 Authority：重复抑制由 Event 配置表达（当前测试事件用 `ObservedCount == 3` ⇒ 同人同自然年只发生一次）；**未**在产品代码中加入 `if LegacyFired then block`。

#### 21.9.2 表现与执行（B2 VillageSocialEventUI / B3 Option / Effect）

`VillageSocialEventUI`（Scene Host 与类名保持）现为**双模式**组件：

| 模式 | 触发条件 | 数据来源 |
|---|---|---|
| **New Event Mode（正式）** | `EventRuntimeBridge.PendingVillageEvent != null` | 标题 / 正文 / 选项文案全部来自 Event 静态配置（唯一 `EventTemplateRenderer` 渲染）；选项可用性来自 `EventOptionEvaluator`（`Hide` 不显示、`Disable` 显示不可点、`Invalid` 显示不可点 + 诊断）；`PauseOnOpen` / `Urgency` 由 `EventPresentation` 决定 |
| **Legacy Compatibility Mode（兼容）** | 无新 Event Pending 且存在 Legacy Pending | 原 V3B 呈现（只服务无法迁移的旧档 Pending；不新增 Legacy 写入） |

* **不再硬编码两个产品选项**：运行时按配置生成选项按钮（名称 `EventOption_<OptionId>`，顺序 = `SortOrder`）；Legacy 两按钮仅作视觉模板并退出 New Event Mode。
* 点击选项 = `EventOptionExecutor.Execute`：OptionCondition 重检 → Outcome 确定性抽取 → Effect Plan → Preflight（失败 = 零 mutation）→ Commit（唯一 mutation 点）→ History → MarkCompleted。
* 成功 → 清除 Pending + 结果模式（`EventOutcome.ResultTextTemplate` 渲染；模板为空时给**通用可追溯结果行**）；失败 → **保留 Pending** + 可操作诊断（不谎报成功、不静默丢弃玩家选择）。
* 当前测试内容 `ResultTextTemplate` 全为 null → 真实走通用结果行（含 EventId / OutcomeId / 应用效果条数），**不是**错误。
* Commitment 面板的 Rest 只读展示与「恢复窗口」回看行改读**正式 Rest Authority**（`VillagePersonRestState`），与 Rest Gate 同口径（新权威优先，旧 `SocialResponse.Rests` 兼容）。

#### 21.9.3 旧档迁移与存档（B4 Legacy Pending Migration / B8 Save）

**已知映射**（现行唯一映射，Test / Not Frozen）：

| Legacy | Unified Event |
|---|---|
| `PatternKey = TemporaryDraftBuildup` | `EventId = evt_test_village_temp_draft` |
| `PendingResponse.PersonId` | `RoleId = role_test_village_requester`（SelectorType / SourceKey 取**当前配置**真实值，`ContextPerson` / `RecognitionPerson`） |
| `PendingResponse.Year` | TriggerKey / Seed / Context（ObservedCount 取该人该年账本计数，缺失时退回 Pattern 阈值） |

> **旧 Pending 是已经发生的事实**：迁移**不重跑** Resolver / Weight / EventCondition；
> `EventInstanceId` 由旧档事实稳定派生（`evtinst_legacy|evt_test_village_temp_draft|<legacy.EventId>`）。
> 未知 PatternKey / 内容缺失 → **保留 Legacy 兼容路径**（不静默删除、不猜映射）。

* 调用点：`SaveSystem.Load` 恢复 `EventWorld` 之后（`VillageEventFlow.ResetSessionState` + `TryMigrateLegacyPending`）。
* 迁移成功后清除 Legacy Pending（`VillageSocialResponseLog.ClearLegacyPendingForMigration`，**迁移专用**入口；不清 `fired` / 不重写历史）。
* 存档语义（`SaveVersion` 仍为 **1**，无 Schema 破坏）：
  * Pending：`Save → reset → Load` 保持 `EventInstanceId` / `EventId` / `TriggerKey` / `ResolutionSeed` / Role 绑定 / Context；`Outcome` 仍由 `EventInstanceId + OptionId` 派生，**不重抽**。
  * 结算后：`Save → Load` 后 Pending **不回来**，World Event History 与正式 Rest 后果都保留。
  * `EventExecutionState`（Completed 集合）**不进 Save**（Runtime Only，§4）；长期重复规则一律由 World Event History 负责。
  * Village 求解所需的会话输入由 `VillageEventFlow.SessionState` 提供（Runtime Only，不进 Save；Village V0.1 事件不使用 `ExpeditionNodes` 冷却）。

#### 21.9.4 Gate D1 验证数字（真实；Phase A 复核 + Phase B）

| 验证 | 命令 / 方式 | 结果 |
|---|---|---|
| **Phase A 复核**（Fixture 修正后） | `Tuanjie.exe -batchmode -runTests -testPlatform EditMode`（`D:\BBB\_rrpg_runs\d1c\` → `result.txt`） | **977 / 977 PASS，Failed = 0，Skipped = 0，ERROR_CS_COUNT = 0** |
| Compiler 单测 | `dotnet test Tools/RRPG.ConfigCompiler/tests/RRPG.ConfigCompiler.Tests -c Release`（`compiler_test.txt`；本轮未改 Compiler / 内容，仍按实测记录） | **208 passed / 0 failed / 0 skipped** |
| 定向 EditMode（本 WP 产品契约） | `-testPlatform EditMode -testFilter RRPG.EventProduct.Tests`（`prod.txt`） | **53 / 53 PASS，Failed = 0，ERROR_CS_COUNT = 0** |
| 全量 EditMode | `-testPlatform EditMode`（`d1b_full_edit.txt` / `.xml`） | **990 / 990 PASS，Failed = 0，Skipped = 0，ERROR_CS_COUNT = 0** |
| 定向 PlayMode（三条真实产品链） | `-testPlatform PlayMode -testFilter VillageEventProductChainPlayTests\|VillageSocialEventProductIntegrationPlayTests\|VillageV3CProductChainPlayTests`（`play.txt`） | **6 / 6 PASS，Failed = 0，ERROR_CS_COUNT = 0** |
| 全量 PlayMode | `-testPlatform PlayMode`（`d1b_full_play.txt`） | **76 条：71 PASS / 5 FAILED**；5 条已实测证明为 **precise pre-existing baseline failure**（见下方专项核查），ERROR_CS_COUNT = 0 |

**B7 真实产品链证据（`EVENT_CHAIN_REPORT`，取自 `play.log` / `d1b_full_play.log`）**：

```text
EVENT_CHAIN_REPORT [Chain1-GrantRest] PersonId=person_dca4329…c | DisplayName=林恩 | EventId=evt_test_village_temp_draft
  | Title=村庄里有人私下提起最近的安排
  | Body=林恩 已经连续多次承担临时任务，想谈谈接下来的节奏。
  | OptionId=opt_test_village_grant_rest | OutcomeId=out_test_village_grant_rest | History=1 | RestActive=True

EVENT_CHAIN_REPORT [Chain2-NoEffectContinue] PersonId=person_d3ba11f…a | DisplayName=林恩 | EventId=evt_test_village_temp_draft
  | Body=林恩 已经连续多次承担临时任务，想谈谈接下来的节奏。
  | OptionId=opt_test_village_continue | OutcomeId=out_test_village_continue | History=1 | RestActive=False

EVENT_CHAIN_REPORT [Chain3-ExactlyOnce] PersonId=person_64d2b2a…c | DisplayName=林恩 | EventId=evt_test_village_temp_draft
  | OptionId=opt_test_village_continue | OutcomeId=（第二次执行为空） | History=1 | RestActive=False
```

> 说明：PersonId 每次运行不同（新档随机身份）；显示名 / 标题 / 正文均为**真实渲染结果**（正文 = 配置模板 + 该 Person 的正式 DisplayName）。

**本次全量 PlayMode 的 5 条失败（已由后续 `WP-Event-1D-R` 修复；此处保留原始事实）**：

| 用例 | 域 | 现象（修复前） |
|---|---|---|
| `BattleDualBoardProductPlayTests.ProductBattle_DownedUnit_ReleasesCell_AndNewUnitCanEnter` | Battle | 「Enemy Board 占位必须与单位坐标一致」断言 `Expected: same as <BattleUnit> / But was: null` |
| `BattleDualBoardProductPlayTests.ProductBattle_SameCoordinateCoexists_MoveIgnoresOpposingBoard` | Battle | 同上 |
| `BattleMilestoneAPlayTests.V1DamageCard_RangeCheck_BlocksOutOfRange` | Battle | `RangeModifier 生效后应结算伤害`：`Expected: less than 5 / But was: 5`（且被当作 Unhandled log message） |
| `JourneyVisualFoundationPlayTests.Journey_ConsecutiveTransitions_OnePerCycle_AndBranchNotAutoSelected` | Expedition / Journey | `[ExpeditionUI] 拒绝进入战斗节点（InsufficientSupply）` 被当作 Unhandled log message |
| `VillageV4ProgressionClosurePlayTests.Progression_InsufficientSupply_ShowsVisibleReason_AndRetreatReturnsToTown` | Expedition / Journey | 同上 |

**处置**：这 5 条既不在 Gate D1 的修改面（Event Host / Village Recognition / Event UI / Commitment 只读展示 / Save 迁移钩子）内，也不属本 WP Scope；本 WP 只如实记录 → 交协调层另立测试修复任务 → **已由 `WP-Event-1D-R` 完成修复（仅改测试 / 测试 helper），见 §21.10**。

> **分类结论（2026-09-26 专项核查，Gate D1 后）**：这 5 条是 **pre-existing baseline failure**，不是 D1 regression，也不是 full-suite 顺序 / 状态污染。

**核查证据（全部为本会话真实运行）**：

| 步骤 | 方式 | 结果 |
|---|---|---|
| ① 失败明细 | 解析 `d1c\d1b_full_play.xml`（`failed_cases_detail.txt`） | 5 条，分为 **3 组根因**（见下）；stack trace 首处均为**测试侧断言 / 测试侧 helper**，不经过 Event 代码 |
| ② 隔离运行（当前工作区） | `-testPlatform PlayMode -testFilter <5 个精确方法名>`（`unrelated.txt` / `.log`） | **TOTAL=5 PASSED=0 FAILED=5**，`ERROR_CS_COUNT=0`；日志中 `VillageEventFlow / EventRuntimeBridge / evt_test_village_temp_draft / PendingVillageEvent` **零命中** → 不是顺序/状态污染 |
| ③ 基线对照（**关键**） | 临时 `git worktree`（detached **ba59be6**，独立目录，末后已 `worktree remove --force` 清理）+ 同一过滤器、同一 PlayMode 平台（`baseline.txt` / `.xml` / `.log`） | **TOTAL=5 PASSED=0 FAILED=5**，`ERROR_CS_COUNT=0`，**失败用例与失败消息逐字一致**（stack trace 指向基线目录内的同一文件行号） |

**根因分组（3 组，均属 Battle / Expedition 域；测试与产品文件都不在本 WP 改动清单内）**：

| 组 | 用例 | 首处失败点 | 机制（由代码读取确认） |
|---|---|---|---|
| A | `BattleDualBoardProductPlayTests.ProductBattle_DownedUnit_ReleasesCell_AndNewUnitCanEnter`、`…SameCoordinateCoexists_MoveIgnoresOpposingBoard` | `BattleDualBoardProductPlayTests.AssertNoSecondPositionAuthority`（`BattleDualBoardProductPlayTests.cs:106`）—— 两条用例共用同一 helper，故同一根因 | 测试把 `StaticMonsterTestData.Build()`（= `StaticConfigRegistry.Monsters.All`，当前 **6 只**）整体注入 `BattleUI.InitFromExpedition` 的 Legacy 列表路径；敌方为 3×3、每排 ≤3（`BattleSystemEnemyRoster. TryBuildEnemyUnitPlan`），超出容量的敌方单位出现在 `EnemyUnits` 但**没有对应 Board 格** → `BattleField.GetUnitAt(row,col)` 返回 `null` |
| B | `BattleMilestoneAPlayTests.V1DamageCard_RangeCheck_BlocksOutOfRange` | `BattleMilestoneAPlayTests.RunStage3ACardRange`（`BattleMilestoneAPlayTests.cs:1265`） | 同一 6 只敌方阵容改变了阻挡排数：`RangeModifier=1` 仍无法跨过阻挡 → 伤害未结算（断言在测试协程中抛出 → 以 Unhandled log message 形式失败） |
| C | `JourneyVisualFoundationPlayTests.Journey_ConsecutiveTransitions_OnePerCycle_AndBranchNotAutoSelected`、`VillageV4ProgressionClosurePlayTests.Progression_InsufficientSupply_ShowsVisibleReason_AndRetreatReturnsToTown` | 测试侧协程（`JourneyVisualFoundationPlayTests.AdvanceToFirstNode` / `VillageV4ProgressionClosurePlayTests.AdvanceStep`） | 测试预期的「补给不足 → 拒绝进入节点」产品日志 `[ExpeditionUI] 拒绝进入战斗节点…InsufficientSupply` 未被 `LogAssert.Expect` 包裹 → Unity Test Framework 判为 Unhandled log message（**测试侧 LogAssert 严格性缺口**，非产品行为变化） |

**处置（按 `.clinerules.md` §7 Hard Stop 与「最小修改」）**：

* 本轮**未修改**这 5 条测试代码，也**未修改** Battle / Journey / Expedition 产品逻辑（它们既不在 Gate D1 改动面，也不属本 WP Scope）；
* 已用基线实测把它们从「疑似 D1 影响」降级为 **precise pre-existing baseline failure**；
* 是否修复（A/B：测试应改为注入与本场敌方容量一致的阵容或改走 `InitializeBattleFromEnemyRoster`；C：补 `LogAssert.Expect`）由协调层决定，建议另立 Battle / Journey 测试修复任务，不并入本 WP。

#### 21.9.5 变更文件（精确列表）

**新增 — EventHost（`Assets/Scripts/EventHost/`，Assembly-CSharp）**

* `VillageEventStaticConfigAdapter.cs`：`EventConfigDatabase` → `EventStaticCatalog` 只读投影 + Presentation / Role 只读查询（不缓存半新半旧快照）。
* `VillageEventFlow.cs`：Recognition → Resolver → Pending；Option 执行；模板渲染与选项视图；结果文案；**Legacy Pending 收敛迁移**；稳定派生（TriggerKey / Seed）。
* `VillageEventOptionView.cs`：选项视图 DTO（UI 只渲染，不自行判断条件）。

**修改 — Village 产品路径**

* `Village/VillageRuntimeBridge.cs`：`EvaluateDraftBuildupResponse` 改为 `VillageEventFlow.RaiseRecognition`（不再 `OpenBuildupResponse`）；新增 `ClearLegacyPendingMigratedByEventCutover`（迁移专用）。
* `Village/UI/VillageSocialEventUI.cs`：双模式（New Event Mode / Legacy 兼容）；配置驱动动态选项按钮；配置驱动 `PauseOnOpen` / `Urgency`；结果模式与失败保留 Pending。
* `Village/Runtime/VillageSocialResponseLog.cs`：新增 `ClearLegacyPendingForMigration`（**唯一**合法调用方 = 迁移；不写 ChoiceHistory / Rest / fired）。
* `Village/Runtime/VillageCommitmentView.cs`：Rest 只读展示与「恢复窗口」回看行改读正式 Rest Authority（新增 2 个重载，旧签名保持兼容）。
* `Village/UI/VillageCommitmentUI.cs`：透传正式 Rest Authority（Person 行、参与者行、选中详情、回看区）。
* `Save/SaveSystem.cs`：Load 后重置 Event 会话输入并执行 Legacy Pending 收敛迁移。

**修改 / 新增 — 测试**

* 新 `Tests/EditMode/EventProduct/VillageEventFlowContractTests.cs`（V1–V15）。
* 迁移 `Tests/EditMode/Village/VillageSocialResponseSaveTests.cs`（3 条 B 类产品路径用例 → Event Product Contract；新增 1 条 Legacy 兼容面用例；保留旧档 Save 收敛与防御性恢复）。
* 新 `Tests/PlayMode/Village/VillageEventProductChainPlayTests.cs`（链 1 GrantRest / 链 2 无效果 Continue / 链 3 exactly-once，含 `EVENT_CHAIN_REPORT` 真实数据报告）。
* 迁移 `Tests/PlayMode/Village/VillageSocialEventProductIntegrationPlayTests.cs`（真实产品入口 + Unified Event 选项）。
* 迁移 `Tests/PlayMode/Village/VillageV3CProductChainPlayTests.cs`（全链迁移为 Unified Event 契约）。
* `Tests/PlayMode/Village/VillageStage1.PlayModeTests.asmdef`（+`Event.Runtime` / +`StaticConfig.Runtime`，供测试强类型断言）。

**未修改（刻意）**：Event Runtime 本体（Resolver / Executor / Gateway 契约 / 模板渲染器）、`EventValidator` 与 Compiler、`RRPG_Events.xlsx` 与 Canonical Mirror、`SaveData` / `VillageSaveData` / `SaveVersion`（仍为 1）、Person 真相源、Scene / Prefab。

#### 21.9.6 Open Items（交协调层裁决 / 后续 WP）

1. **玩家 Option 选择不再持久化**：Event World History 只保存 `EventId / 次数 / 最近村庄日序 / 最近 EventInstanceId`，**不保存** 玩家选了哪个 Option / 得到哪个 Outcome。后果：Commitment 面板的「回应选择」行对**新事件**不再出现（「恢复窗口」行仍来自正式 Rest Authority；旧档 Legacy ChoiceHistory 仍可见）。这不是静默丢失（正式路径从未写过该数据），但**是产品可见的信息减少**，需裁决是否在 Event World 增加 ChoiceHistory（若需要，属 Schema 变更 → 另立任务）。
2. **不残留半切换**：正式 Recognition 不再创建 Legacy Pending / fired；Legacy API 只服务旧档迁移、兼容解析与 Legacy 回归测试。
3. **内容侧仍为 Test / Not Frozen**：`evt_test_village_temp_draft` 的文案、权重、阈值、`Repeatable + Cooldown None`、`ResultTextTemplate` 均为测试内容；正式内容落地时只需改 `RRPG_Events.xlsx` + 重新导表，不需改代码。
4. **D2 / D3 未开始**（Expedition Cutover、Illustration / JourneyCard Adapter 与 P1–P3 之外的产品验收）。
5. **测试债（不阻塞 D1 / D2，登记待办）**：Legacy 兼容 UI 分支（无法迁移的 **Unknown** Legacy Pending 的弹窗呈现）当前只有 **API 级**兼容测试覆盖（`VillageSocialResponseSaveTests.LegacyPending_CompatPath_…` + `VillageEventFlowContractTests.V11`），**没有 UI 级自动化覆盖**；D1 迁移的两条 Legacy PlayMode 用例已按 §21.6.1 转为 Unified Event 契约。
6. **D1 PlayMode 测试隔离复核（专项核查时一并完成）**：`VillageEventProductChainPlayTests` / 迁移后的 `VillageSocialEventProductIntegrationPlayTests` / `VillageV3CProductChainPlayTests` 的 `SetUp`+`TearDown` 均执行 `VillageRuntimeBridge.ResetForTests` + `EventRuntimeBridge.ResetForTests` + `VillageEventFlow.ResetSessionState` 并删除存档；**未**新增对 VillageTime Speed / LogAssert / Scene 宿主的隐式依赖。5 条失败已用「隔离运行 + 基线对照」证明与这些重置无关，因此**本 WP 未修改任何测试隔离代码**（保持最小修改）。

---

### 21.10 `WP-Event-1D-R｜PlayMode Regression Baseline Repair`（窄范围测试修复执行记录）

> **范围**：只修 `ba59be6` 基线就存在的 5 条 PlayMode failure（§21.9.4 专项核查已证明 pre-existing），
> 使 D2 开工前恢复**可靠的完整 PlayMode 基线**。
> **只允许修改测试 / 测试 helper**：本轮**未修改任何产品代码**（Battle / Encounter / Journey / Expedition
> Progression / Supply / Event / Save / Config 全部零改动）。

#### 21.10.1 五条分别怎么修

| 组 | 用例 | 修复（测试侧） | 为什么不是弱化断言 |
|---|---|---|---|
| A | `BattleDualBoardProductPlayTests.ProductBattle_DownedUnit_ReleasesCell_AndNewUnitCanEnter`、`…SameCoordinateCoexists_MoveIgnoresOpposingBoard` | `EnterProductBattle` 的敌方入口从「`StaticMonsterTestData.Build()`（整个 Monster 目录）注入 Legacy 列表路径」改为**产品正式入口**：`EnemyRosterTestFixture.NewRoster(...)`（明确合法的 2 只 / 敌方 Back Left+Right）→ `BattleUI.BindActiveEnemyRoster(roster)` → `InitFromExpedition(..., catalog, ...)`（目录只作模板身份解析源）；并新增 `enemies.Count == 2` 断言 | 原有 6 条空间契约断言（双 Board 独立 / 同坐标共存 / Move 不受对方 Board 影响 / 0HP 释放格 / 新单位进入释放格 / 无第二套 Position Authority）**逐条保留**；新增的敌方数量断言使「本场敌人 = EnemyRoster」成为**更强**的契约 |
| B | `BattleMilestoneAPlayTests.V1DamageCard_RangeCheck_BlocksOutOfRange` | 同上迁移，并使用**为 Range 契约服务**的确定性 fixture：恰好 1 只敌人 / 敌方 Back（Row=2）/ Center（Column=1）；新增 3 条前置断言（敌方数量=1、Row=2、Column=1） | 测试原目标（超距不可结算 → `RangeModifier=1` 后真实结算伤害）与全部断言保留；**未改 Range 规则 / 未改伤害 / 未放大 RangeModifier / 未删任何断言** |
| C | `JourneyVisualFoundationPlayTests.Journey_ConsecutiveTransitions_…`、`VillageV4ProgressionClosurePlayTests.Progression_InsufficientSupply_…` | 在触发**预期产品日志**之前加精确 `LogAssert.Expect(LogType.Error, new Regex("拒绝进入战斗节点.*InsufficientSupply"))`；并把「靠 `AdvanceStep` 内部重复点击撞日志」改为**先用真实 Supply 规则纯查询 `ValidateEnterNode` 预判不足 → 单次调用同一产品入口 `ExpeditionUI.HandleNodeClick`**（一次调用 = 一条日志，条数确定） | 拒绝语义与断言保留并加强：新增「被拒绝后节点未进入（零 mutation）」「Supply 未被扣减/补足」「拒绝原因写入 Journey 玩家可见表面」；**未改产品日志等级、未删拒绝日志、未用全局 `ignoreFailingMessages`、未吞其它 Error** |

**根因（修复前实测确认）**：
- A/B：`StaticMonsterTestData.Build()` = **整个 Monster Registry（当前 6 只）** 被当作「本场敌人」；
  `BattleSystem` 的 Legacy 列表重载**只把前 3 只放进 Enemy Board**，其余单位留在 `EnemyUnits` 且 Row 为默认排 →
  A 触发「占位必须与单位坐标一致」失败；B 因 `GetBlockingRowCount`（遍历单位自身 Row 统计阻挡排）把未上阵单位
  也算成阻挡排（阻挡=2），`RangeModifier=1` 不足以命中。
- C：#4/#5 的产品拒绝日志（`[ExpeditionUI] 拒绝进入战斗节点…InsufficientSupply`）发生在测试未声明期望的路径上，
  被 Unity Test Framework 判为 Unhandled log message；同时两条用例断言 Journey 表面含「补给不足」，
  而战斗节点分支实际写入的是产品诊断文本（含 `InsufficientSupply`）——属测试侧期望与产品文本不一致。

#### 21.10.2 验证数字（真实）

| 验证 | 命令 / 方式 | 结果 |
|---|---|---|
| 定向（本次修复的 5 条） | `-testPlatform PlayMode -testFilter <5 个精确方法名>`（`fix5.txt` / `.xml`） | **TOTAL=5 PASSED=5 FAILED=0**，`ERROR_CS_COUNT=0` |
| 全量 PlayMode | `-testPlatform PlayMode`（`d1r_full_play.txt` / `.xml`） | **TOTAL=76 PASSED=76 FAILED=0 SKIPPED=0**，`ERROR_CS_COUNT=0` |
| D1 定向 PlayMode（三条真实产品链） | `-testPlatform PlayMode -testFilter VillageEventProductChainPlayTests\|…`（`d1r_play.txt`） | **TOTAL=6 PASSED=6 FAILED=0 SKIPPED=0** |
| 全量 EditMode | `-testPlatform EditMode`（`d1r_full_edit.txt`） | **TOTAL=990 PASSED=990 FAILED=0 SKIPPED=0**，`ERROR_CS_COUNT=0` |

**完成状态：`PlayMode Regression Baseline Repaired / Ready for Gate D2`**（D1 定向 PlayMode 6/6 与全量 EditMode 990/990 同轮复验通过；D2 未开始）。

> 运行环境事实（如实）：首次加入新测试文件时，Unity 会在**第一遍编译**报告 `CS0103`（新类型尚未进入编译图），
> 同批测试随后成功运行；在**无文件改动**的复跑中 `ERROR_CS_COUNT=0`（已实测，见 `fix5.txt` 两次运行对比）。

#### 21.10.3 残余同类测试侧模式（如实登记，本轮不改）

审计结论：`StaticMonsterTestData.Build()`（整个 Monster 目录）仍被以下 **今天为绿色**的 PlayMode 用例作为
`InitFromExpedition` 的敌方入参使用，因为它们的契约不依赖「敌方数量 / 每只敌人的唯一合法站位 / 阻挡排数」：
`PersonVisualBindingPlayTests`、`BattleVisualFoundationPlayTests`、`BattleStage5IntentPlayTests`、
`BattleStage6ARunStatePlayTests`、`BattleStage6BRewardLoopPlayTests`、`BattleGate0RulePipelinePlayTests`、
`BattleFormationRosterConsistencyPlayTests`、`BattleMilestoneAPlayTests`（除本 WP 修复的 Range 用例外）、
`VillageV4FullProductChainPlayTests`、`ExpeditionV4F/*`。

* 结论：这不是当前缺陷（它们今天全部通过），但属**同类测试侧脆弱模式** ——
  一旦内容新增怪物（目录变长）或它们将来加入与敌方数量 / 站位相关的断言，就会复现 A/B 两组症状。
* 处置：本 WP 按「最小修改」**只修实际失败的 3 条**；其余保持原样并在此登记，供后续测试清理任务（如需）参考。

#### 21.10.4 变更文件（精确列表；仅测试）

* 新 `Assets/Tests/PlayMode/EnemyRosterTestFixture.cs`（测试侧 deterministic EnemyRoster fixture：合法性自检 + 产品正式绑定入口）。
* `Assets/Tests/PlayMode/BattleDualBoardProductPlayTests.cs`（A 组两条：敌方入口迁移 + 数量断言 + 文档）。
* `Assets/Tests/PlayMode/BattleMilestoneAPlayTests.cs`（B 组：Range 契约 fixture + 3 条前置断言）。
* `Assets/Tests/PlayMode/VillageV4/VillageV4ProgressionClosurePlayTests.cs`（C 组 #5：精确 `LogAssert.Expect` + 单次真实入口 + 零 mutation 断言 + `ExpensiveNodeId` helper）。
* `Assets/Tests/PlayMode/VillageV4/JourneyVisualFoundationPlayTests.cs`（C 组 #4：精确 `LogAssert.Expect` + Supply 预判分支 + 原因可见断言）。

**零改动清单（刻意）**：全部产品代码（`Assets/Scripts/**` 的 Battle / Encounter / Journey / Expedition Progression /
Supply / Event / Village / Save / StaticConfig）、`Assets/Editor/**`、Compiler 与内容、Canonical Mirror、
Scene / Prefab / SO、SaveVersion。

---

### 21.11 `WP-Event-1D Gate D2｜Expedition Event Product Cutover`（执行记录）

> **目标**：把远征中的 Event / Content Node 从「NodeId Placeholder 事件」正式切换到 Unified Event Core，
> 使真实产品链能够 `Journey → Event Node → EventResolver → ResolvedEventInstance → Journey Event Card →
> Option → Outcome / Effect → 继续 Journey`，或 `→ StartBattle → EncounterPool → EnemyRoster →
> Prep / Battle → 回来结算同一个 Event Node`。
> **不做**：Gate D3 Presentation / Producer Acceptance；Event Core 重设计；Village D1 行为回退。

#### 21.11.1 Start Facts：旧产品链的**实际切点**（读码确认，不按历史 Prompt 猜）

| 环节 | 位置 | 切换前行为 |
|---|---|---|
| Event / Content Node 进入 | `ExpeditionUI.HandleNodeClick`（战斗类分支之外） | `adventure.TryEnterNode(nodeId)` → **立即** `TryResolveCurrentNode(Success)`（Placeholder Continue：无事件、无 Option、无效果） |
| Battle 类节点进入 | `ExpeditionUI.HandleNodeClick` → `AdventureManager.TryPrepareAndEnterBattleNode` | WP-Encounter-1C-R 原子进入（保留不动） |
| Battle 最终 outcome | `ExpeditionUI.HandleProgressionReturnFromBattle` | `TryResolveCurrentNode(Victory ? Success : Failure)`（**无** Event 额外时间） |
| Journey 卡面 | `ExpeditionJourneyView.CompleteTravelOnce` → `PresentEncounterCard` | 内容节点先弹**占位**卡（`ContinueContent`，文案「占位内容节点…」）→ 确认后才进入 |
| 卡面模型 / 渲染 | `JourneyEventCardConfig` / `JourneyEventCardView` | 单一卡面（Branch / Battle / 占位共用）→ **复用**，不新建第二套 Event UI |
| 事件核心 | `EventResolver` / `EventOptionExecutor` / `EventStaticCatalog` / `IEventWorldView` / `IEventEffectGateway` | 1A/1B/1C 已冻结；D1 已建立 Village 产品 Adapter 模式（本 Gate 按同一模式接远征） |
| 远征会话 / 补给 / 时间 | `AdventureManager.Progression` / `ExpeditionProgressionState` / `ExpeditionSupplyState` | 唯一 Authority；**已存在** `TryResolveCurrentNode(outcome, additionalElapsedDays, out data)` 与 `SupplyDelta` 正式入口（D0/1D0 预留 seam） |

#### 21.11.2 §18 覆盖审计（切换前扫默认产品 Graph）——**发现真实阻塞并已做最小覆盖调整**

| 事实 | 来源 | 结论 |
|---|---|---|
| 默认图 = 4×3：`node_0`（Battle 入口）+ 9 个非 Boss 节点 + Boss | `MapGenerator.Generate(4,3)` | 每张图恒有 **≥1 Event + 1 Shop + 1 Recruit**（保底）＋随机追加 |
| `Shop` / `Recruit` / `default` → `ExpeditionNodeKind.Event` | `LegacyMapGraphAdapter.MapNodeKind` | 这些节点在 Progression 里**都是 Event kind** → 全部要走正式 Event 路径 |
| 切换前唯一远征事件 `evt_test_exp_ruined_shrine` 的条件 | `EventCondition.cond_test_exp_depth = ExpeditionDepthAtLeast 2` | 行 1 的 Event 节点（投影深度 1）**必然** `NoCandidateEvent` → 正常产品必卡 |
| 同事件的重复规则 | `Event.RepeatPolicy = OncePerExpedition` | 即使深度满足，一次远征内第二个 Event 节点也无候选 → 同样必卡 |

**最小覆盖调整（Test / Not Frozen 内容，Authoring = XLSX）**：

1. `Event` 表 `evt_test_exp_ruined_shrine.RepeatPolicy`：`OncePerExpedition` → `Repeatable`
   （保证任意 Event 节点在任意深度都有候选；OncePerExpedition 语义仍由 Event Core 的会话历史覆盖承担）。
2. `EventCondition` 表 `cond_test_exp_depth.IntValue`：`2` → `0`
   （`ExpeditionDepthAtLeast 0` 恒成立；深度条件不再让浅层 Event 节点无解）。

> 调整后仍走**正式导表**（`rrpg-config build` → Unity Builder → `rrpg-config validate`），
> **不允许**手改 Canonical Mirror；也**不允许**用占位 Continue 兜底。

#### 21.11.3 新增 / 修改的产品结构（Active Event Authority + Gateway + WorldView + Flow）

| 文件 | 角色 |
|---|---|
| `Assets/Scripts/Adventure/ExpeditionEvent/ActiveExpeditionEventState.cs`（新） | **Active Expedition Event Authority**（§3）：NodeId / `ResolvedEventInstance` / `PendingExtraElapsedDays` / 阶段 / 预备战斗阵容（PreparedBattleRoster / PoolId / Depth / Seed / CommittedBattle）/ 最近一次结果（OptionId / OutcomeId / ResultText / HasBattle）。Runtime Only，不进 Save。 |
| `Assets/Scripts/Adventure/ExpeditionEvent/AdventureManager.ExpeditionEvent.cs`（新） | 会话宿主（partial）：持有 Active Event / 事件会话历史（`ExpeditionEventSessionState`）/ exactly-once 记号（`EventExecutionState`）；唯一建立 / 清除 / 会话重置入口；`ProjectedDepthForNode`（与 Battle 路径同一 `EncounterDepthRule`）；Event 战斗的**预生成**与**提交**（同一份 EnemyRoster）；`TryResolveCurrentNode(outcome, additionalElapsedDays, out data)` 转发。 |
| `Assets/Scripts/EventHost/ExpeditionEventWorldView.cs`（新） | `IEventWorldView` 远征 Adapter（§6）：Person（Exists / InVillage / OnExpedition / Available / Party 池）/ Expedition（Depth / Supply / PartySize / NodeOrdinal）= 本次要进入 Node 的投影口径；村庄资源返回**明确哨兵 -1**（本域不拥有），由唯一解释器 Fail Closed。 |
| `Assets/Scripts/EventHost/ExpeditionEventEffectGateway.cs`（新） | `IEventEffectGateway` 远征 Adapter（§7）：`SupplyDelta → TryApplyDelta`；`AdvanceExpeditionTime → 只累积 PendingExtraElapsedDays`；`StartBattle → Preflight 预生成（幂等）+ Commit 提交同一份`；Village-only 效果一律 Fail Closed；战斗 Seed 稳定派生（§12）。 |
| `Assets/Scripts/EventHost/ExpeditionEventFlow.cs`（新） | 编排核心（§4/§8/§9/§10/§11/§14/§15）：原子进入（Validate → Resolve → Enter → 提交同一实例）/ Option 执行（StartBattle Preflight + **Node Settlement 可行性** → `EventOptionExecutor.Execute`）/ 非战斗「继续」结算 / Event 战斗 outcome 结算 / TriggerKey + ResolutionSeed 稳定派生。 |
| `Assets/Scripts/EventHost/ExpeditionEventCardAdapter.cs`（新） | §16/§17 表现适配：`ResolvedEventInstance + Event Config → JourneyEventCardConfig`（Title / Body / Options / availability / 结果文本 / 插图）；模板渲染失败或条件非法一律 Fail Closed。 |
| `Assets/Scripts/UI/ExpeditionUI.cs`（改） | **切点**：非战斗节点 → 正式 Event 流程（原子进入 + 正式卡）；`OnEventOptionChosen`（Option / 结果「继续」/ StartBattle 三分支）；`HandleProgressionReturnFromBattle` → Event 战斗用 `TryResolveActiveEventBattle`（含 PendingExtraElapsedDays）；Retreat → 清会话事件状态。 |
| `Assets/Scripts/UI/ExpeditionJourneyView.cs`（改） | 新增 `PresentFormalEventCard(config, onOptionChosen)`（复用**同一** `JourneyEventCardView`）；提供类别级占位插图作为 fallback。 |
| `Assets/Scripts/Journey/Runtime/JourneyPresentationCycle.cs`（改） | 新增 `JourneyEncounterKind.Event`：正式 Event 节点**进入前不再弹占位卡**（`RequiresEncounterConfirmation` 只对 Battle / 遗留 Content 为 true）。 |
| `Assets/Scripts/UI/ExpeditionJourneyCoordinator.cs`（改） | 非战斗节点分类 → `JourneyEncounterKind.Event`。 |
| `Assets/Scripts/UI/JourneyEventCard.cs`（改） | Option 模型新增 `OptionId` + `Interactable`（availability 的 Disable 如实反映为不可点选 / 变灰）；角色枚举新增 `EventOption` / `ContinueResult`。 |
| `Assets/Scripts/Event/EventConditionEvaluator.Numeric.cs`（改） | 与 D1 §21.8 **对称**的 Fail-Closed：村庄资源负数哨兵（远征 WorldView）一律使条件不成立，避免 `VillageResourceAtMost N` 在损坏配置下 Fail Open。 |

**结算语义（§8）**：`TotalElapsedDays = Node.NodeElapsedDays + PendingExtraElapsedDays`；
基础 Supply 成本 = `PartySize × TotalElapsedDays × PerPersonDailyConsumptionRate`（再叠加 `Node.ExtraSupplyCost`），
全部在 `TryResolveCurrentNode(outcome, additionalElapsedDays, ...)` 内 **exactly-once** 扣除；
Event 绝不直接改 `ExpeditionElapsedDays`。

**软锁防护（§9 / §10）**：Option 执行前先做「有序投影 + Node Settlement 成本」可行性检查
（`ExpeditionEventFlow.TryEvaluateSettlementFeasibility`），不通过则拒绝该 Option 且**零 mutation**
（Pending 保留 / History 不写 / 可改选其它 Option），杜绝「Option 已执行但最后无法结算」的软锁。

#### 21.11.4 内容追加（§18 覆盖 + P-D2 链的确定性 Outcome）

为让 §18 覆盖与三条产品链具备**确定性**，在同一 Test 事件上追加（全部 Test / Not Frozen）：

| 表 | 行 | 作用 |
|---|---|---|
| `EventOption` | `opt_test_exp_store`「储备口粮」(SortOrder 3) / `opt_test_exp_force`「强行清理」(SortOrder 4) | 两个确定性 Option（各自单 Outcome，weight 100） |
| `EventOutcome` | `out_test_exp_store` / `out_test_exp_force` | 结果文本（唯一渲染器渲染） |
| `EventEffect` | `eff_test_exp_store_supply` = `SupplyDelta +2`(Order1) / `eff_test_exp_store_time` = `AdvanceExpeditionTime 1`(Order2) | P-D2-2：**先得补给再承担额外时间**（有序语义） |
| `EventEffect` | `eff_test_exp_force_time` = `AdvanceExpeditionTime 1`(Order1) / `eff_test_exp_force_battle` = `StartBattle(pool_test_general)`(Order2, Terminal) | P-D2-3：Event 内战斗（Preflight 预生成 → Commit 同一份） |

原有 `opt_test_exp_investigate`（加权 30/70：坏结果含 `StartBattle`）保留不变（内容多样性 / E-diversity）。
Legacy 推进用例（VillageV4 / Journey 表现链）在遇到正式 Event 卡时**优先选择确定性非战斗 Option（「离开」）**，
避免意外进入 Event 内战斗而脱离其既有覆盖范围（测试侧适配，见 §21.11.6）。

#### 21.11.5 验证数字（真实）

| 验证 | 结果 | 证据文件 |
|---|---|---|
| Compiler `rrpg-config build` | **PASS，errors=0 / warnings=5**（`Event.json` / `EventCondition.json` / `EventOption.json` / `EventOutcome.json` / `EventEffect.json` 已更新） | `D:\BBB\_rrpg_runs\d2\compiler_build.txt` / `compiler_build2.txt` |
| Compiler `rrpg-config validate` | **PASS，errors=0 / warnings=5**（exit 0） | `D:\BBB\_rrpg_runs\d2\compiler_build2.txt` |
| Unity Builder（`StaticConfigBuilder.ExecuteBuild`） | **`result=PASS`，`noOp=false`**；生成根 Event 域 `events=2 triggers=2 roles=2 conditions=4 presentations=2 options=6 optionConditions=2 outcomes=7 effects=10`（= 追加后 +2/+2/+4） | `unity_builder.txt` / `.log`、`Tools/RRPG.ConfigCompiler/.build-state/builder.stage.txt` |
| Unity 编译（Builder run） | `error CS = 0` | `unity_builder.txt` |
| 定向 EditMode（D2 契约 + D1 哨兵守卫 + D0 时间 seam） | **22 / 22 PASS**，`error CS = 0` | `d2_edit_targeted2.txt` |
| 定向 PlayMode（新 D2 四条链） | **4 / 4 PASS**，`error CS = 0`（P-D2-1 / P-D2-2 / P-D2-3 / P-D2-3b） | `d2_play_targeted3.txt` / `.xml` |
| 全量 PlayMode | 见 §21.11.8 | `d2_full_play2.txt` |
| 全量 EditMode | 见 §21.11.8 | `d2_full_edit2.txt` |

#### 21.11.6 定向 PlayMode（真实产品链）与测试侧适配

新增 `Assets/Tests/PlayMode/ExpeditionEventProductChainPlayTests.cs`（程序集 `BattleStage1.PlayModeTests`，反射访问 Assembly-CSharp）：

| 用例 | 覆盖 |
|---|---|
| `P_D2_1_NonBattleEvent_ResolvesSameNode_AndJourneyContinues` | Journey → Event Node 原子进入 → **正式卡面来自 Event Config**（标题 / 正文 / Option 非占位）→ 确定性非战斗 Option（`out_test_exp_leave`：仅 `AdvanceExpeditionTime 1`）→ 累积 PendingExtraElapsedDays → Result Mode →「继续」→ **同一个 Node** Success 结算（elapsed 与 Supply 真实变化）→ Journey 继续；同时断言 EventInstanceId / EventId 冻结不重抽、Session History 恰好 1 次 |
| `P_D2_2_SupplyDelta_ThenExtraTime_SettlesExactlyOnce` | `SupplyDelta +2 → AdvanceExpeditionTime 1`（有序）：Option 后补给 **+2** 且 elapsed 不变、pending=1；「继续」后 `补给 = 之前 +2 − party × 总时间 × rate`（证明额外时间真实消耗补给）；重复执行 Option / 无活动事件重复「继续」一律拒绝且**零 mutation** |
| `P_D2_3_StartBattle_UsesPreparedRoster_AndVictoryResolvesSameEventNode` | `AdvanceExpeditionTime 1 → StartBattle(pool_test_general)`：阶段进入 AwaitingBattleOutcome；**Commit 使用 Preflight 的同一份 EnemyRoster**（`AreSame`）；Active Encounter 绑定同一个 Event Node；Journey 冻结 → `OnReturnFromBattle(true)` → **同一个 Event Node** Completed、Active Event / ActiveEncounter 清除、Journey 继续 |
| `P_D2_3b_StartBattle_Defeat_ResolvesSameEventNode_WithoutCompletion` | 同链条走 Defeat：同一 Node Resolved-but-not-Completed、Active Event / ActiveEncounter 清除、Journey 隐藏（Return 接管） |

**测试侧适配（D2 语义变化的必然结果；只改测试）**：

| 文件 | 适配 |
|---|---|
| `Tests/PlayMode/VillageV4/JourneyVisualFoundationPlayTests.cs` | `AdvanceToFirstNode` 的完成判据从「已进入」改为 **D2 两步推进完成**（非战斗节点必须已结算；战斗节点仍只要求已进入）；Event 卡优先选择确定性非战斗 Option（「离开」） |
| `Tests/PlayMode/VillageV4/VillageV4ProgressionClosurePlayTests.cs` | `AdvanceStep` 同上（判据 + 确定性 Option 选择） |
| `Tests/EditMode/Event/EventConditionUnavailableWorldValueTests.cs` | 新增 D2 对称用例：**远征域 WorldView 的村庄资源哨兵（-1）必须 Fail Closed**（AtMost / AtLeast 两个方向 + 合法值反向对照） |
| `Tests/EditMode/EventProduct/ExpeditionEventContractTests.cs`（新） | 触发时机 / 稳定派生（TriggerKey、ResolutionSeed）/ §18 覆盖守卫（Repeatable 且无深度下界）/ **No Placeholder Fallback 源码级守卫** / 分类守卫 |

#### 21.11.7 契约覆盖映射（对应 D2 Spec §19–§21 的 E 清单）

| 契约 | 覆盖位置（本 Gate 的真实证据） |
|---|---|
| E1 Atomic Event Enter | PlayMode `P_D2_1`（`IsNodeEntered` + `CurrentNode` == 该 Node + 原子进入日志）；EditMode 覆盖守卫钉住「无候选即不可进入」的内容前提 |
| E2 Frozen Active Event | PlayMode `P_D2_1`（重复读取 EventInstanceId / EventId 不变；卡片由同一实例渲染） |
| E3 Event Config Presentation | PlayMode `P_D2_1`（标题 / 正文 / Option 文案必须来自 EventPresentation / EventOption，且不含占位文案） |
| E4 No Placeholder Fallback | EditMode `E4_...`（源码级：正式路径必须调用事件 Flow，且**不得**残留占位 Continue 日志/语义）+ PlayMode `P_D2_1`（Event 节点走正式卡） |
| E5 Non-Battle Outcome | PlayMode `P_D2_1`（Option → Result Mode →「继续」→ 同一 Node Success → Journey 继续） |
| E6 SupplyDelta Positive | PlayMode `P_D2_2`（Option 后补给 +2，且 elapsed 未提前变化） |
| E7 SupplyDelta Negative / insufficient zero mutation | Event Core 既有 1C 契约（`EffectPreflightFailed` 零 mutation）；产品侧由 `TryEvaluateSettlementFeasibility` 在 Commit 前拒绝（PlayMode `P_D2_2` 覆盖「拒绝时零 mutation」的同类路径）；**PlayMode 未新增专门负数用例**（负数配置内容未加入 Test 内容） |
| E8 AdvanceTime adds elapsed + supply cost | PlayMode `P_D2_2`（`补给 = 之前 +2 − party × 总时间 × rate`，总时间含额外 1 天）+ D0 `ExpeditionEventTimeTests` |
| E9 Final Settlement affordability prevents softlock | 产品实现 = `TryEvaluateSettlementFeasibility`（有序投影 + Node 成本）；PlayMode `P_D2_2` 覆盖「拒绝即零 mutation」；**专门的『不足即拒绝』PlayMode 用例未新增**（需要构造低补给会话；`ExpeditionEventTimeTests.NotAffordableWithAdditionalTime_IsRejected_WithoutSoftLock` 已在 Resolver 层覆盖同一语义） |
| E10 Effect ordering with Supply + Time | 内容 = `SupplyDelta`(Order1) → `AdvanceTime`(Order2)；PlayMode `P_D2_2` 证明「先得补给」在 Commit 阶段真实发生；Core 的有序 Preflight 由 1C 契约测试覆盖 |
| E11 StartBattle preflight roster | PlayMode `P_D2_3`（Preflight 生成非空 roster；失败路径由 `StartBattle Preflight 失败` 分支保证零 mutation） |
| E12 StartBattle commit same roster | PlayMode `P_D2_3`（`AreSame(PreparedBattleRoster, CommittedBattle.Roster)` + Active Encounter 持有同一份） |
| E13 Battle Victory resolves same Event Node | PlayMode `P_D2_3`（同一 Node Completed；Active Event / ActiveEncounter 清除；Journey 继续） |
| E14 Battle Defeat resolves same Event Node | PlayMode `P_D2_3b`（同一 Node Resolved-not-Completed；Return 接管） |
| E15 History / Session history exactly-once | PlayMode `P_D2_1` / `P_D2_2`（`ExpeditionEventSession.OccurrenceCount == 1`） |
| E16 repeated option / repeated battle callbacks no double settle | PlayMode `P_D2_2`（Result 阶段重复执行 Option、无活动事件重复「继续」均被拒绝且补给 / elapsed 零变化） |
| E17 current reachable Event-node coverage | EditMode `E17_...`（长期内容守卫：必须存在 Repeatable 且无深度下界的 Enabled 远征事件）+ §21.11.2 的图审计 |

**E7 / E9 的如实说明**：这两条的产品路径由**同一处**代码兑现（Option 执行前的有序投影 + Node Settlement 成本检查，
`ExpeditionEventFlow.TryEvaluateSettlementFeasibility`），并且 `P_D2_2` 已覆盖「被拒绝即零 mutation」；
但本 Gate **未新增**「低补给会话下的整链拒绝」PlayMode 用例（需额外构造 Carry 不足的会话），
该缺口登记为**测试债**（不阻塞 D2 技术验证，交后续测试补齐任务）。

#### 21.11.8 中间截面（全量运行启动时的如实状态；最终结果见 §21.11.9）

**本轮已确认（真实运行）**：

| 验证 | 结果 |
|---|---|
| Compiler build / validate | PASS（errors=0 / warnings=5） |
| Unity Builder | `result=PASS`，`noOp=false`（生成根已更新，Event 域 options=6 outcomes=7 effects=10） |
| 定向 EditMode（D2 契约 + 哨兵守卫 + 时间 seam） | **22 / 22 PASS**，`error CS = 0` |
| 定向 PlayMode（D2 四条链） | **4 / 4 PASS**，`error CS = 0` |

**全量套件（`d2_full_play2.txt` / `d2_full_edit2.txt`）**：本轮已启动；D2 改变了「非战斗节点」的产品语义
（从「进入即 Success 结算」改为「原子进入 + Option → 结果卡继续 → 结算」），
因此**部分 Legacy 推进类 PlayMode 用例必须随语义迁移**：

| 已迁移 | `JourneyVisualFoundationPlayTests.AdvanceToFirstNode`、`VillageV4ProgressionClosurePlayTests.AdvanceStep`（完成判据 = 非战斗节点已结算；Event 卡优先选择确定性非战斗 Option「离开」） |
| 待核对 | `VillageV4UIRaycastInteractionPlayTests`、`VillageV4FullProductChainPlayTests`、`VillageResourceCarryE2EPlayTests`、`ExpeditionV4F*`（如仍按旧「一步进入即结算」驱动，需要同样的两步迁移） |

> 结论（不夸大）：**D2 的产品实现与内容已达成本轮门槛要求**（Event Node 正式切 Event Core、无占位回退、
> Active Expedition Event Authority、SupplyDelta / AdvanceTime 结算、StartBattle 生命周期、Non-Battle 链），
> 且**定向** EditMode / PlayMode 全绿；**全量套件的最终数字以 `d2_full_play2.txt` / `d2_full_edit2.txt` 为准**，
> 未观测到结果前**不**写 `Gate D2 Technical Verified`（沿用 D1 的同一纪律）。

#### 21.11.9 Final Gate（全量回归真实结果 + 门槛判定）

**全量套件结果（真实；两套全量均发生在最后一次代码 / 测试变更之后，其后仅改动 `Docs/` 之外的内容为零）**：

| 验证 | TOTAL | PASSED | FAILED | SKIPPED | error CS | 证据文件 |
|---|---:|---:|---:|---:|---:|---|
| Compiler full（`dotnet test`） | 208 | **208** | **0** | 0 | — | `gate_compiler_test.txt` |
| `RRPG_检查配置.cmd`（validate，只读） | — | — | — | — | — | **`结果：PASS errors=0 warnings=5` / `RESULT: PASS`**（`gate_check_config.txt`） |
| D2 targeted EditMode | 22 | **22** | **0** | 0 | **0** | `d2_edit_targeted2.txt` / `.xml` |
| D2 targeted PlayMode | 4 | **4** | **0** | 0 | **0** | `d2_play_targeted3.txt` / `.xml` |
| Full EditMode | 999 | **999** | **0** | 0 | **0** | `d2_full_edit2.txt` / `.xml`（17:27:44 → 17:29:18） |
| Full PlayMode | 80 | **80** | **0** | 0 | **0** | `d2_full_play2.txt` / `.xml`（17:26:29 → 17:27:41） |

* Compiler build（`rrpg-config build`）与 Unity Builder 已在切换时执行并 PASS（见 §21.11.5），
  其间与之后**未再改动** Config / Mirror / 产品代码 → 生成根仍为当前内容。
* **Gate 收尾复跑（同一棵树）**：D2 targeted EditMode 复跑 **22 / 22 PASS**（`d2_gate_edit2.txt`，`error CS = 0`）；
  D2 targeted PlayMode 复跑在本次环境中于**编辑器启动阶段停滞**（未产出结果，非测试失败），
  故该 Gate 项沿用**同树**证据 `d2_play_targeted3.txt`（**4 / 4 PASS**，`error CS = 0`）——
  且 Full PlayMode **80/80** 本身已包含这四条链 → 该结论有**两处独立证据**。
* **失败明细：两套全量 `FAILED = 0` → 不存在失败用例**（无需列 Failure Message / stack）。
* Full PlayMode 80 = D1-R 基线 76 + 新增 D2 四条链 4；Full EditMode 999 = D1-R 基线 990 + 新增 9
  （`ExpeditionEventContractTests` 7 + D1 哨兵守卫新增 2）→ 计数自洽。

**Legacy 推进用例的实测结论（以 XML 为准，不凭预判）**：上一轮标记为「待核对」的
`VillageV4UIRaycastInteractionPlayTests` / `VillageV4FullProductChainPlayTests` / `VillageResourceCarryE2EPlayTests` /
`ExpeditionV4F*` **全部通过**（Full PlayMode 0 失败）→ **无需再迁移**。
实际需要迁移的只有两条显式按「点击即 Resolve」语义推进的 helper：
`JourneyVisualFoundationPlayTests.AdvanceToFirstNode`、`VillageV4ProgressionClosurePlayTests.AdvanceStep`
（迁移为 D2 两步推进：普通节点原方式；正式 Event 卡优先稳定 Non-Battle Option → Option 执行 → 如有 Result Mode 则「继续」→ 等当前 Node Resolved；
**未**在通用 helper 中自动选择 StartBattle，Battle Event 由专用测试显式处理）。

**门槛确认（逐一对应 D2 Spec §24）**：

| 门槛项 | 状态 | 真实证据 |
|---|---|---|
| Event Node 正式切 Unified Event Core | ✅ | P-D2-1 / P-D2-2 / P-D2-3 / P-D2-3b（真实产品链） |
| Placeholder Event 正式路径已退出 | ✅ | EditMode `E4_...` 源码级守卫（正式路径必须经 `ExpeditionEventFlow`；不得残留占位 Continue）+ `JourneyEncounterKind.Event` 分类守卫 |
| Active Expedition Event Authority 成立 | ✅ | P-D2-1（同 Node 冻结复用不重抽）+ 结算 / Return 清除断言 |
| SupplyDelta 产品生效 | ✅ | P-D2-2（Option 后补给 +2） |
| AdvanceTime + Supply exactly-once | ✅ | P-D2-2（`补给 = 之前 +2 − party × 总时间 × rate`；重复 Option / 重复「继续」零 mutation） |
| Event StartBattle 使用同一 Prepared Roster | ✅ | P-D2-3（`AreSame(PreparedBattleRoster, CommittedBattle.Roster)` + Active Encounter 持有同一份） |
| Victory / Defeat 结算同一个 Event Node | ✅ | P-D2-3（Completed）/ P-D2-3b（Resolved-not-Completed） |
| Full regression 全绿 | ✅ | Compiler 208/208 + validate PASS + Full EditMode 999/999 + Full PlayMode 80/80 + `error CS = 0` |

**结论：`WP-Event-1D Gate D2 Technical Verified / Ready for Gate D3`**（D3 未开始；未 Commit / Push）。

**遗留（登记为测试债，不阻塞本 Gate）**：E7 / E9 的「低补给会话整链拒绝」PlayMode 用例未新增
（产品路径由 `ExpeditionEventFlow.TryEvaluateSettlementFeasibility` 兑现，`P_D2_2` 已覆盖「拒绝即零 mutation」）。







---

### 21.12 `WP-Event-1D Gate D3｜Presentation Authority Closure + Producer Acceptance Preparation`（执行记录）

> **目标**：闭合 Village / Expedition 两表面的 **Formal Event 表现 Authority**——内容身份（`EventId → EventPresentation`）、
> 共享模板渲染链（唯一 `EventTemplateRenderer`）、插图链（`IllustrationAssetId → 共享 VisualAsset Authority → Sprite`）
> 各自**只有一个**权威入口，两表面**共用**同一表现文本入口，且失败语义统一为 **Fail Closed**
> （不显示原始占位符、不渲染半成品卡面、**不静默换图**）。同时补自动化契约测试与三条产品 Smoke，
> 为制作人 Play Acceptance 做准备。
> **不做**：Producer Acceptance 判定本身、Event Core 重设计、Battle / Branch 表现迁移、
> Legacy Catalog（`JourneyEventPresentationCatalog` / `JourneyEventIllustrationResolver`）物理删除、
> Commit / Push / Final Closeout。

#### 21.12.1 Presentation Host Audit（读真实代码；D3 之前的**实际**表现来源）

| 表现元素 | D3 之前的实际来源 | D3 之后（唯一权威） |
|---|---|---|
| Village 标题 / 正文 | `VillageEventFlow.TryRenderPendingTexts`（**自持**模板替换） | `EventPresentationTexts.TryRenderTexts` → 唯一 `EventTemplateRenderer` |
| Expedition 标题 / 正文 | `ExpeditionEventCardAdapter.TryRenderTexts`（**自持**模板替换） | 同上（**同一**入口） |
| Option 文案 / 副文案 | `EventOption.LabelTemplate` / `SubLabelTemplate`，两个表面各自渲染 | `EventPresentationTexts.RenderTemplate`（同一入口） |
| 插图（Journey 卡） | `NodeId → JourneyEventPresentationCatalog`（Legacy 键） | `EventId → EventPresentation.IllustrationAssetId → StaticConfigRegistry.VisualAssets → Sprite` |
| Pause / Urgency（Village） | Legacy Pattern 目录（`VillageTestSocialPatterns`） | `EventPresentation.PauseOnOpen` / `EventPresentation.Urgency` |
| Option 可用性 | `EventOptionEvaluator`（点击此刻的世界事实） | **不变**（`Hide` → 不出现；`Disable` → 显示但不可点） |
| Result 文本 | Outcome 结算结果（来自 Active Event 状态，不重抽） | **不变** |

**Legacy 保留（刻意，不在本 Gate 删除）**：

* `JourneyEventPresentationCatalog` / `JourneyEventIllustrationResolver` 继续服务**未迁移**表面
  （Battle Encounter 卡、Branch 路线卡、既有占位表现）——它们是「非 Formal Event」的表现来源，不是 Formal Event 的兜底；
* `VillageSocialEventUI.ShowPending`（Legacy 兼容模式）继续由 `VillageTestSocialPatterns` 提供文本，
  只服务**无法迁移的旧档 Pending**；正式模式（`ShowEventPending`）已完全由 `EventPresentation` 驱动。

**Formal Event 路径的硬约束（D3 冻结）**：`NodeId → Legacy Catalog` **不得**再决定 Formal Event 的
标题 / 正文 / Option 文案 / 插图 / 身份判定。

#### 21.12.2 交付物（①–⑦）

| # | 交付物 | 落地位置 | 语义 |
|---|---|---|---|
| ① | **共用表现文本入口**（新） | `Assets/Scripts/EventHost/EventPresentationTexts.cs` | `TryRenderTexts` / `RenderTemplate`：唯一经 `EventTemplateRenderer`；**只读**（不写状态、不执行 Option、不 Resolve Node）；Fail Closed |
| ② | Village 侧改走共用入口 | `VillageEventFlow.TryRenderPendingTexts` | 委托 `EventPresentationTexts.TryRenderTexts`（保留既有 4 参数重载 → 内部传 `null` resolver，向后兼容旧调用点） |
| ③ | Expedition 侧改走共用入口 | `ExpeditionEventCardAdapter.TryRenderTexts` / `RenderTemplate` | Title / Body / Option 文案全部委托共用入口；**Option Label 渲染失败 → 整张卡面 Fail Closed**；SubLabel 失败 → 省略该行（不显示占位符） |
| ④ | **插图唯一正式入口 + 诊断** | `ExpeditionEventCardAdapter.ResolveEventIllustration`（public）+ `LastIllustrationDiagnostic` / `LastIllustrationSource` | 见 §21.12.4；两处卡面构造（Option 卡 / Result 卡）统一改走该方法 |
| ⑤ | **自动化契约测试**（EditMode） | `Assets/Tests/EditMode/EventProduct/EventPresentationAuthorityTests.cs` | D3-E1 / E2 / E3 / E6 / E7 / E8 / E9+E10 / E11（见 §21.12.5） |
| ⑥ | **三条产品 Smoke**（PlayMode，真实产品入口） | Village 链（`VillageEventProductChainPlayTests`）/ Expedition Non-Battle（`P_D2_1` + `P_D2_2`）/ Event Battle 往返（`P_D2_3` + `P_D2_3b`） | 见 §21.12.6：D3 未改产品结算语义，Smoke 的职责是**证明 D3 表现改动未破坏 D2 lifecycle** |
| ⑦ | **当前插图内容审计** | §21.12.7 | 逐条列出 Test Event 的 `EventId → IllustrationAssetId → VisualAsset → SourceAsset → Sprite → 实际显示` |

#### 21.12.3 Fail Closed 语义（两表面统一；D3 冻结）

| 场景 | 期望 | 实现 |
|---|---|---|
| 没有已解析实例（`ResolvedEventInstance == null`） | 失败 + 可操作诊断（**不**渲染空卡面冒充成功） | `EventPresentationTexts.TryRenderTexts` 首行判空 → `false` + 诊断 |
| 没有表现配置（`EventPresentation == null`） | 失败 + 诊断（指名 `EventId`） | 同上 |
| 模板语法非法 / 占位符无法解析 | **空串** + 诊断（绝不把 `{RoleId.DisplayName}` 显示给玩家） | `EventTemplateRenderer` 失败 → 共用入口返回空串 + 诊断 |
| 模板留空（配置未填） | **成功且为空**（合法状态，不产生诊断噪音） | `RenderTemplate` 对空模板直接返回空串（无诊断） |
| Village 渲染失败 | 标题显示「事件无法显示（内容配置问题）」+ 正文显示诊断 | `VillageSocialEventUI.ShowEventPending` 的 else 分支 |
| Expedition Option Label 渲染失败 | **整张卡面 Fail Closed**（不显示半渲染选择） | `ExpeditionEventCardAdapter`：Label 失败即放弃该卡面并记录诊断 |

#### 21.12.4 插图链（唯一正式链 + 失效语义）

```
EventId → EventPresentation.IllustrationAssetId → StaticConfigRegistry.TryGetVisualAssets
        → VisualAssetCatalog.TryGetById → VisualAssetDefinition.Sprite（managed 生成物）
```

| 情况 | 返回 | `LastIllustrationSource` | `LastIllustrationDiagnostic` |
|---|---|---|---|
| `IllustrationAssetId` 为空（Schema `optional`）= **有意不配置** | 类别级占位图 | `category:unconfigured`（无 fallback 图时 `none:unconfigured`） | **空**（不是错误，不产生噪音） |
| VisualAsset 权威不可用 | 类别级占位图 | `category:authority-unavailable` | 明确诊断（含 `EventId` / `IllustrationAssetId` / 重导表指引） |
| `VisualAssetId` 不存在 | 类别级占位图 | `category:unresolved` | 明确诊断（含 `VisualAssetCatalog.MissingIdDiagnosticPrefix`） |
| 已登记但 managed Sprite 未生成 | 类别级占位图 | `category:sprite-missing` | 明确诊断（含 `SourceFile`，提示重跑导表） |
| 正常命中 | **共享 VisualAsset 的同一 Sprite** | `visualasset:<VisualAssetId>` | 空 |

**绝不**发生：换 NodeId 图、取「第一张事件图」、随机图、静默使用 `null` Sprite。
Legacy `JourneyEventIllustrationResolver` 仍存在，但**只**服务未迁移表面（Battle / Branch / 既有占位）。

（后续：若制作人要求 Village 弹窗也带插图，需要给 `VillageSocialEventUI` 增加插图宿主并复用**同一** `ResolveEventIllustration`；
当前 Village Modal 没有插图区域 → 该链在 Village 侧 **N/A**，本 Gate 不新增 UI 结构。）

#### 21.12.5 契约测试（EditMode；`EventPresentationAuthorityTests`）

| 用例 | 断言要点 | 类型 |
|---|---|---|
| `D3E1_VillageFormalMode_DoesNotReadLegacyPatternTextsOrUrgency` | 正式模式切片内必须经配置文本渲染；**不得**出现 `ResolveUrgency(` / `PatternKey`；Legacy 兼容模式仍由 `VillageTestSocialPatterns` 提供文本 | 源码级守卫 |
| `D3E11_VillageFormalMode_UrgencyAndPauseComeFromEventPresentation` | `presentation.PauseOnOpen` / `presentation.Urgency` 必须参与正式模式 | 源码级守卫 |
| `D3E2_ExpeditionFormalPath_DoesNotUseNodeIdPresentationCatalog` | 适配器内**不得**出现 `ResolveForEvent(` / `ResolveCategoryFallback(` / `JourneyEventPresentationCatalog.Instance` / `.FindEntry(`；必须读 `EventPresentation` + `IllustrationAssetId` + `TryGetVisualAssets` | 源码级守卫 |
| `D3E3_VillageAndExpedition_ShareTheSinglePresentationTextEntry` | 两表面均引用 `EventPresentationTexts.*`；三个文件均**无**自实现的 `.Replace(` 模板替换 | 源码级守卫 |
| `D3E6_PresentationRendering_FailsClosed_WithoutShowingPlaceholders` | 非法模板 → 空串 + 诊断；未解析实例 → `false` + 诊断；空模板 → 成功且为空（无诊断） | 行为 |
| `D3E7_ConfiguredIllustrationAssetId_ResolvesThroughSharedVisualAsset` | 真实配置中取一条 Sprite 已生成的 VisualAsset → `ResolveEventIllustration` 返回**同一** Sprite，来源 = `visualasset:<id>` | 行为（真实生成根） |
| `D3E8_UnresolvableConfiguredIllustration_DiagnosesAndNeverSubstitutesAnotherImage` | 不存在的 Id → 返回传入的类别占位图（**不换图**）+ 明确诊断 + `category:*` 来源；未配置 → 无诊断 + `unconfigured`；（环境存在「Sprite 未生成」行时）加固断言 `sprite-missing` | 行为 |
| `D3E9_D3E10_JourneyCard_RendersConfiguredOptions_AndHonorsInteractable` | 统一卡面按配置渲染 **3** 个 Option（不假定固定两个）；`Interactable=false` → 按钮不可点 | 行为（EditMode 只验渲染形状） |

**覆盖映射（D3 Spec §13 的 D3-E1…E12）**：

* E1 / E2 / E3 / E6 / E7 / E8 / E9 / E10 / E11 → 本文件（上表）；
* **E4 / E5（Role `DisplayName` / Context 占位符正确性）** → 由 Core `EventTemplateRendererTests` +
  PlayMode 的真实人物名断言共同覆盖（Village 链断言正文含**真实 DisplayName**且不含 `{`；Expedition 链断言标题来自 `EventPresentation`；
  RoleId / PersonId 绝不显示给玩家）；
* **E12（Expedition Result → 「继续」→ 同一 Node 结算）** → PlayMode（`P_D2_1` 的「继续 → `IsNodeResolved` 同一 Node」）；
* 「点击 → 索引回调」不在 EditMode 覆盖（`JourneyEventCardView.Hide()` 内部 `Destroy`，Edit 模式不允许），由 PlayMode 卡面点击覆盖。

#### 21.12.6 三条产品 Smoke（PlayMode；真实产品入口）

D3 **未**改动产品结算 / 生命周期语义（只改表现来源与插图解析入口），因此三条 Smoke 的职责是
「**D3 之后，D2 建立的产品链仍然完整**」；D3 的**新**表现语义由 §21.12.5 的契约测试（含真实生成根）承担。

| Smoke | 用例（真实产品入口，非测试旁路） | 断言要点 |
|---|---|---|
| **P-D3-1** Village：Recognition → Formal Event → Option → Result → History | `VillageEventProductChainPlayTests.EventChain1_Recognition_GrantRest_PendingRestAndHistory` / `EventChain2_*` / `EventChain3_*` | 真实调度第 3 次抽调 → Unified Event Pending（Legacy pending / fired **不得**被写）→ 标题来自 `EventPresentation`、正文含**真实 DisplayName**且不含 `{` → 按配置生成的 Option 按钮（名称 = `EventOption_<OptionId>`）→ Option → Result → History / Rest 后果；exactly-once |
| **P-D3-2** Expedition Non-Battle | `ExpeditionEventProductChainPlayTests.P_D2_1_NonBattleEvent_ResolvesSameNode_AndJourneyContinues` + `P_D2_2_SupplyDelta_ThenExtraTime_SettlesExactlyOnce` | 真实 Bootstrap（NewGame/Continue → Town → Prep → Confirm → Journey）→ 走到正式 Event 节点（原子进入）→ 正式事件卡（标题来自 `EventPresentation`；正文非占位）→ 配置 Option（「离开」/「储备口粮」）→ Result → 「继续」→ 结算**同一** Node → Journey 继续；额外时间 / SupplyDelta 真实计入且重复执行零 mutation |
| **P-D3-3** Event Battle 往返（不破坏 D2 lifecycle） | `ExpeditionEventProductChainPlayTests.P_D2_3_StartBattle_UsesPreparedRoster_AndVictoryResolvesSameEventNode` + `P_D2_3b_StartBattle_Defeat_ResolvesSameEventNode_WithoutCompletion` | StartBattle Outcome → Preflight 生成的**同一份** `EnemyRoster`（禁止重新 Generate）→ Active Encounter 绑定同一 Node → Prep / Battle → `OnReturnFromBattle` → 结算**同一个** Event Node（Victory → Completed；Defeat → Resolved 未 Completed）+ Journey 恢复 |

> 说明：这三条 Smoke 在 D3 中**必须复跑**而不是沿用 D2 结论——D3 改了「正式卡面标题 / 正文 / Option 文案 / 插图」的
> **来源入口**（正是这三条链每天走的路径），因此它们是 D3 的回归边界。

#### 21.12.7 当前插图内容审计（§11；读真实配置与生成根）

| 事件（Test 内容） | `EventId` | `PresentationChannel` | `IllustrationAssetId`（XLSX → Mirror） | 目标 `VisualAsset` | `SourceFile` | managed `Sprite` | 卡面**实际**显示 |
|---|---|---|---|---|---|---|---|
| 路边废墟神龛（远征） | `evt_test_exp_ruined_shrine` | `JourneyCard` | **空**（Schema `optional`） | — | — | — | 类别级占位图（`LastIllustrationSource = category:unconfigured`，**无诊断**） |
| 村民自述 / 临时抽调（村庄） | `evt_test_village_temp_draft` | `VillageModal` | **空** | — | — | — | Village Modal **没有插图区域** → 该链 N/A |

**权威事实（不是推断）**：

* `Development/Generated/StaticConfig/EventPresentation.json`：两条表现行的 `IllustrationAssetId` 均为 `null`；
* `Development/Generated/StaticConfig/VisualAsset.json`：注册表内**只有 6 行**，全部是 `Enemy/enemy_mon_00x.png` → **没有任何 Event 资源行**；
* 生成根 `Assets/Generated/StaticConfig/Assets/` 下**只有** `Enemy/`（6 张 `enemy_mon_00x.png`）→ 没有 Event Sprite 被生成。

**结论：`Art Content Pending`（内容待办，不是代码缺口）。**

* 代码侧链路已闭合且被真实数据证明：§21.12.5 的 `D3E7` 用**真实注册**的 VisualAsset（`Sprite` 已生成）验证
  `EventPresentation.IllustrationAssetId → 共享 VisualAsset → 同一 Sprite`；`D3E8` 验证失效时**不换图** + 可操作诊断。
* 制作人若要给某个事件加插图，只需：`Development/Config/RRPG_VisualAssets.xlsx` 增行（`SourceFile = Event/<name>.png`）+
  对应事件 `EventPresentation.IllustrationAssetId` 填该 `VisualAssetId` + 放源图 → 重跑导表；
  **不需要**改任何代码 / Scene / Legacy Catalog（这正是 D3 想闭合的性质）。
* 在插图补上之前，正式卡面**不会**显示错误图片（占位图 + 无诊断），也**不会**显示上一个事件的图（绝不换图）。

#### 21.12.8 变更文件（精确列表）

**新增**

* `Assets/Scripts/EventHost/EventPresentationTexts.cs` —— 共用表现文本入口（唯一 `EventTemplateRenderer`；只读；Fail Closed）。
* `Assets/Tests/EditMode/EventProduct/EventPresentationAuthorityTests.cs` —— D3 契约测试（8 条，§21.12.5）。

**修改（产品代码，仅 2 个文件）**

* `Assets/Scripts/EventHost/VillageEventFlow.cs` —— `TryRenderPendingTexts` 改为委托共用入口（保留原 4 参数重载 → 内部传 `null` resolver）。
* `Assets/Scripts/EventHost/ExpeditionEventCardAdapter.cs` —— `TryRenderTexts` / `RenderTemplate` 委托共用入口；
  Option Label 渲染失败 → **整张卡面 Fail Closed**（SubLabel 失败 → 省略该行）；
  `ResolveEventIllustration` 改为 public + 新增 `LastIllustrationSource` / `LastIllustrationDiagnostic`；
  两处卡面构造（Option 卡 / Result 卡）统一改走该入口。

**文档**

* 本文件 §21.12（D3 执行记录）；`Docs/DevelopmentSpecs/README.md`（状态同步）；`Docs/问题清单.md`（问题 / 待办同步）。

**零改动清单（刻意）**

* Event Core（`EventResolver` / `EventOptionEvaluator` / `EventOptionExecutor` / `IEventEffectGateway` / `EventTemplateRenderer` 本体 /
  `EventStaticCatalog`）、Expedition 事件结算与生命周期（`ExpeditionEventFlow` / `AdventureManager.*` / `ExpeditionUI`）、
  Village 运行时（`VillageRuntimeBridge` / `VillageSocialResponseLog`）；
* Battle / Encounter、Progression / Supply、Save / `SaveVersion`；
* Scene / Prefab / ScriptableObject、Config（XLSX）/ Canonical Mirror / 生成根；
* Legacy `JourneyEventPresentationCatalog` / `JourneyEventIllustrationResolver` / `JourneyEventIllustrationBinding`
  —— **保留**且行为不变（仍服务 Battle / Branch / 既有占位表现），本 Gate 不删除、不改配置；
* D1 / D2 既有测试文件（本 Gate **只新增**测试文件，未修改任何既有用例）。

#### 21.12.9 三条产品 Smoke 实测结果

| Smoke | 用例类 / 过滤器 | TOTAL | PASSED | FAILED | `error CS` | 证据文件 | 耗时 |
|---|---|---:|---:|---:|---:|---|---|
| **P-D3-1** Village 链 | `VillageEventProductChainPlayTests` | 3 | **3** | **0** | **0** | `d3_play_smoke1.txt` / `.xml` | 18:30:27 → 18:31:13 |
| **P-D3-2** Expedition Non-Battle | `ExpeditionEventProductChainPlayTests.P_D2_1_` / `.P_D2_2_` | 2 | **2** | **0** | **0** | `d3_play_smoke2.txt` / `.xml` | 18:31:24 → 18:32:03 |
| **P-D3-3** Event Battle 往返 | `ExpeditionEventProductChainPlayTests.P_D2_3`（含 `P_D2_3b` Defeat） | 2 | **2** | **0** | **0** | `d3_play_smoke3b.txt` / `.xml` | 18:32:55 → 18:33:44 |

* P-D3-3 首次运行（`d3_play_smoke3`）在本环境的**编辑器启动阶段停滞**（无结果产出，非测试失败 —— 与 D2 §21.11.9 登记的现象同类）；
  终止后立即重跑（`d3_play_smoke3b`）得到上表结果。**该停顿是环境现象，不是产品 / 测试失败**，且同一场景已由 Full PlayMode 再次覆盖。

* 三条 Smoke 全部走**真实产品入口**（Scene → MainMenuUI / TownUI / ExpeditionUI / Journey Coordinator → 统一卡面点击），
  没有测试旁路进入 Event 生命周期。
* `error CS = 0` 表示该次运行**不存在编译错误**（D3 的两个产品文件 + 新测试文件都已编译进 Assembly-CSharp / 测试程序集）。

#### 21.12.10 Final Gate（全量回归真实结果 + 门槛判定）

**全部验证都发生在 D3 最后一次代码 / 测试变更之后**（其后除 `Docs/` 外零改动）：

| 验证 | TOTAL | PASSED | FAILED | SKIPPED | `error CS` | 证据文件 |
|---|---:|---:|---:|---:|---:|---|
| Compiler full（`dotnet test -c Release`） | 208 | **208** | **0** | 0 | — | `d3_compiler_test.txt`（`COMPILER_TEST_EXITCODE=0`） |
| `RRPG_检查配置.cmd`（validate，只读） | — | — | — | — | — | **`结果：PASS errors=0 warnings=5` / `RESULT: PASS` / `VALIDATE_EXITCODE=0`**（`d3_check_config.txt`；同次运行转存 `d3_check_config2.txt`。控制台代码页可能把中文诊断显示为乱码 → **权威信息是 ASCII 部分**（`errors=0 warnings=5` / `RESULT: PASS`），内容行数（sheet rows）与 D2 完全一致；干净中文版参见 D2 证据 `gate_check_config.txt`） |
| D3 targeted EditMode（`EventPresentationAuthorityTests`） | 8 | **8** | **0** | 0 | **0** | `d3_edit_authority2.txt` / `.xml` |
| P-D3-1（Village 链，PlayMode） | 3 | **3** | **0** | 0 | **0** | `d3_play_smoke1.txt` / `.xml` |
| P-D3-2（Expedition Non-Battle，PlayMode） | 2 | **2** | **0** | 0 | **0** | `d3_play_smoke2.txt` / `.xml` |
| P-D3-3（Event Battle 往返，PlayMode） | 2 | **2** | **0** | 0 | **0** | `d3_play_smoke3b.txt` / `.xml` |
| Full EditMode | 1007 | **1007** | **0** | 0 | **0** | `d3_full_edit.xml`（18:37:16 → 18:38:29） |
| Full PlayMode | 80 | **80** | **0** | 0 | **0** | `d3_full_play.xml`（18:40:04 → 18:40:45） |

* **失败明细：所有套件 `FAILED = 0` → 不存在失败用例**（无需列 Failure Message / stack）。
* **计数自洽**：Full EditMode 1007 = D2 基线 999 + D3 新增 8（本 Gate **只新增**测试文件，未改任何既有用例）；
  Full PlayMode 80 = D2 基线 80（D3 未新增 PlayMode 用例：三条 Smoke 复用 D2 建立的真实产品链用例）。
* 环境说明：`d3_play_smoke3`（P-D3-3 首次运行）在**编辑器启动阶段停滞**，终止后重跑得到 `d3_play_smoke3b`（上表）。
  该现象与 D2 §21.11.9 登记者同类，属环境行为，不是产品 / 测试失败；同一场景另有 Full PlayMode 覆盖。
* 证据目录：`D:\BBB\_rrpg_runs\d3\`（**仓库外**运行记录，不随 Git 提交）。
* 运行残留清理：PlayMode 批处理运行会生成 `Assets/InitTestScene*.unity`（Test Framework 生成物，非交付物）；
  本轮被中断的运行留下 2 组该类文件，已删除 → **工作区不再含测试运行残留**（`git status` 复核）。
  其余运行脚本 / 结果文件全部落在仓库外，仓库内变更 = §21.12.8 的精确清单。

**门槛确认（逐一对应 D3 Spec §15 / §16）**：

| 门槛项 | 状态 | 真实证据 |
|---|---|---|
| 两表面 Formal Event 表现只有一个 Authority（`EventId → EventPresentation`） | ✅ | D3-E1 / E2 / E11（源码级守卫）+ P-D3-1 / P-D3-2（真实文案） |
| 共享模板渲染链（唯一 `EventTemplateRenderer`） | ✅ | D3-E3（两表面均经共用入口；相关文件无自实现替换）+ D3-E6（Fail Closed 行为） |
| 插图唯一正式链（`IllustrationAssetId → 共享 VisualAsset → Sprite`） | ✅ | D3-E7（真实生成根命中**同一** Sprite）+ D3-E8（失效不换图 + 明确诊断） |
| Option 数量 / 可用性来自配置（>2 且如实反映 Disable） | ✅ | D3-E9 / E10（EditMode 渲染形状）+ D2 既有 PlayMode（卡面点击 / 生命周期） |
| Role `DisplayName` / Context 占位符正确，绝不显示内部 Id | ✅ | Core `EventTemplateRendererTests` + P-D3-1（正文含真实 DisplayName）/ P-D3-2（标题来自 `EventPresentation`） |
| 失败语义 Fail Closed（不显示占位符 / 不渲染半成品 / 不静默换图） | ✅ | D3-E6 / D3-E8 + §21.12.3 语义表 |
| Legacy Catalog 不再被 Formal Event 使用，但**保留**服务未迁移表面 | ✅ | D3-E2（Formal 路径无 Legacy 调用）+ §21.12.1（Legacy 保留清单） |
| 三条产品 Smoke 复跑全绿（D3 未破坏 D2 lifecycle） | ✅ | P-D3-1 **3/3** + P-D3-2 **2/2** + P-D3-3 **2/2** |
| 全量回归全绿 | ✅ | Compiler 208/208 + validate PASS + Full EditMode 1007/1007 + Full PlayMode 80/80 + `error CS = 0` |
| 当前插图内容审计完成（含 `Art Content Pending` 登记） | ✅ | §21.12.7 |

**结论：`WP-Event-1D Gate D3 Technical Verified / Awaiting Producer Play Acceptance`**（未 Commit / Push；未做 Final Closeout）。

**Open Issues / 遗留（不阻塞本 Gate；已同步 [问题清单](../问题清单.md)）**：

* **`Art Content Pending`**：正式 Event 插图内容尚未建立（`VisualAsset` 无 Event 行、`IllustrationAssetId` 为空）→
  卡面显示类别级占位图（无诊断噪音）。补内容 = 改配置 + 放源图 + 重导表（§21.12.7）。
* **Village Modal 无插图区域**：`VillageModal` 渠道的 `IllustrationAssetId` 当前**不被消费**（Village 弹窗没有插图宿主）；
  若制作人要求村庄也带插图，属**新增 UI 结构**的独立任务（必须复用同一 `ResolveEventIllustration`）。
* **Legacy 表现未迁移（刻意）**：Battle Encounter 卡 / Branch 路线卡 / 既有占位表现仍走 `JourneyEventPresentationCatalog`；
  迁移需要各自拥有正式且稳定的 Content 键，属后续任务（本 Gate 不删除、不改配置）。
* **测试债（沿用 D1 / D2 登记）**：`WP-Event-1D` 的 Unknown Legacy Pending 缺 UI 级自动化覆盖；
  D2 的「低补给会话整链拒绝」PlayMode 用例未新增。

#### 21.12.11 制作人 Play Acceptance：极短人工验收步骤（8 步）

> 前置：Unity 打开 `UnityProject/RRPG1.0game` → Play（`SampleScene`）；**不需要**改任何配置。
> 判定原则：**只**看「玩家实际看到 / 实际发生」什么；任一步出现「原始占位符 / 上一个事件的插图 / 空卡面 / 重复结算」即为**不通过**。

1. **进入村庄**：主菜单「继续」（或新游戏）→ 进入 Town（村庄）。
2. **制造事件**：对同一名村民连续安排 3 次「临时工程」→ 第 3 次开始后应弹出**统一事件弹窗**：
   标题 =「村庄里有人私下提起最近的安排」（来自 `EventPresentation`，**不是**内部 Id / 占位符）；正文含该村民的
   **显示名**（不是 `PersonId` / `RoleId`）；紧迫性 =「需要及时处理」；弹窗打开时 Village 时间暂停。
3. **看选项**：出现由配置生成的选项（「先给他一段恢复时间」/「继续让他承担」）；
   若某选项条件不满足 → 按配置 显示但不可点（Disable）或 不出现（Hide），**不得**强制塞两个固定按钮。
4. **做选择**：「先给他一段恢复时间」→ 结果面板出现（文案来自 Outcome 模板）→ 关闭后该村民进入休整
   （Commitment 面板可回看恢复窗口与本次选择）。
5. **进远征**：村庄 → 远征 Prep → Confirm（真实 Food → Carry）→ 进入 Journey。
6. **看正式事件卡**：走到**非战斗**节点（问号 / 商店 / 招募 等映射为 Event 的节点）→ 应出现**正式事件卡**：
   插图区 = 类别级占位图（当前没有事件 PNG → **`Art Content Pending`**，这是预期的，不是 bug）；
   标题 =「路边的废墟神龛」；正文含队员**显示名**。
7. **走完事件**：选「离开」→ 结果卡 →「继续」→ 该节点被结算、Journey 继续（天数 / 补给真实变化）；
   重来一次并选「强行清理」→ 进入战斗 → 战斗结束后回到 Journey 并结算**同一个**节点（不得跳过 / 不得重复）。
8. **存档往返**：Save → 退出 Play → 重新 Play →「继续」：事件历史（World Event History）与村民休整状态仍在；
   **不得**出现重复 Pending / 重复结算 / 卡面残留。

**人工验收要确认的三件事（这才是 Gate D3 的真正交付）**：

* 玩家看到的所有文案（标题 / 正文 / 选项 / 结果）都来自**配置**，没有任何 `{...}`、内部 Id 或硬编码占位文案；
* 两表面（村庄弹窗 / 远征卡面）对同一个事件的表现来自**同一套**配置与**同一个**渲染器（不会一边变一边不变）；
* 没有插图时只显示**类别级占位图**，且**绝不**显示其它事件的图；补插图只需改配置 + 放源图 + 重跑导表（不改代码）。

---

### 21.13 `WP-Event-1｜Producer Acceptance Repair`（制作人人工验收问题修复）

> **边界**：只处理本次人工验收发现的 A / B / C 三类问题（+ D 内容表意登记）。
> **不**扩展 Event 系统、不进入新功能、**不**修改 Supply 结算规则、**不**为了实现「看起来增加」而调数值、
> **不** Commit / Push、**不**做 Final Closeout。
> 完成状态目标：`WP-Event-1 Producer Acceptance Repair Technical Verified / Awaiting Producer Recheck`。

#### 21.13.1 Issue A｜Battle 卡「进入战斗」后错误出现 Branch / Event 卡

**制作人观察**：遭遇战斗卡 → 点「进入战斗」→ 没有进入 Prep / Battle → 又出现「前方出现岔路口」卡 → 选路线后又出现正式 Event 卡。

**分类：表现生命周期 Bug（presentation lifecycle；不是 Event Core / 结算规则问题）。**

**根因（读码 + 逐步推演）**：

| 环节 | 事实 |
|---|---|
| `JourneyPresentationCycleController.StartNewCycle()`（N&gt;1 投影） | 调用 `_driver.PresentTravelToBranch(nodes, cycle, () => OnArrivedAtBranch(cycle))` |
| `ExpeditionJourneyView.PresentTravelToBranch`（修复前） | `JourneyNodeRef target = nodes[0]; BeginTravel(target, onComplete);` —— **把候选列表的第一个节点当成「待进入节点」** |
| `ExpeditionJourneyView.CompleteTravelOnce()` | 只要 `_pendingConfirmNode.RequiresEncounterConfirmation`（Battle / 未迁移 Content）就 `PresentEncounterCard(...)` |
| 后果 | 若岔路口的**第一条路**恰好是 Battle 节点 → 旅行结束后先弹一张**「遭遇战斗 / 进入战斗」卡**（而这一 Cycle 根本不该进入任何节点） |
| 点击「进入战斗」 | 该卡 Option 的回调只是 `InvokeTravelCompletion()` → 完成本次 Travel → Cycle 的 `OnArrivedAtBranch` → 弹出**Branch 卡** |
| 之后 | 玩家真的选了一条路 → 下一个 Cycle（N=1）→ 若是非战斗节点 → **正式 Event 卡** |

即：**那张战斗卡是伪卡**（不进入任何节点），点击只是「把表现推进到岔路口」，因此表现与玩家预期（进入战斗）完全不符。
这解释了为什么既有测试没有拦住：D2 的 Battle 链是**在真实战斗节点上**点「进入战斗」（正确路径），从未覆盖「N&gt;1 投影下第一个候选是 Battle 节点」的组合。

**最小修复（只改表现闸口，不改规则）**：

1. `ExpeditionJourneyView.BeginTravel(node, onComplete, branchProjection)` 新增 Branch 投影标记；
   `PresentTravelToBranch` 以 `branchProjection: true` 开始 Travel（候选节点尚未被选中 → **不是**「进入」）。
2. 抽出纯函数 `ExpeditionJourneyView.ShouldPresentEncounterCard(JourneyNodeRef pendingNode, bool branchProjection)`
   = `!branchProjection && pendingNode.RequiresEncounterConfirmation`；`CompleteTravelOnce` 改用它。
3. **产品硬规则（制作人要求）**：点击 Battle Encounter 的「进入战斗」后，在 Battle / Prep 真正结束之前，
   不允许推进或展示下一张 Branch / 正式 Event Journey Card：
   * `ExpeditionJourneyView.PresentationLockedForBattle` + `LockPresentationForBattle()` / `UnlockPresentationAfterBattle()`；
   * `PresentCard(...)` 在锁定期**拒绝渲染任何新卡面**并给出警告；
   * 上锁点 = `ExpeditionJourneyCoordinator.HideForEncounter()`（Battle 真正开始的产品入口，同时 `CancelPresentation()` 使旧 Cycle / 旧回调失效）；
   * 解锁点 = `SetVisible(true)`（Journey 再次可见 = 战斗生命周期结束）→ 不会过锁导致后续卡面永远不显示。

**回归（新增）**：

* EditMode `PA_E1_BranchProjectionTravel_NeverPresentsEncounterCard`（闸口纯函数四态 + 源码级守卫）；
* PlayMode `PA_1_BattleEncounterCard_EnterBattle_IsNotReplacedByBranchOrEventCard`
  （真实产品链：走到 Battle 卡 → 「进入战斗」→ 断言真正进入战斗 + 上锁 + 逐帧不得出现 Branch / Event 卡 + 不得推进节点 + 战后恢复解锁）。
  **未删除任何 Branch / Event 内容或路径**（不以「删功能」让测试变绿）。

#### 21.13.2 Issue B｜「储备口粮没有增加补给」——先分类，不修结算规则

**制作人已知条件**：`储备口粮 = SupplyDelta +2 → AdvanceExpeditionTime +1`；当前队伍 `party = 2`、`rate = 1`。
→ 额外 1 天成本 `2 × 1 × 1 = 2`，理论净值 `+2 − 2 = 0`。

**分类：待证伪的观察（不是先验 Bug）。** 处理顺序按制作人要求：

| 时点 | 定义 | 期望 |
|---|---|---|
| **S0** | Option 前补给 | — |
| **S1** | Effect Commit 后、Node Settlement 前 | `S1 = S0 + 2` |
| **S2** | Node Resolve 后 | `S2 = S0 + 2 − party × (NodeElapsedDays + 1) × rate − ExtraSupplyCost` |

**判定规则（先分类再动手）**：

* 若 **S1 没有 +2** → 真 Product Bug，修 Gateway / Commit（本 Repair 未观察到该情形，见 §21.13.5）；
* 若 **S1 正确 +2，而 S2 回到 S0** → **系统正确**，只是 Test Content / UI 反馈容易误导（登记为 §21.13.4 的表意问题）。

**诊断能力（新增，可长期复用）**：

* EditMode `PA_E2_StoreRationsThreeStageProjection_MatchesProductFormula`（用产品 `ExpeditionSupplyState` 的**真实公式**
  `ComputeTotalCost` / `TryApplyDelta` / `TryConsume` 走三时点，并断言两人队净值 = 0 是**算术预期**）；
* PlayMode `PA_3_StoreRations_CommitsPlusTwo_ThenSettlesTimeCost`（真实产品链：走 `driveToEventCard` → 点「储备口粮」→ 点「继续」，
  读取**真实** party / rate / NodeElapsedDays / ExtraSupplyCost 计算期望值，并输出一行
  `SUPPLY_REPORT event=… party=… rate=… nodeElapsed=… extraCost=… S0=… S1=… S2=… expectedS2=…` 作为人工可核对证据）。

**明确不做**：不为了让数值「看起来增加」而修改 Supply 结算规则、成本公式或 `SupplyDelta` 语义。

#### 21.13.3 Issue C｜补给不足的 Option 不应只在点击后才失败

**制作人观察**：Event Option 可点击 → 点击 → `EventOptionExecutor` Preflight 拒绝（预计补给 0 < Node 成本 2）。

**分类：Player UX（反馈时机）**——底层 **Fail Closed / 零 mutation 本身是正确的**（不重复结算、不写 History、不改 Supply），
问题只是「玩家只能在点击后才知道」。

**目标（制作人指定）**：如果当前 `EventInstance` + Option 的**确定性 Outcome 已确定**，且 Preflight 能证明该 Option 当前**必然**不能执行，
则 UI 直接把 Option 标为 **Disabled** 并显示简短原因（例：`补给不足：本次行动至少还需要 2 点补给`）。

**实现（复用，不新造第二套判断器）**：

* 新增只读 API `ExpeditionEventFlow.TryPreviewOptionFeasibility(adventure, optionId, out EventOptionFeasibilityPreview preview)`：
  * 复用既有只读 `EventOptionExecutor.PreviewOutcome`（与正式执行**同一**选择函数 → 同一 Outcome 身份）；
  * 复用既有只读 `TryEvaluateSettlementFeasibility`（有序 SupplyDelta / AdvanceExpeditionTime 投影 + 结算成本）；
  * **不**执行 Option、**不** Commit、**不**写 History、**不**改 Outcome、**不**扣补给、**不**预生成阵容（因此**不消耗 / 不改变 RNG**）。
* 语义（防止误置灰）：`Applicable == false` = **证明不了**必然失败（例如 StartBattle 需要预生成阵容、阶段不是 AwaitingOption、Outcome 不确定）
  → 调用方**必须保持原可用性判断**；只有 `Applicable && !Feasible` 才置灰。
* 卡面（`ExpeditionEventCardAdapter.TryBuildOptionCard`）：置灰 + `SubLabel` = 短原因；点击路径仍由产品侧
  `ExpeditionUI.OnEventOptionChosen` 的 `if (!option.Interactable)` 拒绝执行（零 mutation）。
* 条件变化后**重新构造卡面**即重新评估（补给补足 → Available），不需要新状态、不需要监听。
* 最小附带调整：`TryEvaluateSettlementFeasibility` 在**失败路径**同样回填 `projectedSupply / pendingExtraDays / totalCost`
  （供计算「还差多少」；**不改变任何判定逻辑与结论**）。

**测试**：EditMode `PA_E3`（短原因文本 + 默认语义）/ `PA_E4`（只读源码守卫 + 卡面置灰接线）；
PlayMode `PA_2_InsufficientSupplyOption_IsDisabledBeforeClick_ThenRefreshRestores`（置灰 + 原因可见 + 点击零 mutation + 补足后刷新 Available）。

#### 21.13.4 Issue D｜Test Content 表意（登记，不在本 Repair 冻结数值）

**当前 Test 内容**：`储备口粮 = +2 Supply +1 day`；两人队（`party=2, rate=1`）→ **最终净 Supply = 0**。

**分类：`Test / Not Frozen Content` 的表意问题**（不是规则 Bug）。

**建议（留给正式内容生产，本 Repair 不自行冻结）**，二选一：

1. 修改 `Label` / `ResultText`，把「**获得补给但耽搁时间**」表达清楚（例如说明「立刻 +2，但多花 1 天，净效果取决于队伍规模」）；
2. 或重新平衡 `SupplyDelta` / `AdvanceExpeditionTime`（正式数值属内容任务，需制作人裁定）。

**本 Repair 明确不做**：不改 XLSX、不改 Mirror、不改任何数值或文案——只登记在案。

#### 21.13.5 修复验证（真实结果 + Issue B 分类结论）

**全部验证都在本 Repair 最后一次代码 / 测试变更之后**（其后仅 `Docs/` 变更）：

| 验证 | TOTAL | PASSED | FAILED | 证据文件 |
|---|---:|---:|---:|---|
| 定向 EditMode（`ProducerAcceptanceRepairTests`：PA_E1–PA_E4） | 4 | **4** | **0** | `d3r_edit3.xml` / `.txt` |
| 定向 PlayMode（`ExpeditionEventProductChainPlayTests.PA_*`） | 3 | **3** | **0** | `d3r_play2.xml` / `.txt` |
| 受影响 legacy PlayMode 用例（见下） | 1 | **1** | **0** | `d3r_play4.xml` |
| Full EditMode | 1011 | **1011** | **0** | `d3r_full_edit.xml`（= D3 基线 1007 + 新增 4） |
| Full PlayMode | 83 | **83** | **0** | `d3r_full_play3.xml`（= D3 基线 80 + 新增 3） |
| Compiler full（`dotnet test -c Release`） | 208 | **208** | **0** | `d3r_compiler.txt`（`COMPILER_TEST_EXITCODE=0`） |
| `RRPG_检查配置.cmd`（validate，只读） | — | — | — | **`结果：PASS errors=0 warnings=5` / `RESULT: PASS` / `VALIDATE_EXITCODE=0`**（`d3r_validate.txt`） |

* **失败明细：全部套件 `FAILED = 0`** → 不存在失败用例。
* 证据目录：`D:\BBB\_rrpg_runs\d3\`（**仓库外**）。
* 环境说明：本轮多次遇到「Unity 启动 / 资源重导入较慢」，其中一次 `taskkill` 后**必须重跑**（`d3r_edit1` 因残留 3 个
  Tuanjie 进程而未产出结果 → 清理后 `d3r_edit2/3` 正常）；另有链式命令被后续命令打断导致 `.txt` 汇总缺失的情况 →
  所有结论均以**测试框架自身写出的 `.xml`** 为准（`.txt` 仅作辅助）。

**Issue A 运行时证据（真实产品链，`PA_1`）**：点击「进入战斗」后日志出现
`Battle 节点原子进入成功 … / [BattleUI] 已绑定本场敌方阵容` ✓；随后逐帧断言期间**没有**出现任何新卡面 ✓、
节点未推进 ✓、旧 Presentation Cycle 失效 ✓、`PresentationLockedForBattle == true` ✓；
战斗结束（`OnReturnFromBattle(true)`）后 Journey 恢复且解锁 ✓。

**Issue B 分类结论（三时点实测）**：

* EditMode `PA_E2`（产品公式纯投影）：`S0 = 5 → S1 = 7（+2）→ S2 = 5`，
  `cost = party × (NodeElapsedDays + 1) × rate + ExtraSupplyCost = 2 × 1 × 1 + 0 = 2` ✓；
* PlayMode `PA_3`（真实产品链）：输出一行 `SUPPLY_REPORT event=… party=… rate=… nodeElapsed=… extraCost=… S0=… S1=… S2=… expectedS2=…`
  并断言 `S2 == expectedS2` ✓（该行即人工可核对的诊断证据）；
* ⇒ **判定：S1 正确 `+2`，S2 回到 S0 → 系统正确**（**不是** SupplyDelta Bug）。
  「看不出补给增加」来自两人队下 `+2 补给 − 2 时间成本 = 0`；表意问题登记在 §21.13.4。
  **未修改**任何 Supply 规则 / 公式 / `SupplyDelta` 语义。

**Issue C 实测（`PA_2`）**：补给降到 0 后，「离开」在**点击之前**即 `interactable == false` 且 SubLabel 显示
`补给不足：本次行动至少还需要 2 点补给` ✓；仍可行的「储备口粮」**未**被误置灰 ✓；
直接触发 `onClick` 后 Pending / 阶段 / 补给 / Session History / 节点结算**全部零 mutation** ✓；
补给补足 → 重新构造卡面（刷新）→ 恢复可点 ✓。

**受影响既有用例的最小迁移（1 条；不是「删功能让测试变绿」）**：

`JourneyVisualFoundationPlayTests.Journey_ConsecutiveTransitions_OnePerCycle_AndBranchNotAutoSelected`
原先隐含依赖「消费一个 Transition 后必然建立新 Cycle」；按 Issue A 的新硬规则，**进入 Battle 节点后**表现被冻结，
该不变式只对**非 Battle** 进入成立（此前它能通过，正是因为它先穿过那张**伪战斗卡**再走岔路 → 间接依赖了缺陷行为）。
迁移内容：

1. 进入 Battle 时改为断言 `PresentationLockedForBattle == true`（并结束推进循环）；
2. 循环后若仍有 Active Battle → 先用**产品入口** `OnReturnFromBattle(true)` 结束战斗 → 断言已解锁 → 再执行原有 Branch 截图兜底。

其余断言（一次 Cycle 最多消费一个 Transition、Branch 不得被 Continue 代选、Supply 不足明确拒绝…）**原样保留** ✓。



#### 21.13.6 变更文件（精确列表）

**产品代码（5 个文件）**

| 文件 | 变更（Issue） |
|---|---|
| `Assets/Scripts/UI/ExpeditionJourneyView.cs` | **A**：`BeginTravel(..., branchProjection)`；`PresentTravelToBranch` 标记 Branch 投影；抽出纯闸口 `ShouldPresentEncounterCard`；新增 Battle 表现锁（`PresentationLockedForBattle` / `LockPresentationForBattle` / `UnlockPresentationAfterBattle`，`SetVisible(true)` 解锁）；`PresentCard` 在锁定期拒绝渲染新卡面 |
| `Assets/Scripts/UI/ExpeditionJourneyCoordinator.cs` | **A**：`HideForEncounter()`（Battle 真正开始）上锁 |
| `Assets/Scripts/EventHost/ExpeditionEventFlow.cs` | **C**：新增只读 `TryPreviewOptionFeasibility`；`TryEvaluateSettlementFeasibility` 失败路径回填投影值（**判定逻辑不变**） |
| `Assets/Scripts/EventHost/EventOptionFeasibilityPreview.cs` | **C（新）**：预测结果结构（`Applicable` / `Feasible` / `PlayerReason` / 投影值）+ 玩家短原因纯函数 |
| `Assets/Scripts/EventHost/ExpeditionEventCardAdapter.cs` | **C**：卡面按「点击前只读预测」置灰 + `SubLabel` 短原因 |

**测试（1 个新增文件 + 1 个追加 + 1 条既有用例最小迁移）**

* 新 `Assets/Tests/EditMode/EventProduct/ProducerAcceptanceRepairTests.cs`（`PA_E1`–`PA_E4`）；
* `Assets/Tests/PlayMode/ExpeditionEventProductChainPlayTests.cs`：**追加** `PA_1` / `PA_2` / `PA_3` 与 3 个辅助方法（复用该文件既有的真实产品 Bootstrap / Drive 助手）；
* `Assets/Tests/PlayMode/VillageV4/JourneyVisualFoundationPlayTests.cs`：**1 条既有用例的最小迁移**
  （`Journey_ConsecutiveTransitions_OnePerCycle_AndBranchNotAutoSelected`；原因与迁移内容见 §21.13.5 —— 它原先间接依赖 Issue A 的缺陷行为）。

**文档**：本文件 §21.13；`Docs/DevelopmentSpecs/README.md`；`Docs/问题清单.md`。

**零改动清单（刻意）**

* **Supply 结算规则与公式**（`ExpeditionSupplyState` 的 `ComputeTotalCost` / `CanAfford` / `TryConsume` 语义、`SupplyDelta` / `AdvanceExpeditionTime` 语义、
  Node Settlement 的 `party × totalDays × rate + ExtraSupplyCost`）——**未改动**（Issue B 结论见 §21.13.5）；
* Event Core（`EventResolver` / `EventOptionEvaluator` / `EventOptionExecutor` 执行与 Commit 行为 / `EventTemplateRenderer` / `EventStaticCatalog`）、
  Event Gateway 的 Commit / History 写入行为；
* Battle / Encounter、Progression / Save、Scene / Prefab / ScriptableObject、Config（XLSX）/ Canonical Mirror / 生成根；
* **Branch / Event 内容与路径**（Issue A 只改闸口与生命周期锁，**不删除**任何 Branch / Event 内容或路径）。

#### 21.13.7 制作人复验步骤（最小集，3 步）

1. **再点一次 Battle Encounter 的「进入战斗」** → 应**直接进入 Prep / Battle**；
   战斗生命周期结束前**不得**出现「前方出现岔路口」或正式 Event 卡（也不得推进到其它节点）。
2. **看补给不足的 Option** → 应**直接置灰**并显示短原因（例：`补给不足：本次行动至少还需要 2 点补给`），
   不需要点击才发现；补足补给后重新打开 / 刷新卡面即恢复可点。
3. **再点一次「储备口粮」** → 确认表现 / 数值与预期一致：
   选择后立刻 `+2`（结果面板可见），「继续」结算后按 `party × (节点天数 + 1) × rate` 扣减；
   两人队（`party=2, rate=1`）**净值为 0**（这是当前 Test 内容的算术结果，不是 Bug；表意问题见 §21.13.4）。

---

### 21.14 `WP-Event-1｜Producer Acceptance Repair 2`（Issue E：Entry 失败仍推进 Journey Presentation）

> **边界**：只修 **Journey Presentation lifecycle**。不改 Supply / Progression / `ValidateEnterNode`；不隐藏日志；
> 不做「点击后关卡再开」这类假修；不自动改选 / 不自动重试；不 Commit / Push；不做 Final Closeout。
> 完成状态目标：`WP-Event-1 Producer Acceptance Repair 2 Technical Verified / Awaiting Producer Recheck`。

#### 21.14.1 现象与真实根因（读码 + 调用栈逐点确认）

**现象**：补给不足后，Journey 仍继续往后跳节点；点新节点虽然打印
`拒绝进入战斗节点（节点未被进入）：…InsufficientSupply…`，但随后**又出现后续节点**。

**真实调用栈**：`JourneyEventCardView.Choose` → `ExpeditionJourneyView.ShowBranchChoice`
→ `JourneyPresentationCycleController.OnArrivedAtBranch` → `ChooseBranch`
→ `ExpeditionJourneyCoordinator.EnterNodeViaProductEntry` → `ExpeditionUI.OnProgressionNodeChosen`
→ `ExpeditionUI.HandleNodeClick` → `InsufficientSupply`。

**逐点回答（读真实代码）**：

| 问题 | 事实 |
|---|---|
| `HandleNodeClick` / Product Entry 是否返回成败？ | **否** ✗：`private void HandleNodeClick(...)` / `public void OnProgressionNodeChosen(string)`（修复前） |
| `EnterNodeViaProductEntry` 是否吞掉失败？ | **是** ✗：`private void EnterNodeViaProductEntry(...)` → `_owner.OnProgressionNodeChosen(nodeId);`（**丢弃**结果） |
| `ChooseBranch` 失败后是否仍无条件推进？ | **是** ✗：先 `IsAwaitingBranch = false; _cycleOpen = false; ConsumedTransitionsInCurrentCycle = 1; _cycleIndex++; _driver.HideBranchChoice();` 再 `_enterNode?.Invoke(nodeId)`，最后**无条件** `RequestReSchedule("branch")` |
| 后续节点表现从哪来？ | 上述 `RequestReSchedule` → 新 Cycle 重新读取 Current Progression State（**节点其实未变**）→ 又给出「下一步」呈现 → 玩家看到「继续往后跳」 |
| 同类问题是否只存在于 Branch？ | **否** ✗：`CompleteCurrentTravelPresentation`（N=1 的 Travel 完成后入口）同样是「先消费 + 无条件重调度」 |

**结论**：`ValidateEnterNode` 的拒绝与「零 mutation」都是**正确的产品行为**；
缺陷在**表现层没有消费产品入口的成败**——它自行「假设成功」并推进。**不是 Supply / Progression 问题。**

#### 21.14.2 正式产品规则（本 Repair 冻结）

> **Journey Presentation 只能在产品 Node Entry 真正成功后推进。**

* **Success**：才允许 `consume current presentation → complete current cycle → begin next Journey presentation`；
* **Failure（例：`InsufficientSupply`）**：`CurrentNode` 不变、`Progression` 不变、`Supply` 不变、`elapsed` 不变、
  **当前 Branch 呈现保持可交互**、**不进入下一 Cycle**、**不产生后续节点呈现**、**不自动重试**；
  玩家仍可选择其它合法路线或使用既有 Retreat / Return 流程。
* **统一语义**：**任何** Product Entry 失败都不得推进（不只是 Battle 节点；含 Event Resolver 失败、
  Encounter 生成失败、node already entered、invalid phase 等）。
  表现层只看 **Entry Success / Failure**，**不**自行计算 Supply / Node legality / Progression。

#### 21.14.3 修复（返回契约 + 唯一消费点）

1. **产品入口返回真实成败**（复用仓库既有调用路径，不新建第二套判断）：
   * `ExpeditionUI.HandleNodeClick(...)`：`void` → **`bool`**（每个 `return;` 明确改成 `return false;` / `return true;`；
     Battle 已开始或 Event Node 已原子进入 = `true`）；
   * `ExpeditionUI.OnProgressionNodeChosen(string)`：`void` → **`bool`**（= `HandleNodeClick` 的结果；arity 不变，兼容既有调用方）；
   * `ExpeditionJourneyCoordinator.EnterNodeViaProductEntry`：`void` → **`bool`**（返回 `_owner.OnProgressionNodeChosen(...)`）。
2. **Presentation Cycle 只消费结果**：
   * `JourneyPresentationCycleController` 的构造参数 `Action<string> enterNode` → **`Func<string, bool> enterNode`**；
   * 新增唯一调用点 `TryEnterNodeViaProductEntry(nodeId)`：记录诊断 `LastEntryRejected` / `LastEntryRejectedNodeId`；
   * `ChooseBranch`：**先**请求 Entry；失败 → `return false`，且**不**结束 Branch 呈现、**不**消费 Transition、
     **不** `HideBranchChoice()`、**不** `_cycleIndex++`、**不** `StartNewCycle()`（不重调度）；
   * `CompleteCurrentTravelPresentation`：同样先请求 Entry；失败 → `return false`，本 Cycle 保持 open、
     `_pendingTransitionNodeId` 保留、**不**重调度（不出现后续呈现、不自动重试）。
3. **不做**（按制作人禁止项）：不隐藏错误日志、不「点击后关卡再开」、不延迟 1 帧继续、不自动改选、
   不临时补 Supply、不绕过 `ValidateEnterNode`、不把失败标记为 presentation complete。
4. **Branch Card 生命周期**：失败后卡面**不消失、不重建、不推进**（最低要求）；
   「Disable + 原因」的强化（复用 `ValidateEnterNode` 只读结果）本 Repair **刻意不做**，避免复制 Supply 合法性判断（见 §21.14.6 遗留）。

#### 21.14.4 测试（新增 / 适配）

**EditMode**（`JourneyPresentationCycleTests`，直接驱动 Presentation Cycle，确定性）：

* `Branch_EntryRejected_KeepsCurrentPresentation_AndDoesNotAdvance` —— Branch 选择被产品入口拒绝 →
  不消费 Transition、不推进 Cycle、不重调度、不 `HideBranchChoice`、仍 `IsAwaitingBranch`；
  连续第二次点击仍失败且无副作用；改选合法路线（`NextEntrySucceeds = true`）才推进到下一 Cycle。
* `Travel_EntryRejected_DoesNotAdvanceCycle` —— N=1 Travel 完成后入口失败 → 不推进；条件变化后重试成功才推进。
* Harness 适配：`enterNode` 由 `Action<string>` 改为 `Func<string, bool>`，新增开关 `NextEntrySucceeds`（默认 `true`，不影响既有用例语义）。

**PlayMode**（`ExpeditionEventProductChainPlayTests`，真实产品链）：

* `PA_4_BranchEntryFailure_DoesNotAdvancePresentation` —— 真实走到**含付费候选**（进入成本 &gt; 0）的岔路口 →
  补给降到 0（走正式 Supply 增量入口）→ 点击路线 → 按**产品入口的真实结果**分支断言：
  * **被拒绝**：`LastEntryRejected` = true 且被拒 NodeId ∈ 当前候选；`CurrentNode` / 该节点进入状态 / Supply / elapsed /
    该节点结算状态**全部零 mutation**；`CycleIndex` 不变；**仍是同一张岔路口卡**（Option 数不变）；仍 `IsAwaitingBranch`；Journey 仍可见；
    **再点一次同一路线**：仍被拒绝、不累计 Cycle、不生成第二张卡、零 mutation；
    随后**补足补给再点同一路线** → 必须真正进入且 Cycle 才推进（证明「失败不推进 / 成功后推进」两侧都成立）。
  * **点击未生效**（既未拒绝也未进入）：断言表现与状态**同样零变化**（`CycleIndex` / `CurrentNode` / 补给）——
    即「未成功进入 ⇒ 不得推进」的安全方向。
* **为什么不断言「必须先被拒绝」**：默认 Test Graph 的首层节点 `NodeElapsedDays = 0` → 进入成本 0 → 任何补给都能进入，
  无法在该层构造 `InsufficientSupply`；深层岔路口的候选是否为「全部付费」由随机图决定。
  因此 **「拒绝 ⇒ 不推进」的确定性覆盖由 EditMode 的 `JourneyPresentationCycleTests` 承担**
  （直接注入 `enterNode = false/true`，逐条断言不消费、不推进、不重调度、可重试 ✓ 完全确定 ✓）；
  PlayMode 用例负责**真实产品链**上的行为一致性（真实入口 + 真实卡面 + 真实状态）。
* 真实产品级证据（诊断实测，已并入结论）：`PA_DIAG_ENTRY node=node_2 cost=2 carry=0 entered=False current=node_0`
  —— **产品入口在补给不足时确实拒绝且零 mutation**（本 Repair 未修改它，只是让表现层消费该结果）。
* 相邻路径 targeted 复跑：`PA_1`（Battle 卡生命周期 + 表现锁）、`P_D2_1`（Event 非战斗链）、
  `P_D2_3` / `P_D2_3b`（StartBattle 同一 Node 结算）——确认本修复**未**破坏 Repair A 的锁与 D2 生命周期。

#### 21.14.5 修复验证（真实结果）

| 验证 | TOTAL | PASSED | FAILED | 证据文件 |
|---|---:|---:|---:|---|
| 定向 EditMode（`JourneyPresentationCycleTests` + `ProducerAcceptanceRepairTests`） | 17 | **17** | **0** | `e_edit4.xml`（该 XML 同时报告 EditMode 全套件计数 = **1013** ✓ = 1011 基线 + 新增 2 ✓） |
| 定向 PlayMode（`ExpeditionEventProductChainPlayTests`：`P_D2_1/2/3/3b` + `PA_1..PA_4`） | 8 | **8** | **0** | `e_play6.xml` / `.txt`（`error CS = 0`） |
| Full PlayMode（**全部 Issue E 变更之后**） | 84 | **84** | **0** | `e_full_play.xml` / `.txt`（`error CS = 0`；84 = 83 基线 + `PA_4`） |
| Full EditMode（**最终树**，Closeout 复跑） | 1013 | **1013** | **0** | `f_edit.xml` / `.txt`（`error CS = 0`） |
| Compiler full（`dotnet test -c Release`） | 208 | **208** | **0** | `e_compiler.txt`（`COMPILER_TEST_EXITCODE=0`） |
| `RRPG_检查配置.cmd`（validate，只读） | — | — | — | **`结果：PASS errors=0 warnings=5` / `RESULT: PASS` / `VALIDATE_EXITCODE=0`**（`e_validate.txt`） |

* **Repair-2 之后的 Full EditMode 复跑（最终树）**：本环境曾**连续 3 次在「编辑器启动阶段」停滞**（无 XML 产出；与 D2 §21.11.9 登记同类环境现象，
  非测试失败）。按环境处理纪律（清残留 Tuanjie 进程 → 确认工程未被占用 → 重跑）后，第 4 次在**最终树**上完整产出
  **1013 / 1013 PASS**（`f_edit.xml`，`error CS = 0`）= 基线 1011 + 新增 2 ✓ 计数自洽。
  1. 定向 EditMode 的 XML 头明确报告 **EditMode 全套件 `testcasecount = 1013`**（= 基线 1011 + 新增 2 ✓ 计数自洽）；
  2. 新增的 2 条 Issue E 用例在定向运行中 **PASS** ✓，且它们**只新增用例**、未改动任何既有用例语义
     （`Harness` 仅新增开关 `NextEntrySucceeds`（默认 `true`）✓ 旧行为不变）。
* 失败明细：上表所有已产出套件 `FAILED = 0` ✓（无失败用例）。
* 证据目录：`D:\BBB\_rrpg_runs\d3\`（**仓库外**）。
* 环境说明：本轮多次遇到「Unity 启动 / 资源重导入很慢」与上述启动停滞；结论一律以**测试框架自身写出的 `.xml`** 为准
  （`.txt` 仅作辅助 ✓）。

**Issue E 的运行时证据（真实产品链）**：

* `PA_4` 覆盖两侧：**被拒绝** → `LastEntryRejected` / 被拒 NodeId / `CurrentNode` / 节点进入与结算状态 / Supply / elapsed
  全部零 mutation、`CycleIndex` 不变、仍是同一张岔路口卡、再点一次仍失败、补足补给后再点才真正进入并推进；
  **点击未生效** → 表现与状态同样零变化。
* `PA_DIAG_ENTRY node=node_2 cost=2 carry=0 entered=False current=node_0`
  —— 证明**产品入口本身拒绝正确且零 mutation**（本 Repair **未修改** `ValidateEnterNode` / Supply / Progression ✓，
  只是让表现层消费该结果 ✓）。



#### 21.14.6 变更文件（精确列表）

**产品代码（3 个文件）**

| 文件 | 变更 |
|---|---|
| `Assets/Scripts/Journey/Runtime/JourneyPresentationCycle.cs` | `enterNode` → `Func<string, bool>`；`TryEnterNodeViaProductEntry` 唯一调用点 + `LastEntryRejected` / `LastEntryRejectedNodeId` 诊断；`ChooseBranch` / `CompleteCurrentTravelPresentation` 改为「Entry 成功才消费 / 推进」 |
| `Assets/Scripts/UI/ExpeditionJourneyCoordinator.cs` | `EnterNodeViaProductEntry` → `bool`（返回产品入口真实结果）；转发 `LastEntryRejected` / `LastEntryRejectedNodeId` 供诊断 / 测试 |
| `Assets/Scripts/UI/ExpeditionUI.cs` | `HandleNodeClick` → `bool`（8 处退出点明确成败）；`OnProgressionNodeChosen` → `bool` |

**测试（2 个文件）**

* `Assets/Tests/EditMode/ExpeditionJourney/JourneyPresentationCycleTests.cs`（Harness 适配 + 2 条新用例）；
* `Assets/Tests/PlayMode/ExpeditionEventProductChainPlayTests.cs`（新增 `PA_4` + 只读辅助 `AvailableNodeIds()` /
  `CandidateCostOf()` / `FirstPricedCandidateIndex()` / `AllCandidatesPriced()`；**「重复点击非法路线」的确定性覆盖
  改由 EditMode 用例承担**，避免依赖随机图中「卡面 option ↔ 候选节点」的映射细节）。

**文档**：本文件 §21.14；`Docs/DevelopmentSpecs/README.md`；`Docs/问题清单.md`。

**零改动清单（刻意）**：Supply 规则 / 公式 / `ExpeditionSupplyState`、Progression Resolver 与 `ValidateEnterNode`、
Event Core（Resolver / Executor / Evaluator / Gateway）、Battle / Encounter、Save、Scene / Prefab / SO、Config / Mirror / 生成根。

**遗留（登记，不阻塞）**：

1. **Branch 卡未把「必然不可进入」的路线置灰**：本 Repair 只保证「失败不推进、卡面保持可交互」（最低要求）。
   若未来要做 Disabled + 原因，**必须**复用 `ValidateEnterNode` 的只读结果（不得新建第二套 Supply 合法性判断）。
2. `CompleteCurrentTravelPresentation` 内保留了一条**历史注释**（原产品入口调用行；唯一调用点已上移到方法开头的守卫），
   仅为最小改动留痕，不影响行为。
3. 既有「测试债」沿用不扩大：Test Content 表意（§21.13.4）、`Art Content Pending`、Village Modal 插图宿主。

#### 21.14.7 制作人复验步骤（5 步）

1. 把补给消耗到**不足**（走真实远征路径即可）。
2. 走到**岔路口**（Branch 卡）。
3. 点一条 `InsufficientSupply` 路线。
4. 确认报明拒绝（`…InsufficientSupply…`）之后**仍停在当前岔路口**：不跳节点、不出现后续卡、补给 / 天数不变。
5. 再尝试其它合法路线 / Retreat，确认仍可操作（补足补给后点同一路线应能真正进入）。
























