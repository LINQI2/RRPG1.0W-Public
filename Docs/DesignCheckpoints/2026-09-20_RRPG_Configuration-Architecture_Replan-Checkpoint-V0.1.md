# RRPG Configuration Architecture Replan Checkpoint V0.1

> 日期：2026-09-20
> 定位：Pre-Spike Architecture Decision Checkpoint
> 状态：RESOLVED / SUPERSEDED BY D-019（2026-09-22）
>
> **2026-09-22 收尾（Spike A T1–T4 已完成）：**
> 本 Checkpoint 登记的长期原则、已否决方案与 Spike Questions 已由实验回答并归位：
>
> - 长期静态配置生产架构 → [决策记录](../决策记录.md) **D-019**（Target Architecture 已确认；Production Migration 尚未开始）；
> - Current / Target 分层 → [技术架构总览](../Architecture/技术架构总览.md) `# Static Configuration Architecture`；
> - 长期术语 → [核心对象与术语映射](../Architecture/核心对象与术语映射.md) §14。
>
> **原正文按 Pre-Spike 历史记录原样保留，不得重写。**
> §8 的 Spike Questions **已不再是当前未决开发任务**；§6 / §7 的 Candidate Organization 仍属**未冻结**建议，
> 不得当作结构 Authority。本文件不拥有 Current Architecture Authority，也不构成迁移授权。
>
> 本文件记录进入 Configuration Architecture Spike A
> 之前已经确认的长期原则、已否决方案和必须通过实验回答的问题。
>
> 本文件不是 GameSpec，
> 不改变 Gameplay Rule；
> 也不是最终 Architecture，
> 不得把仍标记为 Spike Question 的内容解释为已冻结实现方案。

> **取代关系：**本 Checkpoint 取代
> `Docs/DevelopmentSpecs/2026-09-20_RRPG_美术批量导入与Art-Import-Skill规划_V0.1.md`
> （该文件已于 2026-09-20 标记 `SUPERSEDED / DO NOT IMPLEMENT`，正文按历史规划记录原样保留）。
> 取代范围包括该文件的 CSV Art Import 方向与“下一开发任务”结论；
> 该文件经过真实验证的工程技术经验以 Reusable Technique 形式继承（见 §5），但不拥有新架构 Authority。

> **本轮性质：**纯规划纠偏 / Pre-Spike Architecture Checkpoint。
> 不实现代码，不启动 Unity，不修改任何 Gameplay / Runtime / Scene / Prefab / SO / Save。
> 本文件不代表任何功能已经实现，也不改变 Current Gameplay / Architecture Authority。

---

# 0. 三类内容必须分开读

本文件同时记录“已确认方向”“已否决方案”“尚未冻结问题”。三者混淆是本次重规划要避免的主要风险。

| 标记 | 所在章节 | 含义 | 约束力 |
| --- | --- | --- | --- |
| **Confirmed Principle** | §3 | 已经确认的长期原则 | 有长期约束力；变更需要新的明确裁决 |
| **Rejected** | §4 | 已经否决的方案 | 不得再作为默认方案提出 |
| **Reusable Technique** | §5 | 旧 CSV WIP 可继承的工程经验 | **只有技术参考价值，无新架构 Authority** |
| **Candidate Organization** | §6 / §7 | 候选目录 / Workbook / Source Art 组织 | 可调整，**未冻结** |
| **Spike Question** | §8 | 必须通过实验回答的问题 | **无约束力**，不得解释为 Current Architecture |

> 把 §8 的 Spike Question 当作已冻结实现方案、
> 或把 §6 / §7 的 Candidate 当作最终结构引用，都属于误读本文件。
> 本文件也不得被当作 Current Architecture 写入其他文档。

---

# 1. 为什么重新规划

## 1.1 当前配置生产来源是历史混合的

RRPG 当前配置生产来源存在历史混合：

- CSV（`Development/Excel/*.csv`）
- ScriptableObject（per-entry SO）
- Catalog（Catalog 资产）
- C# Seed / Initializer
- C# Content Builder
- Scene / Prefab Inspector 序列化
- 测试 Seed

