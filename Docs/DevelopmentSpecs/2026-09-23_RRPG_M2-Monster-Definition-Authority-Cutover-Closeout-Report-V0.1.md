# RRPG M2 — Monster Definition Authority Cutover：Closeout Report V0.1

> 日期：2026-09-23
> 性质：**M2 CLOSEOUT REPORT（Verification + Evidence；不进入 M3）**
> Authority（架构）：**D-019**；上游契约：`Docs/DevelopmentSpecs/2026-09-22_RRPG_Static-Configuration-Migration-FDS-V0.1.md`（FDS）
> 前置审计：`Docs/DevelopmentSpecs/2026-09-22_RRPG_M2-Monster-Definition-Cutover-Pre-Implementation-Audit-V0.1.md`（M2 Audit）
> 基线：`migration/static-config-m1` @ `f7c2e89123115e8da73a9cd3c9a2be459c83f8dc`（M1 完成点）
---

# 1. M2 Objective

**目标**：把 Monster 静态定义的 **Authority** 从 Legacy 链（`Monster.csv` → `ExcelImporter` → `MonsterData` SO → `DataManager`）
转移到 Static Config 链（`RRPG_Monsters.xlsx` → Compiler → Canonical Mirror → Generated Root → `MonsterRegistry` → `MonsterDefinition`），
并保证：

| # | 要求 | 结果 |
|---|---|---|
| O1 | 产品 Runtime **只**从 `StaticConfigRegistry` 取得 Monster 定义 | ✅（Runtime 对 `DataManager.Instance.AllMonsters` 消费 = 0） |
| O2 | 8 字段语义 / `MonsterId` 身份 / Battle 行为（顺序 → `BattleColumn`）不变 | ✅（E1/E3 与 Battle 回归全绿） |
| O3 | Legacy Authoring 写入口**技术性冻结**（FDS §10.4 A/B/C） | ✅（Importer / Initializer / Demo inline 三处） |
| O4 | `Monster.csv` = **Frozen Legacy**；CSV → XLSX 同步关闭 | ✅（M1ShadowSync `sync` 拒绝执行，exit=1） |
| O5 | 迁移期等价证据保留（Legacy SO ↔ Definition） | ✅（`MonsterShadowTests` E1/E2/E3 + `MonsterRegistryEntryTests` R2） |
| O6 | Fresh Rebuild 可确定性重建生成根（FDS §14 / H11） | ✅（删除生成根后重建，签名一致、entries=3） |
| O7 | **不删除** `MonsterData` / SO / `DataManager` / Legacy Editor Tool；不做 M4 物理退休 | ✅（全部保留在位） |

**非目标（明确不做）**：EnemyPresentation Authority（M3）、`MonsterData` 物理退休与工具清理（M4）、任何 Gameplay / Save 变更。

---

# 2. Migration Timeline

| Slice | 主题 | 关键交付 | 状态 |
|---|---|---|---|
| **0** | Pre-Implementation Audit | `2026-09-22_…-Pre-Implementation-Audit-V0.1.md`：Consumer Map（C1–C9）、字段矩阵、Cutover 方案、D-M2-1 / D-M2-2 决策、实施切片计划、风险登记 R1–R10 | ✅ |
| **1** | Runtime Registry Entry | `StaticConfigRegistry.TryGetMonsters / Monsters / MonsterDiagnostic / UsingLegacyFallback`；`MonsterRegistry.GetById / TryGetById`（+ M1 别名）；`LegacyMonsterFallbackAdapter`（D-M2-1 Disposable 失败保护）；`MonsterRegistryEntryTests` R1–R5 | ✅ |
| **2A** | Runtime Consumer Cutover | C1 `ExpeditionUI` → `StaticConfigRegistry.Monsters.All`；C2 `AdventureManager` → `IReadOnlyList<MonsterDefinition>`；C3 `BattleUI` 物化 `List<MonsterDefinition>`（保 `.Find/.Count/索引器`）；C4 `BattleSystem.InitializeBattle / CreateMonsterUnit` → `MonsterDefinition`（8 字段映射逐字不变）；受影响测试辅助同步 | ✅ |
| **2B** | Test Migration | 产品测试输入统一走 `StaticMonsterTestData`（→ `StaticConfigRegistry.Monsters`，29 个 PlayMode 调用点 + EditMode 10 文件）；Legacy 仅保留于迁移证据测试并显式注明；**无 Mock / 第二套数据系统** | ✅ |
| **2C** | Legacy Freeze | ① `ExcelImporter.ImportAll` 不再导入 Monster（方法保留）② `GameDataInitializer` Monster seed 执行路径禁用 ③ `BattleUI` inline Monster 源移除（Demo → Registry）④ M1ShadowSync `sync` 拒绝执行 ⑤ `Development/Config/README.md` Authority 状态更新 ⑥ H 用例翻转为「旧入口必须已冻结」 | ✅ |
| **2D** | Verification + Closeout | 本文件：Source Authority Audit / Legacy Freeze Audit / StaticConfig Gate / Build / Unity EditMode + PlayMode / Fresh Rebuild / M3·M4 TODO | ✅（本文件） |

