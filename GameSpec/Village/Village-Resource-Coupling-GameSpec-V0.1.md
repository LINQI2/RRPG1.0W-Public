# Village-Resource-Coupling-GameSpec-V0.1

> 版本：V0.1
> 日期：2026-09-07
> 状态：Current / FP-V0.1 / Minimal Village Resource Coupling
> 职责：WP-3｜Minimal Village Resource Coupling 的正式执行 Authority 与验收边界；定义 Farm → Ingredients → Kitchen → Food → Village Consumption / Expedition Carry 与 Person Fulfillment / Hunger 的可执行规则。
> 上承接：`GameSpec/Village/Village-GameSpec-V0.1.md`（V0.1 Resource Authority 最小语义、VillageTime 唯一权威源、第 11 章 Minimum Village World Integration 边界）
> 关联：`GameSpec/Expedition/Expedition-Progression-GameSpec-V0.1.md`（Expedition Supply 语义 cross-sync）；设计意图见 `GameDesign/07 Village.md` §7.14。
> 注意：本文件描述 FP-V0.1 规则，不维护实时实现状态；WP-3 完成事实及验证见 `Docs/项目状态快照.md`，具体代码事实仍应核对实现。

---

# 0. 版本目标与系统定位

## 0.1 正式资源链

FP-V0.1 正式最小资源链：

```text
Farm
↓
Ingredients
↓
Kitchen
↓
Food
├─ Village Consumption
└─ Expedition Carry / Supply
```

Person 影响链：

```text
Food Allocation
↓
Equal Share
↓
Person Daily Fulfillment
↓
Consecutive Underfed Days
↓
Hungry / Shortage State
```

核心原则：

> 资源后果优先落实到具体 Person，再汇总为 Village 状态。

## 0.2 负责

- Building-centric Production 与 Processing；
- 日内真实劳动中的生产/加工与午餐消费、日末聚合；
- Person Daily Fulfillment 与 Consecutive Hunger；
- Risk / Notification 语义；
- Expedition Carry 的真实资源转移与返还；
- 与唯一VillageTime分钟边界及日末聚合、Assignment / Workplace、Expedition Return链对接的规则边界。

## 0.3 不负责

- 决定 Person 是否在村 / 是否可用 / 是否工作（归 Assignment / Occupancy Authority）；
- Expedition Progression 的 Node / Branch / Return / Persistence（归 Expedition GameSpec）；
- Person 社会行为层（进食动画、排队、饭点等）；
- 最终全量资源体系与正式数值。

---

# 1. Resource Set

FP-V0.1 最低资源：

- **Ingredients**；
- **Food**。

不定义最终全量资源体系；不恢复旧 Food / Wood / Stone / Gold 四资源正式体系。

---

# 2. Building-centric Production

生产主体是 Building / Workplace，而不是具体 Person。

```text
Actual Output
=
Base Building Output
× Staffing Efficiency
```

Person 第一版不直接拥有独立“日产 X 资源”的产品规则。

---

# 3. Hard Capacity + Linear Staffing

FP-V0.1：

> Functional Capacity 为硬上限。

第一版 Staffing Efficiency 可按：

```text
Filled Slots / Capacity
```

近似线性计算。

例如 Capacity = 2：

- 0/2 → 0%；
- 1/2 → 50%；
- 2/2 → 100%。

具体 Capacity 数值配置化。

---

# 4. Actual Labor Production

生产按有效人员实际到岗后的劳动分钟推进整批/批次余量；不能按挂名人数在午夜再补整日产出。材料在批次开始投入，暂停保留余量，完成才产出。Daily Unit与时段/通勤参数为Prototype / Not Frozen；Harvest Cycle / Monthly Yield仍Deferred。

# 5. Configurable Conversion Recipe

Kitchen 转换不得代码写死倍率。

至少以配置语义表达：

```text
InputResource
InputAmount
OutputResource
OutputAmount
```

具体 Ingredients → Food Ratio 为配置值，不属于 Architecture Freeze（不写死 1:1）。

---

# 6. Kitchen Automatic Processing

正常情况下：

> Kitchen在实际到岗劳动与可用材料约束内自动推进加工批次；Recipe参数可配置，不在午夜重复整日加工。

前提：

- 有有效 Staffing；
- 有 Ingredients。

FP-V0.1 不要求手动加工数量、Reserve Threshold、Recipe Priority、多配方选择。

---

# 7. Consumption Priority

Village 标准消费顺序：

