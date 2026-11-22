# RRPG 日内工作、持续活动与现场互动开发规格 V0.1

日期：2026-10-05（Asia/Shanghai）
状态：连续 A→B→C→D 授权 / 本地 Implemented & Verified / 未提交；实际验收见实施记录

## 1. 来源、范围和编号

本规格继承 [A 时间与实际到达 V0.2](2026-10-05_RRPG_Intraday-Time-Expedition-Arrival_Development-Spec-V0.2.md) 与制作人已确认的日内生活方向。制作人本次明确授权连续完成 A→B→C→D，允许自行决定并记录可逆、可配置的最小默认值，不再逐阶段等待审批。原 V0.2 的“后续阶段未授权”属于当时语境；本次授权覆盖这些阶段，不能据此停在普通工程问题上。

这是一条 Intraday 模拟迁移，不将原 WP-AL-5 Village World Presentation 静默改号。D 提供本轮现场投影与体验验收所需的最小表现，不声称原 WP-AL-5 的全部内容已完成。完整作物、物流、工资、考勤、随机效率、Knowledge/Rumor、Recognition/Event/LifeEpisode 扩展、远征途中 checkpoint 继续 Deferred。

唯一时间 Authority 是 VillageTime；职责、项目、库存、Person、Activity、Decision、Social/Outbox 继续各自拥有现有事实。本轮无第二 Clock、无第二 Project、无第二 Activity FSM、无第二关系账本，表现不推进到达。不得修改制作人资产、工作簿、Scene/Prefab，所有默认值留在可注入的纯配置记录和现有 Test/Prototype 内容层。

## 2. A 验收前置与隔离

A 完整验收后才接入 B，B 通过再接 C，C 通过再接 D。失败先定位、修复和复跑，不以旧测试 PASS 冒充新阶段已实现。

原工作区 TMP 存在已知历史 exception；A 的原工作区回归另外新增 8 个动态字形，hash 已登记且不恢复。后续验证使用字节复制的独立仓库目录、独立 Editor、独立 company/product 存档目录，禁止 hardlink 或把测试写回原资产。副本保持原仓库层级和 Compiler Generated 输入；副本的测试/Builder 资产写入只作用于副本。原工作区每阶段核对保护文件 hash、Scene/Prefab hash 和空 index。

## 3. B：职责、项目、批次与午餐

### 3.1 最小可调默认值（Prototype / Not Frozen）

- 工作时段 08:00–12:00、13:00–17:00；午餐 12:00，午休至 13:00；通勤默认 15 世界分钟。
- 480 分钟实际劳动为一个旧 Daily Work Unit；普通岗位按整批次兑现现有 PerWorkerDailyOutput。厨房按现有 input/output/units 计算批次时长，在批次开始扣材料，缺料不累计虚假加工工时。
- 同岗位换人只继续岗位批次，不重置或重复兑换。项目进度使用自己的原实例，在整数 Progress 外保存未满一单位的劳动余量；达目标仍由 ProjectSystem 唯一切换 Completed，后果只消费完成增量。
- 项目名单保留容量预约；远征/Unavailable/死亡者不劳动，不删除名单，不重复正式 Join；无人时 Paused 是执行状态而非删除项目，Load 保留。返村后下个合法劳动边界恢复，项目优先于 Primary Assignment。下班/午休不作为整天自主活动 Hard Gate。

### 3.2 时间接线与营养

Village AdvanceSink 处理时段/批次完成/午餐关键边界，正常、Fast、Jump、Return Catch-up 都复用同一 AdvanceTo。每个实际劳动区间只累计一次；午夜停用对应旧整天 Project/资源发放，保留日汇总和唯一 Formation 日结算，不允许新旧双发。

午餐先兑现此前完成生产，再固定截取在村、活着且可供餐的 PersonIds（Ordinal 稳定、去重），需求是完整基础份，不按半天折算。采用公平 Equal Share：库存和应急食材形成每人相同满足比例，不按列表先后分配整份；同一午餐所有人同待遇。满足比例达到可调阈值才清 Hunger streak，否则一次递增；未参加者不写零满足、不增 streak。午后返村不补午餐。已合法消费的远征补给可作为本日供养证据，在返村时只允许清除该日既有营养不足，不对未参与午餐者额外惩罚；同一日不双扣、不双增。

Village 可空 IntradayWork 区块保存批次材料投入/余量、实际劳动当日聚合、LastLunchMinute 和必要营养 evidence。SaveVersion 保持 1。Load replacement，不重放午餐/生产/Join，非法新块 preflight 失败保留旧世界。旧档 missing 区块从加载时刻开始，不追造过去劳动和午餐。

