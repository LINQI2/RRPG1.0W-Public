# V3 Social Response｜Concept Checkpoint V0.1

- **日期**：2026-09-03
- **项目**：RRPG
- **阶段**：Village V3 — Social Response 概念设计
- **状态**：Concept Near Closure
- **用途**：阶段性设计事实保存；供后续系统策划专家审计、制作人裁决与 GameDesign / GameSpec 迁移使用
- **注意**：本文不是正式 GameSpec，不冻结具体算法、数值、事件内容或 Runtime 结构

---

## 1. 本阶段任务定义

V3 不是“加一个事件系统”，也不是“让 NPC 多说几句话”。

它真正要解决的是：

> **玩家长期治理一个共同体以后，过去的生活方式会不会被人物和共同体感知、表达，并逐渐影响后续决策。**

V2 证明的是：

> **我现在把谁的时间投入哪里。**

V3 要第一次证明：

> **我过去长期怎么安排，会改变今天我面对的社会状态。**

因此 V3 是 RRPG 从“机械管理”走向“共同体具有历史与社会意义”的第一座桥。

---

## 2. V3 核心定义

当前最准确的定义：

> **Social Response = 长期世界事实被合理的人物或共同体感知、表达，并在有意义时改变未来决策空间的过程。**

它不是：

> 玩家点一下 → NPC 评论一句。

完整概念链：

```text
World Fact
  ↓
Pattern
  ↓
Observer
  ↓
Recognition
  ↓
Signal
  ↓
Consequence
  ↓
New Decision
  ↓
New World Fact
```

最终应形成循环，而不是单向通知链。

---

## 3. V3 为什么属于 Base 树干

如果没有 Social Response：

玩家长期进行 Assignment、Project、Expedition、资源安排，但这些最终只改变数字。

Village 会退化为：

> **一台会保存人物名字的人力机器。**

V3 的作用是让玩家开始感觉：

> “这里的人知道这些年发生了什么。”

并进一步感觉：

> “我长期怎么治理，真的会形成一种共同体生活。”

因此 V3 不是 Narrative Polish，而是承担：

> **History → Social Meaning**

---

## 4. Social Response 的四种价值

### 4.1 信息价值

帮助玩家发现即时操作中不容易看到的长期模式。

不是：

> “张三刚加入 Project。”

而是：

> “今年张三已经第四次被临时抽调。”

---

### 4.2 情感价值

把抽象统计重新落回具体人。

不是：

> `ProjectParticipation = 4`

而是：

> “今年各种临时工程，好像都有我的份。”

---

### 4.3 战略价值

回应最终应该有资格影响玩家之后怎么做。

Social Response 如果永远不影响决策，只能算氛围表现。

---

### 4.4 历史价值

让玩家多年以后能够形成：

> “我们村以前经历过那一段。”

而不仅是：

> “触发过一个事件。”

它最终应成为 Village History 的组成部分。

---

## 5. 三种回应尺度

这里是**分析层级**，不代表立即建立三套 Runtime System。

### 5.1 Person-level

回答：

> **这件事对一个具体 Person 意味着什么？**

例如：

> 张三长期被抽调。

适合承载：

- Ambient Bark
- Person Thought
- Character Event
- Experience

---

### 5.2 Local Group-level

回答：

> **一个长期模式是否开始影响某一小群人？**

例如：

> 某岗位长期缺员。

合理 Observer 可能是：

- 同岗位人员
- 被该岗位产出影响的人
- 承担相关责任的人

---

### 5.3 Community-level

回答：

> **这件事是否已经成为整个共同体需要面对的问题？**

只有到这一层，才可能进一步连接：

- Village Event
- Community Consequence
- Theme-level Consequence

并不是每个小事实都应该直接升级为“全村问题”。

---

## 6. Observer 是 V3 的硬原则

Social Response 不能来自“系统全知”。

任何社会反馈必须回答：

> **谁注意到了？为什么是他？他凭什么知道？**

例如：

- 张三反复被抽调 → 张三本人当然有资格回应
- 某岗位长期没人 → 实际受这个岗位影响的人最有资格注意
- 某个只有后台隐藏数值知道的变化 → 若没有合理感知路径，不应伪装成 NPC 反馈

因此：

> **No Observer → No Social Response**

如果某信息必须告诉玩家，但没有合理 Observer，可以由普通 System UI 表达，而不是借 NPC 的嘴说。

---

## 7. Experience / Recognition / Event 的边界

### 7.1 Experience

记录客观人生事实：

> 张三在这一年参与了四次 Village Project。

Experience 不写：

> “张三觉得自己被过度使用。”

因为这是解释，不是事实。

---

### 7.2 Recognition

人物对自己的长期事实形成一种当前认识：

> “今年我好像一直在各种工程之间跑。”

Recognition 首先只是：

> **人物意识到了。**

它不必立刻意味着：

- 不满
- 开心
- 愤怒
- 忠诚下降

