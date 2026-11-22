# RRPG M4 — Monster Legacy Physical Retirement Closeout Report V0.1

> 日期：2026-09-23（Asia/Shanghai）
> 阶段：静态配置迁移 **M4 = Monster Legacy Physical Retirement + Migration Closeout**（FDS `2026-09-22_RRPG_Static-Configuration-Migration-FDS-V0.1.md` §11.1 M4 行 / §10.5 / §9 Wave 1F）
> 开发基线：`migration/static-config-m1` = **`286555ca`**（M3 Repository Closed，见 M3 Closeout §11.8）
> 状态：**M4 Ready for Review（本地已提交、未 Push；本报告为交付与验收输入）**
> 编号澄清：本报告的「M4」= 静态配置迁移 M4；与 Person/Profession 系列的「M4 Growth Resolver Alignment」（已完成，测试目录 `ProfessionM4`）无关。

---

## 1. 目标、判据与范围（规划原文对齐）

| 项 | 内容 |
|---|---|
| 目标 | 把 M2 逻辑退休的 Monster Legacy 链**物理清理**：旧 Authoring 入口 / 旧资产 / 旧类型 / 旧回退链；**无 Authority 变化** |
| 完成判据 | **引用 = 0**（Runtime / Editor / Tests）+ **资产 = 0** + **Compile PASS** + **Tests PASS**（§10.5、§11.1、§19.2 Q5） |
| 明确不在范围 | 不删 `DataManager` / `ExcelImporter` **整体**（只删 Monster 路径，FDS §18 N2/N3）；不动其它领域（Character / Skill / Status / ItemCard / Person / Profession）；不改 Gameplay、Save、SaveVersion；不做正式美术（M3-R1）、UI 版式（M3-R8） |
| 删除顺序（§10.5） | `Monster.csv` → Monster SO → `ExcelImporter` Monster 分支 → `DataManager` Monster 路径 → `GameDataInitializer` 残余 → Demo 残余 → **最后** `MonsterData` 类型 |

## 2. 本次任务级裁决（用户，2026-09-23）与落实位置

| # | 裁决 | 落实 |
|---|---|---|
| Q1 | 删除 `enemyBodyPlaceholder` 字段与 Scene 条目；M3 Resolver 内 fallback 为当前规则；更新 FDS 过期表述并记录被 M3 决策取代的原因 | §5 实施 4；FDS V0.3（§0.4 注 / §9 Wave 1D / §10.3 / §19.2 Q3 / 变更记录）|
| Q2 | Root 缺失 → Registry 明确失败 + 可操作 diagnostic；在**现有入口**阻止本次战斗初始化；不得回退旧 Monster、静默 0 敌人或半初始化；恢复 Root 后验证正常路径 | §4、§5 实施 1；测试 `R2` / `R2b` / `M4_1` |
| Q3 | `M1ShadowSync` 冻结工具**保留**，不纳入本次物理删除 | §9 遗留 2（未改动该工具）|
| Q4 | 三份 `StaticMonsterTestData` 保留；`M2DisposableGateRunner` 仅在长期回归能力另有覆盖时退役；清空且无引用的旧 Monster 资产目录与 `.meta` 随旧资产清理 | §9 遗留 3/4；旧资产目录与 `.meta` 已随资产删除 |
| Q5 | Scene 序列化修改必须走 Unity Editor / MCP；未授权直接编辑 YAML；通道不可用则报告阻塞 | §3 环境与通道（Unity batchmode `-executeMethod`，Editor 序列化 API）|
| Q6 | M4-6 PlayMode 挂起已在 M3 关闭 → 只校正文档状态 + 正常全量 PlayMode 回归，不重复开发 | §8 文档同步；M2 报告 §7.2 状态校正 |

**实施顺序（按裁决调整，避免中间断链）**：① Root 缺失明确失败路径 + 退役 `LegacyMonsterFallbackAdapter` 及相应测试 → ② 清理依赖 `MonsterData` 的 Editor/Runtime 入口与旧资产 → ③ 确认 Runtime/Editor/Tests 引用与旧资产**归零**后**最后**删 `MonsterData` 类型 → ④ `enemyBodyPlaceholder` 的代码/测试/Scene 按依赖顺序清理。