## 1.2 旧链条

```text
CSV
→ ExcelImporter
→ per-entry SO / Catalog
→ Resources.LoadAll
→ DataManager
```

该链在原型期可用，但存在长期问题：

- 人工 Authoring 来源不统一；
- 运行时静态配置 Representation 不统一；
- 多处存在潜在重复 Authority；
- 不适合长期 AI / Git / 大规模内容生产；
- 不能仅因为已有实现存在就默认长期保留。

## 1.3 本轮重规划覆盖范围

因此本轮重规划同时覆盖：

- A. Authoring Layer
- B. Build / Compiler Layer
- C. Static Configuration Runtime Layer

**但不自动重构 Game Runtime。**
Game Runtime 的保护边界见 §3.13。

---

# 2. 本 Checkpoint 的边界

本文件是 **Pre-Spike** 记录，只负责：

1. 记录为什么重新规划（§1）；
2. 登记已经确认的长期原则（§3）；
3. 登记已否决方案（§4）；
4. 登记可继承的工程经验（§5）；
5. 登记候选组织方式（§6 / §7）；
6. 登记必须由实验回答的问题（§8）；
7. 定义下一实验 Spike A 的目标、边界与验收门槛（§9 / §10 / §11）。

本文件**不负责**：

- 不冻结具体技术选型；
- 不定义 Runtime Schema；
- 不修改 Gameplay Rule；
- 不定义 Migration 步骤；
- 不替代 GameSpec / Architecture / 项目状态快照 / 开发验收交接；
- 不因为本文件存在就认为配置重构已经批准实施。

---

# 3. 已经确认的长期原则（Confirmed Principle）

## 3.1 Authoring Authority

- 制作人长期主要 Authoring 使用 **XLSX**。
- **CSV 不再作为长期人工 Authoring Authority。**
- Generated JSON / MD / C# / SO / Catalog **均不得成为第二个人工 Authority**。
- 数据应可由 Authoring Source **确定性重建**。

## 3.2 Workbook / Sheet 边界

- **Sheet = 数据语义粒度。**
- **Workbook = 协作所有权 / Git / Agent 写锁粒度。**
- 不追求 Workbook 越少越好。
- 强关联主表 + Child Sheet 应保持**同一个 Workbook**。
- 天然可并行工作的领域允许拆 Workbook。
- Importer / Compiler 必须支持扫描**多个 Workbook**，并建立全局 Registry / Cross-Workbook Validation。

## 3.3 配置表 UX

RRPG **不机械复制**公司表格式。设计目标按以下优先级：

1. 制作人可读性
2. 扩展方便
3. 减少重复维护
4. 维护稳定
5. AI 易理解 / 易安全修改
6. Git / Review 友好
7. 最后才考虑少写程序

当前 Header 方向：

> 同一列中必须**同时存在**：稳定机器字段、强类型、约束 / 默认、中文含义、数据。

当前推荐布局：

```text
Row 1 = machine field
Row 2 = type
Row 3 = rule / default
Row 4 = 高视觉权重中文 Header
Row 5+ = data
```

说明：

- 精确视觉样式仍允许在 Spike 中微调；
- 但「**中文语义与机器 Schema 同列、不拆双份维护**」是长期原则。

## 3.4 不引入无意义 c/s

- RRPG 当前是**单机 Unity 项目**，没有 client / server 配置分流需求。
- 因此当前**不增加**无信息量的 c/s Export 行。
- 未来只有出现**多个真实输出目标**时，才重新引入 export target metadata。

## 3.5 ID / Reference UX

- **Stable ID 是机器 Authority。**
- **DisplayName 只辅助人类识别。**
- 制作人**不应被迫背内部 ID**。
- XLSX 应支持：可读名 + ID 下拉 / 选择体验。
- AI / Generated Artifact 可以直接使用 canonical ID。
- DisplayName 重名时由 ID 消除歧义。

## 3.6 数据表达

长期**不使用**：

- `a;b;c` 表示不定长实体列表；
- `Effect_1 / Effect_2 / Effect_3` 表示可扩展重复结构。

