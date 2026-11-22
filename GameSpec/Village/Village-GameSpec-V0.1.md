# Village-GameSpec-V0.1

> 版本：V0.1
> 日期：2026-09-04
> 状态：Current / Active（V0.1 分阶段规格；未冻结项以正文为准）
> 职责：记录 Village 当前可执行规则、Stage 边界与验收标准。
> 注意：GameDesign 负责“为什么这样设计”；本文件负责“开发与验收时必须知道什么”。
> 测试内容、测试数值、候选承载方式不得自动升级为正式设计。

---

# 0. 版本目标

<a id="v16-building-stage-boundary"></a>

## 2026-10-07 后续阶段归位说明

本文件 V1 / V2、§7 与 §11 Minimum Village World 第一包中“不提前实现 Building”“Building Placement 未冻结”“禁止 Grid / 自由摆放 / 正式建筑列表”等文字保留当时 Stage 的正确边界，不能读取为所有后续阶段的永久禁令。制作人后续已批准有限 Building World Foundation 的规则归位，当前建筑身份、Placement、Construction、派生 Place / 实例级 Workplace 与建筑持久化转交 [Village Building World GameSpec](Village-Building-World-GameSpec-V0.1.md)（Current Rule / Active GameSpec）。

这不代表完整 Building System、Grid、自由摆放技术路线、完整 Economy、Upgrade 或正式建筑内容全部冻结，也不授权 Runtime。原 Village core 的 VillageTime / Assignment / Project / Activity / Social Authority 继续有效；Product Home 与 Resource 按 [GameSpec Authority 矩阵](../README.md)分工。实施 Gate 见 [V16 总 Development Spec](../../Docs/DevelopmentSpecs/2026-10-07_RRPG_Village-Product-Home-and-Building-Foundation_Development-Spec-V0.1.md)：本轮仅 V16-P，A～D Not Authorized / Implementation Not Started。

Village V0.1 目标不是完成一套完整模拟经营，而是建立一个“薄而完整”的村庄系统骨架，并与现有 Adventure / Battle / Person Growth 构成完整纵切片。

总体路线：

1. **Stage V1 — Living Village**：时间、人物、村庄状态持续运行。
2. **Stage V2 — Commitment**：玩家可把具体人物、资源与时间投入未来。
3. **Stage V3 — Social Response**：History / World Fact → Pattern → Observer / Recognition → Response → New Decision。
4. **Minimum Village World Integration**：在进入 V4 前完成最小 Village World 接线；不据此提前冻结正式世界表现、建筑或岗位美术。
5. **Stage V4 — Home ↔ Expedition**：Village 与现有远征 / Run 双向连接。

V1–V3 以 Technical Verified 记录技术完成；Technical Verification 不替代对应 Work Package 已定义的 Closure Gate。Minimum Village World Integration 是 V3 → V4 的显式前置检查点；V4 完成后形成首个 **Village Playable Milestone A**。

2026-09-04 制作人裁决已批准其**第一包职责边界与产品验收**（只批准职责，不冻结类名 / 字段名 / 地图结构 / 美术结构 / 正式建筑系统）；已批准边界与第一包验收见 **第 11 章附录**。

---

# 1. V0.1 核心原则

## 1.1 完整骨架、稀薄内容

V0.1 可以包含多个关键系统，但每个系统只做最薄的一层。

允许：
- 极少人物；
- 极少测试资源；
- 极少 Assignment / Project；
- 极少可解释的长期 Pattern / 社会倾向表达；不要求独立 Trend Runtime；
- 极少 Village Event；
- 极少 Capability。

禁止为了“看起来完整”批量制作内容。

## 1.2 单一真相源

- Village 不创建第二套长期人物真相源。
- Person 长期身份、成长、HP 等优先复用现有长期数据。
- Village 只增加 Village-specific 状态，不复制已有 Person 数据。

## 1.3 时间唯一权威源

所有 Village 长期变化必须依赖同一套 VillageTime。

禁止：
- Project 自己绕开 VillageTime；
- Pattern / Recognition / Event 自己建立独立日历或绕开 VillageTime；
- Jump Month 直接改日期而跳过中间结算。

## 1.4 测试实现不等于正式设计

任何测试资源、测试数值、测试事件、测试岗位、测试 Capability 都必须明确标记为：
- Test
- Demo
- Not Frozen
- Temporary Seed

不得因已实现而自动升级为正式内容。

---

# 2. 核心概念与职责

## 2.1 VillageTime

Village 的唯一世界时间源。

最低职责：
- 实时运行；
- Pause；
- Resume；
- 至少一种加速；
- 整数分钟时间与统一AdvanceTo/AdvanceSession；
- Day Tick（日聚合边界）；
- Month Tick；
- JumpToNextMonth。

### JumpToNextMonth 规则

不能直接把日期修改到下个月。

必须完整执行中间所有应发生的分钟边界、工作/午餐、到场/完成与日/月聚合；不能只补日Tick而漏日内活动。

例如：
- 8月15日 → 9月1日

逻辑上应等价于：
- 正常经过剩余日期；
- 正确执行日资源变化；
- 正确推进 Assignment / Project；
- 正确推进已登记的长期事实、Pattern 判定与 Event；
- 正确处理人物状态变化。

---

## 2.2 Person

Village 中的具体人物，是经营与远征共同使用的一等对象。

V0.1 最低语义：
- PersonId；
- 是否属于长期共同体；
- Primary Assignment 引用（长期持久安排，可为空）；
- 当前 Temporary Occupancy 状态（Active Project / Expedition / Unavailable / 无）；
- 是否可被玩家安排；
- 是否在远征；
- 长期成长引用；
- 长期健康状态引用（沿用既有长期人物真相源，不新建第二份 HP）；
- 生命周期预留接口。

V0.1 不要求：
- 性格；
- 关系网；
- 情绪；
- 信仰；
- 知识倾向；
- 家庭；
- 正式职业转换。

> **2026-10-04 Autonomous Life V0.1 补充：**以上边界表示 Village 基础骨架不依赖“完整 Personality / Relationship System”。已批准的 Autonomous Life Vertical Slice 现在要求**最小 Person-specific Behavioral Difference（人物级行为差异）+ Directional Relationship Foundation（有方向关系基础）**参与自主选择与反馈；仍不要求完整人格系统、完整关系网、多维正式 Relationship Model 或正式关系内容。

健康状态边界：Person 的长期健康状态继续使用既有长期人物真相源。Run 内 CurrentHP 属于远征级状态，Village 不创建其副本。SafeReturn 后 Run HP 如何映射为 Village 长期健康状态尚未冻结，Stage V1 不处理该规则。

---

## 2.3 Person Occupancy / Availability

Person 的安排分为两个独立层次：Primary Assignment 与 Temporary Occupancy。

- **Primary Assignment**：长期、持久的安排。Person 最多拥有一个；玩家修改前保持；被临时占用时暂停实际执行。
- **Temporary Occupancy**：当前的临时占用，至少包括 Active Project、Expedition、Unavailable。
- 无 Temporary Occupancy：Person 在当前 Village 管理层可用；是否进入 Autonomous Activity 还需满足对应 Activity Window / Participation 条件。
- 无 Primary Assignment 且无 Temporary Occupancy：仍可视为 Idle（无长期责任）；**Idle 不再等同于 Autonomous 的唯一前提**。
- 有 Primary Assignment 但无 Temporary Occupancy：长期 Responsibility 继续存在；Person 在责任之外仍可获得 Autonomous Activity Opportunity。

执行原则：
- 存在 Temporary Occupancy → 暂停 Primary Assignment 实际执行；
- Temporary Occupancy 解除 → 自动恢复 Primary Assignment，不要求玩家重新分配；
- 不把 Project 设计成“覆盖并删除 Assignment”。

Autonomous Life 补充：

- Primary Assignment 是长期 Responsibility，不等于全天 Occupancy；
- Expedition / Unavailable / Dead是活动门禁；Active Project名单保留长期承诺，只有真实工作/通勤时段构成当前活动门禁；
- Project完成在实际分钟边界释放执行占用，责任外机会按现时状态判断，不沿旧日初快照额外封锁整日；
- Activity Commitment / Participation Claim 是 Window 内短期参与占用，不写入现有 Temporary Occupancy Authority。