## 3. 环境与通道（未直改 YAML）

| 项 | 事实 |
|---|---|
| 编辑器 | `D:\BBB\Tuanjie\Hub\Editor\2022.3.62t9\Editor\Tuanjie.exe`（`RRPG_TUANJIE_EXE` 未设、`Producer/editor-path.local.txt` 不存在 → 使用本机登记默认路径） |
| 测试通道 | `-batchmode -executeMethod M2DisposableGateRunner.Run`（本项目 `-runTests … -quit` 无效，M2 已记录）；EditMode / PlayMode **串行**执行 |
| Scene 修改通道 | 一次性 Disposable runner `M4SceneCleanupRunner.ClearLegacyEnemyPlaceholder`：`EditorSceneManager.OpenScene` → `MarkSceneDirty` → `SaveScene`（**Unity Editor 序列化 API，未直接编辑 YAML**）；回读 `containsBefore=True containsAfter=False saved=True`，执行后该文件删除 |
| 一次性异常（如实记录） | 首次导入时出现 1 条 `error CS2001: Source file '…/LegacyMonsterFallbackAdapter.cs' could not be found.` —— 属 `Library/Bee` 增量构建缓存引用了已删除文件的**瞬时**错误，随后编译成功（`CompileScripts` 通过、测试正常执行），未影响任何结果 |


## 4. 调用链与「既有入口阻止半初始化」（未新增流程）

```text
ExpeditionUI.OnConfirm
  → StaticConfigRegistry.Monsters                                  （既有入口；M2 Slice 2A C1）
      ├─ Root 不可用 → null + 可操作诊断
      └─ ExpeditionUI:487-494 → Debug.LogError(诊断) + Log(提示) + return   ← 拒绝出征（尚未发生确认状态变更）
  → new AdventureManager(...)（:516）→ adventure.GetEnemyMonsters()（:621）
  → battleUI.InitFromExpedition(...)（:624）→ battleUI.StartBattleWithExpedition()（:637）
  → BattleUI.OnStartBattle()                                        ← 既有开战入口
      ├─【M4 新增】敌方定义不可用（null / 空）→ Warning + UI 提示 + return
      │     不置 _battleStarted、不隐藏准备 UI、不创建 BattleSystem → 不出现 0 敌人 / 半初始化战斗
      └─ 既有阵型预检（TryValidatePendingFormation 失败 = 同一拒绝语义）
  → BattleUI.ExecuteBattle()                                        ← 【M4 新增】防御纵深 guard（同一拒绝语义 + _battleStarted=false）
  → BattleSystem.InitializeBattle(_selectedChars, _enemyMonsters, ...)
```

* **未新增产品流程、未改玩法**：拒绝语义复用既有开战边界（`_battleStarted` 不置位 + 保持战前准备）。
* 日志级别与**既有阵型预检**保持一致（Warning）；**「明确失败」由 Registry 承担**：
  `StaticConfigRegistry.TryGetMonsters` 返回 false + `MissingRootDiagnostic` / `MissingMonsterDatabaseDiagnostic`
  + `LegacyFallbackRetiredNote`（含 `RRPG_一键导表.cmd` 修复指引）；产品链上由 `ExpeditionUI` 以 **Error** 输出并拒绝出征。
* `InitFromExpedition` 收到 `null` 敌方定义时不再抛异常（改为空列表 + Warning），避免「半初始化 / 崩溃」二选一。
* 实施中发现的既有契约事实（如实记录）：`BattleFormationIntegrityTests` 的开战边界用例以**裸 BattleUI**（无敌方定义）验证阵型拒绝；
  首轮实现把 authority 预检放在阵型预检之前且用 **Error** 级别，导致这 2 项用例因「未预期的日志」失败 → 修正为**与阵型预检同级（Warning）**后全部通过（**未修改**这两个既有用例，未改变产品流程）。

## 5. 实施内容（按裁决顺序，逐项可回滚）

**Step 1 — Root 缺失明确失败 + 退役 Legacy 回退链**

