# V4 Home–Away Continuity｜Concept Checkpoint V0.1

- **日期**：2026-09-03
- **项目**：RRPG
- **阶段**：Village V4 — Home ↔ Expedition 概念设计
- **状态**：Concept Near Closure
- **用途**：阶段性设计事实保存；供后续系统策划专家审计、制作人裁决与 GameDesign / GameSpec 迁移使用
- **注意**：本文不是正式 GameSpec，不冻结具体耗时、通信实现、事件内容、UI、数值或派遣方案

---

## 1. 本阶段任务定义

V4 不是“让 Village 和 Expedition 互相传数据”。

它真正要解决的是：

> **玩家离开 Home 进入 Expedition 以后，仍然相信这是同一个世界、同一批具体人物、同一段时间、同一段历史。**

因此 V4 的核心不是接口数量，而是：

> **Home–Away Continuity**

也就是：离家、远征、归家不是三个彼此独立的模式，而是同一个持续共同体中的三个时间阶段。

---

## 2. Home–Away Continuity Contract

当前最值得长期保留的核心合同包括：

### 2.1 Person Continuity

出去和回来始终是同一个长期 Person。

远征不是生成一个临时战斗单位，而是：

> **一个真实生活在共同体里的人暂时离家。**

因此其出发前的生活、远征经历、归来后的状态属于同一人生。

---

### 2.2 Time Continuity

Village 不因为玩家进入 Expedition 而失去时间意义。

当前强方向：

> **Village 在 Away 期间不暂停。**

但这不等于玩家需要一边打 Battle 一边高频管理 Village。

---

### 2.3 History Continuity

Home 与 Away 都会产生真实世界事实。

Away 产生：

- Battle / Expedition 结果
- Person Experience
- 受伤 / Retreat / Defeat 等远征事实
- 外部发现

Home 同时产生：

- Assignment 执行
- Project 推进 / 完成
- 人物年龄与状态变化
- Social Pattern / Recognition
- 其它共同体历史

Return 时：

> **两条历史重新汇合。**

---

## 3. 核心原则：世界连续 ≠ 注意力连续 ≠ 完全失联

这是本轮 V4 最重要的收敛之一。

Away 期间：

> **Simulation Continues**

但：

> **Intervention Can Be Deferred**

玩家不是失去治理权，而是：

> **失去无限即时在场的能力。**

这意味着：

- 信息可能延迟；
- 能采取的行动可能受限；
- 与 Home 的联系需要合理世界路径；
- 一部分事情可以在玩家缺席时自然发生；
- 需要玩家决策的事项不一定立刻打断当前 Battle。

因此：

> **世界连续 ≠ 玩家注意力连续。**

以及：

> **离家 ≠ 完全失联。**

---

## 4. Away 期间的三种处理层级

这三类是当前概念分类，不代表立即建立三套 Runtime System。

### 4.1 Silent History

普通世界事实正常发生，不打断玩家。

例如：

- Assignment 执行；
- Project 推进或完成；
- 年龄变化；
- 普通人物生活事实；
- 轻量 Social Pattern；
- 不需要玩家立即决定的结果。

这些可以在归家后进入：

> **缺席历史 / Return History**

---

### 4.2 Pending Attention

重要，但不需要玩家当下决定。

例如：

- 某人成长；
- 某个社会问题开始形成；
- 某项工程完成；
- 某种新机会出现；
- 某个重要但可延迟处理的状态变化。

Away 期间可以留下：

> **Attention / Pending**

等玩家 Return 后重新接手。

---

### 4.3 Active Contact

真正具有时效性，而且存在合理联系路径的重大事情。

例如：

> 一个青年一路找到远征队：
>
> “勇者！村里出事了……”

Active Contact 不由“严重程度高”单独决定，而至少应满足：

1. 具有明显时效性；
2. 玩家现在做决定真的有意义；
3. 延迟处理可能剥夺玩家公平选择；
4. 存在合理通信路径；
5. 事情足够重要，值得打破 Away 注意力。

---

## 5. Active Contact 不等于强制回家

收到重大 Home 信息后，不应默认：

> “立即结束 Expedition。”

未来事件可以根据实际能力提供不同处理空间，例如：

- 返回；
- 授权共同体自行处理；
- 提供资源；
- 给予指令；
- 若未来系统真实支持，可能调回部分人员。

当前不冻结任何具体选项。

冻结的是更上层原则：

> **重大 Home 事件可以在 Away 中形成新的治理决策，但不必只有“强制退出 Battle”一种答案。**

---

## 6. 信息必须服从世界逻辑

V3 已形成：

> **No Observer → No Social Response**

V4 可以对应为：

> **No Communication Path → No Immediate Away Intervention**

如果没有任何世界内合理方式把 Home 信息传给玩家：

