# RRPG Static Configuration Migration FDS V0.1

> 日期：2026-09-22
> 性质：**Migration Plan / Implementation Contract（FORMAL DEVELOPMENT SPEC；DOCUMENT ONLY）**
> Authority：**D-019**（`Docs/决策记录.md`｜RRPG Static Configuration Production Architecture）
> 基线：**main @ `61e1e57f73276b2d5c2910cdc4ff0a26ebdba9a0`**
> Spike Evidence（**只读参考，不 merge**）：`spike/config-architecture-a` @ `3532351d292463b1c3323784aecaa840f87d8e14`
> 本文件**不是**新的 Architecture Decision；**不实现**、**不执行**迁移。DevelopmentSpecs 只承担**开发期 Binding**；长期 Authority 仍是 D-019 与两份 Current Architecture。

---

# 0. Migration Reality Audit（只读，2026-09-22，以当前 main 为准）

> 本节**不照抄** Spike T0；全部结论来自当前 main 的真实代码 / 资产。

## 0.1 Current Monster Authoring 现状（含隐藏 Authoring 源）

| # | 来源 | 位置 | 说明 |
|---|---|---|---|
| A1 | **CSV（当前正式人工源）** | `Development/Excel/Monster.csv`（188 bytes） | 表头 `MonsterId,Name,MaxHP,Attack,AttackRange,AttackCount,ActionPoints,Faction`；3 行：`mon_001 史莱姆 20/4/1/1/1/enemy`、`mon_002 骷髅弓手 15/5/2/1/1/enemy`、`mon_003_wolf 野狼 24/5/1/1/1/enemy` |
| A2 | **ExcelImporter（Editor 工具）** | `Assets/Editor/ExcelImporter.cs` → `ImportMonsters()`（:463-488）；`ImportAll()`（:24-47） | 读 `Development/Excel/*.csv` 全量导入；Monster 分支写 per-entry SO：`{MonsterId}_{Name}.asset`，目录 `Assets/Resources/ScriptableObjects/Monsters` |
| A3 | **per-entry SO（生成物）** | `Assets/Resources/ScriptableObjects/Monsters/` | 磁盘上 **3 个**：`mon_001_史莱姆.asset`、`mon_002_骷髅弓手.asset`、`mon_003_wolf_野狼.asset`（各带 `.meta`） |
| A4 | **Type** | `Assets/ScriptableObjects/Monsters/MonsterData.cs` | `MonsterData : ScriptableObject`；字段 = `MonsterId / Name / MaxHP / Attack / AttackRange / AttackCount / ActionPoints / Faction`（**8 字段**） |
| A5 | **Runtime 读取入口** | `Assets/Scripts/Core/DataManager.cs` | `AllMonsters = Resources.LoadAll<MonsterData>("ScriptableObjects/Monsters")`（:83）+ `_monsterMap`（:63）→ `GetMonsterById(id)`（:233）。`DataManager` 为**惰性单例**（:20-29，构造函数 `LoadAll()`），**无显式启动钩子** |
| A6 | ⚠️ **隐藏 Authoring 源 1** | `Assets/Editor/GameDataInitializer.cs`（菜单 `Tools/初始化游戏数据`，:14-163） | 在 C# 中**内联创建** `MonsterData`：`mon_001 20/4/…`（:64-69）、`mon_002 15/5/…`（:71-76）→ 数值与 CSV **当前一致**，但这是**第二条人工 Authoring 路径** |
| A7 | ⚠️ **隐藏 Authoring 源 2** | `Assets/Scripts/UI/BattleUI.cs` `SetupDemoData()`（:2000-2047） | :2004 正式路径 `_enemyMonsters = DataManager.Instance.AllMonsters`；若 DataManager 为空 → :2043-2047 **内联创建** `mon_001 / mon_002`（演示回退） |
| A8 | 相邻但**不同域** | `Assets/Scripts/Battle/Stage1/EnemyBehaviorDemoConfig.cs`（:14-71） | `MonsterRules`（`MonsterId → 行为规则`）为**硬编码 C# 演示 AI**（`GetRules(monsterId)`）；**不是**静态属性配置 → Wave 1 不迁移，但登记为**相邻硬编码源** |

**结论（A）**：Monster 静态属性当前存在 **3 条可写路径**（CSV / `GameDataInitializer` / `BattleUI` demo 内联）+ 1 条生成物路径（per-entry SO）。
→ **Shadow 阶段必须显式声明「XLSX 不是第二人工 Authority」；Cutover 前 CSV 仍是唯一人工 Authority**（见 §10）。

## 0.2 Current Monster Consumer Map（真实，逐条来自当前 main）

| # | Consumer | 位置 | 旧依赖（今天） | 备注 |
|---|---|---|---|---|
| C1 | `ExpeditionUI` | `Assets/Scripts/UI/ExpeditionUI.cs:495 / 502-503` | `DataManager.Instance.AllMonsters` → `new AdventureManager(..., allEnemyMonsters)` | **产品链第一处读取** |
| C2 | `AdventureManager` | `Assets/Scripts/Adventure/AdventureManager.cs:56 / 65 / 73 / 446` | 构造参数 `List<MonsterData> enemyMonsters`，仅**持有 / 透传**（`GetEnemyMonsters()`） | 不读静态字段 |
| C3 | `ExpeditionUI`（战斗入口） | `ExpeditionUI.cs:603-610` | `adventure.GetEnemyMonsters()` → `battleUI.InitFromExpedition(chars, hero, charDatas, skills, monsters, _playerData)` | 战斗前唯一入口 |
| C4 | `BattleUI`（Prep 展示） | `BattleUI.cs:93 / 128 / 405-420` | `_enemyMonsters` 列表；`m.MonsterId` 用于 `EnemyPrep_` 卡名，`Name/HP/ATK` 写 Text | 战前敌方卡面 |
| C5 | **`BattleSystem`（字段消费者）** | `BattleSystem.cs:48-49 / 67-69 / 518-529` | `InitializeBattle(..., List<MonsterData> enemyMonsters, ...)` → `CreateMonsterUnit(MonsterData monster)` **逐字段读 8 字段** → `BattleUnit{UnitId, SourceId=MonsterId, UnitType=Monster, MaxHP, Attack, AttackCount, AttackRange, MaxActionPoints, Faction}` | **Cutover 核心点** |
| C6 | `BattleUI`（敌方名字） | `BattleUI.cs:1646` | `_enemyMonsters.Find(m => m.MonsterId == u.SourceId)` | 按 `SourceId` 反查 |
| C7 | `BattleUIDisplayNames` | `BattleUIDisplayNames.cs:20` | 同上（partial class 共享 `_enemyMonsters`） | 敌方显示名 |
| C8 | `BattleUI.SetupDemoData`（兼容/演示） | `BattleUI.cs:2004 / 2043-2047` | `DataManager.AllMonsters` 或内联 demo | 非产品链，但会读 |
| C9 | `BattleSystem`（规则判定） | `BattleSystem.cs:574` | `target.UnitType == UnitType.Monster` | 仅类型判定 |
| C10 | `TurnSystem` / `DeathDoorSystem` | `TurnSystem.cs:92`（注释）、`DeathDoorSystem.cs:76` | 仅类型 / 注释 | 非静态配置消费者 |
| C11 | Editor 侧 | `Assets/Editor/{ExcelImporter,GameDataInitializer,DataValidationTool}.cs` | 生成 / 校验 Monster SO | 工具链，非 runtime |

> **重要事实**：`GetMonsterById`（按 ID 查询）在 **runtime 没有任何调用者**（仅 `DataManager` 定义 + 测试）→ 今天**唯一**真实 runtime 路径是 **`AllMonsters` 列表**。

## 0.3 Bootstrap / Initialization Order（现状）

```text
[New Game]  PersonBootstrapper（PersonDefinitionCatalog → 长期 CharacterInstance）
        ↓
[Town]      Village / ExpeditionUI 等
        ↓
ExpeditionUI.OnConfirm
    → DataManager.Instance            （惰性单例；首次访问触发 Resources.LoadAll）
    → AllMonsters / AllCharacters / AllSkills
    → new AdventureManager(..., allEnemyMonsters)
    → StartProgression(...)
        ↓
节点 = Battle / Elite / Boss
    → battleUI.SetRunState(...) → InitFromExpedition(..., monsters, ...)
    → BattleUI.ShowPrepPhase()          （第一次使用 _enemyMonsters）
    → btnStartBattle → BattleSystem.InitializeBattle(..., _enemyMonsters, ...)
        ↓
BattleSystem.CreateMonsterUnit(monster)    ← 逐字段读 MonsterData（8 字段）
```

**约束（决定新 Static Config Root / Registry 的初始化位置）**：

1. `DataManager` **没有显式 boot**：任何初次访问都会触发加载 —— 潜在首发点包括 `AdventureManager.SeedRunState:108`、`ExpeditionUI:493-495`、`BattleUI:2000-2005`。
2. **Battle / Adventure 第一次消费 Monster 的最早点 = `ExpeditionUI.OnConfirm`（C1）**；更早的触发点是 `BattleUI.SetupDemoData`（演示）及其测试。
3. → 新 **Static Config Root / Registry 必须支持「首次访问即就绪（lazy-or-boot）」**；若采用显式 boot，必须**早于** `ExpeditionUI.OnConfirm`，并且**不得**依赖 Editor / `AssetDatabase` / 场景对象搜索（**Player build 必须可用**）。
4. 当前**没有**「必须切场景 / 必须 New Game」才能读 Monster 的强约束（除 Scene 上可能存在的 Inspector 手工引用 —— Wave 1C 需逐点核对）。