| 文件 | 改动 |
|---|---|
| `Assets/Scripts/StaticConfig/StaticConfigRegistry.cs` | 删除 `UsingLegacyFallback` 属性与 `_usingLegacyFallback`；新增 `LegacyFallbackRetiredNote`；`ResolveMonstersOnce` 在 Root / `MonsterConfigDatabase` 不可用时**明确失败**（`_monsters = null` + 诊断，不再回退）；`Reset()` 不再清 Adapter 状态；类注释更新为 M4 语义 |
| `Assets/Scripts/StaticConfig/LegacyMonsterFallbackAdapter.cs(+.meta)` | **删除**（D-M2-1 A6 条件已满足） |
| `Assets/Scripts/UI/BattleUI.cs` | `InitFromExpedition` null 安全；`OnStartBattle` 加入 authority 预检；`ExecuteBattle` 加入防御 guard |
| `Assets/Tests/EditMode/StaticConfig/MonsterRegistryEntryTests.cs` | R2 重写为「明确失败 + 可操作诊断 + 幂等」；新增 **R2b**（Root 恢复后正常路径自愈）；R1/R3 去掉回退状态断言；R4 保留「StaticConfig 层之外 0 命中」并加入 M4 说明 |

**Step 2 — 清理依赖 `MonsterData` 的 Editor / Runtime 入口与旧资产**

| 文件 / 资产 | 改动 |
|---|---|
| `Assets/Editor/ExcelImporter.cs` | 删除 `ImportMonsters` 方法体与 `M2 FROZEN LEGACY` 注释块（F3；其余 6 领域导入不变） |
| `Assets/Editor/GameDataInitializer.cs` | 删除 Monster seed 注释残留（F5；其余初始化不变） |
| `Assets/Editor/DataValidationTool.cs` | 删除 Monster `Resources.LoadAll` + 空 ID / 重复 ID 两处校验（M4-2） |
| `Assets/Scripts/Core/DataManager.cs` | 删除 `AllMonsters` / `_monsterMap` / Monster SO `LoadAll` / `WarnDuplicateIds(Monster)` / 字典构建 / `GetMonsterById`（F4；`DataManager` 本体与其它领域保留） |
| `Development/Excel/Monster.csv` | **删除**（F1） |
| `Assets/Resources/ScriptableObjects/Monsters/`（3 SO + `.meta` + 目录 `.meta`） | **删除**（F2；目录清空且无引用，裁决 Q4 允许一并删除 `.meta`） |

**Step 3 — 引用归零后最后删除 `MonsterData` 类型**

| 文件 / 类型 | 改动 |
|---|---|
| `Assets/ScriptableObjects/Monsters/MonsterData.cs(+.meta)`、目录 `.meta` | **删除**（F7 / §10.5 第 7 步；删除前实测 Runtime / Editor 引用 = 0） |
| `MonsterDefinition.cs` / `BattleUnit.cs` / `BattleSystem.cs` / `RRPGStaticConfigRoot.cs` / `GameDataInitializer.cs` | 陈旧注释更正（不再声称「Current 链仍是 CSV → MonsterData SO → DataManager」） |

**Step 4 — `enemyBodyPlaceholder`（M3-R2）按依赖顺序清理**

| 顺序 | 改动 |
|---|---|
| ① 测试 | `EnemyPresentationBattleWiringTests.W2` 改为「运行时代码 0 命中 + 字段不存在」（原为「只允许 1 处声明」）；`BattleVisualFoundationTests` 字段清单移除该项；`BattleVisualFoundationPlayTests` 排他断言改为结构性说明（敌我各自 Resolver + 字段已删） |
| ② 代码 | `BattleUIUnitVisualHosts.cs` 删除字段与 Tooltip；相关注释更正 |
| ③ Scene | 一次性 runner（Unity Editor API）清除 `SampleScene.scene` 序列化条目 → 回读 `containsAfter=False` |

**Step 5 — 迁移证据 / 契约测试**

