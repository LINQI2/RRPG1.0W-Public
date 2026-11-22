# RRPG｜卡牌与 Trait 设计工具箱

**内容基线：2026-09-16；导航修订：2026-09-17**  
**Freeze Level：Supporting Candidate**  
**性质：Supporting Tool / 非角色设计主流程 / 非 Current Rule Authority**  
**适用范围：Card / Trait / Skill / Battle 内容的局部机制设计与问题诊断**

> 当角色设计已经知道“玩家应该思考什么”，但具体 Card / Trait 不好落地时，使用本文。  
> 本文不替代《角色设计方法论》，也不定义 Battle Rule。Trigger / Target / Status / Position 等正式含义必须回查 [GameSpec/Battle/](../../GameSpec/Battle/README.md)；Trait 归属回查 [GameSpec/Person/](../../GameSpec/Person/README.md)。

---

## 1. 最小规则语法

一个具体 Battle 内容通常可以拆成：

```text
Host
→ Trigger
→ Condition / Observation
→ Target
→ Operation
→ Value
→ Duration / Frequency
```

并非每个内容都必须包含全部字段。

### 载体（Host）
谁拥有 / 发起这个内容。

### 触发（Trigger）
什么时候结算。

### 条件（Condition）
什么条件下才成立。

### 读取事实（Observation）
读取什么当前 Fact 或 History Fact。

### 目标（Target）
作用于谁 / 什么对象。

### 效果操作（Operation）
实际做什么。

### 数值（Value）
数值、倍率、层数、次数。

### 持续时间（Duration）
持续多久。

### 频率（Frequency）
每回合一次 / 每战一次 / 无限等。

---

## 2. 常用可替换零件

### 零件查询目录