## 0.4 Enemy Visual 现状

* **敌方视觉 = 单一占位 Sprite**：`BattleUIUnitVisualHosts.cs:36 public Sprite enemyBodyPlaceholder;`（Inspector 赋值的**全员共用**占位图）、`:54 enemyBodyScale`；应用点 `:97-102`（`isPlayer ? playerBodyPlaceholder : enemyBodyPlaceholder` → `Body_Visual`）。
* **宿主稳定**：单位卡结构 = `UnitCard → Anchor_Visual → Body_Visual`（同文件 :149 注释）；`ApplyUnitBodyVisual(card, unit)` 与 `ApplyUnitBodyVisual(card, bool isPlayer)` **已存在** → **Wave 1D 只需改「取哪张 Sprite」，不需要新宿主**。
* **MonsterId 在 Battle UI 链可取**：`BattleUnit.SourceId = MonsterId`（`BattleSystem.cs:522`），已用于 `_enemyMonsters.Find(m => m.MonsterId == u.SourceId)`（`BattleUI.cs:1646`、`BattleUIDisplayNames.cs:20`）。
* **fallback 行为（今天）**：`enemyBodyPlaceholder == null` 时**静默保持原 Sprite**（无诊断）→ Wave 1D 必须改为**明确 fallback + 可诊断**，不得静默。
* 我方已有完整视觉链（`PersonId → AppearanceKey → PersonVisualBindingCatalog → surface override → DefaultBodySprite`，见 `PersonVisualResolver.cs`）；**敌方没有**对应链 → 这是 Wave 1D 的净新增。

> **M4 更新（2026-09-23）**：本节是 2026-09-22 的**只读现状快照**，其中 `enemyBodyPlaceholder` 相关事实已被取代 ——
> 该字段自 M3 起**零读取**，并由 **M4 物理删除**（含 `SampleScene` 序列化条目）；敌方视觉唯一取图路径 =
> `MonsterId → EnemyPresentationResolver → Sprite`，fallback 在 Resolver 内部（见 §9 Wave 1D 的 M3/M4 修订与 §19.2 Q3）。

## 0.5 Save 结论（Hard Stop 判定）

* `Assets/Scripts/Save/SaveData.cs` 头部即声明：**"Stores only long-term player collection data. ScriptableObject references, battle state, map progress, UI state, and DataManager caches are not saved."**
* 字段：`SaveVersion / GameVersion / SavedAtUnixTime / Player(PlayerCollectionSaveData) / Village(VillageSaveData)`；`Player` = `Gold / Heroes / Characters / OwnedCharacterCardIds / OwnedItemCardIds / OwnedCharacterCardOwners`。
* 对 `Assets/Scripts/Save/**` 搜索 `Monster|MonsterId` → **0 命中**；`SaveData.cs` 中**无** `RunState / Enemy / ScriptableObject` 字段。
* 敌方编成（`_enemyMonsters`）只存在于**会话 Runtime**（`AdventureManager` / `RunState`），**不进入 Save**。


---

# 1. Migration 总原则

## 1.1 Domain-by-domain migration

**不得**全项目一次重写。每一个配置领域**独立**经历：

```text
Shadow → Equivalence → Runtime Cutover → Product Verification → Legacy Retirement
```

**一个领域完成（含 Legacy 退役与 Closeout）后，才允许开始下一个领域。**

## 1.2 Single Authoring Authority

* 任何正式阶段：**同一个字段不得长期存在两套人工 Authority**。
* 迁移开发期允许并存：`Legacy Source` + `New Generated Shadow`；但 **Shadow 不允许同时人工编辑**（Shadow 是生成物/只读镜像）。
* 切换（Cutover）后：**新 XLSX = Authoring Authority**；旧 CSV / Inspector 来源**立即进入 Frozen Legacy 状态，不得继续编辑**。

## 1.3 No permanent dual runtime

* 允许：**Migration comparator / test-only adapter**（仅测试或迁移验证期）。
* 不得长期保留：`if (useNewConfig) … else useOldConfig …` 这类**产品双路径**。
* 正式 Cutover 后：**Gameplay 只读新 Registry**。
* **Rollback 优先依靠 Git commit rollback**，而不是永久 runtime feature flag（见 §11）。

## 1.4 其他硬原则

* **一次只迁一个领域**；跨领域共享基础设施（Compiler / Builder / Root）集中在 Wave 0 做一次。
* **不借迁移清理其它领域**（见 §9 Wave 1F 的删除边界）。
* 所有正式产物必须**可从 Authoring + SourceAssets 确定性重建**（D-019）。
* 任何 Hard Stop（§17）出现时**停止迁移**，不得用兼容层掩盖。

---

# 2. Authority 与边界（继承 D-019）

| 项 | 规定 |
|---|---|
| Authority | **D-019**（Target Architecture 已确认；Production Migration 原为「尚未开始」→ 本 FDS 是其 **Migration 计划**，不改 D-019 结论） |
| 本文件性质 | **Migration Plan / Implementation Contract**（开发期 Binding）；**不是**新的 Architecture Decision |
| 配置架构负责 | Static Configuration 的 **Authoring / Build / Representation / Lookup** |
| 配置架构**不**拥有 | Gameplay Rule、Runtime State、Person Identity、Battle State、Expedition State、Village Simulation、Save Authority |
| 参数与规则 | 配置可承载 **Gameplay Parameter**；参数的**游戏语义 / Rule Authority** 仍服从 GameSpec 与对应系统 Authority |
| Gameplay 依赖方向 | Gameplay **只**依赖稳定 `Definition / Registry` 契约；**不得**依赖 XLSX / Compiler / Editor Builder / `AssetDatabase` |
| Runtime 约束 | Runtime **不得**要求 `AssetDatabase` 或 Editor assembly |
| Save 边界 | Save 只存**稳定 ID + 长期运行结果**；不得保存 ScriptableObject / Generated Runtime Object 引用 |
| 本 FDS 只覆盖 | **Wave 0 + Wave 1（Monster + Enemy Presentation）** 的目录/工具/契约/验收；其它领域只给**复用协议**（§16） |

---

# 3. Wave 0 — Production Toolchain Foundation

**目标**：把 Spike 中验证过的工具**生产化**，而不是 merge 实验室；**零 Gameplay 语义变化**（本 Wave 不得改变任何 Battle / Adventure / Save 行为）。

## 3.1 正式目录候选（Production V0.1）

| 用途 | 候选路径 | 理由 / 边界 |
|---|---|---|
| Config Compiler（.NET 工程） | **`Tools/RRPG.ConfigCompiler/`** | 已在 `.gitignore` 中显式开例外（`!Tools/**/*.csproj`，2026-09-22）；`Tools/` 是本次已建立的正式工具根 |
| 人工 Authoring（工作簿） | **`Development/Config/`** | 与 `Development/Excel`（Legacy）**并列**，不复制到 Unity `Assets/`；XLSX 为 binary（`.gitattributes` 已冻结） |
| 源资产（美术/音频等） | **`Development/SourceAssets/`** | 不进 `Assets/`、不被 Runtime 直接依赖（`Source Art` 为其已验证子类） |
| Canonical Mirror（文本产物） | **`Development/Generated/StaticConfig/`** | 进入 Git（D-019 冻结的 `Generated Review Mirror`）；CI/Review 的可读面 |
| Unity 侧生成资产 | **`UnityProject/RRPG1.0game/Assets/Generated/StaticConfig/`**（含 `Resources/RRPGStaticConfigRoot.asset`） | Unity Runtime Artifact 的**唯一**生成根（**不入 Git**，见 §8；加载方式见 §3.3） |
| Runtime 代码（只读定义 / Registry） | **`Assets/Scripts/StaticConfig/`（Runtime asmdef，无 Editor 依赖）** | Runtime 不得引用 Editor / `AssetDatabase` |
| Editor 侧 Builder | **`Assets/Editor/StaticConfig/`** | Unity-specific 构建期阶段（Config Database / Catalog / Managed Assets） |
| Producer 入口 | 根目录入口（沿用 `RRPG_*.cmd` 概念） | 具体形态**不冻结**（§15） |

**目录原则**：**浅 / 可读 / AI 易定位 / 并行修改友好**；不为「未来所有领域」预建深 taxonomy。
→ **V0.1 只建立上述根**，领域细分（如 `Development/Config/<Domain>/`）留给实际需要时再拆。

## 3.2 Wave 0 交付物（不含 Gameplay 接线）

1. **正式 Compiler 工程**（去 Spike 命名空间/路径/术语，见 §4.2），可对 **0 或 1 个领域**跑通 build；
2. **正式目录骨架** + `.gitignore`/`.gitattributes` 既有规则**复用**（不新增全仓 eol 策略）；
3. **Canonical Mirror 契约**实现（§7：确定性、`DO NOT EDIT` 标记、stale detection）；
4. **Unity Builder 骨架**（能生成 Aggregate Config Database / Catalog / Managed Assets 到 §3.1 的 Unity 生成根）；
5. **Producer 一键入口**（Wave 0 可只有 cmd/PowerShell 形态）；
6. **Toolchain + Builder 测试矩阵**（§13.1 / §13.2）与 **Fresh Clone Gate**（§14）可执行脚本。

