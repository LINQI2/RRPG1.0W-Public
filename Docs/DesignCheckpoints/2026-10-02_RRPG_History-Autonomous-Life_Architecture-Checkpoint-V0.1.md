# RRPG History × Autonomous Life Architecture Checkpoint V0.1

> 日期：2026-10-02  
> 项目：RRPG  
> 类型：Architecture Checkpoint（架构检查点）  
> 状态：Architecture Near Closure / Not GameSpec Frozen（架构接近收口 / 尚未冻结为正式规则规格）  
> 目的：统一此前分散在 Person、Village、Expedition、Social Response、Event、Profession Formation 等设计中的 `Fact（事实）`、`Experience（客观经历记录）`、`History（历史）` 与 `Autonomous Life（自主生活）` 思想；高层聚合历史单元暂称 `Life Episode（人生经历片段）`，用于下一阶段产品验证。\
> 注意：本文不冻结正式数值、字段、Utility AI 公式、Relationship 维度、Personality 维度、Activity Block 时长或 Runtime 类结构。
> 文档兼容修正：2026-10-02；`Experience` 保留现行 V3 的客观人生事实语义，`Life Episode` 是高层候选工作术语（Not Frozen）。本文仍为 Concept / Not Fully Frozen，不改变 Active Person / Village GameSpec 或提升 Candidate。

---

## 1. 本轮要解决的问题

此前项目已经形成以下顶层方向：

- Person 是长期连续的具体人物，不是抽象 Worker。
- 玩家管理责任与重大承诺，而不是逐分钟控制每个 Person。
- Place 提供 Activity 的可能性，Person / Commitment 决定是否参与。
- Autonomous Life 采用低频、离散触发，不采用每帧全员持续复杂决策。
- Activity 完成后可以产生 Fact / Effect。
- Social Response 采用 Fact-first 思路：长期世界事实经过 Pattern、Observer、Recognition 后形成回应。
- Expedition 与 Village 必须保持 Person / Time / History 连续性。
- Profession Formation 应读取真实人生事实，而不是直接把岗位或次数当职业经验条。

此前未解决的主要缺口：

1. Person 如何从多个 Activity Candidate（活动候选）中选择。
2. Activity Result（活动结果）何时转成 Fact。
3. 高频 Fact 如何聚合成 Life Episode（人生经历片段）。
4. Shared Life Episode（共同人生经历片段）如何支撑关系涌现。
5. 各 Consumer（消费者）应读取历史的哪一层。
6. 如何避免所有系统都直接扫描 Runtime Data 或 Fact 流水账。
7. 如何让人物自主行为真正改变未来，而不是只做氛围动画。

---

## 2. 核心产品目标

本架构不是为了模拟真实人脑，也不是为了建立完整《The Sims》级人生沙盒。

目标是让 RRPG 的人物与共同体产生以下五类 `History Value（历史价值）`：

### 2.1 Causal Echo（因果回响）
玩家能够感受到：

> “现在发生这件事，是因为以前真的做过那件事。”

### 2.2 Person Becoming（人物形成）
玩家能够理解：

> “这个人现在变成这样，是因为他真实经历过这些事情。”

### 2.3 Autonomous Life（自主生活）
玩家能够感受到：

> “即使我没有直接下命令，这些人也在继续生活。”

### 2.4 Relational Emergence（关系涌现）
玩家能够感受到：

> “他们之间的关系是共同经历和互动逐渐形成的，而不是开局写死。”

### 2.5 Historical Path Dependence（历史路径依赖）
玩家能够感受到：

> “这个共同体因为过去不同，所以现在与未来都不同。”

横切验收原则：

### Narrative Legibility（叙事可读性）

任何重要结果最终都必须尽量让玩家能够理解：

> “为什么会这样？”

内部算法可以复杂，但玩家侧必须存在足够清晰的 Narrative Evidence（叙事证据）。

---

## 3. 顶层闭环