**Authority 转移的“明确时刻”**（FDS §10.2 / §10.6）：Slice **2C** 完成时——同一时刻起 `Monster.csv` 视为 Frozen Legacy，
`RRPG_Monsters.xlsx` 为 Current Monster Definition Authoring Authority。

---

# 3. Authority Before / After

## 3.1 Before（M1 结束时的 Current 链）

```text
Development/Excel/Monster.csv          ← 唯一人工 Authoring Authority
        ↓ ExcelImporter.ImportMonsters（Editor 菜单 / batchmode）
Assets/Resources/ScriptableObjects/Monsters/*.asset（3 个 MonsterData SO）
        ↓ DataManager.LoadAll()（Resources.LoadAll<MonsterData>，惰性单例）
        ↓ DataManager.AllMonsters
ExpeditionUI → AdventureManager → BattleUI → BattleSystem → BattleUnit
（旁路隐藏源：GameDataInitializer seed / BattleUI inline demo / M1 ShadowSync 可覆写 XLSX）
```

## 3.2 After（M2 结束时的 Current 链）

```text
Development/Config/RRPG_Monsters.xlsx  ← Current Monster Definition Authoring Authority（人工）
        ↓ RRPG.ConfigCompiler build（RRPG_一键导表.cmd；validate 只读）
Development/Generated/StaticConfig/Monster.json（Canonical Mirror，进 Git，DO NOT EDIT）
        ↓ Unity Builder（StaticConfigBuilder）
Assets/Generated/StaticConfig/（Generated Root + MonsterConfigDatabase；不进 Git）
        ↓ StaticConfigRegistry（lazy / 幂等 / 明确诊断；无 Editor 依赖）
        ↓ MonsterRegistry（稳定顺序 / GetById / TryGetById）
        ↓ MonsterDefinition（8 字段）
ExpeditionUI → AdventureManager → BattleUI → BattleSystem → BattleUnit
```

## 3.3 迁移后的不变量与例外

| 项 | 状态 |
|---|---|
| `MonsterId` 身份语义（`BattleUnit.SourceId` = `MonsterId`） | 不变 |
| 8 字段（`MonsterId/Name/MaxHP/Attack/AttackRange/AttackCount/ActionPoints/Faction`） | 不变（`MonsterDefinition.FieldCount = 8`） |
| 敌方顺序 → `BattleColumn` | 不变（Canonical 顺序 = 原 `AllMonsters` 顺序，E2 已证明） |
| Save / Person Identity / Gameplay 规则 | 未触碰 |
| Legacy 类型与资产 | **保留**（`MonsterData` / 3 个 SO / `DataManager` / `ImportMonsters` 方法体 / DataValidationTool） |
| 唯一例外：失败路径 | `LegacyMonsterFallbackAdapter`（D-M2-1）：**仅在 Root 缺失**时只读回退 + WarnOnce + 明确诊断；单向、Disposable、M4 删除条件已写明 |

# 4. Consumer Migration Summary

> 口径：M2 Audit §3.1 的 C1–C9 + 测试体系。**产品 Runtime 的唯一 Monster 入口 = `StaticConfigRegistry.Monsters` / `TryGetMonsters()`**。

## 4.1 产品 Runtime（已切换）

