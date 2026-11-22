# WP-Event-1 Unified Event Content Foundation V0.1 — Closeout Report

> 类型：**Closeout Report（Technical Verified + Producer Play Acceptance PASS + Committed / Pushed）**
> 范围：**WP-Event-1 Unified Event Content Foundation V0.1**（1A / 1B / 1C / 1D D0–D3 + D1-R + Producer Acceptance Repair 1 & 2）
> 状态：**`WP-Event-1 CLOSED`**（已 Commit `edaa4244b36b4da6ff5272f9716fb1e42a8ae935` → 已 Push `origin/main`；远端核对一致、工作树 clean；见 §N 与 Final State）
> 日期：2026-09-26
> 权威入口：本报告只做**收口与证据汇总**；玩法规则看 [GameSpec](../../GameSpec/README.md)，当前设计状态看
> [GameDesign/当前设计状态](../../GameDesign/当前设计状态.md)，内容 Authoring 看 [Development/Config README](../../Development/Config/README.md)，
> 开发期契约全文见 [WP-Event-1 FDS](2026-09-26_RRPG_WP-Event-1-Unified-Event-Content-Foundation-FDS-V0.1.md) §21.9–§21.14。

---

## A. Repository Facts

| 事实 | 值 / 依据 |
|---|---|
| 目标分支 / HEAD | `main`；收口生成时 HEAD = `ba59be67757471a4d9a92e54469ae401eef66d5e`；**最终 HEAD = `edaa4244b36b4da6ff5272f9716fb1e42a8ae935` = `origin/main`**（2026-09-26 制作人授权后执行 Commit + Push；见 §N） |
| 工作树规模（生成时） | `git status` 显示 46 个 tracked 修改 + 46 个未跟踪**条目**（未跟踪项含**被折叠成一行的新目录**）；实际提交路径数 = **357**（46 M + 311 A，无 D / R）；`git diff --check` = 0；`Assets/InitTestScene*` = 0（无测试运行残留） |
| 工作树性质 | **同一工作树累积交付**（1A→1D→D1-R→Repair 1→Repair 2），已于 2026-09-26 按 §K-A + §K-B **单 Commit** 提交并 fast-forward Push；收口后工作树 clean |
| 最终验证 | 在**当前最终工作树**上重新执行（Compiler / validate / Full EditMode / Full PlayMode）——见 §H |
| 证据目录 | `D:\BBB\_rrpg_runs\d3\`（**仓库外**，不随 Git 提交） |
| 测试运行器 | Tuanjie 2022.3.62t9 batchmode（`-runTests`）；Compiler 用 `dotnet test`（.NET 10） |
| 生成物 | `Development/Generated/StaticConfig/*`（Mirror）+ `UnityProject/.../Assets/Generated/StaticConfig/*`（Unity 生成根，不进 Git） |
| 仓库外产物 | 本次所有 XML / log / 诊断文件均在仓库外 → 工作树内**无**测试运行残留（已核对 `InitTestScene*` 为 0） |

## B. Final Scope（WP-Event-1 全景）

| 阶段 | 内容 | 契约性质 |
|---|---|---|
| **1A** Static Event Content Foundation | `RRPG_Events.xlsx`（Event / EventTrigger / EventRole / EventRoleCondition / EventCondition / EventPresentation / EventOption / EventOptionCondition / EventOutcome / EventEffect）+ Compiler 校验（`DAT044` / `DAT047` 等）+ Mirror + Unity 只读读取入口（`EventStaticCatalog` / `EventConfigDatabase` / `EventConfigBuilder`） | **正式 V0.1 契约**（Schema / 校验 / 读取入口）；**内容是 Test / Not Frozen** |
| **1B** Resolver Foundation | `EventResolver` 求解契约：Trigger 匹配 → Role 绑定（SelectorType）→ Condition 求值 → `ResolvedEventInstance`（含 `EventInstanceId` / RoleBindings / ContextSnapshot）；确定性（无 Guid / 时间 / 随机） | **正式 V0.1 契约** |
| **1C** Option / Outcome / Effect Foundation | `EventOptionEvaluator`（点击此刻世界事实；Hide / Disable）、`EventOptionExecutor`（条件重检 → Outcome → Effect Plan → Preflight → Commit → History → Completed；exactly-once）、`IEventWorldView` / `IEventEffectGateway` 双接缝 | **正式 V0.1 契约** |
| **1D-D0** Authority / Persistence | Event World（长期 History / OccurrenceCount）+ Expedition Session History（Runtime-only）+ Save DTO（Persistence 边界）+ `VillageDayOrdinal` / `VillagePersonRestState` | **正式 V0.1 契约** |
| **1D-D1** Village Product Cutover | Village 正式识别路径接 Unified Event Core（`VillageEventFlow` + `VillageEventStaticConfigAdapter` + `VillageEventEffectGateway`）；旧档 Legacy Pending 收敛迁移 | **正式 V0.1 契约**（Legacy 兼容见 §C/§5） |
| **1D-D2** Expedition Product Cutover | Event / Content Node 正式切 Event Core（原子进入 + `ActiveExpeditionEventState` + Node Settlement exactly-once + StartBattle Preflight/Commit 同一份 EnemyRoster + Victory/Defeat 结算同一 Node） | **正式 V0.1 契约** |
| **1D-D3** Presentation Authority Closure | 两表面表现 Authority 闭合：`EventId → EventPresentation`、共享唯一 `EventTemplateRenderer` 入口（`EventPresentationTexts`）、插图唯一正式链（`IllustrationAssetId → 共享 VisualAsset → Sprite`）、失败语义统一 Fail Closed | **正式 V0.1 契约** |
| **D1-R** PlayMode Regression Baseline Repair | 修复 `ba59be6` 基线既有的 5 条 PlayMode failure（只改测试 / 测试 helper） | 测试基建（**非产品契约**） |
| **Producer Acceptance Repair 1** | Issue A（Battle 卡后误出 Branch/Event 卡 + Battle 表现锁）、Issue B（储备口粮三时点证伪）、Issue C（点击前只读可行性预测 → Option 预置灰）、Issue D（内容表意登记） | **正式产品规则**（A/C 的**行为**）+ 内容债登记（D） |
| **Producer Acceptance Repair 2** | Issue E：产品 Node Entry 返回真实成败；**Presentation progression follows Product progression**（任何 Entry 失败不得推进表现） | **正式产品规则** |

**明确不在 WP-Event-1 范围**：正式 Event 内容批量生产、正式 Event 插图、Village / Journey 正式美术、Person Visual 生产方案、
ChoiceHistory、Event 队列 / Deadline / 惩罚、多事件并发（见 §I / §J / §M）。

---

## C. Product Decisions（本 WP 确认并收口的规则）

> 以下条文是 **V0.1 契约**；其中标注 `Test / Not Frozen` 的是**当前内容**，不是产品规则。

### C1 Unified Event Authority
**Formal Event 内容由 `EventId → Event static config` 唯一决定**，包括：Trigger、Role、Condition、Presentation、Option、Outcome、Effect。
不存在第二套 Event 内容来源；`NodeId` / Legacy Catalog **不是** Formal Event 的 Authority。

### C2 Template
正式占位符只有两种：`{<真实 RoleId>.DisplayName}`、`{Context.Key}`；渲染只经唯一 `EventTemplateRenderer`，失败 **Fail Closed**。
Compiler 侧 `DAT047_EVENT_TEMPLATE_ROLE_REF` 阻止「元变量名字面量」（如 `{Requester.DisplayName}` 未指向真实 RoleId）进入正式内容。

### C3 Village Recognition（当前内容 = Test / Not Frozen）
当前 Test 识别类型 `TemporaryDraftBuildup`，Context = `RecognitionPerson` / `RecognitionType` / `ObservedCount`；
当前 Test 事件阈值 `ObservedCount == 3`。**Legacy fired 不再是新 Event Authority**（旧 fired 只保留旧档兼容读取）。
→ **标注：Test / Not Frozen**（命名、阈值、上下文键都不冻结）。

### C4 History
* **World History**：长期（进 Save；`OccurrenceCount` 用于 Repeat / 会话外重复约束）；
* **Expedition Session History**：**Runtime-only**（不进 Save）；
* Resolver **只读** History；History **只在 Execution 成功之后**写入（失败 = 不写）。

### C5 Exactly-once
`EventInstanceId` 是执行边界：重复 Option / 重复 callback 不得重复 Effect、不得重复写 History、不得重复 Node settlement
（阶段守卫 + 实例已结算拒绝；重复执行返回 `InstanceAlreadyResolved` 且零 mutation）。

### C6 Expedition Event：Enter 语义
Event / Content Node：**Resolve Event 成功后才 Enter Node**；失败 → **零 Progression / Supply / elapsed mutation**（节点不进入、无占位回退）。

### C7 Supply / Time
`AdvanceExpeditionTime` **不直接写 elapsed**：额外天数先累积为 `PendingExtraElapsedDays`；
最终 `NodeElapsedDays + AdditionalElapsedDays` 统一进入 Node settlement，并 **exactly-once** 消耗 Supply
（成本 = `PartySize × 总天数 × PerPersonDailyConsumptionRate + ExtraSupplyCost`）。

### C8 StartBattle（Event 内战斗）
Preflight **预生成** `EnemyRoster` → Commit 使用**同一份**（不重新 Generate）→ Prep / Battle 消费同一份；
Victory / Defeat 都结算**同一个** Event Node（Victory → Completed；Defeat → Resolved 未 Completed）。

### C9 Journey Presentation（Repair 2 冻结）
> **Presentation progression follows Product progression.**

任何 **Node Entry Failure**（InsufficientSupply / Event Resolver 失败 / Encounter 生成失败 / already entered / invalid phase …）都不得：
消费 Journey Cycle、跳到下一节点、自动重试、生成后续 Presentation。表现层**只消费产品入口的成败**，不自行判断 Supply / 合法性 / Progression。

### C10 Presentation Authority（D3 冻结）
Formal Event 表现：`EventPresentation → EventTemplateRenderer`；**Village 与 Expedition 共用同一 Renderer**（`EventPresentationTexts` 为唯一产品入口）。
Formal Event 插图：`IllustrationAssetId → 共享 VisualAsset Authority → Sprite`；失效时**明确诊断 + 类别级占位图，绝不换图**。
**不允许 `NodeId` 偷偷成为 Formal Event Presentation Authority**（Legacy Catalog 只服务未迁移表面：Battle / Branch / 既有占位）。

---

## D. Technical Architecture（实现结构）

| 层 | 位置 | 职责 / 边界 |
|---|---|---|
| Event Core（Runtime, asmdef `Event.Runtime`） | `Assets/Scripts/Event/` | `EventResolver` / `EventTemplateRenderer` / `EventOptionEvaluator` / `EventOptionExecutor` / `EventWorldState`（History）/ `EventStaticCatalog` 接口 / `IEventWorldView` / `IEventEffectGateway`；**不引用 Unity UI / Scene** |
| 静态配置 Runtime（asmdef `StaticConfig.Runtime`） | `Assets/Scripts/StaticConfig/` | `EventConfigDatabase` / `EventConfigBuilder` / `EventDefinitions`（只读记录）；`StaticConfigRegistry` 为唯一领域入口（含 `TryGetVisualAssets`） |
| 产品适配层（Assembly-CSharp） | `Assets/Scripts/EventHost/` | `VillageEventFlow` / `VillageEventStaticConfigAdapter` / `VillageEventEffectGateway` / `ExpeditionEventFlow` / `ExpeditionEventCardAdapter` / `ExpeditionEventEffectGateway` / `ExpeditionEventWorldView` / `EventPresentationTexts`；把 Event Core 接进 Village / Expedition 既有 Authority |
| 会话状态（Assembly-CSharp） | `Assets/Scripts/Adventure/ExpeditionEvent/` | `ActiveExpeditionEventState`（唯一活动事件：实例 / 阶段 / PendingExtraElapsedDays / CommittedBattle）+ `AdventureManager.ExpeditionEvent`（原子进入 / 结算 / 清除） |
| 表现层 | `Assets/Scripts/UI/`、`Assets/Scripts/Journey/Runtime/` | 统一卡面 `JourneyEventCardView` + `JourneyPresentationCycleController`（Cycle / Transition 预算）+ `ExpeditionJourneyCoordinator` / `ExpeditionJourneyView`；**只表现、不拥有规则** |
| Authoring / Compiler | `Development/Config/*.xlsx` → `Tools/RRPG.ConfigCompiler` → `Development/Generated/StaticConfig/*` → Unity Builder | XLSX = 唯一人工 Authoring Authority；Mirror = 生成物；Unity 生成根 = Builder 产物（不进 Git） |

**接缝原则（本 WP 冻结）**：Event Core 只经 `IEventWorldView`（读世界事实）与 `IEventEffectGateway`（写世界后果）与产品交互；
产品侧不存在「第二套 Event 判定 / 第二套 Supply 合法性 / 第二套 Return」。

**关键类型**：`ResolvedEventInstance`（EventInstanceId / EventId / RoleBindings / ContextSnapshot）、`EventOptionExecutionResult`（Code / OutcomeId / HasTerminalEffect）、
`ActiveExpeditionEventState`（Phase：`AwaitingOption` / `AwaitingResultContinue` / `AwaitingBattleOutcome`）。

---

## E. Runtime Product Cutover（真实产品路径）

### E1 Village
`真实调度（Project / 承诺事实）→ VillageSocialFactLedger → Recognition（计数达阈值）→ EventResolutionRequest（TriggerType / TriggerKey / ResolutionSeed / Context）
→ EventResolver → EventRuntimeBridge.PendingVillageEvent → VillageSocialEventUI 正式模式（Title / Body / Options 全部来自 Event Config）
→ Option（EventOptionEvaluator + EventOptionExecutor）→ Result → VillageEventEffectGateway（真实 Authority 写入）→ World History / 村民休整（Rest）`。

* 正式路径**不再**创建 Legacy Pending / fired；Legacy 只保留**旧档收敛能力**（无法迁移的 Unknown Legacy Pending 继续可读 / 可走兼容路径）；
* 确定性：`TriggerKey` 由真实事实派生、`ResolutionSeed` 由 TriggerKey 派生（无 Guid / 时间 / 随机）。

### E2 Expedition
`Journey → Event / Content Node → ValidateEnterNode（只读校验）→ EventResolver → 成功才 TryEnterNode + 提交同一实例为 Active Event
→ 正式事件卡（配置驱动）→ Option（可含 StartBattle Terminal Effect）→ Result「继续」→ Node Settlement（elapsed + Supply exactly-once）→ Journey 继续`；
`StartBattle`：Preflight 生成 `EnemyRoster`（未提交）→ Commit 提交同一份 → Prep / Battle → Victory / Defeat 结算**同一个** Event Node。

* **原子进入**：失败 ⇒ 零 mutation（节点未进入、CurrentNode / Supply / elapsed 不变、无半成品 ActiveEncounter、无占位卡回退）；
* **活动事件冻结**：同一 Node 重复读取 / 卡面刷新不重抽实例（`EventInstanceId` / `EventId` 稳定）；
* 结算成功后清除活动事件（Runtime Only）。

### E3 Persistence
* 长期：`EventWorldState`（History / OccurrenceCount）随 Save；Village 侧休整（`VillagePersonRestState`）随 Save；
* 会话内：`ActiveExpeditionEventState`（含 PendingExtraElapsedDays / CommittedBattle）与 Expedition Session History **Runtime-only**（Load / 重开重建，不伪造历史）。

### E4 Legacy 收敛（本 WP 实际动作）
* 旧 V3B Village Pending（`TemporaryDraftBuildup`）在加载后被**收敛为 Unified Event Pending**（只保留 Pattern / Person / Year，不重判条件），
  随后清除 Legacy Pending（fired / ChoiceHistory / Rest 记录不动，不重写历史）。

---

## F. Presentation Authority

| 表现元素 | 唯一 Authority（V0.1 冻结） |
|---|---|
| Village 标题 / 正文 | `EventPresentation` → `EventPresentationTexts` → 唯一 `EventTemplateRenderer` |
| Expedition 标题 / 正文 | **同一**入口（Village / Expedition 共用） |
| Option 文案 / 副文案 | `EventOption.LabelTemplate` / `SubLabelTemplate` → 同一入口；渲染失败 → 整张卡面 Fail Closed（SubLabel 失败 → 省略该行） |
| Option 可用性 | `EventOptionEvaluator`（Hide → 不出现；Disable → 显示但不可点）+ **点击前只读可行性预测**（见 F3） |
| 插图（Journey 卡） | `IllustrationAssetId` → 共享 VisualAsset Authority → Sprite；未配置 = 类别级占位图（无诊断）；配置但失效 = **明确诊断 + 占位图，绝不换图** |
| Pause / Urgency | `EventPresentation.PauseOnOpen` / `Urgency` |
| Result 文本 | `Outcome.ResultTextTemplate` 的渲染结果（来自 Active Event 状态，不重抽） |

**F1 Fail Closed**：无实例 / 无表现配置 / 模板非法 → 失败 + 可操作诊断（**不**显示 `{...}`、**不**渲染半成品卡面）。

**F2 Presentation lifecycle（Repair 1 + 2）**：
* Battle / Prep 真正结束前**不得**出现新的 Branch / 正式 Event 卡（`PresentationLockedForBattle`；Journey 重新可见时解锁）；
* **任何** Node Entry 失败 → 不消费 Journey Cycle、不跳节点、不自动重试、不生成后续 Presentation（`Presentation progression follows Product progression`）。

**F3 点击前可行性（Repair 1-Issue C）**：`ExpeditionEventFlow.TryPreviewOptionFeasibility`（**只读**：复用 `PreviewOutcome` + `TryEvaluateSettlementFeasibility`；
不 Commit / 不写 History / 不改 Outcome / 不消耗 RNG / 不预生成阵容）→ 必然不可行的 Option 直接 **Disabled + 短原因**；条件变化后重新构造卡面即恢复可点。

---

## G. Producer Acceptance

> **2026-09-26 Producer Play Acceptance：PASS**
> 范围仅代表 **WP-Event-1 Unified Event Content Foundation V0.1**；**不代表** RRPG 整体产品体验验证完成。

制作人实际复验确认（原话要点）：

* Village Formal Event 正常；Expedition Formal Event 正常；
* Battle Encounter 点击「进入战斗」后**不再**错误弹 Branch / Event 卡；
* Supply 不足的 Event Option 已能**提前 Disabled**；
* Supply 不足的 Branch / Node Entry 被拒绝后，Journey **不再继续推进**；补足条件后可正常继续；
* 一键导表后 `RRPGStaticConfigRoot` 恢复，出征正常；未发现其它产品侧问题。

**关于「储备口粮」**：`SupplyDelta +2 + AdvanceTime 1`，两人队净 Supply = 0 属于当前 **Test / Not Frozen 内容**的正常算术结果
（三时点实测：S1 = S0+2 ✓，S2 = S0+2−成本 = S0 ✓）。玩家对「+2 → 时间消耗 −2」缺乏中间反馈，登记为
**`Event Result` / Test Content Presentation Debt（不阻塞）**。

---

## H. Verification（**最终工作树**，真实执行）

> 全部验证均在**最终工作树**上重新执行（不引用历史结论）；证据目录 `D:\BBB\_rrpg_runs\d3\`（**仓库外**）。

| 验证 | TOTAL | PASSED | FAILED | SKIPPED | `error CS` | 证据文件 |
|---|---:|---:|---:|---:|---:|---|
| Compiler full（`dotnet test -c Release`） | 208 | **208** | **0** | 0 | — | `f_compiler.txt`（`COMPILER_TEST_EXITCODE=0`） |
| `RRPG_检查配置.cmd`（validate，只读） | — | — | — | — | — | **`结果：PASS errors=0 warnings=5` / `RESULT: PASS` / `VALIDATE_EXITCODE=0`**（`f_validate.txt`） |
| **Full EditMode** | **1013** | **1013** | **0** | 0 | **0** | `f_edit.xml` / `.txt` |
| **Full PlayMode** | **84** | **84** | **0** | 0 | **0** | `f_full_play2.xml` / `.txt` |

* **失败明细：全部 `FAILED = 0`** → 不存在失败用例。
* **计数自洽**：Full EditMode 1013 = D3 基线 1007 + D3 新增 8 + Repair-1 新增 4 + Repair-2 新增 2；
  Full PlayMode 84 = D2/D3 基线 80 + Repair-1 新增 3 + Repair-2 新增 1。
* **针对性验证**（与本 WP 门槛对应）：
  * 三条产品 Smoke（D3）：`d3_play_smoke1/2/3b`（3/3、2/2、2/2）；
  * D2 lifecycle：`P_D2_1` / `P_D2_2` / `P_D2_3` / `P_D2_3b`（定向 PlayMode 8/8 内含）；
  * Repair-1：`PA_1`（Battle 卡生命周期 + 表现锁）、`PA_2`（点击前置灰 + 零 mutation + 刷新恢复）、`PA_3`（三时点 Supply 投影）；
  * Repair-2：`PA_4`（Entry 失败不推进）+ EditMode `JourneyPresentationCycleTests` 两条确定性用例；
  * 定向结果：EditMode **17/17**（`e_edit4.xml`）、PlayMode **8/8**（`e_play6.xml`，`error CS = 0`）。
* **环境事实（如实记录）**：Repair-2 之后 Full EditMode 曾**连续 3 次**在「编辑器启动阶段」停滞（无 XML 产出，**非**测试失败，
  与 D2 §21.11.9 登记的同类现象）；按环境处理纪律（清残留 Tuanjie 进程 → 确认工程未被占用 → 重跑）后，
  第 4 次在**最终树**上完整产出 **1013/1013 PASS**（`f_edit.xml`，12:44:07Z → 12:45:19Z）。
* 工作树无测试运行残留（`Assets/InitTestScene*` = **0** ✓）；仓库外运行产物不随 Git 提交。



## I. Temporary / Not Frozen（V0.1 内**不得**当作 Final / Frozen）

| # | 对象 | 当前值 / 现状 | 性质 |
|---|---|---|---|
| I1 | Test EventId | `evt_test_village_temp_draft`（村庄）/ `evt_test_exp_ruined_shrine`（远征） | **Test / Not Frozen** |
| I2 | Test OptionId | `opt_test_village_grant_rest` / `opt_test_village_continue`；`opt_test_exp_leave` / `opt_test_exp_store` / `opt_test_exp_force` | **Test / Not Frozen** |
| I3 | Test OutcomeId | `out_test_exp_leave` / `out_test_exp_store` / `out_test_exp_force` 等 | **Test / Not Frozen** |
| I4 | Test RoleId | `role_test_village_requester` / `role_test_exp_scout` | **Test / Not Frozen** |
| I5 | Test 数值 | `ObservedCount == 3` 阈值；`储备口粮 = +2 Supply +1 day`；Test Event Node 的 `NodeElapsedDays = 0` / `ExtraSupplyCost = 0` | **Test / Not Frozen**（**不是**产品规则） |
| I6 | Test 条件 | Expedition Test Event 的深度下界 = `0`（原 `2`，D2 为可玩性做的最小覆盖调整）；`RepeatPolicy = Repeatable` | **Test / Not Frozen** |
| I7 | Test EncounterPool | `pool_test_general` + `enc_test_basic`（V0.1 Compatibility 映射：节点类别 → Pool） | **Test / Not Frozen** |
| I8 | 插图 | 类别级 placeholder（`battleEncounterIllustration` / `contentEncounterIllustration` / `branchIllustration`）；Event 未配置 `IllustrationAssetId` | **Art Content Pending** |
| I9 | 内容数量 | Event 2 条 / Role 2 条 / Option 6 条 / Outcome 7 条 / Effect 10 条 / Condition 4 条（仅够打通产品链） | **Test 规模** |
| I10 | UI / 诊断文案 | 「遭遇战斗」「前方出现岔路」「前往道路 N」「该选项当前不可选」「补给不足：本次行动至少还需要 N 点补给」等 | **Implementation-level / Not Frozen**（可改文案） |
| I11 | Session Seed / 映射细节 | `sessionSeed`、`LegacyNodeElapsedDaysTestPolicy`、Compatibility 节点类别 → Pool 映射 | **Test / Not Frozen** |
| I12 | ChoiceHistory | **当前不存在**（V0.1 不保存 OptionId / OutcomeId） | 未实现（见 §J4） |

---

## J. Known Non-blocking Debt

1. **Art Content Pending** —— 正式 Event Illustration 尚未生产（`VisualAsset` 表内无 Event 行；卡面显示类别级占位图，无诊断噪音）。
2. **Village Modal Illustration Host** —— Village Modal 当前**不消费** `IllustrationAssetId`（无插图宿主）；若制作人要求，属**新增 UI 结构**任务，
   必须复用同一 `ResolveEventIllustration`。
3. **Event Result Feedback** —— 类似「储备口粮」的 `+2 → 时间 −2`：内部正确，但玩家缺少中间反馈（属 `Event Result` / 内容表现债）。
4. **ChoiceHistory** —— V0.1 **不保存** OptionId / OutcomeId（玩家看不到「我当时选了什么」）。
5. **Unknown Legacy Pending 的 UI 级自动化** —— 目前只有兼容层合同测试（API 级：`VillageSocialResponseSaveTests.LegacyPending_CompatPath_*` /
   `VillageEventFlowContractTests.V11`），缺 UI PlayMode 覆盖。
6. **Legacy「整个 Monster 目录当本场敌人」测试路径** —— 仍有若干 Battle 测试走该模式（当前全绿），属**潜在测试债**（不影响 Event 产品路径）。
7. **正式 Art / Content Production** —— 正式事件数量、文本、美术、平衡**尚未开始批量生产**（见 §M）。

---

## K. File Classification（最终 working tree）

> 依据：`git status --porcelain`（最终树）。**不** reset / **不** discard 任何用户文件。

### A. WP-Event-1 正式交付（代码 / Config / Compiler / Tests / Docs）

**Compiler 侧（`Tools/RRPG.ConfigCompiler/`）**
* 改：`src/RRPG.ConfigCompiler/Core/CompilerRunner.cs`、`Core/DiagnosticCodes.cs`、`Core/EntityRequirements.cs`、`Program.cs`
* 新：`src/RRPG.ConfigCompiler/Core/EventValidator.cs`、`tests/RRPG.ConfigCompiler.Tests/EventDomainTests.cs`

**Unity 产品代码（`UnityProject/RRPG1.0game/Assets/`）**
* 新（Event Core / 产品适配 / 会话状态）：`Scripts/Event/`、`Scripts/EventHost/`、`Scripts/Adventure/ExpeditionEvent/`、`Scripts/Shared/`
* 新（配置 Runtime / 编辑期）：`Scripts/StaticConfig/EventConfigDatabase.cs`、`Scripts/StaticConfig/EventDefinitions.cs`、`Editor/StaticConfig/EventMirrorReader.cs`
* 新（Village D0）：`Scripts/Village/Runtime/VillageDayOrdinal.cs`、`Scripts/Village/Runtime/VillagePersonRestState.cs`
* 改：`Scripts/StaticConfig/RRPGStaticConfigRoot.cs`、`Scripts/StaticConfig/StaticConfigRegistry.cs`、`Scripts/Save/SaveData.cs`、`Scripts/Save/SaveSystem.cs`、
  `Scripts/Adventure/AdventureManager.cs`、`Scripts/Adventure/Progression/Runtime/{ExpeditionProgressionResolver,ExpeditionProgressionResult,ExpeditionSupplyState}.cs`、
  `Scripts/Encounter/DeterministicRandom.cs`、`Scripts/Encounter/Encounter.Runtime.asmdef`、`Scripts/Journey/Runtime/JourneyPresentationCycle.cs`、
  `Scripts/UI/{ExpeditionUI,ExpeditionJourneyView,ExpeditionJourneyCoordinator,JourneyEventCard}.cs`、
  `Scripts/Village/VillageRuntimeBridge.cs`、`Scripts/Village/Runtime/{VillageSocialResponseLog,VillageCommitmentView,VillageSaveData}.cs`、`Scripts/Village/UI/{VillageSocialEventUI,VillageCommitmentUI}.cs`、
  `Editor/StaticConfig/StaticConfigBuilder.cs`

**测试（`Assets/Tests/`）**
* 新：`EditMode/Event/`、`EditMode/EventProduct/`、`EditMode/StaticConfig/EventConfigBuilderTests.cs`、
  `PlayMode/ExpeditionEventProductChainPlayTests.cs`、`PlayMode/Village/VillageEventProductChainPlayTests.cs`、`PlayMode/EnemyRosterTestFixture.cs`
* 改：`EditMode/ExpeditionJourney/JourneyPresentationCycleTests.cs`、`EditMode/Village/VillageSocialResponseSaveTests.cs`、
  `PlayMode/BattleDualBoardProductPlayTests.cs`、`PlayMode/BattleMilestoneAPlayTests.cs`、`PlayMode/Village/VillageSocialEventProductIntegrationPlayTests.cs`、
  `PlayMode/Village/VillageStage1.PlayModeTests.asmdef`、`PlayMode/Village/VillageV3CProductChainPlayTests.cs`、
  `PlayMode/VillageV4/JourneyVisualFoundationPlayTests.cs`、`PlayMode/VillageV4/VillageV4ProgressionClosurePlayTests.cs`

**Config / 生成物 / 文档**
* 新：`Development/Generated/StaticConfig/Event.json` 等 **10** 个 Event Mirror 文件；`Development/Config/RRPG_Events.xlsx`（工作簿结构 + Event 表）
* 改：`Development/Config/README.md`、`Development/Config/RRPG_EnemyPresentation.xlsx`、`Development/Generated/StaticConfig/README.md`、`Development/Generated/StaticConfig/VisualAsset.json`
* 文档：本报告（新）、`Docs/DevelopmentSpecs/2026-09-26_...FDS-V0.1.md`（新）、`Docs/DevelopmentSpecs/README.md`、`Docs/问题清单.md`，
  以及 §10 要求的同步项（`决策记录.md` / `GameDesign/当前设计状态.md` / `项目状态快照.md` / `开发验收交接.md` / `项目日志.md`）

### B. Producer Companion Content（同一 XLSX / 源目录内、与 Event WP 不可拆）

| 文件 | 性质 | 归属 |
|---|---|---|
| `Development/Config/RRPG_Events.xlsx` | **表结构 / Schema / 规则列** = WP-Event-1 正式交付（A）；**Test 事件内容行**（Test Event / Role / Option / Outcome / Effect / Condition / Presentation） = 制作人当前可玩内容 | A + **B**（不可拆：同一工作簿） |
| `Development/Config/RRPG_VisualAssets.xlsx` | 共享视觉资源工作簿（自 M3 Slice 1A 起）；**内容行**（6 条 Enemy 源图）属内容侧 | **B**（结构属 M3 既有交付） |
| `Development/Config/RRPG_EnemyPresentation.xlsx` | M3 既有工作簿（MonsterId → VisualAssetId）；本次为**内容变更** | **B**（M3 交付 + 内容） |
| `Development/SourceAssets/**` | 本次**未新增** Event 源图（Art Content Pending ✓） | 无新增 |

> 说明：上述 XLSX 的**内容行**是制作人当前内容（Test / Not Frozen），**不伪装**为系统代码；其 Schema / 校验规则属 A。

### C. 必须排除（不进 Commit）

* 仓库外运行产物：`D:\BBB\_rrpg_runs\**`（测试 XML / log / 诊断脚本 / 临时 runner；**不随 Git 提交**）；
* `UnityProject/RRPG1.0game/{Library,Temp,Logs,obj}/**`、`Tools/**/{bin,obj}/**`、`Tools/RRPG.ConfigCompiler/.build-state/**`（gitignore / 生成物）；
* 测试运行残留：本次最终工作树内 `Assets/InitTestScene*.unity` = **0**（已核对）；
* Scene / Prefab / ScriptableObject：本次 `git status` **无 Scene 条目** → 不存在「无内容变化的 EOL 噪声」；
* 未发现与 WP-Event-1 无关的用户改动被纳入本清单；若有，**保留**且不 reset / 不 discard。

---

## L. Commit Plan（**仅计划；不执行** `git add` / `commit` / `push`）

**目标分支**：`main`；**HEAD** = `ba59be67757471a4d9a92e54469ae401eef66d5e`（交接生成时截面；实际提交前请复读实时 Git）。

### L1 结论：**优先单 Commit**
理由：WP-Event-1 的 **Config（XLSX / 表结构）↔ Mirror（生成物）↔ Runtime（StaticConfig / Event Core）↔ Save（Persistence DTO）↔ Tests**
高度互相绑定——拆分会导致中间提交**不可构建 / 不可测**（例如只提交 Compiler 校验而无 Mirror / Runtime 读取；或只提交 Runtime 而没有内容行）。

### L2 建议 Commit Message
```text
feat(event): add unified event content foundation V0.1
```
**Body（建议）**：
```text
Unified Event Content Foundation V0.1（WP-Event-1）：
- unified event static config: RRPG_Events.xlsx（Event/Trigger/Role/RoleCondition/Condition/Presentation/Option/OptionCondition/Outcome/Effect）
  + Compiler 校验（含 DAT044 / DAT047）+ Mirror + Unity 只读读取入口
- resolver / role binding / conditions: EventResolver / ResolvedEventInstance（确定性：TriggerKey / ResolutionSeed）
- option / outcome / effects: EventOptionEvaluator + EventOptionExecutor（Preflight → Commit → History → Completed；exactly-once）
- village cutover: VillageEventFlow / EventPresentation 正式模式 / 旧档 Legacy Pending 收敛迁移
- expedition cutover: Event Node 原子进入 + ActiveExpeditionEventState + Node Settlement exactly-once
- event battle: StartBattle Preflight/Commit 同一份 EnemyRoster；Victory/Defeat 结算同一 Event Node
- persistence: Event World History（长期）+ Expedition Session History（Runtime-only）+ Save DTO 扩展
- presentation authority: EventPresentation → 共享 EventTemplateRenderer（两表面共用）+ IllustrationAssetId → 共享 VisualAsset
  + Fail Closed；Presentation progression follows Product progression（Entry 失败不推进）
- producer acceptance: Producer Play Acceptance PASS（Issue A/B/C/D + Repair 2）
```
（最终以**实际文件依赖审计**为准；如需按层拆分，需保证每个中间提交都可编译 + 定向测试可跑。）

### L3 提交范围
* **包含**：§K-A 全部 + §K-B（内容行与其工作簿**不可拆**，随工作簿一并提交）；
* **不包含**：§K-C（运行产物 / Library / Temp / .build-state / 无内容变化噪声）；
* **注意**：新增目录必须连同 `.meta` 一并加入（Unity 资产标识）；`Development/Generated/StaticConfig/Event*.json` 虽为生成物，
  但与仓库既有 Mirror 一致**受版本控制**（随内容一起提交）。

### L4 前置条件（全部满足后才可执行）
1. §H 最终验证 **全部 FAILED = 0 / `error CS = 0`**；
2. 工作树按 §K 分类完成，且无 §C 类内容；
3. **制作人明确授权** Commit（本 Closeout **不**自行执行；亦不 Push）。→ **已于 2026-09-26 获明确授权并执行完毕**（见 §N）。

---

## M. Next Product Step（**建议**，不构成新 WP 授权）

WP-Event-1 关闭后，**不要自动继续 Event 功能**。协调层当前产品方向建议回到：

> **First-pass Formal Content / Art Production**

建议范围：

1. **正式 Event 内容批量生产**：正式 EventId / Role / Condition / Option / Outcome / Effect 与文本（含「获得补给但耽搁时间」这类**结果反馈表意**）；
2. **Event Illustration**：生产 `Development/SourceAssets/Event/*.png` → `RRPG_VisualAssets.xlsx` 增行 → 各事件 `EventPresentation.IllustrationAssetId` 指向；
3. **Village / Journey 第一版正式美术**（含 Village Modal 是否引入插图宿主的决定）；
4. **Person Visual 生产方案**（正式外观键 → 资源批量生产与绑定流程）；
5. 之后进入**真实产品体验验证**（首个正式内容切片走完整 Play Acceptance）。

> 明确：以上**不**是本 WP 的授权范围，也不自动开工；需要新的任务授权与现场审计。

---

## N. Final Commit / Push Record（2026-09-26，**已执行**）

> 触发：制作人明确授权「重试推送」。§L4 前置条件全部满足——§H 最终验证全绿、§K 分类完成且无 §C 内容、制作人明确授权。

| 步骤 | 真实结果 |
|---|---|
| 提交前 Gate（重跑） | branch = `main`；HEAD = `ba59be67757471a4d9a92e54469ae401eef66d5e`；`git fetch origin` **EXIT 0**；`git ls-remote origin refs/heads/main` = `ba59be67…` → **无远端漂移**；staged = 0；`MM/AM/MA` = 0；`Assets/InitTestScene*` = 0 |
| Staging | 按 §K-A + §K-B 全量暂存（`git add -A`：§C 内容全部在仓库外 / 被 gitignore 覆盖，且核对确认无 D / R 条目）；暂存后 `git status --porcelain` = **357 条**（46 M + 311 A；untracked 0、unstaged 0） |
| 分类核对 | `Development/` = **16**（= §K-A 期望：2 新 XLSX + 10 Event Mirror + 4 改）；`Tools/` = **6**（4 改 + 2 新）；`UnityProject/` = 326；扩展名仅 `.cs 175 / .meta 151 / .json 11 / .asmdef 6 / .md 5 / .xlsx 3`（**无** Scene / Prefab / `.asset` / 图片 / 二进制）；无越界顶层目录；**EOL-only 变更 = 0**（`core.autocrlf=false`；`.gitattributes` 仅对 `Development/Generated/StaticConfig/*.json` 强制 LF） |
| Commit | `edaa4244b36b4da6ff5272f9716fb1e42a8ae935`（短号 `edaa424`）；`parent` = `ba59be67…`；message = `feat(event): add unified event content foundation V0.1` + §L2 九条 body（原文，UTF-8 无损；已用 `git cat-file commit HEAD` 原始字节复核）；`357 files changed, 30512 insertions(+), 568 deletions(-)` |
| Push | `git push origin main` → `ba59be6..edaa424  main -> main`，**EXIT 0**（fast-forward；**无** merge / rebase / force push / stash / reset / clean / checkout） |
| Push 后核对 | `git ls-remote origin refs/heads/main` = `edaa4244b36b4da6ff5272f9716fb1e42a8ae935` = 本地 HEAD（**一致**）；ahead / behind = **0 / 0**；`## main...origin/main`；工作树 `git status --porcelain` = **0**（clean） |
| 环境事实（如实记录） | 本环境 git 首次尝试时走本地代理 `127.0.0.1`（TCP 443 不可达）→ `git fetch` / `git ls-remote` EXIT 128；当时按规则停在 `Ready for Commit Authorization` 且**零** Git 写操作。网络恢复后从 Gate 重跑并完成全部 Git 写操作。 |
| 提交范围事实修正 | §A「工作树规模（生成时）」的「46 修改 + 46 未跟踪**条目**」是 `git status` 把**新目录折叠为一行**的结果；实际提交路径数 = **357**（新增目录内文件连同 `.meta` 逐个入库），与 §K-A / §K-B 清单逐项一致。 |
| 格式说明 | `git diff --cached --check` = 911 行，全部来自 Unity `.meta` 的 `userData: `（尾空格）既有约定（基线 HEAD 已有 **692** 个 `.meta` 如此）与新文件 EOF 空行 → **不是**本 WP 新增格式债，亦不违反 §K-C（EOL-only = 0）。 |

---

> 本节与 Final State 的 `CLOSED` 标记、以及 `DevelopmentSpecs/README.md` / `项目状态快照` / `开发验收交接`（Handoff v48）/ `问题清单` / `项目日志` / `Development/Config README` 的同步，随一个**文档专用**后续 Commit 落库（不含代码 / 测试 / Config / Mirror 内容改动），以免关闭状态停留在未提交工作树。→ **实际落库**：Commit `6f32ace231ed557e5282f66ab6fb869a8fb0acc2`（`docs(event): mark WP-Event-1 CLOSED`，7 files / +69 / −35），已 Push `origin/main`。

## Final State

> **`WP-Event-1 CLOSED`**（2026-09-26）

（旧口径「**未** Commit / **未** Push；等待制作人 Commit 授权、不使用 `CLOSED` 字样」为收口生成时截面；现已按 §N 完成 Git 写操作并关闭。）

- **Commit**：`edaa4244b36b4da6ff5272f9716fb1e42a8ae935` — `feat(event): add unified event content foundation V0.1`（357 files / +30512 / −568；parent `ba59be67…`）
- **Push**：`origin/main`（`github.com/LINQI2/RRPG1.0W`）— `ba59be6..edaa424  main -> main`；Push 后 `git ls-remote` 与本地 HEAD 一致、ahead / behind = `0 / 0`、工作树 clean
- **验证（最终工作树）**：Compiler **208/208**；validate **PASS**（errors=0 / warnings=5）；Full EditMode **1013/1013**；Full PlayMode **84/84**；两处 `error CS = 0`
- **制作人验收**：**Producer Play Acceptance PASS（2026-09-26）**，范围仅限本 WP V0.1
- **边界不变**：Temporary / Not Frozen（§I）、Known Non-blocking Debt（§J）、Next Product Step（§M，**不**自动开工）