RRPG 的人物生活与历史系统当前统一为：

```text
Commitment / Schedule
（承诺 / 日程）
        ↓
Activity Offering
（活动供给）
        ↓
Person Activity Selection
（人物活动选择）
        ↓
Activity Execution
（活动执行）
        ↓
Activity Result
（活动结果）
        ↓
Fact
（事实）
        ↓
Pattern
（模式）
        ↓
Life Episode
（人生经历片段，候选）
        ↓
History
（历史）
        ↓
Interpretation
（解释）
        ↓
Relationship / Profession / Recognition / Event / Tradition / Opportunity
（关系 / 职业 / 认识 / 事件 / 传统 / 机会）
        ↓
改变 Future State / Future Possibility
（未来状态 / 未来可能性）
        ↓
影响下一次 Person Decision
（人物下一次决策）
```

核心含义：

高层历史聚合可采用 `Fact → Pattern → Life Episode`，或由 `Significant Fact → Life Episode` 直接形成片段；不是所有 Fact 都必须经过 Pattern。这是候选历史读取路径，不改变 V3 Social Response 链路，也不替代 Profession Formation 的标准化 Evidence 输入（见 §14.1 / §14.3）。

> 人物是谁 → 做什么 → 经历什么 → 变成谁 → 下一次更可能做什么。

---

## 4. Autonomous Life（自主生活）架构

### 4.1 三层控制模型

#### Direct Control（直接控制）
玩家直接决定重大责任与承诺，例如：

- Assignment（岗位）
- Expedition（远征）
- Project（项目）
- 重大公共决策

#### Soft Influence（软影响）
玩家通过环境与规则改变人物行为倾向，例如未来可能包括：

- 工时
- 场所开放
- 公共优先级
- 制度 / 政策
- 资源条件

#### Autonomous Life（自主生活）
Person 自主决定大量日常与非关键行为。

核心原则：

> **玩家控制责任，不控制每一分钟。**

---

## 5. Person Activity Selection（人物活动选择）V0.1 候选

第一代不采用单一万能 AI，而采用分层轻量混合结构。

### 5.1 Eligibility Gate（可执行性门槛）

先判断 Activity 当前是否可执行。

例如：

- Place 是否开放；
- Target Person 是否存在且可互动；
- Person 是否被 Expedition / Project / Assignment 强占用；
- Capacity 是否允许；
- 当前状态是否禁止该行为。

不满足则直接排除，不进入评分。

### 5.2 Priority Layer（事务优先层）

不同活动不全部放进同一张分数表。

第一版建议区分：

- P0 Hard Commitment（硬性责任）
- P1 Critical Response（紧急响应）
- P2 Contextual Opportunity（情境机会）
- P3 Daily Autonomous Life（日常自主生活）

Person 只在当前最高有效层级内部进行自由选择。

### 5.3 Utility Evaluation（效用评价）

第一版候选输入保持最小：

- Base Preference（基础偏好）
- Personality Modifier（性格修正）
- Relationship Modifier（关系修正）
- Context Modifier（情境修正）
- Recent History Modifier（近期历史修正）

原型表达可近似：

```text
Utility
=
Base Preference
+ Personality
+ Relationship
+ Context
+ Recent History
```

此式仅用于原型理解，不是正式数值公式。

### 5.4 Candidate Cut（候选截断）

明显低于当前合理区间的 Activity 不参与最终选择，避免“完全不合理行为因为随机而发生”。

### 5.5 Weighted Random（加权随机）

不采用“永远选择最高分”。

在高分候选中按效用权重进行有限随机，使人物：

- 有稳定倾向；
- 但不会像脚本机器人一样每次做同一件事。

### 5.6 Commitment / Inertia（执行承诺 / 行为惯性）

Person 一旦开始 Activity，正常情况下应继续执行。

只有以下情况允许显著打断：