**Wave 0 验收**：`零 Gameplay 语义变化`（无 runtime 消费变化）+ 上述 6 项可跑 + 旧链（CSV→SO→DataManager）**完全不受影响**。

## 3.3 Wave 0 Runtime Root 策略（V0.1 冻结）

为支持「Generated Unity Artifact 不入 Git」（§8），Wave 0 **V0.1 使用单一 Generated Root**：

```text
Assets/Generated/StaticConfig/
  Resources/
    RRPGStaticConfigRoot.asset
```

* Root **至少引用**：`MonsterConfigDatabase` / `EnemyPresentationDatabase` / `VisualAssetCatalog`。
* Runtime 通过**一个固定 exact path**取得生成配置 Root：

```csharp
Resources.Load<RRPGStaticConfigRoot>("RRPGStaticConfigRoot")
```

**明确**：

1. 这是 **exact single-root Load**，**不是** `Resources.LoadAll` 扫描模型（不是「按目录批量发现配置」）。
2. D-019 淘汰的是 **`Resources.LoadAll` + God `DataManager`** 这一**模型**；**不是**禁止 Unity `Resources` API 的全部使用。
3. 产品 **Scene / Prefab 不得**直接序列化引用 per-domain Generated Asset（只能引用稳定 Root / 通过 Registry 解析）。
4. **Gameplay Consumer 不得知道 `Resources` 路径**；它只依赖 **Static Registry**（路径知识只存在于 Root 加载层）。

---

# 4. Spike → Production Promotion Manifest

> 原则：**Promote 技术**，**不 Promote 实验产物**。**不得**把整个 Spike 树 copy 到 main。

## 4.1 Promote / Adapt（升格为正式实现）

| # | Spike 技术 | 处理 |
|---|---|---|
| P1 | XLSX reader（`WorkbookReader` 系列） | **Promote**：保留能力，去 Spike 命名空间；补正式 diagnostics |
| P2 | Machine Field 解析（Row1 机器字段 = 字段身份） | **Promote**：D-019 已冻结该原则 |
| P3 | rule grammar 核心（`RuleParser` / `RuleModel`，含 `default=` / `>=` / `allowed=` / 空=必填） | **Promote**：去实验措辞与内部注释 |
| P4 | schema / data / cross-ref validation（含 Hard Error 定位到 表/Sheet/行/列） | **Promote**：作为正式校验层（Producer 友好消息保留） |
| P5 | deterministic Canonical writer（列序无关、字节确定性） | **Promote**：这是 Mirror 契约的基础 |
| P6 | Asset Manifest（源图 → 生成路径 + `SourceSha256` 确定性） | **Promote** |
| P7 | transactional compiler flow（先校验后写入；失败不写半套） | **Promote**（正式必须有） |
| P8 | OpenXML 包完整性校验 | **Adapt**：作为 package-check 步骤保留 |
| P9 | Unity Builder 模式（内容签名驱动重写 + no-op 确定性） | **Promote**：正式 Builder 的核心语义 |
| P10 | managed asset stale 处理（只删自己根内的 stale 产物） | **Promote** |
| P11 | Producer one-click orchestration 思想（一个动作跑完整链 + 中文成功/失败 + 报告路径） | **Adapt**：**思想**保留；**实现形态不冻结**（§15） |
| P12 | 变更驱动 Dev 工具（`T4CaseDriver` 一类） | **Adapt**：只作为**测试辅助**进入正式测试目录（不作为产品工具） |

## 4.2 Rewrite / Rename（可复用思想，但必须改写）

* **去 Spike namespace**（`Rrpg.ConfigCompiler.SpikeA*` → 正式命名）；
* **去实验路径**（`Development/Spikes/...`、`Generated/` 旧层级 → §3.1 正式根）；
* **去 T1/T2/T3/T4 terminology**（代码、注释、报告字段、文件名一律去除）；
* **去 fixture assumptions**（正式 Compiler 不依赖 Fixture 工作簿/测试语料）；
* **诊断/报告改为正式形式**（稳定的 error code 命名空间 + 面向 Producer 的定位信息 + 面向 CI 的机器可读字段）。

## 4.3 Do NOT Promote（明确不得进入正式产品）

| 类别 | 具体 |
|---|---|
| 候选对比实验 | T2 Candidate A/C 实验实现、比较 runner、`ExpectedDigest` harness、candidate comparison code |
| 交付实验 | T3 Player harness / 交付对照实现 |
| 预览实验 | T4 Preview（场景、Host、Resolver 实验版） |
| 实验场景与资产 | Spike scenes、Spike generated assets |
| 实验语料 | Fixture workbooks、experiment cases、测试用工作簿 |
| 实验记录 | Evidence logs（保留在远端 Spike 分支，**不进 main**） |

> Spike Evidence **只留远端实验分支**（`spike/config-architecture-a @ 3532351d…`）；**不得**把 Spike 树整体复制进 main。

---

# 5. Production Compiler Implementation 方向

## 5.1 建议：直接采用 **`.NET CLI` + `ClosedXML` + `OpenXML SDK`** 作为 Production V0.1

**不建议**在 Migration 阶段重新做语言竞赛（Python / Node / 其它）：除当前 main/环境出现新的硬阻塞，**不重启技术选型**。

**理由（逐条基于既有证据，而非偏好）**：

| # | 理由 |
|---|---|
| 1 | **已有完整 Spike 证据**：XLSX 读取、多行 Header、rule grammar、locale/中文、freeze、cross-ref、确定性输出、事务语义均在 .NET 实现下实测通过 |
| 2 | **不进入 Unity runtime**：Compiler 是**构建期**外部工具，不引入任何 Runtime 依赖或包体成本（符合 D-019 依赖方向） |
| 3 | **中文 / XLSX / freeze / filter 已验证**（Producer 工作簿真实语料上跑过） |
| 4 | **CLI / AI / Windows 流程已验证**：命令行可被脚本、AI Agent、CI 调用；本仓为 Windows + Tuanjie(Unity) 环境 |
| 5 | **包版本已验证**：`ClosedXML` + `DocumentFormat.OpenXml` 的既有组合已在 Spike 中稳定运行；`OpenXML` 包校验也已就位 |
| 6 | **与仓库基础设施一致**：`!Tools/**/*.csproj` 例外已在 2026-09-22 建立，正式 .NET 工程可入库 |

## 5.2 工程约束

* 正式工程路径：**`Tools/RRPG.ConfigCompiler/`**（§3.1）；**不得**引用 Unity 程序集；**.NET 目标与 SDK 版本在 Wave 0 记录并冻结**（可实施细节，非架构）。
* **网络事实（准确表述）**：
  * **正常 `validate` / `build` 不得访问网络**（这是硬约束）；
  * 当前开发工具 bootstrap：**首次 `dotnet restore` 可能需要 NuGet 网络**（除非本机已有缓存）；
  * 包必须 **锁定版本 + locked restore**（`packages.lock.json` / `--locked-mode` 一类）；
  * **Compiler packaging / self-contained publish / 是否分发 binary** 仍属 **D-019 Not Frozen**，Wave 0 实现时再决定。
* **确定性**：相同输入 → 相同输出字节（Canonical Mirror + Manifest）。
* **退出码语义**：`0 = 成功`；`非 0 = 明确失败`（不写半套；见 §7 / §17）。

---

# 6. Production Workbook / Source / Generated 布局（Wave 0/1 冻结必要目录）

## 6.1 推荐布局（V0.1）

```text
Development/
  Config/                      ← 正式人工 Authoring（XLSX）
    RRPG_Monsters.xlsx
    RRPG_EnemyPresentation.xlsx
  SourceAssets/                ← 正式源资产（按内容需要）
    Enemy/
  Generated/
    StaticConfig/              ← Canonical Mirror（进 Git；DO NOT EDIT）
      Monster.json
      EnemyPresentation.json
      VisualAsset.json
      AssetManifest.json

Tools/
  RRPG.ConfigCompiler/         ← 正式 .NET Compiler 工程

UnityProject/RRPG1.0game/Assets/
  Generated/StaticConfig/      ← Unity 生成资产根（Config Database / Catalog / Managed Assets；不入 Git，见 §8）
    Resources/
      RRPGStaticConfigRoot.asset   ← 唯一生成 Root（exact path 加载，见 §3.3）
  Scripts/StaticConfig/        ← Runtime 只读 Definition / Registry
  Editor/StaticConfig/         ← Unity Builder（Editor-only）
```

> 上表为**推荐**（依据 §3.1）；`Development/Excel/`（Legacy CSV）在 Wave 1 期间**保留原样**。

## 6.2 工作簿拆/合规则（**不是**「文件越少越好」）

| 决策维度 | 规则 |
|---|---|
| 拆分依据 | **协作所有权 / Git 写锁粒度**：可并行修改、由不同人或不同任务编辑的内容**拆开** |
| 合并依据 | **数据语义强关联**（主表 + Child Sheet，如 `EnemyPresentation` 与其引用关系）保持**同一 Workbook** |
| **AI 并行修改成本** | 单 Workbook 被多 Agent 同时编辑 → 冲突成本高；因此**不为省文件数而合** |
| 制作人查找成本 | 名称必须**自解释**（`RRPG_Monsters.xlsx` / `RRPG_EnemyPresentation.xlsx`），且每个 Sheet 的 A 列就地说明 |
| Git 冲突 | XLSX 为 **binary**（`.gitattributes` 已冻结）→ **同文件并发编辑不可合并**，这是拆分的主要动因 |
| 领域扩展 | 新领域**新增 Workbook**，不改既有 Workbook 结构（避免大范围冲突） |

