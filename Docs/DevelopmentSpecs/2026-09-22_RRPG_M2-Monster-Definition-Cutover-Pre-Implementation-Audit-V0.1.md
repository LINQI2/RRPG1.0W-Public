# RRPG M2 — Monster Definition Authority Cutover：Pre-Implementation Audit V0.1

> 日期：2026-09-22
> 性质：**FORMAL DEVELOPMENT AUDIT / M2 ONLY / DOCUMENT ONLY**
> 范围：**只记录事实与实施契约**；**不修改任何代码、不修改任何资产、不执行 Authority Cutover**
> Authority（架构）：**D-019**（`Docs/决策记录.md`｜RRPG Static Configuration Production Architecture）
> 上游契约：`Docs/DevelopmentSpecs/2026-09-22_RRPG_Static-Configuration-Migration-FDS-V0.1.md`（以下简称 FDS；§9 Wave 1B/1C、§10.2、§10.4、§10.5、§10.6、§13、§14、§17）
> 本文件**不是**新的 Architecture Decision；它是 M2 实施前的**事实审计 + 实施契约**。

## 本轮声明（Task Boundary）

| 项 | 状态 |
|---|---|
| 本任务动作 | **仅新增本文件** |
| C# 代码 | **未修改** |
| Unity Asset / Scene / Prefab / ScriptableObject | **未修改** |
| `Development/Excel/Monster.csv` | **未修改** |
| `Development/Config/RRPG_Monsters.xlsx` | **未修改** |
| Runtime 行为 | **未改变** |
| Authority Cutover | **未开始**（M2 尚未开始） |
| Commit / Push | **未执行** |

---

# 1. Audit Baseline

## 1.1 版本基线（只读实测）

| 项 | 值 |
|---|---|
| Branch | `migration/static-config-m1` |
| Commit | `f7c2e89123115e8da73a9cd3c9a2be459c83f8dc`（`feat(config): establish monster shadow equivalence`） |
| M0 基线 | `bd2204fe97c4be966c58281a6a569e5e3656e0e3`（`feat(config): establish production static config toolchain foundation`，branch `migration/static-config-m0`） |
| 上一阶段状态 | **M1 Monster Shadow + Equivalence：已完成** |
| 当前阶段状态 | **M2 Monster Definition Authority Cutover：NOT STARTED** |
| 工作区 | 干净（本审计为只读；审计过程中的临时检索文件已清理） |

## 1.2 M1 已冻结事实（M2 的输入基线）

1. **等价性已证明**：`Monster.csv` ⇔ `RRPG_Monsters.xlsx` ⇔ `Development/Generated/StaticConfig/Monster.json` ⇔ `MonsterDefinition`，
   3 monsters × 8 fields **逐条逐字段一致**。
2. **Mirror 语义指纹**：`Monster.json` 头部 `_generated.sourceSha256 = 339f188afa831a7d1ec43e1bae0cc4b69a4fca167c994b05b93d3c6b56624842`。
3. **枚举顺序已证明**：`MonsterRegistry.All` 顺序 = Canonical key 排序，且与 `DataManager.AllMonsters` **逐项一致**（M1 `E2` 用例）；
   `BattleSystem.InitializeBattle` 按 list index 部署到 `BattleColumn 0/1/2`（`BattleSystem.cs:73-75`）→ **枚举顺序是当前可观察产品行为**。
4. **Shadow 链已可构建**：`XLSX → Compiler build → Canonical Mirror → Unity Builder → Assets/Generated/StaticConfig/{Databases,Resources}`；
   生成根含 `Resources/RRPGStaticConfigRoot.asset`（该目录**不进 Git**，`.gitignore:103`）。
5. **M1 边界**：Monitor 侧 Registry 为 Shadow，**产品 Gameplay 不消费**；`Monster.csv` 仍是人工 Authoring Authority。

## 1.3 M2 输入事实（本轮新增只读核对）

| # | 事实 | 证据 |
|---|---|---|
| B1 | 产品场景为 `Assets/Scenes/SampleScene.scene`（团结引擎 `.scene` 扩展名，不是 `.unity`） | 目录实测 |
| B2 | `BattleController`（承载 `BattleUI` 的 GameObject）在场景中 **`m_IsActive: 0`** | `SampleScene.scene:9496/9501` |
| B3 | `ExpeditionUI` 在 `InitFromExpedition`（`:610`）**之后**才 `battleUI.gameObject.SetActive(true)`（`:622`）→ 产品链下 `BattleUI.Start()` 走 `_initializedFromExpedition == true` 提前返回（`BattleUI.cs:247-250`） | `ExpeditionUI.cs:610/622`、`BattleUI.cs:239-253` |
| B4 | 产品场景对 `mon_` **0 命中** → Scene / Prefab 未持有 per-Monster SO 引用（不触发 FDS H5 / H12） | `SampleScene.scene` 全文检索 |
| B5 | `DataManager.GetMonsterById` 全仓 **0 调用者**（仅 `DataManager.cs:233` 定义 + 文档提及）→ 今天唯一真实 Runtime 路径是 `AllMonsters` 列表 | 全仓 `findstr` |
| B6 | `Assets/Scripts/Save/**` 对 `Monster` **0 命中**（目录仅 `SaveData.cs` / `SaveSystem.cs`） | 全仓 `findstr` |
| B7 | `StaticConfig.Runtime.asmdef` 仅引用 `UnityEngine`，且 `autoReferenced: true` → Assembly-CSharp（Battle / UI / Adventure）可直接 `using RRPG.StaticConfig`，**无需改 asmdef** | `StaticConfig.Runtime.asmdef:1-16` |
| B8 | PlayMode 测试 asmdef **均未引用** `StaticConfig.Runtime`（已核 `BattleStage1.PlayModeTests.asmdef`、`ExpeditionV4F.PlayModeTests.asmdef`）；EditMode `StaticConfig.Tests.asmdef` 已引用（含 `StaticConfig.Editor`） | asmdef 实测 |
| B9 | 本机 Tuanjie 编辑器可用：`D:\BBB\Tuanjie\Hub\Editor\2022.3.62t9\Editor\Tuanjie.exe`；Unity 未开（无 `Temp/UnityLockfile`）→ EditMode / PlayMode batchmode 可执行 | 环境实测 |
| B10 | FDS 中的旧命名 `BattleUI.SetupDemoData` 已不存在；对应产品代码现名为 `BattleUI.InitializeDefaultBattleData`（`BattleUI.cs:1989-2058`），入口为 `RunBattle()`（`:371-373`） | 全仓 `findstr` |

