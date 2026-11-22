# WP-Encounter-1 Enemy Encounter Generation Foundation V0.1 — Final Closeout Report

> 类型：Closeout Report（任务证据与最终收口）
> 状态：**Final Closeout Ready for Commit Authorization**（Technical Verified；**Producer Play Acceptance PASS（2026-09-25）**；本轮不 Commit / 不 Push）
> Target Branch：`main`
> 生成时间：2026-09-25（Asia/Shanghai）
> 读法：本报告记录 WP-Encounter-1（1A → 1C-R）的最终范围、产品决定、临时边界、验证证据与文件分类。它不是长期玩家规则 Authority：规则与设计方向见 [Docs/决策记录.md](../../Docs/决策记录.md) D-020 与 [GameDesign/当前设计状态.md](../../GameDesign/当前设计状态.md) §二 / §十；当前内容操作入口见 [Development/Config README](../../Development/Config/README.md)。报告中较早阶段的“待验收 / 待接产品”口径属于当时截面，不等于当前状态。

## 1. WP 组成与最终状态

| 阶段 | 交付内容 | 最终状态 |
|---|---|---|
| 1A Static Config Foundation | Encounter 工作簿（`Encounter` / `EncounterSlot` / `EncounterSlotRule` / `EncounterPool` / `EncounterPoolEntry`）+ Monster 工作簿两张子表（`MonsterEncounterProfile` / `MonsterRole`）+ Compiler / Validator / Canonical Mirror / Unity Builder / 只读 Registry 入口 | 完成 |
| 1A-R Regression Baseline Repair | 长期回归测试不再写死「3 只怪 / `mon_003_wolf`」，改为从 Canonical Mirror 派生当前 Authority 集合（`StaticConfigTestAuthority`）；内容正常增长不再被误判为回归 | 完成 |
| 1B Generator Foundation | `Encounter.Runtime`（`noEngineReferences = true`）：`PoolId + Depth + Seed`（或 `EncounterId + Seed`）→ 合法 `EnemyRoster`（确定性随机、条件筛选、数量组合、威胁预算、3×3 站位求解、`EnemyInstanceId`、明确失败） | 完成 |
| 1C Runtime Cutover | 产品 Battle / Prep 切换到 `Active EnemyRoster`：Session Seed / Encounter Seed、Prep 与 Battle 共用同一份 Roster、`EnemyInstanceId` 与 `MonsterId` 身份分离、1～9 只全部部署、不再用整个 Monster Registry 决定本场阵容、失败不 fallback | 完成 |
| 1C-R Entry Atomicity / Pool Coverage Repair | Battle 类节点进入原子性（预检 → 预生成 → Enter → 提交）；失败零 Progression / Supply / elapsed mutation 且可原地重试；`pool_test_deep` 在 depth=1 具备兼容覆盖（`MinDepth` 2 → 1） | 完成 |
| Final Closeout（本报告） | 仓库级范围审计、Legacy / Temporary 边界审计、文档收口、最终验证、文件分类与提交建议 | 本报告 |

## 2. 最终实现范围（Scope Audit 结论）

### 2.1 Static Config

- Encounter Workbook：`Development/Config/RRPG_Encounters.xlsx` = **Current Encounter Authoring Authority**（5 张数据表 + `00_导航`）。
- Encounter Slot / Rule / Pool：组合需求位（敌人数量的唯一来源）、筛选规则（`Require` / `AnyOf` / `Exclude` × `Role` / `Tier` / `MonsterId`）、遭遇池与池条目（`Weight` / `MinDepth` / `MaxDepth`）。
- Monster Encounter Profile / Role：写在 `RRPG_Monsters.xlsx` 的两张 Encounter 子表（一怪一行画像；战斗定位可一怪多行）。
- Compiler / Validator / Mirror / Builder / Registry：`EncounterValidator` + 领域诊断码 + Canonical Mirror（7 个 JSON）+ `StaticConfigBuilder`（`EncounterConfigDatabase`）+ `StaticConfigRegistry.TryGetEncounterConfig / EncounterConfig / EncounterConfigDiagnostic`（只读、幂等、明确诊断）。

