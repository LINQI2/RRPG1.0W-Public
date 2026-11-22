# RRPG 多 Agent 开发协作协议 V1.6

> 日期：2026-09-24
> 补丁（2026-09-06 Governance Patch）：Cline 自主权边界 + Blocked / Awaiting Decision 定义分离
> 状态：Current / Active（Governance V2 正式协调层）
> 正式路径：`Docs/AI多Agent开发协作协议.md`
> 定位：RRPG 项目的**协调层 Harness / 多 Agent 编排协议 / 新对话恢复入口**。
> 目标：即使原协调对话消失，一个全新的协调 AI 也能可靠回答：
>
> **我们在哪？当前看到的是本地事实还是远端事实？为什么现在要做这一步？下一步谁执行？出现失败时如何合法阻塞、修复或重开？**
>
> 本协议维护 Bootstrap、任务划分、文件所有权、并行保护、冲突、Agent 交接、对话迁移及 Blocked / Awaiting Decision。完整开发与 Closeout 的详细 Authority 为 [AI开发执行规则](AI开发执行规则.md)，硬边界以 `.clinerules.md` 为准。
> Cline 技术执行细节、Unity 操作、Git GUI 细节和 Prompt 模板分别由其它治理/操作文档承担，避免本协议无限膨胀。

---

# 1. 第一原则

RRPG 是多 Agent 协作项目。

> **流程正确与代码正确同等重要。**

协调 AI 必须先恢复流程现场，再恢复具体任务。

同时：

> **Remote Git Fact 不得冒充 Local Workspace Fact。**

GitHub 只能证明已 Commit / Push 的共享事实；本地未提交 Handoff、Scene、代码、SourceTree 状态、Local HEAD 和 Unity Editor 实时状态，必须通过本地证据取得。

---

# 2. Bootstrap｜两种启动模式

## Mode A｜Closed Shared-State Bootstrap

适用：
- 目标分支已经 **Branch Closed**，或相关变化已经 **Repository Closed**；
- 无需恢复进行中的本地开发现场。

仅有 **Work Package Closed** 不足以进入 Mode A：它只证明当前任务已 Push 到 Target Branch，不证明该分支没有其它未发布工作。若仍有需要恢复的本地或分支现场，继续使用 Mode B。

### Level A 必读
1. `AI_CONTEXT.md`
2. `Docs/AI多Agent开发协作协议.md`
3. `Docs/文档地图.md`
4. `Docs/项目状态快照.md`
5. `Docs/问题清单.md`
6. `Docs/开发验收交接.md` 最新 Handoff
7. Target Branch 的远端事实；若核对正式共享仓库状态，再读 `origin/main` / GitHub HEAD

### 必须复述
- 当前项目；
- 最近完整关闭的 Work Package 及其 Target Branch；
- Target Branch 的本地 / 远端事实；需要时补充 origin/main；
- Current 实现/设计摘要；
- 开放问题与未冻结项；
- 下一 Work Package / 下一动作；
- 当前禁止事项。

---

## Mode B｜Mid-stage / Local-Dirty Bootstrap

以下任一成立即进入 Mode B：
- 开发中；
- Technical Checkpoint 之间；
- 待人工验收；
- Fix In Progress；
- Ready for Commit / Push；
- Closeout Complete / Push Pending；
- 当前 Work Package 有未提交文件；
- 当前 Target Branch 达到 Branch Closed 前发生协调对话迁移，且仍有需要恢复的本地 / 分支现场。

### 必须额外取得 Local Workspace Evidence

至少一种：
1. RRPG 对话迁移卡；
2. Cline / 本地 Agent 实时只读 Git 报告；
3. SourceTree 实时截图；
4. 具有本地权限的 Agent 回读；
5. 用户明确提供的本地事实。

### Local Evidence 冲突时的优先原则

> **更直接、更实时、更接近原始事实的证据优先。**

默认参考顺序：

1. 本地 Git 实时读取；
2. SourceTree 当前截图；
3. 当前本地 Agent 回读；
4. Migration Card；
5. 用户历史口述 / 旧聊天摘要。

该排序用于“事实新鲜度与直接性”，**不覆盖用户的最终设计裁决权**。