---

# 2. Migration Objective

## 2.1 当前链（Cutover 前）

```text
Development/Excel/Monster.csv            ← 当前人工 Authoring Authority（唯一）
        ↓ ExcelImporter.ImportMonsters（Editor 菜单 / batchmode）
Assets/Resources/ScriptableObjects/Monsters/*.asset（3 个 MonsterData SO）
        ↓ DataManager.LoadAll()（Resources.LoadAll<MonsterData>，惰性单例，无显式 boot）
        ↓ DataManager.AllMonsters（List<MonsterData>）
ExpeditionUI → AdventureManager → BattleUI → BattleSystem → BattleUnit
```

## 2.2 目标链（M2 之后）

```text
Development/Config/RRPG_Monsters.xlsx     ← Current Monster Definition Authoring Authority（M2 起）
        ↓ RRPG.ConfigCompiler（build）
Development/Generated/StaticConfig/Monster.json（Canonical Mirror，进 Git，DO NOT EDIT）
        ↓ Unity Builder（StaticConfigBuilder.ExecuteBuild）
Assets/Generated/StaticConfig/（生成 Root + MonsterConfigDatabase，不进 Git）
        ↓ StaticConfigRegistry（lazy-or-boot，幂等，明确诊断；无 Editor 依赖）
        ↓ MonsterRegistry（纯 C#；All 稳定顺序 / TryGet / Get + Diagnostic）
        ↓ MonsterDefinition（8 字段纯静态数据）
ExpeditionUI → AdventureManager → BattleUI → BattleSystem → BattleUnit
```

## 2.3 必须保持不变量（Hard Invariants）

| # | 不变量 | 判定方式 |
|---|---|---|
| I1 | **`MonsterId` 身份语义不变**：`BattleUnit.SourceId = MonsterId`、`UnitId` 语义不变 | `BattleSystem.cs:521-522`；回归用例 |
| I2 | **8 字段语义不变**：`MonsterId / Name / MaxHP / Attack / AttackRange / AttackCount / ActionPoints / Faction` | 3×8 等价比较（M1 `E1` 沿用） |
| I3 | **Battle 行为不变**：敌方单位数、list index → `BattleColumn`、HP/ATK/Range/Count/AP/Faction 的消费语义 | M1 `E3`；Battle 回归 |
| I4 | **Save 不变**：Save 域零涉及（`Monster` 0 命中），`SaveVersion` 不变 | FDS §0.5 / §12；回归用例 |
| I5 | **Gameplay 规则不变**：不新增/删除规则、不改伤害与 AI 判定 | §17 H10 判定 |
| I6 | **无 dual read**：产品 Runtime 不存在 `if (useNewConfig) … else …` 式双读取路径 | §10.2-4；源码断言 |
| I7 | **无 Editor 依赖**：Runtime 装配不得引用 `UnityEditor` / `AssetDatabase` | §13.3 R5；asmdef + 源码断言 |

---

# 3. Consumer Map

> 口径：全部条目来自当前工作区**只读实测**（全仓 `findstr /s` + 逐文件阅读），不凭记忆。

## 3.1 产品 Runtime 消费者（Assembly-CSharp）

| # | 文件 | 方法 / 位置 | 当前读取内容 | M2 修改方向 | 风险 |
|---|---|---|---|---|---|
| C1 | `Assets/Scripts/UI/ExpeditionUI.cs` | `OnConfirm()`（`ConfirmExpedition` 成功分支）`:493-503` | `DataManager.Instance.AllMonsters`（整表 `List<MonsterData>`） | 改读 `StaticConfigRegistry.Monsters.All`（`IReadOnlyList<MonsterDefinition>`） | 产品链**首次消费点**（FDS §0.3-2）；Registry 缺失时的行为由 **D-M2-1** 定义 |
| C2 | `Assets/Scripts/Adventure/AdventureManager.cs` | ctor `:62-66`、字段 `:56`、赋值 `:73`、`GetEnemyMonsters() :446` | 仅**持有 + 透传**，不读字段 | 类型改 `IReadOnlyList<MonsterDefinition>`；**不保留旧重载** | 公共构造签名 + 全仓反射测试（§3.4）；FDS §9 Wave1C 明令禁止永久双路径 |
| C3 | `Assets/Scripts/UI/ExpeditionUI.cs` | `OnEnterNode`（Battle / Elite / Boss 分支）`:603-610` | `adventure.GetEnemyMonsters()` → `battleUI.InitFromExpedition(...)` | 随 C2 返回类型变化，**代码本身不变** | PlayMode 反射实参类型（§3.4） |
| C4 | `Assets/Scripts/UI/BattleUI.cs` | `InitFromExpedition()` `:108-128`、字段 `:93`、`ShowPrepPhase()` `:405-426` | `MonsterId`（卡名 `EnemyPrep_`）、`Name`（Text_Name）、`MaxHP`（`HP/HP`）、`Attack`（Text_ATK） | 切 `MonsterDefinition`：**字段同名同义 → 逻辑零改动** | 内部若声明为 `IReadOnlyList<>` 会破坏 C6/C7 的 `.Find` → 见 §3.3 |
| C5 | `Assets/Scripts/Battle/BattleSystem.cs` | `InitializeBattle()` `:48-50`、敌方单位生成 `:67-76`、`CreateMonsterUnit()` `:518-531` | **8 字段全读** → `BattleUnit{UnitId, SourceId=MonsterId, UnitType=Monster, MaxHP, Attack, AttackCount, AttackRange, MaxActionPoints, Faction}` | 形参改 `MonsterDefinition`，逐字段映射**不变** | **Cutover 核心点**；`list index → BattleColumn` 顺序语义不得变（`:73-75`） |
| C6 | `Assets/Scripts/UI/BattleUI.cs` | 战斗中敌方卡生成 `:1646-1647` | `_enemyMonsters.Find(m => m.MonsterId == u.SourceId)` → `Name` | 保持**同一线性查找**实现（等价、零抽象增量） | 与 C7 必须同源，避免形成两套敌方名字链 |
| C7 | `Assets/Scripts/UI/BattleUIDisplayNames.cs` | `GetBattleUnitDisplayName()` `:20-21` | 同 C6（partial class 共享 `_enemyMonsters`） | 同 C6；缺失时 fallback 仍为 `unit.SourceId` | 与 C6 语义一致性 |
| C8 | `Assets/Scripts/UI/BattleUI.cs` | `InitializeDefaultBattleData()` `:1989-2058`（含 `:2004` DataManager 读取与 `:2043-2047` 内联）+ 辅助 `CreateMonsterData()` `:1981-1987` | 内联 `mon_001/mon_002` 全 8 字段；或 `DataManager.Instance.AllMonsters` | **删除内联定义**，改读 `StaticConfigRegistry.Monsters.All`（按固定 `MonsterId` 语义不变） | FDS **H14**；由 B2/B3 实测：**产品链不触发**（`BattleController` 场景内 inactive，且 `InitFromExpedition` 先置位）。仍是「隐藏静态源」，M2 必须消失 |
| C9 | `Assets/Scripts/Battle/BattleSystem.cs:574`；`TurnSystem.cs:92`；`DeathDoorSystem.cs:76` | 规则判定 | 仅 `UnitType == UnitType.Monster` | **不迁移**（非静态配置消费） | 无 |