### 2.2 Generator（`Assets/Scripts/Encounter/`）

- Pool + Depth + Seed → `EnemyRoster`（确定性：同 Seed 同配置 → 同阵容）。
- Fixed / Query 两种选怪方式；`FixedMonster` 仍必须通过所在位的规则。
- `AnyOf` / `Require` / `Exclude` 三种规则方式（同维度 OR / AND、Exclude 优先）。
- Duplicate：允许 / 禁止同种怪重复；禁止而候选不足时**明确失败**。
- Threat Budget：威胁预算区间参与求解；无解 → 明确失败（不是静默放宽）。
- `Any` / `Row` / `Exact` 三种站位模式（`Exact` 必须 Row + Column 且数量 1）。
- 1 / 2 / 3 人阵型的排内列规则（1 居中、2 左右、3 左中右）。
- 敌方战场最大 9 只（3 排 × 3 列）。
- `EnemyInstanceId`：本场实例身份，唯一且不等于任何 `MonsterId`。
- 明确失败：缺池 / 该深度无候选 / 不可解 / 搜索上限 → 显式失败码与诊断，不产生部分阵容。

### 2.3 Runtime Cutover

- Active Encounter：`AdventureEncounterRoster.cs` 是本次远征「当前 Battle 类 Node 敌方阵容」的唯一 Authority。
- Session Seed / Encounter Seed：会话内稳定；同一 Node 不重抽（Prep 重建 / Battle 开始 / UI 重建均复用）。
- Prep / Battle 共用同一份 Roster 对象：`BattleUI.BindActiveEnemyRoster` → Prep 卡 → `BattleSystem.InitializeBattleFromEnemyRoster`。
- `MonsterId`（模板：名称 / 数值 / 美术 / AI）与 `EnemyInstanceId`（本场：BattleUnit 身份 / HP / 状态 / Intent / UI 卡名）分离，UI 卡名统一用本场身份。
- 1～9 只**全部**创建并合法部署（不再只部署前 3 只），入场失败视为完整性错误并拒绝开战。
- 不再使用整个 Monster Registry 决定本场「有几只怪」（目录只提供模板信息）。
- Encounter 生成失败不 fallback：不退回整套 Monster Registry、不生成默认 3 怪、不自动 Resolve、不出现空战斗或半初始化战斗。

### 2.4 1C-R（进入原子性）

- 顺序冻结：`ValidateEnterNode`（纯查询）→ `ProjectedDepth = EnteredNodeCount − 1` → 预生成 Encounter → `TryEnterNode`（唯一 mutation）→ 提交 `ActiveEncounter`（同一份 Roster）。
- 生成失败 ⇒ 零 mutation：节点不进入、`CurrentNode` / Phase 不变、Supply / elapsed 不变、无半成品 `ActiveEncounter`、BattleUI 未绑定阵容；玩家可原地重选或重试（不需要 Retreat）。
- `pool_test_deep` + `enc_test_exact_front` 的 `MinDepth` 由 2 → 1（Elite 可在 depth=1 出现，原窗口在该深度无候选）；未改 Generator 规则、未加静默 fallback、未改怪物数值与池名称。

## 3. 产品决定（本 WP 冻结了什么）

以下内容由 [Docs/决策记录.md](../../Docs/决策记录.md) D-020 记录，并在 [GameDesign/当前设计状态.md](../../GameDesign/当前设计状态.md) §二 / §十 归位：

- **生成方式**：普通 Battle 不再按关卡逐场固定手配敌人，也不从全 Monster Pool 无结构独立随机；Current = `Encounter Template + Composition Slot + Monster Classification + Constraint / Threat → Generated EnemyRoster`，并保留**固定怪 + 条件随机怪**混合配置能力。
- **身份分离**：`EnemyInstanceId` ≠ `MonsterId`；本场事实（数量 / 站位 / HP / 状态 / UI 卡名）用实例身份，模板事实（名称 / 数值 / 美术 / AI 规则）用模板身份。
- **空间与上限**：敌方 Board 最多 9 只；站位模式 `Any` / `Row` / `Exact`；排内 1 人居中 / 2 人左右 / 3 人左中右。
- **重复语义**：同种怪是否允许重复由遭遇控制（不是全局隐式规则）。
- **失败语义**：遭遇生成失败 = 明确失败（不 fallback、不默认 3 怪）。
- **进入原子性**：Battle 类节点「先校验并预生成成功、后才进入」；失败零 mutation。