### Mode B 硬规则

未取得足够本地证据前：
- 不得声称已掌握当前开发现场；
- 不得把 Remote origin/main 当 Local HEAD；
- 不得把 GitHub 旧 Handoff 当本地最新 Handoff；
- 不得启动下一独立 Repository Work Package；
- 可以讨论设计，但必须标明“本地开发现场尚未恢复”。

> **当前 Target Branch 达到 Branch Closed 前发生协调对话迁移，且仍有需要恢复的本地 / 分支现场时，Migration Card 为强制交接机制。Work Package Closed 本身不解除该义务。**

---

# 3. Local / Remote Git Fact 分层

### Local HEAD
用户本地仓库当前提交指针。

### Local Worktree
本地 Modified / Untracked / Staged 文件。

### Remote Target Branch
当前 Work Package 声明的远端交付分支；证明已 Push 的任务事实，但不自动证明分支无待发布工作或已经进入 `main`。

### Remote origin/main
正式共享 main 已 Push 的仓库事实。

### GitHub Current
远端仓库当前可见的 Commit / CI / 文件事实。

> **Remote origin/main = abc123，不意味着 Local HEAD = abc123。**

---

# 4. 团队职责

## 用户｜最终制作人
负责：
- 最终玩法、视觉、版本范围、重大结构裁决；
- 必要真实产品人工验收；
- 固定授权口令；
- 未冻结规则/数值最终决定；
- 决定任务级 Git 写入授权；未授权 Agent 时，通过 SourceTree 或其他明确方式完成 Git 写入。

用户不负责：
- 背固定口令；
- 记下一 Agent；
- 自己拼多 Agent 流程；
- 自己判断每次 SourceTree 文件分组。

---

## ChatGPT / 开发协调对话｜制作人助手 + Harness 编排者
负责：
- 识别 Bootstrap Mode；
- 判断 Workflow State；
- 设计讨论、市场证据、系统审查；
- 定义 Repository Work Package 和 Technical Checkpoints；
- 生成 Cline / 状态管理员 / Codex Prompt；
- 区分机器验证与必要人工验收；
- 阶段转换时输出 `NEXT ACTION`；
- 协助执行 Agent 核对文件所有权与提交边界；
- 给用户 SourceTree 暂存/排除清单和 Commit Message；
- 用户 Push 后通过 GitHub 核验；
- 识别任务依赖与并行所有权冲突，不自动启动未授权任务。

不负责：
- 默认执行 Git 写操作；
- 用 GitHub 猜本地现场；
- 把实现事实升级为设计。

---

## Cline + Unity MCP｜开发执行
负责：
- 自主完成 Existing Host Map；可复用 / 扩展路径清楚时不提交人工审批；
- 当前 Work Package 范围内开发；
- 当前 Work Package、已确认规格和既有权限内，自主处理 Task 内实现、Technical Checkpoint、Fix 与回归（对应 `Docs/AI开发执行规则.md` 3.4 Green / Yellow 档放行）；
- Module / Product Integration Verification；
- 默认由同一开发 Agent 完成 Acceptance 后的 Final Closeout，包括任务范围内状态同步和 Handoff；Git 写入按任务级授权执行；
- **Handoff-only Correction（仅当交接事实本身错误、产品实现无需变化）。**

不负责 / 不应：
- 把普通技术实现选择、可修复编译 / 测试错误或 Existing Host Map 本身升级成人工决策 Gate（只有 `Docs/AI开发执行规则.md` 3.4 Red 档才升级协调层）；
- 擅自设计；
- 擅自跨 Work Package；
- 冻结 Test / Not Frozen；
- 超出授权范围的状态或规则修改；
- 未经任务级授权 Commit / Push。

---

## RRPG 项目状态管理员｜周期性治理

主要负责周期性项目状态审计、Roadmap 一致性检查、Architecture / GameSpec / Current State 交叉核对、大规模文档纠偏与仓库治理。普通 WP、Stage、小功能的 Final Closeout 默认由当前开发 Agent 完成，独立状态管理员不是必经节点。

