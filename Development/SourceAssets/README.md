# Source Assets — Development/SourceAssets

美术 / 音频等**源资产**的正式根（D-019 的 `Source Art` 是其已验证子类）。

## 规则

* 只放**源**（例如 `Enemy/*.png`、未来的音频源），**不放** Unity 导入产物；
* **不得**进入 Unity `Assets/`，**不得**被 Runtime 直接依赖：源资产 → Compiler/Builder → 生成资产；
* 生成的（managed）资产落在 `UnityProject/RRPG1.0game/Assets/Generated/StaticConfig/`（**不进 Git**）；
* 源资产的**内容哈希**进入 Canonical Mirror（`AssetManifest.json`），因此换图 = 需要重新 build。

## M3 状态（已接入 Battle / Prep 表现链）

`Enemy/` 下是**每只怪物一张的临时源图**（不视为最终美术；用于证明不同 MonsterId → 不同 Sprite）：

| 源图 | 用途 | 外观 |
|---|---|---|
| `Enemy/enemy_mon_001.png` | `mon_001`（`va_enemy_mon_001`） | 24×24 红底 + 白标记 |
| `Enemy/enemy_mon_002.png` | `mon_002`（`va_enemy_mon_002`） | 32×32 绿底 + 深色标记 |
| `Enemy/enemy_mon_003_wolf.png` | `mon_003_wolf`（`va_enemy_mon_003_wolf`） | 40×40 蓝底 + 白标记 |

* 命名建议：`<用途>_<对象>.png`（例如 `enemy_mon_001.png`），全部小写 + 下划线；
* 路径必须写成**相对本目录**的形式（例如 `Enemy/enemy_mon_001.png`），不得使用绝对路径或 `..`；
* `ImportPreset` 目前只支持 `sprite_point_100`（Point / 100 PPU / Center；见 Compiler 的 `VisualImportPresets`）。

> **Slice 1B/1D-1 已落地**：Unity Builder 依据 `AssetManifest.json` 把源图**逐字节复制**到
> `UnityProject/RRPG1.0game/Assets/Generated/StaticConfig/Assets/<SourceFile>` 并按预设导入为 Sprite
> （生成物不进 Git）；`BattleUI` 的敌方视觉（Prep + Battle）已经通过 `EnemyPresentationResolver` 消费它。
> 本目录仍是**唯一**人工源图存放处：换图只改这里，然后重新 build（换图 = 必须重新 build）。
>
> 退役记录：旧的单一共享源图 `Enemy/enemy_body_placeholder.png` 已删除（每个 MonsterId 现在有自己的绑定）。