规则：

- 不定长 1:N Entity Relation → **Child Sheet**；
- 固定语义 Tuple → **同行固定列**。

例：

- `Skill` + `SkillEffect`；
- `Status` + `StatusEffect`；
- `Character` + `CharacterSkill`。

## 3.7 一键生产

内部允许多阶段：

```text
XLSX
→ Compiler
→ IR
→ Unity-specific Build
→ Runtime Artifact
```

但制作人正式操作必须收敛为：

- `Validate`
- 或 `Build All`

**不得**要求制作人逐个替换 JSON、逐个生成 C#、逐个打开 Catalog 拖资源。

AI / Cline / CI 应使用**同一确定性 Pipeline**。

## 3.8 Transactional Build

Build 必须：

```text
Read All
→ Validate All
→ Cross Reference Validate
→ Build Temp Output
→ Verify
→ Atomic Replace
```

- 任一 Hard Error：**正式输出 0 更新**；
- 禁止半张表新、半张表旧。

## 3.9 AI / Git 可读生成物

- XLSX 是**人类 Authoring**；
- Build 应产生适合 AI / Git Diff / CI / Debug 的**机器可读 Generated Artifact**；
- Canonical JSON 当前是主要候选 IR，但其**精确 Schema 与 Runtime 是否直接消费仍属于 Spike Question**（见 §8）。

## 3.10 Unity-native Authoring 边界

以下不因为配置体系重构而强制 XLSX 化：

- Scene 结构
- Prefab 结构
- Animation
- Material
- Timeline
- 其它 Unity 原生编辑体验明显更优的对象

配置表可以决定：

> “某个槽位使用哪个内容”。

但不负责：

> “Canvas / Prefab 的空间结构怎么搭”。

## 3.11 UnityEngine.Object

- Sprite / Prefab / AudioClip / Material 等 `UnityEngine.Object` **不要求** Canonical JSON 直接持有对象引用。
- 长期方向：

```text
Stable AssetId
→ Unity-specific Generated Asset Catalog
→ UnityEngine.Object
```

- 具体 Catalog 形态仍由 Spike 验证（见 §8 问题 8）。

## 3.12 Static Config Runtime 可以重审

不得因为当前存在以下实现就默认长期保留：

- per-entry ScriptableObject
- `Resources.LoadAll`
- DataManager

允许重新评估：

- Pure Strong-Typed Definition
- Strong Registry
- Generated Config Database
- Runtime JSON
- Generated Unity Config Asset
- Compatibility Facade

但必须**通过 Spike 证据**选择。

## 3.13 Game Runtime 保护边界

本次配置架构重规划**不自动推翻**：

- `PersonId` Identity Authority
- `CharacterInstance` / `HeroInstance` 长期人物状态
- `ProfessionProgress`
- `RunState`
- `ExpeditionProgressionState`
- `BattleUnit`
- Village Runtime
- Save ID 语义
- Battle / Expedition / Village Gameplay Rule

> 配置架构迁移只处理 **Static Configuration Source / Build / Representation**。
> 配置架构重构不得顺手改变上述运行时语义。

---

# 4. 已否决方案（Rejected）

以下方案已被明确否决，**不得**再作为默认方案提出；重新引入需要新的明确裁决与实验证据。