**明确禁止**：把所有领域塞进**一个超级 Workbook**（会同时恶化上述 5 项）。

**Wave 1 决定（已裁决）**：**保持两本 Workbook**：

| 工作簿 | 负责 |
|---|---|
| `RRPG_Monsters.xlsx` | **Monster pure static stats / definition**（8 字段） |
| `RRPG_EnemyPresentation.xlsx` | **EnemyPresentation** + **VisualAsset binding** |

理由（逐条）：

1. **职责不同**：Gameplay stats 与 Presentation 是两个域；
2. **修改频率不同**：数值调整与换图/排版节奏不同；
3. **AI 并行修改更安全**（不同 Agent 不争同一 binary 文件）；
4. **Git binary workbook 冲突范围更小**（XLSX 不可合并）；
5. **后续视觉资产增长不会污染 Monster 数值表**。

> 这只是 **Wave 1 决定**，**不**推广为「所有领域必须一领域一本」；未来领域的拆/合仍按 §6.2 的 5 个维度判定。

---

# 7. Canonical Mirror Git Contract

> 继承 D-019：**Canonical JSON + deterministic Asset Manifest 进入 Git**（`Generated Review Mirror`）。本节的规则是**实现契约**，不改 D-019。

## 7.1 正式目录

* Mirror 目录：**`Development/Generated/StaticConfig/`**（§3.1 / §6.1）。
* Mirror 内容 = 每个领域的 Canonical JSON + `AssetManifest.json`；**不含** `BuildReport` / 执行态内容。

## 7.2 Mirror 的硬规则

| # | 规则 |
|---|---|
| R1 | **DO NOT EDIT**：Mirror 文件**只由 Compiler 生成**。文件头/首行必须携带 `DO NOT EDIT` 标识（格式在 Wave 0 固定，例如 JSON 内 `"_generated": {"doNotEdit": true, "tool": "...", "sourceSha256": "..."}` 或同等稳定字段） |
| R2 | **不允许人工编辑**；**不成为第二 Authoring Authority**（D-019） |
| R3 | **必须可从 XLSX + SourceAssets 确定性重建**（相同输入 → 相同字节） |
| R4 | **stale detection**：Mirror 必须携带**输入指纹**（工作簿 / 源资产内容哈希）；`validate` 阶段把「当前输入指纹」与「Mirror 内记录指纹」比较，**不一致即报告 mirror-stale** |
| R5 | **Build / validate 必须能证明 `Authoring == Mirror`**：`validate` 在**不写文件**的前提下重算 Canonical 并与磁盘 Mirror 逐字节比较（Mismatch → 明确失败 + 定位到领域 / 文件） |
| R6 | **Hard Error：不得更新正式 Mirror**：当输入非法 / 校验失败 / 事务未完成时，**禁止**写入 Mirror（部分更新视为 Hard Stop，见 §17） |
| R7 | 镜像更新必须是**事务式**：全部成功 → 原子替换；任一失败 → Mirror 保持原样 |

## 7.3 行尾（EOL）策略

* **不修改全仓 `*.json` 策略**（D-019 / §18 明确禁止）。
* **只允许**对正式 Generated 路径做**路径级**规则：Canonical 正式路径确定后，仅针对
  `Development/Generated/StaticConfig/*.json` 增加 **`text eol=lf`**（**path-specific**）。
* 加入时机：**Wave 0 建立目录骨架时**；**不得**一次性扩散到其它 JSON。
* 理由：Mirror 需要**跨平台逐字节确定性**；若 eol 规则缺失导致 Windows/macOS 产出不同字节，会破坏 `R3/R5`。

---

# 8. Generated Unity Runtime Artifacts Git 策略（D-019 留给 Migration 的问题）

> **决策状态**：此问题已由 **Migration FDS Review（2026-09-22）收敛** —— **Wave 0/1 V0.1 正式选择：Generated Unity Runtime Artifacts 默认不进入 Git**（见 §8.2）。
> 这是 **Migration V0.1 Implementation Decision**，**不是**把 D-019 升级为永久架构规则；D-019 对此项仍登记为 Not Frozen。

**对象**：`Assets/Generated/StaticConfig/` 下的 Config Database / Presentation Database / Visual Asset Catalog / Managed Unity Assets（PNG + `.meta`）与唯一生成 Root。

## 8.1 两个方案

| | **A：进入 Git** | **B：不入 Git（本地/CI 生成）** |
|---|---|---|
| fresh clone 体验 | clone 即可打开工程看到资产 | clone 后**必须**先跑一次 Build |
| GUID 稳定需求 | GUID 随提交固定 | GUID 必须**确定性生成**（否则引用漂移） |
| Unity import | 已导入状态随仓库 | 首次导入由本机完成（导入时间↑） |
| 仓库体积 | 体积随内容线性增长（含 PNG 二进制 + `.meta`） | 仓库只留 Authoring + SourceAssets + Mirror |
| SourceAssets 重复 | 源图与生成图**双份**入库 | 无重复 |
| merge conflict | 二进制资产冲突不可合并 | 无产物冲突（只有 Authoring 冲突） |
| deterministic rebuild | 依赖「已提交版本」 | 依赖**必须**能确定性重建（Hard Gate） |
| Player build | 直接可用 | 构建前必须先跑 Builder |
| CI 需求 | 低（可跳过 build 验证） | **高**：CI 必须 build 后才能编译/测试 |
| 产品资产是否引用生成资产 | 允许 Scene/Prefab 直接引用 | **禁止**直接引用 per-entry 生成资产（否则断链） |

## 8.2 决策（Wave 0/1 V0.1）：**Generated Unity Runtime Artifacts 默认不进入 Git**

**不进入 Git 的对象**：

* Aggregate **Config Database**；
* **Presentation Database**；
* **Visual Asset Catalog**；
* **Managed Unity Assets**（含 PNG + `.meta`）；
* **Generated StaticConfig Root**（`Assets/Generated/StaticConfig/` 全部内容）。

**Git Authority（进 Git）**：

```text
XLSX（Authoring）
SourceAssets
Canonical JSON（Mirror）
deterministic Asset Manifest
```

**Unity Generated**：由 **Builder 一键重建**（不作为 Git Authority）。

**前提（必须同时满足，缺一不可）**：

1. 能从 **Authoring + SourceAssets 一键重建**（工具链 + Builder 完整）；
2. **产品 Scene / Prefab 不得直接引用 per-entry Generated asset**（只能引用稳定 Root / 通过 Registry 解析）；
3. **Runtime 只依赖稳定 Generated Root 定位**（固定 exact path；不依赖「某个已提交 GUID」）；
4. **clean clone 可重建验证通过**。

**Fresh Rebuild = Hard Gate**（§14）：

* 若 Fresh Rebuild **失败** → **Hard Stop + Review**（§17）；
* **不得**自动改为「那就把全部 Generated Asset 提交 Git」——变更该策略必须**重新 Review**。

> 依据：本节 §8.1 的 10 项对照（避免二进制产物与 SourceAssets 双份入库、消除不可合并冲突）。

---

# 9. Wave 1 — Monster + Enemy Presentation

**为什么 Monster 是第一领域**：Spike 中覆盖最完整（XLSX / 等价 / Builder / 视觉 / 删除收敛均有实证）；当前 main 的 Monster 结构最小（§0.1：8 字段、3 条数据、1 条 runtime 路径）。

## 9.0 Wave 1 等价基线（当前 main 实测，作为 1A 的判定输入）

| MonsterId | Name | MaxHP | Attack | AttackRange | AttackCount | ActionPoints | Faction |
|---|---|---|---|---|---|---|---|
| `mon_001` | 史莱姆 | 20 | 4 | 1 | 1 | 1 | enemy |
| `mon_002` | 骷髅弓手 | 15 | 5 | 2 | 1 | 1 | enemy |
| `mon_003_wolf` | 野狼 | 24 | 5 | 1 | 1 | 1 | enemy |

* 来源：`Development/Excel/Monster.csv`（A1）与 `Assets/Resources/ScriptableObjects/Monsters/*.asset`（A3）**逐字段一致**（2026-09-22 实测）。
* **等价性必须对「磁盘 SO 实测值」判定**，不得只看 CSV（二者今天一致，但这是**事实**，不是保证）。

## Wave 1A — Shadow Build

**Current Runtime 不变**；**Current Authoring 仍是旧 `Monster.csv`**。

交付：

1. 正式 `Development/Config/RRPG_Monsters.xlsx`（Monster Sheet，8 字段 = §9.0 列）。
2. 正式 `Development/Config/RRPG_EnemyPresentation.xlsx`（`EnemyPresentation` + `VisualAsset` 两个 Sheet，按 §8 的 Authoring 边界）。
3. `Development/SourceAssets/Enemy/`（源图目录）。
4. **Canonical Mirror** → `Development/Generated/StaticConfig/{Monster,EnemyPresentation,VisualAsset,AssetManifest}.json`。
5. **Unity Config Database** + **Presentation Database** + **Visual Asset Catalog** + **唯一生成 Root** → `Assets/Generated/StaticConfig/`（§3.3）。

**初始 XLSX 数据**：从 **Current Legacy Source 确定性迁移/生成**（一次性生成脚本；生成物即 Mirror 与 XLSX 行），**不得**在 Shadow 阶段手工双写。

**Shadow 阶段硬约束**：