## 3.2 产品 Runtime 非消费者（明确不迁）

| 项 | 位置 | 说明 |
|---|---|---|
| Enemy Intent / 敌方 AI 行为 | `BattleSystem` Enemy 阶段（`LockEnemyIntents` / `ExecuteTurnPart2`）；`Battle/Stage1/EnemyBehaviorDemoConfig.cs` | 运行时规则与既定演示 AI，**非** Monster 静态字段；M2 不处理（EnemyBehavior 属独立领域） |
| 敌方视觉 | `Assets/Scripts/UI/BattleUIUnitVisualHosts.cs:36` `enemyBodyPlaceholder`（场景 guid `c51b8b…`） | 单一占位，**不是** Monster 字段；迁移属 **M3**（EnemyPresentation Authority） |
| 奖励 / 掉落 | `RunState` Pending Rewards | 非 Monster 静态字段 |
| 敌方技能 | `BattleUI.cs:1521` 传 `new List<SkillData>()` | 当前无 Monster 技能字段 |

## 3.3 BattleUI 内部类型的实施约定（约束，不是决策变更）

- `BattleUI._enemyMonsters` 在 `InitFromExpedition` 时**物化为 `List<MonsterDefinition>`**（由 `IReadOnlyList<MonsterDefinition>` 得到）。
- 理由：C4 使用 `.Count` / 索引器（`:405-407`），C6 使用 `.Find`（`:1646`），C7 使用 `?.Find`（`:20`）；物化后**全部调用点零改动**，且不引入新的查找抽象或第二套名字链。
- 该物化**不是** dual read：数据来源仍是 Registry 的同一批 `MonsterDefinition` 实例。

## 3.4 测试面（M2 的机械改造量，必须一次性完成）

| # | 组 | 位置 | 内容 | M2 动作 |
|---|---|---|---|---|
| T1 | M1 Shadow Gate 用例 | `Assets/Tests/EditMode/StaticConfig/MonsterShadowTests.cs`（`E1 :116-147`、`E2 :151-170`、`E3 :229-255`、`H :346-396`） | E1 逐字段等价 / E2 枚举顺序 / E3 按序部署 / H 隐藏源冻结断言 | **E1/E2/E3 保留**为 M2 Gate 证据（Legacy SO 仍在磁盘）；**H 用例必须翻转**：`:374-378` 现断言 `BattleUI` 必须含 `CreateMonsterData("mon_001"` → M2 后必须断言**不存在** inline Monster 定义 |
| T2 | EditMode 反射构造 `List<MonsterData>` 传入产品 API | `BattleDualBoardSpatialAuthorityTests.cs:594`、`BattleFormationIntegrityTests.cs:179/651`、`BattleLegacyProductPathCleanupTests.cs:401`、`BattleStage3ASpaceTests.cs:292/307/383`、`ExpeditionV4F/V4FTestKit.cs:159`、`ExpeditionV4F/WP2ExpeditionProfessionLoopTests.cs:92`、`PersonIdM1/FoundationIdentityCleanupTests.cs:103`、`ProfessionM5/ProfessionRunReturnCommitTests.cs:53`、`GrowthRunIntegrationTests.cs:261/271` | 元素类型 `MonsterData` | 元素类型换 `MonsterDefinition`（helper 用 `Type.GetType("RRPG.StaticConfig.MonsterDefinition, StaticConfig.Runtime")`）｜**约 10 文件** |
| T3 | PlayMode 反射取 `GetProperty(dataManager,"AllMonsters")` 作为 `InitFromExpedition` / `AdventureManager` 实参 | `BattleMilestoneAPlayTests`（8 处）、`BattleDualBoardProductPlayTests.cs:158`、`BattleFormationRosterConsistencyPlayTests.cs:145`、`BattleGate0RulePipelinePlayTests.cs:330`、`BattleStage5IntentPlayTests.cs:132`、`BattleStage6ARunStatePlayTests.cs:86/243/365`、`BattleStage6BRewardLoopPlayTests.cs:167/176`、`BattleVisualFoundationPlayTests.cs:139`、`CharacterGrowthPlayTests.cs:248`、`PersonVisualBindingPlayTests.cs:354`、`ExpeditionV4F/{ExpeditionReturnIntegrityProductPlayTests:148/159, ExpeditionStage3ReturnContractPlayTests:88/99, ExpeditionV4FReturnProductPlayTests:114/125, VillageResourceCarryE2EPlayTests:174/185}`、`VillageV4/VillageV4FullProductChainPlayTests.cs:199/210` | 整表 | 改为从 StaticConfig 取（**B8**：PlayMode asmdef 未引用 `StaticConfig.Runtime` → 反射或补 asmdef reference）｜**约 15 文件 / 约 30 处** |

