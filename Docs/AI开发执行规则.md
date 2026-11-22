# RRPG AI 开发执行规则

> 版本：V2.0
> 最后更新：2026-09-24
> 补丁（2026-09-06 Governance Patch）：Agent 自主权三档放行（3.4）+ Anti-Hang（9.4）+ Feature Commit Staging Manifest（13.5）
> 适用范围：Cline/其他代码 Agent + AI Game Developer / Unity MCP + 团结引擎 1.9.1
> 规则入口：项目根目录 `.clinerules.md`
> 阅读方式：按任务读取相关章节。本文是 Development、Verification、Acceptance、风险 Gate、Manual Environment Intervention、Final Closeout、测试清理及 Commit / Push 核验的唯一详细流程 Authority；最高硬规则见 `.clinerules.md`，并行文件所有权和 Agent 交接见 [AI多Agent开发协作协议](AI多Agent开发协作协议.md)。

---

## 1. 目标与职责边界

本项目普通 WP、Stage 或小功能默认由同一 Development Agent 完成：

```text
Audit → Development → Verification
→ Acceptance（仅需用户产品判断时请求用户确认）
→ 同一 Agent Final Closeout
  → Handoff
  → Current 状态文档同步
  → Disposable Test Cleanup
  → Final Diff / Workspace Review
  → Closure Commit（任务级明确授权）
  → Push（任务级明确授权）
→ Git 核验 → Work Package Closed
→ Target Branch 无待发布工作时 Branch Closed
→ 合入正式共享 main 并核验后 Repository Closed
```

任务开始时必须声明 Target Branch。无 Git 写入授权时停在 Ready for Commit / Push。阶段检查点在同一任务内自主推进，不要求每个 Stage 都生成 Handoff、独立提交或人工验收。独立状态管理员不是普通任务必经节点。

AI 应自动承担已经明确、可验证、可回滚的机械工作，不应把 MCP 能可靠完成的步骤默认转交用户。

用户保留最终决定权的事项：

- 游戏设计、玩法取舍和数值平衡；
- 主观视觉、动画节奏和体验验收；
- 重大架构、公共数据结构和存档 Schema 变更；
- 高影响删除、批量资源修改、包和 Project Settings 变更；
- 规则文件或规格之间无法由现有决策消解的冲突。

当前请求已经明确授权的操作，不重复索要同一项授权；但目标、范围或风险在执行中扩大时，必须重新说明并取得确认。

---

## 2. 文档权威与冲突处理

### 2.1 双轨信息模型

按与当前任务相关的内容判断，不要求每次读取全部文档。文档职责和新旧状态先看 `Docs/文档地图.md`：

**设计与规则依据：**
1. 用户当前明确要求；
2. 用户当前采用的设计方向及 `Docs/决策记录.md` 中仍有效的决定；
3. `GameSpec/` 中对应功能规格；
4. `GameDesign/` 中对应概念、体验和玩法意图。

**实现事实依据：**
1. 当前明确授权的本地 Git 工作区、代码、资源和 Unity Editor 实时状态；
2. 远端 Target Branch：当前任务已 Push 的共享事实；GitHub main：正式共享主线事实；二者都不代表本地未 Push 修改；
3. `Docs/项目状态快照.md`：快速当前事实摘要；
4. 开发验收交接、项目日志和 Git 历史：证据/追溯来源。

两轨不能互相自动覆盖。差异先分类为：文档滞后、实现偏离、规则冲突、待技术核对。禁止静默选择一份文件覆盖其他文件，禁止根据旧代码自行补全设计规则。

已核对的主题技术入口：`migration/static-config-m1` 上怪物定义与敌方图片的当前制作链看 `Development/Config/README.md`，敌方源图在 `Development/SourceAssets/Enemy/`；其它尚未迁移领域须逐域核对现有代码与数据，不把 2026-06-28 的历史 CSV/SO 手册当作全仓通用指令。技术实现目录先读 `Development/README.md`，Current Architecture 进入 [技术架构总览](Architecture/技术架构总览.md)和[核心对象与术语映射](Architecture/核心对象与术语映射.md)，只负责对象职责、数据所有权、生命周期、依赖与持久化边界，不替代 GameSpec 或实时状态。两份历史 Architecture 审计稿为 Audit Record，不作为当前开发 Authority。已迁出的历史架构设计仅在需要时通过 Git 历史或仓库外个人归档追溯。纯旧资料只有在有效内容已归位且 Current 文档不再依赖后，才移出活动仓库；不再要求放入仓库内归档目录。

`.clinerules.md` 和本文规定的是开发过程、权限与验证方法，不取代 GameSpec 的游戏设计内容。

文件更新时间只能作为判断线索，不能单独推翻更高层级文件。代码和 Editor 事实可以证明“当前实现是什么”，但不能自行把实现反推成新的游戏规则。

### 2.2 发现不一致时

先把不一致分类：

| 类型 | 处理方式 |
|------|----------|
| 已确认规格清楚，代码或 Editor 只是漏实现 | 在当前任务范围内修复并验证 |
| 状态快照过期，MCP 可读取实时状态 | 以实时状态诊断，在报告/交接中标记待同步；当前任务未明确包含文档同步时不直接修改 |
| 两份设计/规格互相矛盾 | 停止会固化该规则的修改，列出冲突让用户决定 |
| 涉及架构、公共数据结构或存档 Schema 变化 | 先分析影响；当前请求未明确授权时询问用户 |
| 仅有实现差异，但无法判断是 Bug 还是新规则 | 不猜测设计意图，报告证据并询问 |

