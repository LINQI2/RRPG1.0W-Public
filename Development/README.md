# Development 文档说明

> 当前架构入口最后核对：2026-10-06（日内A～D已发布；同一配置链覆盖AutonomousLife / Social，逐域混合来源保留）
> 本文件是活动技术入口。技术任务先读本文件；详细字段和历史设计必须按下方状态使用。
>
> 历史技术资料已于2026-09-09移出活动仓库。需要追溯时通过Git历史或仓库外个人归档查看；这些资料不是Current事实源或开发Authority。

## 1. 当前代码目录

路径：`UnityProject/RRPG1.0game/Assets/Scripts/`

| 目录 | 当前职责 |
|---|---|
| `Adventure/` | 远征选择、地图生成与节点、冒险流程、战斗结算 |
| `Battle/` | 战斗数据、战场、单位、回合和核心战斗流程；当前玩家规则见 `GameSpec/Battle/Battle-GameSpec-V0.1.md` |
| `Card/` | 卡册、出征牌组、战前部署区、手牌、抽牌堆和弃牌堆 |
| `Character/` | CharacterInstance 与 HeroInstance 运行时成长数据 |
| `Core/` | DataManager、PlayerCollection、事件总线、公共事件与枚举 |
| `Encounter/` | 纯逻辑遭遇生成：Pool + Depth + Seed → EnemyRoster；Encounter.Runtime 不依赖产品 UI，但产品适配已让 Battle / Prep 消费冻结阵容，不再停在历史 1B |
| `Event/` | 纯逻辑事件识别、条件、选项/结果/效果、模板与持久化 DTO；Event.Runtime 不依赖具体 Unity / Save / 产品 UI |
| `EventHost/` | Assembly-CSharp 的 Village / Expedition 产品适配、WorldView / EffectGateway、EventRuntimeBridge 与表现投影；已接产品与 Save |
| `Growth/` | 长期人物成长查询、兑现与配置；Growth.Runtime 与产品适配分层，按 Person-owned 数据写入 |
| `Journey/` | JourneyPresentationCycleController 表现周期；不产生规则时间，不拥有 Progression / Supply |
| `Person/` | 人物定义、开局职业配置与 PersonBootstrapper；配置身份与具体 PersonId 分开 |
| `Profession/` | 职业进度、经历形成与 FormationResolver；不按角色模板反推长期职业 |
| `Social/` / `SocialHost/` | 纯Social事实/有向关系与Social-owned解释器；宿主/配置/保存接缝 |
| `AutonomousLife/Activity/` / `ActivityHost/` | 纯Activity Window / Claim / FSM；日内计划、到场/完成、最小Presence与Save |
| `AutonomousLife/Selection/` / `AutonomousLifeHost/` | Snapshot / Desire / Weighted Order / Arbitration；Host只读世界投影，日内机会exactly-once |
| `AutonomousLife/SocialFeedback/` / `AutonomousLifeHost/` | 独立Activity↔Social integration / Durable Outbox / pre-next-Snapshot Barrier |
| `Save/` | P0 SaveData DTO 与本地 JSON 单存档 SaveSystem |
| `Shared/` | RRPG.Deterministic.Runtime：共享确定性随机原语，无引擎依赖 |
| `Skill/` | 技能执行与效果入口 |
| `Status/` | 状态运行时和状态系统 |
| `StaticConfig/` | 生成配置 Registry / Database、EnemyPresentationResolver 与共享 VisualAssetCatalog |
| `UI/` | 主界面、领地、远征、地图、战斗、商店、招募、事件和结算表现；BattleUI 使用 partial 文件拆分表现职责 |
| `Village/` | VillageRuntimeBridge 与 Runtime 的时间、资源、安排、项目、World、返乡后果；产品 UI 不另建 Simulation Authority |

Monster Definition 已由 `Development/Config/RRPG_Monsters.xlsx` 经 Compiler、Unity Builder、`StaticConfigRegistry` 进入产品 Runtime；Enemy Presentation 由 `RRPG_EnemyPresentation.xlsx` 与 `Development/SourceAssets/Enemy/*.png` 经同一构建链进入 Resolver。M0–M4 已进入正式共享 `main`，这是 Monster / Enemy Presentation 的 Current 技术入口；Monster Legacy CSV / per-entry SO 已退休。

共享图片资源现在由 `RRPG_VisualAssets.xlsx / VisualAsset` 单独维护，EnemyPresentation 只引用 VisualAssetId。Event 内容来自 `RRPG_Events.xlsx`，经同一 Compiler / Mirror / Builder 进入 EventConfigDatabase；Event Core 已通过 EventHost 接入 Village / Expedition、正式文本投影和 EventWorld 存档。旧 1A / 1B “尚未接产品”只描述当时阶段，不是当前实现。

