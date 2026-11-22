# Authoring — Development/Config

已迁移领域的表格数据入口是本目录工作簿（`.xlsx`）；敌方图片源文件放在 `Development/SourceAssets/Enemy/`。

> 当前入口核对（2026-10-08）：九本工作簿复用同一Compiler / Mirror / Builder / Registry。V16-D新增窄的Village Presentation绑定；Event、AutonomousLife、Social均已接产品与Save；下方日期化阶段只说明当时边界，操作规则仍按本手册。

## 规则

* **表格数据由 XLSX 人工维护**（D-019）；**源图由 `Development/SourceAssets/` 人工替换**。`Development/Generated/StaticConfig/*.json` 与 Unity `Assets/Generated/StaticConfig/` 生成资产不可人工编辑。
* 每个领域一本工作簿（FDS §6.2）：Wave 1 为 `RRPG_Monsters.xlsx` + `RRPG_EnemyPresentation.xlsx`；共享视觉资源独立为 `RRPG_VisualAssets.xlsx`；事件内容为`RRPG_Events.xlsx`；自主选择/投影为`RRPG_AutonomousLife.xlsx`，关系解释规则为`RRPG_Social.xlsx`。
* `.xlsx` 在 Git 中是 **binary**（`.gitattributes` 已冻结）→ 同一文件不要多人同时改。
* 不得把工作簿放进 Unity 的 `Assets/`：Authoring 与 Runtime 资产是两个世界。

## 工作表写法（由 Compiler 强制）

每个 Sheet 的行 1–4 是表头区，业务字段从 **B 列**开始（A 列是给制作人看的说明栏，不参与导出）：

| 行 | 用途 | 能不能改 |
|---|---|---|
| A 列 | 说明栏（A1 机器字段 / A2 数据类型 / A3 规则 / A4 中文说明 / A5+ 行备注） | 可以改（不导出） |
| 行 1 | 机器字段（字段身份） | **不要改** |
| 行 2 | 数据类型（`id` / `int` / `float` / `bool` / `string` / `ref:<实体>`；或 `ignore` / `note`） | **不要改** |
| 行 3 | 规则（留空 = 必填；支持 `default=` / `>=` / `<=` / `allowed=A\|B` / `optional` / `key`） | **不要改** |
| 行 4 | 中文表头 | 可以改（不导出） |
| 行 5+ | 数据 | 可以改 |

**可以随便做**：调整列顺序、插入备注列（该列行 2 写 `ignore` 或 `note`）、改中文表头、写行备注。
以上都不会改变导出结果（列序无关性由自动化测试守住）。

## 当前工作簿与职责

| 工作簿 | 人工内容 / Authority | Runtime消费者 |
|---|---|---|
| RRPG_Monsters.xlsx | Monster / MonsterEncounterProfile / MonsterRole | MonsterRegistry / Encounter |
| RRPG_EnemyPresentation.xlsx | MonsterId→VisualAssetId | EnemyPresentationResolver |
| RRPG_VisualAssets.xlsx | 共享VisualAsset / SourceFile / ImportPreset | VisualAssetCatalog，敌方与正式Event插图 |
| RRPG_Encounters.xlsx | Encounter / Slot / Rule / Pool / Entry | EncounterGenerator→冻结EnemyRoster |
| RRPG_Events.xlsx | 十张统一事件内容表 | Village / Expedition Event；不拥有其他域状态 |
| RRPG_AutonomousLife.xlsx | BehaviorChannel、PersonBehavior/Preference、Place、Activity / Provider / Binding / Repetition / Calibration、ActivitySocialProjection | Selection Host不可变Snapshot / Catalog；Activity→Fact语义投影 |
| RRPG_Buildings.xlsx | BuildingDefinition：BuildingDefinitionId / DisplayName / Category / FootprintWidth / FootprintDepth | BuildingRegistry → Home Catalog / Placement；4种白模，Test / Not Final Balance |
| RRPG_Social.xlsx | RelationalFactType / RelationshipInterpretationRule / SocialCalibration | Social-owned ConfiguredRelationshipInterpreter |
| RRPG_VillagePresentation.xlsx | BuildingPresentationBinding：Definition + Lifecycle → PresentationKey；VillagePresentation：PresentationKey → 共享VisualAssetId | HomePresentationResolver → 持久Building View；只负责Sprite表现 |

