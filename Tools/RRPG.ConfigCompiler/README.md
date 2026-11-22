# RRPG.ConfigCompiler — 当前静态配置操作与校验

正式 Static Configuration 工具链（FDS §3.1 / §4 / §5）。**不进入 Unity runtime**，不引入任何包体依赖。

## 当前状态（2026-10-08）

九本Authoring工作簿及字段职责见[Config操作手册](../../Development/Config/README.md#当前工作簿与职责)。Monster / EnemyPresentation / 共享VisualAsset / Encounter / Event / AutonomousLife / Social / BuildingDefinition均使用一套Compiler → Generated Review Mirror → Unity Builder → StaticConfigRegistry；V16-D在同一链增加VillagePresentation / BuildingPresentationBinding，当前本地实施状态查V16执行记录。已发布领域不能把历史1A“只编数据”当作现在未接产品。

### 校验职责

- 通用Schema、声明式Row3、duplicate key、跨Workbook ref保持唯一机制，任一Hard Error→Mirror 0更新，成功事务式替换，validate只读比对不修文件。
- EncounterValidator校验预算、Slot/阵型、条件引用与Enabled内容；EventValidator校验Trigger域、角色/条件/Option-Outcome-Effect、Presentation模板与资源引用，不生成运行时事件。
- AutonomousLifeValidator校验选择配置与必需Calibration，含InjuryLight / InjuryHeavy / InjuryDisabled；SocialValidator校验Fact / rule / Strength Calibration与ActivitySocialProjection跨域引用。Activity→Fact mapping不拥有关系delta，Social规则才拥有解释。
- VisualAsset唯一共享源为RRPG_VisualAssets.xlsx；EnemyPresentation仅引用VisualAssetId。AssetManifest记录SourceAssets图片内容hash，换图必须重新build；目前PNG→Sprite已实证，其它UnityObject类型不泛化。
- Monster.csv / MonsterData / per-entry Monster SO已退休；历史M1ShadowSync拒绝sync，其只读迁移查询不成为制作入口。其它CSV / SO领域仍逐域核对。

Prototype / Calibration与正式规则区分见配置手册；4人/3Place/5Activity、Affinity delta不因校验成功而Frozen。详细旧阶段与验证证据见[DevelopmentSpecs导航](../../Docs/DevelopmentSpecs/README.md)。

## 定位

```text
Development/Config/*.xlsx  ──Compiler──►  Development/Generated/StaticConfig/*.json
   （唯一人工 Authority）                     （Canonical Mirror，进 Git，DO NOT EDIT）
                                          └──Unity Builder──► Assets/Generated/StaticConfig/
                                                                 （生成资产，不进 Git）
```

## 用法

```text
rrpg-config validate [--root <repo>]        只读检查（含 Mirror 比对；不写任何文件）
rrpg-config build    [--root <repo>]        校验通过才写 Canonical Mirror（Hard Error → 0 更新）
rrpg-config package-check <file.xlsx>       单文件 Open XML 结构校验
```

制作人不要手敲命令 → 用仓库根三个入口（`RRPG_打开配置表.cmd` / `RRPG_检查配置.cmd` / `RRPG_一键导表.cmd`）。

**退出码**：`0` = 成功；`1` = Hard Error；`2` = 用法错误；`3` = 环境错误。

## 工程与依赖

* `src/RRPG.ConfigCompiler/` — `.NET 10`（`net10.0`），`AssemblyName = rrpg-config`；
* `tests/RRPG.ConfigCompiler.Tests/` — xUnit；**测试语料只存在于测试工程内**（临时目录生成），
  正式 `Development/Config/` 不放测试语料（FDS §7）；
* 版本锁定：`ClosedXML 0.104.2`、`DocumentFormat.OpenXml 3.1.1`、`System.IO.Packaging 8.0.1`（安全钉版）；
* `packages.lock.json` + `RestorePackagesWithLockFile=true` → **首次 `dotnet restore` 可能需要 NuGet 网络**；
  **正常 `validate` / `build` 不访问网络**（FDS §5.2）。

## 目录 / 状态

| 路径 | 进 Git | 说明 |
|---|---|---|
| `Development/Config/` | ✅ | 人工 Authoring（XLSX） |
| `Development/SourceAssets/` | ✅ | 源资产（不进 Unity `Assets/`） |
| `Development/Generated/StaticConfig/` | ✅ | Canonical Mirror（**DO NOT EDIT**） |
| `.build-state/` | ❌ | 最近一次执行报告（BuildReport / builder.stage.txt） |
| `.build-tmp/` | ❌ | 原子替换前的临时产物 |
| `bin/` `obj/` | ❌ | 本地构建产物 |

## Spike → Production Promotion Map（FDS §4）

**Promote（已生产化）**：XLSX 读取（多行表头 + A 列说明栏 + `ignore`/`note`）、机器字段身份、
规则语法（`default=` / `>=` / `<=` / `allowed=` / `optional` / `key` / 空 = 必填）、
Schema / Data / Cross-ref 校验（定位到 表 / Sheet / 行 / 列）、确定性 Canonical 写出、
事务式提交（失败 0 更新）、Open XML 包校验、稳定诊断码与退出码。

**Rewrite / Rename（已改写）**：去 Spike 命名空间 / 去 `Development/Spikes/...` 路径 /
去 T1–T4 术语 / 去 fixture 假设 / 路径解析改为正式布局（`CompilerPaths`）。

**Do NOT Promote（留在 Spike 分支 `spike/config-architecture-a @ 3532351d…`，只读参考）**：
T2 候选 A/C 对比实现、T3 Player 交付 harness、T4 Preview 场景与宿主、Spike 场景/资产、
实验工作簿脚手架（`Scaffold/*`）、实验记录。

**完成记录（M0–M4）**：生产工具链基础（M0）、Monster Shadow + 等价（M1）、Monster Definition Authority Cutover（M2）、
Enemy Presentation 编译、Builder、Resolver 与产品消费接线（M3）、Monster Legacy 物理退休（M4）。M3 的分 Slice 细节见上文“M3 状态”，M4 验证与保留项见 Closeout §9 / §11.8。

## 可重复验证

```text
Tools\RRPG.ConfigCompiler\verify-toolchain.cmd
```

按顺序执行：`dotnet build` → `dotnet test` → `rrpg-config validate` → Git 规则核对
（生成根必须被忽略 / Mirror 必须可追踪）→ 若本机可解析 Unity 编辑器，再跑
Builder（`-executeMethod`）与全量 EditMode 套件；编辑器不可用时该步骤会**明确报告 SKIPPED**，
不会假装通过。

## M0 边界（历史：M0 阶段（Production Toolchain Foundation）不做；**已被 M1/M2 取代**）

> 以下为 M0 阶段的范围声明，保留作为历史记录；`Monster Shadow`、`Authority Cutover` 等事项已在 **M1（Shadow + 等价）** 与 **M2（Monster Definition Authority Cutover）** 落地，见本文件“M2 状态”。

不创建 `RRPG_Monsters.xlsx` / `RRPG_EnemyPresentation.xlsx`、不做 Monster Shadow / 等价比较、
不改 `Monster.csv` / `MonsterData` / `DataManager` / `ExcelImporter` / Battle / Expedition / Save、
不做 Authority Cutover、不删除任何 Legacy。