| # | 已否决方案 | 否决要点 |
| --- | --- | --- |
| R1 | CSV 作为长期人工 Authority | 与 §3.1 冲突；人工 Authoring 必须统一到 XLSX |
| R2 | CSV + Inspector / Catalog 双人工 Authority | 直接违反“Generated Artifact 不得成为第二个人工 Authority”（§3.1） |
| R3 | `Incoming` 作为正式长期 Source Art Authority | Source Art 需要可重建的正式 Authority（§7） |
| R4 | 「空白 = keep old value」的长期增量 Patch Authoring 模型 | 无法从 Authoring Source 确定性重建；语义隐式、易漏改 |
| R5 | `@CLEAR` 作为全量配置体系核心语义 | 是增量 Patch 模型的补丁语义，不构成完整配置体系 |
| R6 | 每种美术各造一套独立 Importer | 重复 Authority、重复校验、长期维护成本不可控 |
| R7 | 为了少写程序优先牺牲制作人长期维护 | 违反 §3.3 的 UX 优先级 |
| R8 | 单个 `Everything.xlsx` 巨型工作簿 | 破坏 §3.2 Workbook = 协作所有权 / 写锁粒度 |
| R9 | 一张 Sheet 一个 Workbook 的极端碎片化 | 破坏强关联主表 + Child Sheet 同 Workbook 原则 |
| R10 | 把所有 Unity-native Asset 都强制迁 XLSX | 违反 §3.10 Unity-native Authoring 边界 |
| R11 | 为了统一而把所有数据实例生成巨大 C# 源码 | 生成物体积 / 编译成本不可接受；Codegen 用途另见 §8 问题 9 |
| R12 | 未经实验直接冻结 `Resources` / DataManager / SO | 违反 §3.12 必须以 Spike 证据选择 |
| R13 | 未经实验直接宣布 JSON Runtime 为最终方案 | Canonical JSON 目前只是**候选 IR**（§3.9） |

> 说明：R1–R5 直接来自旧 CSV 规划与旧增量 Patch 语义的退役范围；
> R6 / R10 / R11 是本次重规划为长期可维护性而否决的工程方向。

---

# 5. 旧 CSV WIP 可继承的工程经验（Reusable Technique ≠ Architecture Authority）

旧 CSV Art Batch Import V0.1 实验未 Commit、未 Push、未进入 Product History，已仓库外归档。
其中经过真实验证的工程经验仍可供新架构参考，但**不拥有新架构 Authority**，
也不得据此恢复旧 CSV Authority 或旧增量 Patch 语义。

可继承的技术经验：

1. **Dry Run / Apply 两阶段**：先完整计划，再执行修改；
2. **Hard Error 时零写入**：任何 Hard Error 阻断整批，不允许部分成功；
3. **先完整计划再修改**；
4. **stable generated asset path**：生成物路径稳定，可重复定位；
5. **`.meta / GUID` 更新保护**：更新既有图片不得造成引用断裂；
6. **`TextureImporter` / `AssetDatabase` 在团结引擎兼容环境的可用经验**；
7. **Import Report**：机器与人都可审计的导入报告；
8. **幂等 Apply**：重复执行不产生额外差异；
9. **Catalog 回读验证**：Apply 后回读并核对；
10. **坏批次阻断**：坏数据不污染正式输出；
11. **测试隔离经验**：实验产物与正式资产隔离；
12. **不依赖制作人逐个 Inspector 绑定的方向**。

> 结论：**Reusable Technique ≠ Architecture Authority。**
> 这些经验在 Spike A 中只能作为实现手段参考，
> 不能被引用为新架构已经验证过的证据，也不能反向决定 §8 的 Spike Question 答案。

---

# 6. 当前候选 Workbook 方向（Candidate Organization，未冻结）

> 本节只记录**候选组织方式**，不是最终结构。
> 最终数量与边界允许根据实际并发 / 协作所有权 / 生命周期调整。

候选结构：

```text
Development/Config/
├─ People/
│  ├─ RRPG_People.xlsx
│  └─ RRPG_Professions.xlsx
├─ Combat/
│  ├─ RRPG_Characters.xlsx
│  ├─ RRPG_Monsters.xlsx
│  ├─ RRPG_SkillsAndStatus.xlsx
│  ├─ RRPG_ItemCards.xlsx
│  └─ RRPG_BattleCards.xlsx
├─ Presentation/
│  ├─ RRPG_PersonPresentation.xlsx
│  ├─ RRPG_EnemyPresentation.xlsx
│  └─ RRPG_WorldPresentation.xlsx
└─ System/
   └─ RRPG_SystemTuning.xlsx
```

说明：

- 文件夹 + Workbook 名承担**一级导航**；
- 每本可有局部 `00_导航` Sheet；
- 自动生成的 `__Lookup`（供下拉使用）由 Compiler 生成，**制作人不手工维护**；
- 候选结构只用于 Spike A 的局部验证，不得被解释为已经批准的全项目配置结构。

