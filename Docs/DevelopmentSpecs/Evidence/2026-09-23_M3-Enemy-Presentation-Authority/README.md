# M3 Enemy Presentation Authority — 验收画面证据（仓库内保留）

> 用途：保存 M3 的**真实 Game View 整屏截图**与来源标注，使其随提交长期保留（`UnityProject/RRPG1.0game/screenshots/**` 是 gitignored 的本机证据目录，不能承担仓库留档）。
> 关联报告：`Docs/DevelopmentSpecs/2026-09-23_RRPG_M3-Enemy-Presentation-Authority-Cutover-Closeout-Report-V0.1.md`（§7.6 / §7.7 / §11）。
> 本目录不承担生成物职责：只有重新执行该验收测试才更新，不手动编辑。

## 1. 证据清单

| 文件 | 大小 | SHA256 | 说明 |
|---|---|---|---|
| `m3_prep_gameview.png` | 154,946 B | `647BF8FB5A63F72925F795253D3B753C07760C518239296616EE1856743D1AB2` | **Prep（战前准备）整屏截图** |
| `m3_battle_gameview.png` | 229,494 B | `49E9E66EF0675C4FF311707924B2723B5A54963E5DE357D51D2A56111EDA1DA9` | **Battle（战斗）整屏截图** |
| `m3_prep_gameview.png.provenance.txt` | 109 B | — | 测试原始标注（Prep），逐字节保留 |
| `m3_battle_gameview.png.provenance.txt` | 111 B | — | 测试原始标注（Battle），逐字节保留 |

两张 PNG 与产出时的本机证据文件**字节一致**（SHA256 逐项相同），仅做了目录搬迁。

## 2. 来源标注（Provenance）

* **口径**：Game View 整屏截图（真实渲染窗口画面），**不是**离屏合成图。
* **产出方式**：**非 `-batchmode`**（带显示器的编辑器会话）启动团结引擎 `2022.3.62t9`，用一次性 Gate Runner（`M2DisposableGateRunner`）执行 PlayMode 测试
  `BattleVisualFoundationPlayTests.EnemyPresentation_PrepAndBattle_ResolveFromAuthority`（真实产品链：Town → Expedition → Battle/Prep）。
  测试内 best-effort 调用 `ScreenCapture.CaptureScreenshot` + 帧计数等待，并写同名 `*.txt` 标注是否真的产出
  （`Game View screenshot PRODUCED` / `NOT produced`）——用于区分「整屏截图」与「离屏合成」两种证据。
* **原始（本机、gitignored）位置**：`UnityProject/RRPG1.0game/screenshots/WP-D/m3_prep_gameview.png`、`…/m3_battle_gameview.png`。
* **重要限制**：`-batchmode`（无 Game View）下 `ScreenCapture` **不产出文件**，因此这条证据**必须在有显示器的编辑器会话**中生成；
  CI / headless 无法复现整屏截图（只能产出第 3 节的离屏合成图）。

## 3. 与「离屏合成图」的区别（两种口径不要混用）

| 口径 | 文件 | 位置 | 用途 |
|---|---|---|---|
| **Game View 截图**（本目录，进 Git） | `m3_prep_gameview.png` / `m3_battle_gameview.png` | `Docs/DevelopmentSpecs/Evidence/2026-09-23_M3-Enemy-Presentation-Authority/` | 整屏**人工视觉验收**（可见性 / 裁切 / 缩放 / 遮挡） |
| **运行时 Sprite 赋值证据**（gitignored） | `m3_prep_enemy_presentation_runtime.png` / `m3_battle_enemy_presentation_runtime.png`（+ 同名 `*.txt` 标签） | `UnityProject/RRPG1.0game/screenshots/WP-D/` | 只证明 Runtime 实际写入 `Body_Visual` 的 Sprite 是什么（`mon_00x → 资产`对应关系） |

## 4. 复现步骤（可独立重跑）

1. 关闭该工程的编辑器窗口（避免 `Temp/UnityLockfile` 冲突）。
2. 以**非 batchmode** 启动编辑器并驱动测试（示例）：
   `Tuanjie.exe -projectPath <repo>\UnityProject\RRPG1.0game -executeMethod M2DisposableGateRunner.Run -m2TestMode PlayMode -m2TestFilter BattleVisualFoundationPlayTests.EnemyPresentation_PrepAndBattle_ResolveFromAuthority -m2Summary Tools/RRPG.ConfigCompiler/.build-state/m3f-gui-summary.txt -logFile <log>`
   （去掉 `-batchmode` 就是本条证据与无过滤全量的唯一环境差异。）
3. 检查 `UnityProject/RRPG1.0game/screenshots/WP-D/m3_prep_gameview.png(.txt)` 与 `…/m3_battle_gameview.png(.txt)` 是否产出且标注为 `PRODUCED`。

## 5. 本轮人工核对结论（摘要）

* 三张临时敌方图在 **Prep 与 Battle 均可见且互相可区分**（红+白标记 / 绿+深色标记 / 蓝+白标记）。
* 未见明显**裁切**，未见**异常缩放**（`PreserveAspect` + `PPU=100`，三者显示尺寸接近一致，属预期）。
* 一项**既有**观察（非 M3 引入、本轮未修改）：Prep 敌方卡列较窄，HP 数值与名字有轻微文字重叠 → 已记为 `M3-R8`，留给 UI 版式工作。
* 正式美术到位后需重做一次**生产级人工视觉验收**（本目录图对应的是临时占位图，见 `M3-R1`）。