## 4. Temporary / Compatibility / Not Frozen（不得写成正式长期架构）

| 项目 | 当前口径 | 说明 |
|---|---|---|
| `pool_test_general` | **Test / Not Frozen** | V0.1 兼容用测试池；不是正式普通战斗池 |
| `pool_test_deep` | **Test / Not Frozen** | V0.1 兼容用测试池；不是正式精英 / Boss 池 |
| Normal → `pool_test_general`、Elite / Boss → `pool_test_deep` | **V0.1 Compatibility 映射** | Node → EncounterPool 的正式关系（Biome / Region / 内容表）**未建立**；Selector 是兼容接缝（诊断用 override 见 `EncounterPoolOverrideForDiagnostics`） |
| `EncounterDepth = EnteredNodeCount − 1` | **V0.1 当前兼容语义** | 深度 = 已进入 Node 数（不是「战斗场数」）；无限远征正式 Depth 模型 **Not Frozen** |
| 当前威胁值 / 权重 / 深度区间 / 测试遭遇 / 测试 Boss 内容 | **Test / Not Frozen** | 只用于证明链路与规则；不是正式内容与平衡 |
| 当前 Session Seed / Encounter Seed 生命周期 | **V0.1 当前实现** | 会话内稳定、同 Node 不重抽；跨会话 / 持久化策略未冻结 |
| Legacy Map → ExpeditionGraph 适配（`LegacyMapGraphAdapter`） | **Compatibility Graph Source** | 第一版后台 Progression 来源；不是正式地图 / 无限远征架构 |

## 5. 本轮最终验证（2026-09-25，Final Closeout 真实执行）

环境：Windows；Tuanjie Editor 1.9.1（`2022.3.62t9`）batchmode；仓库根 `D:\BBB\RRPG1.0W`；Target Branch `main`。

| 验证项 | 入口 / 命令 | 本轮结果 |
|---|---|---|
| Config Compiler 单测 | `dotnet test Tools/RRPG.ConfigCompiler/tests/RRPG.ConfigCompiler.Tests/RRPG.ConfigCompiler.Tests.csproj` | **139 / 139 通过，失败 0** |
| Config 编译（Authoring → Canonical Mirror） | `RRPG_一键导表.cmd` 步骤 1/3（内部 `rrpg-config build`） | **PASS，errors=0 / warnings=3**（3 个 `WB004_SHEET_SKIPPED` 均为 `00_导航` 非数据页）；全部 Mirror 文件 `unchanged` → Authoring 与 Mirror 一致 |
| Unity Builder（生成根） | `RRPG_一键导表.cmd` 步骤 2/3（`StaticConfigBuilder.ExecuteBuild`） | **PASS**（`builder.stage.txt`：`result=PASS`、`noOp=true`）；`action=encounter: encounters=4 slots=9 rules=8 pools=2 entries=4 profiles=6 roles=9`、`monster.json: rows=6 fields=8`、`enemy presentation: bindings=6`、`visual assets: entries=6`；`unchanged: content-signature 命中，生成根与当前 Mirror 一致（不触碰任何产物）` |
| Config 校验（只读） | `RRPG_一键导表.cmd` 步骤 3/3 与 `RRPG_检查配置.cmd` 等价入口（`rrpg-config validate`） | **PASS，errors=0 / warnings=3**；`Mirror 未写入（validate 只读）` |
| Unity EditMode（全量） | `Tuanjie.exe -batchmode -runTests -testPlatform EditMode` | **810 / 810，Failed = 0，Skipped = 0** |
| Unity PlayMode（Encounter 产品路径） | `-runTests -testPlatform PlayMode -testFilter EncounterProductPathPlayTests` | **2 / 2，Failed = 0**（`ProductBattleNode_ConsumesFrozenRoster_PrepEqualsBattle_AndResolveClears`、`ProductBattleNode_GenerationFailure_DoesNotEnterNode_AndAllowsRetry` 均 Passed） |
| Unity Compile | 同一批 batchmode 日志检查 `error CS` | **0 × error CS**（EditMode / PlayMode 日志均为 0） |