不得用代码现状静默覆盖规格，也不得用过期文档否定已经验证的 Editor 实际状态。

---

## 3. 任务开始前

### 3.1 按需读取

- 所有开发任务：`.clinerules.md`、本文相关章节；
- 玩法功能：对应 `GameSpec/`，必要时再读 `GameDesign/`；
- 数据/架构：按需读 [技术架构总览](Architecture/技术架构总览.md)与[核心对象与术语映射](Architecture/核心对象与术语映射.md)，代码目录入口仍为 `Development/README.md`；Current Architecture 中的 Candidate / Not Frozen / Legacy 边界继续有效；
- 已迁移的怪物定义／敌方图片：`Development/Config/README.md` 与 `Development/SourceAssets/Enemy/`；其它 CSV/SO 领域先逐域核对当前代码和数据，历史制作方法仅供追溯；
- Scene/Inspector/UI：通过 MCP 读取实时状态，历史状态文件只作线索；
- 存档：先读 `GameSpec/README.md` 的 Save Authority 矩阵与 `GameSpec/Save/Save-GameSpec-V0.1.md`，再结合 Person、Expedition、Village 等领域 Current GameSpec；Pre-8/30 Save 旧规格已退休，Legacy schema / DTO 通过 Git History 与当前代码按需追溯，不能覆盖 Current Save GameSpec。

### 3.2 保护现场

修改前必须：

- 查看 Git 状态和相关差异；
- 识别并保留用户已有的未提交改动；
- 不覆盖、不回退、不顺带格式化无关文件；
- Unity 任务先记录 Console 基线；
- 涉及 Scene/Prefab 时，先读取当前活动 Scene、精确目标对象和组件。

工作区不干净不等于不能开发。只有本次修改会覆盖、混淆或无法隔离现有改动时才暂停询问。

### 3.3 Existing Host Map：已有系统迁移前置检查

在已有可运行模块上进行增量开发、重构或新规格迁移时，创建新的正式 Scene、Canvas、UI、Controller、Manager、Prefab 或产品入口之前，必须先完成简短的 Existing Host Map。它是实施前检查，不要求另建长期文档，可直接写在任务计划或 Stage 报告中。

至少记录：

| 新规格职责 | 当前已有宿主/真实路径 | 当前处理方式 | 迁移处理 | 理由与影响 |
|---|---|---|---|---|
| 功能 X | 类、对象、Prefab、Hierarchy 或入口链 | 现有数据与行为 | 复用 / 扩展 / 替换 / 新建 | 为什么这样处理、影响哪些上下游 |

处理优先级：

1. 直接复用现有宿主；
2. 扩展现有对象；
3. 替换旧实现，但保留真实产品宿主和上下游入口；
4. 已审计确认无法合理复用时才新建正式宿主。

规格或 Binding Spec 中的对象名首先表示功能身份或推荐名称，不构成重复创建命令。已有对象能承担该职责时，应建立“规格名 → 实际对象/路径”映射并复用，不得只为名称一致而再建一套对象。

若仍需新建承担相同正式职责的第二套宿主，实施前必须报告：现有宿主为何不能复用、新建的必要性、对现有产品路径的影响，以及旧宿主未来如何替换、停用或移除。缺少这些证据时不得新建。

Debug UI、Test Harness、Mock、临时 Canvas 和独立 Runner 可以平行存在，但必须明确使用 `Debug` / `Test` 等身份，默认不接入正常玩家流程，也不得作为 Feature/Stage 产品集成完成的证据。

### 3.4 Existing Host Map 三档放行规则

Existing Host Map 是实施前自检与任务证据，不是默认人工审批 Gate。按结果把方案分三档，决定是否请求人工确认：

**Green / Auto Proceed**（→ 自主继续，不等待人工确认）
- 方案与当前 Task / Work Package 一致；
- 同一职责域；
- 复用 / 扩展 / 替换现有宿主；
- 无新设计或高风险变化。

**Yellow / Proceed + Report**（→ 自主处理，最终报告说明）
- 同一职责域内少量必要额外文件；
- 局部兼容适配；
- 测试结构调整；
- 可修复的编译 / 绑定 / 局部实现问题。

**Red / Stop**（→ Awaiting Decision）
- 跨 Work Package；
- 固化未决设计；
- 当前任务未授权的高风险 Schema / SaveVersion / 公共接口变化；
- 必须建立第二套正式产品路径；
- 无法在原任务边界内实现。

当前 Work Package、已确认规格和既有权限内的普通技术实现问题属 Green / Yellow 档，执行 Agent 自主处理；只有 Red 档才升级协调层。不得把普通技术选择、可修复编译 / 测试错误或 Existing Host Map 本身当作人工审批 Gate。

---

## 4. 任务等级与验证深度

### L0：纯文本

文档、注释、命名说明。无需启动 Unity，但要检查链接、路径和相互引用。

### L1：纯代码、低风险

小型逻辑修复、日志、局部空引用保护、不涉及 Inspector 的实现。

```text
修改代码
→ Asset Refresh
→ 等待编译结束
→ 读取 Editor State
→ 对比 Console 基线
```