- 更高 Priority Layer 出现；
- 当前目标失效；
- Place / Target 失效；
- 世界发生重大变化；
- 玩家直接改变重大责任。

避免 Utility 分数轻微波动导致人物反复转向。

---

## 6. AI 技术职责边界

第一代 RRPG 不选择“一个 AI 算法统治所有人物行为”。

### 当前候选职责

- `HFSM（Hierarchical Finite State Machine，分层有限状态机）`
  - 负责大状态与执行阶段组织。
- `BT（Behavior Tree，行为树）`
  - 可负责已经决定 Activity 后的执行逻辑。
- `Utility AI（效用型 AI）`
  - 当前最强候选：决定自由时间内“现在更想做什么”。
- `Smart Object / Activity Provider（智能对象 / 活动提供者）`
  - Place / Person / Facility 向世界提供可用 Activity。
- `GOAP（Goal-Oriented Action Planning，目标导向行动规划）`
  - 当前不需要；保留给未来复杂多步骤自主目标。
- `HTN（Hierarchical Task Network，分层任务网络）`
  - 当前不需要；未来若出现长期个人目标，可能比 GOAP 更适合 RRPG。
- `LLM（Large Language Model，大语言模型）`
  - 第一代不拥有 Simulation Authority（模拟权威）。
  - 可在未来用于叙事文本、人物表达、日记、事件文案与历史摘要。

原则：

> **确定性系统决定世界发生什么；LLM 可以帮助世界把已经发生的真相说出来。**

---

## 7. Activity（活动）最小语义

Activity 在设计层至少应能表达：

- Actor（行为者）
- Target（目标，可选）
- Place（场所）
- Activity Type（活动类型）
- Eligibility（可执行条件）
- Participant Requirement（参与者需求）
- Start / Completion（开始 / 完成）
- Context（情境）
- Result（结果）
- Fact / Effect Hook（事实 / 即时效果接口）

具体 Runtime 字段与接口未冻结。

---

## 8. Activity Result → Fact（活动结果→事实）

### 8.1 Fact 的定义

`Fact（事实）`：

> 已经真实发生、具有世界语义、可被未来系统或玩家理解的事实。

Fact 不等于所有 Runtime Data。

例如：

- “张三在李四重伤期间主动探望李四”可以是 Fact。
- “张三导航距离 17.2 米”不是 Fact。
- “HP = 17”不是 Fact。
- “张三在狼群袭击中重伤归来”可以是 Fact。

### 8.2 三种结果

Activity Result 经过 Fact Compiler（事实编译器）后可进入：

#### A. Ignore（忽略）
没有长期或模式价值。

#### B. Ephemeral Fact（短期事实）
单次意义较弱，但可能参与后续 Pattern 检测。

#### C. Significant Fact（重大事实）
单次事件本身已经值得长期知道。

---

## 9. 第一版 Fact Family（事实家族）候选

Fact 按“世界语义”分类，不按 Battle / Village / Expedition 来源分类。

### 9.1 Participation Fact（参与事实）
谁实际参与了什么。

### 9.2 Decision / Commitment Fact（决策 / 承诺事实）
谁作出了什么具有长期意义的决定或承诺。

### 9.3 Relational Act Fact（关系行为事实）
谁对谁做了什么具有社会意义的行为。

### 9.4 World Change Fact（世界变化事实）
世界发生了什么长期或永久变化。

### 9.5 Transition / Loss Fact（转变 / 失去事实）
某个重要状态何时发生转折。

此分类仍为 V0.1 架构候选，不是最终 GameSpec 枚举表。

---

## 10. Fact → Pattern（事实→模式）

`Pattern（模式）`：

> 一段时间内反复出现、具有语义的规律。

Pattern 的职责是压缩大量普通 Fact，而不是建立一套隐藏万能评分条。

例如：

- Frequent Drinking（频繁饮酒）
- Repeated Temporary Commitment（反复临时抽调）
- Frequent Co-presence（频繁共同活动）
- Long-term Farm Participation（长期参与农耕）