说明：

- 本轮所有数字均为**本次真实执行**，未引用历史 PASS；历史基线只作回归定位参考（1C-R 前 EditMode 807，1C-R 后 810）。
- 过程中发生一次环境事件：首次执行 `RRPG_一键导表.cmd` 时项目被一个已打开的 Tuanjie 编辑器实例（PID 15968）占用，Unity 报 `HandleProjectAlreadyOpenInAnotherInstance` 并中止，当轮**未**产生 Unity 证据；按 Manual Environment Intervention 通知制作人关闭编辑器后，本轮重新完整执行，取得上表全部结果。首次失败不计入验证结论。
- 附带发现（**不在本 WP 修复范围**，建议后续单独的仓库脚本任务处理）：`RRPG_一键导表.cmd` 的编辑器占用守卫检查 `%PROJ%\Temp\UnityLockfile`，而 Tuanjie 实际创建的是 `Temp\TuanjieLockfile` → 守卫失效，占用时失败会退化为 Unity 崩溃日志，而非明确的 `[BLOCKED]` 提示。

## 6. Producer Play Acceptance（2026-09-25）

**结果：PASS。** 制作人实际游玩确认：

- Prep（战前准备）敌人数 / 图片 / 阵型正常；
- 开战后没有换怪 / 多怪 / 少怪；
- 没有敌人重叠；
- 下一场 Encounter 正常；
- 当前未发现产品侧问题。

**范围声明**：这是 **WP-Encounter-1 的产品链人工验收**（Prep → Battle → Resolve → 下一场 Encounter）。**不得扩写为「RRPG 整体产品体验验证完成」**——整个游戏的正式体验验证尚未开始。

## 7. 文件分类（Commit Boundary）

### A. WP-Encounter-1 正式交付物

**Config / Mirror（1A）**：`Development/Config/RRPG_Encounters.xlsx`、`Development/Generated/StaticConfig/{Encounter,EncounterSlot,EncounterSlotRule,EncounterPool,EncounterPoolEntry,MonsterEncounterProfile,MonsterRole}.json`、`Development/Config/README.md`、`Development/Generated/StaticConfig/README.md`、`Development/README.md`、`Tools/RRPG.ConfigCompiler/README.md`。

**Compiler / Validator（1A）**：`Tools/RRPG.ConfigCompiler/src/RRPG.ConfigCompiler/Core/{EncounterValidator.cs,DiagnosticCodes.cs,EntityRequirements.cs,CompilerRunner.cs}`、`Tools/RRPG.ConfigCompiler/tests/RRPG.ConfigCompiler.Tests/{EncounterDomainTests.cs,Fixtures.cs}`。

**Unity Builder / Registry（1A）**：`UnityProject/RRPG1.0game/Assets/Editor/StaticConfig/{EncounterMirrorReader.cs,StaticConfigBuilder.cs,MirrorJsonText.cs}`、`Assets/Scripts/StaticConfig/{EncounterConfigDatabase.cs,EncounterDefinitions.cs,StaticConfigRegistry.cs,RRPGStaticConfigRoot.cs}`。

**Generator（1B）**：`Assets/Scripts/Encounter/`（`Encounter.Runtime.asmdef`、`DeterministicRandom.cs`、`EncounterGenerationInput.cs`、`EncounterGenerationResult.cs`、`EncounterGenerator.cs`、`EncounterProductRules.cs`、`EnemyBoardFormation.cs`、`EnemyRoster.cs`）。