不强制 Play，但不得只以“代码已写完”作为完成。

### L2：代码 + Editor 配置

MonoBehaviour、UI Controller、新组件、Inspector 引用、Canvas 或层级调整。

```text
读取 Scene/对象/组件
→ 修改代码
→ Refresh/编译/Console
→ 精确配置对象和字段
→ 回读 Inspector
→ 在安全条件下保存目标 Scene
→ 有运行行为时定向 Play
```

### L3：玩法功能

战斗、卡牌、状态、商店、招募、地图、主菜单、存档、结算等。

```text
读取对应 GameSpec
→ 检查代码与 Editor 实际状态
→ 最小范围实现
→ 编译
→ Editor 配置
→ 运行最短可证明路径
→ 回读状态/Console
→ 退出 Play
```

不要求每次跑完整游戏，只运行足以证明改动的路径；但功能属于已有产品流程时，该最短路径仍必须从真实玩家入口进入现有宿主，不能以独立 Harness 替代 Product Integration Verification。

### L4：跨系统或高风险

公共接口、PlayerCollection、SaveData、SO Schema、BattleSystem 核心流程、多 Prefab 联动、Project Settings、大规模重构。

先做只读影响分析和短计划。若用户当前请求已经明确包含该变化，可以直接实施；否则在会固化重大决策前询问用户。复杂任务可在可用时换用更强推理模型，但模型名称不是项目规则的一部分。

---

### 4.1 风险驱动 Gate

Gate 不是通用开发流程。仅在跨多个 Authority、Save 边界、Runtime 生命周期、Return Transaction、多模块高风险串联，或某一步失败可能造成难以回滚的数据/资源损坏时选用。计划中说明独立风险与 Gate 通过条件。

Gate 之间是人工检查边界；Gate 内部默认无人值守执行：

```text
Planning → Implementation → Compile → Targeted Tests → Self-fix → Regression → Gate Report
```

禁止每个 Planning、Implementation 或 Test 步骤都停下来请求批准；不适用的验证标明原因。Green / Yellow / Red（§3.4）只是风险放行判断，不形成第二套状态机。

### 4.2 Hard Stop

以下事项必须暂停相关操作，报告证据并请求用户决策：

- 需要改变已冻结 Architecture 或 Authority；
- 需要改变已关闭 WP 的核心行为；
- 需要修改 SaveVersion；
- 需要扩大当前 Scope；
- 需要进行破坏性 Legacy 清理；
- 测试暴露其他系统真实缺陷，且无法在当前任务内安全处理。

这是设计、Authority、范围或安全决策阻塞，对应 Awaiting Decision。已明确授权的裁决在原范围内有效；新出现的范围或风险不能据此自动放行。普通环境操作按 §6.3 处理。

## 5. Unity 实际状态原则

涉及以下内容时，禁止只根据代码或旧记录猜测：

- Scene、Hierarchy、GameObject、Transform、Active；
- Component、Inspector 字段、引用绑定、Missing Script；
- Prefab、Canvas、Layer、EventSystem、UI 射线；
- 运行时对象、Console 和 Play Mode 状态。

必须优先通过 MCP 读取实际 Editor 状态。读取不到时，先排查连接和工具暴露；确实无法访问时明确记录未验证项，由用户决定人工检查、稍后重试或另行调用备用检查工具。

UI/输入/渲染故障的检查顺序：

1. 组件完整性和字段引用；
2. Active、Layer、Canvas、GraphicRaycaster、RaycastTarget、Mask/RectMask2D；
3. 层级、遮挡、排序和手动配置；
4. Console 与运行时状态；
5. 最后才修改代码。

连续修复失败时不得断言“第 3 次就一定是配置问题”。两轮同层修改无新证据或三轮总验证无进展后，必须重新检查其他层；仍无进展则停止扩大修改并报告证据。

---

## 6. MCP 工具选择与降级

工具优先级：

```text
专用 MCP Tool
> 通用 Unity Object / Component Tool
> 只读 Reflection
> Reflection 调用
> script-execute
```

代码文件默认由代码 Agent 的工作区文件工具修改，以便保留精确 diff；Unity MCP 的 Script 工具只在工作区文件工具不可用或专用能力确有优势时使用。

### 6.1 `script-execute`

`script-execute` 是任意 Editor C# 执行能力，不是常规 Unity 操作捷径。

只有同时满足以下条件才可使用：

1. 已检查当前会话没有对应专用 Tool，或专用 Tool 已确认失败；
2. 操作范围局部、代码可审阅、结果可回读；
3. 明确说明降级原因；
4. 持久性修改已获得当前请求授权或单独批准。

不得用它替代已有的 Play、Component、Asset、Scene 或 Console 专用工具；不得用于批量删除、Git 操作、扫描用户目录、修改存档、Package 或 Project Settings。

### 6.2 Play Mode 专用工具

进入/退出 Play 优先使用 `editor-application-set-state`，状态确认使用 `editor-application-get-state`。

若客户端没有暴露专用工具，按以下顺序排查：

1. AI Game Developer 面板确认该 Tool 已启用；
2. 确认当前 MCP 会话的实际工具列表包含它；
3. 重新连接 MCP；
4. 刷新动态 Tools；
5. 在新会话再次确认；
6. 专用 Tool 调用失败并记录错误后再考虑降级。