* **XLSX 不得成为第二人工 Authority（Shadow Authority 收紧）**：
  * Wave 1A 期间 **`Monster.csv` 仍是唯一人工 Authority**；
  * Shadow `RRPG_Monsters.xlsx` 的初始内容**必须由 Legacy Source 确定性生成/同步得到**；
  * **不得把「人工修改 Shadow XLSX」当成产品内容变更来源**；
  * Shadow XLSX 的用途仅限：**Compiler 验证 / Equivalence / Builder / Registry shadow 测试**；
  * **明确时刻**：只有 **M2 Authority Cutover 之后**，正式制作人的内容修改才允许进入 `RRPG_Monsters.xlsx`（§10.2）。
* **逐字段比较**（对**当前全部** Monster，而非抽样）：

```text
Legacy（MonsterData 磁盘值 + CSV 校验）
   vs
New（XLSX → Canonical → Config Database → Definition）

字段：MonsterId / Name / MaxHP / Attack / AttackRange / AttackCount / ActionPoints / Faction
```

* **必须 100% 等价**：任何不等价（数量、字段、值、大小写、空格、ID 差异）→ **不得进入 1B**（Hard Stop §17）。
* 等价比较器实现方式：**测试或迁移期工具**（§1.3 允许），**不得**进入产品 runtime。

**1A 验收**：Mirror + Database + Catalog 生成成功；等价比较 **3/3 Monster × 8/8 字段 PASS**；产品 runtime **零改动**；旧链行为**零变化**。

## Wave 1B — Runtime Registry Introduction

交付：正式 **Pure Monster Definition / Registry**（`Assets/Scripts/StaticConfig/`，Runtime asmdef）。

* Definition = 只读静态字段（8 字段）；Registry = 稳定查找（`Get / TryGet`，见 §13.3）。
* **先不接正式 Gameplay Consumer**（`BattleSystem` / `BattleUI` 此时仍读 `MonsterData`）。
* **必须验证**：`Canonical → Database → Registry` 与 Legacy 等价（对全部 3 条 Monster 的 8 字段）。
* **硬约束**：**不得**把 `XLSX`、JSON parser、Editor Builder、`AssetDatabase` 带进 Runtime assembly（编译期 + 运行时依赖都要证明）。

**1B 验收**：Registry 在 **Player 可用的加载方式**下可初始化（§0.3 的 lazy-or-boot）；无 Editor 依赖；等价测试 PASS；产品行为**零变化**。

## Wave 1C — Consumer Cutover

**必须一次性枚举所有真实 Monster Consumer**（**以下来自 §0.2 的实测清单，不得凭记忆**）：

| Consumer | 旧依赖 | 新依赖（目标） |
|---|---|---|
| C1 `ExpeditionUI:495/502-503` | `DataManager.AllMonsters` → `AdventureManager(..., monsters)` | **Monster Registry**（取全部定义 / 或按 Run 需要的子集） |
| C2 `AdventureManager:56/65/73/446` | `List<MonsterData>` 持有 + 透传 | `IReadOnlyList<MonsterDefinition>`（或稳定 ID 列表 + Registry） |
| C3 `ExpeditionUI:603-610` | `GetEnemyMonsters()` → `InitFromExpedition(..., monsters, ...)` | 同上（透传 Definition / Registry 句柄） |
| C4 `BattleUI:93/128/405-420` | `_enemyMonsters` + `m.MonsterId/Name` | `MonsterDefinition`（`MonsterId` / `Name` 同语义） |
| C5 `BattleSystem:48-49/67-69/518-529` | `InitializeBattle(..., List<MonsterData>, ...)` → 逐字段读 | `InitializeBattle(..., IReadOnlyList<MonsterDefinition>, ...)`（**字段语义完全不变**） |
| C6 `BattleUI:1646` | `_enemyMonsters.Find(m => m.MonsterId == u.SourceId)` | Registry `TryGet(unit.SourceId)` |
| C7 `BattleUIDisplayNames:20` | 同上 | 同上 |
| C8 `BattleUI.SetupDemoData:2004/2043-2047` | `DataManager.AllMonsters` / 内联 demo | **演示路径**：改为 Registry（若为空则保留**显式** demo 分支，并标注为 demo-only） |

**规则**：

* 目标：Gameplay **只消费 `MonsterDefinition / Registry`**，**不再消费 `MonsterData` ScriptableObject 本身**。
* 若某 consumer 暂时无法直接迁：允许**极窄 Compatibility Adapter**，但必须
  ① **单向** `new → compatibility`（新链为 Authority，适配层只读、不反向写）；
  ② 明确标注 **Disposable**；
  ③ 写明**删除条件**（例如「C8 demo 路径改为 Registry 后删除」）；
  ④ **不得**成为第二 Runtime Authority。
* **不得**保留长期 dual-read（§1.3）。
* `AdventureManager` 的构造签名是**产品链路**，改签名必须同步更新其**测试**（PlayMode 大量用例通过反射构造），**不得**为省事保留旧重载作为永久双路径。

**C8 / Demo 边界补充（SetupDemoData）**：

* `SetupDemoData` 的 **inline Monster definition 属隐藏静态源** → **必须在 M2 Cutover 时消失**（见 §10.4-A/B/C）。
* 但 **Demo 功能本身不一定删除**：若 Demo 仍需要，**改为读取 New MonsterRegistry**（按固定 `MonsterId`）。
* 「删除整个 Demo」**不是本 Migration 的默认目标**。

## Wave 1D — Enemy Presentation Cutover

**目标链**（正式 Battle）：

```text
MonsterId
   ↓（EnemyPresentation）
VisualAssetId
   ↓（Visual Asset Catalog）
Sprite
   ↓（现有 Visual Host：UnitCard → Anchor_Visual → Body_Visual）
```

**硬要求**：

* **不改变 `MonsterId` 身份语义**（`BattleUnit.SourceId` 仍是 MonsterId；`UnitId` 语义不变）。
* **不改变 Battle 规则**（HP/Attack/Range/Count/AP/Faction 的消费语义不变）。
* **不改变 Board / Roster / Range 等规则**（只替换「取哪张 Sprite」）。
* **missing visual 必须有明确 fallback**：`MonsterId` 无 Presentation 行 / 行引用了不存在的 `VisualAssetId` / Catalog 缺该资产 → **明确 fallback + 可诊断输出**；**不得**静默（现状是静默保持旧 Sprite，见 §0.4）。
* **fallback 的落位（M3 实现决定 → M4 关闭，取代本 FDS 的原 Wave 1D 表述）**：原表述要求 `enemyBodyPlaceholder`
  作为 fallback 且**保留字段、Wave 1 不删除**。M3 Final Closeout 已改为把 fallback **移入 Resolver 自身**
  （`EnemyPresentationResolver.FallbackSprite` + `WarnOnce` 诊断；M3 报告 D-M3-6 / D-M3-7），该字段因此**零读取**；
  M4 随即按 §9 Wave 1F / §10.5 **物理删除该字段与 Scene 序列化条目**（M3-R2 关闭）。

```text
missing EnemyPresentation  → Resolver 内 fallback sprite + WarnOnce（唯一取图路径仍为 Resolver）
missing VisualAsset        → 同上
missing Sprite             → 同上
```

  * 该 fallback 是 **Presentation failure fallback**，由 Resolver 内部产生（不读任何资产、不依赖旧字段）；
  * **不是** **Enemy visual Authoring Authority**（每个 Monster 的图仍必须来自 Authoring + Catalog）；
  * 旧的 Inspector 手拖占位**不再参与**敌方视觉（字段已删除，不存在第二取图路径）。
* **不要求 Scene / Prefab 手拖每个 Monster Sprite**：Inspector 手拖引用**不得**成为正式路径（每个 Monster 的图必须来自 Registry/Catalog）。
* 现有 `ApplyUnitBodyVisual(card, unit)` 的**宿主与调用点复用**（§0.4）；实现上只改「sprite 从哪里解析」。

**1D 验收**：3/3 Monster 在 Battle 中显示各自 `VisualAssetId` 对应的 Sprite；缺失场景走明确 fallback；无手工 Sprite 引用。

## Wave 1E — Product Verification

**必须使用真实产品链**（不是单测拼装）：

```text
New Game → Town → Expedition Prep → Confirm → Journey → Battle → Enemy Spawn
→ Battle 行为 → Battle Return → Retreat / Return Town
```

必须确认：

* enemy **stats** 正确（HP/Attack/Range/Count/AP/Faction，逐字段 = §9.0 基线）；
* enemy **name** 正确（`BattleUIDisplayNames` 链）；
* enemy **visual** 正确（1D）；
* **battle 结果一致**（与 Cutover 前同输入的胜败/伤害/回合行为一致）；
* **journey 往返不回归**（Journey Presentation / Encounter 中断 / Return 链）；
* **Save 不受影响**（§12：Save 内容与 SaveVersion 不变）。

同时（全量 Gate）：

* **全量 EditMode**、**全量 PlayMode**、**compile 0 error**；
* **配置 build**（Compiler）成功；
* **bad-batch gate**（非法输入 → 0 写入，Hard Error）；
* **no-op gate**（重复 build → 全部 `unchanged`）。

## Wave 1F — Legacy Retirement

**只有 Wave 1E 全部验收通过才能执行。**

Monster 领域退休对象**候选**（逐项需在 1F 任务里确认零引用后删除）：

