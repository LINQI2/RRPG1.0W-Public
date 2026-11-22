# RRPG Intraday A→B→C→D 实施与验收记录

日期：2026-10-05（Asia/Shanghai）
状态：A～D 本地 Implemented / Verified / Technical Stage Verified；未提交 / 未发布 main
最终回归完成：2026-10-05 23:55（Asia/Shanghai）
Git 基线：main@1ee486289214dd0497a0d0b048da1c30592b0be7

Binding：[A V0.2](2026-10-05_RRPG_Intraday-Time-Expedition-Arrival_Development-Spec-V0.2.md)、[B～D V0.1](2026-10-05_RRPG_Intraday-Work-Lifecycle-Onsite_Development-Spec-V0.1.md)。原 WP-AL-5 不静默改号。

## A：Implemented / Verified（仅当前本地工作区）

- VillageTime 单一整数世界分钟、Gregorian WorldInstant、AdvanceSession 原请求/原目标/完整游标/等待实例/保存许可；Update、TickDay、Jump、Return 复用一条推进链。
- Progression native DurationMinutes、整次远征累计 ceil/节点扣增量；Legacy Event 的天单位在 adapter 转换，不修改工作簿。
- Return 在真实 departure+settled elapsed 到达；Pending 使用真实已有 VillageSocialEventUI，经 Overlay 临时挂载、真实 EventSystem Raycast/点击、结果关闭、原请求续跑；不修改 Scene/Prefab。
- FinalSave 失败继续 AwaitingFinalSave 世界锁，只能专用 retry，未 Completed 不恢复 Town；验证十秒后 Clock、Village/Social/Decision 和存档字节不变。
- nullable 新时间块、invalid preflight、旧档近似迁移、Replacement/无 Load replay，SaveVersion=1。
- 真实原生 Progression 30 小时节点→08:00 出发→次日 14:00 Town 返程验证通过；失败选项、错误关闭、禁用 UI 不能放行；成功结果关闭后才续跑。

实际证据：Compile 0 errors；targeted EditMode 83/83；full EditMode 1398/1398；full PlayMode 96/96；产品 targeted 14/14；追加严格专项 6/6（十秒冻结、非法选项/禁用 UI、原生节点等）。追加专项仅增强 Play 测试，未再改变已全量验证的 Runtime。

首次失败如实保留：浮点 JSON round-trip 一 ULP 精度断言、TownUI root/Canvas 区别、脱离父 Canvas 后 WorldSpace、普通确认入口没有关闭 realtime gating、等待阶段显示 Pause/关闭结果的接线，以及旧 Journey 天显示断言；均已定位修复并按影响复跑。隔离副本初始层级缺失导致源码/Generated 测试失败，补齐等价仓库层级及生成输入后全量通过；失效外部 MCP 的授权 Error 日志通过仅隔离 Editor 关闭重连解决，未屏蔽 Runtime Error。

## B：Implemented / Verified（仅当前本地工作区）

岗位整批与厨房材料投入、项目实际劳动余量、通勤、午休、午餐 Equal Share、远征名单保留但不劳动、返村恢复、真实劳动 Formation 已接入同一 AdvanceSink。午夜停止旧整天发放。SaveVersion=1，IntradayWork nullable/invalid preflight/replacement，不重放生产、供餐或 Join。

Compile 0 errors；纯域 14/14；专项 EditMode 19/19；最终 full EditMode 1417/1417；full PlayMode 97/97；受影响真实产品路径 8/8。实际结果分别保存为 B-pure-initial.txt、B-targeted-initial.txt、B-full-edit-final.txt、B-full-play-final.txt、B-product-final.txt。

首次 full EditMode 1395 PASS / 22 FAIL，源于旧整天结算、项目整天互斥、未绑定人物来源的夹具；迁移夹具使用真实显式 PersonId 与连续世界推进，保留失败重试、身份隔离、恢复和完成一次等断言。首次 full PlayMode 93 PASS / 4 FAIL，定位并修复跳过实际劳动的 UI 测试驱动、旧项目出征门禁断言及产出诊断。最后详情测试误读只含战斗摘要的卡片，改为点击已有真实人物详情面板并验证保留工程职责；没有为断言扩写卡面或删除职责覆盖。所有失败证据保留。

## C：Implemented / Verified（仅当前本地工作区）

稳定日内 Opportunity Identity（NoMajor 也去重）；Selection 只 Commit；实际通勤后 Begin、duration 后 Complete；刷新保留活跃实例/Claim，跨日保留。ActivityWorld 保存已验证的活跃语义与计划时刻，恢复重建 Claims 而不开窗/执行；严格 active invalid preflight 保留旧状态。DecisionWorld 保留原每日字段并增加最后机会 marker。实际完成日期经 Prepare→Complete→Enqueue→Drain，delivery fault 不回写 Completed，下一机会 barrier fail closed。缺少匹配时钟的显式 Save 拒绝写出，不替换调用方快照，SaveVersion=1。

