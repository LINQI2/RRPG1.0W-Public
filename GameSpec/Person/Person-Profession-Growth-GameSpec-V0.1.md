# Person-Profession-Growth-GameSpec-V0.1

> 版本：V0.1
> 日期：2026-09-06
> 状态：Current Rule（Person / Growth / Profession Formation 基础规则已由制作人确认归位）；Formation 的数值尺度、阈值、正式内容与最终命名保持 Not Frozen
> 职责：记录 Person / Profession / Growth / Run-Return Experience 的当前可执行规则与验收边界。
> GameDesign 负责“为什么这样设计”；本文件负责“开发与验收时必须知道什么”。
> 范围：只收用户已确认的当前规则；M1–M4 的具体技术实现（结构字段形式、Resolver、存档哨兵、测试结构）不构成规则，见 §11。

---

# 1. Person 身份

- **PersonId = 具体人物稳定实例身份**；同一 Person 长期连续，贯穿 Village / Battle / Growth / Run。
- **CharacterId / HeroId = 静态模板身份**（该人物“是什么模板”），不等于实例身份，不作为人物长期真相源。
- 角色卡在长期层归属具体人物实例（Card → Person）；不同 Person 即使模板相同也不共用同一个长期 Person。

# 2. Assignment ≠ Profession

- Assignment 只描述人物当前投入什么，不自动改写人物长期身份（Assignment ≠ Profession）。
- 普通 Person 当前允许：**无 Profession**，或拥有 **1 个 Active Profession**。
- 多职业 / Dormant 的正式结构仍未冻结（见 §13）。

# 3. Profession Template = 职业共享定义

- Profession 的职业级共享定义（身份、理论卡池边界等）属于模板层。
- 模板只承载职业共享内容，**不承载任何具体 Person 的动态成长**。
- 具体 Person 的动态成长状态只属于 Person 自身，不写入共享定义。

# 4. 成长归属

以下全部是**具体 Person 的成长**，不是模板或职业的全局进度：

- Level 与 Exp（该 Person 在该职业的成长）；
- Unlocked Pool（该 Person 当前已解锁、可用的角色牌集合）；
- Profession Trait progression；
- Starter Build（该 Person 每次 Run 的起始配置）。

不存在由模板或职业共享的单一成长进度。

# 5. Trait 归位

- **当前不存在正式的“独立 Person-specific battle Trait”类别**；当前 Trait 归位为 **Profession Trait**（该 Person 当前职业身份的战斗机制表达）。
- **职业变化 / 职业休眠时，对应 Trait 同步变化 / 休眠**。
- Trait 不通过普通成长流程随意替换；不等同于可装备物、通用状态或一次 Run 获得的临时卡牌。
- Trait 的具体效果、强化节点、数值与表现方式仍未冻结（见 §13）。

# 6. Level 与主动升级

- **Level = 该 Person 当前职业的战斗成长阶段**。
- 升级**不自动增加**基础 HP / Attack / Range。
- 达到条件后由玩家**主动逐级确认**，可以延后；在经验允许时可连续确认。
- 每一级具体开放什么内容、经验曲线与等级上限未冻结（见 §13）。

# 7. 牌池分离

- **Full Card Pool（职业理论允许的完整集合）与 Person Unlocked Pool（该 Person 当前已解锁）分离**。
- 普通长期成长候选与 Run 卡牌候选**只能使用该 Person 当前已解锁池**。
- 重大特殊人生事件允许**极少数明确例外**（由未来规格定义，不自动扩散为常规规则）。

# 8. Starter Slot

- **Starter Slot 数量有限**。
- 长期卡牌奖励可以：**加入空槽 / 替换已有槽 / 拒绝**。
- **即使仍有空槽，也允许替换**（替换是合法选择，不是空槽耗尽后的兜底）。

# 9. Growth Node

- 成长节点**不要求每级都是“三选一”**。
- Growth Node 可以包含：扩槽、卡牌成长、扩池、职业特性强化等类型。
- 每级出现哪些节点与概率未冻结（见 §13）。

# 10. Run / Return 经验规则