治理任务先只读核实证据，写入限定在用户授权文件；不重做设计、不改产品实现、不擅自修正其他 Owner 的 Handoff、不冻结测试值，不因角色身份获得 Git 写权限。

---

## 游戏系统策划专家
负责：
- 顶层系统设计；
- GameDesign / GameSpec 审查；
- 规则冲突与未冻结项；
- 用户裁决问题。

---

## Codex
用于：
- 大型代码/架构审计；
- 文档治理；
- 跨文件安全重构；
- Git / 架构事实复核。

默认无 Git 写权限。

---

# 5. Repository Work Package / Technical Checkpoint / Closure Gate

## Technical Checkpoint
同一个 Work Package 内的机器验证节点。

满足：
- 同一规格/同一用户体验闭环；
- 只是技术拆分；
- 机器可稳定判断；
- 不需要新的用户设计裁决。

可：

```text
V2A → Technical Verified
↓
V2B → Technical Verified
↓
V2C
```

不要求：
- 固定验收口令；
- Handoff；
- 状态同步；
- 独立 Commit。

---

## 风险驱动的可选 Gate

是否使用 Gate、人工检查边界与 Gate 内部无人值守序列统一见 [AI开发执行规则](AI开发执行规则.md) §4.1。普通任务不因准备关闭而强制进入 Closure Gate。

## Repository Work Package

具有明确 Scope、Owner、文件边界和验收条件的交付单位。普通任务由同一 Agent 完成 Final Closeout；提交拆分只按执行手册 §13.3 的明确理由决定。



### 倾向拆成新 Work Package 的条件

满足任一项时，应优先考虑新 Work Package：

- 有独立风险检查或用户产品验收边界；
- 形成可单独交付/回滚的产品能力；
- 进入新的正式系统职责域；
- 有明显独立回滚价值；
- 会改变正式规格或重要系统边界；
- 失败时希望与上一能力隔离。

### 更适合作为 Technical Checkpoint

- 同一规格；
- 同一体验闭环；
- 仅内部技术拆分；
- 不产生独立用户交付价值。

未经授权不启动下一任务；已授权并行任务必须先声明文件所有权、依赖和提交边界。不能安全隔离时暂停重叠部分。

### 文件所有权与并行保护

- 开始时记录任务 Owner、Target Branch、基线 SHA、允许读写的精确文件白名单、现有 Modified / Untracked / Staged 路径及其归属；未归属文件不得据为本任务。写入前复核白名单与并行冲突。
- 同一文件不得由多个 Agent 并行写入。共享状态快照、日志、问题清单和 Handoff 也必须明确写入时段与单一 Owner；交接所有权后再继续写入。
- 其他 Agent 的任务外变化只记录路径或类别，不读取正文或差异、不评价、不覆盖、回退、暂存或清理；任务范围外变化必须保持隔离。
- 写入前发现他人修改，立即暂停重叠文件并协调所有权；保留双方工作，不自动选择一方覆盖。
- Agent 交接需明确已完成项、未完成项、实际验证、文件清单、Git授权范围和恢复位置；接收者复核现场后续接，不机械重跑。
- 多任务可安全隔离时不因dirty自动停止；全部仓库关闭条件仍由执行手册§13.7判断。

---

# 6. Workflow State Machine｜引用与协作状态

完整状态及转换唯一读取 [AI开发执行规则](AI开发执行规则.md) §13；本协议不维护第二套开发/关闭状态机。

协作时只记录当前执行阶段、Owner、Target Branch、证据和下一动作：正常推进由同一开发 Agent 承担；客观环境缺失进入 Blocked；新的范围/设计/安全裁决进入 Awaiting Decision；验收失败回到范围内 Fix。Ready for Commit / Push、Push Pending、Work Package Closed、Branch Closed、Repository Closed 严格沿用执行手册定义。

---

# 7. 异常状态

## 7.1 Blocked / Awaiting Decision

两个状态定义不同，禁止混用；Hard Stop清单与Manual Environment Intervention格式分别见执行手册§4.2、§6.3。

**Blocked = 缺少客观安全执行条件。**