不得静默使用 `script-execute` 切换 `EditorApplication.isPlaying`。

---

### 6.3 Manual Environment Intervention

技术方案已经明确，但 AI 缺少可靠环境操作能力时，立即发出 Manual Environment Intervention 通知。常见情况：打开/关闭 Unity 或 Tuanjie Editor、License 激活/登录、打开指定 Scene、进入/退出 PlayMode、MCP 连接/重连、工程锁、SourceTree 或 Git 凭据 Push、GUI Test Runner、系统权限。

```text
Manual Environment Intervention
当前卡点：<步骤、错误或环境状态>
AI 不能可靠完成的原因：<具体能力或权限限制>
用户只需操作：<精确且最小的操作>
完成后请回复：<可识别的完成确认>
恢复位置：<原阻塞步骤>
```

用户完成后从原阻塞点继续，不重跑已完成的开发、测试或前序 Closeout。仅在新变化使旧证据失效时，说明原因并补做受影响验证。环境等待属于 Blocked；不把它升级成设计 Hard Stop。Push 阻塞同时保留 §13.7 的 Push Pending 状态。

## 7. MCP 工具启用与权限策略

工具名以当前 AI Game Developer 版本实际暴露的名称为准；下列名称是能力示例，不因版本改名而放弃同类专用工具。

### 7.1 长期启用：只读和闭环核心

- Asset：find/get/refresh；
- GameObject：find/get；
- Component：get/list；
- Scene：get/list-opened/open；
- Object：get-data；
- Editor：console-get-logs、application-get-state、selection-get；
- Screenshot：Game/Scene/Camera/isolated view；
- Testing：tests-run；
- Package：list/search（只读）。

以下能力也应长期可用，但是否自动批准取决于任务上下文：

- `editor-application-set-state`；
- GameObject create/modify/set-parent；
- Component add/modify；
- Scene save/set-active；
- Console clear；
- Asset create-folder；
- Selection set。

### 7.2 自动批准等级

| 等级 | 默认策略 | 典型能力 |
|------|----------|----------|
| A 只读/临时 | 可自动批准 | find/get/list、Console 读取、Editor State 读取、截图、只读 Package 查询 |
| B 明确任务内的可回滚修改 | 条件自动批准 | Refresh、定向测试、Play/Stop、创建单个对象、添加组件、修改明确字段、保存本次明确修改的 Scene |
| C 高权限或影响面不透明 | 每次审阅/批准 | assets move/copy/modify、Prefab save/apply、object-modify、Reflection call、`script-execute` |
| D 破坏性/项目级 | 默认关闭或明确批准 | delete/destroy、script-delete、package add/remove、Project/Physics/Input/Build Settings、Tags/Layers 结构、批量修改 |

B 级自动执行条件：目标唯一、当前任务明确要求、修改范围可回读、没有混入用户未提交的 Scene/Prefab 改动。保存 Scene 前若无法区分既有脏改动，必须报告并询问。

删除规则：用户当前请求若已精确指定单个目标，可视为授权，但执行前仍须回读目标；模糊删除、批量删除和范围扩大必须确认。优先使用可恢复方式。

### 7.3 当前 38 / 73 的处理

“38 / 73 已启用”是 2026-08-16 的用户报告，项目仓库没有保存完整的面板启用清单，因此不能仅凭数量断言工具齐全。

38 个处于合理的最小工具集区间，但只有实际包含以下闭环能力才算足够：

```text
Editor State 读/写
+ Asset Refresh
+ Console
+ Scene/GameObject/Component 读写
+ Scene Save
+ Play/Stop
+ Screenshot 或状态回读
+ Tests（如项目任务需要）
```

建议长期保持约 30～45 个高频工具，不追求 73 / 73。每次插件或 MCP 版本升级后，按能力而不是旧工具名重新审计。

---

## 8. MCP Extension 策略

### 8.1 必须区分的两层

- Unity Package/Module：工程实际依赖；
- MCP Extension：给 Agent 增加该子系统的专用操作能力。

工程当前已经安装 `com.unity.timeline 1.7.7`，并带有 Animation、ParticleSystem、AI/Navigation、Tilemap 等引擎模块。这不代表对应 MCP Extension 必须启用，也不授权删除现有包。

### 8.2 当前建议

| Extension | 当前策略 | 启用条件 |
|-----------|----------|----------|
| UGUI/UI（若面板提供） | 优先长期启用 | 当前项目大量使用 Canvas、Legacy Text、Button、ScrollRect、拖拽 |
| Animation | 按任务临时启用 | 大量编辑 Animator、Clip、Controller 或正式 UI 动画 |
| Particle System | 按任务临时启用 | 正式技能、命中、状态或 UI 粒子制作 |
| Timeline | 默认不启用 MCP 扩展 | 出现剧情 Sequence、Boss 演出、Cutscene；注意 Unity 包已安装 |
| Input System | 默认不启用 | 引入 Input Actions、手柄或多设备输入；当前 manifest 未安装 `com.unity.inputsystem` |
| Cinemachine | 默认不安装/不启用 | 出现复杂虚拟摄像机、跟随或镜头混合需求 |
| Navigation | 默认不启用 | 出现 NavMesh 角色移动；当前节点地图不需要 |
| Tilemap | 默认不启用 | 出现基于 Tilemap 的关卡/世界地图编辑 |
| Terrain/ProBuilder/Splines | 默认不启用 | 只有明确 3D 关卡制作需求时评估 |