---

### 7.3 Event / Consequence

只有当这种事实已经值得玩家处理时，才进入真正决策：

- 某人提出要求
- 出现新的安排
- 形成新的 Project
- 形成新的 Assignment 需求
- 打开新的机会或代价

因此：

> **Experience ≠ Recognition ≠ Event**

---

## 8. Response 不等于 Complaint

V3 必须避免退化成：

> 玩家长期做事 → NPC 抱怨 → Debuff。

同一种长期事实可能形成：

- 自豪
- 疲惫
- 习惯
- 疑惑
- 被需要感
- 新机会
- 矛盾

因此：

> **Response ≠ Complaint**

以及：

> **Consequence ≠ Penalty**

Social Response 不应变成“人物专门阻碍玩家治理”的系统。

---

## 9. Theme 与 V3 的关系

此前已确认：

> Theme 不应该直接做成“团结值 70 / 发展值 30”。

V3 很可能是 Theme 从系统中自然长出的关键入口之一：

```text
玩家长期做出实际安排
  ↓
人物和群体逐渐感受到后果
  ↓
某些 Pattern 开始具有社会意义
  ↓
真正的社会问题进入玩家视野
  ↓
Theme 逐渐显形
```

因此：

> **V3 是机械行为转化成社会意义的桥。**

Theme 不是由 V3 “生成数值”，而是通过 Social Response 逐渐显现。

---

## 10. Social Response 正向 Gate

一个事实想进入 V3 核心反馈，至少检查五项：

### 10.1 Influenceable

玩家能影响它吗？

如果玩家完全无能为力，频繁反馈容易产生无助和噪音。

### 10.2 Decision-Relevant

它会影响未来决策吗？

若完全没有决策意义，只属于氛围候选。

### 10.3 Patterned

是否已经持续 / 重复到形成 Pattern？

一次偶然行为通常不值得社会化。

### 10.4 Observed

是否存在合理 Observer？

谁真正感受到了它？

### 10.5 Informative

它是否提供玩家当前不容易直接看到的新信息？

如果玩家刚刚亲手完成操作并完全知道发生了什么，就不需要再用 Social Signal 重复一遍。

概括为：

> **Influenceable + Decision-Relevant + Patterned + Observed + Informative**

---

## 11. Anti-Gate：什么不应该进入 Social Response

### 11.1 纯技术状态

例如：

- Save
- Tick
- ID
- UI 刷新
- 序列化
- 内部计数

这些属于 System Fact，不属于 World Fact。

> **System Fact ≠ World Fact**

---

### 11.2 单次偶然操作

一次安排不是长期模式。

不要让世界对每个普通操作过度敏感。

---

### 11.3 已经非常清楚的信息

需要区分：

- 操作确认
- 状态 UI
- Social Signal

三者不是一回事。

---

### 11.4 无决策意义的琐碎事实

可以作为 Ambient Flavor，但不应该占用 V3 核心注意力。

---

### 11.5 没有 Observer 的隐藏数据

不让人物成为设计师的嘴。

---

### 11.6 纯道德评价玩家的内容

避免默认让人物说：

> “你做得很好。”

> “你太残忍了。”

更优先表达：

- 人物处境
- 人物观察
- 人物诉求
- 可验证事实

评价权尽量留给玩家。

---

### 11.7 为生成反馈提前创造万能社会指标

当前不因 V3 自动建立：

- Village Satisfaction
- Social Pressure
- Unity Value
- Opinion 总量条
- Mood 总量条

除非未来大量玩法稳定证明它们确实必要。

---

## 12. 第一版原型强候选

当前认为第一版至少应验证两个尺度，但以下仍是**强候选，不是已冻结正式内容**。

### Case A｜Person-level

> 同一个具体 Person 被反复从 Primary Assignment 抽去 Temporary Commitment。

希望验证：

> 长期个人事实 → Recognition → Person Response

优势：

- 直接承接 V2
- 变量少
- 因果透明
- 不依赖 Food / Profession / Relationship

风险：

- 容易被误做成“人物不满系统”

---

### Case B｜Community-level

> 某个 Workplace 长期严重缺员 / 空置。

希望验证：

> 长期结构事实 → 合理 Observer → Community Signal / Village Response

优势：

- 不依赖 Food
- 能验证共同体级回应
- 不局限于个人对话

风险：

- 不应因为该案例就提前建立完整 Community Satisfaction / Trend System

---

## 13. 第一版明确不需要做什么

不因 V3 提前新增：

- Mood System
- Opinion System
- Personality System
- Relationship System
- Aspiration System
- Faction System
- 满意度总量条
- 正式 Trend Runtime System
- Food
- Profession Formation
- 政治制度
- 完整心理模拟
- 大量随机 Bark 库

其中：

> **Trend 当前仍是设计概念，不等于 Runtime System。**

只有未来大量消费者反复需要统一 Pattern 数据层时，再考虑正式抽象。