Pattern 可以表达：

- 时间范围；
- 发生次数；
- 频率；
- 参与人物；
- 相关 Place；
- 当前是否仍持续。

但 Pattern ≠ Life Episode；Experience 继续表示现行 V3 的客观人生事实，不与高层片段混用。

---

## 11. Life Episode Formation（人生经历片段形成，候选）

`Life Episode（人生经历片段）`：

> 由一个重大 Fact，或一组相关 Fact / Pattern 在特定人物、关系、主题与时间范围内聚合形成的高层历史单元；能够自然回答“这个人曾经历过什么”。

这是本 Checkpoint 的候选工作术语，仍为 **Not Frozen**。现行 [Village GameSpec](../../GameSpec/Village/Village-GameSpec-V0.1.md) §5.2.3 的 `Experience（经历）` 继续表示客观人生事实，例如“张三今年参与了四次 Village Project”，不被重新定义为高层人生片段。

候选分层为 `Runtime Data → World Fact / Life Fact → Pattern → Life Episode → Life History / Community History`；重大 Fact 可直接形成 Life Episode，不强制经过 Pattern。

第一版形成路径分三类。

### 11.1 Pattern Life Episode（模式型人生经历片段）

大量重复普通 Fact 经聚合形成。

例如：

> “张三青年时期连续两个季节参与村庄耕作。”

用于压缩日常生活。

### 11.2 Shared Life Episode（共同人生经历片段）

多个 Person 围绕同一关系主题、情境或重大事件形成共同历史。

例如：

> “李四养伤期间，张三长期陪伴并照顾他。”

原则：

> 同一 Shared Life Episode 只保留一个 Canonical Life Episode（同一片段的唯一记录），多个 Person 以不同 Role（角色）引用。

避免为同一件事建立互相独立的两套历史。

### 11.3 Milestone Life Episode（里程碑人生经历片段）

重大单次 Fact 可直接形成 Life Episode。

例如：

- 第一次远征；
- 被救一命；
- 同伴死亡；
- 严重背叛；
- 首次重大外部发现；
- 灾难中的唯一生还。

重大 Fact 可作为 Anchor Fact（锚点事实），吸收同一情境附近的普通 Fact，形成完整人生片段。

---

## 12. Life Episode Lifecycle（人生经历片段生命周期，候选）

Life Episode 可以经历：

```text
Candidate（候选）
↓
Ongoing（进行中）
↓
Closed（结束）
↓
Historical（历史记录）
```

例如：

“张三长期务农”可能先处于 Ongoing，换岗或季节结束后转 Closed，再成为长期 Life History 的一部分。

---

## 13. History（历史）不是第三套重复数据库

### 13.1 Life History（人生历史）

现行 [Person GameSpec](../../GameSpec/Person/Person-Profession-Growth-GameSpec-V0.1.md) §12.7 中，Life History / Daily Experience 是人生事实 Source of Record。以下只描述未来高层历史视图的候选组织方式，不替代当前客观记录职责。

高层视图优先组织为：

> 某个 Person 所引用的 Life Episode + Major Transition 的历史视图。

不是重新复制一套事实。

### 13.2 Community History（共同体历史）

优先定义为：

> 从已有 Life Episode / Transition 中筛选共同体尺度重要内容的历史视图。

Person History 与 Community History 应尽量共享同一历史真相源。

---

## 14. Consumer Contract（消费者读取契约）

总原则：

> **Use the Highest Semantic Layer Possible（尽量读取最高且足够使用的语义层）。**

在不绕过现行 GameSpec 输入契约的前提下，能读 Life Episode，就不要重新扫描大量 Fact。\
能读 Pattern，就不要重新统计 Runtime Data。

### 14.1 Profession Formation（职业形成）

