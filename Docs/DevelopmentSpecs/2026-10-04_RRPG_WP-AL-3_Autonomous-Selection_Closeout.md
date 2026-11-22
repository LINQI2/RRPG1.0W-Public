# WP-AL-3 Autonomous Selection & Arbitration Closeout

> 状态：Executed / Verified / Runtime Review PASS / Integrity Repair Review PASS / Awaiting Main Publication。
> 日期：2026-10-04。当前实现位于 review/wp-al-3；main 仍为 9c9a750999bcfcb04e9f01c9e2a0d1385f5c67c6，未合入。
> 职责：记录已完成实现、验证、真实 Commit Review 与交付边界；不新增 Product Rule、Freeze 或顶层 Architecture Authority。
> Current 技术职责见[技术架构总览](../Architecture/技术架构总览.md#wp-al-3-current)与[对象映射](../Architecture/核心对象与术语映射.md#wp-al-3-objects)。开发期契约见[Development Spec V0.1](2026-10-04_RRPG_WP-AL-3_Autonomous-Selection_Development-Spec-V0.1.md)，产品证据见[Final Checkpoint](../DesignCheckpoints/2026-10-04_RRPG_WP-AL-3_Autonomous-Selection_Design-Checkpoint-V0.2-Final.md)。

## 1. 基线、提交与 Review

| 阶段 | 真实 Commit / 结论 |
|---|---|
| main 代码基线 | 9c9a750999bcfcb04e9f01c9e2a0d1385f5c67c6 |
| Authority Landing | 1bbb67f5b49e617e92afb5240cadea4df6638c3d；Authority Landing Review PASS |
| Runtime Implementation | e2dd368275db627c873d328c3652e0ce723404d6；56文件，+4015 / −63 |
| Integrity Repair | 0a87394d4f9cfc73c78bd44537c6fee70b7000e4；11文件，+270 / −106 |
| 最终真实 Commit Review | 开发15.0已直接审阅GitHub上述实现及修复；Runtime Review PASS、Integrity Repair Review PASS，制作人明确确认 |
| Publication | Awaiting Main Publication；本轮仅文档Commit / Push到同一review分支，不merge / push main |

WP-AL-1 Social与WP-AL-2 Activity已经发布main。WP-AL-3的Current指已真实实现且通过Review的技术能力，不表示已经发布main；完整Autonomous Life的其它Target能力不随此包转为Current。日期化Spec首页保留开发前语境，以当前导航、Current Architecture及本报告恢复实施状态。

## 2. 最终模块与实际范围

| 层 | 实际实现 / 文件 | 所有权与依赖 |
|---|---|---|
| 纯Selection域 | [AutonomousSelection.Runtime.asmdef](../../UnityProject/RRPG1.0game/Assets/Scripts/AutonomousLife/Selection/Runtime/AutonomousSelection.Runtime.asmdef)、DecisionInputs.cs、SelectionCatalog.cs、DesireEvaluation.cs、DecisionPlanning.cs、DecisionWorld.cs | 仅引用AutonomousActivity.Runtime与RRPG.Deterministic.Runtime；noEngineReferences=true，不依赖mutable Social / Village / Person / Event / UI / UnityEngine |
| Host / Snapshot Projection | [AutonomousSelectionRuntimeBridge.cs](../../UnityProject/RRPG1.0game/Assets/Scripts/AutonomousLifeHost/AutonomousSelectionRuntimeBridge.cs)，含AutonomousSelectionConfigAdapter | Assembly-CSharp；从现有SimulationPersonSource、Social只读查询、Activity最近状态与静态配置复制唯一Snapshot |
| Config Runtime | [AutonomousLifeConfigDatabase.cs](../../UnityProject/RRPG1.0game/Assets/Scripts/StaticConfig/AutonomousLifeConfigDatabase.cs)、RRPGStaticConfigRoot、StaticConfigRegistry | StaticConfig.Runtime，生成聚合数据库 / 查询接缝，不存Runtime人物状态 |
| Compiler / Builder | AutonomousLifeValidator、CompilerRunner；AutonomousLifeMirrorReader、StaticConfigBuilder | 复用既有authoring / build流水线，没有第二套配置系统 |
| Save / 日结 | SaveData、SaveSystem、VillageRuntimeBridge | additive DecisionWorld区块；唯一HandleDayTick接线；不改Return顺序或建立新Clock |
| 正式回归 | SelectionTestData、SelectionMechanismTests、SelectionIntegrationTestKit、SelectionIntegrationTests；AutonomousLifeConfigTests | Gate A–H、真实Save / Village集成、配置及因果 / Headless经验回归 |
| Prototype内容 | [RRPG_AutonomousLife.xlsx](../../Development/Config/RRPG_AutonomousLife.xlsx)与12个Compiler JSON | Test / Prototype / Calibration，不改正式Person定义、建筑或产品数值 |

Runtime Commit包括18个新Unity meta；标准空字段54个尾随ASCII空格在提交前清理，GUID / importer / 字段结构 / 编码 / 换行未改变。Integrity Repair没有新增资产 / meta。正常生成的Unity managed assets沿既有Builder治理，不人工编辑Generated；临时authoring、formatter与MCP experience runner不进入仓库。

## 3. Current 决策链

```text
WP-AL-2 ActivityDailyWindow
→ Host构造一次DecisionSnapshot
→ Prototype Candidate Generation（Possibility）
→ Actor / Participant / Daily Participation检查（Eligibility）
→ Additive Desire Evaluation + Contribution Breakdown
→ Plausibility Cut
→ Deterministic Weighted Candidate Order（含NoMajor）
→ Proposal / Minimal Acceptance
→ Round-based Fair Arbitration / 原candidate pointer fallback
→ WP-AL-2 TryCommitActivity → TryBeginExecution → TryComplete
→ AutonomousDecisionWorld标记当日Resolved
```

- DecisionSnapshot及其人物、profile、preference、relationship、context输入为防御性复制的只读数据。Evaluation只消费Snapshot，不持续查询mutable世界。PersonId始终是Runtime identity；PersonDefinitionId只作静态配置provenance。
- ResolvedBehaviorProfile missing channel返回其定义的Neutral；SpecificPreferenceView missing key返回0，读取不创建记录。Prototype画像不写回Person核心Authority。
- Possibility、Eligibility、Plausibility独立：Provider产生可表达候选；窗口与当日参与状态判断资格 / 可用性；加法Desire和Threshold判断合理性。Contribution按Behavior、Preference、Relationship、Context、Repetition分家族输出Breakdown，不整本读取History。
- WeightedCandidateOrder使用现有DeterministicRandom.StableHash64，为round / actor / candidate构造独立selection identity。序列单次生成，稳定tie-break，不随输入集合顺序改变。Proposal的arbitration domain独立，FairArbitration不读取Utility；按稳定随机priority分轮处理冲突，不以固定Person更新顺序裁决，不做Global Max-Utility Matching。
- Reject / ConflictLoss只前进原pointer，不reroll。MinimalAcceptance提供正式IParticipantAcceptance接缝，当前只检查最小合法性 / 参与可用性；richer Target Acceptance Utility未实现。
- NoMajorActivityOption实际由SelectionOption.Candidate=null / IsNoMajor表达，是Synthetic Decision Option，不创建Activity、Claim、Completion或成功参与记录。人物不主动发起仍可作为他人活动参与者；整轮NoMajor同样写daily exactly-once标记。
- PrototypeActivityProvider / IDecisionCandidateProvider属于Selection候选生成，复用WP-AL-2 ActivityCandidate；WP-AL-2仍拥有Window / Claim / FSM / successful Daily Participation，Selection不另建Authority。
- DecisionPlan / ActorDecisionTrace记录候选、Eligibility / Plausibility、分家族贡献、Weight / RandomIdentity / OrderKey、提案轮次与规划结果；DecisionRoundResult.Executions另记录真实Commit / Begin / Complete阶段结果和成功Completion。规划结果不冒充实际执行成功。

## 4. Host、Daily Integration与Headless边界

VillageTime仍是唯一时间Authority。现有日链：
State → capture occupiedAtStartOfDay → Projects / completion → Assignments（同一快照）→ Resource Settlement → Formation → WP-AL-2 OpenDailyWindow → WP-AL-3 RunDailyPhase。

direct Tick / Update / Jump Month / Return Catch-up共用这条HandleDayTick路径。日初Project当天完成仍阻止活动、次日恢复；Primary Assignment / PersonRest不阻止；Expedition / Unavailable / Dead / 日初Active Project阻止。

SimulationPersonSource=null保留WP-AL-2旧Window并明确MissingPersonSource；Selection不消费旧窗口、不猜第二份人物来源。Coordinator还验证currentDay / 有效Daily Phase / Window同日 / Snapshot同一Window引用 / AlreadyResolved，防stale及same-day replay。已绑定空世界可以resolve真实空轮并记录防重。

全部Provider / Acceptance / Snapshot规划在任何Activity mutation前完成；规划异常返回PlanningFailure，不逃逸到Village Day Tick，也不写resolved标记。执行仅通过WP-AL-2 Gateway：Commit → Begin → Complete，失败时通过既有Fail清Claim；记录失败、继续其它互不冲突赢家，不reroll、不回滚先前成功。所有已规划赢家处理完后整轮标记Resolved；个别执行失败不触发同日自动重试。保证是本轮内存流程边界，不扩写为跨进程或全世界事务。

Headless不需要Scene / GameObject、导航、动画或正式World Presentation；Completion目前不生成Social Fact、Relationship Mutation、Event / Recognition / Life Episode。测试HelpNeed仅用显式Test Context Fixture，不建立正式World State。

## 5. Public API与Save Schema

主要只读 / 编排API：DecisionSnapshot、ResolvedBehaviorProfile.Query、SpecificPreferenceView.Query、SelectionCatalog、ActivityDesireProfile、DesireEvaluator.Evaluate、WeightedCandidateOrder.Build、IParticipantAcceptance、DecisionPlanner.Plan、FairArbitration.Resolve、DecisionCoordinator.Run及IActivityExecutionGateway。

Host提供RunDailyPhase、BuildSaveData、Restore、ResetForNewGame / ResetForTests；PlannerForTests与ContextFixtureForTests是测试接缝。AutonomousSelectionConfigAdapter负责配置到Snapshot / Catalog的投影，不成为Person Identity Authority。

SaveData.AutonomousDecisionWorld仅保存：

| 字段 | 含义 |
|---|---|
| HasLastResolvedDay | 是否已有完成Decision Round的日期 |
| LastResolvedYear | 最近Resolved日期年 |
| LastResolvedMonth | 最近Resolved日期月 |
| LastResolvedDay | 最近Resolved日期日 |

SaveVersion=1。所有Save入口经统一Build捕获DecisionWorld；不保存Candidate Order、Utility Trace、Proposal、Arbitration、RNG、Window、Claim或Activity Instance。已记录同日 / 更早日期阻止Replay；这不是跨域数据库式Exactly-Once。

Restore为Replacement：null / legacy missing block / 未有Resolved日 → empty，valid block字面替换；不重新生成候选、reopen Window或replay每日Phase。Player Restore成功后才实际恢复DecisionWorld；failed Load保留原DecisionWorld；NewGame显式reset。

## 6. Integrity Repair — 已Review PASS

- 原Persistence先清LastResolvedDay再验证非法日期，失败会丢失防重状态；修复共用只读TryReadDay / IsValid，成功后才写日期。invalid Restore返回false并保留旧状态；Bridge也保留原LastResult。
- SaveSystem在任何World Restore前预检AutonomousDecisionWorld，非法区块明确Load失败、返回null并诊断；后续真实Restore仍消费失败结果。未借此重构Village / Event世界级Load transaction。
- InjuryLight / InjuryHeavy / InjuryDisabled三个Prototype Adapter Calibration key由Compiler要求存在且finite / 0..1；Builder的MirrorReader及Host BuildCatalog共用StaticConfig完整性检查，在受伤Person读取前拒绝缺失 / 重复 / 非法值。数值继续来自工作簿，不硬编码到Selection算法。

## 7. 最终验证与经验观察

| 验证 | 最终真实结果 |
|---|---|
| Unity Compile | 0 errors |
| Selection targeted EditMode / Gate A–H | 96/96 PASS（原81加15个Integrity回归） |
| Compiler full tests | 228/228 PASS（原222加6） |
| Full EditMode | 1277/1277 PASS，0 regression |
| Full PlayMode | 93/93 PASS |
| Config / Generated chain | 工作簿OpenXML有效；现有Compiler真实生成12JSON，build / validate / reproducibility回归PASS；Unity Builder成功，重复Build no-op |
| Semantic Self-Review | 原Runtime与Integrity Repair逐读源码PASS；不以测试结果替代源码检查 |
| Git hygiene | Runtime与Repair的git diff --check / cached check / Commit check均PASS；本轮文档另做链接 / 锚点 / diff / scope检查 |
| Non-document保护 | Scene / Prefab未改，制作人dirty资源及旧Review附件哈希不变、未提交；测试前存档已恢复 |

本轮文档收口引用上述已执行证据，不重新跑Runtime / Tests / Compiler。MCP在域重载 / 模式切换后曾暂报No tests found，Editor ready后真实执行取得最终PASS，不隐去工具发现过程，也不记作产品缺陷。

八类因果检查均有真实专项：Person Difference、Relationship Difference、Context Override、Repetition Control、Conflict Arbitration、NoMajor fallback、同输入deterministic order、Trace explainability。真实配置20日经验场景中王五Drink更多，存在Conflict和NoMajor；持续Injury / HelpNeed场景覆盖全部5活动（VisitSick4、Help9）。稀疏伤情的4个观察日未选中VisitSick，保留Calibration观察，不为测试绿反向调参。这是Headless原型验证，不宣称正式产品数值或视觉体验已冻结。

制作人已有RRPG_Events.xlsx增量不纳入本轮Authoring生成；仅在隔离临时authoring root用同一Compiler编译新工作簿，带回12个实际Compiler产物，原Event JSON不变。不声称该dirty Events工作簿与旧镜像已同步；不存在第二套配置系统或人工编辑正式Generated。

## 8. Calibration / Prototype — Not Frozen

Current Runtime支持以下配置，并不使具体值成为Frozen Product Rule：

- Behavior具体值、Weak / Medium / Strong映射、Family Budgets、Threshold、Weight Curve参数、NoMajor Weight、RelationshipReferenceMagnitude。
- InjuryLight / InjuryHeavy / InjuryDisabled数值；输入完整性 / 有限性 / Context范围校验不等于正式伤病玩法冻结。
- 张三 .8/.8、李四 .5/.5、王五 .8/.2及leisure.drink +1、赵六 .2/.8测试画像；这些prototype definition keys不替代Runtime PersonId、不改正式人物名册。
- Home / Tavern / Workshop只是3个Test Place；Rest / Drink / Talk / VisitSick / Help及其Base Desire、绑定强度等为Prototype Content，不是正式建筑或活动平衡。

## 9. Target / Deferred与遗留风险

完整Personality / Trait Runtime、Behavior / Preference Formation、Personality Evolution、Needs / Mood / Habit、更多正式Behavior Channels、richer Target Acceptance Utility、Negotiation / Invitation、reciprocal proposal coalescing、global optimization / maximum matching、RepeatStreak / recent-N history、Life History直接参与每次Utility仍未实现。最小重复贡献只读取最近主动发起类型，不提前冻结额外历史窗口。

Activity → Social Fact、Relationship Feedback、Production Relationship Interpreter、Event / Recognition、Life Episode Runtime、Navigation / Animation / Village World Presentation继续Target / Deferred。当前Social只读关系快照参与欲望计算，不产生Feedback。

继续保留[问题清单边界](../问题清单.md#wp-al-1-boundaries)：Production Relationship Interpreter ownership须在首个真实Social Producer前裁决；Village / Event全世界统一Load transaction是既有债务，未解决。semantic-only Target未来扩展仍保留；stale-window与Day Tick fail-closed接入条件已在本WP实现 / 验证 / Review PASS，见[生命周期更新](../问题清单.md#wp-al-3-integrity)。

本轮仅Closeout / Current Architecture / 状态导航归位；没有新的产品或架构裁决冲突。不merge main，不启动WP-AL-4。