### Village Presentation D 制作约束

`BuildingPresentationBinding`以BuildingDefinitionId + Lifecycle为复合键，Definition引用BuildingDefinition，Lifecycle只允许UnderConstruction / Completed，PresentationKey引用VillagePresentation。`VillagePresentation`以PresentationKey为身份，VisualAssetId引用唯一共享VisualAsset表。两张表经同一通用Compiler生成同名Canonical Mirror；Builder生成VillagePresentationDatabase，挂既有RRPGStaticConfigRoot，由StaticConfigRegistry提供只读入口。只增加必要字段登记、Mirror读取和生成容器，不建立另一套Compiler或Asset Catalog。

换源图：替换`Development/SourceAssets/Village/farm-proof.png`，保持`va_village_farm_basic`不变，再Compiler build / Unity Builder。换绑定：只改VillagePresentation农田两行的VisualAssetId为`va_village_farm_visual_b`，不覆盖PNG，再重新导表。样图与可复现输入见[TechnicalProof](../SourceAssets/Village/TechnicalProof/README.md)，均为Technical Proof / Not Final Art，正式交付恢复A图绑定。

正常重建后Home的既有建筑视图在下次读取刷新时重解析Sprite，不销毁建筑实体；退出并重新进入Home也消费最新配置。建筑图片按现有Footprint归一化显示，Collider、占地与选中框继续使用Gameplay Footprint，选中框不复制图像。此路径只证明Editor中的显式Compiler / Builder重建刷新，不承诺已发布Player自动监听源文件。不修改Gameplay / Save、实例身份、位置、施工、岗位或分配，不验证Prefab / Animator / Spine / Material等类型。

若其它工作簿已有未授权dirty内容，先使用隔离Compiler root与已发布该领域输入，验证无关Mirror哈希不变再同步本任务生成输出；不要把制作人的dirty Events一起导入。

### Building C1/C2 制作约束

BuildingDefinition维护五个静态字段，FootprintWidth / FootprintDepth 为正整数，以 Row3 `>=1` 沿通用 Compiler 校验。ID 定义种类，不是建筑实例。房屋 / 农田 / 厨房 / 工坊为白模测试内容；Definition不含施工数值、资源、容量或VisualAsset。另表BuildingFunctionBinding维护BuildingDefinitionId / FunctionSlotId / FunctionType / FunctionProfileId；当前仅Workplace，Profile引用既有ws_farm_01 / ws_kitchen_01 / ws_test_01 / ws_test_02。Compiler验证重复Slot、类型及Profile，生成BuildingFunctionBinding.json；Builder与Registry读回，禁止手改镜像。房屋无功能，农田/厨房/工坊绑定白模岗位；实例容量继承Profile，不能混同静态Profile与实例ID。C2实际施工及Completed岗位已验收；动态资源产出Deferred，固定资源批次尚未消费实例岗位。BuildingDefinition.json 是 Compiler 生成镜像，不手改；Builder 生成 BuildingConfigDatabase 并挂同一 RRPGStaticConfigRoot，Runtime 从 StaticConfigRegistry.TryGetBuildings 读取目录。缺库或空库给出诊断。

### AutonomousLife / Social 制作约束

Selection与Arbitration算法在Runtime，不在表内写脚本。missing Behavior / Preference为Neutral / 0且读取不创建状态；InjuryLight / InjuryHeavy / InjuryDisabled等必需Calibration由Compiler及Runtime Adapter验证。PersonDefinitionId仅静态画像provenance，PersonId才是实例身份。