## 3.5 Save（唯一结论：零涉及）

| 项 | 事实 | 证据 |
|---|---|---|
| `Assets/Scripts/Save/**` | 对 `Monster` **0 命中**；目录仅 `SaveData.cs` / `SaveSystem.cs` | B6 |
| 敌方编成 | 只存在于会话 Runtime（`AdventureManager._enemyMonsters` / `RunState`），**不进入 Save** | FDS §0.5 / §12 |
| 结论 | **M2 不触碰 Save**；不触发 FDS §17 **H2** | — |

---

# 4. Legacy Source Classification

> 分类口径：**Runtime Legacy**（被产品 Runtime 读取）｜**Authoring Source**（可产生静态内容）｜**Demo / Fallback Source**（非产品链的演示或回退静态内容）。

| # | 来源 | 类型 | 位置 | 当前角色 | M2 处置 | M4 处置 |
|---|---|---|---|---|---|---|
| L1 | **`MonsterData`（SO 内容资产）** | Runtime Legacy（内容载体） | `Assets/Resources/ScriptableObjects/Monsters/{mon_001_史莱姆, mon_002_骷髅弓手, mon_003_wolf_野狼}.asset`；类型 `Assets/ScriptableObjects/Monsters/MonsterData.cs`（8 字段） | 被 `DataManager` 加载，经 `AllMonsters` 进入战斗 | **不删除、不修改**；Cutover 后**不再被产品 Runtime 消费**；作为等价比较的 Legacy 基准保留 | 物理删除（FDS §10.5 步骤 2 + 步骤 7：类型最后删） |
| L2 | **`DataManager.AllMonsters`（读取入口）** | Runtime Legacy（读取入口） | `Assets/Scripts/Core/DataManager.cs:33 / 63 / 83 / 96 / 105 / 233-237` | 唯一真实 Runtime 读取入口（`Resources.LoadAll<MonsterData>` + `_monsterMap`） | **逻辑退休**：保留代码但**所有产品消费者停止调用**（`AllMonsters` / `GetMonsterById`）；不删类型、不删字段 | 删除 Monster `LoadAll` / 缓存（FDS §10.5 步骤 4） |
| L3 | **`ExcelImporter.ImportMonsters`（Authoring 写入口）** | Authoring Source（Legacy） | `Assets/Editor/ExcelImporter.cs:463-488`；入口 `ImportAll()` `:25-47`（`:38` 调用） | `Monster.csv` → 写 per-entry Monster SO | **FDS §10.4-A：`ImportAll()` 停止调用 `ImportMonsters`**（方法体留至 M4；不得影响其它领域导入） | 删除 Monster importer 代码（§10.5 步骤 3） |
| L4 | **`GameDataInitializer` Monster seed** | Authoring Source（Legacy，Editor-only） | `Assets/Editor/GameDataInitializer.cs:64-76`（`mon_001` / `mon_002` 内联；菜单 `Tools/初始化游戏数据`） | 第二条人工 Authoring 路径（与 CSV 集合 Drift：缺 `mon_003_wolf`） | **FDS §10.4-B：禁用 Monster 分支**（保留代码、留 M4 注释标注） | 删除 Monster seed 残余（§10.5 步骤 5） |
| L5 | **`BattleUI` inline Monster（Demo / Fallback）** | Demo / Fallback Source | `Assets/Scripts/UI/BattleUI.cs:1981-1987`（`CreateMonsterData`）+ `:2043-2047`（内联 `mon_001` / `mon_002`）；`.scene` 中 `BattleController` `m_IsActive: 0`（B2/B3） | 仅在「未经过远征初始化即启动 BattleUI」时生效（开发/调试/局部测试），**产品链不触发** | **FDS §10.4-C：内联 Monster static definition 必须消失**；Demo 若仍需 → **改读 `StaticConfigRegistry.Monsters`**（按固定 `MonsterId`）；若仅测试需要 → 迁 Test Fixture | 删除无用 Demo legacy 残余（§10.5 步骤 6） |
| L6 | **`Monster.csv`（Frozen Legacy）** | Authoring Source（冻结点） | `Development/Excel/Monster.csv` | M2 起**不再是 Authority** | **不修改文件内容**；仅在 README 标注 **Frozen Legacy**（不得加 CSV 注释行：第 1 行是表头，`ExcelImporter.cs:466-468` 直接读取） | 删除 `Monster.csv`（§10.5 步骤 1） |
| L7 | **M1ShadowSync（CSV → XLSX 同步器）** | Migration Tool（M1-only） | `Tools/RRPG.ConfigCompiler/migration/RRPG.ConfigCompiler.M1ShadowSync` | M1 用于确定性生成 Shadow XLSX | **D-M2-2：Authority Cutover 后冻结**（拒绝执行 / M1-only 硬开关） | 随 M1 迁移期结束退役（不属于 M4 Legacy 清理清单，按 D-M2-2 单独处置） |

## 4.1 M2 / M4 边界（不得混淆）