| # | 对象 | 边界 |
|---|---|---|
| F1 | `Development/Excel/Monster.csv` | **Frozen Legacy → 删除**；Cutover 后不得再编辑 |
| F2 | `Assets/Resources/ScriptableObjects/Monsters/mon_*.asset`（3 个 per-entry SO + `.meta`） | 删除（`MonsterData` **类型**是否保留见下） |
| F3 | `ExcelImporter` 的 **Monster 分支**（`ImportMonsters`） | **仅删该分支**；`ExcelImporter` 若仍服务其它 CSV（Character/Skill/Status/ItemCard/Person/Profession）→ **继续保留** |
| F4 | `DataManager` 的 **Monster Resources.LoadAll 路径**（`AllMonsters` / `_monsterMap` / `GetMonsterById`） | **仅删 Monster 部分**；`DataManager` 若仍服务 Character / Skill / 其它 → **继续保留** |
| F5 | `GameDataInitializer` 中的 **Monster seed**（A6，:64-76） | 删除该段；其余 seed（Character/Hero/Skill/Status/Card）**保留** |
| F6 | `BattleUI.SetupDemoData` 的内联 demo monsters（A7，:2043-2047） | 删除或改为 Registry 驱动的 demo |
| F7 | `MonsterData` **类型**（`MonsterData.cs`） | **放在最后**：确认无 runtime / Editor / 测试引用后再删；若仍有测试或过渡用，保留但标 `Legacy`（不得成为产品依赖） |

**禁止**：借 Monster Migration 清理其它领域（Character / ItemCard / Skill / Status / Person / Profession 的 CSV、SO、Importer 分支、DataManager 路径**一律不动**）。

## 9.1 Wave 1 明确**不迁移**的相邻项

| 项 | 归属 | Wave 1 处理 |
|---|---|---|
| `EnemyBehaviorDemoConfig.cs`（`MonsterId → 行为规则`，硬编码 C#） | **Enemy AI / Behavior domain**（**不属于** Monster Static Definition，也**不属于** Enemy Presentation） | **不迁**；**不改行为语义**；**只验证**其中的 `MonsterId` 在 Cutover 后仍能解析（可解析性 = 回归断言） |

> 未来是否把行为规则迁入 XLSX，**由其自己的 Migration Audit 决定**（不在本 FDS 授权范围内）。

---

# 10. Authority Cutover 时刻（**两个独立 Gate**）

> **两个 Gate 必须分开记录、分开验收**：Monster **Definition** Authority（M2）与 Enemy **Presentation** Authority（M3）是**两次独立 Cutover**。
> 本 FDS **不再**把 Monster Cutover 要求成「Enemy Visual 也已经完成」。

## 10.0 两个 Gate 总览

| Gate | 阶段 | Authority 变化 | 条件 |
|---|---|---|---|
| **Monster Definition Authority Gate** | **M2** | `Monster.csv` → `RRPG_Monsters.xlsx` | §10.2 |
| **Enemy Presentation Authority Gate** | **M3** | `RRPG_EnemyPresentation.xlsx` + `SourceAssets/Enemy` → 正式 Authoring Authority | §10.3 |
| **Monster Legacy Physical Retirement** | **M4** | 无 Authority 变化（只做物理清理） | §10.5 |

## 10.1 Shadow 阶段定义（M1）

| 阶段 | `Monster.csv` | `RRPG_Monsters.xlsx` |
|---|---|---|
| Wave 1A（Shadow，M1） | **Current Monster Definition Authoring Authority** | **Shadow Candidate（由 Legacy 确定性生成；不得人工编辑、不得当作内容变更来源）** |
| Wave 1B–1C（M2 之前） | 同上 | 同上（已成为 Runtime 数据来源，但仍**不得**手工改 XLSX） |

## 10.2 Monster Definition Authority Gate（M2）

**Cutover 前**：`Monster.csv` = **Current Monster Definition Authoring Authority**；`RRPG_Monsters.xlsx` = **Shadow Candidate**。

**Gate 条件（全部满足）**：

1. **8 字段 100% equivalence**（3/3 Monster × 8/8 字段；对 Legacy **磁盘 SO 实测值**判定）；
2. **New Registry ready**（Player 可用路径；无 Editor 依赖）；
3. **所有真实 Monster Stats Consumer 已切换**（§0.2 的 C1–C5 数值链；C6/C7 名字链亦须改 Registry）；
4. **Runtime 无 dual read**（不存在 `if (useNewConfig) … else …` 产品双路径）；
5. **tests / product gate 满足 M2 范围**（全量 EditMode / PlayMode / compile + M2 范围真实产品链）；
6. **§10.4 的三项旧 Authoring 写入口已技术性禁用**。

**Cutover 结果**：`RRPG_Monsters.xlsx` = **Current Monster Definition Authoring Authority**；`Monster.csv` = **Frozen Legacy**。

## 10.3 Enemy Presentation Authority Gate（M3）

**Cutover 前**：敌方视觉仍是 `enemyBodyPlaceholder`（单一占位，§0.4）；Presentation 数据属 Shadow。

**Gate 条件（全部满足）**：

1. `MonsterId → EnemyPresentation → VisualAssetId → Catalog → Sprite` 在 Battle 链上生效；
2. **M2 已完成**（Definition 已是 XLSX Authority）；
3. missing 场景走**明确 fallback + 诊断**（§9 Wave 1D）；
4. **不存在** Scene/Prefab 手拖 per-Monster Sprite 的正式路径；
5. Wave 1E 全量 Gate 通过（含视觉断言）。

**Cutover 结果**：`RRPG_EnemyPresentation.xlsx` + `SourceAssets/Enemy` = **Current Enemy Presentation Authoring Authority**；`enemyBodyPlaceholder` 由 M3 降为 **Resolver 内 fallback**、并由 **M4 物理删除**（本行原「降为 fallback（不删除）」已被 M3 D-M3-6 / D-M3-7 与 M4 取代；见 §9 Wave 1D 与 §19.2 Q3）。

## 10.4 M2 必须技术性禁用 Legacy Authoring 写入口

> 「Frozen Legacy」**不能只靠文档**。以下三项**必须在 M2 完成**（不得拖到 M4），否则 Cutover 后仍存在旧静态内容生成路径。

| # | 要求 |
|---|---|
| A | `ExcelImporter.ImportAll()` **不得再调用** `ImportMonsters(Monster.csv)`；旧 `ImportMonsters` 方法**可暂留至 M4**，但**正式入口不得再触发它** |
| B | `GameDataInitializer` **不得再通过正式菜单 / 初始化路径创建 Monster seed SO**（可先禁用 Monster 分支；物理删除留 M4） |
| C | `BattleUI.SetupDemoData` 在 Cutover 后**不得继续 inline 创建 Monster static definition**；若 Demo 仍需要 → **从 New MonsterRegistry 按固定 `MonsterId` 读取**；若仅测试需要 → **迁 Test Fixture** |

## 10.5 DataManager 与 MonsterData 退休顺序（M2 逻辑 / M4 物理）

**M2（逻辑退休）**：所有真实 Monster consumer **停止读取** `DataManager.AllMonsters` 与 `MonsterData`；`DataManager` **仍可暂时加载**旧 Monster 资产（已无 Gameplay consumer），**等待 M4 清理**。

**M4（物理清理）**：先确认全仓 **Runtime / Editor / Tests** 对 `MonsterData` 的真实引用**归零**，然后**依序**执行：

```text
1. 删除 Monster.csv
2. 删除 3 个 Monster SO（+ .meta）
3. 删除 ExcelImporter 的 Monster importer 代码
4. 删除 DataManager 的 Monster LoadAll / 缓存
5. 删除 GameDataInitializer 的 Monster seed 残余
6. 删除无用 Demo legacy 残余
7. MonsterData 类型（最后一步）
```

**`MonsterData` 类型删除条件（必须同时满足）**：引用 = 0（Runtime / Editor / Tests）；资产 = 0；Compile PASS；Tests PASS。

**不得**因为「以后不要 per-entry SO」而**提前删除类型**。

## 10.6 Cutover 与 commit 边界

* **M2 = 逻辑 / Authority 退休；M4 = 物理清理** —— 两件事**不得混为一谈**，也不得压进同一 commit。
* **任何 branch 上不得长期出现「CSV 与 XLSX 都能改」**：从 M2 Cutover 那一刻起，`Monster.csv` 必须被视为 **Frozen**（可加文件头说明或 README 标注；**不**改文件内容以求「可编辑性」）。
* M2 与 M3 的 commit 必须在提交信息中**分别标注**：Monster Definition Authority 转移（M2）、Enemy Presentation Authority 转移（M3）。

---

# 11. Migration Commit / Rollback Strategy

## 11.1 Commit 阶段（每个 commit 职责单一、可独立 Review）

| 阶段 | 内容 | 允许的 Gameplay 变化 | 独立可验证 |
|---|---|---|---|
| **M0** | Production Toolchain Foundation（§3：目录 / Compiler 工程 / Mirror 契约 / Builder 骨架 / 入口 / 测试骨架） | **无**（零语义变化） | ✅（工具链 Gate + §14 脚手架） |
| **M1** | Monster Shadow Build + Equivalence（1A：XLSX / SourceAssets / Mirror / Database / Catalog + 等价报告） | **无** | ✅（3×8 等价） |
| **M2** | **Monster Definition Authority + Runtime Consumer Cutover + Legacy Authoring 入口冻结**（1B + 1C + §10.4） | 读取路径切换（**行为不变**）；`Monster.csv` → **Frozen Legacy** | ✅（M2 Gate：等价 / Registry / 全部 Consumer / 无 dual read / 测试） |
| **M3** | **Enemy Presentation Authority + Visual Cutover**（1D + 1E） | 敌方视觉来源切换（**规则不变**）；Presentation 成为 Authoring Authority | ✅（M3 Gate + 1E 全量 Gate） |
| **M4** | **Monster Legacy Physical Retirement + Migration Closeout**（1F + §10.5） | **无**（只删无人引用的 Legacy） | ✅（引用=0 / Compile PASS / Tests PASS） |

