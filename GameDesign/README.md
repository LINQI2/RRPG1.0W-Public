# GameDesign 使用说明（导航入口）

> 最后审查：2026-09-26
> 本文件是 Design Conversation Bootstrap、统一 Freeze Taxonomy 与 `GameDesign/` 导航入口，不维护实现状态矩阵。
> V0.2 设计收口状态、Legacy 降级与后续设计入口见 `GameDesign/当前设计状态.md`。
> 本目录按产品总览、Current 设计领域、横切原则与设计状态组织；文件顺序不代表实现阶段或规则冻结等级。
>
> **Battle 切版：** Battle 的可执行规则以 `GameSpec/Battle/` 为准；本目录的领域设计不保存与之竞争的旧 Battle Current 规则。旧永久死亡、个人行动力+TP、角色卡上阵凭证、旧道具牌堆及旧 Slot 容器、旧距离／阻挡公式、满 HP 入场等只作 Legacy 追溯。Current 空间规则服从 [Battle GameSpec](../GameSpec/Battle/Battle-GameSpec-V0.1.md) R5.0 / R6；新旧同为 3×3 不代表旧规则整体恢复。

## 一、GameDesign 的职责

- 记录设计意图、体验目标、叙事理念和可继续演进的设计方向，主要回答「为什么这样设计」。
- 保留旧有故事理念、世界观方向和叙事核心；游戏设计持续演进，但所有 Current 设计必须使用本文件定义的最小 Freeze Taxonomy 表达其权威边界。
- **不负责记录代码完成状态**：当前实现与验证状态由 `Docs/项目状态快照.md`、Git 与代码承担。
- **不把当前实现自动视为永久设计决定**：「当前实现」只说明游戏现在做到了什么，不代表该设计已永久确定；不得因代码当前这样实现，就擅自把它升级为永久游戏规则。

## 二、Design Conversation Bootstrap

本入口服务没有聊天历史的新系统策划 / 制作人助手，不复制 `AI_CONTEXT.md`，也不维护第二份实现状态。最小阅读顺序：

1. **本文件**：先理解 Authority、Freeze Taxonomy 与提升规则。
2. **`01 产品定位与设计支柱.md`**：恢复 RRPG 是什么、核心幻想、八条 Design Pillars、三层职责与非目标。
3. **`当前设计状态.md`**：读取 Current Product Architecture、Current Freeze Ledger、Legacy 禁止恢复项与当前 Design Frontier。
4. **`Docs/决策记录.md`**：读取跨系统长期决定及其原方案、理由、保留基因和重审条件。
5. **当前专题对应的领域设计文件**：理解该领域“为什么这样设计”。
6. **只有当专题涉及可执行规则或验收时，才读取对应 `GameSpec/`**；GameSpec 是规则 Authority，不从 Checkpoint 直接跳到开发。
7. **只读取 `当前设计状态.md` 或专题文档明确指向的 Checkpoint**：Checkpoint 用于恢复讨论位置、候选与历史理由，默认不是 Current。

完成 Bootstrap 后，新 Agent 至少应能回答：

- RRPG 的产品身份、Design DNA 与 Battle / Person / Village 分工；
- 哪些是 Confirmed Architecture，哪些只是 Candidate 或 Concept；
- 当前 Design Frontier 与下一未决问题；
- 哪些旧规则已经 Rejected / Replaced 或只属 Legacy，不得恢复；
- 当前专题的规则 Authority 和相关 Checkpoint 分别在哪里。

若上述问题无法由仓库证据回答，应报告 Documentation Gap，不得用代码实现或常识补全正式设计。

## 三、统一 Freeze Taxonomy

以下七种状态是 RRPG Design 当前唯一规范状态：