禁止：
- 同一Person同时实际执行互斥工作/活动；两个Active Project的名册互斥保持，远征可以保留原Project名册但其执行暂停，不形成同时劳动；
- 远征中的人物在 Village 被重复安排。

---

## 2.4 Assignment（Primary Assignment）

定义：
> Person 持续承担的长期职责（Primary Assignment）。

特征：
- Person 最多拥有一个 Primary Assignment；
- 持续性的长期安排，玩家修改前保持；
- 没有天然“完成100%后消失”；
- Assignment 本身没有 Progress；
- 对应有限岗位 / Assignment Slot，满员后不能继续分配；
- 未被 Temporary Occupancy 占用时才实际执行；
- 实际执行可周期性地影响 Resource / VillageState，并记录可供 History / Pattern / Recognition 读取的事实；
- 人物被临时占用（Project / Expedition / Unavailable）时暂停实际执行，解除后自动恢复；
- 人物离开后立即停止该 Assignment 的作用；
- Assignment ≠ Profession；Assignment 不自动修改人物永久 Profession。

---

## 2.5 Project

定义：
> 有开始、有进度、有完成条件的有限长期承诺（Temporary Occupancy 的一种）。

最低结构：

Person + Time + Optional Resource → Progress → Completion → Consequence

本 V0.1 规格对 Project 的规则定义，由 Stage V2 落地：
- Project 保存具体 Participant Person（Participant PersonIds）；
- Project 可以有有限 Participant Capacity；正式 Project 的具体容量以后分别定义，V2 测试内容可使用 Test Capacity；
- 同一 Person 默认不能同时正式参与两个 Active Project；
- 参与者参与期间临时暂停其 Primary Assignment，完成后自动恢复；
- Project 依赖 VillageTime 推进；
- 完成后 Consequence 只结算一次；
- 完成后释放参与者；
- Project 架构允许未来支持取消，但 V2 首版是否提供玩家主动取消操作为 Not Frozen；不提前冻结完整 CancellationPolicy。

---

## 2.6 Resource

定义：
> 玩家可以储存、投入、消耗的有限量。

FP-V0.1 正式最低资源集（2026-09-07 归位）：

- **Ingredients**：Village production / processing input；可作为 Emergency Consumption 候选；不是 Expedition 标准 Carry 资源；
- **Food**：Village 标准生存消费资源；Expedition 标准 Carry / Supply 映射资源。

同时注明：

- FP-V0.1 正式最低资源集 ≠ 最终全量资源列表；正式全量资源种类仍未冻结；
- 不自动确认食物 / 木材 / 石材 / 金币四资源体系；
- Resource daily settlement 只依赖现有唯一 VillageTime / Day Tick，不得新建独立 ResourceTime / FoodClock（见 §1.3）；
- 测试资源与测试数值必须继续显式标记 Test / Not Frozen，不自动升级为正式数值。

> 生产、加工、消费、Person Fulfillment、Hunger 与 Expedition Carry 的详细执行规则由 `Village-Resource-Coupling-GameSpec-V0.1.md` 专项 GameSpec 负责；本文件只保留 V0.1 Resource Authority 的最小语义。

---

## 2.7 VillageState

只保存共同体级事实。

允许：
- 生存状态；
- 社会状态；
- Capability 状态；
- 少量全局指标。

禁止：
- 把所有人物私有状态塞进 VillageState；
- 让 VillageState 变成万能字典。

---

## 2.8 长期 Pattern / Trend（设计语义）

定义：
> 由人物、玩家行为与 VillageState 长期积累，但尚未形成必须处理事件的社会倾向、问题或机会。

本节描述设计语义，不要求建立独立 `Trend` Runtime System、统一程度条或持久化对象。V0.1 优先由 World Fact / History、Pattern、Observer / Recognition 与 Event 的组合承担：

- 长期变化有可解释来源；
- Pattern 可由已登记事实识别；
- 合理 Observer 能够认识并表达其社会意义；
- 值得玩家处理时，可以进入 Village Event 条件或形成其他可感知回应。

这里的 Trend 不是随机事件倒计时，也不等于已冻结的 Runtime 中间层。

重大长期 Pattern 原则上应至少部分可读，不要求公开精确公式。

---

## 2.9 Village Event

定义：
> 共同体内部状态、人物或长期 Pattern 在某个节点显现成的具体情境。

与 Expedition Event 区分：

### Village Event
主要表达：
- 内部后果；
- 社会暗流；
- 人物与长期选择的回响。

### Expedition Event
主要表达：
- 外部未知；
- 冒险随机性；
- 外部环境的不确定性。

Village Event 最低结构：

Context → Choices → Consequences

重大 Village Event 应尽量能够追溯到：
- Person；
- 长期 Pattern / Recognition；
- VillageState；
- Time；
- 历史选择；
- 当前阶段条件。

---

## 2.10 Consequence

定义：
> 玩家行为或世界事件真正留下来的状态变化。

V0.1 可以作用于：
- Person；
- Resource；
- VillageState；
- World Fact / History 或长期 Pattern 所依据的状态；
- Capability；
- 后续 Event 条件。

Consequence 可以：
- 即时生效；
- 延迟生效。

---

## 2.11 Capability

定义：
> 共同体当前已经获得的“新的行为、内容或成长可能性”。

Capability 不等同于永久数值 Buff。

未来可用于控制：
- 新牌进入 Unlocked Pool；
- 新 Village Action；
- 新 Project；
- 新 Event Choice；
- 新事件池；
- 新 Run 内容；
- 新成长方向。

V4 至少验证一次：
Village → Capability → Unlocked Pool / Run 可能性发生变化。

---

## 2.12 Expedition Bridge

职责：
> Village 与现有 Adventure / Run / Battle 的双向接口。

### Village → Expedition
传递：
- 出征人物；
- 人物长期状态；
- Starter；
- HP；
- 其它已有正式长期数据。

### Expedition → Village
返回：
- HP；
- Growth Exp；
- Pending 收益；
- 已确认 Run 结果；
- 后续可扩展经历 / 世界信息。

不重做 Adventure。

---

# 3. Stage V1 — Living Village

## 3.1 目标

让 Village 成为一个拥有时间、人物和持续状态的长期世界。

## 3.2 必须实现

### 时间
- 显示当前日期；
- Pause；
- Resume；
- 至少一种 Speed；
- 整数分钟时间与统一AdvanceTo/AdvanceSession；
- Day Tick（日聚合边界）；
- Month Tick；
- JumpToNextMonth。

### Person
- 读取现有长期人物；
- 显示人物是否在村庄；
- 显示人物是否远征；
- 显示人物是否不可用；
- 不创建第二套 Person 真相源。

### VillageState
- 至少一个 Test 生存状态；
- 至少一个随时间变化的测试规则；
- 可保存；
- 可加载。

### UI 最低可读性
玩家至少能回答：
- 今天是什么时间？
- 村里有哪些人？
- 谁当前可用？
- 村庄基础状态是什么？

## 3.3 Save / Load

必须支持：
- VillageTime 持久化；
- VillageState 持久化；
- 人物 Village 占用状态持久化或可可靠重建。

旧档兼容：
- 新增 Village 数据不得导致原有有效存档无法继续使用；
- 旧档缺少 Village 数据时，应能够以兼容默认状态进入 Village；
- 具体 SaveVersion 与迁移方式由存档规格确定，本规格不自行决定 SaveVersion。

V1 不实现：
- 离线真实时间自动流逝。

## 3.4 V1 禁止顺手实现