ActivitySocialProjection仅表达ActivityType→PersistentRelationalFactType；Rest / Drink / Talk为合法NoFeedback，VisitSick / Help分别为visited_sick_person / helped_person语义事实。Activity表不得写Relationship delta。Social表单独维护FactType、SourceRole / TargetRole、Channel、Polarity、Strength与Calibration；当前Target→Actor Affinity Positive Medium，4/8/12只是Prototype / Calibration / Not Frozen。

当前4人 / 3Place / 5Activity与行为、Desire、Weight等数值都是Prototype，不是正式人物/建筑平衡。日内工作窗口、通勤、活动时长与接待阈值在现有Host可注入配置中，不伪称已经全部进XLSX，也不另建第二套表格编译系统。

[工具操作](../../Tools/RRPG.ConfigCompiler/README.md)说明诊断与构建；当前Runtime接线归[Architecture](../../Docs/Architecture/技术架构总览.md)。改内容前核对工作簿dirty范围；制作人已有RRPG_Events.xlsx增量不因其它域任务自动获准生成/提交。

### VisualAsset Authority 独立化（2026-09-26）

* 通用视觉资源（`VisualAssetId + SourceFile + ImportPreset → Sprite`）的人工 Authority **只有一份**：
  `RRPG_VisualAssets.xlsx / VisualAsset`。它由 `RRPG_EnemyPresentation.xlsx` **无损迁移**而来（6 行业务数据不变，
  `AssetManifest.json` 也保持不变）。
* `RRPG_EnemyPresentation.xlsx` 现在**只**负责「哪只怪用哪个视觉资源」（`MonsterId → VisualAssetId`），
  通过 `ref:VisualAsset` 引用共享表（跨 Workbook 引用直接复用既有跨表校验）。
* 任何工作簿里再出现第二张 `VisualAsset` 表都会被拒绝（`WB006_DUPLICATE_ENTITY_SHEET`，硬失败）。
* 已实证范围仍只有 PNG → Sprite；**不要**把它写成「所有未来 UI / Prefab / Material / Spine 都必须走这张表」。

### `RRPG_Events.xlsx` 说明

* 十张表描述「什么时候、对谁、发生什么」，但 **Event Core 不拥有** Village Simulation、Expedition Progression、
  Encounter、Person Identity、Resource Authority：`StartBattle` 只能**引用** `EncounterPool.PoolId`；
  `VillageResourceDelta` 只允许 `Ingredients` / `Food`；`GrantPersonRest` 只表达「调用正式 Rest Authority」，
  具体 Rest 窗口仍服从当前 Village Rule（事件表不另造第二套时间参数）。
* 一个**启用**的事件必须：≥1 Trigger、恰好 1 条 Presentation、≥1 Option、每个 Option ≥1 Outcome；
  允许 0 Role / 0 Condition / 0 Effect。**未启用**的事件允许先当草稿写一半（已写出的引用仍必须真实存在）。
* 一个事件的正式 Trigger 只能属于一边（Village 或 Expedition）；`ManualDebug` 不算正式触发时机。
  村庄事件只能用 `VillageModal`，远征事件只能用 `JourneyCard`。
* 文本模板 V0.1 只允许 `{RoleId.DisplayName}` 与 `{Context.Key}`；
  不允许 if / 函数调用 / C# / Lua / 任意表达式脚本（写了会直接报错）。
* 当前两条事件（`evt_test_village_temp_draft` / `evt_test_exp_ruined_shrine`）及其权重、冷却值、条件数值、
  效果数值、文案都是 **Test / Not Frozen 测试内容**，不构成正式内容与平衡。
* 插图来源将来是 `Development/SourceAssets/Event/*.png`；**当前没有正式 Event PNG，因此测试事件的插图留空**
  （不允许凭空生成美术图）。