Extension 启用流程：确认项目确实使用该系统 → 检查现有包和版本 → 评估新增工具数量/权限 → 任务期启用 → 完成后决定长期保留或关闭。

不得为了工具数量安装 Package。Package 的安装、移除或升级属于项目级变更，必须有明确授权并完成编译和回归验证。

---

## 9. 代码、Console 与 Play 验证

### 9.1 Module Verification 与 Product Integration Verification

验证必须区分两层：

**Module Verification** 证明局部模块本身正确，例如单元测试、EditMode、局部 PlayMode、数据/逻辑验证、直接调用组件或独立 Test Harness。它不能单独证明功能已经进入产品。

**Product Integration Verification** 适用于已有产品流程中的功能，必须证明：

- 从真实玩家入口进入，而不是直接打开测试 HUD 或调用目标组件；
- 使用正式流程提供的真实数据；
- 进入 Existing Host Map 记录的现有产品宿主；
- 新行为已经接入或替换旧行为，而非与旧流程平行存在；
- 上游入口、下游退出/结算/返回流程没有被破坏；
- Debug/Test Harness 默认不出现在正式玩家路径。

验证路径必须在报告中写明。若 Product Integration Verification 未执行、失败或只能通过独立 Harness，任务只能标记为“部分完成/阻塞”或继续“开发中”，不得标记对应 `Technical Stage Verified`、Feature 已集成或 `Playable Milestone Ready`。

### 9.2 Technical Stage 与 Playable Milestone

> 核心原则：**AI执行粒度细，人类交互粒度粗。**

**Technical Stage** 是 Agent 内部的小型开发检查点。每个 Technical Stage：

- 可以单独实现；
- 必须完成编译和适用的 Module Verification；
- 属于已有产品路径时，必须完成适用的 Product Integration Verification；
- 必须记录实现状态、验证证据和遗留问题；
- 默认不因为该 Stage 验证通过就请求用户人工验收。

机器验证全部通过且下一 Stage 没有必须由用户决定的玩法或产品缺口时，状态记为 `Technical Stage Verified`，Agent可以继续下一 Stage。即使该 Stage 修改高风险产品路径、但尚未形成可玩里程碑，也不能以“不请求用户验收”为由跳过 Product Integration Verification。

**Playable Milestone** 是若干 Technical Stage 形成的完整体验闭环：用户无需理解代码、Legacy、Placeholder 或内部 Stage 边界，只通过正常游戏操作即可自然判断正确或错误。达到 `Playable Milestone Ready`，或出现机器无法判断的主观产品/设计问题时，由当前 Agent 判断必要的产品 Acceptance 并请求用户参与；是否使用风险 Gate 单独按 §4.1 判断。

例如，“回合开始抽牌 → 使用共享 AP 出牌 → 结束回合 → 下一回合再抽牌”形成的完整卡牌回合可以是 Playable Milestone；“SharedDeck 类已经实现，但尚未接回合抽牌”只是 Technical Stage，不是合理的人工验收点。

### 9.3 Technical Checkpoint 与流程归属

Technical Stage 是内部实现粒度，可作为同一任务 Technical Checkpoint 的证据。机器验证通过且无 Hard Stop 时继续；只有需用户产品判断时才请求 Acceptance。Gate 使用由 §4.1 定义，最终收尾与关闭由 §13 定义。跨 Agent 的所有权与证据交接按多 Agent 协议执行，不另建关闭状态机。

只要修改 `.cs`，至少执行：

```text
记录修改前 Console
→ Asset Refresh
→ 等待 compiling/update 状态结束
→ 读取 Console
→ 区分已有问题和新增问题
```

L2/L3 继续执行 Editor 或 Play 验证。若 Unity/MCP 不可用，必须明确写“未完成 Unity 编译/Play 验证”，不能把静态检查当作已验证。

清理 Console 前必须先记录基线。定向测试可在记录后清理，再运行目标路径。

以下改动通常需要主动 Play：玩法行为、UI 交互、拖拽、生命周期、Save/Load、战斗/状态/商店/招募/事件、场景或界面切换。

失败后允许基于新证据修复并重试。默认最多三轮；若没有新证据、影响范围扩大或需要改变已确认规则，应提前停止。报告失败现象、已尝试方案、当前证据和建议下一步。

### 9.4 Anti-Hang 与本轮验证

测试、Unity Editor 或 MCP 查询不得无限等待或重复轮询：

- 单纯重复同一 `tests-run` / Editor State / MCP 查询，没有状态变化、没有新输出、没有新证据，**不算新一轮进展**；
- 连续无状态变化、无新输出、无新证据时，必须停止重复调用，转入诊断或替代验证路径；
- 有其它安全验证路径则继续；必要验证无法继续则转 `Blocked`（`Docs/AI多Agent开发协作协议.md` 7.1），不得无限轮询；
- 任何一次重复查询必须能说明本轮相对上一轮的差异，否则就是没有进展。

历史基线只能作参照，不能当本轮证据：

- 历史测试 / 历史 Handoff / 历史 PASS 只能作为历史基线，不能标记为本轮 PASS；
- 本轮未执行成功的验证必须写 **本轮未验证**，不得借历史结果记 PASS；
- 引用历史基线时须注明“沿用 X 基线（验证名 + 结果）”，并确认其适用范围覆盖当前改动范围。