| # | Consumer | Before | After | 行为差异 |
|---|---|---|---|---|
| C1 | `ExpeditionUI.OnConfirmExpedition` | `DataManager.Instance.AllMonsters` | `StaticConfigRegistry.Monsters.All`（在 `ConfirmExpedition` **之前**解析；彻底不可用时 LogError + 拒绝出征） | 无（列表内容/顺序一致） |
| C2 | `AdventureManager` ctor / `GetEnemyMonsters()` | `List<MonsterData>` | `IReadOnlyList<MonsterDefinition>`（**无旧重载**） | 无 |
| C3 | `BattleUI.InitFromExpedition` / `_enemyMonsters` | `List<MonsterData>` | `IReadOnlyList<MonsterDefinition>` → 物化为 `List<MonsterDefinition>`（`Count`/索引器/`Find` 调用点零改动） | 无 |
| C4 | `BattleSystem.InitializeBattle` / `CreateMonsterUnit` | `List<MonsterData>` → 逐字段读 | `IReadOnlyList<MonsterDefinition>` → 逐字段读（映射逐字未变） | 无 |
| C5 | `BattleUI` 战前卡面（`:405-426`） | `m.MonsterId/Name/MaxHP/Attack` | 同名字段（`MonsterDefinition`） | 无 |
| C6 | `BattleUI` 战斗中敌名（`:1646`） | `_enemyMonsters.Find(...)` | 同一实现（未改为 Registry 按 ID 查找——刻意保持行为等价迁移） | 无 |
| C7 | `BattleUIDisplayNames`（`:20`） | 同 C6 | 同 C6 | 无 |
| C8 | `BattleUI.InitializeDefaultBattleData`（Demo） | `DataManager.AllMonsters` + 内联 `mon_001/mon_002` | **`StaticConfigRegistry.Monsters`**（inline 源与 `CreateMonsterData` helper 已删除） | Demo 行为/顺序不变；不可用时 LogError + 空列表（不静默） |
| C9 | 规则判定（`UnitType.Monster`） | 仅类型判定 | 不变 | 无 |

## 4.2 测试体系（已迁移；Legacy 仅存于迁移证据）

| 组 | 迁移方式 | 结果 |
|---|---|---|
| PlayMode（15 文件 / 29 调用点） | `StaticMonsterTestData.Build()` → `StaticConfigRegistry.Monsters.All`（按 asmdef 各一份同名 Helper，只返回 `MonsterDefinition`） | 产品测试 **0** Legacy 依赖 |
| EditMode（10 文件） | 类型解析器 + `StaticConfigRegistry.Runtime` 回退；元素类型 `MonsterData` → `MonsterDefinition` | 0 Legacy 输入 |
| 迁移证据（保留 Legacy，显式注明） | `MonsterShadowTests`（E1 逐字段 / E2 顺序 / E3 顺序行为 / H 冻结门）；`MonsterRegistryEntryTests.R2`（D-M2-1 回退等价） | 迁移证据链完整，且**不是**产品输入 |

## 4.3 Source Authority Audit（Task 1，实测）

| 检查 | 命令口径 | 结果 |
|---|---|---|
| Runtime 消费 `DataManager.Instance.AllMonsters` | `Assets/Scripts/**` 全量检索 | **0 个文件** ✅ |
| Runtime 出现 `MonsterData`（剥注释、词边界） | `Assets/Scripts/**` | 仅 **2** 处允许项：`Core/DataManager.cs`（Legacy 载体：字段/`LoadAll`/`GetMonsterById`）与 `StaticConfig/LegacyMonsterFallbackAdapter.cs`（D-M2-1 回退适配，且**仅出现在 WarnOnce 文案字符串**，无编译期类型依赖） ✅ |
| Runtime 出现 `CreateMonsterData(` | `Assets/Scripts/**` | **0** ✅ |
| `GetMonsterById` 调用者 | 全仓 | **0**（仅定义） |
| Legacy 迁移证据测试使用 Legacy | `Assets/Tests/**` | 允许（`MonsterShadowTests` / `MonsterRegistryEntryTests.R2`） |

**结论**：产品 Runtime 的 Monster Authority 已完全转移到 StaticConfig；Legacy 仅作为「载体 / 失败保护 / 迁移证据」保留到 M4。

---

# 5. Legacy Freeze Summary