Compile 0 errors；最终专项（含反馈 integration/30 日 Headless）52/52；正常/Fast/Jump/Return 31 日持久世界等价专项 1/1；full EditMode 1441/1441（随后仅追加等价测试，单项 1/1）；full PlayMode 98/98，包含真实 Town driver 通勤→执行→完成与 Save 不提前完成。结果文件 C-targeted-final-52of52.txt、C-path-equivalence-final.txt、C-full-edit-1441of1441.txt、C-full-play-final.txt。

首次专项 18/20：真实 Save 夹具使用未知人物模板被原恢复流程过滤；改为已有模板并断言加载后 PersonId 仍存在，20/20 复跑。首次 full EditMode 1406/1437：旧午夜/即时完成断言、预算 Yield 续跑夹具，以及旧时钟 reset 留下持久 Activity/Decision 的跨测试污染；修正测试专用 reset 与恢复顺序，未放宽正式 Load 校验。受影响复跑 231/232，最后旧 Headless Completion 断言改为实际时间推进后的 Canonical Fact/方向/累计反馈；最终全量通过。另补齐班中项目完成后的岗位通勤意图刷新。

## D：Implemented / Verified（仅当前本地工作区）

日内定向候选表达 ReceptionAtArrival，Proposal 仅接受访客出发意图，不提前占目标；到场才验证实际地点与接待兼容。在家 Rest 可接待；工作、外出或其它专注执行不被强制中断。Known Home 与实际 Presence 分离，缺人物来源不伪装为空世界。多人同访不共用结果账本；每位访客稳定 MemberResultId=`activity:<ActivityInstanceId>`，个人有效门槛 30 分钟（可调）；不足门槛/扑空 Interrupted，无成功事实，达门槛的本人早退 Prepare 后完成一次，其他访客和目标自己的 Rest Claim 保留。目标离开按实际分钟结算，职责开始与新的出发意图即时结束接待，不多算下一通勤段。

ActivityWorld 的最小实际地点/时刻与最近扑空目标/类型/时刻兼容保存；最近一次同目标同类型 120 分钟冷却，不是最近 N 次历史。活跃实例保存明确目标参与模式和实际目的地，replacement 重建本人 Claim，不 Claim receiver、不 replay。缺源发生在活跃通勤中时，边界预检返回 RetryableBoundaryFailure，原 Clock/游标/Claim 保留；只修复 host 绑定后续原请求，不解除其它世界写入锁。SaveVersion=1。

既有 VillageWorldLayer 增加可关闭现场面板，显示姓名、地点、时分、通勤/工作/休息/访客/扑空与同址人物，真实 EventSystem Raycast 验证检查/关闭/本人中断。关闭视图期间活动继续，视图不拥有 Clock、Presence 或 Fact。Runtime Canvas 创建，不修改 Scene/Prefab，无需额外人工 Unity 配置。原 WP-AL-5 完整 Village World Presentation 仍独立 Deferred，未启动。

实际最终证据：Compile 0 errors；受影响 targeted EditMode **94/94 PASS**（含纯接待、候选冷却、Social feedback、MissingSource 与 31 日 normal/Fast/Jump/Return 等价）；真实 Town 产品 **8/8 PASS**；最终 full EditMode **1456/1456 PASS**；full PlayMode **99/99 PASS**。证据文件 D-targeted-final-94of94.txt、D-product-final-8of8.txt、AD-full-edit-final-1456of1456.txt、AD-full-play-final-99of99.txt；D-onsite-product-final.png 已逐图检查。

首次 D targeted 51/52：反射数组参数展开；修正后 41/42：夹具在远征未收口时普通 Save 被合法拒绝，按既有保存许可收口后验证 round-trip，42/42。完整 EditMode 1455/1455 后自检补齐源缺失的可恢复暂停；该轮 1454/1456 的两项失败是预检遗漏公开诊断更新，旧 Window/游标已保留；修复结构化诊断后 targeted 94/94、最终 full 1456/1456。局部变量命名/枚举资格等编译错误均修正，最终 compile failed=false。最初现场截图暴露内部 PersonId 与入口重叠，改为姓名、地点和明确层级后重新进行真实点击/中断与截图验收。没有为通过测试删除产品约束或改动无关逻辑。

## 当前边界、接口与剩余事项