```text
Food
↓ insufficient
Ingredients Emergency Consumption
```

- Food 是标准消费资源；
- Ingredients 是低效 Emergency Consumption 资源；
- 具体 Consumption Value 配置化。

---

# 8. InVillage Demand

只有当前 InVillage Person 产生 Village Food Demand。

```text
InVillage Person
→ Village Food Demand

Expedition Person
→ 不计入 Village Food Demand
→ 由 Expedition Supply 承担
```

Return 后重新计入 Village Demand。

---

# 9. Uniform Base Demand

FP-V0.1 每个 InVillage Person 使用统一基础 Food Demand。

后续可扩展 Age / Injury / Special State Modifier，第一版不实现。

---

# 10. Equal Share

当可用标准食物价值不足时：

> 默认 Base Allocation = Equal Share。

不按 Profession / Status / Rank / Random / Personality 进行基础优先分配。

---

# 11. Person Daily Fulfillment

每个 Person 独立记录：

> Daily Food Fulfillment Ratio。

Village-level shortage summary 应由 Person-level Facts 汇总产生。

---

# 12. Consecutive Hunger Model

Hunger 基于：

- Fulfillment Threshold；
- Required Consecutive Days。

`RequiredConsecutiveDays = 1` 必须天然支持“立即生效”。

FP-V0.1 可保持最小 Normal / Hungry 或等价结构。

---

# 13. Recovery

第一版可采用简单规则：

> 当天恢复正常 Fulfillment → Underfed 连续计数清零；Hungry 状态恢复规则可保持最小可逆实现。

更复杂恢复滞后 Deferred。

---

# 14. Intraday Resource / Lunch / Daily Aggregation Order

VillageTime唯一分钟边界驱动：实际到岗劳动 → Farm整批产出/ Kitchen材料投入与整批加工 → 午餐Food优先、Ingredients应急 → Equal Share Fulfillment / Hunger → 午夜聚合与Formation。当天已完成产出可用于后续加工/午餐，未完成批次不提前变成成品；不在同一午夜同时再次执行旧日产/日加工。

只给供餐时实际在村人物记营养；远征期间不额外计Village Demand，返程合法补给只校正该日已有营养证据，不补猜缺席日。日内Staffing随实际劳动/外出变化，不沿日初名册假定全天执行。复杂进食动画、Needs、疲劳仍不属于该规则。

# 15. Risk / Notification Rules

## 15.1 Risk Visible

例如：

> Food `-2/day`

只作为 HUD / Inspector 的被动信息，不主动 Alert。

## 15.2 Impact Notify

当 Food 不足，开始依赖 Ingredients Emergency Consumption，第一次发生时 Notice。

## 15.3 Sustained Impact

连续依赖应急资源：

> Warning / Explanation / Response Opportunity。

## 15.4 Actual Shortage

当 Food + Emergency Ingredients 都不足，则记录具体 Person 的不足 Fulfillment。

## 15.5 Consequence

持续 Hunger 必须允许产生可逆 Consequence。

但 FP-V0.1 本 GameSpec 不冻结具体效率惩罚、健康惩罚、Mood、Death、Social penalty。

只冻结：

> 持续忽视不能永远没有后果。

---

# 16. Expedition Carry / Supply Coupling

## 16.1 Departure

出征携带 Food 为真实资源转移：

```text
Village Food -= CarryAmount
Expedition Carry += CarryAmount
```

不复制资源。

## 16.2 Expedition Person Demand

出征 Person 不再参与 Village Food Consumption（见 §8）。

## 16.3 Base Supply Consumption

正式修正早期候选：

> **Base Expedition Supply Consumption 由 Party Size × Expedition Elapsed Time × Consumption Rate 推导。**

```text
BaseSupplyConsumption
=
PartySize
× ElapsedTime
× PerPersonConsumptionRate
```

整次基础消耗累计向上取整：ceil(累计 PartySize × PerPersonDailyRate × DurationMinutes / 1440)，每节点只扣累计新增差额，另加Explicit Extra。不得每节点独立ceil。具体数值与Party Size计数仍Not Frozen。

## 16.4 Extra Supply Cost

Node / Encounter 只有在特殊内容明确需要时，才提供 Explicit Extra Supply Cost，例如绕路、丢失补给、特殊 Event、环境代价、额外安全消耗。

禁止普通 Node 因 Time Cost 已产生基础 Supply 消耗后又固定重复扣 Supply。

## 16.5 Total Supply