| # | 冻结点 | 位置 | 冻结方式 | 保留物 |
|---|---|---|---|---|
| F1 | Monster CSV → SO 导入 | `Assets/Editor/ExcelImporter.cs` → `ImportAll()` | 移除 `ImportMonsters(...)` 调用 + `// M2 FROZEN LEGACY / MonsterData authoring retired. / Kept for M4 physical retirement only.` | `ImportMonsters` 方法体（M4 删）；其余 6 领域导入不受影响（H 测试逐个断言） |
| F2 | Monster seed 创建 | `Assets/Editor/GameDataInitializer.cs` | 2 个 `CreateAndSet<MonsterData>(...)` 执行路径注释禁用 + `// M2 FROZEN LEGACY / Monster seed creation disabled. / StaticConfig MonsterDefinition is current authority.` | 代码以注释保留（M4 删）；其它初始化不变 |
| F3 | BattleUI inline Monster | `Assets/Scripts/UI/BattleUI.cs` | 删除 `CreateMonsterData` helper 与 `mon_001/mon_002` 内联；删除 2A 的 Disposable `ToMonsterDefinitions`；新增 `ResolveDemoEnemyMonsters()` → `StaticConfigRegistry.Monsters` | Demo 行为 / 顺序 / 规则不变 |
| F4 | CSV → XLSX 覆写 | `Tools/RRPG.ConfigCompiler/migration/…M1ShadowSync/Program.cs` | `sync` **拒绝执行**：`M1 ShadowSync is frozen after M2 Monster Definition Authority Cutover.` + `CSV -> XLSX synchronization is no longer allowed.` + `MIGRATION ONLY / M1 CLOSED` → `ExitHardError`（实测 exit=1） | 工具保留；`equivalence` / `status` 只读仍可用；`ShadowWorkbookWriter.Sync(` 已无调用点 |
| F5 | 文档状态 | `Development/Config/README.md` | XLSX = **Current Monster Definition Authoring Authority**；`Monster.csv` = **Frozen Legacy Source**；同步已关闭；三处旧写入口已冻结 | 未改任何 XLSX / CSV 内容 |
| F6 | Gate 翻转 | `Assets/Tests/EditMode/StaticConfig/MonsterShadowTests.cs` | `H_HiddenSources_AreFrozen_LegacyAuthoringClosed`：断言四类旧入口**已冻结** + Source Audit（Runtime Legacy 消费 = 0）+ Legacy 仍存在（M4 前证明） | E1/E2/E3 与 M1 等价证据未删改 |

**Legacy 资产当前状态**：`Monster.csv`（Frozen，只读）、`RRPG_Monsters.xlsx`（Authority）、3 个 `MonsterData` SO（Frozen 保留）、`MonsterData.cs`（保留）、`DataManager`（保留、0 产品消费）、`ImportMonsters` 方法体（保留）、M1ShadowSync（保留、sync 冻结）、`LegacyMonsterFallbackAdapter`（Disposable 保留）。

**已知遗留（不在 M2 范围，登记 M4）**：
- `Assets/Editor/DataValidationTool.cs:26` 仍 `Resources.LoadAll<MonsterData>`（**只读校验器**，不产生内容）。
- `ExcelImporter.ImportMonsters` 方法体内仍含 `CreateInstance<MonsterData>()`（入口已冻结，无调用方）。
- Compiler 的 `validate` / Producer 输出仍会打印 M1 Shadow 时代的状态文案（`M1 SHADOW：… Shadow Candidate …`），与 2C 后的 Authority 事实不一致（**工具输出文案待更新**，见 §7）。

---

# 6. Verification Evidence

> 全部为**本轮实测**（2026-09-23）；证据文件位于 gitignored 的 `Tools/RRPG.ConfigCompiler/.build-state/`。

| # | 验证 | 命令 / 过滤 | 结果 | 证据 |
|---|---|---|---|---|
| V1 | Compiler build | `dotnet build src/…RRPG.ConfigCompiler.csproj -c Release` | **PASS — 0 warning / 0 error** | `BUILD_EXIT=0` |
| V2 | Compiler tests | `dotnet test tests/…RRPG.ConfigCompiler.Tests.csproj -c Release` | **PASS — 97/97**（失败 0） | `TEST_EXIT=0` |
| V3 | **M1ShadowSync 冻结（实测）** | `dotnet run --project migration/… -- sync --root <repo>` | **REFUSED — exit=1**；输出 `M1 ShadowSync is frozen after M2 Monster Definition Authority Cutover.` / `CSV -> XLSX synchronization is no longer allowed.` / `MIGRATION ONLY / M1 CLOSED` | `SYNC_EXIT=1` |
| V4 | M1ShadowSync 只读等价（保留能力） | `… -- equivalence` | **PASS**：`[A]` CSV 3 条；`[B]` XLSX→Canonical validate PASS；结论 `3 × 8 逐条逐字段一致（CSV ↔ XLSX ↔ Mirror）` | `EQUIV_EXIT=0` |
| V5 | Producer 只读校验 | `rrpg-config validate --root <repo>` | **PASS**（errors=0 / warnings=4：均为 `RRPG_EnemyPresentation.xlsx` 导航 Sheet 跳过 + 两个空 Sheet —— **M3 域**，非 M2 缺陷） | `VALIDATE_EXIT=0` |
| V6 | **StaticConfig Gate（聚焦）** | EditMode `-m2TestFilter RRPG.StaticConfig.Tests` | **PASS — 24/24**（`MonsterShadowTests` 10 + `StaticConfigBuilderTests` 9 + `MonsterRegistryEntryTests` 5） | `m2s2d-staticconfig-summary.txt`（12:31:26 起） |
| V7 | **Unity EditMode 全量** | EditMode（无过滤） | **PASS — 730/730**（失败 0 / 跳过 0） | `m2s2d-editmode-summary.txt`（12:33:39 起） |
| V8 | **Unity PlayMode 产品链**（Town → Expedition → Battle → Return） | PlayMode `-m2TestFilter VillageV4FullProductChainPlayTests` | **PASS — 1/1** | `m2s2d-playmode-chain-summary.txt`（12:35:10 起） |
| V9 | **Unity PlayMode 返程链** | PlayMode `-m2TestFilter ExpeditionV4FReturnProductPlayTests` | **PASS — 3/3** | `m2s2d-playmode-v4f-summary.txt`（12:36:28 起） |
| V10 | **Fresh Rebuild（Task 6）** | 删除 `Assets/Generated/StaticConfig` → `-executeMethod RRPG.StaticConfig.EditorTools.StaticConfigBuilder.ExecuteBuild` | **PASS**：`noOp=false`；created `MonsterConfigDatabase` / `EnemyPresentationDatabase` / `VisualAssetCatalog` / **`RRPGStaticConfigRoot.asset`**；`monster database: entries=3`；signature 与删除前**完全一致** | `builder.stage.txt` |