Profession Formation 当前继续服从 [Active Person GameSpec](../../GameSpec/Person/Person-Profession-Growth-GameSpec-V0.1.md) §12.2 / §12.7 / §12.10 / §12.14：真实发生的 Activity / Event 产生标准化 `Formation Evidence Contribution（身份形成证据贡献）`，并按日进入 `Formation Resolver（身份形成解析）`。

```text
真实 Activity / Event
→ Formation Evidence Contribution
→ Day End 汇总 / Daily Formation Resolver
→ Formation Strength / Candidate / Target / Stability
→ Formation
```

`Life History / Experience（人生历史 / 客观经历记录）`承担事实追溯、解释与未来扩展职责，不要求 Formation 每次直接扫描或重放完整历史。未来若存在 `Evidence Summary（证据摘要）`，只能作为兼容现有 GameSpec 的聚合 / 调试 / 追溯承载，不得建立绕过 Formation Evidence 的旁路；Life Episode 也不成为新的 Formation Runtime Authority。

避免直接把每日 Activity 次数作为职业经验条；本节不新增数值、阈值或字段。

### 14.2 Relationship（关系）

可读取：

- Recent Relational Fact（近期关系事实）
- Shared Life Episode（共同人生经历片段）
- Recent Pattern（近期互动模式）

关系状态是解释结果，不是历史事实本身。

### 14.3 Social Response（社会回应）

继续服从 [Active Village GameSpec](../../GameSpec/Village/Village-GameSpec-V0.1.md) §5.1 / §5.2，保持 V3 Current Rule 的读取方向：

```text
World Fact
→ Pattern
→ Observer
→ Recognition
→ Response
→ New Decision
→ New World Fact
```

这里的 Response 是社会回应的概括；GameSpec §5.1 展开的 `Recognition → Signal → Consequence → New Decision` 继续有效，不由本 Checkpoint 改写。

Social Response 可以读取与当前 Pattern 相关的 Life History / Life Episode 作为上下文，但不改变 V3 链路，也不要求 Pattern 从高层 Life Episode 反向生成。候选历史聚合的 `Fact → Pattern → Life Episode` / `Significant Fact → Life Episode` 与 V3 Social Response 是不同读取路径，不建立循环 Authority。

### 14.4 Recognition（认识）

读取相关：

- Pattern
- Life Episode（人生经历片段，候选上下文）
- 当前处境

Recognition 是解释，不是客观历史。

### 14.5 Event（事件）

可以读取：

- Significant Fact（重大事实）
- Recognition（认识）
- Life Episode（人生经历片段，候选）
- Derived State（派生状态）

Event 是历史的显化出口之一，不是历史真相源。

### 14.6 Living Tradition（活传统）

优先读取：

- Community Pattern（共同体模式）
- Life Episode（人生经历片段，候选）
- Transition（转变）
- Practice / Teaching（实践 / 传授）

不直接扫描每一次普通行为。

### 14.7 Opportunity（机会）

读取：

- History
- Derived State
- 当前世界条件

过去决定当前有哪些未来可能性。

### 14.8 Utility AI（效用型 AI）

主要读取：

- 当前状态
- Personality（性格）
- Relationship State（关系状态）
- Recent Pattern（近期模式）
- Relevant Memory Summary（相关历史摘要）
- Context（当前情境）

不要每次 Activity Selection 都扫描整个人生历史。

### 14.9 LLM（大语言模型）

未来主要读取：

- 已确认 Fact
- Relevant Life Episode（相关人生经历片段，候选）
- Relationship Context
- Current State / Event

LLM 可以叙述、总结、表达，不直接创造未经模拟系统确认的世界真相。

---

## 15. 写权限边界

### 15.1 Producer（生产者）

负责产生新的 Fact。

来源可以包括：

- Activity
- Battle
- Expedition
- Project
- Event Choice
- World Transition

### 15.2 Compiler（编译器）

可以：

- Fact → Pattern
- Fact / Pattern → Life Episode（重大 Fact 可直接形成片段）
- 压缩或淘汰短期 Fact
- 保留 Evidence Summary

