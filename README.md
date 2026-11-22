# RRPG

单人长期共同体经营 + 远征式 RPG 卡牌构筑游戏。当前设计定位见 [产品定位与设计支柱](GameDesign/01%20产品定位与设计支柱.md)。

## 给第一次了解项目的人

- **公开范围与限制：** [公开快照范围与阅读说明](公开快照范围与阅读说明.md)。

这是一项由个人策划主导的 Unity 独立游戏研发实践。除了探索人物成长、村庄经营与远征卡牌玩法，我还持续尝试让 AI 参与游戏研发中的需求拆解、设计讨论、工程执行与验证；本人负责产品方向、重要设计取舍、任务组织和需要人工判断的体验验收，并非独自手写全部工程代码。

- **看游戏设计思路：** [游戏设计总览](GameDesign/README.md)、[核心循环与系统关系](GameDesign/02%20核心循环与系统关系.md)。
- **看具体设计方法：** [角色设计方法论](GameDesign/Identity/角色设计方法论.md)、[卡牌与 Trait 设计工具箱](GameDesign/Identity/卡牌与Trait设计工具箱.md)。
- **看 AI 协同与研发探索：** [AI 多 Agent 协作协议](Docs/AI多Agent开发协作协议.md)、[AI 开发执行规则](Docs/AI开发执行规则.md)。
- **看实现与校验：** [游戏规则](GameSpec/README.md)、[配置与生产流程](Development/Config/README.md)。

这是项目早期主线的**清理后文件快照**，不是完整商业游戏或可直接下载游玩的成品；不公开私有 Git 历史，也不代表所有第三方安装文件或本地工作现场都可以重新分发。以下继续保留原工程内部的阅读顺序和阶段交接记录，其阶段结论应按原日期理解。

## 当前 Unity 工程

`UnityProject/RRPG1.0game/`（团结引擎 1.9.1，工程版本标识 `2022.3.62t9`）

## 第一次阅读

开发17.0 V17-R2 Village视觉/空间技术原型已获制作人验收；**Technical Prototype PASS / Art Master NOT FROZEN**。A仍默认白模，B可逆；收尾、保护与发布状态见[V17-R2执行记录](Docs/DevelopmentSpecs/2026-10-09_RRPG_V17-R2_Execution-Record.md)。不冻结当前PNG、最终比例/布局/镜头/动画或美术量产规则，不自动进入后续工作包。

- 人工从本 README 继续读 [AI_CONTEXT.md](AI_CONTEXT.md)，再进入[文档地图：按“我要做什么”找入口](Docs/文档地图.md)
- [项目状态快照](Docs/项目状态快照.md) —— 严格 Current 摘要、分支边界与当前问题入口
- [设计导航](GameDesign/README.md)与[当前设计状态](GameDesign/当前设计状态.md) —— 设计意图、候选和后续讨论
- [GameSpec 规则入口](GameSpec/README.md) —— 已确认规则与验收标准

支持自动读取仓库指令的 AI 工具可先经 [AGENTS.md](AGENTS.md) 进入上述顺序；人工已经在 README 时无需回读 AGENTS。AGENTS 只是 Bootstrap Adapter，不是 Authority。

## 仓库目录中文导览

| 英文路径 | 去这里找什么 |
|---|---|
| [GameDesign/](GameDesign/README.md) | 设计意图、体验方向和候选；具体可执行规则仍查 `GameSpec/` |
| [GameSpec/](GameSpec/README.md) | 已确认的玩家规则与验收依据，先读该目录的 `README.md` |
| [Docs/](Docs/文档地图.md) | 文档地图、Current 状态、技术架构、开发协作规则与阶段证据 |
| [Development/](Development/README.md) | 内容来源、生成镜像及技术实现入口；从 `Development/README.md` 分辨新旧配置链 |
| [Tools/](Tools/RRPG.ConfigCompiler/README.md) | 配置构建与校验工具，操作先按 [Development/Config/README.md](Development/Config/README.md) |
| [UnityProject/](UnityProject/) | 游戏工程、产品代码与 Unity 资源 |
| [工程规范](Docs/工程规范.md) | 当前命名、注释、职责隔离、Unity 与配置生产原则；旧 Prompts 已退休，历史内容由 Git History 追溯 |

