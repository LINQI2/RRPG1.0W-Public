# RRPG M3 — Enemy Presentation Authority Cutover · Closeout / Handover Report V0.1

* 日期：2026-09-23
* 分支：`migration/static-config-m1`（M0–M3 连续在同一迁移分支）
* 状态：**M3 Ready for Commit & Review**（1A / 1B / 1C / 1D-1 全部落地并验证；**Final Closeout 轮已通过**：EditMode 752/752、无过滤 PlayMode 71/71、真实 Game View 整屏截图已取得；未 commit）
* 前置：M0（Toolchain）→ M1（Monster Shadow）→ M2（Monster Definition Authority Cutover）已完成

---

## 1. 目标

把「敌方视觉」从**单一 Inspector 占位**（`BattleUI.enemyBodyPlaceholder`）切换为**配置 Authority**：

```text
Development/SourceAssets/Enemy/*.png            （人工源图）
        ↓ Compiler
Development/Generated/StaticConfig/{EnemyPresentation,VisualAsset,AssetManifest}.json   （Canonical Mirror，进 Git）
        ↓ Unity Builder
Assets/Generated/StaticConfig/**                （生成根：*Database.asset / *Catalog.asset / managed Sprite；不进 Git）
        ↓ Runtime
MonsterId → EnemyPresentationResolver → Sprite  （Battle / Prep 同一入口）
```

## 2. 切片与交付

| 切片 | 交付 | 关键产物 |
|---|---|---|
| **M3 Slice 0** | 只读前置审计（文本交付，未落盘为文档） | 事实：唯一消费点 = `BattleUI`，无 Presentation Authority |
| **M3 Slice 1A** | Compiler 侧领域接线（Schema / 校验 / Mirror） | `SourceArtValidator` / `VisualImportPresets` / `EnemyPresentation.json` / `VisualAsset.json` / `AssetManifest.json` |
| **M3 Slice 1B** | Unity Builder 消费 + managed Sprite 生成 | `EnemyPresentationMirrorReader` / `VisualAssetMirrorReader` / `AssetManifestReader` / `ApplyImportPreset` |
| **M3 Slice 1C** | Runtime Resolver + Registry 领域入口 | `EnemyPresentationResolver` / `StaticConfigRegistry.TryGetEnemyPresentation` / `TryGetVisualAssets` |
| **M3 Slice 1D-1** | BattleUI 消费切换（Prep + Battle）+ 临时素材 + 端到端验证 | `BattleUI.ApplyEnemyBodyVisual` / `PresentationResolver` / 3 张 per-Monster 临时源图 |

## 3. 事实：敌方视觉的**实际消费点**（先核对再接线）

| 阶段 | 代码位置 | 事实 |
|---|---|---|
| **Battle（战斗中单位卡）** | `BattleUIUnitVisualHosts.ApplyUnitVisualHosts(card, unit, isPlayer=false)` → （改造前）`ApplyUnitBodyVisual(card, false)` → `enemyBodyPlaceholder` | **真实消费点**：所有敌方单位卡共用同一张 Inspector 占位图 |
| **Prep（战前布阵敌方格）** | `BattleUI.ShowPrepPhase()` 创建 `EnemyPrep_<MonsterId>` 卡，只写 `Text_Name / Text_HP / Text_ATK` | **Prep 确实显示敌方单位**，但改造前**没有任何代码设置其身体 Sprite** → 沿用 `UnitCard.prefab` 自带图（隐式消费点） |

结论（与用户指令一致）：Prep 阶段**确实显示敌人** → 因此把 Prep 也接入**同一个** Resolver（而不是为满足清单新造功能）；
同时把「唯一取图路径」收敛为一个方法，避免 Prep / Battle 各自找图。

改造后：

* `BattleUI.ApplyEnemyBodyVisual(GameObject card, string monsterId)` = **Prep 与 Battle 的唯一敌方身体入口**；
* `BattleUI.PresentationResolver`（lazy 字段 `_enemyPresentationResolver`）= **Prep 与 Battle 共用的同一个 Resolver 实例**；
* 旧的 `ApplyUnitBodyVisual(card, isPlayer)`（读 `enemyBodyPlaceholder` 的那条路径）**已删除**；
* `enemyBodyPlaceholder` 字段**保留**（Scene 序列化兼容），但运行时代码中**只剩字段声明**一处出现（有 EditMode Gate 断言）。


---

## 4. Authority 链与冻结契约

| 层 | 位置 | 说明 |
|---|---|---|
| 人工 Authoring | `Development/Config/RRPG_EnemyPresentation.xlsx` | `EnemyPresentation`（MonsterId → VisualAssetId，key=MonsterId）+ `VisualAsset`（VisualAssetId / SourceFile / ImportPreset） |
| 源资产 | `Development/SourceAssets/Enemy/*.png` | 只放 `.png`；`SourceFile` 必须写成相对该目录的路径（禁绝对路径 / `..`） |
| Canonical Mirror | `Development/Generated/StaticConfig/*.json` | `EnemyPresentation.json`(rows=3) / `VisualAsset.json`(rows=3) / `AssetManifest.json`(assetCount=3)；DO NOT EDIT |
| 生成根 | `Assets/Generated/StaticConfig/**` | `EnemyPresentationDatabase.asset` / `VisualAssetCatalog.asset` / `Assets/Enemy/*.png`（managed Sprite）；不进 Git |
| Runtime | `StaticConfigRegistry` → `EnemyPresentationResolver` → `BattleUI` | 唯一读取路径 |

**冻结契约**

1. `AssetManifest.managedRootRelative = Assets/Generated/StaticConfig/Assets`；
   `ManagedAssetPath == managedRootRelative + "/" + SourceFile`（Builder 不得自行推导，违反即 Hard Error）。
2. `ImportPreset` 目前只有 `sprite_point_100`（Sprite / Single / Point / 100 PPU / Uncompressed / 无 mipmap /
   AlphaIsTransparency / Center / Clamp）。改动点共三处且必须同源：
   `VisualImportPresets`（Compiler）+ XLSX Row 3 `allowed=` + `StaticConfigBuilder.ApplyImportPreset`。
3. **换图 = 必须重新 build**：源图内容哈希进 `AssetManifest`；内容与 Mirror 不一致时 Compiler `validate` 报
   `MIR002_MIRROR_STALE`，Builder 报「源图内容与 Mirror 不一致（换图必须先重新 build）」并**拒绝写入**。
4. Builder 侧 `Missing asset` 一律 Hard Error（VisualAsset 无 Manifest 条目 / 源图缺失 / 孤儿条目 / Sprite 不可用）。
5. `enemyBodyPlaceholder` 不再参与敌方视觉；`playerBodyPlaceholder` 仅作为**我方** Person Visual 的回退占位。