### 3.3 B 验收

08–12 劳动后午休不增长、换人不双产、厨房缺料/后到货不瞬间兑换、批次材料投入 Save/Load 一次、项目无人保留/远征暂停/返村恢复、项目完成后果一次、午餐前后生产区别、Equal Share reorder 一致、午餐远征及午后返村不处罚、Formation 按真实劳动比例每日一次、normal/Fast/Jump/Catch-up 等价、真实 Town 操作和显示。

## 4. C：持续活动与机会

### 4.1 契约

保留 Decision Snapshot、Desire、Plausibility、Weighted Ordering、Minimal Acceptance、公平 Arbitration 算法。替换每日“一轮后立即 Complete”为稳定日内 Opportunity Identity + 持续 Activity。Prototype 机会为工作/午休边界之外的固定机会点（默认 07:00、17:00、20:00），及合法完成后的下一可配置机会；不逐帧 reroll，不把同一机会随 source reorder 重抽。

Opportunity ID 包含绝对世界分钟和 phase；NoMajor 也持久去重。Daily exactly-once 历史字段兼容保留，新实例使用 opportunity exactly-once；保存上次 resolved opportunity，Load 不开窗、不选择、不 replay。新机会刷新只能更新机会快照，不能清除正在通勤/执行的 Instance 或 Claim。现有旧 DailyWindow API 保留兼容语义，正式日内 Host 使用明确新模式。

安排成功只 Commit；经过实际通勤后 Begin；经过配置 duration（Prototype 默认 Rest/Drink/Talk 60，VisitSick/Help 90 分钟）才 Complete。Activity Runtime 仍拥有 FSM 和 Claims，Host 只投影时间/上下文。每个完成先经过现有 Prepare→Complete→Enqueue→Drain；下一快照前 Feedback Barrier fail closed。提前取消/死亡/离村按明确 Interrupted 终止，不产生成功 Fact；活动跨日保留，不因 midnight 清空。

### 4.2 持久化与边界

ActivityWorld additive 版本字段保存 active Instance 的稳定身份、candidate 语义、原计划/到达/完成时刻、FSM、必要参与状态；Claim 由验证后的 active Instance 重建，不额外保存第二占用表。PersonActivityState 保留原字段，新增必要 last completion/opportunity 去重；不增加 RepeatStreak 或 recent-N history。

Restore preflight 全部 active 数据（日期、顺序、重复身份、互斥 actor、状态、已完成时间），成功后 replacement。合法旧块只有 PersonActivityStates 仍支持；invalid DTO 保留当前 ActivityWorld。Load 不自动推进或提交过去完成，仅下一 Advance 处理未来边界。SaveVersion=1；活跃世界推进会话的保存许可仍由 A AdvanceSession 唯一决定。

### 4.3 C 验收

Commit ≠ Complete、通勤时间消耗、相同机会重复/NoMajor 重复、stable reorder、刷新保留 Instance/Claim、跨日和日内 Save/Load、非法恢复零 mutation、完成一次/关系反馈一次、stale window/缺 source/未 settled barrier、不重放过去机会、正常和跳过路径一致。

### 4.4 本地工程选择（已本地验证，Prototype / Not Frozen）

机会默认 07:00、17:00、20:00；成功完成后的再机会间隔为 30 世界分钟，可注入配置。身份取世界分钟与明确 phase，不取帧数或墙钟。活动通勤默认 15 分钟，实际执行 duration 与通勤分开。职责有优先权：早班通勤起点及下午恢复时中断仍占用该职责人物的自主活动，再开始职责通勤，不把未完成活动伪装成功。不承担职责的人不受该中断影响。

旧每日 API 保留兼容模式；正式 Host 使用显式日内机会模式，Selection 算法仅获得该模式及稳定机会身份。安排成功只返回 Committed，不借 gateway 返回虚假的 Begin/Complete 成功。活跃实例时刻仅由现有 VillageTime 提供，Activity 域比较输入数值，不自行推进时间。Social projection 使用实际完成日期；Load 只恢复，下一次正常推进才检查合法未来边界。

显式 `SaveWithVillage` 继续尊重调用方给出的 Village 快照，不擅自替换成另一份世界。存在日内活跃 Activity、完成分钟或机会 marker 时，缺失/不匹配的时间快照会明确拒绝 Save，不写出自身无法安全 Load 的档。普通正式 Save 和 Return FinalSave 一直携带当前 VillageTime。测试专用世界 reset 同步清除旧的时间相关 Activity/Decision/Feedback 夹具，避免新时钟与旧持久实例混合；这不改变正式 Load/NewGame 的替换责任。

