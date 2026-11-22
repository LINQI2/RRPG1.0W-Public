# DevelopmentSpecs 状态导航

> 2026-10-10 V17-R3-A：[人物详情执行记录](2026-10-10_RRPG_V17-R3-A_Person-Detail_Execution-Record.md)。**R3-A PRODUCT ACCEPTED / Final Closeout Prepared**；制作人已明确「验收通过」。Disposable清理后完整Compiler264/264、EditMode1495/1495、PlayMode111/111通过；验收后最新存档及22/22保护核验一致。Handoff80已落盘；生成时Commit/Push尚未执行。R3-B未启动，Authority及Deferred边界保留。下方旧阶段条目按发布日期读取。

> 2026-10-09 V17-R3-P：[人物详情与集中管理规划](2026-10-09_RRPG_V17-R3_Person-Management_Development-Spec-V0.1.md)获制作人规划验收，**R3-P PLANNING ACCEPTED / PUBLISHED**；仅6份Markdown。Authority对照、A/B最小片、迁移与测试矩阵已采用，成长关闭/撤职劳动意图/关系与历史查询缺口保留。R3-A/B未实现；发布成功后下一阶段R3-A，已获连续推进确认，本次不实施；B未授权，Issues84/85/86/88不关闭。

> 2026-10-09 V17-R2：**Work Package Closed / Published main / Technical Prototype PASS / Art Master NOT FROZEN**。Closure2a52f85已正常Push并三方核验；清理后本轮Compiler264/264、EditMode1492/1492、PlayMode109/109 PASS，保留1 Edit / 2 Play技术契约。默认A、B可逆，Tavern/Farm与Test02稳定布局修复已验收；最新存档及22项保护完整，实际32路径与manifest一致。证据见[V17-R2执行记录](2026-10-09_RRPG_V17-R2_Execution-Record.md)，不冻结美术或进入下包。

> 2026-10-09 开发17.0：V17-R1动态Farm / Kitchen闭环Work Package Closed / Product Accepted / Published main；Closure dbea0b5已正常Push并三方核验。实现、Disposable清理、清理后全量测试与实际发布证据见[V17-R1执行记录](2026-10-08_RRPG_V17-R1_Execution-Record.md)。仅此窄包已关闭，不冻结完整V17路线，不进入下一工作包。下方V16 Deferred按原阶段读取。

> V16-P/A/B/C1/C2/D全阶段已验收并发布main；D Closure `97ea2598945b48f682ef34f042f4f5074d6c04d1`已正常Push核验。清理后Compiler264/264、EditMode1485/1485、PlayMode106/106 PASS；D仅Building PNG/Sprite，不授权新范围。

> 2026-10-08 V16-C2：制作人验收通过；Work Package Closed / Published main，发布状态以累计记录和实时Git为准。清理后Compiler257/257、EditMode1483/1483、PlayMode105/105 PASS；动态资源产出 Deferred；D Work Package Closed / Product Accepted / Published main。

> 2026-10-08 V16-C1：制作人验收通过；Work Package Closed / Published main。清理后Compiler252/252、完整EditMode1477/1477、PlayMode103/103 PASS；C2 Work Package Closed / Product Accepted / Published main，D Work Package Closed / Product Accepted / Published main，后续由开发16.0安排。

> 最后核对：2026-10-08（V16-P/A已Published Main；B已获产品验收、Work Package Closed / Published main；C1 Work Package Closed / Product Accepted / Published main；C2 Work Package Closed / Product Accepted / Published main，D Work Package Closed / Product Accepted / Published main）
> 本文件只标注目录内资料的用途与当前状态，不建立新的玩法、设计或架构 Authority。文件正文保留发布时语境；首页的旧 `Ready for Execution / Landing` 不自动表示当前仍待执行。

使用顺序：先确认当前任务授权与 [项目状态快照](../项目状态快照.md)，再从本索引选择一份直接相关资料。历史实现过程查 [项目日志](../项目日志.md)和 Git History；玩家规则仍从 [GameSpec README](../../GameSpec/README.md)定位。

## Current Guide