| 文件 | 改动 |
|---|---|
| `Assets/Tests/EditMode/StaticConfig/MonsterShadowTests.cs` | E1/E2（Legacy 逐字段等价、枚举顺序对照）**退役** → 证据改由 Git 历史（M2 Audit §5 R9）；H Gate 翻转为「物理删除 + Runtime 引用 = 0」；`F_FreshRebuild` 改为断言 Authority 侧事实；删除 Legacy 反射 helper |
| `Assets/Tests/EditMode/StaticConfig/MonsterLegacyRetirementTests.cs` | **新增**（M4 关闭契约）：`M4_1` 开战入口拒绝半初始化；`M4_2` 字段 + Scene 条目已删除；`M4_3` 资产 / 类型 / `DataManager.AllMonsters` 已删除 + 退役诊断常量存在 |

## 6. 验证证据（逐项，本轮实测）

| # | 验收项 | 方法 / 用例 | 结果 |
|---|---|---|---|
| V1 | 编译 | Unity 导入 + `CompileScripts`（测试运行内） | **PASS**（0 error CS；首轮 1 条瞬时 `CS2001` 见 §3） |
| V2 | dotnet（Compiler / xUnit） | `dotnet test … -c Release -v q` | **108 / 108 PASS**（失败 0，1 s） |
| V3 | **EditMode 全量** | `-executeMethod M2DisposableGateRunner.Run -m2TestMode EditMode`（filter=all） | **754 / 754 PASS**（0 failed / 0 skipped，16.2 s，`testCaseCount=754`） |
| V4 | **PlayMode 无过滤全量** | 同上 `-m2TestMode PlayMode`（filter=all） | **71 / 71 PASS**（0 failed / 0 skipped，34.8 s，`testCaseCount=71`） |
| V5 | 缺 Root → 明确失败 | `MonsterRegistryEntryTests.R2_MissingRoot_FailsExplicitly_WithActionableDiagnostic` | **PASS**（`TryGetMonsters=false`、`registry=null`、`Monsters=null`、诊断含 `RRPG_一键导表.cmd` 与「退役」、重复读取幂等） |
| V6 | Root 恢复 / Fresh Rebuild | `MonsterRegistryEntryTests.R2b_AfterRootRestored_NormalPathRecovers` + `MonsterShadowTests.F_FreshRebuild_RegistryStillHasThree`（`RebuildFromZero()`） | **PASS**（重建后 3 条、诊断为空、8 字段契约与名单一致） |
| V7 | 既有开战入口拒绝半初始化 | `MonsterLegacyRetirementTests.M4_1_BattleUI_RefusesStart_WhenEnemyAuthorityUnavailable` | **PASS**（`IsBattleStarted=false` + 预期诊断输出） |
| V8 | Prep / Battle 三怪三图 + fallback（真实产品链） | `BattleVisualFoundationPlayTests.EnemyPresentation_PrepAndBattle_ResolveFromAuthority`（Prep 敌方格与 Battle 单位卡同 Resolver、一怪一图、不得退化为 fallback）+ `ProductBattle_VisualHosts_RefreshFromRuntimeFacts` + EditMode `R4/R5/R6/R9`（fallback 只在 Resolver + WarnOnce） | **PASS**（两条 PlayMode 用例各 0.4 s；fallback 计数与 WarnOnce 断言通过） |
| V9 | 字段 + Scene 序列化条目 | `MonsterLegacyRetirementTests.M4_2_…` + Scene runner 回读 + `git grep` | **PASS**（字段不存在；`containsBefore=True → containsAfter=False saved=True`；Scene / 运行时代码 0 命中） |
| V10 | Legacy 资产 / 类型 / 入口归零 | `MonsterLegacyRetirementTests.M4_3_…` + `MonsterShadowTests.H_…`（E 段 Runtime 引用 = 0 / F 段资产与类型不存在）+ `git grep` | **PASS**（`Monster.csv`、Monster SO 目录、`MonsterData` 类型、`DataManager.AllMonsters` / `GetMonsterById`、`LegacyMonsterFallbackAdapter` 全部不存在；Runtime/Editor 的 `MonsterData` 引用 = 0） |
| V11 | 与 M3 基线对照 | §7 | **逐项给出（无回退）** |