> 玩家不应该凭 UI 以全知视角实时知道所有 Village 变化。

反过来，如果有人真的找到玩家，或者未来存在其它合理通信方式：

> 玩家可以收到并处理信息。

因此：

> **信息传播本身属于世界的一部分。**

---

## 7. 不可逆事件不等于必须实时中断

是否允许玩家在 Away 时错过某件不可逆事实，关键不只是：

> “这件事严重吗？”

而应问：

### 7.1 风险是否可预期？

例如：

> 一位非常年迈、状态不佳的人可能在玩家离家期间寿终。

如果出发前玩家能合理理解这种风险：

> 缺席可以成为有意义的选择后果。

### 7.2 延迟是否剥夺公平选择？

如果某件事完全无法预知，却在后台随机永久抹掉关键对象：

> 这不是世界连续性，而是不公平。

因此：

> **不可逆 ≠ 必须打断。**
>
> **真正的 Gate 是预期性与公平性。**

---

## 8. Battle 内实时中断不是默认策略

即使发生 Active Contact，也优先在自然 Away 节奏节点呈现，例如：

- 一场战斗结束；
- 进入新地图节点前；
- 扎营；
- 路线选择；
- 遭遇间隙。

而不是：

> 玩家正在出牌时突然切入 Village Modal。

因此：

> **事实发生时间**
>
> 与
>
> **玩家收到信息的呈现时间**

可以不同。

只有未来被证明真正必要的极端情况，才考虑实时突破当前 Battle Flow。

---

## 9. Away 暂时打断生活，但不擦除生活

远征应该：

> **暂时打断人物生活，而不是删除人物生活。**

例如：

出发前：

> 张三有 Primary Assignment。

Away：

> Expedition 作为 Temporary Occupancy 暂停其实际执行。

正常回来：

> 尽量重新接入原生活轨迹。

但如果历史真实改变了条件，例如：

- 重伤；
- 原岗位消失；
- Village 结构变化；
- 人物状态变化；

则不需要机械恢复。

这时：

> **不是系统忘记恢复，而是人生已经改变。**

---

## 10. Expedition 首先给 Person 增加人生事实

Battle → Person 的最低层不应优先理解为：

> “战斗职业经验 +5”。

更底层的是：

> **这个人真的经历过什么。**

例如：

- 第一次远征；
- 重伤生还；
- 发现某地；
- 参与撤退；
- 保护同伴；
- 接触新的文化 / 知识。

这些首先属于：

> **Experience / Life Fact**

以后 Profession / Identity Formation、Event、Social Response、Trait 等系统再各自解释。

---

## 11. Village → Expedition 的连接强度

V4 的目标不是“所有系统互相影响”。

当前更倾向：

> **少数高语义连接，避免全连接。**

### 11.1 强连接

接近不可缺：

- Person Continuity
- Time / Occupancy Continuity
- Experience / History Continuity

缺少其中任意一个，都容易让 Village 与 Expedition 看起来像两个游戏。

---

### 11.2 中连接

应该存在，但不能硬门槛化：

- Resource Flow
- Capability / Unlock Flow
- Person State
- Failure Consequence
- Social Consequence
- Expedition Discovery → Village Future Options

---

### 11.3 松散 / 多源连接

不应为了“融合”强行一一映射：

- Expedition 具体任务来源
- Theme 具体内容
- Battle 数值强化
- Village 具体经济结构
- DLC Branch

---

## 12. High-Semantic, Low-Coupling

V4 应避免一个常见误区：

> 连接得越多 = 越融合。

如果 Food、Mood、关系、建筑、天气、Profession、伤病、政策、Trend 全部双向影响 Battle：

> 玩家会越来越难理解结果为什么发生，系统耦合也会迅速膨胀。

因此当前设计原则是：

> **High-Semantic, Low-Coupling**
>
> 高语义连接，低规则耦合。

---

## 13. 双系统连接不能制造“双倍劳动”

连接不应该让玩家重复做同一个决定。

例如不希望：

```text
远征前取消张三岗位
→ 选张三出征
→ 回来后找张三
→ 再重新分配岗位
```

V2 的：

> Temporary Occupancy 解除 → Primary Assignment 自动恢复

是正确方向。

因此：

> **双系统联动应该保留决策意义，消除重复操作。**

---

## 14. Playstyle Freedom

早期曾提出：

> 手操远征较快、派遣较慢

其原始目的不是“必须有派遣”，而是解决：

> 双核心游戏会不会强迫玩家频繁切换到自己当下不想玩的另一侧？

这一产品问题保留。

但具体方案：

> 派遣 / Delegation

当前降为未冻结候选。

更上层原则为：

> **双核心互相赋予意义，但不依靠高频强制切换证明关联。玩家应能够阶段性沉浸于当前偏好的核心玩法。**

因此：