## 5. Fallback 契约（**只存在于 Resolver 内**）

`EnemyPresentationResolver`：

| 入口 | 语义 |
|---|---|
| `TryResolve(monsterId, out Sprite sprite, out string diagnostic)` | **严格**：仅当 Authority 链路完整时返回 `true`（diagnostic 空）。返回 `false` 时 `sprite` **仍是 fallback（非 null）** + 明确诊断 |
| `Resolve(monsterId)` | **显示安全**：永不返回 `null`（Authority 成功 → 其 Sprite；否则 fallback）。BattleUI 用这个入口 |

触发回退的情形（全部**不静默**：fallback sprite + 明确诊断 + WarnOnce）：

1. 生成根 / 领域不可用（Registry 诊断）；
2. 该 MonsterId 没有 Presentation 绑定（含领域为空）；
3. `VisualAssetId` 不在 `VisualAssetCatalog` 中；
4. VisualAsset 存在但 `Sprite == null`（生成根不完整）；
5. `monsterId` 为 null / 空（调用方错误）。

* fallback sprite = **运行时生成**的 1×1 洋红 Sprite（与 Unity missing-asset 同色；不依赖任何资产 / Editor / 场景）。
* WarnOnce：`WarnCount` 每次回退都 +1，但日志只输出一次；`ResetWarnOnce()` / `StaticConfigRegistry.Reset()` 可复位。
* **BattleUI 不做任何判断**：只传入 MonsterId、显示返回的 Sprite（EditMode Gate W2 + PlayMode 断言共同守住）。

## 6. 临时素材状态（明确定位：不是最终美术）

| MonsterId | VisualAssetId | 源图 | 尺寸 / 外观 | SourceBytes |
|---|---|---|---|---|
| `mon_001` | `va_enemy_mon_001` | `Enemy/enemy_mon_001.png` | 24×24 红底 + 白标记 | 100 |
| `mon_002` | `va_enemy_mon_002` | `Enemy/enemy_mon_002.png` | 32×32 绿底 + 深色标记 | 118 |
| `mon_003_wolf` | `va_enemy_mon_003_wolf` | `Enemy/enemy_mon_003_wolf.png` | 40×40 蓝底 + 白标记 | 142 |

* 生成方式：一次性工具（`.build-state/m3d-png`，已删除）直接写出 RGBA8 PNG（自实现 CRC32 + BCL `ZLibStream`），
  并经 `verify` 模式自检（chunk CRC + zlib 解压 + 扫描线长度）。
* 用途：**证明不同 MonsterId → 不同 Sprite**（尺寸 / 颜色 / 资产实例三重可区分），并让制作人一眼看出「哪只怪用了哪张图」。
* 正式美术到位后的替换动作：把新图放进 `Development/SourceAssets/Enemy/`，更新 XLSX 的 `SourceFile`（或覆盖同名文件）
  → 跑 `RRPG_一键导表.cmd`（Compiler build → Builder）→ `validate` 通过即完成；**不需要改任何代码 / Scene / Prefab**。
* 旧的单一共享源图 `Enemy/enemy_body_placeholder.png` 已随 1D-1 退役（SourceAssets 中删除；生成根里的旧 managed 资产
  由 Builder 的 stale 回收删除，见 §7）。


---

## 7. 验证证据（本轮实测）

### 7.1 Compiler（域 + Mirror）

```text
rrpg-config.exe build   --root <repo>   → 结果：PASS   errors=0   warnings=2（两条 00_导航 SKIP，属预期）
    updated Generated/AssetManifest.json       bytes=1689
    updated Generated/EnemyPresentation.json   bytes=1229
    updated Generated/VisualAsset.json         bytes=1514
    unchanged Generated/Monster.json           bytes=2121      ← M2 无漂移
rrpg-config.exe validate --root <repo>  → 结果：PASS   errors=0（Mirror 只读比对通过）
```

Mirror 指纹（当前值）：

| 文件 | 关键值 |
|---|---|
| `EnemyPresentation.json` | rows=3 / `sourceSha256=080bad67a3ad1f67dcd179fd8aaeaa88f4e60ddfd3bf058190636fb8521fb5bf` |
| `VisualAsset.json` | rows=3 / `sourceSha256=892d3e54fe1a931e924dfb070fdb264e88c059cb686d229a7844e277e9f36b49` |
| `AssetManifest.json` | assetCount=3 / `sourceSetSha256=74c7e9be3d9eb94112330a3592d42baf8e895b4980fdb033cb662abdfa97654e` |
| `Monster.json` | rows=3 / `sourceSha256=339f188afa831a7d1ec43e1bae0cc4b69a4fca167c994b05b93d3c6b56624842`（未变） |

### 7.2 Unity Builder（首次构建 / 重复 no-op / stale 回收）

首次构建（`StaticConfigBuilder.ExecuteBuild` → `builder.stage.txt`）：

```text
stage=build  result=PASS  noOp=false  signature=67f3711838275bb0ba1d56957492b1e3100ceb3559ae0938736b7054e0b32e0e
action=enemy presentation: bindings=3 sourceSha256=080bad67a3ad
action=visual assets: entries=3 sourceSetSha256=74c7e9be3d9e
action=managed asset(updated): Assets/Generated/StaticConfig/Assets/Enemy/enemy_mon_001.png preset=sprite_point_100
action=managed asset(updated): Assets/Generated/StaticConfig/Assets/Enemy/enemy_mon_002.png preset=sprite_point_100
action=managed asset(updated): Assets/Generated/StaticConfig/Assets/Enemy/enemy_mon_003_wolf.png preset=sprite_point_100
action=enemy presentation database: entries=3
action=visual asset catalog: entries=3
action=deleted(stale): Assets/Generated/StaticConfig/Assets/Enemy/enemy_body_placeholder.png
action=deleted(stale): Assets/Generated/StaticConfig/Assets/Enemy/enemy_body_placeholder.png.meta
```

重复构建（相同输入）：`result=PASS  noOp=true`（内容签名命中即**不触碰任何产物**）；旧共享 managed 占位资产被 stale 回收 ✓。

### 7.3 失败路径已被实测（不是理论）

* 本轮真实验证了 Builder 的 `Missing asset` / 不可导入源图路径：临时 PNG 的 chunk CRC 写序错误时，
  Builder 明确失败：`managed Sprite 生成失败：…enemy_mon_001.png（源图…无法作为 Sprite 导入）`，且**未写入 SO**。