* **M2 = 逻辑 / Authority 退休；M4 = 物理清理**（不要混为一谈；见 §10.5）。
* **M3 收口实测（2026-09-23，Final Closeout 轮）**：EditMode 全量 **752/752 PASS**、**无过滤 PlayMode 全量 71/71 PASS（35.7 s）**，
  并取得真实 **Game View 整屏截图**（Prep / Battle 各一张；三张临时敌方图均可见、可区分、无裁切）→ M3 技术收口成立，
  遗留项见 `2026-09-23_RRPG_M3-Enemy-Presentation-Authority-Cutover-Closeout-Report-V0.1.md` §9 / §11。
* 具体数量可调整，但**不得**把整个 Migration 压成一个巨型 commit。
* 每个 Mx 必须能**单独回滚**（下一阶段不依赖上一阶段的未提交工作）。

## 11.2 Rollback 原则

* **优先 Git rollback** 到上一稳定 commit（`git revert` / 回到上一 tag/commit）。
* **不**为了 rollback 而永久增加产品 runtime toggle / feature flag。
* 过渡期允许的「双读」只存在于**迁移开发分支的工作中**（并且必须是 §1.3 的可删除形态），**不进入 main 的长期形态**。

---

# 12. Save Boundary（结论 + 冻结条件）

**§0.5 已证明（当前 main 实测）**：

* `SaveData` 只含 `SaveVersion / GameVersion / SavedAtUnixTime / Player / Village`；
* `SaveData.cs` 明确声明**不保存 ScriptableObject 引用 / battle state / map progress / UI state / DataManager caches**；
* `Assets/Scripts/Save/**` 中 **0 处** `Monster|MonsterId`；
* 敌方编成只在会话 Runtime（`AdventureManager` / `RunState`），不入 Save。

**因此本 FDS 冻结：**

> **Monster Migration（Wave 1）不提升 `SaveVersion`**（No SaveVersion bump）。

**条件与 Hard Stop**：

* 该冻结**以 §0.5 的只读审计为前提**；若 Wave 1 实施期间发现任何**静态配置对象引用**进入 Save（例如新增字段持有 `MonsterData` / `MonsterDefinition` / 生成资产引用）→ **Hard Stop**，重新设计（不得以「反正 JsonUtility 也会忽略」为由放过）。
* Wave 1E 必须包含 **Save 回归断言**：`Save → Stop → Play → Continue` 后长期数据与 `SaveVersion` 与基线一致。

---

# 13. Tests / Acceptance Matrix

## 13.1 Toolchain（Compiler）

| # | 用例 | 期望 |
|---|---|---|
| T1 | XLSX 解析（含中文 Sheet/表头） | 成功；无 locale 依赖 |
| T2 | Machine Field 解析（Row1 = 字段身份） | 字段识别正确；未知字段 → 明确错误 |
| T3 | 列顺序重排 | Canonical **字节不变** |
| T4 | 插入 `ignore` / `note` 列 | 不导出、不影响 Canonical |
| T5 | 确定性 Mirror（重复 build） | 字节一致 + `unchanged` |
| T6 | **bad batch → 0 写入** | 非 0 退出；Mirror 与 Generated **未变** |
| T7 | Cross-ref（引用不存在目标） | Hard Error + 定位（表/Sheet/行/列） |
| T8 | SourceAssets 校验（缺失 / 非 PNG / 越界路径） | Hard Error（0 写入） |
| T9 | Mirror stale 检测（改 XLSX 但不 build） | `validate` 报 mirror-stale |

## 13.2 Builder（Unity）

| # | 用例 | 期望 |
|---|---|---|
| B1 | stale asset 回收 | 只删 Builder 拥有根内的 stale 产物 |
| B2 | 缺资产 | 明确失败（不静默、不半写） |
| B3 | 源变更 | 只重写受影响产物 |
| B4 | no-op | 全部 `unchanged`（含场景语义） |
| B5 | clean rebuild | 从零重建得到等价产物 |
| B6 | Catalog 解析 | 每个 `VisualAssetId` → 可解析 Sprite |

## 13.3 Runtime（Registry）

| # | 用例 | 期望 |
|---|---|---|
| R1 | `Get / TryGet`（存在 / 不存在） | 命中返回值；未命中明确 false / null（不抛、不猜） |
| R2 | missing id | 明确诊断，不静默回退到其它 Monster |
| R3 | duplicate protection | 重复 ID → 明确失败（不取首个） |
| R4 | **initialization order** | 在 §0.3 的「首次消费前」可用；重复初始化幂等 |
| R5 | **no Editor dependency** | Runtime asmdef 不引用 Editor / `AssetDatabase`；Player 可加载 |

## 13.4 Migration Equivalence（Wave 1A / 1B）

| # | 用例 | 期望 |
|---|---|---|
| E1 | 全部 Monster × 8 字段：Legacy(SO 磁盘值) vs New(Definition) | **逐条逐字段一致**（3×8） |
| E2 | Monster 集合数量 | 一致（3 = 3） |
| E3 | 字段类型/格式（int 解析、字符串大小写与空格） | 一致（不做隐式 trim / 大小写归一） |

## 13.5 Product Regression（Wave 1E）

* 真实产品链（§9 Wave 1E）端到端；
* 全量 **EditMode** / **PlayMode** / compile；
* Save 回归（§12）；
* Journey / Expedition / Battle Return 不回归。

---

# 14. Fresh Clone / Rebuild Gate（**Hard Gate**）

**Migration 完成前必须证明**：从一个**不依赖本机旧 Generated 缓存**的状态出发（例如 clean clone / 删除所有生成物后），使用

```text
XLSX + SourceAssets + 正式 Toolchain
```

能够完整走通：

```text
Build → Canonical Mirror → Unity Runtime Artifact → Test → Player / Editor 正常运行
```

**判定**：

* §8 决策为 **Generated Unity Artifact 不入 Git** → 本节是 **Hard Gate**：不通过**不得**进入 Wave 1E/F，且**必须** **Hard Stop + Review**（**不得**自动改为「那就把全部 Generated Asset 提交 Git」）。
* 同时必须证明：**可从 Authoring 重建等价产物**（D-019 的确定性要求），且 **Generated Root 能在 Fresh Rebuild 后通过固定路径加载**（§3.3；见 §17 H11）。
* 「等价产物」判定：Config Database / Catalog 的**语义摘要**一致（不要求逐字节，除非实现能保证）；Managed Assets 内容哈希一致。

---

# 15. Producer UX

**正式 Production Rollout 后再做最终 UX 验收**（D-019 委派给 Migration / Rollout）。但本 FDS 的最低目标（Wave 1 必须满足）：

制作人的正常操作**只需要**：

```text
打开配置 → 修改 → 放 SourceAssets → 保存并关闭 Excel → 单一 Build Action
```

**不得**要求：

* 手工维护 `Catalog Size + 1`；
* 逐怪物拖 Sprite；
* 复制 / 编辑 JSON；
* 理解 GUID / `.meta`；
* 手工多阶段执行命令。

**入口形态（Wave 0 Production V0.1 = Migration V0.1 Implementation Choice）**：采用**仓库根**三个入口

```text
RRPG_打开配置表.cmd
RRPG_检查配置.cmd
RRPG_一键导表.cmd
```

内部允许：**PowerShell / .NET Compiler / Unity batch Builder**；**制作人不需要理解内部阶段**。

> 该形态 **≠ 永久 Producer UI Authority**：未来可替换为 **Editor Window / GUI**，只要遵守 D-019「单一 Build Action」原则。

**Wave 1 的成功判定**：制作人（非程序）能在**不看命令行**的情况下完成「改表 → 一键 → 看到结果」，且错误信息能定位到 表/Sheet/行/列。

---

# 16. 下一领域迁移协议（本 FDS 只定协议，不定各领域 Schema）

**Monster Wave 验证成功后**，其它领域必须复用同一协议：

```text
Audit（只读现实核对，含隐藏 Authoring 源与全部 Consumer）
   → Shadow Build（新 XLSX + Generated，Legacy 仍是 Authority）
   → Equivalence（逐条逐字段 100%）
   → Registry（Runtime 只读定义 / 查找）
   → Consumer Cutover（一次性枚举全部消费者；不得 dual-read 长期化）
   → Product Verification（真实产品链 + 全量测试）
   → Legacy Retirement（只删本领域，边界严格）
```

**候选顺序**（可依真实依赖审计调整；**不是** D-019 的新冻结项）：

```text
Monster + Enemy Presentation
   → Character / Hero
   → Skill / Status
   → ItemCard
   → Person / Profession
   → BattleCard
   → Remaining Static Config
```

**注意**：`Person / Profession` 已有 `PersonDefinitionCatalog / ProfessionDefinitionCatalog`（Catalog 形态，见 Current Architecture），迁移时必须**先审计其既有形态**，避免产生第二套定义；**PersonId Identity Authority 与 PersonBootstrap 语义不得改变**（D-019 边界）。

---

# 17. Hard Stops