| 维度 | **M2（本审计范围）** | **M4（不在本范围）** |
|---|---|---|
| 性质 | **逻辑 / Authority 退休** | **物理清理** |
| 代码 | 消费者切 Registry；旧写入口冻结（保留代码） | 删除 `MonsterData` 类型 / `DataManager` Monster 加载 / importer / seed / demo 残余 |
| 资产 | **不动**任何 SO / CSV / XLSX 内容 | 删 `Monster.csv` + 3 个 Monster SO（含 `.meta`） |
| Authority | `RRPG_Monsters.xlsx` 成为 Monster Definition Authority；`Monster.csv` 标 Frozen | 无 Authority 变化 |
| 判定 | 见 §7 Slice 4 的 Gate | `MonsterData` 引用 = 0（Runtime / Editor / Tests）且资产 = 0 且 compile / tests PASS |

**M2 明确禁止**（与 FDS §18 / 用户任务一致）：删除 `MonsterData` / `DataManager` / `ExcelImporter` / `GameDataInitializer`；修改 Gameplay 规则；修改 Save；迁移 EnemyPresentation（M3）；处理 EnemyBehavior；做 Legacy 物理退休。

---

# 5. Authority Cutover Decision

## 5.1 M1（已完成）：Shadow，仅用于验证

```text
Development/Excel/Monster.csv        = Current Monster Definition Authoring Authority
        ↓（M1 Shadow Sync，MIGRATION ONLY）
Development/Config/RRPG_Monsters.xlsx = Shadow Candidate（不得人工当作内容变更来源）
        ↓ Compiler build / Builder
Mirror + Generated Root + MonsterDefinition + MonsterRegistry = Shadow only（产品 Gameplay 不消费）
```

- Shadow 阶段的 XLSX 用途**仅限**：Compiler 验证 / Equivalence / Builder / Registry Shadow 测试（FDS §9 Wave 1A「Shadow Authority 收紧」）。
- 篡改 Shadow XLSX 不产生产品内容；`sync` 会覆盖（FDS §17 **H15** 风险）。

## 5.2 M2 之后：XLSX 成为 Authority

| 项 | Cutover 前 | **Cutover 后（M2 完成时）** |
|---|---|---|
| Monster Definition Authority | `Development/Excel/Monster.csv` | **`Development/Config/RRPG_Monsters.xlsx`** |
| `Monster.csv` | 可写的人工源 | **Frozen Legacy**（只读参考；不得再作为内容变更来源） |
| XLSX | Shadow Candidate | **Current Authoring Authority**（制作人改内容只改这里） |
| 产品 Runtime 数据来源 | `MonsterData` SO（经 `DataManager`） | `MonsterDefinition`（经 `StaticConfigRegistry` → `MonsterRegistry`） |
| Legacy 写入口 | CSV importer / Editor seed / demo inline | **已技术性冻结**（FDS §10.4 A/B/C 全部满足） |

## 5.3 Cutover Gate（FDS §10.2，全部满足才可 Cutover）

| # | 条件 | 本审计中的对应证据/计划 |
|---|---|---|
| G1 | 8 字段 100% equivalence（3×8，对 Legacy 磁盘 SO 实测值） | M1 `E1`（保留）；§7 Slice 4 |
| G2 | New Registry ready（Player 可用路径；无 Editor 依赖） | §7 Slice 1（`StaticConfigRegistry.Monsters`）；B7 / I7 |
| G3 | 所有真实 Monster Stats Consumer 已切换（C1–C5），名字链（C6/C7）亦切 | §3.1 / §7 Slice 2 |
| G4 | Runtime 无 dual read（无 `if (useNewConfig) … else …` 产品双路径） | §7 Slice 4 源码断言；例外仅 D-M2-1 的**失败路径**（非开关式双读） |
| G5 | tests / product gate 满足 M2 范围（全量 EditMode / PlayMode / compile + 真实产品链） | §7 Slice 4 |
| G6 | §10.4 三项旧 Authoring 写入口已技术性禁用 | §7 Slice 3（L3 / L4 / L5） |

---

# 6. Decision Log

> 本节记录 **M2 实施前已裁决** 的两项决策。它们**约束 M2 实施**，但**不改动 D-019 架构**，也不改变 FDS 的 Migration 契约。

## 6.1 D-M2-1 — Generated Root Missing Strategy

**Decision：Disposable Fail-safe Adapter（一次性失败保护适配）**

**背景（问题的真实来源）**

- 生成根 `Assets/Generated/StaticConfig/**` **不进 Git**（`.gitignore:103`；FDS §8.2 决策）→ fresh clone / 清理生成物后，`Resources.Load<RRPGStaticConfigRoot>(RRPGStaticConfigRoot.ResourcesPath)` 可能为 `null`（`RRPGStaticConfigRoot.cs:63-66`、`StaticConfigRegistry.cs:36-47`）。
- Cutover 后产品 Monster Authority = Registry；若 Registry 缺失而**静默**处理，则敌方单位数为 0（`BattleSystem.cs:67-76` 空列表），属**产品级静默退化**。
- FDS §9 Wave1C 允许「极窄 Compatibility Adapter」，条件为：① 单向 new → compatibility；② 明确标注 Disposable；③ 写明删除条件；④ 不得成为第二 Runtime Authority。

**决策内容**

```text
Runtime 取敌方定义的唯一决策顺序：

1) StaticConfigRegistry.TryGetMonsters(out registry, out diagnostic)
       → 命中：使用 registry（MonsterDefinition / MonsterRegistry = Authority）
2) 仅当 Registry / Root 缺失（失败路径）：
       → Disposable Fail-safe Adapter 只读回退 DataManager.AllMonsters
       → 同时 WarnOnce（明确诊断：Root 缺失 + 请先运行 RRPG_一键导表.cmd）
3) Adapter 永不反向写入、永不成为 Authority；一旦 Registry 可用，Adapter 不参与任何路径
```

**约束（实现时必须同时满足）**