---

## 10. Scene、Prefab、SO 与 CSV

### Scene

- .scene、.prefab、.asset、.meta 不属于普通文本文件，修改其中的 UI/对象不自动授权直接编辑 YAML；
- 修改前读取活动 Scene、目标对象和组件；
- 禁止只凭名字猜对象，必须用路径、组件或唯一结果确认；
- 修改后回读验证；
- 只保存本次明确修改的 Scene；若 Scene 还有无法隔离的既有改动，不直接保存；
- MCP 不可用时停止该写操作并报告；只有用户明确批准“直接编辑指定序列化文件”后才允许降级编辑。

### Prefab

- 读取可自动执行；
- 单个 Prefab 的明确字段小改可在当前授权内执行；
- 基础 Prefab 重组、批量 Apply、删除组件、多 Prefab 联动需要明确批准和影响分析。

### CSV / ScriptableObject

配置生产是按领域并存的 Transitional 状态。以下仅是逐域核实后仍使用 CSV / 手工 Resources SO 的领域的现有链；已迁移的 Monster Definition / Enemy Presentation 改走 `Development/Config/README.md` 所述 XLSX / SourceAssets 链，不使用此链：

```text
Development/Excel/*.csv
→ Unity CSV Importer
→ Assets/Resources/ScriptableObjects/
→ DataManager
→ DataValidationTool
→ 必要的 Play 验证
```

- 对已核实仍使用 CSV 的领域，优先修改其当前来源，不直接批量修改正式 SO；其它领域不可照此推断；
- 已迁移 Static Config 的生成资产位于该领域当前 Generated Root（例如 `Assets/Generated/StaticConfig/`），由 Builder 拥有并可重建，不得人工编辑；
- CharacterCard 的现行配置方式须核对当前代码和数据；2026-06-28 的 CSV/SO 手册仅供历史追溯，不直接作为当前操作指令；
- 导入后运行 `RRPG/Tools/Validate Game Data`；
- `Assets/_Archive/` 不是正式数据源；
- 新建 SO 必须使用 `ScriptableObject.CreateInstance<T>()`；
- 已存在的 SO 不得为了“重建”而无条件先删除，应优先就地更新；确需替换时按删除权限处理。

---

## 11. 视觉、存档与 Git 特殊边界

### 视觉

设计文档中的左/中/右、前/后等描述首先是逻辑概念，不自动等于像素坐标。AI 可以实现用户已明确的视觉数值并通过 MCP 配置，但不得自行决定“更漂亮”“更舒服”的主观方案。可调视觉参数应尽量可在 Inspector 调整。

### SaveSystem

Save 任务先进入 `GameSpec/README.md` 查看当前 Authority 矩阵与 `GameSpec/Save/Save-GameSpec-V0.1.md`，再结合 Person、Expedition、Village 等领域 Current GameSpec 确认持久化语义。Pre-8/30 Save 旧规格已退休；Legacy schema / DTO 通过 Git History 与当前代码按需追溯，不能覆盖 Current Save GameSpec。遇到旧 DTO、Injury、IsDead 或旧 Battle 字段时，不得默认按 Current 实现。普通实现和明确 Bug 修复仍须在上述边界内执行；改变 SaveData 字段、SaveVersion 语义、ID 恢复策略、单/多存档模型、迁移策略或保存范围属于结构性变化。保存触发职责继续作为待正式技术方案记录的问题，不由当前实现自动裁决。不得用测试覆盖用户真实存档；测试必须使用隔离路径、备份或明确的测试存档。

### Git

可自动执行 `status`、`diff`、`log`、查看分支。除非用户明确要求，不自动 commit、rebase、reset、clean、覆盖式 checkout、force 或删除分支。禁止回退用户现有改动来方便本次任务。

---

## 12. 项目已验证的引擎约束

- 团结引擎 1.9.1 基于 Unity 2022.3.62f1/当前工程标识 `2022.3.62t9`；
- `Resources.GetBuiltinResource<Font>("Arial.ttf")` 不可用，使用项目已验证的字体方案；
- `Image` 与 Legacy `Text` 都属于 `Graphic`，不要挂在同一 GameObject；
- 未迁移且仍采用手工 Resources SO 的领域继续使用既有 `Assets/Resources/ScriptableObjects/`；已迁移 Static Config 使用其当前 Generated Root（例如 `Assets/Generated/StaticConfig/`），不得人工编辑；
- CSV 使用 UTF-8；若现有导入器要求 BOM，以导入器实测为准；
- ScriptableObject 使用 `CreateInstance<T>()`，不能 `new T()`；
- UI 对象的 Layer、遮挡和 Raycast 设置必须从 Editor 实际状态验证。

若后续实测推翻其中某项，记录证据后更新规则，不能继续传播旧结论。

---

## 13. 修改范围、完成层级与最终交接

坚持解决当前任务所需的最小修改：不顺手重构、统一命名、升级架构、添加未来功能或清理无关代码。额外问题若不阻塞当前任务，只记录并在开发侧报告中说明。

### 13.1 开发侧交付标准

完成标准按任务等级判断。典型 UI 组件任务的开发侧交付应包含：