**未做的验证（如实声明）**：本轮**未**重跑「非 batchmode 真实 Game View 整屏截图」（需有显示器的交互会话，属 M3-R9 环境事实）；
M4 对视觉的验证以 V8 的真实产品链断言（Prep / Battle 同一 Resolver、一怪一图、fallback 排他）为准，与 M3 的截图口径不混淆。

**V2–V4 的执行时点**：均为**最终交付树**上的结果 —— 即一次性 `M4SceneCleanupRunner` 已删除、测试注释修正落盘之后的复跑
（EditMode `durationSeconds=16.2 / wallSeconds=18.2`；PlayMode `durationSeconds=34.8 / wallSeconds=46.2`；两次 `filter=<all>`、`failed=0 / skipped=0 / inconclusive=0`）。

## 7. 与 M3 基线的差异（逐项）

| 项 | M3 基线（`286555ca`） | M4（本次） | 差异性质 |
|---|---|---|---|
| 编译 / dotnet | 0 error CS；108/108 | 0 error CS；108/108 | 无变化 |
| EditMode 全量 | **752/752** | **754/754** | −2（E1/E2 迁移证据退役）+1（R2b）+3（M4 契约）= **+2** |
| PlayMode 无过滤全量 | **71/71**（35.6 / 35.7 s） | **71/71**（34.8 s） | 用例数不变、全绿 |
| Runtime `MonsterData` 引用 | 允许 2 处（`DataManager` + 回退适配） | **0** | M4 判据达成 |
| Legacy 资产 | `Monster.csv` + 3 SO + `MonsterData.cs`（Frozen 保留） | **全部删除**（含 `.meta` 与空目录） | F1/F2/F7 |
| Root 缺失行为 | Legacy 只读回退（3 条 + WarnOnce） | **明确失败**（false + 可操作诊断）；既有入口拒绝出征 / 开战 | D-M2-1 A6 关闭 |
| `enemyBodyPlaceholder` | 字段保留（零读取）+ Scene **悬空引用** | **字段与 Scene 条目删除** | M3-R2 关闭 |
| Gameplay / Save / SaveVersion / 其它领域 | 不变 | **不变** | Hard Stop 未触发；`DataManager` / `ExcelImporter` 本体与 6 个其它领域导入/校验保留 |

## 8. 文档同步与提交方案

**已同步文档**

| 文档 | 内容 |
|---|---|
| `Docs/DevelopmentSpecs/2026-09-22_RRPG_Static-Configuration-Migration-FDS-V0.1.md` | **V0.3**：§0.4 追加 M4 更新注；§9 Wave 1D fallback 表述更替（Resolver 内 fallback）+ 字段物理删除；§10.3 Cutover 结果行同步；§19.2 Q3 记录**原裁决被 M3 实现取代的原因**；§19.3 变更记录新增 V0.3 行 |
| `Docs/DevelopmentSpecs/2026-09-23_RRPG_M2-Monster-Definition-Authority-Cutover-Closeout-Report-V0.1.md` | §7.2：`M4-6` 行标注**已关闭、不在 M4 待办、不重复开发**；新增「M4 实施结果」行指向本报告 |
| 本报告 | M4 Closeout（目标 / 裁决 / 实施 / 调用链 / 验证 / 差异 / 提交 / 遗留） |
| `Docs/项目状态快照.md` / `Docs/项目日志.md` / `Docs/开发验收交接.md` / `Docs/版本更新记录.md` | M4 事实、M3-R2 关闭、回退链退役与失败语义、验证数字 |
| `Development/Config/README.md` / `Tools/RRPG.ConfigCompiler/README.md` | 生产者与工具面文档更新为 M4 事实（不再声称「保留给 M4」/「字段仅剩声明」） |

**提交方案（本地提交，未 Push）**