适用：
- MCP / Editor 不可用并阻塞必要操作或验证；
- 缺少必要权限；
- 本地现场无法恢复；
- 脏改动无法安全隔离；
- Anti-Hang 已触发且没有其它证明路径。

**Awaiting Decision = 缺少新的范围 / 设计 / 高风险裁决。**

适用：
- 必须跨 Work Package；
- 规格 / 设计存在无法消解冲突；
- 必须冻结未决定规则；
- 必须改变未授权的高风险 Schema / SaveVersion / 公共接口；
- Existing Host Map 证明必须创建第二套正式产品路径。

> 普通 Task 内技术实现选择不得进入上述任一状态。

要求（两个状态共同）：
- 不伪装成开发中；
- 不猜规则；
- 输出阻塞证据、决策者、解除条件。

---

## 7.2 User Rejected / Fix In Progress

人工验收失败：
- 不生成最终 Handoff；
- 不进入状态同步；
- 只修真实失败项；
- 自动回归；
- 用户窄复验。

---

## 7.3 Reopened

Work Package Closed、Branch Closed 或 Repository Closed 后发现真实产品缺陷：

```text
发现真实缺陷
↓
协调 AI 分类影响
↓
用户明确同意重开
↓
Reopened / Fix In Progress
↓
Cline 只修该问题
↓
自动回归
↓
必要窄复验
↓
必要 Acceptance 确认后进入 Final Closeout
↓
新的 Handoff Version / ID
```

无需 Reopen：
- 纯 Git 提交遗漏；
- 纯文档错字；
- 历史 Handoff 后续 HEAD 变化；
- 不涉及实现变化的 Git 事实整理。

---

# 8. Acceptance 与状态同步授权

Acceptance、固定口令及 Git 授权分支唯一按执行手册 §13.3、§13.7执行。固定验收反馈不自动授予commit/push权限。

同一开发 Agent在Final Closeout中同步受影响且已授权的Current事实，不要求先Feature Commit或独立状态管理员审计。独立治理任务仍可使用“同步本次状态。”指定状态快照、项目日志、问题清单的一次性窄写授权；先核实证据，遵守明确白名单，不扩张到GameSpec、决策或实现。

---

# 9. Handoff Git Facts｜历史快照

Handoff 描述：

> **开发任务关闭时的事实。**

Git Facts 默认是：

> **Handoff 生成时的 Local Git / Worktree Snapshot。**

后来 Closure Commit 改变 HEAD，不使旧 Handoff 自动失效。

Current 提交事实由：
- Git / GitHub；
- 项目状态快照；
- 项目日志

承担。

只有：
- Handoff 生成时事实本身错误；
- 或 Schema 明确要求 Current 值

才修 Handoff。

---

# 10. Handoff Correction

执行 Agent 自检或独立审计发现 Handoff 问题时按以下事实类型分流；这不是每个任务必经的人工 Gate。

### A｜历史快照正确，只是后来 Git 变化
例如：
- Handoff 生成时确实“未提交”；
- 后来才产生 Feature SHA。

处理：

> **不修改 Handoff，继续状态同步。**

---

### B｜Handoff 当时事实写错，但产品实现没有变化

例如：
- 测试数写错；
- 修改文件表漏记；
- 当时 Local HEAD 记录错误；
- 验收范围陈述与证据不一致，但实际产品无需修改。

处理：

```text
Owner 自检 / 审计
↓
停止状态写入
↓
返回 Cline / Handoff Owner
↓
Handoff-only Correction
↓
回读 + diff
↓
Owner / 审计者重新核对
```

> **不进入 Reopened Development。**

---

### C｜Handoff错误暴露产品实现也有问题

例如：
- 声称已接入产品路径，但实际没有；
- 声称某 UI 固定宿主存在，实际不存在；
- 漏文件导致功能不完整。

处理：

> **进入 Reopened / Fix Package。**

状态管理员不得自行修 Handoff 或代码。

---

# 11. Git / 状态同步职责

完整顺序、Git授权和Push核验见 [AI开发执行规则](AI开发执行规则.md) §13。同一开发Agent负责本任务收尾，协调者只在跨Agent或所有权冲突时协助；不强制用户手动双提交。

---

# 12. Staging Boundary Check｜文件所有权检查