---

# 7. Source Art 方向

确认：长期需要**可重建的 Source Art Authority**。

当前候选：

```text
Development/ArtSource/
```

**不是**旧目录：

```text
Development/ArtImport/Incoming/
```

- `Incoming` / `Inbox` 如果未来保留，**只能作为“待整理素材箱”**；
- **不得**成为正式 Build Authority。

正式美术资源关系方向：

```text
Source File
→ stable AssetId
→ Generated Unity Asset Path
→ Asset Catalog
```

- Source 文件名变化**不得**迫使 Gameplay / Presentation Binding 整体改 Key；
- 具体目录与命名在 Spike 验证（§8 问题 8）。

---

# 8. Spike Questions（尚未冻结）

> **以下 14 项全部尚未冻结。**
> 任何一项都**不得**被解释为 Current Architecture，也不得在实验前写成实现方案。
> 答案只能来自 Configuration Architecture Spike A 的实验证据（§9）。

| # | Spike Question | 当前状态 |
| --- | --- | --- |
| 1 | Config Compiler 技术栈：Python / .NET / Node / Luban？ | 未冻结 |
| 2 | XLSX 库：openpyxl / ClosedXML / ExcelJS / 其它？ | 未冻结 |
| 3 | Canonical IR：JSON 具体 Schema？ | 未冻结 |
| 4 | Pure Config Runtime：Runtime JSON deserialize？Generated Config Asset？Binary？其它？ | 未冻结 |
| 5 | DataManager：退休？Compatibility Facade？其它？ | 未冻结 |
| 6 | per-entry SO：哪些退休？哪些保留？ | 未冻结 |
| 7 | Strong Registry：API / 生命周期 / 加载方式？ | 未冻结 |
| 8 | Visual Asset Catalog：单全局？多领域？Root 组合？ | 未冻结 |
| 9 | C# Codegen：Enum / ID / Schema / 不生成？ | 未冻结 |
| 10 | MD：是否值得长期生成？ | 未冻结 |
| 11 | Luban：只借鉴？部分采用？完整采用？ | 未冻结 |
| 12 | XLSX 修改工具：如何安全保留样式 / dropdown / freeze pane / validation？ | 未冻结 |
| 13 | Generated JSON 是否进入游戏包？ | 未冻结 |
| 14 | Generated Mirror 最终采用 JSON / TSV / 两者？ | 未冻结 |

> 记录规则：只有当 Spike A 给出可复现证据，且制作人 / 开发顾问明确裁决后，
> 相应问题才可从本表移出并升级为 Confirmed Principle。

---

# 9. 下一实验：Spike A

正式名称：

> **Configuration Architecture Spike A — Monster + Enemy Visual Vertical Slice**

定位：

> **EXPERIMENT / NOT MIGRATION**

## 9.1 为什么选 Monster

- 数据结构简单；
- 已有 CSV；
- 已有 `MonsterData` SO；
- 已有 DataManager consumer；
- 已有 Battle consumer；
- `MonsterId` 稳定；
- Enemy Visual Binding 尚缺；
- 可以同时验证纯数据 + Unity Sprite。

## 9.2 Spike 目标

1. 验证多个 XLSX Workbook；
2. 验证多行 Header / UX；
3. 验证 Cross-Workbook Reference；
4. 验证外部 Config Compiler；
5. 验证 Canonical IR；
6. 对比 Pure Config Runtime 候选方案；
7. 验证 Strong Monster Registry；
8. 验证 Stable AssetId；
9. 验证 Generated Unity Asset Catalog；
10. 让 Battle 中不同 Monster 显示独立 Sprite；
11. 缺图保持当前 enemy placeholder fallback；
12. 验证一键 Build；
13. 验证从 Authoring Source 零生成物重建；
14. 验证 AI / Git Review 体验。

## 9.3 Spike 不得自动做的事