| # | 建议 commit message | 范围 |
|---|---|---|
| C1 | `refactor(config): physically retire monster legacy chain (M4)` —— **实测 `7fe3154`**（31 files / +330 −824） | Step 1–5 全部代码 / 资产 / Scene / 测试（`StaticConfigRegistry`、删除回退适配器、`BattleUI` 入口、Editor/Runtime 入口清理、`Monster.csv` + 3 SO + `MonsterData` 类型删除、`enemyBodyPlaceholder` 字段与 Scene 条目、`MonsterShadowTests` / `W2` / PlayMode 测试更新、新增 `MonsterLegacyRetirementTests`） |
| C2 | `docs(config): record M4 monster legacy physical retirement closeout` —— **实测 `a846f03`**（9 files / +322 −59） | 本报告 + FDS V0.3 + M2 报告状态校正 + 快照 / 日志 / 交接 / 版本记录 + 两份 README |

> 如需更细粒度（例如把 Scene 变更或测试退役拆成独立 commit 便于单独回滚），可在授权后按文件白名单重排；
> 当前方案保证 **C1 可单独回滚**（回到 M3 的 Legacy 保留状态），C2 为纯文档。

## 9. 遗留与后续（诚实清单）

1. **M4-6（PlayMode 挂起）**：M3 已关闭；本次仅**文档状态校正** + 正常全量 PlayMode 回归（71/71），**未重复开发**。
2. **`M1ShadowSync`**：按裁决**保留**（冻结的迁移证据工具；`sync` 仍非 0 拒绝执行，只读 `equivalence` / `status` 可用）。其退役不属于 M4。
3. **`StaticMonsterTestData` ×3**（PlayMode 根 / `ExpeditionV4F` / `VillageV4`）：按裁决**保留**（被多个 PlayMode 产品链测试使用）。
4. **`M2DisposableGateRunner`**：**保留** —— 本环境内置 `-runTests … -quit` 无效（M2 §6.3 记录），该 runner 是当前唯一可用的 headless EditMode / PlayMode 执行通道；长期回归能力**没有**替代覆盖，故不满足「仅在已有覆盖时退役」的条件。
5. **M3 遗留不变**：M3-R1（临时素材非最终美术）、M3-R3（仅一种 ImportPreset）、M3-R7（无 per-Monster scale/offset/pivot）、M3-R8（Prep 敌方列文字重叠，留 UI 版式工作）、M3-R9（真实 Game View 截图需 GUI 会话）、M3-R10（`.build-state` 日志一次性）。
6. **数据事实**：M4 只删「无人引用的 Legacy」，**未新增任何内容**；Monster 定义与敌方表现的内容变更仍分别只改 `RRPG_Monsters.xlsx` / `RRPG_EnemyPresentation.xlsx` + `SourceAssets/Enemy/*.png`。

## 10. 环境事实与阻塞声明

* **无阻塞**：Scene 修改经 Unity Editor（`-executeMethod` + `EditorSceneManager`），**未直接编辑任何 YAML**（满足裁决 Q5）。
* 环境一次性事实：`RRPG_TUANJIE_EXE` 未设置、`Producer/editor-path.local.txt` 不存在 → 使用本机登记默认编辑器路径；
  首轮导入出现 1 条由 `Library/Bee` 增量缓存引起的瞬时 `CS2001`（随后编译成功、测试全绿）。
* 环境噪声（M2 已登记）：`com.ivanmurzak.unity.mcp` 在批处理下的 MCP 连接告警（`Test run finished, but the pending MCP request id was missing`），与测试结果无关。
* 未做（环境依赖）：非 batchmode 真实 Game View 整屏截图（需有显示器的交互会话）；如需该证据，请在 GUI 会话复跑一次 `BattleVisualFoundationPlayTests`。
* 实施中的一次修正（如实记录）：authority 预检最初使用 **Error** 且置于阵型预检之前，导致 `BattleFormationIntegrityTests`
  两条既有开战边界用例因「未预期的日志」失败 → 改为**与既有阵型预检同级（Warning）**并通过；**未修改**那两个既有用例、未改动产品流程。

## 11. Git 与工作区（交付时状态）