- Battle 偏好玩家远征时，Village 不应不断用琐事拉回；
- Village 偏好玩家也不应被固定频率 Battle 任务强行拖走；
- 两边仍通过 Person / Time / History / Possibility 强连接。

---

## 15. Return / Reorientation 的核心职责

Return 不应理解为：

> “领完奖励回村”。

它真正承担：

> **Home 与 Away 两条历史重新汇合，玩家重新获得完整在场能力。**

当前最值得保留的三个词：

> **Remember → Reorient → Re-engage**

---

## 16. Remember

回答：

> **我离开期间发生了什么？**

包括：

### Away History

- 战斗结果；
- 远征发现；
- 人物受伤；
- Retreat / Defeat；
- Person Experience。

### Home History

- 时间过去多久；
- 哪些 Assignment 继续执行；
- 哪些 Project 完成；
- 哪些人物发生变化；
- 哪些 Social Pattern / Recognition 形成；
- 是否存在重要缺席事实。

Return 的第一职责是：

> **把离开前和回来后的世界差异变得可读。**

---

## 17. Reorient

回答：

> **现在这个共同体已经是什么状态？**

玩家离开前建立的 Mental Model 在经过一段 Away 时间后可能已经过期。

因此 Reorientation 要帮助玩家重新理解：

- 谁已经回来；
- 谁不能立刻恢复原生活；
- 哪些 Assignment 已自动恢复；
- 哪些原条件已改变；
- 哪些 Project 已完成；
- 当前有什么值得注意。

这不是“汇报工作”，而是：

> **帮助玩家重新接管一个已经继续生活了一段时间的共同体。**

---

## 18. Re-engage

回答：

> **现在我想重新介入什么？**

Return 不应把所有信息混成一个待办列表。

应该区分：

### History

已经发生、无法再改变。

### Attention

值得关注，但不一定立即决定。

### Pending Decision

真正因为玩家回来而需要重新接手的事情。

因此：

> **过去是什么 → 现在是什么 → 接下来什么需要我。**

---

## 19. Return 不等于 Summary Screen

Return 是一段产品体验职责，不是已经冻结的 UI。

未来可能通过：

- 世界里的欢迎 / 新变化；
- 少量最重要历史提示；
- Attention 入口；
- Log；
- Pending Event

共同完成。

当前不得从 Concept 推导：

> “必须做一张大型归家总结页。”

总体仍遵守：

> **世界先说话，UI 负责解释。**

---

## 20. Battle Settlement 与 Return 应分开理解

### Battle Settlement

回答：

> **这次 Away / Expedition 发生了什么？**

例如：

- Victory / Retreat / Defeat
- 战利品
- Run 奖励
- 外部发现
- Away 中人物状态

### Return / Reorientation

回答：

> **Home 现在变成了什么？**

例如：

- Home elapsed time
- Home History
- 人物重新归位
- 需要恢复的人
- Attention
- Pending Decision

这两个职责不同，不应默认揉成一个“大结算面板”。

---

## 21. Return 不应变成待办清零

归家以后不要求：

> “处理完所有红点才能继续”。

应继续区分：

- History
- Attention
- Pending Decision

只有真正必须作出决定、否则世界无法合理继续的内容，才有资格阻塞。

Return 的目标是：

> **恢复玩家自由与完整在场能力。**

而不是制造新的强制劳动。

---

## 22. 适度“错过”是有价值的

玩家不在的时候，一些生活可能已经发生。

Return 不应该让世界看起来：

> “所有重要事情都暂停等你回来。”

例如：

- 某个孩子第一次承担了一件事；
- 某老人寿终；
- 两个人完成工程；
- 村里自行解决了一次小冲突。

适度的：

> **“我错过了。”**

会强化：

> Village 拥有玩家之外的生命。

但仍须遵守：

> 可预期 + 公平性

这两个护栏。

---

## 23. Active Contact 与 Return 必须闭环

如果 Away 中玩家收到重大 Home 联系并选择：

> “你们自己处理。”

那么 Return 后应能看到：

> 他们后来如何处理。

如果未来选择：

> 调回某人；

则归家时应能看到：

> 该人物在 Home 期间发生了什么。

因此 Active Contact 不应成为孤立 Away Event，而应最终回到：

> **Return / History Continuity**

---

## 24. Minimum Village World Integration 的意义

V3 → V4 前的 Minimum Village World Integration 不只是美术前置。

其更深意义是：

> **如果 Home 不是一个可以被重新进入、重新认识的地方，Return/Reorientation 就无法成立。**

玩家必须能感觉：

> “我离开了这里，现在又回到了这里。”

而不是：

> “我从 Battle Tab 切回 Management Tab。”

因此这一开发顺序仍然合理：

> V3 → Minimum Village World Integration → V4

---

## 25. 当前 V4 产品验收方向