> 本节只管辖 Person 成长经验（ProfessionExp）。金币、材料、战利品等其它 Pending 收益**不受**“Failure 完整保留 ProfessionExp”规则覆盖；其保留与损失规则由对应 Battle / Expedition 规则管辖，损失比例未冻结。

## Run 内

- `PendingProfessionExp = Σ(本人实际参战 Battle 获得的 Exp) + Σ(本人获得的 Event Exp)`。
- **随队不等于参与**：未实际参战、或未获得该 Event 的成员为 0，不因随队分摊。
- 单场胜负获得的经验相同。
- **不按输出 / 击杀 / MVP 分配经验**。

## Return

- Run 内只累计 Pending，**不写长期**；**Return 时统一把已产生的 ProfessionExp 写入该 Person 的长期成长**。
- **Normal Return 包含两类**：无限远征的主动撤退；有限关卡完成后的返回。
- **Expedition Failure 也完整保留已经产生的 ProfessionExp**（失败不清零、不没收已获得经验）。
- 第一版**不额外给予 Expedition Completion ProfessionExp**（完成远征本身不额外发经验）。

# 11. 当前规则与实现边界

- 本规格描述玩家规则与验收边界；**实现侧结构为 Test / Not Frozen**（示例：进度容器字段形式、模板定义字段形式、权威解析器、存档哨兵、M1–M4 测试结构），不因实现存在而成为规则，也不得因本规格要求特定实现结构。
- **Current Rule 不等于实现已完成**：当前代码运行方式与规则不一致时，按差异分类登记，不以实现覆盖规则。
- 卡牌奖励、Run 奖励候选等 Battle 侧玩法细节，与规则冲突部分以对应 Battle GameSpec 与本规格为准逐项核对，不自动互覆。

# 12. Profession Formation（Current Rule）

> Formation 基础规则已由制作人确认归位；数值尺度、阈值、正式内容与最终命名保持 Not Frozen（见 §13）。

## 12.1 定义

Profession Formation：一个当前**没有 Active Profession 的 Person**，经过长期真实生活、工作与事件经历后，其某一种职业身份达到足够成熟、稳定的程度，并正式成为该 Person 的 **Active Profession**。

Formation **不是 Assignment 的即时结果，也不是玩家直接点击职业**。`Assignment ≠ Profession` 继续成立。

## 12.2 Evidence 来源

Formation Evidence 的主体来自真实发生的：
- 日常有效工作；
- 日常生活 Activity；
- 随机 / 重大人生 Event；
- 家庭 / 导师 / 特殊机会相关经历；
- 玩家参与的人生方向事件。

**Assignment 本身不等于 Evidence**；它只是通常会持续产生相关实际 Activity。Person 因远征、生病、休养、特殊事件占用等未实际工作时，不应仅因岗位挂名继续获得对应 Evidence。

## 12.3 Activity → Profession 多对多

一个 Activity / Event 可以同时为多个 Profession 提供 Formation Evidence，但必须存在**可解释的主次关系**；不允许为了丰富度机械向大量 Profession 撒 Evidence。

## 12.4 Talent / Family / Ability 边界

Talent、Family Background 等正式系统**仍未冻结**。若未来存在，只作为：
- Evidence Rate Modifier；
- Evidence Mapping Modifier；
- Activity / Event Exposure Modifier。

它们不能替代真实经历，也不能直接赋予 Profession。M6 核心必须在没有正式 Talent / Family 系统时独立成立。

## 12.5 Unified Formation Strength

所有 Profession 使用**统一语义尺度**的 Formation Strength；具体技术存储尺度（0~1 / 0~100 / 0~10000）**不冻结**。

Profession 之间的差异优先通过：Evidence Source / Activity Mapping / Exposure / Event / Modifier 表达，而不是各 Profession 各自拥有不同 Formation Curve。默认统一 Qualification 语义尺度。

## 12.6 Growth / Decay Curve（方向规则）

- Formation Strength 增长：**慢 → 快 → 慢**；
- 长期没有该 Profession 相关有效 Activity 时衰减：**慢 → 快 → 慢**。