[返回专题流程](README.md#日常操作流程) · [打开工作台](Identity角色内容设计工作台.xlsx)

目录用于查表达方式与规则出处，不代表所有操作已有正式规则或导入器。查到“待确认”时，把缺少的语义记入工作台 09_验证与开放问题。

| 零件 | 中文含义 | 何时用 | 限制与检查 | 填写位置 | 规则出处 |
|---|---|---|---|---|---|
| 载体 / 归属（Host / Owner） | 谁发起效果、牌归属于谁 | 角色牌与通用牌需要区分来源时 | 来源不等于目标；所有权变化不是默认支持 | 卡牌：身份、目标域、备注；特性：身份、目标 | [§7 卡牌分类](../../GameSpec/Battle/Battle-GameSpec-V0.1.md) |
| 触发（Trigger） | 何时开始一次结算 | 出牌后、回合开始等触发效果 | 写清前后顺序；新的触发事件需另行确认 | 卡牌：触发/条件；特性：触发 | [§13 回合；具体事件扩展待规则确认](../../GameSpec/Battle/Battle-GameSpec-V0.1.md) |
| 条件（Condition） | 达到什么条件才生效 | 机会牌、条件增益 | 区分出牌合法性与额外收益条件；无条件时写“无” | 触发/条件、效果正文 | [§5 目标可达性、§15 失能；具体条件为内容候选](../../GameSpec/Battle/Battle-GameSpec-V0.1.md) |
| 读取事实（Observation） | 读取当前值或历史记录 | 按生命、意图、位置产生不同价值 | 写明读取时点；不能把读取隐含成修改 | 触发/条件、环境依赖 | [§5–6 空间、§14 意图、§15 生命](../../GameSpec/Battle/Battle-GameSpec-V0.1.md) |
| 目标（Target） | 效果作用对象和选择方式 | 对自己、友方、敌方、牌生效 | 区分手选/自动；核对空目标、失能目标、范围 | 目标域；特性目标 | [§5.3 攻击距离、§15.4 失能目标](../../GameSpec/Battle/Battle-GameSpec-V0.1.md) |
| 效果操作（Operation） | 实际改变什么 | 造成伤害、治疗、抽牌、移动等 | 列出结算顺序；名称存在不代表工程已实现 | 效果正文及伤害/治疗/抽牌等列 | [§4.4 移动、§10 抽牌、§12 行动力；细节待具体规则确认](../../GameSpec/Battle/Battle-GameSpec-V0.1.md) |
| 数值（Value） | 幅度、倍率、层数或次数 | 给已确定效果填写参数 | 空白不等于0；未知写待数值；测试常量注明来源 | 费用、伤害等参数列、备注 | [§23 未冻结参数；不由目录补数值](../../GameSpec/Battle/Battle-GameSpec-V0.1.md) |
| 持续与频率（Duration / Frequency） | 何时结束、多久允许触发一次 | 回合内增益、重复触发 | 写清刷新/叠加/失效条件；未定义则登记问题 | 持续时间、次数/频率 | [§2 生命周期、§13 回合；具体叠加规则待确认](../../GameSpec/Battle/Battle-GameSpec-V0.1.md) |
| 位置与邻接（Position / Adjacency） | 读取或改变逻辑位置关系 | 补位、换岗、空间条件 | 按逻辑数据；移动后重算关系；不凭画面距离 | 邻接/独狼/群羊、移动、环境依赖 | [§4.4、R5.0、R6.1–6.2](../../GameSpec/Battle/Battle-GameSpec-V0.1.md) |
| 独狼 / 群羊 | 以关系稀疏或密集产生价值的设计方向 | 空间构筑、条件牌 | 当前规格提到方向，不能据名称自行冻结人数阈值 | 邻接/独狼/群羊、触发/条件 | [§6 设计方向；具体阈值回查该卡](../../GameSpec/Battle/Battle-GameSpec-V0.1.md) |
| 费用与行动力（Cost / AP） | 消耗或改变全队共享行动资源 | 费用取舍、返还、资源转换 | 费用与返还分列；检查0费抽牌循环 | 费用AP、行动力变化、效果正文 | [R12.1–R12.4](../../GameSpec/Battle/Battle-GameSpec-V0.1.md) |
| 抽牌与保留（Draw / Retain） | 改变手牌获取或回合末去向 | 资源整理、跨回合筹备 | 检查牌堆耗尽、回合结束、费用循环 | 抽牌、效果正文、持续时间 | [§10、R11.2–R11.4](../../GameSpec/Battle/Battle-GameSpec-V0.1.md) |
| 状态（Status） | 为对象附加可读取的状态 | 条件治疗、状态利用 | 目录不是正式状态库；无状态定义先登记规则需求 | 状态效果、持续时间、开放问题ID | [§7 允许读取状态；具体新增状态待确认](../../GameSpec/Battle/Battle-GameSpec-V0.1.md) |
| 历史事实（History Fact） | 之前目标、之前归属、本回合行动记录 | 接力、累计行为奖励 | 未确认的历史事件/重置/表现应登记需求 | 历史依赖、次数/频率、开放问题ID | [当前规格未确认通用History协议；作为候选](../../GameSpec/Battle/Battle-GameSpec-V0.1.md) |
| 临时牌 | 效果生成、生命周期受限的牌候选 | 收获、加工后的兑现 | 注明生成位置、归属、离场时机；普通牌规则不足时登记问题 | 效果正文、持续时间、开放问题ID | [§11 普通牌生命周期；临时牌细节不得推定](../../GameSpec/Battle/Battle-GameSpec-V0.1.md) |
| 身份特性（Profession Trait） | 人物当前身份的战斗机制表达 | 初始特性、成长特性 | 具体效果和节点仍未冻结；不当作独立人格特性 | 05_Trait配置 | [§5 特性归位、§9 成长节点](../../GameSpec/Person/Person-Profession-Growth-GameSpec-V0.1.md) |

---

## 3. 选择预算

额外主动选择本身就是高权限零件。

它会同时增加：

- 控制权；
- 稳定性；
- 信息质量；
- 泛化能力；
- 操作次数；
- 认知负担。

优先顺序通常可以考虑：

> 当前目标 / 自动目标 / 最高最低 / 随机  
> → 再到额外主动选择。

如果“选谁”本身就是核心玩家问题，则选择是合法的。

例：

> Worker《换岗》如果核心就是“和谁交换”，就不能为了简化机械删除目标选择。

---

## 4. 泛化性预算

以下内容拥有更高泛化权限：

- 任意 Card；
- 任意效果；
- 任意属性；
- 任意 Target；
- 自动最优适配；
- 跨多个 Battle Fact 的通吃效果。

越泛化：

- 越不容易卡手；
- 越容易压缩其它内容空间；
- 越容易成为通用最优解。

所以：

> **灵活性本身就是强度。**

---

## 5. 读取事实（Fact）≠ 修改事实

角色可以通过反复读取某种 Fact 建立身份感，而不需要每次修改 Fact 本体。

例如：

> 读取 Hand 结构  
> → 最后只产生普通 Shield

也可以非常有身份。

不要为了“特殊”就频繁改：

- Cost；
- Card Type；
- Owner；
- Target；
- Effect本体。

---

## 6. 当前事实（Current Fact）与历史事实（History Fact）

### 当前事实（Current Fact）

玩家当前直接能看到：

- HP；
- Position；
- Adjacency；
- Intent；
- Hand；
- Status。

认知成本通常较低。

### 历史事实（History Fact）

需要玩家记住“刚才发生过什么”：

- Previous Owner；
- Previous Target；
- Acted This Turn；
- 本回合第几次；
- 累计使用次数。

History Fact 不是禁止，但属于额外认知预算。

初期内容默认优先 Current Fact。

---

## 7. 环境可用性检查（Gate）

设计内容时必须问：

> **它首次出现时，所依赖的环境 Fact 是否保证存在？**

典型风险：

- Worker 只有自己一人，却拿到只能对邻接友军生效的 Starter；
- Herbalist 初期没有 Status 环境，却拿到纯净化牌；
- Hunter 依赖特殊机会，但 Encounter 不产生该机会；
- Cook 只有一张牌，却要求复杂 Hand 结构。

处理方式：

- 增加基础效果兜底；
- 后移 Placement；
- 换成当前环境已存在的 Fact；
- 不作为 Starter。

---

## 8. 线性数值不等于新机制

以下很容易是假变化：

> 治疗3 → 治疗5  
> 攻击6 → 攻击8  
> 1AP治疗4 / 2AP治疗8

如果玩家问题不变，只是数值更大：

> 不要硬升成新子题 / 新机制族。

---

## 9. 同效果操作（Operation）≠ 角色重叠

两个 Identity 都可以：

- +Attack；
- Heal；
- Draw；
- Move；
- Shield。

判断是否重叠要看：

- 为什么使用；
- 读取什么；
- 在做什么判断；
- 哪个 Fact 被重新估值；
- 触发窗口；
- 最终系统职责。

例：

> Farmer +Attack = 培养成长。  
> Herbalist +Attack = 身体代价换取主动价值。

Operation相同，身份语义不同。

---

## 10. 两态优先原则

当一个内容需要根据状态变化效果时，优先：

> 一个清晰判断维度  
> + 两种主要结果。

例如：

- Ally / Enemy；
- 受伤 / 未受伤；
- 有Status / 无Status。

不要早期就堆：

> 阵营 + HP + Status + Position + History

形成规则表阅读。

---

## 11. 副作用必须真的影响决策

“收益 + 代价”结构中：

> 代价必须至少偶尔让玩家产生“现在不该用”。

如果副作用长期可忽略：

> 它只是文案，不是玩家问题。

同样，副作用也不应复杂到：

> 需要维护多个跨回合隐藏条件。

---

## 12. 新关键词 / 新系统投入产出（ROI）

新增 Battle Fact / Keyword / Status 前至少问：

1. 是否会被多个独立内容重复需要？
2. 是否真的产生新决策？
3. 是否能压缩大量重复文本？
4. UI是否能清楚表现？
5. 是否值得新增规则 / 测试 / 教学成本？

否则先用现有 Battle Language 验证。

---

## 13. 问题诊断顺序

一个 Card / Trait 不好用时，按以下顺序检查：

```text
Identity是否错？
↓
母题是否错？
↓
子题是否错？
↓
具体实现是否错？
↓
Trigger / Target / Observation / Operation 是否错？
↓
Placement是否错？
↓
数值是否错？
↓
反馈表现是否错？
```

越往上层，推翻所需证据越高。

---

## 14. 验证样例（Proof Sample）与正式内容

Proof Sample 只负责：

> 证明一个子题是否能落地。

默认：

> 每个重要子题 1～2 个就够。

不要因为 Proof Sample 写出来了，就：

- 自动进入正式牌池；
- 自动进入 Starter；
- 自动进入配置；
- 自动形成 Build。

---

## 15. 起始牌（Starter）检查

Starter 至少检查：

- 最低战斗闭环；
- 环境可用性；
- 第一层 Identity 认知；
- 认知负担；
- 重复可用性；
- 后续空间保留。

Starter 不要求：

> 一张牌展示 Full Identity。

---

## 16. PvE 体验检查

不要直接套 PvP 的：

- Anti-fun；
- 卡差；
- 对手Counterplay。

PvE更应该问：

- 是否过早杀死玩家自己的决策空间？
- Encounter 是否还能提出新问题？
- Build 成型后是否进入过长垃圾时间？
- 优势能否被玩家明显感知？