| 文件 | 当前用途 | 边界 |
|---|---|---|
| [引擎场景层级与配置查找指南](../引擎场景层级与配置查找指南.md) | 制作人查UI、布局、卡片模板、字体 / 图片与数据入口 | 以2026-10-04 Editor回读为准；不冻结玩法 / 数值，后续场景变化同步 |
| [Battle Combat Feedback Foundation V0.1 实施报告](2026-10-02_Battle-Combat-Feedback-Foundation-V0.1-Report.md) | 真实 Delta、Presentation Batch、Player/Profile、Slider/Radial/Number 与替换接口 | 开发14.0 Technical Review PASS；纯 Closeout 白名单发布 main，生成时事实见 Handoff、最终发布查 Git；下一节点仅 Product/Game Design Gate，不启动新实现 |
| [Current UI TMP / Battle Surface V0.1 实施报告与配置入口](2026-10-01_RRPG_Current-UI-TMP-Battle-Surface-V0.1_Report.md) | 当前 TMP 字体、Battle HUD、Card Art / Description / Owner / Keyword 配置 | Implementation-only Foundation 已复核并 Push main；中文字体与 AP 无数字底图已接入；Card Art / Common Icon 内容 Pending，Portrait override 可选 |
| [美术素材配置操作手册 V0.1](2026-09-20_RRPG_美术素材配置操作手册_V0.1.md) | 当前跨领域美术操作入口 | 必须按领域分流；不冻结最终美术架构 |
| [Person Visual Binding Guide](2026-09-19_RRPG_Person-Visual-Binding-V0.1_Manual-Configuration-Guide.md) | Person Visual 当前手工绑定 | Implementation-only / Not Frozen |
| [Journey Event Illustration Guide](2026-09-19_RRPG_Journey-Event-Illustration-Binding-V0.1_Manual-Configuration-Guide.md) | Journey Event 插图当前手工绑定 | Implementation-only / Not Frozen，不是 Event Content Authority |

已迁移的 Monster Definition / Enemy Presentation 不从上述手工 Catalog 指南进入；使用 [Development/Config README](../../Development/Config/README.md)。

## Active / Ready

### 开发16.0规划入口