衰减只依据：距离该 Profession **上一次有效相关 Activity 已经中断多久**。不因为正在做另一个 Profession 而额外加速旧 Profession 衰减（避免双重惩罚）。不设置独立硬 Grace Period；短期中断的宽容由衰减曲线前段承担。

## 12.7 Life History 与 Formation 分离

Life History / Daily Experience 是人生事实 **Source of Record**（记录这个 Person 一生实际发生过什么）。FormationState 只保存算法需要的派生状态（Formation Strength、相关活动中断信息、Formation Target / Stability 等）。Formation **不复制完整 Life History，也不每天重放全部人生历史重新计算**。

## 12.8 Qualification / Candidate

某 Profession 的 Formation Strength 达到统一成熟区时，成为 **Formation Candidate**。Candidate：
- 可同时存在多个；
- 可逆；
- 具有轻度滞后，避免边界来回跳。

不设置 Profession-specific hard prerequisite；关键经历通过 Evidence Contribution 体现，不额外加 specialFlag 门槛。

## 12.9 Formation Window

第一个 Candidate 出现后进入 **Formation Window**。Candidate 达成当天不立即 Formation。Window 内：Evidence 继续增长 / 衰减；新 Candidate 可以出现；Candidate 可以退出；Leader 可以改变；Ambiguity 可以持续；Stability 可以建立或回落。

## 12.10 Daily Resolution

Formation 当前按**每日结算一次**。一天内 Work / Life / Event 先产生标准化 Evidence Contribution；Day End 统一：汇总当日 Evidence → 更新 Formation Strength → 更新 inactivity / decay → 更新 Candidate → 判断 Formation Target → 更新 Stability → 判断 Formation。**不在 Activity 中途即时 Formation**。

## 12.11 Target / Dominance / Ambiguity

- 只有一个 Candidate → 当前 Formation Target = 该 Candidate；
- 多个 Candidate 且有明显 Leader → Target = Leader；
- 多个 Candidate 无明显 Leader → Target = None，进入 Ambiguous。

Dominance 由 Formation Strength 的**领先幅度 + 持续时间**表达；不额外建设复杂 Trend System。Leader 改变或差距缩小时，原连续 Dominance / Stability 会被削弱。

## 12.12 Formation Stability

- Formation Target 持续一致时，Stability 增长；
- Ambiguous / 无 Target 时，Stability 缓慢下降；
- 明确转向新 Target 时，旧 Target Stability 更快下降、新 Target Stability 建立。

Stability 不参与 Strength 计算，只回答“这种身份结论已经稳定存在多久”。短期事件不能瞬间清掉长期 Stability。

## 12.13 Automatic Formation

某 Profession：已成为 Candidate、是当前明确 Target、持续获得足够 Stability → **自动 Formation**；玩家不需要再次确认。因为过去多年的 Assignment / Life 已经构成长期选择。

## 12.14 Long-term Ambiguity / Life Direction Event

多个 Profession 长期势均力敌、始终无法自然建立稳定 Target 时，可以进入 **Life Direction Event**。玩家**不是直接指定 Profession**，可以选择：推向方向 A / 推向方向 B / 不干预（让 Person 自己决定）。

玩家选择本身作为一次重要 Life Event，为对应 Profession 提供较大的 Formation Evidence Contribution。之后仍回到统一 Formation Resolver：

```text
Event → Evidence → Strength → Target → Stability → Formation
```

**不允许出现“点击选项直接写 Active Profession”的旁路**。人生方向事件未来可再次发生，但只能建立在长期 Ambiguity 仍未自然解决的前提下，不能短期重复刷。

## 12.15 Formation 初始化

首次 Profession Formation 时：
- Active Profession = 对应 Profession；
- Profession Level = 1；
- ProfessionExp = 0；
- Profession Trait = 该 Profession 固定初始 Trait；
- Initial Unlocked Pool = 该 Profession 固定配置；
- Initial Starter Build = 该 Profession 固定配置；
- Formation Evidence 不清零；
- 写入 Life History；
- Formation 完成后立即具备 Battle Access；
- Assignment 不自动改变。