### 15.3 Interpreter（解释器）

例如：

- Relationship
- Profession
- Recognition
- Tradition

默认：

> 读取 History → 更新自己的 Derived State。

这是高层候选解释职责；Profession Formation 的正式输入与每日解析继续按 §14.1 和 Active Person GameSpec 执行。

不得回头重写过去。

### 15.4 Transition Recorder（转变记录器）

当 Derived State 出现具有世界意义的正式变化时：

> 产生一条新的 Transition Fact。

例如：

```text
真实 Activity / Event
↓
Formation Evidence Contribution
↓
Daily Formation Resolver（按 Active Person GameSpec）
↓
形成 Hunter 身份
↓
Transition Recorder
↓
“张三正式成为村庄猎人”
↓
进入新的历史
```

这不是循环污染，而是时间继续向前产生新历史。

---

## 16. Simulation Authority ≠ World Presentation

人物在世界中的表现不能反向决定模拟是否成立。

例如：

- Utility AI 已决定 Visit LiSi。
- Navigation（导航）负责走过去。
- Animation（动画）负责表现探望。
- Simulation Authority 负责判断 Activity 是否真正完成。

不得因为 Sprite 是否准确走到某个像素位置，反向成为唯一业务真相。

世界表现负责：

> 让玩家看见正在发生的生活。

模拟权威负责：

> 决定世界实际上发生了什么。

---

## 17. 第一代 Autonomous Life Vertical Slice（自主生活纵切）建议

目标不是建立完整社会模拟，而是验证：

> 几个有明显差异的人，在玩家不干预的情况下，会因为 Personality、Relationship 与 Context 选择不同活动；行为会留下 Fact，并至少一次改变未来互动。

建议最小内容：

### Person
- 3–5 名具体 Person

### Place
- Home（住宅）
- Tavern / Common Place（酒馆 / 公共场所）
- Medical / Rest Place（医疗 / 休息场所）

### Activity
- Rest（休息）
- Drink（饮酒）
- Talk（交谈）
- Visit Sick Person（探望伤员）
- 可选 Help / Walk（帮助 / 散步）

### Personality
第一版只测试极少数行为轴，例如：

- Sociability（社交倾向）
- Caring（关怀倾向）

### Relationship
第一版只需要最小 Closeness（亲近程度）或同等薄层状态。

### 时间
至少存在一个 Free Activity Block（自由活动时间块）。

### 验证案例

```text
李四受伤
↓
进入自由活动时间
↓
世界提供 Rest / Drink / Talk / Visit
↓
张三因 Personality + Relationship + Context 更倾向 Visit
↓
张三完成探望
↓
生成 Relational Fact
↓
形成 Shared Life Episode 或更新相关 Pattern
↓
Relationship State 变化
↓
未来李四更可能主动帮助张三
```

如果该闭环成立，第一代 Autonomous Life 核心价值即得到初步证明。

---

## 18. 当前明确不做

第一代不因为本架构提前建立：

- 完整 Needs（需求）系统；
- 完整 Mood（情绪）系统；
- 完整家庭系统；
- 恋爱 / 婚姻完整模拟；
- 大规模 Relationship 多维模型；
- GOAP / HTN 正式实现；
- LLM Simulation Authority；
- 每帧全员复杂决策；
- 所有 Fact 永久保存；
- 统一万能 Importance Score（重要性评分）；
- 全部 Village 内容一次性接入 Autonomous Life；
- 完整厨房、酒馆、医疗、政策、派系等横向系统。

---

## 19. 当前已否决或降级方案

### 已否决
- 所有系统直接读取大量 Runtime Data。
- 每个领域分别建立 Battle Fact / Village Fact / Expedition Fact 等互不兼容历史语言。
- 用单一隐藏大分数解释人物全部行为与人生意义。
- 所有活动都永久进入 Life History。
- Personality 直接写死行为。
- Place 主动“抢 Person”执行活动。
- LLM 决定正式世界事实。