* 修复方式：PNG 写入按 `CRC = CRC32(type || data)` 修正，并用自写 `verify`（chunk CRC + zlib 解压 + 扫描线长度）确认 3 张源图合法。
* Compiler 侧：换图而未重新 build 时 `validate` 报 `MIR002_MIRROR_STALE`（1A/1B 自动化测试覆盖）。

### 7.4 Unity EditMode（全量）

```text
M2DisposableGateRunner（EditMode，无过滤，M3 Final Closeout 轮复测）
result=PASS   status=Passed   passed=752   failed=0   skipped=0   inconclusive=0
filter=<all>   durationSeconds=15.0   wallSeconds=16.6   testCount=752
日志：Tools/RRPG.ConfigCompiler/.build-state/m3f-emfull.log
汇总：Tools/RRPG.ConfigCompiler/.build-state/m3f-emfull-summary.txt
```

> 说明：`M2DisposableGateRunner` 本轮加了**逐测试日志**（`test#N START/END <结果> duration/wall`）与**可配置等待上限**
> （`-m2RunWaitSeconds`），用于无过滤全量运行时的进度与挂起定位（属于一次性 Gate harness，不是产品代码）。

M3 新增/更新的定向测试：

| 测试 | 覆盖 |
|---|---|
| `EnemyPresentationDomainTests`（1A，11 个） | Schema 形状 / 源图缺失 / 非法预设 / 路径越界 / 扩展名 / 引用未解析 / Mirror 确定性 / 换图 → stale → rebuild |
| `EnemyPresentationBuilderTests`（1B，7 个） | JSON→SO、两类查询、Missing asset 明确失败、byte-identical + 预设、no-op、RebuildFromZero |
| `EnemyPresentationResolverTests`（1C，9 个） | 解析成功 / 链路闭合 / 4 类回退 / fallback 只在 Resolver / 无 Editor 依赖 / Registry lazy+幂等+诊断 / WarnOnce |
| `EnemyPresentationBattleWiringTests`（1D-1，6 个：W1–W6） | BattleUI 暴露共享 Resolver 与敌方唯一入口；运行时代码只剩 `enemyBodyPlaceholder` 字段声明（旧布尔入口已删）；Resolver 返回正式 Sprite；3 怪 → 3 不同资产与 Sprite（尺寸可区分）；回退与 WarnOnce；BattleUI 写入行为（含 `card = null` 安全返回） |

### 7.5 Unity PlayMode

**（A）定向测试（Slice 1D-1 时实测）**

```text
1) BattleVisualFoundationPlayTests.EnemyPresentation_PrepAndBattle_ResolveFromAuthority
   result=PASS   passed=1   failed=0
   步骤日志：step1 进入正式战斗 → step2 → step3 resolver acquired → step4 prep cards checked count=3
            → step6 prep evidence written → step7 start battle → step8 battle started
            → step8b battle evidence written → step9 fallback + warn once
   断言要点：Prep 3 张敌方卡 + Battle 敌方单位卡的 Body_Visual == Resolver sprite == Catalog sprite；
            旧 enemyBodyPlaceholder ≠ 实际值；Prep 与 Battle 得到同一 Sprite；
            未登记 MonsterId → fallback sprite + WarnOnce（WarnCount 1→2，日志只一次）
   M3 相关日志实测：全量日志中真实出现一次 `RRPG.StaticConfig.EnemyPresentationResolver:WarnOnce`（回退只告警一次）
2) BattleVisualFoundationPlayTests.ProductBattle_VisualHosts_RefreshFromRuntimeFacts（既有回归，敌方断言已按 1D-1 更新）
   result=PASS   passed=1   failed=0     ← 我方视觉 / Formation / HP+Shield+Intent / 背景+RowVisual / 手牌卡面 / End Turn 全部回归通过
```

**（B）无过滤 PlayMode 全量（M3 Final Closeout 轮，关闭 M4-6 的验证）**

```text
M2DisposableGateRunner（PlayMode，-m2TestFilter 未传 → <all>）
run #1: result=PASS  passed=71  failed=0  skipped=0  inconclusive=0
        filter=<all>  durationSeconds=35.6  testCount=71（run started: testCaseCount=71）
        日志 Tools/RRPG.ConfigCompiler/.build-state/m3f-pmfull.log
        汇总 Tools/RRPG.ConfigCompiler/.build-state/m3f-pmfull-summary.txt
run #2（同一代码状态，Gate harness 汇总口径修复后复跑）:
        result=PASS  passed=71  failed=0  skipped=0  inconclusive=0
        filter=<all>  durationSeconds=35.7  wallSeconds=47.3  testCount=71
        日志 Tools/RRPG.ConfigCompiler/.build-state/m3f-pmfull2.log
        汇总 Tools/RRPG.ConfigCompiler/.build-state/m3f-pmfull2-summary.txt
```

结论（**这不是「两项定向通过就算全量通过」**，而是真实无过滤全量）：

1. **M4-6「PlayMode 全量挂起」已定位并通过无过滤 PlayMode 全量两次验证关闭**：全量 71 个测试**全部执行完并 Passed，两次运行总耗时 35.6 / 35.7 秒**
   （逐测试 `wall` 0.4–0.9 s）。既然全量只需半分钟，1800 s 的等待上限从来不是瓶颈 ——
   原先的「挂起」就是 §7.6 记录的**真实死锁**：`WaitForEndOfFrame` 在 batchmode（无 Game View）永不触发。
   该处已改为帧计数等待（`CaptureShotAndWait`），本轮无过滤全量一次跑通（两次均 0 failed、`END Failed` 计数 0）。
2. 本轮为定位/计时给 Gate Runner 加了逐测试日志；如再出现挂起，可凭日志最后一条 `test#N START` 精确定位到具体测试。
3. 复现命令（可复用）：
   `Tuanjie.exe -batchmode -projectPath UnityProject/RRPG1.0game -executeMethod M2DisposableGateRunner.Run -m2TestMode PlayMode -logFile <log>`
   （可选 `-m2TestFilter <完整测试全名>`；可选 `-m2RunWaitSeconds <秒>`）

**（C）无过滤 PlayMode 全量中的 M3 相关用例（逐条摘录）**

| # | 测试 | 结果 |
|---|---|---|
| 23 | `BattleVisualFoundationPlayTests.EnemyPresentation_PrepAndBattle_ResolveFromAuthority`（1D-1 新增 E2E） | Passed |
| 24 | `BattleVisualFoundationPlayTests.FormationPresentation_OneTwoThreeUnitRows_InProductBattle`（含敌方 legacy 断言更新） | Passed |
| 25 | `BattleVisualFoundationPlayTests.ProductBattle_VisualHosts_RefreshFromRuntimeFacts`（legacy 回归） | Passed |

### 7.6 运行画面证据（`screenshots/` 为 gitignored 证据目录）

