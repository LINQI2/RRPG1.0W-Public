# GameSpec / Person 使用说明

`GameSpec/Person/` 是 RRPG 的 **Person / Profession / Growth / Run-Return Experience** 当前可执行规则 Authority。

## 当前规格

- `Person-Profession-Growth-GameSpec-V0.1.md`：Person 身份（PersonId vs 模板身份）、Assignment ≠ Profession、无/单 Active Profession 边界、Profession Template 共享定义、成长归属（Level/Exp/Unlocked Pool/Profession Trait progression/Starter Build）、Trait 归位（当前 Trait = Profession Trait）、Level 与主动升级、牌池分离（Full Pool vs Person Unlocked Pool）、Starter Slot、Growth Node、Run/Return 经验规则，以及 §12 Profession Formation（Evidence → Strength → Candidate → Target → Stability → 自动 Formation；Life Direction Event；Formation 初始化与 Template Authority）的 Current Rule。

## 与其它 GameSpec 的关系

- Battle 玩法规则（战场 / 抽牌 / 出牌 / 胜负 / 单场行为）→ `GameSpec/Battle/`；本目录只接管 Person 长期身份与成长、Formation、以及 Run/Return 长期经验归置。
- Village 规则（时间 / 占用 / Assignment / Project / Social Response）→ `GameSpec/Village/`；Village GameSpec 的 Profession Formation Source Hook 与本目录职业边界对齐（Hook 为 Test，不提升为正式 Formation）。
- Formation 基础规则已 Current；Formation 数值尺度、阈值、Talent / Family / Ability 正式系统、Future Transition、Dormant、职业数量、正式内容与数值仍 Not Frozen，不得由实现侧自行补全。

## 读取边界

- 具体技术实现（M1–M4 结构字段形式、权威解析器、存档哨兵、测试结构）**不构成规则**；规则与实现差异以本 GameSpec 为准登记，不以实现覆盖规则。
- 规则正文类名/字段名仅作语义说明；实际实现结构变化不自动改变玩家规则。
