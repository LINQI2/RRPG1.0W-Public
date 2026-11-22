# Canonical Mirror — Development/Generated/StaticConfig

> 当前入口核对（2026-10-06）：本目录是生成镜像的只读 review 面。Monster / Enemy Presentation / Encounter / Event / AutonomousLife / Social 已接入产品链；下文 1A / 1B 等段落描述各阶段职责，“不接产品”只适用于当时阶段。当前制作操作看[配置手册](../../Config/README.md)，当前实现与阶段证据看[项目状态快照](../../../Docs/项目状态快照.md)。本 README 是可维护的治理文件，生成 JSON 不在本次修改范围。

> **DO NOT EDIT.** 本目录的任何 `*.json` 都由 `RRPG.ConfigCompiler` 生成；人工编辑会被
> `validate` 报为 mirror-stale（`MIR002_MIRROR_STALE`），并会在下一次 build 被覆盖。

## 这个目录是什么

* **D-019 冻结的 `Generated Review Mirror`**：进 Git、可读、可 review、可 diff 的机器镜像；
* 它是 **XLSX（表格数据人工来源）+ SourceAssets（按内容需要的源资产人工来源）** 的确定性重建结果；
* 它**不是** Unity 运行时资产（运行时资产在 `UnityProject/RRPG1.0game/Assets/Generated/StaticConfig/`，**不进 Git**）。

## 硬规则（FDS §7.2）

| # | 规则 |
|---|---|
| R1 | 文件首部必须带 `_generated.doNotEdit = true` + tool + formatVersion + `sourceSha256` |
| R2 | 不得人工编辑；不得成为第二 Authoring Authority |
| R3 | 相同输入 → 相同字节（UTF-8 无 BOM、LF、无时间戳、不写机器路径） |
| R4 | `sourceSha256` = 当前 Authoring 的**语义内容**指纹（换列序 / 改中文表头 / 加行备注不改变它） |
| R5 | `validate` 重算 Canonical 并与本目录逐字节比较；不一致即明确失败（**只读，不修文件**） |
| R6 | 输入非法 / 校验失败 → **0 更新**（Hard Error） |
| R7 | 更新是事务式的：全部成功才原子替换 |

## 当前生成域（同一Canonical Mirror链）

本目录当前包含 **Monster 领域**（M2）、**Enemy Presentation 领域**（M3）、**Encounter 领域**（WP-Encounter-1A）
与Event、AutonomousLife、Social领域的Canonical Mirror：

| 文件 | 内容 |
|---|---|
| `Monster.json` | 来源 `Config/RRPG_Monsters.xlsx` / Sheet `Monster`（8 字段） |
| `EnemyPresentation.json` | Sheet `EnemyPresentation`（MonsterId → VisualAssetId） |
| `VisualAsset.json` | 共享表 `Config/RRPG_VisualAssets.xlsx` / Sheet `VisualAsset`（VisualAssetId → SourceFile / ImportPreset；**唯一一份**） |
| `AssetManifest.json` | SourceAssets 源图内容哈希（**换图 = 必须重新 build**） |
| `MonsterEncounterProfile.json` | `RRPG_Monsters.xlsx` / Sheet `MonsterEncounterProfile`（一怪一行：ThreatCost / Tier / RandomWeight） |
| `MonsterRole.json` | `RRPG_Monsters.xlsx` / Sheet `MonsterRole`（同一怪可多行：Melee + Tank） |
| `Encounter.json` / `EncounterSlot.json` / `EncounterSlotRule.json` / `EncounterPool.json` / `EncounterPoolEntry.json` | `RRPG_Encounters.xlsx` 的 5 张表（遭遇生成静态配置基础） |
| `Event.json` / `EventTrigger.json` / `EventRole.json` / `EventRoleCondition.json` / `EventCondition.json` / `EventPresentation.json` / `EventOption.json` / `EventOptionCondition.json` / `EventOutcome.json` / `EventEffect.json` | `RRPG_Events.xlsx` 的 10 张表（统一事件内容基础） |
| `Activity*.json` / `BehaviorChannel.json` / `PersonBehavior.json` / `PersonPreference.json` / `Place.json` / `AutonomousLifeCalibration.json` | AutonomousLife选择/投影配置；只读生成，不是保存状态 |
| `RelationalFactType.json` / `RelationshipInterpretationRule.json` / `SocialCalibration.json` | Social-owned解释规则；数值Prototype / Calibration Not Frozen |