**Runtime Cutover（1C / 1C-R）**：`Assets/Scripts/Adventure/{AdventureManager.cs,AdventureEncounterRoster.cs}`、`Assets/Scripts/Battle/{BattleSystem.cs,BattleSystemEnemyRoster.cs,BattleEnemyRosterMapping.cs}`、`Assets/Scripts/UI/{BattleUI.cs,BattleUIEnemyRoster.cs,BattleUIEnemyIntent.cs,BattleUIFormationPresentation.cs,BattleUIRulePipeline.cs,ExpeditionUI.cs}`。

**Tests**：`Assets/Tests/EditMode/Encounter/`（全部，含 `EncounterProductIntegrationTests` 的 R1 / R2 / R2b）、`Assets/Tests/EditMode/StaticConfig/{EncounterConfigBuilderTests.cs,StaticConfigTestAuthority.cs}`、`Assets/Tests/PlayMode/EncounterProductPathPlayTests.cs`、1A-R 的基线修复修改（`EnemyPresentationBattleWiringTests.cs`、`EnemyPresentationResolverTests.cs`、`MonsterRegistryEntryTests.cs`、`MonsterShadowTests.cs`）。

**Docs（本次 Closeout）**：本报告、`Docs/DevelopmentSpecs/README.md`、`Docs/决策记录.md`（D-020）、`GameDesign/当前设计状态.md`、`Docs/项目状态快照.md`、`Docs/开发验收交接.md`、`Docs/项目日志.md`。

### B. Producer Companion Content（制作人内容；与 WP 必须共同提交）

| 文件 | 为什么必须与 WP 共同提交 |
|---|---|
| `Development/Config/RRPG_Monsters.xlsx` | **同一文件同时包含**制作人的 6 怪正式内容与 WP-1A 必需的 `MonsterEncounterProfile` / `MonsterRole` 两张子表；XLSX 是二进制包，**无法按 Sheet 拆分提交**。它同时是生成器与产品检查点的输入 Authority。 |
| `Development/Config/RRPG_EnemyPresentation.xlsx` | 制作人新增怪物（6 只）的 `MonsterId → VisualAssetId` 绑定与 `VisualAsset` 源图登记；Encounter 测试内容引用这些怪物，缺失则当前 Encounter 产品链无法正确显示敌人。 |
| `Development/SourceAssets/Enemy/enemy_mon_001.png`、`002.png`、`003.png`（新）、`004.png`、`005.png`、`006.png`（新）；`enemy_mon_003_wolf.png`（删除，改名为 `enemy_mon_003`） | 制作人的敌方正式 / 候选源图与命名整理；`AssetManifest.json` 的源图哈希随之上移（**换图 = 必须重新 build**）。它们**不是** WP 生成的资产，但当前 Encounter 产品链的显示正确性依赖它们（制作人验收「每只怪图片正确」即来自此）。 |
| `Development/Generated/StaticConfig/{Monster.json,EnemyPresentation.json,VisualAsset.json,AssetManifest.json}` | 上述制作人内容经 Compiler 生成的 Canonical Mirror；必须与 XLSX 同时提交，否则 mirror 与 Authoring 不一致（`validate` 会失败）。 |

### C. 必须排除（不得进入本 WP 提交）

| 文件 / 类别 | 判定 |
|---|---|
| `UnityProject/RRPG1.0game/Assets/Scenes/SampleScene.scene` | **排除**：工作区 blob 与 `HEAD` blob 完全相同（`672af96376ceca50c4b873bdaf1fb44e1df9a38b`），`git diff --numstat` 与 `--ignore-cr-at-eol` 均为空 → **仅 CRLF/LF 统计噪声，零内容变化**；本 WP 未新增需要重新绑定的序列化字段（`BattleUIEnemyRoster` 使用非序列化字段，复用既有 `unitCardPrefab` / `monsterCardScale` / `_rowCards`）。 |
| `.og*` / `.pe*` / `.pf*` / `.qa*` / `.qb*` / `.qc*` / `.vd*` / `_probe_check*.txt`、`D:\_*.txt` | **排除**：本轮与历史探针脚本、测试结果 XML、日志；验证结束后已清理。 |
| `Tools/RRPG.ConfigCompiler/.build-state/**`（`builder.stage.txt` / `unity-builder.log` / `BuildReport.*`） | **排除**：`.gitignore` 已覆盖（本地构建状态，不进 Git）。 |
| `UnityProject/RRPG1.0game/{Library,Temp,Logs,Assets/Generated}/**` | **排除**：Unity 缓存与生成根；`.gitignore` 已覆盖，生成根由 Builder 重建。 |
| `UnityProject/RRPG1.0game/TestResults/BattleStage1.EditMode.results.txt` | **排除且不改动**：M4 之前遗留的 tracked 历史结果文件，不属于本 WP；本轮未修改它。 |