## 6.1 Task 3 — StaticConfig Gate 细化（Registry 行为）

| 要求 | 覆盖用例 | 结果 |
|---|---|---|
| Root 正常 → 返回 3 个 monster | `R1_RootPresent_MonstersReturnsThreeDefinitions`、`S1/S2` | ✅ |
| 顺序 = `mon_001, mon_002, mon_003_wolf` | `S2`（Registry 稳定顺序）、`E2`（与 Legacy 枚举顺序逐项一致） | ✅ |
| 缺 Root → 触发 Disposable Adapter | `R2_MissingRoot_FallsBackToLegacy_WithWarnOnce` | ✅ 回退返回 Legacy 3 条 |
| **WarnOnce**（恰好一次） | `R2`（`Application.logMessageReceived` 计数 = 1，重复读取不再告警） | ✅ |
| **明确中文诊断** | `R2`（含 `RRPG_一键导表.cmd` 修复指引 + `Disposable` 标记） | ✅ |
| **业务层不判断 fallback** | `R4`（`LegacyMonsterFallbackAdapter` / `UsingLegacyFallback` 在 StaticConfig 层之外 **0 命中**） | ✅ |
| 无 Editor 依赖 / 幂等 / 查询 API | `R3`、`R4`、`R5`、`B1/B2/B5`、`R_RuntimeAssembly_HasNoEditorDependency` | ✅ |

## 6.2 Task 6 — Fresh Rebuild 细节

```text
[删除] Assets/Generated/StaticConfig  →  GENERATED_ROOT_DELETED（目录确认为空）
[重建] -executeMethod …StaticConfigBuilder.ExecuteBuild
       stage=build  result=PASS  noOp=false
       action=created: Databases/MonsterConfigDatabase.asset
       action=created: Databases/EnemyPresentationDatabase.asset
       action=created: Databases/VisualAssetCatalog.asset
       action=created: Resources/RRPGStaticConfigRoot.asset
       action=monster database: entries=3
       action=signature: f24834804376af452af32b81b955a8ea7cb32f828dac974f5e54729c57767874
       action=mirror files: 1（Monster.json，rows=3 fields=8 sourceSha256=339f188afa83）
[确认] ROOT_ASSET_REBUILT（exact path = Resources/RRPGStaticConfigRoot）
       exact-path 加载断言：StaticConfigBuilderTests B1/B5 + MonsterRegistryEntryTests R1（均在 V6 的 24/24 内）
```

**确定性证据**：删除前后 `content-signature` **完全一致**（`f2483480…7874`）→ 同输入同产物（D-019 确定性要求；FDS §14 等价产物判定）。

## 6.3 环境与 Anti-Hang 记录（透明披露）