Encounter（遭遇生成）静态配置由 `Development/Config/RRPG_Encounters.xlsx`（+ `RRPG_Monsters.xlsx` 的两张 Encounter 子表）经同一 Compiler / Mirror / Builder 链进入 `EncounterConfigDatabase`（WP-Encounter-1A）；纯逻辑生成器位于 `Assets/Scripts/Encounter/`（WP-Encounter-1B，只读静态配置）。**WP-Encounter-1C / 1C-R 起产品已切换**：Battle / Prep 消费 AdventureManager 冻结的 `Active EnemyRoster`（`EnemyInstanceId` = BattleUnit 本场身份，`MonsterId` = 模板身份），不再把整个 Monster Registry 当本场敌人、也不再只部署前 3 只；Battle 类节点采用**进入原子性**（预检 → 预生成 → Enter → 提交），生成失败时节点不进入、Progression 零 mutation。`DataManager` 与 CSV/SO 流程仍服务其它尚未迁移领域，具体操作须按当前代码和数据逐域核对；2026-06-28 的历史 CSV/SO 手册不再作为当前怪物制作入口。

## 2. 当前技术资料状态

| 路径 | 导航与编辑边界 |
|---|---|
| `Config/README.md`、`Config/*.xlsx` | Monster / Enemy Presentation / VisualAsset / Encounter / Event / AutonomousLife / Social操作入口与人工表格数据 |
| `SourceAssets/README.md`、`SourceAssets/Enemy/*.png` | 源图说明与可人工替换的敌方源图 |
| `Generated/StaticConfig/README.md`、`Generated/StaticConfig/*.json` | 生成镜像说明与只读结果；JSON 不人工编辑 |
| `Excel/*.csv` | 其它尚未迁移领域的现有数据；逐域核对代码与来源，不套用怪物新链 |
| `../Tools/RRPG.ConfigCompiler/README.md` | 当前配置 Compiler、构建与校验工具说明 |
| `../UnityProject/RRPG1.0game/` | 游戏工程、Runtime 与 Unity 生成资产；生成资产不人工编辑 |

当前新链为 `Config` 工作簿 + `SourceAssets` 源图 → `Tools` 构建／校验 → `Generated` JSON 镜像 → Unity Builder 与 Runtime；旧 CSV/SO 链只在逐域核实后用于尚未迁移内容。

| 文件 | 状态 | 使用方式 |
|---|---|---|
| `Config/RRPG_Monsters.xlsx`、`Config/RRPG_EnemyPresentation.xlsx`、`Config/RRPG_VisualAssets.xlsx`、`SourceAssets/Enemy/*.png` | Monster / Enemy Presentation / 共享视觉资源当前人工源 | 按 `Config/README.md` 检查、构建；生成镜像不人工编辑 |
| `Config/RRPG_Encounters.xlsx`、`Config/RRPG_Events.xlsx` | Encounter / Event 当前人工源 | 规则与未冻结内容另查 Authority；生成镜像不人工编辑 |
| `Excel/*.csv`（不含已于 M4 删除的 `Monster.csv`） | 其它未迁移领域的现有来源，逐域核对 | 不使用 2026-06-28 的旧手册推断当前玩法或全部导入路径 |

## 3. 技术资料阅读入口

| 要查什么 | 现有入口 |
|---|---|
| 整体职责、依赖、数据来源与持久化边界 | [技术架构总览](../Docs/Architecture/技术架构总览.md) |
| 具体对象、标识、生命周期与易混术语 | [核心对象与术语映射](../Docs/Architecture/核心对象与术语映射.md) |
| 程序集依赖、产品调用链及 Event / Encounter 接缝 | 技术架构总览 §17、§21、§22；两份 Architecture 都基于同一套实现事实 |
| 本地工程整理的实施范围、名称映射和验证证据 | [优化总结与验收入口](../Docs/DevelopmentSpecs/2026-10-04_RRPG_架构优化总结与验收清单.md)、[工程审计与实施记录](../Docs/DevelopmentSpecs/2026-10-02_RRPG_工程现状与引用审计.md)；已获收尾发布授权，不是新的长期 Architecture |
| 已迁移领域的配置制作步骤 | [Config/README.md](Config/README.md)、[源图说明](SourceAssets/README.md)、[生成镜像说明](Generated/StaticConfig/README.md) |
| 构建与校验工具 | [Config Compiler 说明](../Tools/RRPG.ConfigCompiler/README.md) |
| 命名、代码、Unity 与开发执行规范 | [工程规范](../Docs/工程规范.md)负责持久工程原则；[根规则](../.clinerules.md)与[AI开发执行规则](../Docs/AI开发执行规则.md)负责 Agent 工作流 |

技术说明沿用上述两份 Current Architecture，供制作人与 AI 按问题查阅；原先的专题拆分设想不再作为创建新架构文档的待办。需要补充对象或数据结构说明时，先确认对应代码事实和现有章节职责，再更新已有入口。

公共数据结构、SaveData、SO 字段或核心接口变化仍需用户明确授权。发现文档与代码不一致时，先记录差异，不自动改变规则或数据结构。

当前日内劳动、机会、到场与完成均由VillageTime唯一AdvanceSink编排；代码职责详见[Current Architecture](../Docs/Architecture/技术架构总览.md)，人工参数详见[Config手册](Config/README.md)。D现场面板是最小产品交互，完整空间表现尚未实现，开发16.0方向见[当前交接](../Docs/开发验收交接.md)。