* 字段 / 枚举 / 未冻结项的完整中文说明写在 `RRPG_Events.xlsx` 的 `00_导航` 里；制作人不需要自己翻译英文表头。
* 正式读取入口：`StaticConfigRegistry.TryGetEventConfig / EventConfig / EventConfigDiagnostic`；Resolver / OptionExecutor 经产品 Adapter 消费，EventWorld 保存长期 History / Flags / Pending。

```text
RRPG_Events.xlsx（人工 Authority：10 张事件表）
   ↓ Compiler build（RRPG_一键导表.cmd）
Development/Generated/StaticConfig/Event*.json（Canonical Mirror，DO NOT EDIT）
   ↓ Unity Builder
Assets/Generated/StaticConfig/Databases/EventConfigDatabase.asset（生成根，不进 Git）
   ↓ StaticConfigRegistry.TryGetEventConfig / EventConfig / EventConfigDiagnostic
EventRuntimeBridge / VillageEventFlow / Expedition Event Adapter → 产品 UI / Save
```

## Event Runtime 消费与制作约束

EventResolver 只读 IEventWorldView，经 TriggerType / TriggerKey / ResolutionSeed / EventContext 产生不可变 ResolvedEventInstance；NoCandidateEvent 为正常结果。解析不等于结算，Resolver 不写 History。正式产品已接 Village / Expedition UI、Effect Gateway 和 Save。

Option 点击时重检条件，确定 Outcome → Effect Plan → 整条 Preflight → Commit → History → Instance 完成。Outcome 由 EventInstanceId + OptionId 稳定派生；重开 UI 不 reroll。Preflight 失败零 mutation；Commit 失败不登记完成或 History，同实例成功仅一次。

EventNotSeen / EventSeenCountAtLeast 的 TargetKey 是世界长期历史中的 EventId，Compiler 要求真实引用；SeenCountAtLeast.IntValue >= 1。冷却使用 VillageDays / ExpeditionNodes 序数差，不使用现实时间。外部效果由 IEventEffectGateway 委托正式资源、Rest、远征与战斗 Authority，事件配置不另建 World State。

EventWorld 保存 Flags / World History / 至多一条 Pending Village Event；Completed Instance 与 Expedition Session History 不落档。远征返程 Pending UI、处理后续跑以及分钟时间推进服从 [Village GameSpec](../../GameSpec/Village/Village-GameSpec-V0.1.md) 与 [Save GameSpec](../../GameSpec/Save/Save-GameSpec-V0.1.md)，不沿用旧“额外天数立即结算”解释。

1A～1D 的阶段、测试及制作人验收证据留在 [WP-Event-1 Closeout](../../Docs/DevelopmentSpecs/2026-09-26_RRPG_WP-Event-1-Unified-Event-Content-Foundation-Closeout-Report-V0.1.md)，不作为当前待执行队列。

## Encounter 制作与产品消费

工作簿职责见上方当前清单；MonsterEncounterProfile / MonsterRole 与 Encounter 表通过同一 Compiler 校验。

* `MonsterEncounterProfile`：一怪一行（`ThreatCost` 威胁值 / `Tier` 级别 / `RandomWeight` 基础随机权重）。
* `MonsterRole`：**同一只怪可以有多行**（例如 `Melee` + `Tank`）；身份 = `MonsterId` + `Role`。
* `RRPG_Encounters.xlsx` 说明：
  * **敌人数量的唯一来源是 `EncounterSlot` 的 `CountMin` / `CountMax`**；`Encounter` 表刻意**不**再写敌人数（避免双 Authority）；
  * **`Enabled = true` 的遭遇必须至少有 1 个组合需求位**（`DAT029`，硬失败）；`Enabled = false` 的草稿允许暂时没有 Slot；
  * 同一个遭遇下所有位的 `CountMax` 合计 **不得超过 9**（敌方战场是 3 排 × 3 列 = 9 格）；
  * 站位模式：`Any`（任意排，Row/Column 留空）/ `Row`（指定排，Column 留空）/ `Exact`（精确格，必须 Row + Column 且 `CountMin = CountMax = 1`）；
  * 选怪方式：`FixedMonster`（必须填 `FixedMonsterId`，且必须存在于 Monster 表）/ `Query`（不得填 `FixedMonsterId`）；
  * 筛选维度第一版只有 `Role` / `Tier` / `MonsterId`；**不支持** Species / Allegiance / Biome / Curse / 世界时间 / 世界状态 / Encounter Modifier。
