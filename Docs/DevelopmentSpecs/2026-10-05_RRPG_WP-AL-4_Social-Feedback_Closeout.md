# WP-AL-4 Social Feedback & Historical Reciprocity Closeout

> 状态：Executed / Verified / Runtime Review PASS / Awaiting Main Publication。
> 日期：2026-10-05；当前实现位于review/wp-al-4，main仍为7fcffe5b28be9156a45b95c3981b53ec8b9cac9b，尚未合入。
> 职责：保存实际实现、验证、GitHub真实Commit Review与现场边界，不新增Product Rule / Freeze或顶层Architecture Authority。
> Current职责见[技术架构总览§26](../Architecture/技术架构总览.md#wp-al-4-current)与[对象映射§21](../Architecture/核心对象与术语映射.md#wp-al-4-objects)；开发期Binding见[Spec](2026-10-04_RRPG_WP-AL-4_Social-Feedback_Development-Spec-V0.1.md)，产品规则见[Village §12.6.1](../../GameSpec/Village/Village-GameSpec-V0.1.md#wp-al-4-feedback)，讨论证据见[Final Checkpoint](../DesignCheckpoints/2026-10-04_RRPG_WP-AL-4_Social-Feedback_Design-Checkpoint-V0.1-Final.md)。

## 1. 基线、真实提交与Review

| 阶段 | 真实事实 |
|---|---|
| main代码基线 | 7fcffe5b28be9156a45b95c3981b53ec8b9cac9b |
| Authority Landing | e1beced596716405c84653c2633b6059cb0e4ecf；Authority Landing Review PASS |
| Runtime | 70a65341c5ee3a1f26e9c90b75f4aa0e155b10bd；49文件，+1416 / −62 |
| Runtime Review | 开发15.0直接审阅GitHub真实Runtime Commit；Architecture / Semantic Review PASS，制作人已明确确认 |
| 当前发布边界 | WP-AL-1/2/3已Published Main；WP-AL-4已实现并通过Review，Awaiting Main Publication；本轮只文档Commit / Push同review/wp-al-4，不merge / push main |

Current Implementation表示已真实实现并通过Review，不表示全部Autonomous Life Target已完成，也不表示已发布main。日期化Checkpoint / Spec与历史日志中的开发前语境保留；本次文档提交自身SHA查Git，不预写未来值。

## 2. 实际实现范围与模块依赖

| 层 | 实际对象 / 文件 | 所有权与边界 |
|---|---|---|
| 纯integration域 | [AutonomousSocialFeedback.Runtime.asmdef](../../UnityProject/RRPG1.0game/Assets/Scripts/AutonomousLife/SocialFeedback/Runtime/AutonomousSocialFeedback.Runtime.asmdef)、ActivitySocialFactProjector、SocialFeedbackWorld、SocialFeedbackWorldPersistence | references=[AutonomousActivity.Runtime, Social.Runtime]；noEngineReferences=true，无UnityEngine；Activity与Social不直接互相依赖 |
| Feedback Host | [AutonomousSocialFeedbackRuntimeBridge.cs](../../UnityProject/RRPG1.0game/Assets/Scripts/AutonomousLifeHost/AutonomousSocialFeedbackRuntimeBridge.cs) | Assembly-CSharp；编排Prepare / Complete / Enqueue / Drain，拥有Outbox世界，不接管Activity FSM或Social Ledger |
| Social规则 | [ConfiguredRelationshipInterpreter.cs](../../UnityProject/RRPG1.0game/Assets/Scripts/Social/Runtime/ConfiguredRelationshipInterpreter.cs)、SocialRuleCatalog、SocialRuntimeBridge | Social单一拥有Production Interpreter；Social.Runtime仍references=[]、noEngineReferences=true |
| Config | AutonomousLifeConfigDatabase、SocialConfigDatabase、Root / Registry、AutonomousLifeMirrorReader、SocialMirrorReader、StaticConfigBuilder | 复用现有StaticConfig authoring / generated / registry，没有第二套配置系统 |
| Compiler | AutonomousLifeValidator / SocialValidator / CompilerRunner、对应Compiler tests | 同一Compiler执行跨表规则 / 引用 / Calibration完整性验证 |
| Save / Daily | SaveData、SaveSystem、VillageRuntimeBridge、AutonomousSelectionRuntimeBridge | nullable Feedback Save block与pre-decision Barrier；不改VillageTime / Return顺序或Selection算法 |
| Tests | SocialFeedbackDomainTests、SocialFeedbackIntegrationTests；SocialSaveIntegrationTests生产入口迁移 | Gate A–H、真实Save / Load、日结与Historical Reciprocity、30日Headless闭环 |

Runtime Commit完整文件清单由上述真实Commit记录，不复制源码作为第二份实现Authority。14个新增Unity meta随对应新资产提交；标准空字段42处尾随空格清理未改GUID / importer / 字段结构 / 编码 / 换行。Unity generated managed资产由Builder正常生成，不人工编辑，不把忽略资产纳入Commit。

## 3. Current因果链与准确执行时序

正式因果链为Completed Activity的发生语义，经prepared semantic payload、Durable Outbox、Social-owned Interpreter，形成Canonical Relational Fact与Target→Actor Relationship Mutation；Save / Load保留后，下一次Decision Snapshot消费当前Relationship，改变future behavior evaluation。

实际执行顺序必须区分因果描述与代码时序：

```text
Executing Activity
→ Prepare Projection（mapping / FactType / Actor / Target / rule / outbox conflict完整验证）
→ WP-AL-2 TryComplete success
→ Enqueue已验证的Semantic Fact payload
→ immediate Drain
→ Social-owned Interpreter
→ Canonical Relational Fact + Target→Actor Relationship Mutation
→ Save / Load
→ pre-next-decision Settlement Barrier
→ next Decision Snapshot
→ future behavior changes
```

Prepare在Complete之前；不能Complete后才发现投影配置非法。NoFeedback是合法路径，不调用Social。Complete后的Delivery失败仍保留Activity Completed，Feedback Pending / Faulted保留，当前Round可完成；下一次Snapshot之前由Barriersettle或fail closed。

ActivityCompletionSnapshot是ephemeral occurrence / projection input；RelationalFactRecord是canonical persistent fact；Significant / LifeEpisode未来是derived downstream projection。没有建立三套Fact Ledger。

## 4. Prototype Policy、Production API与Outbox

| Activity | Persistent relational fact |
|---|---|
| Rest / Drink / Talk | none |
| VisitSick | relational.visited_sick_person |
| Help | relational.helped_person |

Failed / Interrupted / NoMajor没有成功关系事实。Fact表达发生语义，不表达IncreaseAffinity或delta。SourceOccurrenceId稳定为`activity:<ActivityInstanceId>`，不使用Guid / wall clock / Unity Random / string.GetHashCode。

生产API为`SocialRuntimeBridge.CommitRelationalFact(fact)`；Social选用ConfiguredRelationshipInterpreter与配置化SocialRuleCatalog。Production caller不选择长期关系解释语义；低层SocialCommitService仍保留pure-domain / test seam注入。Interpreter只获只读Query Surface，不直接写Registry / Ledger。

VisitSick / Help当前解释为**Fact.Target→Fact.Actor**，当前只有Affinity channel，不自动生成对称Mutation。Weak / Medium / Strong=4/8/12、VisitSick / Help=Positive Medium、所有Affinity delta均**Prototype / Calibration / Not Frozen**；Runtime支持不提升为正式平衡，不实现current-affinity curve / saturation / diminishing return。

Outbox保存已经投影完成的不可变稳定payload，不保存Completion供未来重新解释。same occurrence / same payload幂等；不同payload显式Conflict。Drain按occurrence ordinal顺序：

| SocialCommitResult | Outbox结果 |
|---|---|
| Success / Duplicate | ack并删除；没有Delivered History |
| CommitInProgress | 保持Pending |
| SourceOccurrenceConflict / InvalidFact / InvalidMutation / DuplicatePairMutation / InterpreterFailure | Faulted + FaultCode；不自动无限retry |

FeedbackDrainResult / FeedbackDeliveryTrace保留结构化诊断；异常contained，不逃逸到DayTick。Outbox与Social occurrence幂等提交构成**effectively-exactly-once feedback**，不宣称跨域global 2PC、崩溃恢复UI或新的Canonical Ledger。

## 5. Config authoring与Generated链

- [RRPG_AutonomousLife.xlsx](../../Development/Config/RRPG_AutonomousLife.xlsx)新增ActivitySocialProjection，只表达ActivityType→PersistentRelationalFactType，无Relationship delta。
- [RRPG_Social.xlsx](../../Development/Config/RRPG_Social.xlsx)负责RelationalFactType、RelationshipInterpretationRule、SocialCalibration；Rule表达FactType / SourceRole / TargetRole / Channel / Polarity / Strength。
- 现有XLSX→RRPG.ConfigCompiler→Generated JSON→Unity Builder→StaticConfigRegistry已接入，生成4份新增JSON；原12份Autonomous JSON未变化。
- 生成使用隔离的两份授权工作簿，未读取制作人dirty RRPG_Events.xlsx。Config build / validate通过，生成可复现；真实Builder重复为no-op。
- Workbook导出 / OpenXML / Compiler验证通过；Artifact预览渲染不可用，不宣称视觉预览PASS。配置值仍Calibration，不成为新Product Authority。

## 6. Save Schema与Restore边界

顶层`SaveData.SocialFeedbackWorld.Outbox[]`字段：

```text
SourceOccurrenceId, FactType, ActorPersonId, TargetPersonId
Year, Month, Day
Status (Pending / Faulted), FaultCode
```

SaveVersion保持1；SocialWorld仍拥有Canonical Facts + Relationships。Feedback Save不保存Window / Candidate / Claim / Instance / Completion / Interpreter / Delivered History。

Build深拷贝并按occurrence稳定排序；SaveSystem在任何World Restore前preflight Feedback DTO，非法payload / 状态 / duplicate occurrence明确失败且保留旧Feedback World。valid为Replacement，null / legacy missing为空；Player Restore成功后才Restore Feedback；Load不自动Drain / replay，NewGame显式清空。

该安全模式不解决Village / Event既有世界级统一Load transaction技术债，也不把各Host局部保证扩展为全世界rollback。

## 7. Daily Integration与Historical Reciprocity真实证据

唯一VillageTime.TickDay→HandleDayTick链：State→日初占用快照→Projects→Assignments→Resource→Formation→Feedback Drain Barrier→Open Activity Opportunity→Selection。Barrier未settled不打开新的Window、不构建新Decision Snapshot；诊断SocialFeedbackUnsettled。Selection Host也在Snapshot前检查，防独立入口绕过。

direct Tick / Update / Jump Month / Return Catch-up共用该链，未新增Clock / Catch-up simulator，未改变Return顺序或replay过去日期。

真实Control Worlds E2E：World A无历史反馈；World B张三VisitSick王五→Canonical Fact→王五→张三Affinity增加→真实Save / Load保留。随后在其它输入一致、张三NeedsHelp的Context下，王五Help张三的Relationship Contribution B>A、Desirability B>A、Selection Weight B>A。

不要求weighted order必将Help排第一。保持同Relationship但去除raw Facts时，评价结果不变：raw Fact不进入WP-AL-3 Utility，不能与Relationship double-count。另有真实Selection→WP-AL-2 FSM→Feedback的30日Headless测试及NoMajor无反馈覆盖。

## 8. 最终验证（Runtime阶段证据，本轮不重跑）

| 检查 | 最终结果 |
|---|---|
| Compile | 0 errors |
| Targeted Social Feedback / Gate A–H | 72/72 PASS |
| Compiler | 248/248 PASS |
| Full EditMode | 1349/1349 PASS |
| Full PlayMode | 93/93 PASS |
| Config generation | PASS；build / validate 0 errors，可复现 |
| Builder repeat | no-op |
| Runtime semantic self-review | PASS；随后开发15.0真实Commit Architecture / Semantic Review PASS |
| git diff --check / cached check | Runtime提交前PASS；本轮文档检查另在文档提交前执行 |
| Scene / Prefab | unchanged |

本轮仅文档Closeout，不运行Unity Test Runner / Builder，不把旧证据冒充本轮重跑结果。

## 9. Workspace Integrity Warning与Deferred

**已知例外**：制作人原有未提交`UnityProject/RRPG1.0game/Assets/Art/Fonts/SourceHanSansCN/RRPG_SourceHanSansCN_Regular_TMP.asset`在最终回归后hash与开工记录不同；未进入Runtime Commit。当前没有制作人原始本地副本可精确恢复，不能使用HEAD覆盖、不能猜测恢复、不能stage / commit；本轮不运行可能继续触碰该资产的Unity测试。

Runtime阶段33项保护 / Scene / Prefab检查中32项不变，TMP为唯一已知workspace integrity exception；额外86个既有Growth资源不变。准确结论是**受保护资源除该已知TMP exception外均核验不变**，不是全部protected unchanged。该异常不是WP-AL-4 Runtime Semantic Failure，字体内容修复不在本轮范围；本轮另核对任务前后非文档哈希保持不变。

Recognition Runtime、Event Integration、LifeEpisode Runtime / persistence、History UI、Character Knowledge、Witness / Rumor、Significance Algorithm、richer Relationship channels、current-affinity curves / saturation / diminishing return、Production Fault Recovery UI与Village / Event世界级Load transaction继续Target / Deferred。Personality / Formation等既有后置范围不变，Prototype / Calibration仍Not Frozen。

Production Relationship Interpreter ownership已Implemented / Verified / Review PASS，历史生命周期保留在[问题清单](../问题清单.md#wp-al-4-ownership)。WP-AL-5 Not Started；不把WP-AL-4完成解释为整个Autonomous Life Target完成。