本轮实测发现：**batchmode（无 Game View）下 `ScreenCapture.CaptureScreenshot` 不出文件，且 `WaitForEndOfFrame` 永不触发**
（会把测试挂住 —— 这正是既有 PlayMode 套件挂起的根因之一，已记为 M4-6 的具体成因，并顺手改为帧计数等待）。

因此运行时画面证据改为**离屏合成**（`RenderTexture` + `Graphics.DrawTexture` + `EncodeToPNG`，不依赖 Game View），
内容 = **Runtime 实际写入 `Body_Visual` 的那些 Sprite**：

| 证据文件 | 内容 | 校验 |
|---|---|---|
| `UnityProject/RRPG1.0game/screenshots/WP-D/m3_prep_enemy_presentation_runtime.png` | Prep 敌方格 3 张卡的 Sprite 并排 | 224×80 RGBA8；chunk CRC + zlib 通过 |
| `UnityProject/RRPG1.0game/screenshots/WP-D/m3_battle_enemy_presentation_runtime.png` | Battle 敌方单位卡 3 张 Sprite 并排 | 224×80 RGBA8；chunk CRC + zlib 通过 |
| 对应 `*.png.txt` | 标签：`battle:mon_001 asset=enemy_mon_001` / `battle:mon_002 asset=enemy_mon_002` / `battle:mon_003_wolf asset=enemy_mon_003_wolf` | 与 Mirror / 生成资产逐项对应 |

> 局限（已经更新）：这批离屏合成图**不是**整屏截图（batchmode 拿不到 Game View）。
> **真实 Game View 整屏截图已在 M3 Final Closeout 轮取得**，见 §7.7；本节图仅作「运行时 Sprite 赋值证据」保留，
> 二者口径不要混用。

### 7.7 Game View 人工视觉验收（M3 Final Closeout 轮）

**可行性结论：本环境支持有显示器的编辑器会话 → 已取得真实 Game View 截图。**
方式：**非 `-batchmode`** 启动编辑器（同样用 Gate Runner 驱动同一测试），测试内 best-effort 调
`ScreenCapture.CaptureScreenshot`（帧计数等待），并写同名 `.txt` 记录是否真的产出（避免把离屏合成误当整屏截图）。

**仓库内保留路径（随提交长期保留）**：`Docs/DevelopmentSpecs/Evidence/2026-09-23_M3-Enemy-Presentation-Authority/`
（含两张整屏截图 + 两份测试原始来源标注 + 该目录 `README.md`）。
生成时的本机路径 `UnityProject/RRPG1.0game/screenshots/WP-D/` 是 **gitignored**，只作为产出现场，不承担留档。

| 证据文件 | 大小 | 说明 |
|---|---|---|
| `…/Evidence/2026-09-23_M3-Enemy-Presentation-Authority/m3_prep_gameview.png` | 154,946 B | Prep 阶段整屏；同名 `.provenance.txt` = 测试原始标注 `Game View screenshot PRODUCED` |
| `…/Evidence/2026-09-23_M3-Enemy-Presentation-Authority/m3_battle_gameview.png` | 229,494 B | Battle 阶段整屏；同上 |
| `…/Evidence/2026-09-23_M3-Enemy-Presentation-Authority/README.md` | — | 来源标注、SHA256、复现命令、与离屏合成图的口径区分 |

> SHA256（与本机产出文件逐字节一致）：
> Prep `647BF8FB5A63F72925F795253D3B753C07760C518239296616EE1856743D1AB2`；
> Battle `49E9E66EF0675C4FF311707924B2723B5A54963E5DE357D51D2A56111EDA1DA9`。

**人工检查结果（对两张整屏截图逐项核对）**

| 检查项 | Prep | Battle |
|---|---|---|
| 三张临时图可见 | ✅ 三只怪物卡各显示一张：**红底+白标**（史莱姆）/ **绿底+深色标**（骷髅弓手）/ **蓝底+白标**（野狼） | ✅ 同上三张，且各自带「进攻」Intent 与行动力 1 |
| 是否可区分 | ✅ 颜色 + 标记方块位置三重可区分 | ✅ 同 |
| 明显裁切 | ✅ 未见（Body_Visual 完整可见，未被卡的边界截断） | ✅ 未见 |
| 异常缩放 | ✅ 未见（PreserveAspect 生效，三张按各自 Rect 等比显示；PPU=100 使三者显示尺寸接近一致，属预期） | ✅ 未见 |
| 遮挡 | ⚠️ 敌方卡列较窄，HP 数值与名字有轻微文字重叠（`20/20` 与「史莱姆」）——**这是 Prep 敌方列的既有排版特征**，与 M3 取图无关，本轮不改版式以免超出范围 | 无异常（数值/名字/Intent/AP 均可读） |

> 口径说明：以上为**对真实整屏截图的人工核对**；离屏合成图（§7.6）仅作为「运行时 Sprite 赋值证据」，二者不混用。
> 本轮**未改任何游戏表现代码**（仅测试内的截图/等待机制）。

### 7.8 最终消费链核对（静态 + 运行时）

**静态（代码路径）**

| 事实 | 证据 |
|---|---|
| 敌方 Body 视觉**唯一入口** | `BattleUIUnitVisualHosts.ApplyEnemyBodyVisual(card, monsterId)`（声明处 `BattleUIUnitVisualHosts.cs:126`，内部 `PresentationResolver.Resolve(monsterId)` → `ApplyEnemyBodyVisual` 写 `Body_Visual`） |
| 只有两个调用点，且都走上述唯一入口 | Prep：`BattleUI.cs:435`（`ShowPrepPhase()` 内 `ApplyEnemyBodyVisual(go, m.MonsterId)`）；Battle：`BattleUIUnitVisualHosts.cs:78`（`ApplyUnitVisualHosts(...)` 的 `isPlayer == false` 分支） |
| **同一 Resolver 实例** | `BattleUIUnitVisualHosts.PresentationResolver`（`_enemyPresentationResolver` lazy 单例，`EnemyPresentationResolver.FromStaticConfig()`）；Prep 与 Battle 都经同一个 `this` 上的属性取图 |
| 旧字段不参与运行时取图 | `enemyBodyPlaceholder` 在 `Assets/Scripts` 全文只剩 **3 处**：1 处字段声明（`BattleUIUnitVisualHosts.cs:38`）+ 2 处注释；**没有任何读取** |
| 业务层不判断 fallback | `BattleUI.cs` / `BattleUIUnitVisualHosts.cs` 均无 VisualAsset / 预设 / fallback 条件分支 |

**数据链（三只怪 → 三个不同 Sprite）**