## 5. D：到場再验、多人个人结果与最小表现

访客出发是自己的意图，不预占目标或远程保证接受。Simulation Presence 来自已保存的时间/地点/执行状态，Known Place 默认 Home；到达时才检查目标在场与接待兼容。静态默认位置与通勤时长可注入，不建立完整 Knowledge。

Rest 可接待 VisitSick/Talk；专注工作/外出/Unavailable 不可被强行中断。无目标/不在场/拒绝接待 → Interrupted/扑空结果，无成功 Social Fact；Prototype 同目标同类型 120 分钟冷却，只记录必要最近失败抑制，不整本历史参与 Utility。

每位访客拥有独立稳定 ActivityInstanceId 和 MemberResultId；共享场景只是按地点/目标投影的共处组，不是新 Activity Authority。多位访客可兼容同访，Target 不获得互斥多份 Claim。个人有效参与门槛默认 30 分钟（可配置）；早退不足门槛不成功，达到门槛按本人结果完成一次，其他访客继续。非访客共同在场不自动产生 Fact，不全员两两奖励。Fact source 使用个人稳定 instance/result identity，继承 Canonical Ledger exactly-once 与 Durable Outbox。

表现采用现有 Village World View 的 Runtime 投影，显示人物位置、通勤/工作/午休/休息/访客/扑空状态和时分；已有输入用于查看与可逆中断，不写 Scene/Prefab、不新增表现 Clock。Sprite/动画只跟随模拟，关闭 view、切换视图和 Jump 不改变结果。

### D 验收

真实 Town 路径可见人物时间/地点/状态；目标离开后扑空，无 Fact；Rest 接待；两人同访、一人早退，个人结果不双算、剩余成员继续；跨日/SaveLoad result identity 稳定；Prepare 必须在有效完成前；同 input 正常/Fast/Jump/Catch-up 同权威结果；关闭 view 不改变 Presence/完成；不修改制作人资源。

### D 本地工程选择（已本地验证，Prototype / Not Frozen）

日内定向 Prototype 活动使用显式 Arrival Reception 模式；旧 Exclusive Target 模式继续兼容。Proposal 接受的是访客自己的出发意图，返回“接待待到场验证”，仅占访客和显式 exclusive participants，不占目标。现场有效参与门槛 30 分钟；未达门槛的取消/扑空不成功，达到门槛的本人提前离开经 Prepare 后完成一次，其他访客继续。MemberResultId 从本人的稳定 ActivityInstanceId 派生，与既有 activity occurrence 对齐，不建立 Group Ledger。

Known Home 默认 `Home:<PersonId>`，区分不同人物住宅；Tavern/Workshop 仍为共享 Prototype Place。ActivityWorld 的最小 Presence 记录保存实际已到达地点/时刻，Work Adapter 只在真实到岗边界投影；通勤中的位置由已保存计划与 FSM 派生，不用 Sprite 到达判定。接待只允许目标确实在自己的 Known Home 且空闲或正在 Rest；工作、外出或其它专注活动不强制打断。最近一次扑空仅保存目标/类型/时刻，用于 120 分钟可调冷却，不引入最近 N 次历史。

现有 VillageWorldLayer 加入可关闭的最小现场读模型与检查/中断入口；不写 Scene/Prefab，不替代 WP-AL-5 完整世界表现，不在 view 中推进时间或创建人物事实。

## 6. 收口、测试与尚未实施状态

每阶段 targeted、受影响真实产品路径、必要 full EditMode/PlayMode；最终 Compile 0、全回归 0 known failure、链接/锚点、git diff --check、scope/hash 审查。首次失败和修复过程如实保留。仅通过的阶段更新 Current；尚未实施的阶段保持 Target，最终统一交付 diff、证据和剩余事项。

本规格初次落仓时 B/C/D 尚未实现；连续实施后已分别验证，最终 Compile 0、targeted 94/94、full EditMode 1456/1456、full PlayMode 99/99、真实 Town 8/8 PASS，详细首次失败和证据见 [实施记录](2026-10-05_RRPG_Intraday-AD_Execution-Record.md)。本地 Current，不表示 main 已发布。暂不 stage/commit/push/main publication。

活跃通勤缺人物来源时在推进 mutation 前可重试暂停；host 绑定恢复只续原请求，不放开世界写权限。工作开始和新自主出发意图按当时分钟结束已接受接待；失败投影不伪装 Completed。