## 8. 未完成 / Open Issues

真实未完成事项（本轮**不**创建 1D / M5 或任何后续 Work Package）：

- 正式 Encounter 内容与平衡（数量、构成、难度曲线、权重、威胁预算、深度区间）。
- 正式 Node → EncounterPool 内容绑定（Biome / Region / 内容表）；当前只有 V0.1 兼容映射。
- Species / Allegiance / Biome、Encounter Modifier / Curse 等筛选与遭遇修饰维度（当前筛选维度只有 `Role` / `Tier` / `MonsterId`）。
- 正式 Elite / Boss 内容与数值（当前 `enc_test_boss` 等只是测试内容）。
- 无限远征正式 Depth 模型与终止条件。
- 正式错误 UI（当前是 `Debug.LogError` + Journey denied 文案，不是正式玩家界面）。
- 其它静态配置领域仍未迁移（已迁移：Monster Definition / Enemy Presentation / Encounter）。

## 9. Commit 建议

- **建议**：**单一 Commit**（WP 级收口提交）。
- **理由**：配置链、生成器、产品切换、测试与 Canonical Mirror 互为依赖——单独提交 `RRPG_Encounters.xlsx` 或单独提交 Mirror 会让 `validate` 失败；单独提交 Runtime 会让测试与实现不一致；制作人内容（6 怪 XLSX / 图片 / Mirror）与 WP 输入**同文件耦合**（见 §7 B），无法安全拆分。
- **建议 Commit message**：

```text
feat(encounter): add enemy encounter generation foundation V0.1

- config: Encounter workbook + Monster encounter profile/role sheets + mirror
  (Encounter / EncounterSlot / EncounterSlotRule / EncounterPool /
  EncounterPoolEntry / MonsterEncounterProfile / MonsterRole)
- generator: Encounter.Runtime (pool + depth + seed -> EnemyRoster, threat budget,
  slot rules, 3x3 formation solving, EnemyInstanceId, explicit failure)
- runtime: product Battle/Prep consume frozen Active EnemyRoster
  (MonsterId template vs EnemyInstanceId instance, 1..9 enemies deployed,
  no Monster Registry fallback)
- atomic node entry: pre-generate before Enter; failure leaves zero
  Progression / Supply / elapsed mutation
- tests: EditMode 810/810, Compiler 139/139, PlayMode EncounterProductPathPlayTests
- producer play acceptance PASS (2026-09-25)
```

- **精确文件集合**：§7 A + §7 B（不含 §7 C）。

## 10. 下一步产品阶段（建议）

WP-Encounter-1 关闭后**不继续 Encounter 功能开发**（不自动进入 1D / M5）。建议下一阶段回到：

1. **Art Production Surface Audit（第一版正式美术生产面审计）**：审计 Person Visual（人物视觉）、Journey Event Illustration（旅途事件插图），以及 Battle / Journey / Village 中当前仍靠手工 Inspector / Catalog 的高频美术域，判断哪些必须进入批量生产链、哪些保持 Unity-native / Inspector 更合理。
2. 之后才进入 **First-pass Formal Art Replacement（第一版正式美术批量替换）** → **Experience Validation（产品体验验证）**。

以上仅为建议，不构成新 Work Package 授权；启动需要制作人明确授权、Target Branch 与现场审计。