| 文件 | 当前状态 | Gate / 读取边界 |
|---|---|---|
| [Village Product Home + Building Foundation Development Spec V0.1](2026-10-07_RRPG_Village-Product-Home-and-Building-Foundation_Development-Spec-V0.1.md) | **P/A Published；B WORK PACKAGE CLOSED / PRODUCT ACCEPTED / PUBLISHED MAIN；C1 Work Package Closed / Published main；C2 Work Package Closed / Product Accepted / Published main；D Work Package Closed / Product Accepted / Published main** | P → A → B → C1 → C2 → D；V16-P与A已发布，B已获制作人验收并发布；C1 Work Package Closed / Product Accepted / Published main；C2 Work Package Closed / Product Accepted / Published main，D Work Package Closed / Product Accepted / Published main。规则先读 [GameSpec 矩阵](../../GameSpec/README.md)，技术目标查 [Architecture §27](../Architecture/技术架构总览.md#v16-product-world-target)，采用理由查 [Checkpoint](../DesignCheckpoints/2026-10-07_RRPG_Village-Visual-Interaction-and-Building_Architecture-Checkpoint-V0.1.md)。执行 Agent 只执行当前明确授权的 Gate，V16既定Gate已关闭，新Work Package须另行授权 |

**2026-10-07 当前交付增量**：V16-P Published；V16-A Foundation及追加修复Work Package Closed / Published main，Closure 3ec724b已正常推送并核验。清理后完整EditMode1472/1472、PlayMode102/102 PASS；发布事实查[累计V16 Execution Record](2026-10-07_RRPG_V16_Execution-Record.md)与[项目状态快照](../项目状态快照.md)。B已实施并技术验证，最终全量EditMode1474/1474、PlayMode102/102 PASS，制作人产品验收已通过，Work Package Closed / Published main；C1 Work Package Closed / Product Accepted / Published main；C2 Work Package Closed / Product Accepted / Published main，D Work Package Closed / Product Accepted / Published main；C2用户明确报告开发16.0已通过Remote Desktop Commander复核本地记录、测试代码与Git工作区；作为用户验收证据记录。

当前没有仅凭目录文件即可自动启动的新 Work Package。新的执行必须有用户当前授权、Target Branch 和现场审计。

`3P Shared Deck Gate 0`、WP-A～WP-E 与 WP-1 的开发规格都已经被后续实现使用；其正文仍是开发期契约与追溯材料，不是当前待办队列。

| 文件 | 当前状态 | 读取方式 |
|---|---|---|
| [WP-AL-4 Social Feedback Development Spec V0.1](2026-10-04_RRPG_WP-AL-4_Social-Feedback_Development-Spec-V0.1.md) | **Executed / Verified / Runtime Review PASS / Published Main** | Runtime 70a6534已审阅；当前职责见[Architecture §26](../Architecture/技术架构总览.md#wp-al-4-current)，最终72/248/1349/93证据及TMP exception见[Closeout](2026-10-05_RRPG_WP-AL-4_Social-Feedback_Closeout.md)。Spec保留发布时语境，不自动授权后续包 |
| [Autonomous Life Technical Architecture V0.1](2026-10-04_RRPG_Autonomous-Life_Technical-Architecture-V0.1.md) | **Target Technical Architecture / Reviewed**；Social与Activity已发布main；Selection / Arbitration已实现、Review PASS并发布main；WP-AL-4 Social Feedback已Implemented / Verified / Runtime Review PASS、Published Main（Target契约§29）；Event / Episode / Presentation仍Target | Current读[Architecture](../Architecture/技术架构总览.md)与[对象映射](../Architecture/核心对象与术语映射.md)，证据见[WP-AL-3 Closeout](2026-10-04_RRPG_WP-AL-3_Autonomous-Selection_Closeout.md)；不因部分实现把整份Target转为Current，不自动授权新的后续包 |
| [WP-Event-1 Unified Event Content Foundation Closeout Report](2026-09-26_RRPG_WP-Event-1-Unified-Event-Content-Foundation-Closeout-Report-V0.1.md) | **WP-Event-1 V0.1 收口证据**：范围（1A→1D D3 + D1-R + Producer Repair 1/2）/ 产品决定（≤D-021）/ 技术架构 / 运行时切换 / 表现 Authority / 制作人验收（**2026-09-26 Producer Play Acceptance PASS**）/ 最终验证 / Temporary·Not Frozen / 债务 / 文件分类 / Commit Plan（**已执行**：Commit `edaa424` + Push `origin/main`，见 §N）/ Next Product Step；状态 **`WP-Event-1 CLOSED`**（已 Commit `edaa4244b36b4da6ff5272f9716fb1e42a8ae935` / 已 Push `origin/main`） | 读当时收口证据；长期规则看GameSpec / GameDesign，Current架构与操作分别看Architecture / 配置手册；最终 Commit / Push 事实看该报告 §N |
| [WP-Event-1 Unified Event Content Foundation FDS](2026-09-26_RRPG_WP-Event-1-Unified-Event-Content-Foundation-FDS-V0.1.md) | **Executed / Published Main**；阶段Awaiting Acceptance只属当时语境 | 开发契约与Repair证据；Current操作查配置手册、Current接线查Architecture，最终验收/发布见Closeout §N；不作为待执行队列 |

## Closed / Executed FDS（Development-time Binding）

| 收口证据 | 状态 / 边界 |
|---|---|
| [WP-AL-4 Social Feedback Closeout](2026-10-05_RRPG_WP-AL-4_Social-Feedback_Closeout.md) | Executed / Verified / Runtime Review PASS / Published Main；72/248/1349/93、Config / Barrier / Historical Reciprocity与TMP例外；已发布main；不代表完整Target完成，正文保留发布前生成语境 |


| 文件 | 当前状态 | 读取方式 |
|---|---|---|
| [WP-AL-3 Autonomous Selection Development Spec V0.1](2026-10-04_RRPG_WP-AL-3_Autonomous-Selection_Development-Spec-V0.1.md) | **Executed / Verified / Runtime与Integrity Repair Review PASS / Published Main** | 实现时代码基线main@9c9a750；该包历史发布点main@b239c90e6370c39a6e21fd5986acad63820f95bd；Runtime e2dd368、Repair 0a87394已完成。Current职责读[Architecture §25](../Architecture/技术架构总览.md#wp-al-3-current)，96/228/1277/93及边界见[Closeout](2026-10-04_RRPG_WP-AL-3_Autonomous-Selection_Closeout.md)；原首页保留开发前语境，不自动授权后续包 |
| [WP-AL-1 Social Foundation Development Spec V0.2](2026-10-04_RRPG_WP-AL-1_Social-Foundation_Development-Spec-V0.2.md) | **Executed / Review PASS / 已发布 main（0c8722e）** | 实际开发基线 700343f；日期化首页保留发布时语境，证据见 [Closeout](2026-10-04_RRPG_WP-AL-1_Social-Foundation_Closeout.md)，最终发布查 Git |
| [WP-AL-2 Activity Lifecycle / Daily Integration Spec V0.2](2026-10-04_RRPG_WP-AL-2_Activity-Lifecycle-Daily-Integration_Development-Spec-V0.2.md) | **Executed / Runtime 与 Closeout Review PASS / 已发布 main（70754649 + 52aa035b）** | 实际开发基线 0c8722e；窗口 / 生命周期 / Save / 唯一 Day Tick 接线已实现；不包含自动 Selection，证据见 [Closeout](2026-10-04_RRPG_WP-AL-2_Activity-Lifecycle-Daily-Integration_Closeout.md)；不授权 WP-AL-3 |
| [WP-1 Expedition Progression FDS](2026-09-07_RRPG_WP-1_Expedition-Progression_Final-Development-Spec.md) | Executed / 历史 `Development Closed` | 查 WP-1 契约与验收边界 |
| [3P Shared Deck Gate 0 Spec](2026-09-17_RRPG_3P-Shared-Deck_Gate-0_Development-Spec.md) | Executed；文件内 `Ready for Execution` 是发布时状态 | 查 Gate 0 配置化卡牌效果架构的执行输入；后续状态查项目日志 |
| [WP-A Battle Dual Board Spec](2026-09-18_RRPG_WP-A_Battle-Dual-Board-Spatial-Authority-Migration_Development-Spec.md) | Executed | 查双 Board 迁移契约；不是待启动任务 |
| [WP-B Expedition Return Integrity Spec](2026-09-18_RRPG_WP-B_Expedition-Return-Integrity_Development-Spec.md) | Executed | 查 Return 异常恢复契约；跨进程恢复仍不在其范围 |
| [WP-C Battle Legacy Cleanup Spec](2026-09-18_RRPG_WP-C_Battle-Legacy-Product-Path-Cleanup_Development-Spec_FINAL.md) | Executed | 查旧产品路径退出范围；后续 Legacy 资产清理需另立任务 |
| [WP-D Battle Visual Foundation Spec](2026-09-18_RRPG_WP-D_Battle-Visual-Foundation-V0.1_Development-Spec.md) | Executed | 查表现宿主与验证边界；不冻结正式美术 |
| [WP-E Expedition Journey Visual Foundation Spec](2026-09-18_RRPG_WP-E_Expedition-Journey-Visual-Foundation-V0.1_Development-Spec.md) | Executed | 查 Journey 表现实现契约；正文 Pre-Execution 口径属于发布时截面 |
| [Static Configuration Migration FDS](2026-09-22_RRPG_Static-Configuration-Migration-FDS-V0.1.md) | M0–M4 首波已执行；后续领域未自动授权 | Monster / Enemy Presentation 实施契约与迁移边界；Current 操作看 Development/Config README |

## Closeout Report / Audit Evidence

| 文件 | 当前状态 / 用途 |
|---|---|
| [WP-AL-3 Autonomous Selection Closeout](2026-10-04_RRPG_WP-AL-3_Autonomous-Selection_Closeout.md) | Authority 1bbb67f、Runtime e2dd368、Repair 0a87394；模块 / API / Save / Daily / Headless / Trace / Prototype及96+228+1277+93最终证据；Runtime / Integrity Repair / Closeout Review PASS，已发布main；正文保留生成时Awaiting Publication语境，不改写历史；Calibration Not Frozen |
| [WP-AL-1 Social Foundation Closeout](2026-10-04_RRPG_WP-AL-1_Social-Foundation_Closeout.md) | 19 源码 / asmdef、21 meta、API / Schema / 48+1112+93 与 Semantic Review 历史证据；正文未提交状态是生成时快照，最终已发布 main 0c8722e，不改写历史 |
| [WP-AL-2 Activity Lifecycle Closeout](2026-10-04_RRPG_WP-AL-2_Activity-Lifecycle-Daily-Integration_Closeout.md) | 0c8722e 基线、70754649 Runtime、53 文件 / 27 meta、API / Schema / Gate A–E、69+1181+最终93、首次 PlayMode 92/93 与复跑、GitHub Review PASS、Current / Target 与后续接入边界；已发布 main，正文保留 Review Branch 阶段 / 发布前生成语境，不改写历史 |

以下三份是此前本地工程优化任务的材料；其累计机器回归与当时的 commit / push 授权只适用于该历史任务，不能沿用到 WP-AL-1。实际发布结果查 Git，任务证据不覆盖两份既有 Current Architecture。

| 文件 | 用途 |
|---|---|
| [架构优化总结与验收清单](2026-10-04_RRPG_架构优化总结与验收清单.md) | 制作人 / 开发15.0统一交付入口：优化结果、38份发布文件（含仓库信息治理与最终Handoff）、清理、验收、History Authority Mapping、并行边界与后续规划 |
| [工程现状与引用审计](2026-10-02_RRPG_工程现状与引用审计.md) | 分批实施、引用及测试证据；附录保留审计方法和完整初始Scene对象快照 |
| [引擎场景层级与配置查找指南](../引擎场景层级与配置查找指南.md) | 日常操作：层级、UI宿主、Inspector、Prefab与配置位置 |

工程整理的历史 Handoff / 38 文件清单通过 Git History 追溯，实际 Closure Commit / Push 结果查 Git；[开发验收交接](../开发验收交接.md)只保留最新已验收任务交接，不再承担旧整理的清单。独立规划、快照、并行说明和 History 调查已分别并入上述报告 / 审计，不再作为独立文件维护。

| 文件 | 类型 | 当前读法 |
|---|---|---|
| [M2 Pre-Implementation Audit](2026-09-22_RRPG_M2-Monster-Definition-Cutover-Pre-Implementation-Audit-V0.1.md) | Historical Audit Record | M2 实施前事实截面；“M2 尚未开始”不是 Current |
| [M2 Monster Authority Closeout](2026-09-23_RRPG_M2-Monster-Definition-Authority-Cutover-Closeout-Report-V0.1.md) | Closeout Report | M2 验证与 Authority Cutover 证据；M4 后 Legacy 已进一步退休 |
| [M3 Enemy Presentation Closeout](2026-09-23_RRPG_M3-Enemy-Presentation-Authority-Cutover-Closeout-Report-V0.1.md) | Closeout Report | 以 §11.8 读取最终分支 Push 事实；前文 Ready / Push Pending 是历史截面 |
| [M4 Monster Legacy Retirement Closeout](2026-09-23_RRPG_M4-Monster-Legacy-Physical-Retirement-Closeout-Report-V0.1.md) | Closeout Report | 以 §11.8 读取最终分支 Push 事实；按当前语义是迁移分支 Branch Closed 证据，不代表已合入 main |
| [WP-Encounter-1 Encounter Generation Closeout](2026-09-25_RRPG_WP-Encounter-1-Enemy-Encounter-Generation-Foundation-Closeout-Report-V0.1.md) | Closeout Report | WP-Encounter-1（1A → 1C-R）的范围 / 产品决定 / Temporary 边界 / 验证证据 / 文件分类；产品方向与 Freeze 边界以 [决策记录](../决策记录.md) D-020 与 [GameDesign/当前设计状态](../../GameDesign/当前设计状态.md) 为准，本报告不是长期规则 Authority |

## Historical / SUPERSEDED

| 文件 | 状态 | 规则 |
|---|---|---|
| [美术批量导入与 Art Import Skill 规划](2026-09-20_RRPG_美术批量导入与Art-Import-Skill规划_V0.1.md) | **SUPERSEDED / DO NOT IMPLEMENT** | 只作历史规划追溯；不得据此恢复 CSV + Catalog 混合 Authority 或启动实现 |

## 判断规则

- Current Guide 只在其明确覆盖的领域内有效。
- DevelopmentSpec 是开发期 Binding，不是长期玩家规则或架构 Authority。
- Closeout Report 记录当时任务证据；其中的早期状态由同报告后续校正段解释。
- `SUPERSEDED / DO NOT IMPLEMENT` 不得作为实现输入。
- 找不到当前状态时先查项目状态快照、问题清单与 Git；不要在旧正文顶部继续叠加“后来又怎样”的补丁。

## 日内生活 A→B→C→D — Published Main / Current Implementation / Verified

[A Spec V0.2](2026-10-05_RRPG_Intraday-Time-Expedition-Arrival_Development-Spec-V0.2.md)：FDS Review PASS；[B～D Spec V0.1](2026-10-05_RRPG_Intraday-Work-Lifecycle-Onsite_Development-Spec-V0.1.md)：连续授权与可调 Prototype 默认。A～D 已于 2026-10-06 标准 fast-forward 发布 main，发布点 `93a9e8cd6e0409ce335753221276bf88fe35f53f`；当前证据、Save/API 边界、保护例外及未解决事项见 [实施记录](2026-10-05_RRPG_Intraday-AD_Execution-Record.md)。Compile 0、targeted 94/94、full EditMode 1456/1456、full PlayMode 99/99、真实 Town 8/8 PASS。用户已授权提交、推送与 main 发布；日期化规格及实施记录的发布前语境保留。开发16.0入口见[当前交接](../开发验收交接.md)；原 WP-AL-1～4 Published Main 事实与原 WP-AL-5 独立 Deferred 编号保留。