| 事项 | 事实 | 处置 |
|---|---|---|
| 内置 CLI `-runTests … -quit` | 本项目编辑器**启动期必定 domain reload**，`-quit` 抢先退出 → Test Starter 从不执行、无 results XML（M1 起即如此） | 使用 **Disposable Gate Runner**（公开 `TestRunnerApi` + `EditorApplication.Exit`，含 PlayMode domain reload 恢复）；作为 2D 交付的 Disposable 项登记 |
| **并发编辑器事故**（本轮新增） | 一次 EditMode 启动时上一实例尚未退出 → 新实例日志 `Multiple Tuanjie instances cannot open the same project.` 并立即退出（**未产生摘要**） | 依 Anti-Hang：读日志确认原因 → `taskkill` 残留进程 + 清理 lockfile → **串行重跑**（V7 为串行重跑结果） |
| PlayMode 全量（无过滤） | Slice 2A 曾挂起：日志 1238 行后无新增、内存持平、**0 条 `fail:`**；本轮按任务只跑指定产品链用例 | 全量 PlayMode 挂起定位**登记为 M4 前置 TODO**（记录时间/日志位置/XML/是否有 fail） |
| MCP 包噪声 | `com.ivanmurzak.unity.mcp` 后台连接 `localhost:29387` 失败会产出异常日志，可能让当次随机用例因 “Unhandled log message” 失败（Slice 2A 命中 1 次，复跑 0 次） | 记录为环境噪声；不修改项目配置 |

---

# 7. Remaining M3 / M4 TODO

> 本清单**不在 M2 修复**（按任务边界：发现问题只登记，不顺手修）。

## 7.1 M3 — Enemy Presentation Authority（下一阶段）

| # | TODO | 依据 |
|---|---|---|
| M3-1 | `MonsterId → EnemyPresentation → VisualAssetId → Catalog → Sprite` 在 Battle 链生效；`RRPG_EnemyPresentation.xlsx` + `Development/SourceAssets/Enemy` 成为 **Presentation Authoring Authority** | FDS §10.3 / Wave 1D |
| M3-2 | `enemyBodyPlaceholder` 降级为 **fallback**（missing Presentation / missing VisualAsset / missing Sprite → 明确 fallback + WarnOnce 诊断；不得静默保持旧 Sprite） | FDS §9 Wave 1D / §0.4 |
| M3-3 | 不允许 Scene / Prefab 手拖 per-Monster Sprite 成为正式路径 | FDS §9 Wave 1D |
| M3-4 | M3 完成后 `validate` 的 4 条 EnemyPresentation 空表/导航 warning 应消失（当前为 M3 域待落地，不是 M2 缺陷） | 本轮 V5 证据 |

## 7.2 M4 — Legacy Physical Retirement

| # | TODO | 依据 |
|---|---|---|
| M4-1 | `MonsterData` 物理退休七步（**依序**）：删 `Monster.csv` → 3 个 Monster SO（+`.meta`）→ `ExcelImporter` Monster importer 代码 → `DataManager` Monster `LoadAll`/缓存 → `GameDataInitializer` 残余 → 无用 Demo 残余 → **最后** `MonsterData` 类型 | FDS §10.5 |
| M4-2 | `DataValidationTool` 清理（`:26` `Resources.LoadAll<MonsterData>`，Legacy 只读校验器） | 本轮 §5 遗留 |
| M4-3 | 删除 `LegacyMonsterFallbackAdapter`（条件：生成链 Fresh Rebuild 稳定可用 + `MonsterData` 引用归零） | D-M2-1 A6 |
| M4-4 | 迁移证据退役：`MonsterShadowTests` 的 Legacy 侧读取 / `MonsterRegistryEntryTests.R2` 的 Legacy SO 读取改由 Git 历史快照承担 | M2 Audit §5 R9 |
| M4-5 | Disposable 收敛：`M2DisposableGateRunner`、3 份 `StaticMonsterTestData`、Demo 专用解析函数等按「最小长期回归集」清理 | Slice 1/2B/2C 记录 |
| M4-6 | ~~PlayMode 全量挂起定位（时间戳 / 日志位置 / 是否产出 XML / 是否有 fail）并修复或正式标注~~ → **✅ 已关闭（2026-09-23，M3 Final Closeout 轮）：不在 M4 待办、不重复开发** | Slice 2A + 本轮 §6.3 |
| **M4-6 结论（2026-09-23，M3 Final Closeout 轮）** | ✅ **已定位并通过无过滤 PlayMode 全量两次验证关闭**：根因 = `BattleVisualFoundationPlayTests.CaptureShotAndWait` 内的 `WaitForEndOfFrame` 在 `-batchmode`（无 Game View）**永不触发** → 测试挂死（不是超时/性能问题：**无过滤全量 71 个测试总耗时仅 35.6 / 35.7 s**）。修法 = 改为帧计数等待 + 测试内 best-effort 截图并写 `.txt` 标注是否产出。验证 = 无过滤 PlayMode 全量 **71/71 PASS × 2 次**（日志 `Tools/RRPG.ConfigCompiler/.build-state/m3f-pmfull.log`、`m3f-pmfull2.log`）。详见 M3 Closeout 报告 §7.5B / §7.7 / §11.1 |  |
| **M4 实施结果（2026-09-23）** | M4-1…M4-5 已实施并关闭（Monster Legacy 物理退休：入口 / 资产 / 类型 / 回退链 / 迁移证据测试）；M4-6 仅做状态校正、**未重复开发**。详见 `Docs/DevelopmentSpecs/2026-09-23_RRPG_M4-Monster-Legacy-Physical-Retirement-Closeout-Report-V0.1.md` | M4 |