| Freeze Level | 含义 | 能否指导开发 | 能否进入 GameSpec | 调整边界 | 新 Agent 解释方式 |
|---|---|---|---|---|---|
| **Confirmed Architecture** | 用户已采用的产品身份、跨系统职责、长期原则或明确边界；确认的是“必须满足什么”，不等于具体机制、数值或数据结构已冻结 | 可约束范围、职责与 Anti-Gate；若缺少可执行规则，不得自行补全实现 | 当它产生可执行规则或验收边界时可以同步进入 | 仍可通过新的明确设计决定调整，但不能被候选、Checkpoint 或实现静默覆盖 | 视为 Current 产品架构；继续区分其中未冻结的承载方式 |
| **Current Rule / Active GameSpec** | 已采用并写入 Active GameSpec 的可执行规则、边界或验收依据 | 可以，但只在已授权 Work Package 内按 GameSpec 开发 | 已经位于 GameSpec；变化必须先有设计决定并同步规格 | 不可由 Agent、代码现状或测试值自行改变 | 视为当前开发规则 Authority，不等于实现已经完成 |
| **Supporting Candidate** | 为已确认问题提供的当前最强工作模型或承载方式，但仍可能换名、换结构或被替代 | 不可作为正式 Requirement；只有明确授权的 Test / Hook 范围可以试验，并必须标记候选 | 不得以正式规则身份进入；只能以 Candidate / Test 边界被引用 | 可根据后续设计和证据调整 | 视为“优先继续研究的方案”，不是已冻结答案 |
| **Concept / Not Fully Frozen** | 已形成可讨论骨架或接近阶段性收口，但关键结构、规则或内容仍未决定 | 不可授权 Runtime 开发 | 不得作为正式规则进入；用户采用其中明确结论后，先归位 Current Design | 可以自由调整；`Closure`、文件较新或内容详细都不提升等级 | 视为讨论前沿和历史证据，不视为 Current Requirement |
| **Deferred** | 问题有效且未来可能必须解决，但当前阶段主动后置 | 不可 | 不可 | 到文档写明的触发条件或阶段后重新审视 | 视为“不要现在设计”，不是拒绝其问题价值 |
| **Rejected / Replaced** | 某个方案、规则或承载方式已被明确否决或由新方案替代 | 不可 | 不可恢复；若历史索引仍在 GameSpec，必须显式标 Legacy / Replaced | 只有新的明确设计决定才能重新提出 | 读取其失败原因与保留基因，不执行原方案 |
| **Legacy / Historical** | 已失去 Current 职责但仍有历史、迁移或设计基因价值的材料 | 不可作为 Current 指令 | 不可作为 Current Rule | 可用于追溯和提取历史基因，不能因修改日期新而复活 | 默认不读；专题追溯时按需读取 |

统一解释规则：

- `Not Frozen` / `Test / Not Frozen` 是安全修饰语，表示具体值或结构不得升级，不另建第八种状态。
- `Concept Near Closure`、`Concept Closure-ish`、`Strong Candidate`、`Strong Product Hypothesis` 是置信度或描述性标签，不是独立 Freeze Level；未被用户采用并归位 Current 前，一律按 `Concept / Not Fully Frozen` 处理。若只是某个承载方式，按 `Supporting Candidate` 处理。
- `Checkpoint Closure` 只表示该轮讨论可以停止或问题骨架已清楚，不等于 `Confirmed Architecture`。
- 同一主题可以同时包含不同层级，例如“稳定身份必须影响 Battle”是 Confirmed Architecture，而 `Profession` 仍是 Supporting Candidate。
- 当前项目实例化状态只维护在 `GameDesign/当前设计状态.md` 的 Freeze Ledger；本文件不复制该 Ledger。

## 四、与 GameSpec / 状态快照 / 决策记录的关系

| 层 | 文件 | 职责 |
|---|---|---|
| 设计意图与体验方向 | `GameDesign/` | 「为什么这样设计」、体验目标、叙事理念、可演进方向 |
| Current Product Architecture / Freeze Ledger / Design Frontier | `GameDesign/当前设计状态.md` | 当前采用的设计、各主题 Freeze Level、Legacy 边界与下一设计问题 |
| 日期化设计证据 | `Docs/DesignCheckpoints/` | 讨论位置、候选、阶段性 Closure 与历史理由；默认不是 Current Authority |
| 当前可执行规则与验收依据 | `GameSpec/` | 已确认玩法规则、系统验收标准；与 GameDesign 冲突时先登记，不自行选边 |
| 当前实现与验证状态 | `Docs/项目状态快照.md` | 现状事实层，只记录当前实现与验证证据 |
| 明确形成的长期决定 | `Docs/决策记录.md` | 用户确认且仍有效的跨系统决定 |
| 当前实现事实 | 代码与 Unity Editor | 「目前实际做到什么」，不自动代表规则 |