遇到以下任一情况：**停止 Migration**，**不得**用兼容垃圾掩盖问题：

| # | Hard Stop |
|---|---|
| H1 | Shadow 数据与 Legacy **不等价**（任意字段 / 任意条目） |
| H2 | Save 持有 **SO 引用**或静态配置对象引用 |
| H3 | 发现**未纳入 Consumer Map 的真实读取路径**（§0.2 之外的消费者） |
| H4 | **Fresh rebuild 不能恢复**（§14 失败） |
| H5 | Generated Artifact 策略导致**产品引用断裂**（Scene/Prefab 引用 per-entry 生成资产） |
| H6 | Unity **Runtime 依赖 Editor assembly** / `AssetDatabase` |
| H7 | 同一领域出现**双人工 Authority**（CSV 与 XLSX 都能改） |
| H8 | Build 失败但**部分更新**了正式 Mirror |
| H9 | 正式 Battle **需要手工逐怪物拖引用** |
| H10 | 迁移期间发现需要改变 **Gameplay Rule / Save 语义 / Person Identity** 才能完成 |
| H11 | **Generated Root 无法在 Fresh Rebuild 后通过固定路径加载**（§3.3） |
| H12 | 产品 **Scene / Prefab 直接依赖非 Git Generated GUID** |
| H13 | **M2 之后旧 `ImportAll` 仍能重建 Monster SO** |
| H14 | **M2 之后 `SetupDemoData` 仍有 inline Monster definition** |
| H15 | **Shadow XLSX 在 Cutover 前被当作第二人工 Authority**（人工改 Shadow 当成产品内容变更） |

**上报要求**：Hard Stop 必须写清 **现象 / 证据（文件:行）/ 影响范围 / 需要的决策**，然后**停下**。

---

# 18. Explicit Non-Goals

本 FDS **不授权**：

| # | 不做 |
|---|---|
| N1 | 一次迁全部配置领域（必须 domain-by-domain） |
| N2 | 删除 `DataManager` **整体**（只允许删其 Monster 路径，且仅限 Wave 1F） |
| N3 | 删除 `ExcelImporter` **整体**（只允许删其 Monster 分支） |
| N4 | 删除**所有** ScriptableObject（D-019：不写成「所有 SO 废弃」） |
| N5 | 改 **Gameplay 规则**（Battle / Expedition / Village / Person 一律不动） |
| N6 | 改 **Save 语义**（含 SaveVersion、字段含义、保存时机） |
| N7 | Event System 重构 |
| N8 | Addressables 迁移 |
| N9 | Mod / Hot Update |
| N10 | UI 正式美术重排（含 Preview / 表现层样式） |
| N11 | **全仓 `*.json` normalize**（禁止把 `*.json text` 改成 `eol=lf`；只允许未来对正式 Generated 路径做 path-specific 规则，见 §7.3） |
| N12 | 修改正式 `Docs/Architecture`、GameSpec、GameDesign、项目状态快照 / 日志 / 交接（本文件**只新增自己**） |

---

# 19. FDS 状态 与 待 Review 的问题

## 19.1 状态

* 本文件：**Migration FDS V0.2 —— Ready for Final FDS Review**；**未实现任何代码**、**未执行任何迁移**。
* 本文件**不**改 D-019；D-019 的 Target Architecture 与 Not Frozen 清单**继续有效**。
* 本文件登记 Spike 分支为**只读实验 Evidence**（`spike/config-architecture-a @ 3532351d…`），**不 merge**。

## 19.2 已裁决项（本轮收敛，Q1–Q9 全部关闭）

| 原问题 | 裁决 | 落位 |
|---|---|---|
| Q1 Generated Unity Artifacts 是否入 Git | **Wave 0/1 V0.1：不入 Git**（Migration V0.1 Implementation Decision，**不**升级 D-019 为永久规则）；Fresh Rebuild = Hard Gate | §8、§14、§17 H11 |
| Q2 正式目录命名 | **接受** `Tools/RRPG.ConfigCompiler/`、`Development/Config/`、`Development/SourceAssets/`、`Development/Generated/StaticConfig/`、`Assets/{Generated,Scripts,Editor}/StaticConfig/` | §3.1、§6.1 |
| Q3 `enemyBodyPlaceholder` | **原裁决：保留为 fallback（不删除）** → **已被 M3 实现取代（2026-09-23）**：M3 把 fallback 移入 `EnemyPresentationResolver`（D-M3-6 / D-M3-7；字段零读取），**M4 物理删除字段与 Scene 序列化条目**（M3-R2 关闭；FDS §9 Wave 1F / §10.5） | §9 Wave 1D、§10.3、M3 Closeout §8·§9 |
| Q4 `SetupDemoData` | **Demo 改读新 Registry**（inline Monster definition 必须 M2 消失；不默认删除整个 Demo） | §9 Wave 1C、§10.4-C |
| Q5 `MonsterData` 类型 | **M4 最后删除**（引用=0 / 资产=0 / Compile PASS / Tests PASS） | §9 Wave 1F、§10.5 |
| Q6 `EnemyBehaviorDemoConfig` | **Enemy AI / Behavior domain：本 Wave 不迁**；只验证 `MonsterId` 仍可解析 | §9.1 |
| Q7 两本 Workbook | **保持两本**（理由见 §6.2） | §6.2 |
| Q8 `!Tools/**/*.csproj` 宽例外 | **保持**；已由 Repository Infrastructure 正式接受，Migration **不再讨论** | §6.2 / 无关本 FDS |
| Q9 Producer 入口形态 | **Wave 0 V0.1 = 仓库根三个 `.cmd`**（Migration V0.1 Implementation Choice，≠ 永久 UI Authority） | §15 |

> **当前没有阻塞 Wave 0 开始的制作人未决项。**

## 19.3 Implementation 阶段可能触发 Hard Stop / 重新 Review 的风险（**不是**未决问题）

以下在实施中**可能**触发 §17，届时按 Hard Stop 流程上报：

* Fresh Rebuild 无法通过固定路径加载 Generated Root（H11）；
* 产品 Scene/Prefab 出现对非 Git Generated GUID 的依赖（H12）；
* M2 后仍存在旧静态内容生成路径（`ImportAll` / `GameSetupDemoData` / Shadow 被当内容源）（H13–H15）；
* 发现 §0.2 Consumer Map 之外的**真实** Monster 读取路径（H3）；
* `MonsterData` 类型在 M4 仍有引用（则 M4 延后，不提前删除）。

## 19.3 变更记录

| 日期 | 版本 | 说明 |
|---|---|---|
| 2026-09-22 | V0.1 | 首版：只读 audit（§0）+ Wave 0/1 计划 + Promotion Manifest + Mirror/Git 契约 + Cutover/Hard Stop/验收矩阵 + Non-Goals |
| 2026-09-22 | **V0.2** | Final Review 语义收敛：**(1)** Generated Unity Artifacts 不入 Git 定为 Wave 0/1 V0.1 决策（§8，Migration V0.1 Decision，不改 D-019）；**(2)** 冻结 Wave 0 Runtime Root 策略（§3.3：单一 Root + exact path 加载，非 LoadAll 扫描）；**(3)** 目录收敛为 `Development/Generated/StaticConfig/` + mirror path-specific `text eol=lf`（§6.1/§7.3）；**(4)** 两本 Workbook 定为 Wave 1 决定（§6.2）；**(5)** **拆开** Monster Definition Authority Gate（M2，§10.2）与 Enemy Presentation Authority Gate（M3，§10.3）；**(6)** M2 必须技术性禁用 Legacy Authoring 写入口（§10.4 A/B/C）；**(7)** M2 逻辑退休 / M4 物理清理顺序与 `MonsterData` 最后删除条件（§10.5）；**(8)** `enemyBodyPlaceholder` = fallback（§9 Wave 1D）；**(9)** `EnemyBehaviorDemoConfig` 归 Enemy AI domain 不迁（§9.1）；**(10)** SetupDemoData 边界（§9 Wave 1C）；**(11)** Compiler 网络措辞修正为「正常 validate/build 不访问网络；首次 restore 可能需网络」（§5.2）；**(12)** Producer 入口 V0.1 = 根目录三个 `.cmd`（§15）；**(13)** Q8 从待裁决移除（已由 Repository Infrastructure 接受）；**(14)** Shadow Authority 收紧（§9 Wave 1A）；**(15)** Commit 策略更名为 Authority/物理分离（§11.1）；**(16)** Hard Stop 新增 H11–H15（§17）；**(17)** §19 待裁决项全部收敛为已裁决 + Implementation 风险清单 |
| 2026-09-23 | **V0.3（M4 收口更新）** | **(1)** §0.4 追加 M4 更新注（该节为 2026-09-22 只读快照；`enemyBodyPlaceholder` 已零读取并物理删除）；**(2)** §9 Wave 1D 的 fallback 表述更替为「Resolver 内 fallback + WarnOnce」（M3 D-M3-6 / D-M3-7 实现决定），并记录该字段于 M4 物理删除；**(3)** §10.3 Cutover 结果行同步为「M3 降级为 Resolver 内 fallback → M4 物理删除」；**(4)** §19.2 Q3 记录**原裁决被 M3 实现取代**的原因（fallback 落位改变 + 唯一取图路径仍是 Resolver）；**(5)** §10.5 / §9 Wave 1F 的 M4 物理清理判据与本次实施一致（引用=0 / 资产=0 / Compile PASS / Tests PASS），`MonsterData` 类型最后删除。 |