> 历史 Stage 边界；后续有限 Building Foundation 见 [2026-10-07 归位说明](#v16-building-stage-boundary)，不删除本节原禁止项。

- 建筑；
- 正式食物经济；
- Assignment；
- Project；
- 正式 Trend Runtime（V0.1 不预设）；
- Village Event；
- Policy；
- Culture；
- Relationship；
- Training；
- Research；
- Capability 正式内容；
- 新卡牌内容；
- 正式数值；
- 正式美术优化。

> **Y1 注记（2026-09-07 Cross-document Closure）**：以上条目是 V1 阶段的开发边界，不代表当前 FP-V0.1 永久禁令。FP-V0.1 已正式确认 **Ingredients → Food Minimal Survival Resource Loop** 为 Current（见 `Village-Resource-Coupling-GameSpec-V0.1.md`）。本节历史内容保留不改。

## 3.5 V1 Technical Acceptance

至少验证：

### Time
- 正常流逝；
- Pause 停止；
- Speed 生效；
- 日/月切换；
- JumpToNextMonth 不漏 Tick。

### Person
- 长期人物正确进入 Village；
- Expedition 人物不可在 Village 重复使用；
- 不复制人物身份数据。

### State
- 测试状态随时间正确变化；
- Save / Load 一致。

### Regression
不得破坏：
- Adventure；
- Battle；
- Person Growth；
- SafeReturn；
- Starter；
- Unlocked Pool。

达成后：
**Stage V1 Technical Verified**

---

# 4. Stage V2 — Commitment

## 4.1 目标

让具体 Person 的长期时间投入与临时共同体承诺形成真实机会成本。

核心因果链：

Person
→ Persistent Primary Assignment
→ Current Village Contribution
→ Temporary Project Commitment
→ Opportunity Cost
→ Project Progress
→ Completion
→ Automatic Return to Primary Assignment

## 4.2 Primary Assignment 规则

- Person 最多拥有一个 Primary Assignment；
- Assignment 是持续性的长期安排，玩家修改前保持；
- Assignment 对应有限岗位 / Assignment Slot，满员后不能继续分配；
- Assignment 本身没有 Progress；
- 未被 Temporary Occupancy 占用时才实际执行；
- 实际执行可产生：Test Village Contribution；Profession Formation Source Hook (Test)；
- Assignment ≠ Profession；Assignment 不自动修改人物永久 Profession。

## 4.3 Temporary Occupancy 规则

V2 将“长期安排”与“当前执行”拆开。Temporary Occupancy 至少包括：

- Active Project；
- Expedition；
- Unavailable。

执行原则：

- 存在 Temporary Occupancy → 暂停 Primary Assignment 实际执行；
- Temporary Occupancy 解除 → 自动恢复 Primary Assignment；
- 无 Primary Assignment 且无临时占用 → Idle / Autonomous；
- 不把 Project 设计成“覆盖并删除 Assignment”。

## 4.4 Assignment Capacity / Test Workplace

V2 首版至少支持：

- Assignment / Workplace 唯一身份；
- Capacity；
- 当前绑定的具体 Person；
- Test Contribution。

V2 只验证：

> Village Structure 可以限制可用 Assignment Opportunity。

不冻结：

- 正式农田；
- 铁匠铺；
- 学校；
- 正式岗位容量；
- 建筑升级；
- 建筑摆放；
- 正式建筑美术。

可以明确使用：Test Workplace / Test Assignment Slot。

## 4.5 Project 最小规则

Project 至少具有：

- Identity；
- Lifecycle；
- Participant Capacity；
- Participant PersonIds；
- Progress；
- Completion Result。

规则：

- 玩家主动启动 Test Project；
- 玩家选择具体 Person；
- 参与者临时暂停 Primary Assignment；
- Project 依赖 VillageTime 推进；
- 同一 Person 默认不能同时正式参与两个 Active Project；
- 完成只结算一次；
- 完成后释放参与者；
- 参与者自动恢复原 Primary Assignment。

## 4.6 Opportunity Cost（显式规则）

当 Assignment 中的人物被抽调参与 Project 时，原 Assignment 当前有效贡献必须下降或停止；Project 则获得推进。

Project 完成后：

- 原 Assignment 的实际执行恢复。

玩家必须能够理解：

> “因为我把这个具体的人抽去 Project，所以原岗位现在损失了贡献。”

## 4.7 Profession Formation Source Hook (Test)

> 当前口径（2026-09-09）：本节记录的是 **Stage V2 当时的开发边界**。Formation 基础规则现已是 Current，统一服从 `GameSpec/Person/README.md`、`GameSpec/Person/Person-Profession-Growth-GameSpec-V0.1.md` 与 `GameSpec/README.md`；最终命名、数值、正式内容、Transition、Dormant 等仍为 Not Frozen / Supporting Candidate。以下“不实现”只说明 V2 不负责这些内容，不代表整个 Profession / Formation 系统至今未冻结。

V2 只记录或暴露 Formation 可以读取的执行事实来源，不在该阶段实现正式 Profession / Formation 内容；`Profession` 作为最终命名在该阶段尚未冻结。

规则：

- 人物实际执行 Assignment / Project 时，可以产生未来 Formation 可读取的执行事实来源；
- 仅保留 Primary Assignment、但当前被 Project / Expedition 占用时，不应继续产生该 Assignment 的执行事实来源；
- Hook 的具体字段、权重与最终命名均为 Test / Not Frozen；
- 不实现 Experience Bar、隐藏评分、候选算法、年龄阈值或身份定型；
- 不实现 Profession 定型；
- 不实现 Profession Trait（职业特性）；
- 不实现 Profession Card Pool。

## 4.8 Workload / 工时模型边界

当前按真实到岗后的劳动分钟产生岗位/项目进度，午休/下班/外出不劳动。项目优先于岗位，完成后按实际边界重新通勤原职责，名册与Capacity保留长期安排而不等同实际在场。

工作窗口、通勤时长、480分钟等单位与数值是当前可配置Prototype默认，不冻结正式班次或Efficiency。复杂疲劳、Needs与完整日程仍Deferred；长期资源规则见[Resource Coupling](Village-Resource-Coupling-GameSpec-V0.1.md)，当前实现归Architecture。

## 4.9 人物适配 Suitability（薄层）

V0.1 允许存在薄层适配。

原则：
> 人物适配制造偏好，不制造默认硬职业锁。

Suitability 可以影响：
- 效率；
- 后续可扩展状态收益。

V0.1 不冻结：
- 正式职业体系；
- 正式适配标签；
- 正式倍率。

## 4.10 Idle / Autonomous 原则

Idle 不等于浪费。

系统不得默认要求所有人时刻必须被塞入岗位。

未来 Idle 可以服务于：
- 远征准备；
- 突发事件响应；
- 恢复；
- 人物生活；
- 对话与关系。

V0.1 不要求全部实现。

## 4.11 V2 测试内容

建议仅：
- Test Workplace / Test Assignment Slot（含 Test Capacity）；
- 少量 Test Project；
- 复用现有少量人物。

全部标记 Not Frozen。

## 4.12 V2 Technical Checkpoints（V2A / V2B / V2C）

同一 Repository Work Package 内按 V2A → V2B → V2C 推进。

### V2A — Assignment Core

机器验证至少包括：
- Primary Assignment；
- Capacity；
- Assignment 持久；
- Test Contribution；
- Save / Load；
- Expedition / Unavailable 时不错误执行；
- Profession Formation Source Hook (Test)。

不要求用户人工验收。

### V2B — Project Commitment

机器验证至少包括：
- Project Participants；
- Temporary Occupancy；
- Opportunity Cost；
- Progress；
- Completion；
- Auto Return；
- 同一 Person 不可进入两个 Active Project；
- Save / Load 中途恢复。

不要求用户人工验收。

### V2C — Product Integration + Functional UI

接入当前 Town 产品路径。

玩家至少能够：
- 选择具体 Person；
- 指定 Assignment；
- 看到岗位 Capacity；
- 启动 Test Project；
- 选择参与 Person；
- 看到原 Assignment 暂停 / 贡献变化；
- 看到 Project Progress；
- 完成后看到人物自动恢复原 Assignment。

正式固定 UI 骨架继续遵循：Root / Panel / Blocker / 核心按钮 / 主要容器必须 Edit Mode / Prefab Mode 可见可调。动态人物列表 / 重复条目可 Runtime 生成。

## 4.13 V2 Final Closure Gate

V2 最终用户人工验收只验证：

1. 是否能明确把具体 Person 分配到岗位；
2. 是否能理解岗位是否已满；
3. 抽去 Project 后是否能理解原岗位贡献为什么变化；
4. Project 完成后人物是否自动恢复原 Assignment。

整个过程是否清楚表达：

> “我把这个具体人的时间承诺给了另一件事。”

以下技术测试由自动验证承担，不交给用户：Save 结构；Tick；序列化；一人双 Project；Project Progress 精确计算；Illegal State；自动回归。

## 4.14 World Visualization 边界

V2 Closure Gate 不要求：
- 正式村民地图移动；
- 正式农田 / 铁匠铺 / 学校美术；
- Pathfinding；
- 工作动画；
- Project 世界施工表现。

V2 可使用：Test Workplace + Functional UI。

数据 / 运行结构不得阻止未来映射：

> Person ↔ Workplace / Assignment ↔ Project ↔ World Anchor

Minimum Village World Integration 必须在 V3 之后、V4 之前完成；它是进入 Village Playable Milestone A 路线的显式前置检查点。

## 4.15 V2 明确禁止提前实现

> 历史 Stage 边界；当前有限 Building 规则见 [2026-10-07 归位说明](#v16-building-stage-boundary)，不表示完整 Building / Grid / Economy 已冻结。

- 正式 Food / Wood / Stone 经济；
- 正式建筑系统；
- 自由建筑；
- 完整工时 / 班次系统；
- 疲劳 / 心情；
- AI 最优分配；
- 自动推荐；
- 批量 Assignment；
- 正式 Trend Runtime（V2 不预设）；
- Village Event；
- Capability 正式内容；
- 人物主动提 Project；
- 完整 Profession 算法；
- Profession Trait（职业特性）；
- Profession Card Pool；
- 正式 Battle 联动；
- 正式 Village World 可视化。

> **Y1 注记（2026-09-07 Cross-document Closure）**：以上“禁止提前实现正式 Food / Wood / Stone 经济”是 V2 阶段的开发边界，不代表当前 FP-V0.1 永久禁令。FP-V0.1 已正式确认 **Ingredients → Food Minimal Survival Resource Loop** 为 Current（见 `Village-Resource-Coupling-GameSpec-V0.1.md`），不恢复旧四资源体系。本节历史内容保留不改。

达成 V2A + V2B + V2C 机器验证与集成后：**Stage V2 Technical Verified**。对应 Work Package 仍须通过 4.13 Final Closure Gate，才能进入 `Development Closed`。

---

# 5. Stage V3 — Social Response

## 5.1 目标

让 Village 从“执行玩家命令”升级为“会回应玩家的共同体”。

V3 不是单纯事件系统或 Bark 系统，而是第一次证明：

> 我过去长期怎么安排，会改变今天我面对的社会状态。

它承担的是 **History → Social Meaning**。

V3 概念链：

```text
World Fact
→ Pattern
→ Observer
→ Recognition
→ Signal
→ Consequence
→ New Decision
→ New World Fact
```

## 5.2 Social Response 机制与 Observer

Pattern / Observer / Recognition / Signal 是 V3 的设计语义与职责边界，不代表必须分别建立 PatternSystem、ObserverSystem、RecognitionSystem 或对应持久化数据结构。V3 应优先复用现有 World Fact / Person / History / Event 数据，只有真实实现需求证明必要时才抽象。

### 5.2.1 定义

Social Response = 长期世界事实被合理的人物或共同体感知、表达，并在有意义时改变未来决策空间的过程。

它不是“玩家点一下 → NPC 评论一句”。

### 5.2.2 Observer 是硬原则

> No Observer → No Social Response。

任何社会反馈必须能回答：谁注意到了？为什么是他？他凭什么知道？

没有合理 Observer 的信息，由普通 System UI 表达，不得借 NPC 之口伪装成人物反馈。

### 5.2.3 Experience ≠ Recognition ≠ Event

- **Experience**：记录客观人生事实（如“张三今年参与了四次 Village Project”），不写解释与评价；
- **Recognition**：人物对自身长期事实形成一种当前认识（如“今年我好像一直在各种工程之间跑”）。Recognition 本身不等于不满、忠诚下降或任何情绪结论；
- **Event / Consequence**：只有当该事实已经值得玩家处理时，才进入真正决策。

### 5.2.4 Response ≠ Complaint；Consequence ≠ Penalty

同一长期事实可以形成：自豪 / 疲惫 / 习惯 / 疑惑 / 被需要感 / 新机会 / 矛盾。

V3 不是“人物专门阻碍玩家治理”的系统。

### 5.2.5 正向 Gate（设计审查判据）

以下五项是判断某条事实“是否值得进入 V3 核心反馈”的设计审查标准，不是五个 Runtime 布尔条件：

> Influenceable + Decision-Relevant + Patterned + Observed + Informative

即：玩家能影响、影响未来决策、已形成持续 Pattern、存在合理 Observer、提供玩家不易直接看到的新信息。

不要求 Runtime 建立五字段 Gate 模型或统一评分系统。

### 5.2.6 Anti-Gate（不得进入 Social Response）

- 纯技术状态（Save / Tick / ID / UI 刷新 / 序列化等：System Fact ≠ World Fact）；
- 单次偶然操作（一次安排不是长期模式）；
- 已经非常清楚的信息（操作确认、状态 UI ≠ Social Signal）；
- 无决策意义的琐碎事实（可作 Ambient Flavor，不占 V3 核心注意力）；
- 没有 Observer 的隐藏数据（不让人物成为设计师的嘴）；
- 纯道德评价玩家的内容；
- 为生成反馈提前建立的万能社会指标（见 5.2.7）。

### 5.2.7 Trend 不是 V3 必需 Runtime System

- V3 **不预设 Trend 为正式 Runtime System**；长期模式先以 Pattern / Recognition 语义表达；
- 只有当未来大量消费者反复需要统一 Pattern 数据层时，才考虑是否抽象为正式 Trend System（后置决定，不在 V3 冻结）；
- V3 不默认建立：Village Satisfaction / Social Pressure / Opinion 总量条 / Mood 总量条等万能社会指标。

## 5.3 Event 触发

Village Event 是 Social Response 到达“需要玩家处理”时的出口，不要求单阈值触发，可由多个条件组合。

进入 Event 之前必须满足 5.2 的 Observer 与长期事实前提；重复日常与可后台表达的内容不弹 Event。

允许的触发条件组合（概念层，具体组合规则与阈值未冻结）：长期事实模式 / Time / Person / VillageState / ChoiceHistory / Capability / 随机窗口。

## 5.4 Event Urgency

V3 至少需要覆盖 Immediate / Deadline / Optional / Ignorable 等事件紧迫性语义；正式分类数量与命名不冻结。

## 5.5 PauseOnOpen

Village Event 可以定义打开时是否自动暂停 VillageTime；不要求所有事件统一暂停。

## 5.6 Consequence

支持：
- 即时后果；
- 延迟后果；
- 修改后续 Event 条件；
- 修改 ChoiceHistory；
- 修改 Person / Resource / Capability / 长期程度状态（长期程度状态是否抽象为正式 Trend 数据层后置，不预设）。

Consequence ≠ Penalty。

## 5.7 Policy / Institution 边界

V3 不实现完整 Policy Tree / Institution System，也不预建 Policy / Institution 专用 Runtime 接口。

V3 Consequence 的设计不得封死未来表达长期规则改变或永久世界规则修正的可能。架构不封死 ≠ 本阶段必须实现接口。

## 5.8 V3 最小测试内容（强候选原型，全部 Not Frozen）

V3 验证围绕少量真实 Pattern，不追求事件数量。当前强候选两个尺度：

**Case A — Person-level**：同一具体 Person 被反复从 Primary Assignment 抽去 Temporary Commitment。
验证：长期个人事实 → Recognition → Person Response → 后续决策变化。

**Case B — Community-level**：某个 Workplace 长期严重缺员 / 空置。
验证：长期结构事实 → 合理 Observer → Community Signal → 后续决策变化。

不依赖 Food / Profession / Relationship 系统；不因原型提前建立完整 Trend / Community Satisfaction Runtime System。

> **Y1 注记（2026-09-07 Cross-document Closure）**：此句是 V3 最小测试内容的依赖边界，不代表当前 FP-V0.1 永久禁令。FP-V0.1 已正式确认 **Ingredients → Food Minimal Survival Resource Loop** 为 Current（见 `Village-Resource-Coupling-GameSpec-V0.1.md`）。本节历史内容保留不改。

## 5.9 V3 Technical Acceptance 与 Closure

V3 不能只证明“世界会说话”（Fact → Bark），必须至少一次真实跑通完整决策回环：

```text
Past → Recognition → Response → New Decision → New Future
```

即：长期事实 → 可感知回应 → 后续决策发生变化。

**依赖边界**：V3 Minimal Runtime 的回应表现可用 UI / 文本表达（Bark / Text Signal / Event / Log），**不依赖 Minimum Village World Integration 的 Social Surface**；World 世界化表达是 V3 之后的增强，不构成 V3 Closure 前置条件。

机器/产品验收至少验证：
- Observer 原则成立（反馈可追溯到合理观察者与真实长期事实）；
- Experience / Recognition / Event 职责分离正确；
- 至少一个 Case（5.8）跑通完整决策回环；
- 同一 Pattern 不重复错误触发；
- Consequence 只结算一次；延迟后果正确；
- ChoiceHistory 可被读取；
- Event Urgency（Deadline / Ignore）与 PauseOnOpen 行为正确；
- Save / Load 一致；时间推进 / Jump Month 不漏已登记的长期事实；
- V1 / V2 回归通过。

**Trend 不作为验收对象**（非 Runtime，见 5.2.7）。

达成后：
**Stage V3 Technical Verified**

---

# 6. Stage V4 — Home ↔ Expedition

## 6.1 目标

把 Village 与现有远征 / Battle / Person Growth 接成一个完整游戏。

## 6.2 Person Occupancy ↔ Expedition

- Primary Assignment 是长期持久安排，不因出征被删除或要求玩家手动释放；
- Expedition 是 Temporary Occupancy：出征后暂停 Primary Assignment 实际执行，返程解除占用后自动恢复；
- Active Project 也是 Temporary Occupancy，同一 Person 不得同时处于 Active Project 与 Expedition；进入 Expedition 前必须先通过已批准的 Project 生命周期解除 Active Project 占用，本节不新增 Project 取消规则；
- 出征后 Village 立即将该人物的 Temporary Occupancy 标记为 Expedition；
- 返程后解除 Expedition 占用，并恢复为可由 Village 管理的状态。

## 6.3 Expedition 与 VillageTime

方向：
> 远征期间 VillageTime 继续推进。

V0.1 不冻结：
- 一场战斗对应多少天；
- 一个地图节点对应多少天；
- 不同节点是否耗时不同。

这些必须配置化。

## 6.4 Expedition → Village 回流

至少正确回流：
- HP；
- Growth Exp；
- Pending 收益；
- 长期人物状态；
- 已确认 Run 结果。

禁止重复结算。

## 6.5 Experience Record 最小接口

V0.1 不实现完整经历系统。

但允许留下最小经历记录，例如：
- 参加过某次远征；
- 遭遇过某类事件；
- 重伤归来；
- 击败某 Boss。

正式内容、触发逻辑、性格变化全部后置。

## 6.6 Capability → Unlocked Pool

V4 必须真实跑通至少一次：

Village Project / Event Consequence
→ 获得 Capability
→ 某人物或通用 Unlocked Pool 扩大
→ 后续人物升级中的 Card Growth 三选一，以及 Run 卡牌奖励候选，均能够读取新增内容。

这条是 V0.1 验证“Village 扩大未来可能性”的关键连接。

边界：Unlocked Pool 同时服务“人物升级中的 Card Growth 三选一”与“Run 卡牌奖励候选”。本连接只要求 Unlocked Pool 扩大的内容能被这两类候选读取，不得扩大为“所有升级节点都是三选一”。

## 6.7 Exit Mode 边界

必须允许 Expedition Bridge 区分：
- SafeReturn；
- Retreat；
- Defeat。

但 V0.1 不冻结：
- Retreat 正式损失比例；
- Defeat 正式损失比例；
- 不得把当前占位行为自动写成正式规则。

## 6.8 V4 Playable Milestone A

### 高层 Product Acceptance｜已批准五问

1. 我有没有开始把村民当成具体人，而不是 Worker？
2. 抽走一个具体 Person 去做别的事情时，我会不会产生真实犹豫？
3. 长期安排有没有让 Village 产生可感知的不同反应？
4. 选择六人远征时，我会不会考虑“他们是谁”，而不仅是战斗数值？
5. 远征回来以后，我会不会想看看家里和这些人又发生了什么？

### Operational Checklist

以下操作清单仅用于验证上述五问，不构成第二套平行 Milestone A 定义：

人工体验至少覆盖：

1. Village 时间与人物持续运行；
2. 玩家安排人物；
3. 至少一个 Assignment / Project；
4. 时间推进；
5. 长期事实经 Pattern / Recognition 形成可感知 Social Response；
6. Village Event 出现；
7. Choice 产生可见后果；
8. 玩家选择符合 Temporary Occupancy 条件的人物出征；Active Project 占用者不得同时出征；
9. Existing Battle / Run 正常进行；
10. SafeReturn；
11. HP / Exp / Pending 收益正确回流；
12. Village 状态变化；
13. Capability 至少一次改变 Unlocked Pool / 后续 Run 可能性；
14. 下一次远征能够实际读取该变化。

完成 Operational Checklist，并以五问完成高层 Product Acceptance 后：
**Village Playable Milestone A**

---

# 7. Do Not Build List — V0.1

V0.1 明确不进入：

- 完整生产链；
- 多层建筑升级；
- 正式建筑布局；
- 多资源复杂经济；
- 完整 Policy Tree；
- 完整 Culture System；
- 信仰系统；
- 知识系统；
- 意识 / 精神 / 质疑点；
- 完整人物关系网；
- 婚姻 / 家庭；
- 完整职业转换；
- 完整 Training System；
- 完整 Research Tree；
- 完整装备系统；
- 大规模 Village Event 内容；
- 正式篇章内容；
- 正式经济数值；
- 完整离线模拟；
- 完整派系 / 议会 / 投票系统。

---

# 8. 当前未冻结清单

以下均不得由开发自行决定为正式规则：

- 正式全量 Resource 种类（FP-V0.1 最低集 = Ingredients + Food 已确认，见 §2.6；不恢复旧四资源体系）；
- BuildingMaterial / 建材的正式长期落点、用途及是否属于 Village 正式资源体系；
- 正式资源消耗数值（消费 / Fulfillment / Hunger 语义见 `Village-Resource-Coupling-GameSpec-V0.1.md`，数值仍不冻结）；
- 正式时间倍率；
- 远征节点耗时；
- Assignment 正式类别；
- Project 正式类别；
- Project 多人协作；
- Project 取消资源返还；
- Suitability 正式结构；
- 是否需要正式 Trend Runtime / 列表；
- 若未来建立 Trend Runtime，其信息是否显示精确数值；
- Event 正式频率；
- Event 正式内容；
- Policy 正式系统；
- Culture 正式系统；
- Relationship 正式系统；
- Capability 最终命名；
- Capability 正式内容；
- Retreat / Defeat 损失；
- Experience 正式玩法；
- 正式 Village UI / 美术；
- 长期稳定角色身份的最终命名，以及 Formation 的正式内容、阈值、数值、Transition / Dormant；
- Profession Formation Source Hook 的正式字段、权重、算法与命名；
- Profession Trait 的正式结构、效果及其与 Person-specific Trait 的关系；
- 所有正式数值。

---

# 9. 开发执行规则

1. 开发按 V1 → V2 → V3 → Minimum Village World Integration → V4 顺序推进。
2. 不允许提前实现下一 Stage。
3. 每个 Stage 完成后先验证，再继续。
4. V1–V3 必须完成适用的 Technical Verification；若对应 Work Package 定义了 Closure Gate，还必须完成其规定的人工产品确认后，才能进入 `Development Closed`。
5. V4 才形成 Village Playable Milestone A；此前 Stage 的必要 Closure Gate 不构成另一套 Milestone A。
6. 所有 Demo / Test 内容必须明确标记。
7. 当前已有 Battle / Growth / Adventure 逻辑优先复用，不重复造系统。
8. 发现 GameSpec 无法回答的真实规则冲突时，停止并上报，不自行推断。

---

# 10. 下一设计入口

Village V0.1 系统骨架完成后，设计侧可继续推进：

- Village 玩家认知与信息架构；
- 首页信息优先级；
- 长期 Pattern / Social Response 的可感知方式；
- Person 状态与经历呈现；
- 事件打断强度；
- 红点 / 弱提示 / 强提醒规则；
- Village 视觉语言与场景化表达。

这些不应反向改变本 GameSpec 的核心职责，除非后续形成新的正式设计决策。

---

# 11. 附录：Minimum Village World Integration——第一包已批准边界（V3 后 / V4 前前置检查点）

> 归位日期：2026-09-04（制作人裁决）。
> 本章只同步**已批准的第一包职责边界与产品验收**，不扩展新玩法；实现候选一律为 **Supporting Candidate / Test / Not Frozen**。
> 对应概念证据：`Docs/DesignCheckpoints/2026-09-03_Minimum-Village-World-Integration_Concept-Checkpoint-V0.1.md`；除本章已归位内容外，该 Checkpoint 的其余 World 内容不自动授权。

## 11.1 已批准职责边界（Confirmed Architecture）

第一包第一证明目标：

1. Person 在 Village World 中具有可识别 Presence；
2. Assignment / Active Project 能锚定到最小 Place；
3. 一个真实 Project 完成后，World 留下可持久化的可见痕迹；
4. Save / Load 后该世界变化仍存在。

职责分工：

- **World 负责**：Presence；Place Anchor；Persistent World Change 的最小空间承载。
- **UI 继续负责治理**：Roster、Assignment、Project、Social Response 与其它现有管理面板。

实现硬约束（违反即偏离本规格）：

- Person Presence 必须从现有 Person / Assignment / Project / Expedition / Unavailable 真相源**派生**；不允许建立第二套 occupancy 或第二套人物长期数据。
- Project → World Change 必须复用现有 Project 完成点，只允许**单向追加式**世界痕迹；不新增第二套完成判定。
- Save 只保存**不能从现有 Runtime 派生**的最小 World / Place 状态；旧档兼容必须保持（沿用可空区块默认初始化范式，不提升 SaveVersion）。
- 不建立第二套时间源（VillageTime 唯一权威源，见 §1.3）。

## 11.2 第一包产品验收

本前置检查点至少必须通过以下两项人工确认（经真实产品路径：现有 Town / Village 宿主 → 时间推进 → 保存 → 重新加载；自动测试通过不替代人工确认）：

1. **Person Presence**：不打开 Roster / Commitment 面板，只看 World 即可判断至少一个具体 Person 当前在哪里 / 在做什么。
2. **Persistent World Change**：一个真实 Project 完成后，World 留下可见痕迹；Save / Reload 后该痕迹仍在。

## 11.3 实现候选（Supporting Candidate / Test / Not Frozen）

以下仅为实现候选，不冻结，正式结构与命名以后续设计决定为准：

- Place Registry 的类名与数据结构；
- Workplace / ProjectSite 的内部类型或命名；
- Scene 对象布局与同场景 / Camera / Layer 承载方式；
- 坐标存储方式；
- Marker / Primitive / 色块等世界表现；
- Tooltip / 点击交互；
- 所有正式美术与地图参数。

## 11.4 第一包禁止

> 仅限本第一包；后续 Building Foundation 见 [2026-10-07 归位说明](#v16-building-stage-boundary)。本节原文和 SaveVersion 限制按历史阶段读取，不能提前承诺 V16 建筑增量无需兼容审计。

Grid / 自由摆放；正式建筑列表；正式资源经济；NPC 移动 / 导航 / routine；World Bark / 正式 Social Surface；Living Tradition / Historical Legacy；Profession Formation；V4 Home-Away / Return Anchor；新社会系统；新数值体系；第二套时间源；第二套 Occupancy；SaveVersion 提升或长期 Person 字段改动。

> **Y1 注记（2026-09-07 Cross-document Closure）**：“正式资源经济”属 Minimum Village World Integration 第一包的禁止范围（该第一包只承载 Presence / Place Anchor / Persistent World Change），不代表当前 FP-V0.1 永久禁令。FP-V0.1 已正式确认 **Ingredients → Food Minimal Survival Resource Loop** 为 Current（见 `Village-Resource-Coupling-GameSpec-V0.1.md`）。本节历史内容保留不改。

> 注：Minimum Village World Integration 的完整 World 概念（正式 Grid / 地图尺寸 / Territory / 扩张 / NPC 导航 / 建筑与美术等）不在本章批准范围，仍按 **Concept / Not Fully Frozen** 处理，不随第一包自动进入开发。

---

# 12. Autonomous Life Vertical Slice V0.1 — Approved Product Rules

> 归位日期：2026-10-04。
> Freeze：本章 = **Current Rule / Acceptance Authority**。
> 本章不冻结 Utility 数值、正式 Personality / Relationship 内容、类名 / DTO / asmdef，也**不自动授权 Runtime Work Package**。
> Product Spec：`Docs/DesignCheckpoints/2026-10-04_RRPG_Autonomous-Life-Vertical-Slice_Product-Spec-V0.2.md`。

## 12.1 Scope / Product Goal

Autonomous Life V0.1 必须证明：

> 具体 Person 会在世界提供的合法可能性中，根据自身差异、Directional Relationship 与当前 Context 自主选择 Activity；真实 Activity 产生 Semantic Fact，Fact 改变当前 Relationship 并进一步改变未来自主选择。

Gate A：

```text
Person Difference + Relationship + Context
→ Activity Selection
→ Completed Activity
→ Semantic Fact
→ Relationship Interpreter
→ Directed Relationship State
→ Future Activity Selection
```

Gate B：

```text
Significant Fact
→ Stable Historical Projection
→ History View
```

Gate B不得成为Gate A的技术前置依赖；它是后续历史表达目标，WP-AL-4不实现Recognition / Event / LifeEpisode Runtime或Save。

## 12.2 Person Availability / Time

- `PersonId` 是长期人物身份；BattleUnitId / CardId 不得建立长期社会状态。
- `VillageTime` 是唯一 Village 时间 Authority；Autonomous Activity Window 是其 Scheduling Phase，不建立第二套 Clock。
- Primary Assignment 是长期 Responsibility，不等于全天 Occupancy。
- Expedition / Unavailable / Dead与实际劳动/通勤、现有本人Claim是当前机会Hard Gate；Active Project名单不是全天门禁。
- 每名Eligible Person同一时段不能互斥参与多个Major Activity；日内多次机会可连续活动，是否可再次参与读取完成时刻/机会/Claim，而非一天只完成一次。旧daily API仅作兼容。
- Jump Month、正常Update/Tick、Expedition Return Catch-up必须经过同一日内AdvanceTo边界语义；Presentation 可以省略，Simulation 不可省略。

## 12.3 Activity Offering / Selection

- Place / Person / Context 可以提供 Activity Offering。
- Provider 负责“当前有哪些合法可能性”；Person Decision 负责“当前更想做什么”。
- 自主选择必须能够读取：
  - Person-specific Behavioral Difference；
  - Directional Relationship；
  - Current Context；
  - Recent Repetition 等合法当前状态。
- 稳定倾向不得退化为 100% 固定脚本。
- Context 可以显著改变一般行为倾向，但普通 Contextual Activity 不使用硬 `P2 > P3` 覆盖所有其它自主活动。
- 非法 Candidate 不得进入选择或被提交。
- 自主选择必须可解释、非机械、可复现，并保证多人冲突安全。

具体算法链见 [Autonomous Life Target Technical Architecture V0.1](../../Docs/DevelopmentSpecs/2026-10-04_RRPG_Autonomous-Life_Technical-Architecture-V0.1.md) §9 / §11 / §12 / §28。Utility Evaluation、Plausibility Cut、Deterministic Weighted Candidate Ordering、Deterministic Fair Arbitration 已按Confirmed技术契约实现并发布main；具体实现归[Current Architecture §25](../../Docs/Architecture/技术架构总览.md#wp-al-3-current)，算法不升级为产品数值规则。具体分数、Curve、阈值为 Calibration / Not Frozen；算法与 tie-break 的 WP-AL-3 目标选择见 §28，不能混为正式产品数值。

### 12.3.1 WP-AL-3 — Confirmed Product Rules

Possibility = 世界是否提供，Eligibility = 规则是否允许，Plausibility = 具体人物在当前情境下是否可能想做。三层分离；非法活动直接拒绝，不用极低 Utility 伪装。相似条件下行为有稳定倾向，强 Context 可以覆盖一般习惯，但不能机械选最高分或退化为固定脚本。

NoMajorActivityOption（无主要活动选项）是正式决策项：“当前机会没有特别想主动做的主要活动”。它进入个人候选顺序，可作为冲突后的停止点；不是 Activity，不 Claim / Commit / 进入 FSM，不产生 Fact，不表示拒绝参与别人的活动。

Utility / Desire 只决定个人意愿顺序；Arbitration 只解决意愿冲突，不再用 Utility 高低决定胜者。单轮所有人从同一 Snapshot 形成各自 Candidate Order，且每人只生成一次；Reject / Conflict 后指针后移，不重新评价或 reroll。

Participant Acceptance 有正式接口：Accepted / Rejected（有明确原因）。Minimal Acceptance验证合法参与者、Exclusive参与可用性及无HardReject；ReceptionAtArrival目标的实际接待在到场时判定，提案接受不保证人在场；原型没有正式 HardReject 内容。完整 Target Acceptance Utility、Negotiation / Invitation 后置。NoMajor 不等于 Acceptance Rejected。

多人采用 Round-based Proposal Arbitration：未解决 Actor 提当前候选 → Acceptance → 统一 Claim Conflict 仲裁 → Winner 进入既有 Lifecycle；Loser / Reject 指针后移，直到成功、NoMajor 或耗尽。结果不能由固定 Person 更新顺序决定；不采用 Global Max-Utility Matching / maximum matching。

Decision System 与 Formation System 分离，只读取当前行为画像 / 偏好 / 关系 / Context / Recent Activity，不塑造人格。History 不整本直接扫描进每次 Utility，优先读取已经形成的 Current State，避免同一经历通过 Fact + Relationship + Life Episode 重复计权。

以上是 Product Rule / Confirmed；Snapshot、加法贡献、Plausibility Cut、加权排序与公平仲裁的具体算法属于 [Target Technical Architecture](../../Docs/DevelopmentSpecs/2026-10-04_RRPG_Autonomous-Life_Technical-Architecture-V0.1.md#wp-al-3-selection)，技术契约与实现状态分开；WP-AL-3对应子范围已发布main，Current职责查[技术架构总览](../../Docs/Architecture/技术架构总览.md#wp-al-3-current)。依据：[Design Checkpoint V0.2 Final](../../Docs/DesignCheckpoints/2026-10-04_RRPG_WP-AL-3_Autonomous-Selection_Design-Checkpoint-V0.2-Final.md) §2 / §25。

## 12.4 Unified Activity Lifecycle

所有 Major Activity 必须服从统一语义生命周期：

```text
Available / Candidate
→ Selected / Proposed
→ Conflict Resolution
→ Committed
→ Executing
→ Completed / Failed / Interrupted
→ Result
```

规则：

- Commit 前 Candidate / Proposal 不产生长期世界副作用；
- Commit 后不因 Utility 小幅变化每帧重新决策；
- Failed / Interrupted 不产生“成功完成”Fact；
- Completed 是长期副作用提交的唯一前提；
- Live Village 与 Headless Catch-up 使用同一 Simulation Contract。

## 12.5 Multi-person Conflict

- 多 Person 不能按固定 Update 顺序逐个“先选先占”；
- 一个 Activity Window 使用统一 World Snapshot；
- Eligible Person 先独立形成 Proposal / Candidate Order，再统一仲裁；
- 冲突失败者按原稳定候选顺序尝试下一候选，不无限 reroll；
- 同一个 Exclusive Target / Participant 不得被多个 Major Activity 非法重复占用；
- 决策与仲裁必须在同输入下可复现。
- Utility 不裁决冲突胜者；分轮仲裁及正式 Acceptance 接口按 §12.3.1，具体优先级 / tie-break 查 Target Architecture §28。

## 12.6 Completion / Fact / Relationship

正式关系反馈链：

```text
Activity Completed
→ Relational Fact
→ Relationship Interpreter
→ Directed Relationship State
→ Future Activity Selection
```

要求：

- Relational Fact 记录客观语义，不记录动画帧 / Transform / 技术日志；
- A→B 与 B→A 允许不同；
- Activity / Event 不得直接 `Relationship += X`；
- 同一个客观 Source Occurrence 不得重复提交造成双重关系变化；
- Relationship 当前状态必须 Save / Load 连续；
- 同一历史不得通过 Relationship + 同一 Fact + 同一 Life Episode 重复计权。

具体Relationship维度与Affinity delta数值不作为正式产品常量冻结；Social解释权及WP-AL-4反馈规则见下节，Save / Outbox已实现承载归Current Architecture，不由实现冻结正式关系数值。

<a id="wp-al-4-feedback"></a>

### 12.6.1 WP-AL-4 — Social Feedback / Historical Reciprocity（Confirmed Product Rules）

Activity / Event Producer只表达“发生了什么”，不拥有长期关系数值解释权；Production Relationship Interpreter由Social Domain单一拥有，生产调用方不得自行选择解释语义。Activity不直接修改长期Relationship，也不在Activity / Event配置中保存关系奖励delta。

V0.1 Fact Policy：

| Activity | Persistent Canonical Relational Fact | 方向反馈 |
|---|---|---|
| Rest / Drink / Talk | 无 | Talk不自动增加长期关系 |
| VisitSick | relational.visited_sick_person | Fact.Target→Fact.Actor正向 |
| Help | relational.helped_person | Fact.Target→Fact.Actor正向 |

不默认同步增加Actor→Target。Failed / Interrupted和NoMajor不产生成功关系事实。具体Affinity delta、Weak / Medium / Strong=4/8/12、VisitSick / Help=Positive Medium均为Prototype / Calibration / Not Frozen；+8不是正式产品规则。

ActivityCompletionSnapshot是ephemeral occurrence / projection input；RelationalFactRecord是唯一canonical persistent relational fact；Significant / Life Episode是下游Consumer投影，不是另外两套Fact Ledger。Producer不设置Significant=true，Outbox只存待投递payload，不成为Canonical历史。旧Product Spec中Ephemeral Lifestyle / Ephemeral Relational / Significant Relational三类术语按此生命周期读取。

历史互惠是本WP核心验收：张三VisitSick王五 → Canonical Fact → 王五→张三Affinity正向 → Save / Load → 后续张三NeedsHelp；保持其它输入相同，王五Help张三的Relationship Contribution、Final Desirability及Selection Weight均应高于无历史反馈世界。不要求Weighted Ordering必然选中Help，不把raw Fact再次叠入WP-AL-3 Utility。

新的WP-AL-3 Decision Snapshot构建前，所有更早发生且应影响关系的Feedback必须settled。未settled则不打开新Activity Window、不运行Selection；记录结构化诊断，不向外抛Day Tick异常、不replay旧日。已成功Completed的活动不能因后续Social Drain失败被伪装为Failed；保留Pending / Faulted，由下一Snapshot前Barrier处理或fail closed。

具体Projector、Prepare→Complete→Enqueue→Drain、Durable Outbox、SourceOccurrenceId、Social-owned configured interpreter、配置、Save block与Barrier归[Target Architecture §29](../../Docs/DevelopmentSpecs/2026-10-04_RRPG_Autonomous-Life_Technical-Architecture-V0.1.md#wp-al-4-feedback-target)。这些对应子范围已Published Main，Current入口见[Architecture §26](../../Docs/Architecture/技术架构总览.md#wp-al-4-current)；Recognition / Event / LifeEpisode Runtime与UI继续Deferred，不建立第二套Fact / Relationship / Clock / Activity Authority。依据：[WP-AL-4 Final](../../Docs/DesignCheckpoints/2026-10-04_RRPG_WP-AL-4_Social-Feedback_Design-Checkpoint-V0.1-Final.md)。

## 12.7 Recognition / Event

Relational Fact 可以同时被 Relationship 与 Social Response 消费：

```text
Relational Fact
├─→ Relationship Interpreter
└─→ Recognition Projection
     → Observer / Significance Gate
     → VillageRecognitionRaised
     → Unified Event Core
```

规则：

- Relationship Feedback 不依赖 Event；
- 每条 Fact 不得直接弹 Event；
- Recognition 可以来自重大单次 Fact 或 Fact Pattern；
- Observer Gate 约束 Social Response / Event，不阻止直接参与者的 Relationship Update；
- Autonomous Life Event 必须复用 Unified Event Core，不新建第二套 Event Pipeline；
- Event Outcome 若改变 Relationship，应产生新的 Semantic Relational Fact / 正式 Social Transition，不直接改 Relationship 数值。

## 12.8 Historical Projection

- `PersonLifeHistory` 继续承担 Person-owned 客观人生事实。
- Shared Relational Fact 不复制成多份 PersonLifeHistory 真相。
- `Life Episode` V0.1 作为 Derived Historical Projection；不要求 LifeEpisodeLedger / LifeEpisodeSaveData。
- 相同 Canonical Source 必须产生稳定语义投影。
- 完整纵切Gate B的稳定Episode / History View是后续目标；WP-AL-4仅做Social Feedback / Historical Reciprocity，不实现LifeEpisode Runtime / Save。Significance由下游Consumer判断，Canonical Fact不按重大程度另分Ledger。
- Life Episode 不替代 Formation Evidence，不作为 Relationship 第二修改输入。

## 12.9 Persistence / World Isolation

至少验证：

### Continuity

```text
Save
→ Load same world
→ Relationship State / required Social State 保持
→ Future Behavior 因果连续
```

### Isolation

```text
World A 有 Relationship / Fact
→ NewGame 或 Load World B
→ World A Social State 不残留
```

路径/动画进度不持久化；日内Activity的稳定计划/执行语义必须Save/Load连续，restore重建必要Claim，不重开Window或重放活动。未Completed活动不留成功Fact或半次关系变化。字段与非法恢复边界见[Save GameSpec §7](../Save/Save-GameSpec-V0.1.md)。

## 12.10 Debug Explainability

每次自主 Decision 至少可追踪：

- Actor Person；
- Candidates / Providers；
- Eligibility 与 Invalid Reason；
- Behavior / Relationship / Context / Repetition 等主要 Modifier；
- Final Desirability / Selection Weight；
- Weighted Candidate Order；
- Conflict Result；
- Committed Activity；
- Activity Lifecycle State；
- Deterministic Random Identity（若该阶段使用随机）。

Debug 表现形式不冻结，但 Explainability 数据能力属于正式开发要求。

## 12.11 History Authority Anti-Gate

当前已有：

- `VillageSocialFactLedger`
- `WorldEventHistory`
- `PersonLifeHistory`

它们职责不同，不得按名字自动合并，也不得任选一个直接充当 Autonomous Life 通用 Relational Fact Authority。

原则：

> `No Parallel Authority（禁止平行权威） != Force One Ledger（强行单一账本）`

允许不同职责 Authority 共存，但同一个客观事实只能有一个 Canonical Source of Truth。

## 12.12 Prototype Content / Not Frozen

V0.1 可使用以下最小 Test Content：

- 4 Person（WP-AL-3 原型：张三 Sociability / Supportiveness .8/.8；李四 .5/.5；王五 .8/.2 + leisure.drink +1；赵六 .2/.8）；
- 3 Place（WP-AL-3 原型：Home / Tavern / Workshop，Workshop 取代旧 Rest / Medical 测试承载候选）；
- 5 Activity：Rest / Drink-Leisure / Talk / Visit Sick / Help；
- Sociability / Supportiveness 两个测试行为轴；
- 单维 Affinity 测试关系通道。

全部明确：

> **Test / Prototype Content / Not Frozen**

原型内容选择已确认，但四人物具体值与 Activity Base Desire 仍为 Calibration / Not Frozen，不是正式角色平衡或正式村庄建筑。Activity Base Desire 的测试值见最终 Checkpoint §18；HelpNeed 可用 Test Context Fixture，不建立正式世界状态或进入 Save。完整原型以 [WP-AL-3 Final](../../Docs/DesignCheckpoints/2026-10-04_RRPG_WP-AL-3_Autonomous-Selection_Design-Checkpoint-V0.2-Final.md) 为当前范围依据。

不得因实现进入正式内容 Authority。

## 12.13 Product Acceptance

至少验证：

1. Behavioral Difference（行为差异）
2. Relationship Differentiation（关系差异）
3. Context Override（情境覆盖）
4. Historical Reciprocity（历史互惠）
5. Repetition Control（重复控制）
6. Target Conflict（目标冲突）
7. Explainability（可解释性）
8. Persistence / World Isolation（持久化 / 世界隔离）
9. Live / Headless Consistency（现场 / 无表现一致性）
10. Unified Event Integration Smoke（统一事件接合冒烟）

正式概率阈值、正式内容量、正式美术不在本章冻结。

## 12.14 Implementation Gate

Runtime Work Package 开工前必须同时满足：

- 本章 Current Rule 已归位；
- Autonomous Life Technical Architecture 已归位；
- 当前任务明确授权对应 Development Spec；
- Write Scope / Save Compatibility / Test Matrix 明确；
- 已完成WP-AL-1～4与日内A～D不是待启动包；后续实现仍须明确授权对应范围，不能从已落仓规格推断自动开工。

历史首个开发包：

> `WP-AL-1 Social Foundation（社会域基础）`

只建立 Relational Fact + Directed Relationship + Save / Load / Reset / Exactly-once Commit，不实现 Activity / Utility / Event Content / Village World Presentation。

当前实现单独查[Current Architecture](../../Docs/Architecture/技术架构总览.md)：WP-AL-1 Social、WP-AL-2 Activity / Window / Claim / FSM / ActivityWorld与WP-AL-3 Selection / DecisionWorld均已Published Main。WP-AL-4 Social Feedback已Published Main / Current Implementation；原日期化[Development Spec](../../Docs/DevelopmentSpecs/2026-10-04_RRPG_WP-AL-4_Social-Feedback_Development-Spec-V0.1.md)保留其开发期契约，不由已完成本包推导后续能力已实现。

<a id="intraday-time-a"></a>
## 13. 日内时间、职责执行与现场生活（Confirmed；Published Main / Runtime Verified）

[A V0.2](../../Docs/DevelopmentSpecs/2026-10-05_RRPG_Intraday-Time-Expedition-Arrival_Development-Spec-V0.2.md)与连续授权的[B～D 规格](../../Docs/DevelopmentSpecs/2026-10-05_RRPG_Intraday-Work-Lifecycle-Onsite_Development-Spec-V0.1.md)将本节迁移到日内语义；[实际证据](../../Docs/DevelopmentSpecs/2026-10-05_RRPG_Intraday-AD_Execution-Record.md)表明 A～D 已验证并于 2026-10-06 发布 main（`93a9e8cd6e0409ce335753221276bf88fe35f53f`）。旧 WP-AL-1～4 main 发布事实保留；§12活动正文已按本次采用的日内规则原位同步；旧发布阶段的每日/即时模型由日内实现替代，不把历史证据改写成当时已有日内生活。

VillageTime 唯一拥有整数分钟、AdvanceSession 原请求/目标/完整游标及驱动余量。TickDay 显式 +1440 分钟保留时分，Jump 到下月首日00:00，Update/Return 共用边界链，跨月 MonthTicked→DayTicked。活跃 Yield/Waiting/Failed/AwaitingFinalSave 拒绝新推进与手动世界写；返程 Pending 真 UI 成功处理并关闭结果后只续原请求，FinalSave 失败继续锁世界、只重试保存。

职责与项目名单保留长期意图/容量，执行在实际通勤到岗后及工作时段内；外出暂停而不删除，返村不重复 Join。项目优先于岗位，午休/下班不劳动；项目完成后在实际边界重新通勤原职责。旧整天产出/项目进度改为真实劳动，午夜只做日聚合/Formation 等，不再同时跑旧整日生产或瞬时完成 Activity。

自主生活采用稳定日内机会，NoMajor 也去重；Proposal→Commit 是出发计划，实际通勤后 Begin、实际参与后 Complete。Assignment、项目名单与 PersonRest 本身不构成全天自主生活门禁；真实工作/通勤、外出/死亡/Unavailable 与当前本人 Claim 才约束对应时段。刷新不清活跃实例，跨日可继续；新快照前 Social feedback 必须 settled。

定向到访不提前占目标，不远程保证接待；到达时检查实际地点与兼容，Rest 在家可接待，工作/离村不可强行打断。多人同访各自结果，一人有效参与后早退不结束其他人，未达门槛/扑空无成功 Fact；不是全员两两奖励。Known Home 不是实际在场，最近扑空仅用于可调冷却，不建立完整 Knowledge/History。现有现场面板只投影时分、地点、状态与同址人物，关闭不改变模拟；原 WP-AL-5 完整世界表现仍 Deferred。

工作/午餐/机会/通勤/持续时长/个人有效门槛/扑空冷却默认均可注入、Prototype / Not Frozen。Scene/Prefab 与工作簿未修改。