```text
代码完成
+ 编译通过
+ Component 挂载
+ Inspector 绑定回读
+ 定向 Play/状态验证
+ Console 无本次新增错误
```

以上证明 Technical Stage 达到相应机器验证门槛；它不自动等于“待用户人工验收”。

如果任务修改的是已有产品流程，以上清单还必须包含：Existing Host Map 已执行，以及 Product Integration Verification 已从真实玩家入口通过。仅有 Module Verification、独立 Demo 或 Test Harness 通过时，不满足 `Technical Stage Verified` 的集成门槛。

开发侧报告保持简洁：

- 实现：实际修改和行为变化；
- Editor：自动配置了什么；
- 验证：编译、Console、Play/Test 的路径与结果；
- 未验证/阻塞：明确缺少哪一步；
- 需要用户判断：只列设计、视觉或高风险决策；
- 当前状态：`开发中`、`Technical Stage Verified`、`Playable Milestone Ready` 或 `部分完成/阻塞`。
- Final Closeout 报告须附 §13.5 的 Closure Commit Staging Manifest，先完成 Disposable Cleanup，再核对本任务文件范围。

若 MCP 已完成全部配置，明确写：`无需额外人工 Unity 配置。`

#### Milestone Acceptance Card

请求 Playable Milestone 人工验收时，只提供一份一页以内、原则上不超过 10 项的简短验收卡。每项只写：

> 操作 → 正确表现

验收卡不得解释类、Controller、Schema、Legacy、Placeholder 或内部 Stage 边界，也不得要求用户判断这些技术身份。用户必须能仅凭正常游玩判断每项对错；无法通过正常游玩直接判断的内容由 Agent 自动验证，不加入验收卡。

Changed、Legacy、Placeholder、技术证据和自动测试结果继续保留在开发报告中，默认不作为用户人工验收材料。

### 13.2 状态与证据分离

| 状态 | 含义 |
|---|---|
| 开发中 / Technical Stage Verified | 实现或验证进行中；检查点通过后继续同一任务 |
| Playable Milestone Ready | 已具备真实产品验收条件，需用户判断时提供验收卡 |
| Acceptance Passed | 既定验收标准通过；必要用户产品确认已取得，不代表关闭 |
| 部分完成 / Blocked / Awaiting Decision | 如实报告缺口；协作异常状态按多 Agent 协议 §7.1 |
| Ready for Commit / Push | Closeout 内容已准备、审阅通过，尚无本任务 Git 写入授权 |
| Closeout Complete / Push Pending | 本地收尾与提交完成，Push 因凭据、网络或环境失败；不得写任何 Closed 状态 |
| Work Package Closed | 当前任务完整收尾，Closure Commit 已 Push 到声明的 Target Branch，并核对该任务无遗漏 |
| Branch Closed | Target Branch 本地 / 远端一致，暂存区为空、tracked workspace clean，且该分支没有待发布工作 |
| Repository Closed | 相关变化已经进入正式共享 `main`，并完成 local main / origin/main / workspace 核验 |
| Development Closed | 历史状态词；保留原记录，不作为新任务关闭用语 |

Acceptance Passed ≠ Work Package Closed ≠ Branch Closed ≠ Repository Closed。实现、编译、Console、Play/Test、用户 Acceptance 分开记录；静态检查不代替 Unity 编译，自动测试不代替主观产品验收，历史 PASS 不冒充本轮 PASS。无需用户判断的客观任务记录 Acceptance 的标准和机器证据，人工项写“不适用”。

### 13.3 Acceptance 与 Final Closeout

需要产品判断时，以当前任务明确验收确认为准；固定口令“验收通过，收尾。”仍可使用。“可以继续”等含糊反馈不算验收；附有失败项或未满足条件时只处理失败范围。客观任务不强制等待人工口令。

Acceptance 通过后，同一 Agent 在任务授权范围内依次完成：

1. 实际写入 Handoff，并回读 HANDOFF 区域及 diff；字段格式见 [开发验收交接](开发验收交接.md)。必须记录 Target Branch；未提交时如实记录，不预填未来 SHA 或任何 Closed 状态。
2. 同步受影响的 Current 状态文档；只处理任务事实，不改写历史、不由实现反推规则。任务白名单或“仅审计/不改状态”等限制始终优先。
3. 按 §13.6 分类并清理本任务 Disposable 资产。
4. Final Diff / Workspace Review：核对全部任务修改、删除、新增、必要 .meta、残留、暂存区、其他任务保护边界、证据及 Authority 一致性；完成文档链接/格式检查和适用验证。
5. 生成 §13.5 精确清单；存在归属不明、遗漏、任务外混入或必要证据不足时暂停对应收尾步骤。
6. 按 §13.7 的任务级 Git 授权分支执行，并报告真实状态。

普通任务允许产品代码、必要长期测试、状态同步与 Handoff 形成一个原子 Closure Commit。仅在用户要求、文件所有权冲突、并行隔离、明显独立回滚价值或明确仓库治理理由下拆分提交；不强制 Feature Commit → 独立状态管理员 → Docs Commit。

### 13.4 状态同步与文档影响

当前开发 Agent 负责完整收尾；独立状态管理员用于周期性治理，其职责见多 Agent 协议 §4。