| MonsterId | VisualAssetId | SourceFile（XLSX 授权） | Managed Sprite（Builder 产出） |
|---|---|---|---|
| `mon_001` | `va_enemy_mon_001` | `Enemy/enemy_mon_001.png` | `Assets/Generated/StaticConfig/Assets/Enemy/enemy_mon_001.png`（preset `sprite_point_100`） |
| `mon_002` | `va_enemy_mon_002` | `Enemy/enemy_mon_002.png` | `Assets/Generated/StaticConfig/Assets/Enemy/enemy_mon_002.png`（同上） |
| `mon_003_wolf` | `va_enemy_mon_003_wolf` | `Enemy/enemy_mon_003_wolf.png` | `Assets/Generated/StaticConfig/Assets/Enemy/enemy_mon_003_wolf.png`（同上） |

Builder 阶段证据（`Tools/RRPG.ConfigCompiler/.build-state/builder.stage.txt`）：
`stage=build result=PASS`、`enemy presentation: bindings=3`、`visual assets: entries=3`、
`managed asset(updated)` × 3、`deleted(stale): enemy_body_placeholder.png` → 旧占位图已被回收。
Mirror 证据：`EnemyPresentation.json` rows=3、`VisualAsset.json` rows=3（`Development/Generated/StaticConfig/`）。

**运行时（三条证据互相独立）**

1. **EditMode（W1–W6，752/752 全量内）**：BattleUI 暴露共享 Resolver 与敌方唯一入口；3 怪 → 3 个不同资产与 3 个不同 Sprite（尺寸可区分）。
2. **PlayMode E2E**：`Body_Visual == Resolver.Resolve == Catalog.Sprite`（Prep 与 Battle 同时成立）且 `!= enemyBodyPlaceholder`。
3. **真实 Game View 截图**（§7.7）：Prep 与 Battle 各看到三张不同临时图。

**Fallback / WarnOnce 证据（保留，未删）**

| 层 | 证据 |
|---|---|
| 单元（EditMode，1C） | `R4_MissingPresentation_ReturnsFallbackSprite_WithDiagnostic`、`R5_MissingVisualAsset_OrMissingSprite_ReturnsFallback`、`R6_FallbackLogic_ExistsOnlyInsideResolver`、`R9_WarnOnce_LogsOnce_ButCountsEveryFallback` |
| 运行（PlayMode E2E step9） | 未登记 `MonsterId` → 返回 fallback sprite 且 `WarnCount` 1→2、日志只输出一次；日志中可见 `RRPG.StaticConfig.EnemyPresentationResolver:WarnOnce` |

---

## 8. 决策记录（M3）

| # | 决策 | 结论 / 状态 |
|---|---|---|
| D-M3-0 | 切片拆分 | 1A Compiler 域 → 1B Builder 消费 → 1C Runtime Resolver → 1D-1 表现接线（Prep+Battle）→ M3 收尾 ✅ |
| D-M3-1 | 是否为每只怪物加临时素材 | **是**（1D-1）：3 张可区分临时源图，用于运行时证明「不同 MonsterId → 不同 Sprite」；正式美术另行替换 |
| D-M3-2 | Sprite 导入预设 | `sprite_point_100`（Sprite / Single / Point / 100 PPU / Uncompressed / 无 mipmap / AlphaIsTransparency / Center / Clamp）；三处同源可改 ✅ |
| D-M3-3 | Schema 是否扩展 per-Monster Scale/Offset | **否**（保持 3 列）；缩放/偏移仍由 Inspector 表面参数（`enemyBodyScale` / `unitBodyVisualOffset`）控制 |
| D-M3-4 | Resolver 入口形态 | `StaticConfigRegistry` 新增两领域入口 + 独立 `EnemyPresentationResolver`（Runtime，静态工厂 + 注入式 `Create`） ✅ |
| D-M3-5 | Prep 是否也绑 MonsterId | **是**（Prep 确实显示敌人 → 与 Battle 共用同一 Resolver 实例）；事实与代码位置见 §3 |
| D-M3-6 | Fallback 归属 | **只在 Resolver 内**（fallback sprite + WarnOnce 诊断）；BattleUI 不判断、不拼装 ✅ |
| D-M3-7 | `enemyBodyPlaceholder` 处置 | 保留字段（Scene 序列化兼容），但**运行时代码只剩字段声明**；旧的按阵营取图方法已删除 ✅ |
| D-M3-8 | batchmode 画面证据 | `ScreenCapture` / `WaitForEndOfFrame` 在 headless 不可用 → 用离屏合成产出运行时 Sprite 证据；既有测试的 `CaptureShotAndWait` 改为帧计数（顺带解释 M4-6） ✅ |

## 9. 遗留问题 / 后续（诚实清单）

| # | 事项 | 性质 | 建议 |
|---|---|---|---|
| M3-R1 | 临时素材不是最终美术 | 内容 | 正式图到位后替换 `SourceAssets/Enemy/*.png` + 重跑一键导表（零代码改动） |
| M3-R2 | `enemyBodyPlaceholder` 字段仍存在（Scene 里有序列化引用） | 清理（Disposable） | M4 与 Scene 清理一起删除；删除条件：连续两轮 fresh rebuild + 战斗验证通过 |
| M3-R3 | 只有一种 ImportPreset | 扩展点 | 需要 bilinear / 不同 PPU 时，同时改 `VisualImportPresets` + XLSX `allowed=` + `ApplyImportPreset` |
| M3-R4 | 未覆盖：`VisualAssetCatalog.Sprite == null` 的真实生成根场景 | 测试 | 已用合成 SO 覆盖（1C R5/W5）；如需真实场景，可手工删 managed 资产后跑 Builder 观察 Hard Error |
| M3-R5 | PlayMode 全量套件挂起 | 环境/测试 | ✅ **已定位并通过无过滤 PlayMode 全量两次验证关闭**：根因为 batchmode 下 `WaitForEndOfFrame` 永不触发；改为帧计数后**无过滤全量 71/71 PASS、35.6 / 35.7 s**（见 §7.5B） |
| M3-R6 | `StaticConfigRegistry` 领域入口暂未被除 BattleUI 之外的消费者使用 | 事实 | Expedition / Village 等如需敌方视觉，一律走同一 Resolver（不得自建取图路径） |
| M3-R7 | 敌方视觉尚无「per-Monster 差异化表现」（scale/offset/pivot 仍统一） | 范围 | 需要时按 D-M3-3 的扩展路径处理（Schema + Builder + Resolver），不要散落到 Scene/Prefab |
| M3-R8 | Prep 敌方卡列较窄，HP 数值与名字有轻微文字重叠（Game View 截图中可见） | UI 版式（**非 M3 引入**） | 属既有 Prep 排版，留待 UI 版式轮；M3 不动版式以免超范围 |
| M3-R9 | 非 batchmode 会话可产出真实 Game View 截图；batchmode 下 `ScreenCapture` 仍不出文件 | 环境事实 | 测试内已 best-effort + 写 `.txt` 标注 PRODUCED/NOT produced；若 CI 需要整屏证据，需在有显示器会话运行 |
| M3-R10 | `.build-state` 全量日志会被 Compiler `build` 的 stale 回收清空 | 证据留存 | durable 证据仍以 `builder.stage.txt` + 本报告摘录为准；需要长期留档时另行归档日志 |