设计与规则依据、实现事实依据是两条独立轨道：实现比规则新不代表规则已改变；旧设计与当前实现不同，也不能否认实际代码现状。差异先分类为：文档滞后、实现偏离、规则冲突、待技术核对。

## 五、Current 导航

| 层次 | 入口 | 职责 |
|---|---|---|
| Top-level | [产品定位与设计支柱](01%20产品定位与设计支柱.md)、[核心循环与系统关系](02%20核心循环与系统关系.md) | 产品身份、体验支柱、系统之间的职责 |
| Current Design Domains | [Battle](03%20Battle.md)、[Person](04%20Person.md)、[Cards](05%20Cards.md)、[Expedition](06%20Expedition.md)、[Village](07%20Village.md) | 各领域设计意图；可执行玩家规则仍以对应 Current GameSpec 为准 |
| Cross-domain principle | [数值设计原则](数值设计原则.md) | 数值的设计职责、追溯与未冻结边界，不复制正式配置值 |
| Cross-domain principle | [Art Direction / 美术与视觉设计](ArtDirection/README.md) | RRPG 顶层视觉方向、参考游戏对照、视觉语言研究与后续 Visual Bible 入口；当前整体为 **Concept / Not Fully Frozen**，不承担 GameSpec / Runtime Rule Authority |
| Design State / Frontier | [当前设计状态](当前设计状态.md)、[DesignCheckpoints 状态导航](../Docs/DesignCheckpoints/README.md) | 前者是 Current Freeze Ledger；后者用于定位讨论证据，Checkpoint 默认不是 Current |
| Character Content | [Identity 专题](Identity/README.md) | 未冻结的角色内容设计候选，不建立第二套 Person / Profession Authority |

Pre-8/30 的 Status、Meta 与 Person Risk 旧专题已退出活动仓库；仍有效的设计原则分别归位 Battle、Person 和当前设计状态。历史原文可通过 Git History 追溯，不得据此恢复旧规则。

> Identity 专题中的五个角色方向、36 张卡牌候选、4 个 Trait 候选、6 个 Build Seed 与构筑接口均为 **Concept / Not Fully Frozen** 的设计候选，不是 Current Rule。该专题不新增 Identity Runtime、Person Identity Authority 或第二套 Profession 规则。`Identity/Identity角色内容设计工作台.xlsx` 是结构化候选内容的主编辑源，不是 Runtime 配置；专题 Markdown 不维护第二套参数源。“可进入 Prototype”只表示通过纸面设计后可以进入下一项设计验证 Gate；Prototype 的开发、实现或 Work Package 仍需独立授权。

## 六、当前设计状态入口

- V0.2 设计归位、Legacy / Historical Candidate、未冻结内容与下一入口见 **`GameDesign/当前设计状态.md`**。
- 当前实现与验证状态由 `Docs/项目状态快照.md`、`Docs/问题清单.md` 和 Git 维护，本目录不维护第二份实现快照。

## 七、Checkpoint → Current → GameSpec 生命周期

正式生命周期及 Authority 以 `Docs/文档地图.md` 为准，最小链为：

> Design Discussion / Checkpoint → 用户采用 → Current Design → 若影响可执行规则则同步 GameSpec → Development / Validation

- 不得从 Checkpoint 直接进入开发；
- Current Design 可以领先实现，但不得伪造实现已完成；
- 实现不能反推或提升设计；
- 已被 Current 吸收的 Checkpoint 继续保留历史价值，不永久承担 Current 职责；
- GameDesign 与 GameSpec 冲突时先报告文档滞后或规则冲突，不自行选边改写。

## 八、过时内容处理规则

- 明确被新设计替代的内容，在原处标注「已被……替代」，并链接新依据。
- 有历史价值但已经失去 Current 职责的纯旧文档，在确认有效内容已归位且 Current 文件不再依赖后，可移出活动仓库；Git History 是仓库内历史追溯的默认来源。制作人如另有需要，可额外进行仓库外个人归档，不要求每次退休文件都复制一份。
- 无法确认是否废弃的内容不得删除；当前无法裁决时，进入 `GameDesign/当前设计状态.md` 的后续讨论入口。
- 不要让旧设计和当前设计同时以「当前有效」的口吻存在。
- 当前实现与设计不一致时，先记录差异与来源；不把实现自动反写为规则，也不因实现现状删除原设计。