| # | 约束 |
|---|---|
| A1 | **Registry 优先**：任何情况下先尝试 Registry；不得以「开关」形式做新旧并行读取 |
| A2 | **缺 Root 才 fallback**：仅 Registry / Root 不可用（缺失、加载失败）时进入回退分支 |
| A3 | **WarnOnce**：回退必须产生**一次**明确、可操作的诊断（含修复指引 `RRPG_一键导表.cmd`）；不得静默 |
| A4 | **单向**：`Registry → Adapter → DataManager`（只读）；Adapter 不得写入任何静态源，也不得缓存为新 Authority |
| A5 | **临时**：代码必须显式标注 `M2 DISPOSABLE FAIL-SAFE ADAPTER`（含删除条件注释） |
| A6 | **M4 删除**：M4 物理清理时 Adapter 与 `DataManager.AllMonsters` 一并删除；删除条件 = 生成链在 Fresh Rebuild 下稳定可用（FDS §14）+ `MonsterData` 引用归零 |

**允许 / 不允许（边界写清，避免被误判为 dual read）**

| 允许 | 不允许 |
|---|---|
| Registry 缺失时的**失败路径**回退（带 WarnOnce） | 用配置开关 / 环境变量在产品链上**选择**新旧数据源 |
| Adapter 返回**同一 8 字段语义**的只读定义列表 | Adapter 合并 / 覆盖 / 修补两边数据（例如「以 CSV 为准、XLSX 补缺」） |
| 回退发生时产品仍可运行（fresh clone 未导表场景可玩） | 回退成为默认路径或长期路径（必须 Warn，且 M4 删除） |

## 6.2 D-M2-2 — M1ShadowSync Lifecycle

**Decision：Frozen after Authority Cutover（Authority Cutover 后冻结）**

**原因**

1. M1ShadowSync 的同步方向是 **`Monster.csv` → `RRPG_Monsters.xlsx`（覆写）**；Cutover 后 XLSX 是 Authority，再让它运行等于**用 Frozen Legacy 覆盖新 Authority**。
2. 保持「CSV 与 XLSX 都能改」= **双人工 Authority**（FDS §17 **H7**）与「Shadow XLSX 被当第二 Authority」（**H15**）风险。
3. FDS §10.6：从 Cutover 那一刻起 `Monster.csv` 必须被视为 **Frozen**；任何仍能把它写入正式内容源的路径都必须关闭。

**冻结内容**

| # | 要求 |
|---|---|
| F1 | After Cutover：M1ShadowSync **不得**再对 `Development/Config/RRPG_Monsters.xlsx` 执行写操作 |
| F2 | 实现方式（实施时择一，需在 Slice 3 记录）：拒绝执行并输出明确原因（推荐）／改为 M1-only 显式开关（如 `--m1-shadow-only`）且默认拒绝 |
| F3 | 该工具的 README / 注释必须标注 **MIGRATION ONLY / FROZEN AFTER M2 CUTOVER** |
| F4 | 与 M2 其它冻结项（L3 / L4 / L5）在同一 Slice 内完成，避免出现「部分冻结窗口」 |
| F5 | 不删除该工具（删除属 M1 迁移期退役，不在 M2 范围） |

---

# 7. M2 Implementation Plan

> 每个 Slice 独立可 Review；**顺序不可颠倒**（Slice 1 是 Slice 2 的前置；Slice 3 必须在 Slice 2 之后，以避免出现「产品已切新源但旧写入口仍可覆盖」的窗口）。

## Slice 1 — Runtime Registry Entry

**目标**：让 `MonsterDefinition / MonsterRegistry` 在 Runtime 侧**可被产品取得**，且**产品行为零变化**（本 Slice 结束时没有任何消费者切换）。

| # | 内容 | 位置 |
|---|---|---|
| 1.1 | 新增领域访问器：`StaticConfigRegistry.TryGetMonsters(out MonsterRegistry registry, out string diagnostic)` | `Assets/Scripts/StaticConfig/StaticConfigRegistry.cs` |
| 1.2 | 新增便捷只读属性 `StaticConfigRegistry.Monsters`（惰性 + 幂等；失败返回 `null` 并把**明确诊断**写入 `Diagnostic`，不静默） | 同上 |
| 1.3 | 复用既有：`RRPGStaticConfigRoot.Load()`（exact path）→ `MonsterConfigDatabase.CreateRegistry()`（**不新建第二套 Registry**） | `RRPGStaticConfigRoot.cs:63-66`、`MonsterConfigDatabase.cs:42-45` |
| 1.4 | D-M2-1 的 Disposable Fail-safe Adapter 在 Registry 失败路径落地（**只读** + WarnOnce + `M2 DISPOSABLE` 标注 + 删除条件） | 实施时定位 |
| 1.5 | 断言无 Editor 依赖（Runtime asmdef 仅引用 UnityEngine；不引用 `AssetDatabase` / `UnityEditor`） | `StaticConfig.Runtime.asmdef`（B7 / I7） |

**验收**：EditMode 新增用例——幂等初始化、Root 缺失的明确诊断、Registry 命中 3 条且与 Canonical 顺序一致；**产品行为 0 变化**（无消费者切换）。

## Slice 2 — Consumer Cutover

**目标**：把产品 Runtime 的 Monster Authority 从 `MonsterData / DataManager` 切到 `MonsterDefinition / MonsterRegistry`；**行为不变**。

| # | 内容 | 位置 |
|---|---|---|
| 2.1 | C5 `BattleSystem.InitializeBattle(...)` 形参 + `CreateMonsterUnit(MonsterDefinition)`：逐字段映射保持 | `BattleSystem.cs:48-50 / 67-76 / 518-531` |
| 2.2 | C2 `AdventureManager` ctor / 字段 / `GetEnemyMonsters()` → `IReadOnlyList<MonsterDefinition>`；**不保留旧重载** | `AdventureManager.cs:56 / 62-73 / 446` |
| 2.3 | C4 `BattleUI` 字段与形参切类型；按 §3.3 在 `InitFromExpedition` 物化 `List<MonsterDefinition>` | `BattleUI.cs:93 / 108-128 / 405-426` |
| 2.4 | C6 / C7 敌方名字链保持同一线性查找实现（物化后调用点零改动） | `BattleUI.cs:1646-1647`、`BattleUIDisplayNames.cs:20-21` |
| 2.5 | C1 `ExpeditionUI` 改读 `StaticConfigRegistry.Monsters.All`（含 D-M2-1 失败路径） | `ExpeditionUI.cs:493-503` |
| 2.6 | C3 `ExpeditionUI` 战斗入口透传（随 C2 类型变化） | `ExpeditionUI.cs:603-610` |
| 2.7 | C8 `BattleUI.InitializeDefaultBattleData` 删除 inline Monster → 改读 Registry；删除 `CreateMonsterData` helper | `BattleUI.cs:1981-1987 / 1989-2058` |