## 10. 交接说明

**制作人（改内容）**

1. 打开 `Development/Config/RRPG_EnemyPresentation.xlsx`：`EnemyPresentation` 一行一只怪（`MonsterId` → `VisualAssetId`），
   `VisualAsset` 一行一张图（`VisualAssetId` / `SourceFile` / `ImportPreset`）。
2. 源图放 `Development/SourceAssets/Enemy/`；`SourceFile` 写相对路径（如 `Enemy/enemy_mon_001.png`）。
3. 双击 `RRPG_一键导表.cmd`（需先关 Unity 编辑器）→ Compiler build → Unity Builder → 生成根刷新。
4. 只检查不改：`RRPG_检查配置.cmd`（`validate`，只读；换图未重新导表会报 `MIR002_MIRROR_STALE`）。

**开发者（改代码）**

* 需要敌方身体视觉时：**只调用 `EnemyPresentationResolver`**（`Resolve` / `TryResolve`），
  不要读 `enemyBodyPlaceholder`、不要按阵营硬编码、不要自己 `Resources.Load`。
* 需要新增领域：照 `EnemyPresentation` 的做法（Compiler 域 + Mirror + Builder 容器 + Resolver + Registry 入口 + Gate 测试）。
* 改生成链后必须重跑：Compiler build → `validate` → Builder（首次 + 重复 no-op）→ EditMode 全量 → PlayMode 定向。

**验收画面证据（仓库内留档）**

* Game View 整屏截图（Prep / Battle）与来源标注：`Docs/DevelopmentSpecs/Evidence/2026-09-23_M3-Enemy-Presentation-Authority/`（含该目录 `README.md`：SHA256、复现命令、口径区分）。
* 离屏合成图（仅“运行时 Sprite 赋值证据”，**不要**当整屏截图）：`UnityProject/RRPG1.0game/screenshots/WP-D/m3_*_enemy_presentation_runtime.png`（gitignored，本机现场）。

---

## 11. M3 Final Closeout Review（2026-09-23）

### 11.1 本轮执行的验证（四项要求 → 真实结果）

| 要求 | 结果 | 证据 |
|---|---|---|
| ① **无过滤 PlayMode 全量** | ✅ **PASS**：`passed=71 / failed=0 / skipped=0 / inconclusive=0`、`durationSeconds=35.7`、`wallSeconds=47.3`、`filter=<all>` | 日志 `Tools/RRPG.ConfigCompiler/.build-state/m3f-pmfull2.log`；汇总 `…/m3f-pmfull2-summary.txt`；逐测试 `test#1..71 START/END` 全部 `Passed`（`END Failed` 计数 = **0**） |
| ② **最终消费链核对** | ✅ Prep / Battle 均经**唯一入口** `ApplyEnemyBodyVisual` → **同一** `EnemyPresentationResolver` 实例；3 怪 → 3 个不同 managed Sprite；`enemyBodyPlaceholder` 仅剩声明、**零读取**；fallback + WarnOnce 证据保留 | §7.8（静态证据）+ §7.7（画面）+ §7.5（运行）+ `builder.stage.txt` |
| ③ **真实画面** | ✅ **已取得真实 Game View 整屏截图**（Prep / Battle 各一张）：三张临时图均可见、可区分、无裁切、无异常缩放 | §7.7；仓库内留档 `Docs/DevelopmentSpecs/Evidence/2026-09-23_M3-Enemy-Presentation-Authority/m3_{prep,battle}_gameview.png`（+ `.provenance.txt` = `PRODUCED`） |
| ④ **报告与状态文档更新** | ✅ 本文 §7.4 / §7.5 / §7.7 / §7.8 / §9 / §11；FDS §11.1 收口注记；M2 报告 §7.2 `M4-6` 行标注「已定位并通过无过滤 PlayMode 全量两次验证关闭」；项目级状态文档同步见 §11.7 | 见对应文档 |

**PlayMode 全量挂起（M4-6）定位结论（不是「两项定向通过」）**

* 真实全量运行：71/71 通过、总耗时 35.7 s（逐测试 wall 0.4–0.9 s）——**不存在性能/超时问题**。
* 根因：`CaptureShotAndWait` 中的 `WaitForEndOfFrame` 在 `-batchmode`（无 Game View）**永不触发** → 测试永久挂住。
* 修复：帧计数等待（测试代码），并让截图 best-effort + 写 `.txt` 标注是否产出（不改游戏表现代码）。
* 结论：**已定位并通过无过滤 PlayMode 全量两次验证关闭**（run#1 `m3f-pmfull.log`、run#2 `m3f-pmfull2.log`，均 71/71 PASS、failed=0）。

### 11.2 本轮修复清单（全部为测试/证据机制，无产品行为改动）

| 文件 | 改动 | 性质 |
|---|---|---|
| `Assets/Tests/PlayMode/BattleVisualFoundationPlayTests.cs` | `CaptureShotAndWait` 帧计数等待（既有测试通用）；新增 `CaptureGameViewAndReport`（best-effort 整屏截图 + PRODUCED/NOT produced 标注）；E2E 中新增 Prep/Battle 两次整屏截图尝试 | 测试代码 |
| `Assets/Tests/EditMode/StaticConfig/M2DisposableGateRunner.cs` | 逐测试日志（`test#N START/END`，含 per-test duration/wall）；`-m2RunWaitSeconds` 可配置等待上限；汇总新增 `durationSeconds` / `wallSeconds` / `testCount`（一次性 Gate harness） | 测试工具（Disposable） |

> 期间两次自身失误已修正并复测：① runner 漏声明 `_runStartedAt` 导致编译失败（失败快速可见，未污染结论）；② `testCount` 字符串拼接口径错误 + `wallSeconds` 跨 domain reload 丢失（已修，EditMode 复测 `wallSeconds=16.6`、PlayMode 复测 `wallSeconds=47.3` 正常）。

### 11.3 仍待人工完成 / 明确未完成的事项