* `Development/Config/RRPG_Events.xlsx` = **Current Event Content Authoring Authority**（WP-Event-1A，2026-09-26）；详见
  [Development/Config/README.md](../../Config/README.md) 的「WP-Event-1A 状态」。
* `Development/Config/RRPG_VisualAssets.xlsx` = **Current 共享 VisualAsset Authoring Authority**（WP-Event-1A 起独立）；
  `VisualAsset.json` 的 `source.workbook` 因此从 `RRPG_EnemyPresentation.xlsx` 变为 `RRPG_VisualAssets.xlsx`。
  **这是允许的 metadata 变化**：业务行（6 条敌方临时源图）与 `AssetManifest.json` 完全未变。
* `Development/Config/RRPG_Encounters.xlsx` = **Current Encounter Authoring Authority**（WP-Encounter-1A，2026-09-25）；
  它承载遭遇模板 / 组合需求位 / 筛选规则 / 遭遇池 / 池条目。**敌人数量的唯一来源是 EncounterSlot 的数量列。**
* `Development/Config/RRPG_Monsters.xlsx` = **Current Monster Definition Authoring Authority**（M2）；
  WP-Encounter-1A 给它增加了两张 Encounter 子表（`MonsterEncounterProfile` / `MonsterRole`），**没有**改动 `Monster` 表本身。
* WP-Encounter-1A 的 Mirror 只是**语义重建结果**：1A 不生成敌人、不接 Battle / Prep（属后续 1B / 1C）。
  当前Encounter产品生成与Prep / Battle接线已落地；Mirror中的威胁预算 / 权重 / 深度区间等都是 **Test / Not Frozen 测试值**。
* 各文件详细的行数与指纹不再在本 README 里逐条抄写（容易过期）：要核对请看 `validate` 输出或
  `Tools/RRPG.ConfigCompiler/.build-state/BuildReport.md`（本地状态，不进 Git）。


* `Development/Config/RRPG_Monsters.xlsx` = **Current Monster Definition Authoring Authority**（M2）；
  `Development/Config/RRPG_EnemyPresentation.xlsx` = **Current Enemy Presentation Authoring Authority**（M3）。
  本目录的 Mirror 是它们的**只读 review 面**。
* `Development/Excel/Monster.csv`：M2 起不再作为来源（CSV → XLSX 同步已关闭），**M4 已物理删除**；不得恢复为制作入口。
* `AssetManifest.json` 是**唯一**由 SourceAssets 内容（而不只是 XLSX 文本）决定的 Mirror 文件：
  源图内容变化 → `validate` 报 `MIR002_MIRROR_STALE`，重新 build 后收敛。
* 尚未迁移的领域**没有** JSON 是**合法状态**：不得为了"看起来有输出"而伪造领域 JSON。
* **M3 Slice 1B**：Unity Builder 会读取本目录的 `EnemyPresentation.json` / `VisualAsset.json` / `AssetManifest.json`，
  生成 `Assets/Generated/StaticConfig/Databases/{EnemyPresentationDatabase,VisualAssetCatalog}.asset`
  以及 `Assets/Generated/StaticConfig/Assets/**` 下的 managed Sprite（**都不进 Git**）。
* **M3 Slice 1D-1**：产品侧消费已切换 —— `StaticConfigRegistry` → `EnemyPresentationResolver` →
  `BattleUI`（Prep 敌方格 + Battle 单位卡同一入口）。当前敌方源图为**临时素材**（每怪一张，可区分）。
* **WP-Event-1A（2026-09-26）**：Unity Builder 也会读取本目录的 10 个 `Event*.json`，生成
  `Assets/Generated/StaticConfig/Databases/EventConfigDatabase.asset`（**不进 Git**）；
  Runtime 只读入口是 `StaticConfigRegistry.TryGetEventConfig / EventConfig / EventConfigDiagnostic`，
  该1A阶段不接产品的声明仅是历史；当前Unified Event Core已求解/执行并接Village / Expedition / Save，长期操作规则见Config README。

> 本 README 是**治理文件**，不属于配置产物；Compiler 的 stale 回收不得删除它。
> 与它并列出现在本目录的其它非 `*.json` 治理文件同理（不属于 Mirror 产物面）。

## 谁写它

```text
RRPG_一键导表.cmd        （Compiler build → Unity Builder → verify）
或   rrpg-config build --root <repo>
```

查看状态（只读、不写文件）：

```text
RRPG_检查配置.cmd
或   rrpg-config validate --root <repo>
```