```text
Total Supply Cost
=
Base Time-derived Supply Consumption
+
Explicit Extra Supply Cost
```

## 16.6 Return

Safe Return / Retreat：

> 未消耗 Expedition Carry 默认可返还 Village。

Failure / Event 是否损失额外 Carry 为 Deferred。

## 16.7 Catch-up Ordering

必须保持世界语义：

```text
Expedition End
↓
Remaining Carry still Away
↓
Village Catch-up
↓
Catch-up Complete
↓
Remaining Carry returns to Village
↓
Clear Expedition Occupancy
↓
Final Save
```

核心原则：

> 未来带回的 Food 不得参与其返回之前那些天的 Village Catch-up。

具体 V4F 函数调用顺序留给 WP-3 Final Development Spec 做代码级审查。

---

# 17. Abstract Meal Resolution

FP-V0.1 可以把 Meal Resolution 视为 Daily Settlement 中的抽象阶段。

不要求真实 18:00 饭点、NPC 去 Kitchen 排队、真实进食路径、饭点动画、Hour-level Schedule。

社会行为层继续 Deferred。

---

# 18. Explicitly Deferred

不得在本轮升级 Current：

- Personality；
- Relationship；
- 偷食；
- 让食；
- 抢食；
- 口粮优先政策；
- Child / Elder Demand；
- Injury Demand；
- Daily Schedule；
- Hour-level Meal；
- Real Eating Path；
- Work Hours；
- Farm Harvest Cycle；
- Monthly Lump-sum Yield；
- Food Quality；
- Multi-recipe；
- Multi-input Recipe；
- Storage Capacity；
- Spoilage；
- Logistics；
- Warehouse；
- Reserve Threshold；
- Food Shortage Death；
- Disease；
- Mood；
- Final Hunger Consequence values。

---

# 19. Cross-document Authority 与未冻结数值

- Production / Processing 使用既有 Workplace / Building 语义；Functional Capacity 复用现有 Assignment / Workplace Capacity 语义，不建立第二套 Capacity；
- Person Presence / Occupancy / Availability 继续由既有 Village Runtime 决定；本 GameSpec 只定义资源的 Daily Settlement 消费侧规则；
- Expedition Progression（Node / Branch / Return / Persistence / Legacy Map / RunState）Authority 见 `GameSpec/Expedition/Expedition-Progression-GameSpec-V0.1.md`，本 GameSpec 不重新定义；
- Product Home World 表现层见 `GameSpec/Village/Product-Home-World-GameSpec-V0.1.md`，本 GameSpec 不修改其 Presentation / Selection / Inspector / Camera 规则；
- 未冻结：所有具体数值（Base Building Output、Capacity、Conversion Ratio、Demand、Consumption Value、Consecutive Days、Hunger Consequence）、Global Inventory 实现结构、Daily Delta 正式公式；
- 测试内容 / 测试值不得自动升级为正式设计；
- 文档归位阶段不产生实现验收；WP-3 Final Development Spec 生成后，本规格作为其规则 Authority。

## 19. 累计补给与日内劳动/午餐（Published Main / Runtime Verified）

§16 基础补给采用整次累计 ceil、节点只扣新增差额加 Explicit Extra；规则及13组断言见[A V0.2 §5](../../Docs/DevelopmentSpecs/2026-10-05_RRPG_Intraday-Time-Expedition-Arrival_Development-Spec-V0.2.md)。实际返程、FinalSave和累计成本已本地验证。Calibration Not Frozen。

[B～D 规格](../../Docs/DevelopmentSpecs/2026-10-05_RRPG_Intraday-Work-Lifecycle-Onsite_Development-Spec-V0.1.md)已连续授权并验证：实际到岗劳动兑现整批；厨房材料在批次开始投入，缺料不累计虚假加工，换人/SaveLoad不双发。午夜停止旧整天生产/供餐，日聚合不再第二次扣粮。

午餐在 Noon 单次 Equal Share：在村且可用者得到相同比例，不按列表先后发整份；远征/死亡/Unavailable 不消费、不写零满足、不递增 Hunger。午后返村不补午餐；合法远征补给只清除该日已登记的营养不足，不重复处罚或扣粮。具体工作时间、产能、配方、需求/阈值等仍 Prototype / Not Frozen。实际证据与先前整份列表差异的修复见[实施记录](../../Docs/DevelopmentSpecs/2026-10-05_RRPG_Intraday-AD_Execution-Record.md)。