提交前按执行手册§13.5生成Cleanup后的精确清单；此处只维护跨Agent文件归属检查，不另设通用人工Gate。

执行 Agent 必须把当前 Worktree 文件分成三类：

## A｜本次必须暂存
确定属于本次 Commit。

## B｜本次必须排除
例如：
- 另一个任务的 Dirty Files；
- Unity 自动副作用；
- 无关 ProjectSettings；
- 临时 Scene；
- backup；
- 不属于当前 Commit 语义的 Docs / 代码。

## C｜无法判断，先停止
任何文件归属不明时：

> **不得让用户继续提交。**

先取得：
- git diff；
- SourceTree 截图；
- Cline / 本地 Agent 说明；
- 用户确认。

### 提交边界复核

核对本任务必要文件、删除和 .meta 无遗漏；Handoff、状态文档与产品文件可进入同一Closure Commit。并行任务变化必须排除；无法确认所有权时停止对应暂存/提交。

# 13. Commit Boundary｜按任务与所有权划分

普通任务默认原子Closure Commit；仅因用户要求、所有权冲突、并行隔离、独立回滚价值或明确治理理由拆分，详见执行手册§13.3。角色和文件类型本身不要求分成Feature / Docs两组。

---

# 14. Formal Document Impact Review

Work Package Closed 前，对 Handoff 标记的正式文档逐项给出：

- **已处理**
- **无需处理**
- **另立任务**
- **未授权**

基础范围：
- GameSpec
- GameDesign（如受影响）
- `Docs/决策记录.md`
- `Docs/项目日志.md` 与 Git History
- `Development/`

---

## Governance Document Impact Review

如果当前 Work Package 改变了：
- 开发流程；
- 工具链；
- Agent 权限；
- Git 策略；
- 验收方式；
- 文档治理；
- Harness / 工作流；

则额外检查：

- `.clinerules.md`
- `AI_CONTEXT.md`
- `Docs/AI开发执行规则.md`
- `Docs/AI多Agent开发协作协议.md`
- `Docs/文档地图.md`
- 必要时 `Docs/开发验收交接.md`

同样必须明确：

> 已处理 / 无需处理 / 另立任务 / 未授权

---

# 15. Local Residual Check

Work Package Closed 前必须确认：

> 当前 Work Package 不再残留“本应提交但未提交”的文件。

至少检查：
- 代码；
- Scene；
- Prefab；
- `.meta`；
- SO；
- 测试；
- Handoff；
- 状态文档。

存在其他任务变化时必须明确归属与共享工作区占用；本任务提交完成不等于 Work Package Closed，本分支关闭也不等于 Repository Closed。关闭条件统一见执行手册 §13.7。

优先证据：
- SourceTree；
- 本地 Agent `git status`；
- 用户确认。

GitHub 无法替代本项。

---

# 16. 自动验证与人工验收

机器负责：
- 公式；
- Tick；
- Pause / Speed；
- Save round-trip；
- Product Save Path；
- Product Integration Verification 与真实产品路径接入；
- 状态互斥；
- Runtime/HUD 解耦；
- 序列化；
- 回归。

用户负责：
- UI 是否看得见、点得到；
- Edit Mode / Prefab Mode 可调性；
- 产品路径的实际游玩体验是否真实可用；
- 模态/输入体验；
- 布局、视觉、语义；
- 自动测试绿但实际体验异常。

> **机器能稳定验证的，不转嫁给用户。**

---

# 17. NEXT ACTION 固定输出卡

协调 AI 在流程转换节点必须输出：

## NEXT ACTION

**当前模式：**

`Closed Shared-State Bootstrap / Mid-stage Local-Dirty`

**当前节点：**

`<Workflow State>`

**节点依据：**

`<例如：Handoff已落盘 + Ready for Commit / Push>`

**下一执行者：**

`<用户 SourceTree / Cline / 状态管理员 / Codex>`

**你现在要做：**

`<一个明确动作>`

**发送内容 / Commit Message：**
> `<固定口令 / Prompt / Commit Message>`

**完成标志：**

`<怎样算完成>`

**完成后带回来：**

