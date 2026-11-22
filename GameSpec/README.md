# GameSpec 使用说明

`GameSpec/` 是 RRPG 已确认玩法规则和系统验收标准的详细权威层；本文件是规则层的唯一总入口，不另建平行规则权威索引。

## 领域 Authority 矩阵

| 领域 | 当前规则入口 | 职责边界 |
|---|---|---|
| Battle | [Battle/README.md](Battle/README.md) → [Battle GameSpec](Battle/Battle-GameSpec-V0.1.md)；Scene/UI 另见 [Binding Spec](Battle/Battle-Scene-UI-Binding-V0.1.md) | 玩家战斗行为由 Battle GameSpec 决定；Binding 只负责对象与协作边界 |
| Person | [Person/README.md](Person/README.md) → [Person GameSpec](Person/Person-Profession-Growth-GameSpec-V0.1.md) | PersonId、Assignment ≠ Profession、成长与 Return Experience；Profession Formation 的已冻结范围在该规格内，具体数值与正式内容仍 Not Frozen |
| Expedition | [Expedition GameSpec](Expedition/Expedition-Progression-GameSpec-V0.1.md) | Progression、Branch、Return、Supply 与 Journey Presentation；不代替 Village 资源或 Save 跨进程恢复 Authority |
| Village | [Village GameSpec](Village/Village-GameSpec-V0.1.md)、[Product Home](Village/Product-Home-World-GameSpec-V0.1.md)、[Resource Coupling](Village/Village-Resource-Coupling-GameSpec-V0.1.md)、[Building World](Village/Village-Building-World-GameSpec-V0.1.md) | Village core 管时间、Assignment、Project、Activity / Social；Product Home 管只读表现、Selection / Inspector / Camera 与内部 Surface 连续性；Resource 管 Ingredients / Food 生产、消费与 Carry；Building World 管稳定建筑实例、Placement、Construction、派生 Place / 实例级 Workplace 与建筑持久化。四份按职责分工，不互相覆盖，不建立平行 Person、Occupancy、资源或 Simulation Authority |
| Save | [Save/README.md](Save/README.md) → [Save GameSpec](Save/Save-GameSpec-V0.1.md) | 本地持久化契约与非承诺边界；各领域状态语义仍由各自 GameSpec 负责，触发 Ownership 见问题 40 |

正式Shop / Recruit Node Content若未来立项，须由新的领域设计决定；统一Event Foundation现已接产品，但其Prototype内容不等于正式内容冻结，不能从 Pre-8/30 旧规格恢复固定产品模型。Status 不另立顶层 GameSpec；Battle 状态规则由 Battle Current GameSpec 管。独立 Item Card 产品模型已被替代。

Pre-8/30 Legacy GameSpec 已退出活动仓库。如需旧规则、旧接口、旧 DTO 或迁移起点，通过 Git History 按需追溯；不得从历史内容恢复 Current 产品规则。旧验收 checkbox 或较新的历史修改日期也不能提升其玩法权威。

`Product-Home-World-GameSpec-V0.1.md` 中的“WP-2”是旧编号，不是当前 Milestone A 的 WP-2。该旧编号不构成当前任务队列。Person、Village、Profession Foundation、Social 与日内 A～D 已有已发布子范围，实际状态查项目状态快照；完整 World / Narrative 与原 WP-AL-5 仍未启动，不能由旧编号自动授权。

当前实现事实核对 [项目状态快照](../Docs/项目状态快照.md)、代码与 Unity Editor；实现不自动修改规则。

## 使用规则

1. 先从本文件确认领域 Authority、状态和边界，再读取与当前任务直接相关的单份规格，不默认加载全部规格。
2. **规则和验收标准是权威**；规格正文描述“应该怎样”，代码和 Unity Editor 描述“目前怎样”。两者冲突时不得自动用实现覆盖规则，也不得用旧规格否认当前实现。
3. 规格正文中的类名、方法名、当前 UI 实现等属于“实现备注”，用于说明实现现状，**不能覆盖规则正文**；实现备注失效时应核对代码，而不是反向改变玩法规则。
4. 验收标准中的 `[ ]` 表示资料库中尚未登记该条书面验收证据，不等于功能一定未实现或从未 Play 测试。
5. 只有交接事实明确覆盖某条标准且用户验收范围包含该行为时，才建议更新 checkbox。
6. 文档头部日期可能晚于或早于部分正文版本标记，日期仅作线索；实际变更需要结合 Git、项目快照和交接核对。
7. 规则变化必须来自用户确认的设计决定，不能由 Cline、状态管理员或当前实现自行推导。
8. 同一系统存在明确标记为 Current / Active 的新版规格时，旧版同系统规格只作为迁移或历史参考；除非新版入口明确保留某一条，否则不得把旧规则补回当前玩法。

## 当前已知待核对项

- Battle 旧固定金币/经验及失败安慰金币是否与新卡牌奖励并存，仍需设计决定；旧结算规格不是 Current 答案。
- Save 触发职责见[问题 40](../Docs/问题清单.md)；旧 DTO 或调用位置不裁决长期 Ownership。
- Pre-8/30 旧规格的验收 checkbox 不自动代表 Current 验收状态；旧 Shop / Recruit / Event、固定地图节点及 Death's Door 仅作历史/兼容追溯。