- VillageTime/AdvanceSession 唯一拥有推进，Yield/Waiting/FinalSave 不复制目标、驱动余量或游标。
- VillageIntradayWork、Project/Assignment 各自拥有真实劳动与长期职责；Activity Lifecycle/Claim 仍归 AutonomousActivity.Runtime，Selection 仅使用不可变 Snapshot，Social/Outbox 权威不变。
- 新注入配置：IntradayActivitySchedule 与 IntradayReceptionPolicy；新语义入口包括 OpenOpportunity、TryCommitScheduledAtPlace、TryBeginExecutionAt、TryCompleteAt、TryCompleteMemberResult、RecordActualPresence/RecordMissedVisit、QueryIntradayPresence、TryLeaveIntradayActivity。Host 提供实际 VillageTime，不创建另一 Clock。
- ActivityWorld IntradaySchemaVersion=1：PersonActivityStates（参与/发起/完成分钟及 Presence/最近扑空）与 ActiveInstances（稳定实例/机会/候选语义、目标参与方式/实际目的地、计划时刻、Committed/Executing）。AutonomousDecisionWorld 增加最后机会 minute/id marker。IntradayTime 与 Village.IntradayWork 为 nullable additive 区块；顶层 SaveVersion=1，invalid preflight、replacement、legacy missing 不猜历史，不 Load replay。
- 数值、住宅静态知识、机会/时长/有效门槛/扑空冷却仍 Prototype / Calibration / Not Frozen；非完整 Knowledge、Negotiation、Group/History 系统。
- 未解决：TMP 已知 workspace integrity exception（下节）；Village/Event 全世界 Load transaction 既有债务；全球存档触发所有权仍另案。无本轮阻塞或新增必须制作人裁决的玩法问题。
- 原 WP-AL-5 完整世界表现、Recognition/Event/LifeEpisode、工资物流/完整营养、远征途中 checkpoint 仍 Deferred。不得以 D 的最小现场面板冒称 WP-AL-5 已实现。
- 交付为本地 Technical Stage Verified；无 stage/commit/push，main 未发布本轮。完整文件清单与 diff 单独输出到任务产物目录，包含新增源码/测试/meta，不包含制作人资源或旧 Review diff。

## 工作区完整性

A 开始时保护资源 17 个。原工作区 TMP 先前 WP-AL-4 exception 不恢复；A 原项目 Play 又新增动态字形 U+4E16/U+4FDD/U+5C0F/U+5F85/U+6301/U+754C/U+8DD1/U+9636，任务开始 SHA256 `279F94EA5507640BEB361CADD3F4D2575F472355AC19160240581C2F4968EDCB`，隔离前 SHA256 `5A06C2E5D3671BC491E74EA432BAD0479CFD7A04BE8244ED598F0049BB8F6802`。这是保留的 workspace integrity exception，不冒充“全部 protected unchanged”，不覆盖、不猜测恢复、不暂存。

独立字节副本（无 hardlink）采用与仓库相同的 UnityProject/Development 层级、独立 Editor、独立 company/product 存档目录。后续 Unity Runner/Builder 只在该副本执行。隔离建立后 17/17 保护文件无新增变化；相对 A 开始，另外 16 个保持不变。原 Scene/Prefab 不改，index 为空，main HEAD 不变。两个旧 Review diff 未改/未暂存。

外部实际结果文件与保护 manifest 保留在本机任务产物目录，最终交付时统一提供 diff 与验收证据；不新建 Runtime-Review.diff，不 stage/commit/push。

## 最终交付核验（2026-10-06）

完整实现diff与manifest包含144个任务文件（87 tracked修改、57新文件，其中27组新源码/asmdef与meta、3份新增规格/记录），排除17个制作人dirty/untracked和两个旧Review diff。git diff --check exit0；57个新文件另行no-index whitespace检查无报警（exit1仅表示存在diff）。14份Markdown的306个相对链接和36个锚点全部通过；Current/Target/Calibration/Deferred逐段回读通过。

比对开工2902个既有文件，任务范围外没有新增变化；Scene/Prefab无变化。隔离后的17个保护hash全部相同；相对A开始，仅已明示TMP字形例外，另外16个未变。27个新meta只删除标准空字段60个ASCII尾随空格，GUID/其它字节/编码/换行不变，结束时无自动写回。main仍1ee486289214dd0497a0d0b048da1c30592b0be7，暂存区空；未stage/commit/push。

外部交付：2026-10-06_RRPG_Intraday-AD_Implementation.diff、AD-file-manifest.md、AD-test-evidence-index.json、AD-final-verification.json、AD-git-status-short.txt；最终测试原始结果与现场截图同目录保留。首次失败证据仍在，不使用旧A阶段diff冒充完整A～D。