| # | 事项 | 状态 |
|---|---|---|
| 1 | **最终美术替换**（`SourceAssets/Enemy/*.png` → 正式图，重跑一键导表，零代码改动） | ⏳ 未完成（仍为临时占位图；**这是内容工作，不是技术收口阻塞项**） |
| 2 | **生产级人工视觉验收**（制作人/主美在正式美术到位后确认观感、尺寸策略 per-Monster） | ⏳ 未完成（本轮已完成「当前临时图」的整屏可见性/裁切/缩放核对） |
| 3 | `enemyBodyPlaceholder` 字段物理删除（M3-R2，需与 Scene 清理一起做） | ⏳ 留 M4 |
| 4 | per-Monster scale/offset/pivot（M3-R7）、更多 ImportPreset（M3-R3） | ⏳ 按需（扩展路径已定义） |
| 5 | Prep 敌方列文字重叠（M3-R8，**非 M3 引入**的既有排版） | ⏳ 留 UI 版式轮 |

### 11.4 提交范围与 Git 事实

**授权边界**：用户本轮授权「一次性提交 M3」（Commit）；**未授权 Push** → 本轮不执行 `git push`，收尾停在本地提交。
（仓库惯例：Handoff 的 Git Facts 记录生成时快照、不回填未来 SHA；提交 hash 见 `git log -1` 与本轮任务报告。）

**提交前基线**：`HEAD = 34f42aa feat(config): cut over monster definition authority to static config`（M2 收口 commit）。

**暂存清单（M3 Closure Commit `24b8470` 实测：`56 files changed, +4710 / −70`）**

| 范围 | 项数 | 内容 |
|---|---|---|
| `Development/Config` | 2 | `RRPG_EnemyPresentation.xlsx`（1 怪 1 图：3 个 VisualAsset + 3 条绑定）、`README.md` |
| `Development/Generated/StaticConfig` | 4 | `EnemyPresentation.json`、`VisualAsset.json`、`AssetManifest.json`（Canonical Mirror）、`README.md` |
| `Development/SourceAssets` | 4 | `Enemy/enemy_mon_00{1,2,3_wolf}.png`（3 张临时源图）、`README.md` |
| `Docs/DevelopmentSpecs` | 8 | M3 本报告、`Evidence/2026-09-23_M3-Enemy-Presentation-Authority/`（5 项：2 张 Game View 截图 + 2 份来源标注 + 目录 README）、FDS §11.1 收口注记、M2 报告 M4-6 结论行 |
| `Tools/RRPG.ConfigCompiler` | 10 | 域（`SourceArtValidator` / `VisualImportPresets` / `CompilerRunner` / `EntityRequirements` / `PrimaryErrorSummary` / `Program`）+ 测试（`EnemyPresentationDomainTests` / `Fixtures` / `TestKit`）+ `README.md` |
| `UnityProject/RRPG1.0game` | 28 | Builder（`StaticConfigBuilder` + 4 个 Reader / `MirrorJsonText`）、Runtime（Database / Catalog / Registry / 两类契约 / `EnemyPresentationResolver`）、BattleUI 两个消费文件、EditMode 4 + PlayMode 1 测试（均含 `.meta`） |

**明确未纳入**（符合 `.clinerules` §5 Disposable / 生成物边界）：`Assets/Generated/StaticConfig/**`（生成根，gitignored）、
`UnityProject/RRPG1.0game/screenshots/**`（本机证据现场，gitignored）、`Tools/RRPG.ConfigCompiler/.build-state/**`（一次性日志与汇总，gitignored）、
Unity `Temp/**` 与锁、PlayMode 产物与一次性诊断转储（提交前已删除）。
`UnityProject/RRPG1.0game/TestResults/BattleStage1.EditMode.results.txt` 属**既有已跟踪文件且本轮未修改**（保持原样、不在本轮改动内）。

**暂存后核对（提交前实测）**：`git status` 除上述 gitignored 目录外，**无任何未暂存 / 未跟踪项**；
`git diff --cached` 范围与上表逐项一致（无 Scene / Prefab / SO / ProjectSettings / Packages / GameSpec / Save 改动）。

### 11.5 结论：M3 技术收口是否成立

**成立。** 判据：

1. **Authority 与消费链成立**：`MonsterId → EnemyPresentationResolver → Sprite` 是敌方视觉的唯一取图路径；
   Prep 与 Battle 共用同一 Resolver 实例（`BattleUI.cs:435` / `BattleUIUnitVisualHosts.cs:78`），
   业务层无 fallback 判断、旧 `enemyBodyPlaceholder` 零读取（§7.8）。
2. **全量回归成立**：EditMode **752/752**、无过滤 PlayMode **71/71**（35.7 s），且 M4-6 的挂起根因已定位并通过无过滤 PlayMode 全量两次验证关闭（§7.4 / §7.5B）。
3. **画面成立**：真实 Game View 整屏截图证明三只怪显示三张不同图，无裁切/遮挡问题（§7.7）。
4. **未完成项性质明确**：剩下的只有**内容**（最终美术）、**后续清理**（M4 物理删除）与**既有排版**（Prep 文字重叠），
   均不影响 M3 的技术结论，且都不需要改动本轮的 Authority 设计。

> 交付口径：M3 = **技术收口成立 / Ready for Commit & Review**；**尚未 commit**，等待用户对本报告的确认。

### 11.6 提交前可复现性核对（M3 是否能从持久化输入重建）

**结论：能。** M3 的权威输入、代码与 Canonical Mirror 全部进入本轮提交，可从 Git 独立重建，不存在「只有当前机器才能重建 M3」的情况。

| 重建环节 | 持久化位置（在本轮提交内） | 在 Git |
|---|---|---|
| 权威配置（1 怪 1 图） | `Development/Config/RRPG_EnemyPresentation.xlsx`（**已跟踪**，本轮更新） | ✅ |
| 人工源图（3 张临时图） | `Development/SourceAssets/Enemy/enemy_mon_00{1,2,3_wolf}.png` | ✅（本轮新增跟踪） |
| Canonical Mirror | `Development/Generated/StaticConfig/{EnemyPresentation,VisualAsset,AssetManifest}.json` | ✅（本轮新增跟踪） |
| Compiler（XLSX → Mirror） | `Tools/RRPG.ConfigCompiler/src/**` + `tests/**` + 根入口 `RRPG_一键导表.cmd` / `RRPG_检查配置.cmd` / `Tools/RRPG.ConfigCompiler/verify-toolchain.cmd` | ✅ |
| Builder（Mirror → 生成根 + managed Sprite） | `UnityProject/RRPG1.0game/Assets/Editor/StaticConfig/**` | ✅ |
| Runtime 消费 | `Assets/Scripts/StaticConfig/**`、`Assets/Scripts/UI/BattleUI.cs` / `BattleUIUnitVisualHosts.cs` | ✅ |
| 生成根（**故意**可重建而不是留档） | `Assets/Generated/StaticConfig/**` | ❌ gitignored（由 Builder 重建） |
| 验收画面证据（留档） | `Docs/DevelopmentSpecs/Evidence/2026-09-23_M3-Enemy-Presentation-Authority/` | ✅（本轮新增跟踪） |