**验收**：`Assets/Scripts/**`（Runtime）对 `DataManager.Instance.AllMonsters` / `MonsterData` **0 命中**；3×8 等价用例 PASS；Battle 部署顺序用例 PASS；Save 用例 PASS。

## Slice 3 — Legacy Freeze

**目标**：满足 FDS §10.4 A/B/C + §10.6，关闭所有旧静态内容写入口。

| # | 内容 | 位置 |
|---|---|---|
| 3.1 | L3：`ImportAll()` 停止调用 `ImportMonsters`（其它领域导入不受影响；留 `M2 FROZEN LEGACY` 注释） | `ExcelImporter.cs:25-47`（`:38`）、`:463-488` |
| 3.2 | L4：禁用 `GameDataInitializer` Monster 分支（留注释；物理删除留 M4） | `GameDataInitializer.cs:64-76` |
| 3.3 | L5：inline Monster 消失（与 Slice 2.7 同一改动，此处复核并登记） | `BattleUI.cs:2043-2047` |
| 3.4 | L7：按 D-M2-2 冻结 M1ShadowSync（拒绝执行 / M1-only 开关 + README 标注） | `Tools/RRPG.ConfigCompiler/migration/RRPG.ConfigCompiler.M1ShadowSync` |
| 3.5 | L6 + Authority 标注（**文档层，不改内容**）：`Monster.csv` = Frozen Legacy；`RRPG_Monsters.xlsx` = Current Monster Definition Authority | `Development/Config/README.md`（及必要的 `Development/Excel/` 说明处）；**不得**改 CSV / XLSX 内容 |

**验收**：源码断言——`ImportAll` 不再引用 `ImportMonsters`；`GameDataInitializer` 不再创建 Monster SO；`BattleUI` 不再含 inline Monster 定义；M1ShadowSync 拒绝写入 XLSX。**`MonsterData` / `DataManager` / `ExcelImporter` / `GameDataInitializer` 均未被删除。**

## Slice 4 — Verification

**目标**：产出 M2 Gate 证据（不新增产品功能）。

| # | 验证 | 方式 |
|---|---|---|
| 4.1 | 等价：Legacy 磁盘 SO vs `MonsterDefinition`，3 monsters × 8 fields 逐条逐字段 | M1 `E1` 用例（保留） |
| 4.2 | 顺序：非 Canonical 顺序仍按 list index → `BattleColumn` | M1 `E3` 用例（保留） |
| 4.3 | Registry 契约：`S1–S4`（数量 / 8 字段 / 稳定顺序 / 重复 ID 失败 / 未命中诊断）+ Slice 1 新增用例 | EditMode |
| 4.4 | 无 dual read：Runtime 目录 `DataManager.Instance.AllMonsters` / `MonsterData` 命中 = 0；不存在开关式双读 | 源码断言（沿用 M1 `CountOccurrencesUnder` 手法） |
| 4.5 | Hidden Source：`H` 用例**翻转**为「inline 源必须不存在」+ `ImportAll` / `GameDataInitializer` 冻结断言 | EditMode（T1） |
| 4.6 | Fresh rebuild：`StaticConfigBuilder.RebuildFromZero()` → exact path Root 可加载 → Registry = 3 → 产品链可打（FDS §14 / H11） | EditMode + batchmode |
| 4.7 | 测试类型改造完成：T2（约 10 文件）与 T3（约 15 文件 / 约 30 处）全部通过 | EditMode / PlayMode 编译 + 运行 |
| 4.8 | 产品链回归：Town → 确认出征 → Battle / Elite / Boss → 战斗 → 返回 | PlayMode（现有产品链用例） |
| 4.9 | 全量回归：EditMode 全量 + PlayMode 全量（**串行**，不并行）+ 工具链 `dotnet build` / `dotnet test` / `rrpg-config validate` | batchmode + cmd |
| 4.10 | Save 回归：`Monster` 0 命中，`SaveVersion` 不变 | EditMode / PlayMode |

**验收**：§5.3 的 G1–G6 全部有 PASS 证据。（本轮**未执行**任何测试；测试属 Slice 4 动作。）

## Slice 5 — M2 Closeout

| # | 内容 |
|---|---|
| 5.1 | 汇总 State / Handoff：M2 改动清单、Gate 证据、未覆盖项与原因 |
| 5.2 | Disposable Cleanup：清理一次性测试与临时产物（长期回归仅保留冻结契约的最小代表集） |
| 5.3 | Authority 状态同步：`Monster.csv` Frozen、`RRPG_Monsters.xlsx` Authority、`Development/Config/README.md` 同步 |
| 5.4 | Commit 切片与信息（FDS §10.6）：Authority 转移标注为 M2；M3 / M4 不得混入同一 commit |
| 5.5 | 精确暂存清单 + Final Diff / Workspace Review；**等待用户授权后才 commit / push** |
| 5.6 | M4 待办登记：`MonsterData` 物理清理七步（FDS §10.5）+ D-M2-1 Adapter 删除条件 |

---

# 附录 A — Monster 字段需求矩阵（M2 判定输入）