---

## 14. Social Response 的表现出口

当前只是表达候选，不代表每层都必须实现。

### 14.1 Ambient Bark

回答：

> “现在有人注意到了。”

适合轻量表达。

### 14.2 Person Thought / Journal

回答：

> “这件事对这个人的人生意味着什么。”

### 14.3 Community Signal

回答：

> “这已经不是一个人的问题了。”

### 14.4 Event / Consequence

回答：

> “现在需要玩家处理。”

### 14.5 Log / Life History

回答：

> “后来到底发生过什么。”

总体原则：

> **世界先说话，UI 负责解释，Event 负责要求处理。**

---

## 15. V3 最终不能只做到 Signal

如果只做到：

> Fact → Bark

只能证明：

> 世界会说话。

不能证明：

> 世界会回应。

因此完整 V3 Closure 至少要证明一次：

> **长期事实 → 可感知回应 → 后续决策发生变化。**

不要求第一版建立巨大 Event Framework，但至少有一个真实案例跑完整：

```text
Past
  ↓
Recognition
  ↓
Response
  ↓
New Decision
  ↓
New Future
```

---

## 16. V3 产品验收原则

未来 V3 不应主要验收：

- Trend 类有没有写
- Bark 有没有播放
- Event 数量够不够

而应该问：

### 16.1 History

> 我能不能感觉村庄记得我过去长期怎么经营，而不是每天清零？

### 16.2 Humanity

> 我能不能理解为什么是这个人 / 这些人在回应？

### 16.3 Causality

> 我能不能看懂反馈来自真实发生的长期事实，而不是随机剧情？

### 16.4 Decision

> 这种回应有没有让我重新考虑下一步安排？

可以压成：

> **History + Humanity + Causality + Decision**

---

## 17. 当前已确认

- V3 不是单纯事件系统或 Bark 系统
- Social Response 服务“过去影响现在”
- Observer 是硬原则
- Experience / Recognition / Event 职责必须分开
- Response 不等于 Complaint
- Consequence 不等于 Penalty
- Theme 可以通过 Social Response 从机械行为逐渐显形
- V3 最终不能只停在 Signal，至少要有一次真实决策回环
- V3 不应以 Mood / Opinion / Relationship 等重模拟为默认基础

---

## 18. 强候选但未冻结

- Person-level 原型：同一 Person 被反复 Temporary Commitment
- Community-level 原型：Workplace 长期缺员 / 空置
- Ambient Bark 作为最低成本可感知 Signal 出口
- Recognition 作为 Experience 与 Event 中间层的概念
- Person / Local Group / Community 三尺度作为内容分析工具

---

## 19. 明确未冻结 / 后置

以下内容全部尚未设计或冻结：

- Pattern 具体算法
- “重复多少次”才形成长期模式
- Signal 的具体文本内容
- Observer 选择算法
- Event 具体选项
- Consequence 数值或持续时间
- V3 是否拆 V3A / V3B
- Trend 是否最终成为正式 Runtime System
- Community-level 状态是否需要结构化
- 正式 UI / 场景表现方式
- 是否需要 Thought / Journal 独立系统
- V3 与 Theme 的正式数据接口
- V3 与 Profession / Identity 的正式接口

---

## 20. 当前禁止误读

不得从本文推导：

- 已确认存在 Mood / Opinion / Relationship 系统
- 已确认存在正式 Trend System
- 已确认 Person 会普遍拒绝玩家命令
- 已确认 V3 必须依赖 Food
- 已确认某个具体 Event 内容
- 已确认重复抽调一定产生负面后果
- 已确认 Workplace 缺员一定触发全村事件
- 已确认所有 Social Response 都必须有数值后果
- 已确认某个具体 Pattern 阈值

---

## 21. 当前阶段状态

> **V3 Social Response Concept：Near Closure**

概念层已经解决：

- V3 为什么存在
- 与 V2 的区别
- Social Response 核心链
- Experience / Recognition / Event 边界
- Observer 原则
- 正向 Gate
- Anti-Gate
- Theme 关系
- 第一版原型尺度
- Closure 产品验收方向
- 禁止提前扩展的重系统

继续往下讨论：

- 张三具体说什么
- Pattern 几次才成立
- Event 选项是什么
- 后果持续多久
- UI 怎么做

已经进入具体内容 / 算法 / GameSpec 阶段，应后置。

---

## 22. 推荐后续流程

当前不直接修改 GameDesign / GameSpec。

建议流程：

```text
V2 Final Closure 完成
  ↓
RRPG 游戏系统策划专家
  ↓
V3 Concept Checkpoint 独立审计
  ↓
制作人裁决
  ↓
Codex 最小迁移到 Current GameDesign
  ↓
再进入 V3 GameSpec
  ↓
开发
```

本文本身应视为：

> **阶段性设计事实包 / Design Checkpoint**

而不是最终 Current Truth 或实现规格。