Formation Evidence 高低**不得**转化为额外初始战力、额外卡牌、额外 Trait 强度或额外 Level。禁止通过故意拖延 Formation 获得额外奖励。

Authority：Initial Starter / Initial Unlocked Pool / Profession Trait Definition 属 **Profession Template 的共享初始配置**；Person 只持有自己的 Trait progression / Unlocked Pool / Starter 等动态状态。

## 12.16 Formation 后 Evidence 生命周期

首次 Profession Formation 后：
- 所有 Profession 的 Formation Evidence 继续随未来生活增长 / 衰减；
- 不删除其它 Profession Evidence；
- 玩家在 Ambiguity Event 中未推动的其它 Evidence 也不惩罚、不清除。

但 M6 的首次 Formation Candidate / Stability Resolver **不再负责自动替换 Active Profession**。Future Profession Transition 未来可以读取同一套 Evidence。

## 12.17 Future Profession Transition Boundary

未来允许存在**少量、长期人生导致的 Profession 转向**。当前仅保留设计方向：
- 新身份长期、压倒性成立时，未来可以考虑自动转变；
- 旧身份与新身份都高度成立时，职业替换更敏感，可由人生方向事件介入。

M6 **不实现**：Transition Resolver；自动转职；Dormant；旧职业历史数据结构；ProfessionProgress 切换；Starter 恢复 / 重新选择；Trait 切换规则；转职次数限制。

## 12.18 Formation 后 Village 影响

Formation 本身不默认提供普通生产效率数值 Buff。Profession 未来可以影响：Profession-specific Activity / Event / Opportunity / Capability。即：Formation 前，生活塑造 Profession；Formation 后，Profession 可以反过来改变未来生活可能性。Capability System 本身仍 Not Frozen。

## 12.19 玩家可见性

Formation 内部算法默认隐藏；不直接展示 Formation Strength 数值、Candidate Bar、Stability、Dominance Gap、Formation Probability。原则：**算法可以隐藏，原因不能隐藏**。玩家通过长期可观察的人生过程理解原因。Formation 时应有轻量 Life History / narrative 表达。

## 12.20 Persistence

Formation 是十几年级长期过程：**Formation Strength 与继续算法所必需的长期状态必须 Save / Load 连续**。具体 DTO / field / SaveVersion 处理属于开发方案，不在本轮规则文档冻结技术结构。

## 12.21 M6 V0.1 与 Village Work 边界

正式 Daily Work System 尚未完整冻结；M6 V0.1 **不建设完整 Work System**。当前只要求：标准化 Evidence Input；Daily Formation Resolver；Persistence；Formation 初始化；必要的测试输入。如果现有 Village Assignment 有稳定、安全的真实宿主，可以接一个最小 Hook；**不允许为了接 M6 顺手冻结或重构完整 Village Work Simulation**。

# 13. 仍未冻结（Not Frozen）

以下仍不得由开发自行决定为正式规则：

### Formation（§12）内仍 Not Frozen

- Formation Strength 实际数值尺度（0~1 / 0~100 / 0~10000 形式）；
- Qualification Threshold；
- Candidate hysteresis 具体数值；
- Dominance Gap；
- Stability Duration；
- Stability 增减速度；
- Long Ambiguity 触发时长；
- Life Direction Event 具体 Evidence 数值；
- Work → Evidence 正式数值；
- Event → Evidence 正式数值；
- Talent / Family / Ability 正式系统（若存在只可能作 Evidence Rate / Mapping / Exposure Modifier）；
- Future Profession Transition（仅保留 §12.17 设计方向，M6 不实现）；
- Dormant / 多 Active Profession 正式结构；
- 正式年龄门槛。

### 全局仍 Not Frozen

- 正式职业数量与正式职业内容；
- 正式经验曲线 / 升级所需数值；
- 正式卡牌数量与卡池规模、权重；
- Trait 具体效果与数值；
- Capability System；
- Profession 最终命名（命名仍可调整）；
- 任何正式数值；
- SaveVersion 提升或长期 Person 数据结构的正式变更。