| 字段 | 当前存在（SO / `MonsterDefinition`） | Consumer（文件:行） | 状态 |
|---|---|---|---|
| `MonsterId` | ✅ / ✅ | `BattleSystem.cs:521-522`（`UnitId` + `SourceId`）、`BattleUI.cs:412 / 1646`、`BattleUIDisplayNames.cs:20` | **保留**（身份语义不变，I1） |
| `Name` | ✅ / ✅ | `BattleUI.cs:421`、`BattleUI.cs:1647`、`BattleUIDisplayNames.cs:21` | **保留** |
| `MaxHP` | ✅ / ✅ | `BattleSystem.cs:524`、`BattleUI.cs:422` | **保留** |
| `Attack` | ✅ / ✅ | `BattleSystem.cs:525`、`BattleUI.cs:423` | **保留** |
| `AttackRange` | ✅ / ✅ | `BattleSystem.cs:527` | **保留** |
| `AttackCount` | ✅ / ✅ | `BattleSystem.cs:526` | **保留** |
| `ActionPoints` | ✅ / ✅ | `BattleSystem.cs:528`（`maxActionPoints`） | **保留** |
| `Faction` | ✅ / ✅ | `BattleSystem.cs:529` | **保留** |
| Sprite / 每怪视觉 | ❌ / ❌ | `BattleUIUnitVisualHosts.cs:36 / 97-102`（`enemyBodyPlaceholder`，**非** Monster 字段） | **M3**（EnemyPresentation） |
| AI / Behavior | ❌ / ❌ | Enemy Intent 由 `BattleSystem` 规则生成（运行时状态） | **Future / 不迁** |
| 掉落 / 奖励 | ❌ / ❌ | `RunState` Pending Rewards | 不迁 |
| 初始技能 | ❌ / ❌ | `BattleUI.cs:1521` 敌方技能传空列表 | 不迁 |

**结论**：M2 **不需要任何字段扩展**；`MonsterDefinition.FieldCount = 8`（`MonsterDefinition.cs:25`）与需求一致。`MonsterDefinition` 与 SO 的字段**声明顺序**不同（`MaxHP` 位置），与消费者无关，不构成风险。

---

# 附录 B — 风险登记（M2 实施期）

| # | 风险 | 证据 | 处置 |
|---|---|---|---|
| R1 | 生成根缺失导致静默 0 敌人 | `.gitignore:103`、`StaticConfigRegistry.cs:36-47`、`BattleSystem.cs:67-76` | **已裁决 D-M2-1**：Disposable Fail-safe Adapter + WarnOnce |
| R2 | M1ShadowSync 可用 Frozen CSV 覆写新 Authority | `Development/Config/README.md:38-39`；FDS H7 / H15 | **已裁决 D-M2-2**：Cutover 后冻结 |
| R3 | M1 `MonsterShadowTests` 的 `H` 用例与 M2 目标方向相反（现断言 inline 源**存在**） | `MonsterShadowTests.cs:363-378` | Slice 4.5：**同批翻转**为「inline 源必须不存在」；E1/E2/E3 保留为 Gate 证据 |
| R4 | `BattleUI` 若声明 `IReadOnlyList<>` 会破坏 C6/C7 的 `.Find` | `BattleUI.cs:1646`、`BattleUIDisplayNames.cs:20` | §3.3 约定：`InitFromExpedition` 物化 `List<MonsterDefinition>` |
| R5 | `AdventureManager` ctor 是产品链路且被大量反射测试构造，漏改即全量红 | `AdventureManager.cs:62-66`；§3.4 T2 / T3 | 只保留新签名；Slice 2 与 T2/T3 同批完成 |
| R6 | 冻结 `ImportAll` 时误伤其它领域导入 | `ExcelImporter.cs:31-42` | 仅移除 Monster 调用（`:38`），其余导入不动 |
| R7 | 给 `Monster.csv` 加注释会破坏 CSV 解析（第 1 行 = 表头） | `ExcelImporter.cs:466-468` | 只在 README 标注 Frozen；**不改** CSV 内容 |
| R8 | 公共接口变更（`BattleSystem.InitializeBattle` / `BattleUI.InitFromExpedition` / `AdventureManager` ctor）属高风险权限类别 | `.clinerules.md §6` | 由 FDS §9 Wave1C 明确授权；Slice 2 报告需显式声明变更面 |
| R9 | M1 等价用例的 Legacy 侧依赖 `Resources.LoadAll<MonsterData>` / `DataManager.Instance` | `MonsterShadowTests.cs:422-453` | M2 不阻塞（SO 与 `DataManager` 均保留）；M4 物理清理时改由 Git 历史快照承担等价证据（登记为 M4 待办） |
| R10 | PlayMode 测试 asmdef 未引用 `StaticConfig.Runtime` | B8 | 反射取类型或补 asmdef reference（Slice 4.7 实施时择一，需保持团结引擎 asmdef 不能引用 Assembly-CSharp 的既有约定） |

---

# 附录 C — 本轮边界复核（DOCUMENT ONLY 证据）

| 检查 | 结果 |
|---|---|
| 新增文件 | 仅 `Docs/DevelopmentSpecs/2026-09-22_RRPG_M2-Monster-Definition-Cutover-Pre-Implementation-Audit-V0.1.md` |
| C# / Unity Asset / Scene / Prefab / SO 改动 | 无 |
| `Development/Excel/Monster.csv` | 未改 |
| `Development/Config/RRPG_Monsters.xlsx` | 未改 |
| Runtime 行为 | 未变 |
| Authority Cutover | 未开始 |
| commit / push | 未执行 |

**本文件性质**：M2 的实施前契约（FORMAL DEVELOPMENT AUDIT）。它**不**改变 FDS 的 Gate 条件与 Hard Stop 清单；若实施期发现 FDS §17 任一 Hard Stop，仍须**停止并上报**（现象 / 证据（文件:行）/ 影响范围 / 需要的决策）。

## 变更记录

| 日期 | 版本 | 说明 |
|---|---|---|
| 2026-09-22 | V0.1 | 首次落盘：M2 Pre-Implementation Audit（Baseline / Objective / Consumer Map / Legacy Classification / Authority Cutover Decision / Decision Log（D-M2-1、D-M2-2）/ Implementation Plan Slice 1–5 / 附录 A–C） |