`<报告 / SHA / SourceTree 截图 / 状态管理员结果>`

**现在禁止：**
- `<禁止事项 1>`
- `<禁止事项 2>`

---

# 18. Emergency Conversation Migration Card

Target Branch 达到 Branch Closed 前迁移协调对话，且仍有需要恢复的本地 / 分支现场时强制生成。仅达到 Work Package Closed 不自动免除。

```text
【RRPG 对话迁移卡】

项目：
Repository Work Package：
内部 Technical Checkpoint：
流程节点：
Target Branch：

Bootstrap Mode：
Mid-stage / Closed Shared-State

Local Git（若有证据）：
branch:
Local HEAD:
Local Worktree:
staged:

Remote Git：
origin/main:
GitHub HEAD:

最新 Handoff：
Version:
ID:
本地 / 远端可见性：

已完成：
未完成：

Current 决策：
1.
2.

未冻结 / 开放问题：
1.
2.

关键开发规则：
1.
2.

下一执行者：
下一固定口令 / Prompt / SourceTree 动作：

当前禁止：
1.
2.

Level A 必读：
AI_CONTEXT
AI多Agent开发协作协议
文档地图
项目状态快照
问题清单
最新 Handoff
Remote origin/main

额外 Local Evidence：
<Migration Card / Cline / SourceTree / 本地 Agent>
```

---

# 19. 按职责域确定权威源

| 事实域 | 权威来源 |
|---|---|
| Cline 执行权限 / 硬约束 | `.clinerules.md` |
| Agent 快速身份 / 启动入口 | `AI_CONTEXT.md` |
| 完整开发 / Verification / Acceptance / Gate / Final Closeout | `Docs/AI开发执行规则.md` |
| 多 Agent 编排 / 对话迁移 / Work Package | `Docs/AI多Agent开发协作协议.md` |
| 当前正式玩法规则 | Current `GameSpec/` |
| 设计意图 | Current `GameDesign/` |
| 本次开发实际交接 | 最新正式 Handoff |
| 项目当前整体状态 | `Docs/项目状态快照.md` |
| 未解决问题 | `Docs/问题清单.md` |
| Local 工作区事实 | Local Git / Editor / SourceTree / 本地 Agent |
| Remote 版本事实 | GitHub / origin/main |

> 一个职责域不得越权覆盖另一个职责域。

---

# 20. 关闭状态核验入口

关闭条件以根硬规则和 [AI开发执行规则](AI开发执行规则.md) §13.7 为准。协调者复用执行 Agent 提供的真实 Git 证据，不能凭 Handoff 落盘或 Acceptance Passed 宣布关闭。feature / migration 分支可以 Work Package Closed / Branch Closed；只有变化进入正式共享 `main` 并完成 main / remote / workspace 核验后才是 Repository Closed。历史 `Development Closed` 与旧分支 `Repository Closed` 记录不批量改写，按其当时上下文解释。

---

# 21. 治理入口一致性

文档地图登记各Authority分工；根规则链接执行手册与协作协议；Handoff只维护固定格式与最近真实交接。治理更新检查这些现有入口，不新增重复Workflow文档。历史Feature/Docs Commit、Gate、状态管理员事件保持原样。

---

# 22. 主协议维护边界

后续不要无限扩写本文件。

本文只长期维护：

- Bootstrap与本地/远端证据；
- 任务划分、文件所有权、并行保护与冲突；
- Agent交接、NEXT ACTION与对话迁移；
- Blocked / Awaiting Decision。

详细开发、Gate、Closeout及Git关闭条件由执行手册维护。

以下内容不继续堆入正文：
- SourceTree 图形界面逐步教程；
- Unity 具体 Bug 案例；
- 每个 Agent 完整 Prompt；
- 具体业务系统实现规则。

这些进入独立操作指南 / 技术规则 / 模板库。

---

# 23. 协作摘要

恢复本地现场 → 确认Scope与Owner → 隔离并行文件 → 按执行手册推进 → 必要时明确交接与恢复点。

Handoff Git Facts是生成时历史快照。独立状态管理员用于周期性治理；普通任务由同一Agent完成收尾，Git写入必须有任务级明确授权。