- Handoff：最近一次真实交接与生成时 Git 快照；
- 项目状态快照：当前事实；
- 项目日志与 Git history：历史；
- 问题清单：问题生命周期；
- 决策记录：用户明确确认的长期决定；
- GameSpec：已确认玩家规则；
- [项目日志](项目日志.md)与 Git History：版本/里程碑级变化和历史追溯。

逐项判定受影响资料为“已处理 / 无需处理 / 另立任务 / 未授权”，不要求每次全改。必要同步超出明确白名单时请求范围授权，不假装完成。改变治理规则时核对根规则、执行手册、协作协议、文档地图及Handoff固定格式的相关引用。历史验收、测试数字、Feature/Docs Commit事件保持原样。

### 13.5 Closure Commit Staging Manifest

先完成 Cleanup，再输出：

```text
Closure Commit Staging Manifest
Must Stage
- 本任务精确路径（含必要删除和 .meta）
Must Not Stage
- 受保护的其他任务路径、临时产物或无关变化
Expected staged file count: N
```

清单覆盖代码、必要长期回归测试、必要配置、状态文档及 Handoff，不按文件类型机械拆组。只有正式长期回归测试及任务明确要求的持久证据可以作为测试证据资产进入 Closure Commit。TestResults 与临时日志默认不提交；已跟踪的一次性产物经清理后，其删除应进入本任务清单，不因 tracked 身份保留内容。

实际路径/数量与清单不一致或归属无法判断时，停止暂存/提交并核实；不得混入其他 Agent 的工作。未获 Git 写授权时，清单只是准备材料，不执行暂存。

### 13.6 Disposable Tests by Default

Development / Acceptance 允许创建专项测试、Gate Test、Acceptance Test、临时 TestRunner / Editor Runner、TestResults、临时日志和诊断脚本。Final Closeout 默认删除本任务一次性 Gate 专项测试、Acceptance 测试、Runner、TestResults、临时日志和诊断脚本；没有产物时不机械清理。先确定精确目标与所有权，保护其他任务资产，不以 Cleanup 扩张为破坏性 Legacy 清理。

Tests 目录不是开发历史档案库；Git history、Handoff、项目日志承担历史证据。只允许保护已冻结核心契约的 **Long-term Regression Test** 最小代表集合长期保留，例如：

- PersonId / CardId Identity；
- Save / Load Compatibility；
- Return Exactly-Once、Unique Final Save；
- Progression Authority、Growth Authority；
- VillageTime Single Clock、Resource Authority；
- Food ↔ Carry No-Copy；
- 其他 Current Architecture 已明确冻结的不变量。

“以后可能能检查”“以后也许有用”“已经为开发写出来了”均不是长期保留的充分理由。重复保护同一事实时合并为最小代表集合。记录保留测试所保护的契约及清理结论；Cleanup 后按影响复核编译/测试入口与必要回归，不保留失效引用。

### 13.7 Git 授权与三级关闭

同一 Agent 负责完整 Final Closeout，但 commit 和 push 必须取得任务级明确授权。

- **已授权**：用户在开始或验收后明确授权“收尾、提交并 Push”，当前 Agent可完成任务范围内Cleanup、状态同步、Handoff、Diff检查，仅暂存本任务文件、创建Closure Commit并Push。授权限当前任务及明确文件范围，不产生永久Git权限。
- **未授权**：完成内容准备、精确暂存清单与建议提交名称，停在 **Ready for Commit / Push**；由用户通过SourceTree或其他明确方式完成Git写入。
- **Push失败**：本地提交完成而Push因凭据、网络或环境失败时，仅标记 **Closeout Complete / Push Pending**；不得标记 Work Package Closed、Branch Closed 或 Repository Closed。按§6.3请求必要环境介入，用户Push后从Git核验继续，不重复开发、测试或前序Closeout。

关闭必须按层级逐项确认：

1. Development / Fix完成，Verification通过，必要用户Acceptance通过。
2. Final Closeout内容完成：Current状态同步、Disposable Cleanup、Handoff落盘与回读、Final Diff / Workspace Review通过。
3. Closure Commit 已完成并 Push 到声明的 **Target Branch**；读取实时远端证据确认本地提交已发布，不能仅凭陈旧的远端跟踪引用推断成功。满足 1–3 且本任务无遗漏，可写 **Work Package Closed**。
4. Target Branch 的 local HEAD == origin/&lt;Target Branch&gt;，暂存区为空、**tracked workspace clean**，且该分支没有待发布工作，才可写 **Branch Closed**。
5. 只有相关变化已合入正式共享 `main`，并确认 local main == origin/main、暂存区为空、tracked workspace clean，才可写 **Repository Closed**。已配置 origin/HEAD 时核对其指向 origin/main；未配置不算失败。
6. 无需删除与本任务无关且明确受保护的本地忽略文件。若仍有其他并行任务的已知变化，只报告本任务或分支的精确状态，不越级宣称 Branch Closed / Repository Closed。

feature / migration 分支可以 Work Package Closed / Branch Closed，但在尚未合入 `main` 时不能使用今天定义的 Repository Closed。历史 `Development Closed` 保留旧语义；历史 feature / migration 分支若写过 Repository Closed 且同时明确 main 未合入，按今天的 Branch Closed 证据理解，不批量改写。Handoff 保留生成时的历史 Git 事实；最终关闭报告提供实际 Closure SHA、Target Branch、Push 及工作区证据，不回填未来 SHA。