未来 V4 不应主要验：

> 数据有没有双向传输。

更应该问：

### 25.1 Departure

> 我有没有感觉自己真的把属于这个村庄的具体人带走了？

### 25.2 Away

> 我有没有感觉 Home 仍然存在，而不是冻结 / 删除？

### 25.3 Continuity

> 我有没有感觉 Away 与 Home 处于同一段连续时间与历史？

### 25.4 Return

> 我回来以后能不能明显感觉 Away 和 Home 都经历了一段真实时间？

### 25.5 Reorientation

> 我能不能快速重新理解当前 Village 状态，而不是一脸茫然？

### 25.6 Re-engagement

> 归家后的变化有没有自然产生下一步值得我做的决定？

最终最核心的一问：

> **我有没有真的感觉自己“离开过家，又回来了”？**

---

## 26. 当前已确认 / 强方向

- V4 核心是 Home–Away Continuity，不是数据接口数量
- Person Continuity 是强连接
- Time / Occupancy Continuity 是强连接
- History Continuity 是强连接
- Away 期间 Village 不暂停是当前强方向
- 世界连续不等于玩家注意力连续
- Away 不等于完全失联
- 大多数事实可后台发生
- 重要但可延迟事项可留到 Return
- 极少数具有时效性且存在合理通信路径的重大事项可形成 Active Contact
- Active Contact 不默认等于强制回家
- No Communication Path → No Immediate Away Intervention
- Return 核心职责是 Remember → Reorient → Re-engage
- Battle Settlement 与 Return / Reorientation 职责不同
- 双系统连接应避免重复劳动
- Playstyle Freedom 是应解决的产品问题，但“派遣”不是已冻结答案

---

## 27. 强候选但未冻结

- Silent History / Pending Attention / Active Contact 三类 Away 处理层
- Active Contact 优先在自然 Away 节奏节点呈现
- Return 中 History / Attention / Pending Decision 三类信息分层
- Expedition Life Facts 作为未来 Formation / Event / Social Response 的事实来源
- Delegation / 派遣作为未来减少重复劳动的候选能力

---

## 28. 明确未冻结 / 后置

以下全部尚未冻结：

- 远征正式耗时公式
- Village Away 期间正式资源如何完整结算
- Active Contact 具体事件内容
- 哪些事件一定可以 / 不可以延迟
- 通讯系统
- 信使移动模拟
- Party Split
- 中途调回人物
- 自动派遣 / Delegation
- 手操 / 派遣效率与时间比例
- Return UI
- Summary Screen 是否存在
- Return 信息条数
- Failure 具体损失
- SafeReturn 后 Run HP → Village 长期健康映射
- Expedition Experience 正式数据结构
- Profession / Identity Formation 与 Expedition 的正式接口
- Theme / DLC 与 Away 通信机制的具体映射

---

## 29. 当前禁止误读

不得从本文推导：

- 所有 Village Event 都能在 Away 中联系玩家
- 所有重大事件都必须实时打断 Battle
- 所有不可逆事件都必须暂停 Battle
- 玩家在 Away 中实时知道 Village 全部状态
- 已冻结正式通讯系统
- 已冻结派遣
- 已冻结 Party Split
- 已冻结归家 Summary UI
- 已冻结远征正式时间倍率
- 已冻结人物伤病 / Failure 后果
- Village 与 Battle 需要所有系统双向互相影响

---

## 30. 当前阶段状态

> **V4 Home–Away Continuity Concept：Near Closure**

概念层已经解决：

- V4 为什么存在
- Person / Time / History 三条连续性
- Away 时世界与玩家注意力的关系
- 不暂停的强方向
- Silent History / Pending Attention / Active Contact 的处理逻辑
- 通信路径与公平性原则
- Active Contact 不等于强制返乡
- Return / Reorientation 的核心职责
- Battle Settlement 与 Return 的边界
- Playstyle Freedom 的问题层与方案层分离
- 双系统避免全连接与双倍劳动

继续往下讨论：

- 远征几天
- 哪类事件联系
- 信使如何实现
- Return UI 长什么样
- 如何拆队
- 派遣多久
- 具体损失多少

已经进入 GameSpec / 内容 / 算法 / UI / 数值阶段，应后置。

---

## 31. 推荐后续流程

当前不直接修改 GameDesign / GameSpec。

建议：

```text
V2 Final Closure 完成
  ↓
RRPG 游戏系统策划专家
  ↓
V3 / V4 Concept Checkpoint 独立审计
  ↓
制作人裁决
  ↓
Codex 最小迁移到 Current GameDesign
  ↓
分别进入 V3 / V4 GameSpec
  ↓
开发
```

本文本身应视为：

> **阶段性设计事实包 / Design Checkpoint**

而不是最终 Current Truth 或实现规格。