* **XLSX 按项目既有规则入 Git**：`Development/Config/*.xlsx` 均为已跟踪文件（`RRPG_Monsters.xlsx`、`RRPG_EnemyPresentation.xlsx`），
  没有「XLSX 不入 Git」的例外，因此无需为 M3 记录仓库外的唯一副本位置。
* 唯一**本机专属**项是编辑器可执行文件路径（`RRPG_TUANJIE_EXE` 环境变量，或 `Tools/RRPG.ConfigCompiler/Producer/editor-path.local.txt`；
  后者按设计 gitignored，并在 `Producer/README.md` 说明）——属运行环境而非配置输入，不阻塞重建。
* 重建路径（生产侧）：`RRPG_一键导表.cmd` = Compiler build（XLSX → Mirror）→ Unity Builder（生成根 + managed Sprite，按 `sprite_point_100` 导入）→ `validate`；
  只读检查用 `RRPG_检查配置.cmd`（换图未重新导表会报 `MIR002_MIRROR_STALE`）。
* 统计与哈希（供复核）：
  * XLSX SHA256 `a3f043dd7a6e4e4d0c8fe622561e14023e7fbca2cb1b8843ad1e5bb85808c6a6`
  * 源图 SHA256 `62befdfd…b5efcf`（`mon_001`）、`9e27a32d…f954150`（`mon_002`）、`909f9347…1f7220d`（`mon_003_wolf`）
  * Game View 截图 SHA256 `647BF8FB…743D1AB2`（Prep）、`49E9E66E…61EDA1DA9`（Battle）
* **注意（证据留存边界）**：§7.4 / §7.5B 引用的全量日志与汇总位于 `Tools/RRPG.ConfigCompiler/.build-state/`，
  该目录是**一次性证据**（会被 Compiler `build` 的 stale 回收清空）且 gitignored，**不随提交保留**；
  长期留档以本报告的实测记录 + `builder.stage.txt` 要点摘录（§7.2 / §7.8）+ 仓库内画面证据（§7.7）为准。

### 11.7 项目级状态文档同步（docs-only 提交）

M3 Closure Commit `24b8470` 之后，按仓库治理**新增一次 docs-only 提交**同步项目级状态文档
（**未** amend / **未** reset `24b8470`、**未** Push、**未**改代码；本次提交 hash 见 `git log -1`，按仓库惯例不回填未来 SHA）：

| 文档 | 本轮增量（只记 M3 确实需要记录的部分，不重复整份报告） |
|---|---|
| `Docs/项目状态快照.md` | 顶部新增 M3 冻结事实块（配置面 / Authority 链 / 消费口径 / 验证证据 / 遗留）；顶部事实表新增「静态配置迁移当前工作分支」行；§一 项目阶段表新增静态配置迁移 M3 行 |
| `Docs/项目日志.md` | 新增 2026-09-23 M3 条目（任务性质 / 开发基线 / 配置面与链路 / 验证证据 / 边界与遗留 / Git 事实） |
| `Docs/开发验收交接.md` | Handoff 升级至 v38（基本信息 / 实际变化 / 开发侧验证证据 / 未验证·遗留·待决策 / 正式文档影响 / Git 事实 / 完整性声明） |
| `Docs/版本更新记录.md` | 新增 2026-09-23 内部开发里程碑条目（敌方表现 Authority 转移） |

**四份文档的统一状态口径（与 §11.4 一致）**：M3 = **Acceptance Passed / 本地 Closure Commit 完成 / Push Pending（未授权、未执行）**；M4 未启动。
**远端事实（本轮实测）**：`origin/main = 57edf6f`、`origin/migration/static-config-m1 = 34f42aa` → M3 提交 `24b8470` **仅存在本地**。
本次 docs-only 同步**不改变** M3 的冻结事实、验证结论或 §11.4 的提交范围。
（Push 完成后的事实见 §11.8；本节「Push Pending」是本次同步当时的真实状态。）

### 11.8 Repository 关闭事实（Push 完成 + 远端核对）

**2026-09-23 同日补记（Push 成功后）**：M3 的两次提交已 Push 到 `origin/migration/static-config-m1`，M3 状态从
「Acceptance Passed / 本地 Closure Commit 完成 / Push Pending」转为 **ACCEPTED / REPOSITORY CLOSED**。
§11.4 的提交范围、§11.5 的技术结论与 §11.6 的可复现性核对**均不变**；§11.7 与 Handoff v38 中的
「Push 未授权、未执行」是 Push 前的真实历史状态，保留不重写。

| 项 | 实测事实 |
|---|---|
| Push 形态 | fast-forward（无 force、无 rebase、无 amend / reset）：`34f42aa → 24b8470 → f22b40a` |
| Push 内容 | M3 Closure Commit `24b8470`（56 files / +4710 −70）+ docs-only `f22b40a`（`docs(config): sync M3 closeout report and project status documents`，5 docs / +120 −62） |
| 分支 | `migration/static-config-m1` → 远端 `origin/migration/static-config-m1` |
| 远端核对（实测） | `git ls-remote origin refs/heads/migration/static-config-m1` = `f22b40a560c4b66a98b1a811b9834c10b38f022d` == 本地 HEAD |
| 跟踪引用 | `git fetch` 后 `origin/migration/static-config-m1` = `f22b40a`；`git rev-list --left-right --count origin/migration/static-config-m1...HEAD` = `0 0` |
| 工作区 | `git status --porcelain -uall` 为空（tracked clean、无 untracked 残留） |
| 未触碰 | `main` / `origin/main = 57edf6f` 未变；未改代码、资产、生成物、`.gitignore`；**M4 未启动** |
| 环境事实（不属提交内容） | 本机 `~/.gitconfig` 的 `http.proxy` 随 VPN 端口轮换（当日实测 `127.0.0.1:15833`）；此前的 Push 尝试因代理端口与监听端口不一致在**预检阶段中止**（`RESULT=ABORT_PRECHECK_FAILED_NO_PUSH`，未执行任何 push），端口对齐后一次成功 |

> 交付口径：M3 = **ACCEPTED / REPOSITORY CLOSED**（技术验证数字与冻结事实见 §7、§11.5；遗留项 M3-R1…R10 按 §9 归属 M4 / UI 版式工作）。