- 迁 Person
- 迁 Profession
- 迁 Skill 全体系
- 迁 BattleCard
- 删除 DataManager
- 删除旧 `MonsterData`
- 删除旧 CSV
- 重构 Battle Rule
- 修改 Save
- 把实验实现扩散为正式 Architecture

> Spike A 的产物是**证据**，不是**迁移结果**。
> 实验范围内的实现不得被引用为“配置架构已经完成迁移”。

---

# 10. Spike A Producer Experience Gate

Spike A 必须记录制作人最终真实试用。制作人**只执行**：

1. XLSX 将史莱姆 HP 改一个值；
2. 给一个 Monster 换测试图；
3. 新增一个测试 Monster；
4. 一键 Build。

**不得**要求制作人：

- 手工 Catalog Size +1；
- 手工拖 Sprite；
- 手工替换 JSON；
- 手工生成 C#；
- 多阶段逐项执行。

游戏 / Registry 必须能看到对应结果。

> **技术全绿但制作人流程仍复杂，不得视为 Spike 成功。**

---

# 11. Spike A 成功后也不是自动 Migration

必须明确以下顺序：

```text
Spike A
↓
Architecture Decision
↓
制作人 / 开发顾问确认
↓
Migration FDS
↓
正式迁移
```

**不允许**：

```text
Spike A 能跑
↓
直接迁全项目
```

> Spike A 通过只证明候选方向可行为，不等于 Migration 已获批准。

---

# 12. 下一步

本 Checkpoint 落仓后：

- **不开启实现**。

下一节点是：

> 新对话 → Configuration Architecture Spike A T0

T0 只读检查：

- 当前 Repository / Authority；
- Monster Consumer Map；
- Enemy Visual Consumer Map；
- DataManager / Resources / MonsterData；
- 当前工具环境；
- Config Compiler 候选环境；
- Unity / 团结引擎限制；
- 当前测试基线；
- 是否存在隐藏 Authoring 入口。

T0 之后才决定具体 Spike 实现任务。

---

# 13. 范围与验证（本轮）

本轮为**纯文档**任务：

- 不实现代码；
- 不启动 Unity；
- 不需要 Unity 编译 / EditMode / PlayMode；
- 不修改任何 Gameplay / Runtime / Scene / Prefab / SO / Save。

本轮允许修改 / 新增的文件只有：

1. `Docs/DevelopmentSpecs/2026-09-20_RRPG_美术批量导入与Art-Import-Skill规划_V0.1.md`（仅头部退役标记，正文保持历史原样）；
2. 本文件 `Docs/DesignCheckpoints/2026-09-20_RRPG_Configuration-Architecture_Replan-Checkpoint-V0.1.md`。

除此之外**零修改**：

- 不修改文档地图；
- 不修改状态快照；
- 不修改项目日志；
- 不修改 Handoff / 开发验收交接；
- 不修改 Architecture；
- 不修改 GameSpec。

验证方式：

- `git diff`
- `git diff --check`
- 精确回读两份文档
- 确认旧文件 `SUPERSEDED` 标记醒目
- 确认本文件 Confirmed / Rejected / Spike Questions 三类没有混淆
- 确认无第三个文件被修改

---

# 14. 一句话摘要

> RRPG 的配置生产从“CSV + Inspector 混合人工 Authority”转向
> **XLSX 单一人工 Authoring Source + 确定性 Compiler + 可重建 Generated Artifact**，
> 运行时 Representation 与 Compiler 技术栈**尚未冻结**，
> 由 Configuration Architecture Spike A（Monster + Enemy Visual）以实验证据决定。

---

> 状态提醒：本文件为 **Pre-Spike Checkpoint，已于 2026-09-22 退役（RESOLVED / SUPERSEDED BY D-019）**，
> 不代表 Current Architecture，也不代表已批准实现；其 Spike Questions 不再是当前未决开发任务。
> 任何把本文件 §6 / §7 / §8 内容当作已冻结结构或已冻结技术选型的引用都属于误读。
> 现行长期结论见 [决策记录 D-019](../决策记录.md)、[技术架构总览](../Architecture/技术架构总览.md) 与 [核心对象与术语映射](../Architecture/核心对象与术语映射.md)。