### 降级 / 后置
- GOAP
- HTN
- 完整 Relationship
- 完整 Personality
- 完整 Needs / Mood
- 完整家庭与恋爱
- 大规模 NPC AI
- 复杂长期个人目标

---

## 20. 当前验证结果

### 20.1 与既有 RRPG 设计兼容

本轮没有推翻此前架构，反而把以下分散设计接成统一闭环：

- 2026-09-06 Village Future Design Envelope 中的 Place / Activity / Autonomous Life；
- V3 Social Response 的 Fact-first / Pattern / Observer / Recognition；
- V4 Home ↔ Expedition 的 Person / Time / History Continuity；
- Person / Profession Formation 的 Life History / Evidence Source；
- Village Long-term Build 的 Historical Path Dependence / Living Tradition。

### 20.2 最小行为模型验证方向

当前最强候选为：

> Eligibility Gate  
> → Priority Layer  
> → Utility Evaluation  
> → Candidate Cut  
> → Weighted Random  
> → Commitment / Inertia

该模型满足：

- 人物存在长期偏好；
- 情境可以压过日常性格；
- 不永远机械选择最高分；
- 近期重复可被抑制；
- 行为完成后可进入 Fact / History 闭环。

正式数值与曲线仍需独立原型验证。

---

## 21. 尚未解决的问题

以下内容仍未冻结：

- Personality 正式维度与生成方式；
- Relationship 正式维度；
- Utility AI 正式公式、响应曲线与权重；
- Activity Block 时长与日程结构；
- Pattern 形成阈值；
- Significant Fact 的正式触发条件；
- Life Episode 正式字段；
- Life Episode 合并 / 拆分算法；
- History UI 与玩家可读表现；
- Relationship 如何从历史更新；
- Life Episode 与 Profession / Tradition 的未来兼容读取方式（不替代现行 Formation Evidence → Daily Resolution）；
- 是否需要 Habit（习惯）；
- 是否需要 Needs / Mood；
- 自动寻路与 Activity Execution 的正式技术实现；
- World Presentation 的第一版美术规格；
- 行为 Debug / Explainability 工具设计。

---

## 22. 下一步

### Step 1 — 文档治理
将本 Checkpoint 作为 History × Autonomous Life 专题的阶段性架构事实源。

### Step 2 — Prototype Spec
整理 `Autonomous Life Vertical Slice V0.1（自主生活纵切 V0.1）` 产品规格。

### Step 3 — Data Simulation
用 3–5 Person × 4–6 Activity 做纯数据模拟，验证：

- 人物差异；
- 重复率；
- 情境覆盖；
- 关系反馈；
- 行为可解释性。

### Step 4 — Village Productization
数据模型通过后进入：

- Village World 第一版；
- 初版美术；
- Person 场景可见；
- Activity Point；
- 自动寻路；
- 简单活动表现；
- Behavior Debug Inspector（行为调试查看器）。

### Step 5 — Life Episode Vertical Slice
接入：

> Activity → Fact → Pattern → Life Episode → Relationship / Event / Tradition 等候选历史消费者

重大 Fact 可直接形成 Life Episode。Profession Formation 继续走 §14.1 的 `Activity / Event → Formation Evidence Contribution → Daily Resolver`；片段只提供兼容的追溯与上下文，不成为 Formation 旁路。

并验证至少一次真实 Future Behavior 回环。

---

## 23. 一句话总结

> **RRPG 的人物不是由职业标签和固定脚本定义，而是在玩家责任、世界机会、个人倾向、人物关系与具体情境中自主行动；这些真实行动留下客观事实，并可聚合为模式与 Life Episode（人生经历片段），再由不同系统按各自读取契约解释成关系、身份、传统与未来机会，从而让“人物是谁”和“共同体是什么”真正从历史中长出来。**