## 7.3 M2 遗留（工具文案；非阻塞，建议与 M3 前置一并处理）

| # | 事项 | 状态 |
|---|---|---|
| M2-R1 | Compiler `validate` / Producer 输出仍打印 M1 Shadow 时代文案（`M1 SHADOW：本目录的 XLSX 目前是 Shadow Candidate…Current Monster Authoring Authority 仍是 Monster.csv（M2 才切换）。`），**与 2C 后的事实相反** → 需更新工具输出文案（不改语义、不改 Authority） | ✅ **已在 Slice 2D.1 修复**（见 §9） |
| M2-R2 | 若制作人仍按旧文档操作 `Monster.csv`，会被静默忽略 → 已在 `Development/Config/README.md` 说明；建议 Producer 入口脚本补一行状态提示（与 M2-R1 同批文案工作） | ✅ **已在 Slice 2D.1 修复**（validate/help 输出 + 工具 README 均声明 Authoring Authority 与 Frozen Legacy；根 `.cmd` 入口本身为 ASCII-only 薄封装，无文案面） |

---

# 8. 状态与边界声明

| 项 | 状态 |
|---|---|
| 本文件性质 | M2 **Closeout Report**（验证 + 证据 + TODO 登记）；**DOCUMENT ONLY** |
| 本轮代码改动 | 仅 `Docs/DevelopmentSpecs/` 新增本文件（**未改任何 Gameplay / Save / EnemyPresentation / Legacy 资产**） |
| M2 结论 | **Monster Definition Authority Cutover 完成**：产品链唯一入口 = `StaticConfigRegistry`；Legacy Authoring 写入口已冻结；`RRPG_Monsters.xlsx` = Authority；`Monster.csv` = Frozen Legacy |
| 禁止项核对 | 未进入 / 未修改 EnemyPresentation；未改 Battle Gameplay / Save；未删 `MonsterData` / SO / `DataManager` / Legacy Editor Tool；未做 M4 物理退休 ✅ |
| 下一步 | **M3 — Enemy Presentation Authority**（需新一轮 Review 授权） |

## 变更记录

| 日期 | 版本 | 说明 |
|---|---|---|
| 2026-09-23 | V0.1 | 首次落盘：M2 Closeout Report（Objective / Timeline Slice 0–2D / Authority Before-After / Consumer Migration Summary / Legacy Freeze Summary / Verification Evidence（V1–V10 + Fresh Rebuild + Anti-Hang）/ M3·M4 TODO / 状态与边界） |
| 2026-09-23 | V0.1.1 | 追加 §9：Slice 2D.1 M1 文案漂移修复（工具输出 / README 统一反映 M2 Authority）；M2-R1 / M2-R2 标记为已修复 |

---

# 9. Slice 2D.1 — M1 文案漂移修复（Text-only）

> 背景：M2 Authority Cutover（2C）后，Compiler / 工具 / README 的部分输出仍声明 M1 Shadow 时代事实
> （“XLSX 是 Shadow Candidate”“Current Monster Authoring Authority 仍是 Monster.csv”），与 Authority 事实相反。
> 本 Slice **只修文案**：不改 Runtime、不改测试逻辑、不改配置数据、不改资产。

## 9.1 修改文件（5 个，全部为文案/文档）