* 当前工作簿里的全部遭遇、位、规则、池、条目、威胁值、权重、深度区间都是 **Test / Not Frozen 测试内容**，不是正式内容与平衡。
* 详细字段 / 枚举 / 未冻结项的说明就写在 `RRPG_Encounters.xlsx` 的 `00_导航` Sheet 里（制作人不需要自己翻译英文表头）。

EncounterGenerator 从 PoolId / Depth / Seed（或 EncounterId / Seed）生成合法 EnemyRoster；正式 Battle / Prep 消费冻结的 Active EnemyRoster，不在开战时重新生成。进入 Battle / Elite / Boss 前先校验并预生成 Encounter，失败时不进入节点、不扣补给、不留半阵容，可重试。

Enabled 遭遇必须存在合法解，长期守卫测试检查内容可解性。Node → EncounterPool 的正式内容映射仍 Not Frozen，当前 pool_test_general / pool_test_deep 是兼容测试内容；不得升级为正式区域平衡。历史预算、浅层覆盖修复与制作人游玩验收见 [Encounter Closeout](../../Docs/DevelopmentSpecs/2026-09-25_RRPG_WP-Encounter-1-Enemy-Encounter-Generation-Foundation-Closeout-Report-V0.1.md)，不是当前待办。

### Authority 链（WP-Encounter-1A 起）

```text
RRPG_Encounters.xlsx（人工 Authority：Encounter / EncounterSlot / EncounterSlotRule / EncounterPool / EncounterPoolEntry）
RRPG_Monsters.xlsx 的 MonsterEncounterProfile / MonsterRole（人工 Authority：威胁值 / 级别 / 定位）
   ↓ Compiler build（RRPG_一键导表.cmd）
Development/Generated/StaticConfig/{Encounter,EncounterSlot,EncounterSlotRule,EncounterPool,EncounterPoolEntry,
                                    MonsterEncounterProfile,MonsterRole}.json（Canonical Mirror，DO NOT EDIT）
   ↓ Unity Builder
Assets/Generated/StaticConfig/Databases/EncounterConfigDatabase.asset（生成根，不进 Git）
   ↓ StaticConfigRegistry.TryGetEncounterConfig / EncounterConfig / EncounterConfigDiagnostic
EncounterGenerator → 冻结 Active EnemyRoster → Battle / Prep
```

## Monster / Enemy Presentation 当前入口与 Legacy 退役

当前工作簿清单为唯一人工入口。VisualAsset 已独立到共享工作簿，不得在 EnemyPresentation 重建第二份。M2～M4 的迁移证据留在日期化 Closeout。

### Authority 链（M2 Slice 2C 起）

```text
RRPG_Monsters.xlsx（人工 Authority）
   ↓ Compiler build（RRPG_一键导表.cmd）
Development/Generated/StaticConfig/Monster.json（Canonical Mirror，进 Git，DO NOT EDIT）
   ↓ Unity Builder
Assets/Generated/StaticConfig/（生成根，不进 Git）
   ↓ StaticConfigRegistry / MonsterRegistry
MonsterDefinition → Battle / Expedition（产品 Runtime 唯一读取路径）
```

### Authority 链（M3 起：Enemy Presentation —— 已接入 Battle / Prep）