目录用途与“要做什么→去哪看／去哪改”见 [文档地图](Docs/文档地图.md)。正式共享 Current 以 `main` 为准；已关闭工作分支在完整合入后可以删除，历史通过 Git History / Archive Tag 按需追溯。

## 事实来源约定

Monster Definition 与 Enemy Presentation 的 M0–M4 已进入 `main`，Monster Legacy CSV / per-entry SO 已退休。当前制作链入口见 `Development/Config/README.md`，实施证据见 `Docs/DevelopmentSpecs/2026-09-23_RRPG_M4-Monster-Legacy-Physical-Retirement-Closeout-Report-V0.1.md` §11.8。其它领域仍须逐域判断；D-019 长期方向不代表全项目迁移完成。

GitHub 已推送分支是跨 AI 的「已提交共享事实」；`main` 也不代表本地未 Push 的实时状态。涉及开发现场时，以明确授权的本地工作区、代码和 Unity Editor 状态为准。

## 当前能力与开发16.0接手

WP-AL-1～4 与日内 A～D 已发布 main；日内实现发布点为 `93a9e8cd6e0409ce335753221276bf88fe35f53f`，发布状态同步点为 `3952a7cb0fe5191ff643e2a8669819a1b49a32d0`，实时 HEAD 以 Git 为准。当前支持分钟时间/实际返程、实际劳动与午餐、持续自主活动与关系反馈、最小现场查看/中断；正式村庄空间表现尚未完成。

V16-P/A/B/C1/C2/D全部验收并发布main。Village Home / Building接手从[当前设计状态](GameDesign/当前设计状态.md) → [GameSpec Authority 矩阵](GameSpec/README.md)（[Product Home](GameSpec/Village/Product-Home-World-GameSpec-V0.1.md) / [Building World](GameSpec/Village/Village-Building-World-GameSpec-V0.1.md)）→ [Architecture Current / Target](Docs/Architecture/技术架构总览.md#v16-product-world-target) → [V16 Development Spec](Docs/DevelopmentSpecs/2026-10-07_RRPG_Village-Product-Home-and-Building-Foundation_Development-Spec-V0.1.md)进入。采用理由见[2026-10-07 Checkpoint](Docs/DesignCheckpoints/2026-10-07_RRPG_Village-Visual-Interaction-and-Building_Architecture-Checkpoint-V0.1.md)。开发17.0的V17-R1动态Farm / Kitchen生产闭环已验收关闭并发布main（Closure dbea0b5三方核验），真实实现、清理后回归与发布事实见[V17-R1执行记录](Docs/DevelopmentSpecs/2026-10-08_RRPG_V17-R1_Execution-Record.md)和[Current Snapshot](Docs/项目状态快照.md)。只关闭R1，不冻结完整V17路线或进入后续工作包；正式经济平衡、美术与完整页面继续Deferred。

从[状态快照](Docs/项目状态快照.md)确认现状，从[GameSpec](GameSpec/README.md)进入规则，从[Current Architecture](Docs/Architecture/技术架构总览.md)进入实现职责，从[配置操作](Development/Config/README.md)进入人工源与构建链。日期化开发规格和执行记录仅保留开发契约与验收证据，不代替这些长期入口。

[全仓审计清单与结果](Docs/2026-10-06_全仓文档一致性审计.md)记录逐份身份、审查与剩余冲突。[开发16.0交接](Docs/开发验收交接.md)记录V16-A真实交付、后续村庄视觉与交互方向、保护文件及 TMP 例外，D 最小现场面板不等于原 WP-AL-5 完整表现。

2026-10-04 工程整理的38文件交付与累计验证作为历史证据见[架构优化总结](Docs/DevelopmentSpecs/2026-10-04_RRPG_架构优化总结与验收清单.md)及 Git，不再由最近一次交接承担旧清单。