| 文件 | 修改内容 |
|---|---|
| `Tools/RRPG.ConfigCompiler/src/RRPG.ConfigCompiler/Program.cs` | ① validate/build 头 `Production V0.1 · M0` → `· M2`；② 原 `M1 SHADOW：… Shadow Candidate … / Current Monster Authoring Authority 仍是 Monster.csv` 两行 → `M2 AUTHORITY：… = Current Monster Definition Authoring Authority（内容变更只改这里）` + `Frozen Legacy：Development/Excel/Monster.csv = Frozen Legacy Source（不再被读取；CSV → XLSX 同步已关闭）`；③ `--help` 头改 M2 并新增 Authority / Frozen Legacy 说明 |
| `Tools/RRPG.ConfigCompiler/migration/…M1ShadowSync/Program.cs` | `status` 输出重写为：`M1 SHADOW 状态（只读 · MIGRATION ONLY / M1 CLOSED）` + `当前 Authority` + `sync 已冻结` + `frozen legacy CSV`（原 `legacy Authority : True` 标签已消除） |
| `Tools/RRPG.ConfigCompiler/migration/…M1ShadowSync/ShadowWorkbookWriter.cs` | **仅类文档**：标注 M1 CLOSED / M2 起**不可达**（sync 冻结）+ 不得重新接上执行路径；说明其写出的 导航 Sheet 文案属 M1 时代产物（详见 §9.3 残余） |
| `Tools/RRPG.ConfigCompiler/README.md` | 标题 `（M0）`→`（M2）`；新增「M2 状态（Current Authority）」章节；原「M0 边界（本轮不做）」标注为**历史**且已被 M1/M2 取代 |
| `Development/Generated/StaticConfig/README.md` | 「M0 状态（本目录当前为空）」→「M2 状态」：列出 `Monster.json`（rows=3 / fields=8 / `sourceSha256=339f188a…`）+ XLSX Authority + CSV Frozen Legacy + M3 领域尚无 JSON 为合法状态 |

## 9.2 验证证据（本轮实测）

| 步骤 | 结果 |
|---|---|
| `dotnet build`（compiler） | **PASS — 0 warning / 0 error**（`BUILD_EXIT=0`） |
| `dotnet test`（compiler） | **PASS — 97/97**（`TEST_EXIT=0`；**无任何测试断言旧文案** → 未改测试逻辑） |
| `rrpg-config validate` | **PASS**（errors=0 / warnings=4 仍为 M3 域）× 输出已含：`M2 AUTHORITY：本目录的 XLSX = Current Monster Definition Authoring Authority…` / `Frozen Legacy：Development/Excel/Monster.csv = Frozen Legacy Source…`（`VALIDATE_EXIT=0`） |
| `rrpg-config`（help） | 显示 `RRPG ConfigCompiler（Production V0.1 · M2）` + Authority / Frozen Legacy 两行（`HELP_EXIT=2` = 无参数时的既有用法退出码，非回归） |
| `M1ShadowSync build` | **PASS — 0/0**（`SHADOW_BUILD_EXIT=0`） |
| `M1ShadowSync sync` | **REFUSED — exit=1**（文案不变，仍指向新 Authority） |
| `M1ShadowSync status` | **PASS**（`STATUS_EXIT=0`）→ `M1 CLOSED` / 当前 Authority = XLSX / sync 已冻结 / `frozen legacy CSV : True` |
| `M1ShadowSync equivalence` | **PASS**（`EQUIV_EXIT=0`）→ `3 × 8 逐条逐字段一致（CSV ↔ XLSX ↔ Mirror）`（M1 只读证据保留） |
| 漂移全仓扫描 | `Shadow Candidate` / `M1 SHADOW：` / `Current Monster Authoring Authority` / `M0 Production Toolchain Foundation` 在 `Tools/RRPG.ConfigCompiler/tests/**`、compiler `src`、工具 README、`Development/*/README.md`、根 `.cmd` → **0 命中** ✅ |
| Unity 侧 | **未触碰**（改动仅在 `Tools/**` 与 `Development/**` 文档；不在 Unity 工程内）→ 2D 的 Unity 证据（EditMode 730/730、PlayMode 1/1 + 3/3）继续有效，无需重跑 |

## 9.3 残余（明确记录，不在本 Slice 处理）

| # | 残余 | 原因 / 处置建议 |
|---|---|---|
| R-1 | `RRPG_Monsters.xlsx` 的 `00_导航` Sheet 文本仍是 M1 时代文案（由 M1 工具写入） | 该 Sheet 是**配置数据（XLSX 内容）** → 本 Slice 明令不得修改；建议在**下一次合法 XLSX 内容编辑**（或 M3 的 EnemyPresentation 工作簿落地时一并）由制作人更新导航文案。编译/校验不受影响（导航 Sheet 恒被跳过） |
| R-2 | `ShadowWorkbookWriter.cs` 中两处 导航标题字符串仍写 `M1 SHADOW / …` | 该写入路径自 M2 起**不可达**（`sync` 先拒绝）；已在类文档标注 M1 CLOSED 与禁止重新接上。字符串本身属“若复活会写出错误治理文案”的历史资产 → 随 **M1 工具退役（M4 之后）** 一并清理 |
| R-3 | M1ShadowSync 的状态标题仍含 `M1 SHADOW` 字样 | **刻意保留**：该工具本身即 M1 工具（`MIGRATION ONLY / M1 CLOSED`），标题描述的是**工具身份**而非 Authority；其 Authority 行已明确指向 XLSX |