```text
RRPG_EnemyPresentation.xlsx（MonsterId → VisualAssetId）+ RRPG_VisualAssets.xlsx（VisualAssetId → SourceFile）
Development/SourceAssets/Enemy/*.png（源图；内容哈希进 Mirror）
   ↓ Compiler build
Development/Generated/StaticConfig/{EnemyPresentation,VisualAsset,AssetManifest}.json（Canonical Mirror，DO NOT EDIT）
   ↓ Unity Builder（生成 managed Sprite）
Assets/Generated/StaticConfig/{Databases/EnemyPresentationDatabase,Databases/VisualAssetCatalog,Assets/Enemy/*.png}
   ↓ StaticConfigRegistry → EnemyPresentationResolver
BattleUI（Prep 敌方格 + Battle 单位卡，同一入口）→ Sprite
```

* `SourceFile` 必须写成相对 `Development/SourceAssets/` 的路径（只支持 `.png`）；`ImportPreset` 目前只支持 `sprite_point_100`
  （取值域写在 Sheet 的 Row 3 `allowed=`，与 Compiler 的预设表同源）。
* **换图 = 必须重新 build**：`AssetManifest.json` 记录源图内容哈希，`validate` 会报 `MIR002_MIRROR_STALE`。
* 当前敌方源图是**临时素材**（每只怪物一张，尺寸/颜色可区分，用于验证「不同 MonsterId → 不同 Sprite」）；
  正式美术到位后只需替换源图 + 重跑一键导表，不需要改代码 / Scene / Prefab。
  详见 `Docs/DevelopmentSpecs/2026-09-23_RRPG_M3-Enemy-Presentation-Authority-Cutover-Closeout-Report-V0.1.md`。

### `Development/Excel/Monster.csv` = **已于 M4 物理删除**

* **M4（Monster Legacy Physical Retirement，FDS §9 Wave 1F / §10.5）已执行**：`Monster.csv`、3 个 Monster SO、
  `MonsterData` 类型、`ExcelImporter` Monster 分支、`DataManager` Monster 路径、`GameDataInitializer` Monster seed 与
  D-M2-1 Legacy 回退适配**全部删除**；Monster 定义只能改 `Development/Config/RRPG_Monsters.xlsx`。
* **CSV → XLSX 同步已关闭**（决策 D-M2-2）：`Tools/RRPG.ConfigCompiler/migration/RRPG.ConfigCompiler.M1ShadowSync`
  的 `sync` 动词**拒绝执行**（非 0 退出：`M1 ShadowSync is frozen after M2 Monster Definition Authority Cutover.`）。
  该工具按 M4 裁决**保留**，供迁移证据只读查询（`equivalence` / `status`；`MIGRATION ONLY / M1 CLOSED`）。
* 产品 Runtime 对 Legacy 的消费 = **0**；`DataManager` / `ExcelImporter` 本体保留（仍服务其它领域）。
* 敌方视觉：Authoring = `RRPG_EnemyPresentation.xlsx` + `Development/SourceAssets/Enemy/*.png` →
  Compiler → Canonical Mirror → Unity Builder → Runtime `MonsterId → EnemyPresentationResolver → Sprite`；
  旧的 `enemyBodyPlaceholder` 字段与 Scene 条目**已于 M4 删除**（fallback 只在 Resolver 内）。

### 制作人操作

```text
RRPG_打开配置表.cmd      打开本目录（改 XLSX）
RRPG_检查配置.cmd        只检查（不生成；Unity 开着也行）
RRPG_一键导表.cmd        检查并生成（需要先关闭 Unity 编辑器）→ Mirror + Unity 生成根
```

> `Development/Excel/Monster.csv` 已于 M4 物理删除；怪物定义请只改本目录的 `RRPG_Monsters.xlsx`。

## 入口

```text
RRPG_打开配置表.cmd      打开本目录
RRPG_检查配置.cmd        只检查（不生成任何东西；Unity 开着也行）
RRPG_一键导表.cmd        检查并生成（需要先关闭 Unity 编辑器）
```