* 分支：`migration/static-config-m1`；开发基线 `286555ca`（M3 Repository Closed）。
* 写入面：**仅** M4 白名单（StaticConfig Runtime / BattleUI / 3 个 Editor 工具 / `DataManager` / 5 个测试文件 / `SampleScene.scene` / 4 份 Docs / 2 份 README / 本报告 + 新增 1 个测试文件）；**未触碰** `main`、GameSpec、决策记录、Save、Packages、ProjectSettings、其它领域资产。
* 一次性资产已清理：`M4SceneCleanupRunner.cs(+.meta)` 已删除（Disposable）；`.build-state/**` 为 gitignored 证据现场。
* 本地提交：**`7fe3154`（C1）+ `a846f03`（C2）+ `fbab8c9`（C3，哈希回填）**；提交后 `git status --porcelain -uall` 为空。
* 状态：交付时为 **M4 Ready for Review（本地已提交、未 Push；Push 未授权）** —— **Push 完成后的实际状态见 §11.8**。

### 11.8 Repository 关闭事实（Push 完成 + 远端核对）

**2026-09-23 同日补记（Push 成功后）**：M4 的三次提交已 Push 到 `origin/migration/static-config-m1`，M4 状态从
「Ready for Review（本地已提交、未 Push）」转为 **ACCEPTED / REPOSITORY CLOSED**。§6 的验证证据、§7 的与 M3 差异、
§9 的遗留归属**均不变**；§11 与 §12、Handoff v39 中的「Push 未授权、未执行」是 Push 前的真实历史状态，保留不重写。

| 项 | 实测事实 |
|---|---|
| Push 形态 | fast-forward（无 force、无 rebase、无 amend / reset）：`286555ca → 7fe3154 → a846f03 → fbab8c9`（`git push` 输出 `286555c..fbab8c9`） |
| Push 内容 | C1 `7fe3154`（31 files / +330 −824）+ C2 `a846f03`（9 files / +322 −59）+ C3 `fbab8c9`（2 files / +4 −3） |
| 分支 | `migration/static-config-m1` → 远端 `origin/migration/static-config-m1` |
| 推送前预检（实测） | `git ls-remote origin refs/heads/migration/static-config-m1` = `286555ca21c92030e17973e63b2bad4b09883f0d` == M3 基线 `286555ca`；`git fetch origin --prune` RC=0；`git status --porcelain -uall` 为空；`286555ca` 是 HEAD 祖先且远端是 HEAD 祖先 → **FF 可行**才执行 push |
| 远端核对（实测） | `git ls-remote origin refs/heads/migration/static-config-m1` = `fbab8c984858d4a9d0c34de39007118b3f677724` == 本地 HEAD |
| 跟踪引用 | `git fetch` 后 `origin/migration/static-config-m1` = `fbab8c9`；`git rev-list --left-right --count origin/migration/static-config-m1...HEAD` = `0 0` |
| 工作区 | `git status --porcelain -uall` 为空（tracked clean、无 untracked 残留） |
| 未触碰 | `main` / `origin/main = 57edf6f` 未变；本收口提交为 **docs-only**（未改代码 / 资产 / Scene / 生成物 / `.gitignore`）；**M5 未启动** |

> 交付口径：M4 = **ACCEPTED / REPOSITORY CLOSED**（技术验证数字与冻结事实见 §6 / §7；遗留项按 §9 归属 —— `M1ShadowSync` /
> `StaticMonsterTestData` ×3 / `M2DisposableGateRunner` 按裁决保留，M3-R1 / R3 / R7 / R8 / R9 / R10 不变）。

## 12. 结论

**M4 技术收口成立**：Legacy Monster 链已按 FDS §10.5 顺序**物理清理完毕**（引用 = 0 / 资产 = 0 / Compile PASS / Tests PASS），
Root 缺失改为**明确失败 + 可操作诊断**且由既有入口阻止半初始化战斗，`enemyBodyPlaceholder`（M3-R2）已删除；
dotnet **108/108**、EditMode **754/754**、无过滤 PlayMode **71/71** 全绿，Gameplay / Save / SaveVersion 未变。
按裁决保留 `M1ShadowSync`、三份 `StaticMonsterTestData` 与 `M2DisposableGateRunner`；**Push 未授权，等待 Review。**
