# RRPG｜Identity 角色内容设计专题

**内容基线：2026-09-16；导航修订：2026-09-17**  
**Freeze Level：Concept / Not Fully Frozen**  
**阶段标签：第一产品阶段角色内容设计已收口，可进入原型验证（Prototype）**  
**职责：GameDesign 角色内容专题；不承担 Person / Battle Current Rule Authority**

> 本专题使用 `Identity` 作为产品内容设计术语，用来表达“长期人生形成的稳定身份”。  
> 当前 [GameSpec/Person/](../../GameSpec/Person/README.md) 与 Runtime 仍使用 `Profession`，Formation / Starter / Trait / Full Card Pool 等 Current Rule 继续服从 GameSpec；最终命名未冻结。

---

## 1. Authority 与阅读边界

读取顺序：

1. [GameDesign/04 Person.md](../04%20Person.md)：人物长期设计上游；
2. 本 README：进入 Identity 内容专题；
3. 对应 `Characters/<Identity>.md`：角色内容语义；
4. [Identity角色内容设计工作台.xlsx](Identity角色内容设计工作台.xlsx)：结构化内容候选；
5. 需要方法时读 [角色设计方法论.md](角色设计方法论.md)；
6. 具体 Card / Trait 机制卡住时读 [卡牌与Trait设计工具箱.md](卡牌与Trait设计工具箱.md)；
7. 涉及可执行规则时回到 [GameSpec/Person/](../../GameSpec/Person/README.md) / [GameSpec/Battle/](../../GameSpec/Battle/README.md)。

Authority 原则：

> **Current GameSpec > 本专题任何候选内容。**

本专题不会重新定义：AP、Adjacency、Card Flow、Starter Slot、Unlocked Pool、Profession Trait、Formation 或其它 Current Rule。

---

## 2. 术语

| 术语 | 本专题含义 | 当前规则关系 |
|---|---|---|
| Identity | 长期人生形成的稳定身份；本轮产品内容设计术语 | 当前 GameSpec / Runtime 对应工作模型仍称 Profession |
| Battle Identity | 该身份进入 Battle 后，让玩家反复思考的核心问题 | 设计术语，不是 Runtime 字段 |
| 母题 | 身份经验翻译为 Battle 的上层策略抓手 | 设计术语 |
| 子题 | 母题下真正不同的玩家问题 | 设计术语 |
| Build Seed | 一组内容开始持续互相重新估值并具有成型潜力 | 可跨 Identity 涌现 |
| 强候选（Strong Candidate） | 置信度标签 | **不是 Freeze Level** |

本专题五角色具体内容默认 Freeze Level：**Concept / Not Fully Frozen**。

---

## 3. 第一产品阶段 Scope

| Identity | Battle Identity | 核心体验 | 阶段状态 |
|---|---|---|---|
| Farmer 农民 | 促成并经营成长过程 | 成长积累 → 决定何时兑现 | 设计阶段收口 / 待原型验证 |
| Hunter 猎人 | 识别并捕获机会 | 识别窗口 → 精准捕获 | 设计阶段收口 / 待原型验证 |
| Cook 厨师 | 料理已有输入，让既有价值更高效地被使用 | 加工增效 → 资源跑顺 | 设计阶段收口 / 待原型验证 |
| Worker 劳工/帮工 | 通过主动重构关系补上结构缺口 | 结构杠杆 | 设计阶段收口 / 待原型验证 |
| Herbalist 采药人 | 判断什么处理在什么状态下才正确 | 风险掌控 | 设计阶段收口 / 待原型验证 |

Closure 只表示本轮纸面设计可以停止，不提升 Freeze Level。

---

## 4. 日常工作入口

### 看角色

`Characters/<Identity>.md` → `Identity角色内容设计工作台.xlsx / 01_角色总览 / 02_母题总览`

### 设计卡牌

`02_母题总览` → `03_母题子题` → [卡牌与Trait设计工具箱.md](卡牌与Trait设计工具箱.md) → `04_卡牌配置` → `06_阶段与解锁` → `09_验证与开放问题`

### 设计 Trait

先确认 [GameSpec/Person/](../../GameSpec/Person/README.md) 当前 Trait 规则，再使用工具箱和工作台。  
当前 Current Rule：Trait 归位为 **Profession Trait**，不存在正式独立 Person-specific battle Trait。

### 接入 Runtime

本 XLSX 是设计工作台，不是 Runtime 数据源。  
当前工程内容源仍在 `Development/Excel/`，尤其是 `Profession.csv`；正式映射必须另建开发任务。

---

## 5. 五角色原型验证（Prototype）核心问题

- Farmer：成长节奏与收获时机是否真正形成爽感；
- Hunter：机会窗口频率与可读性是否足够；
- Cook：什么时候出现明确“整套资源跑顺了”的 Breakpoint；
- Worker：全局 Position / Adjacency 等空间 Fact 的引用密度是否足够；
- Herbalist：透支 → 调理是否形成有张力的风险循环。

---

## 6. Future Identity

Carpenter、Drunk、Tavern Owner、Village Chief 等历史候选未被删除，只是不属于当前第一产品阶段 Battle Scope。

历史设计演进与本轮 Closure 证据写入：

[Docs/DesignCheckpoints/2026-09-16_RRPG_Identity-Content-Design_Concept-Checkpoint-V0.1.md](../../Docs/DesignCheckpoints/2026-09-16_RRPG_Identity-Content-Design_Concept-Checkpoint-V0.1.md)


## 日常操作流程

在工作台点击蓝色文字跳转。从本仓库打开工作簿并保留相对目录结构，以使用专题 Markdown 与 Current 规则入口；单独打开 XLSX 时，工作表内部导航仍可使用，仓库文件入口需要对应文件。规则链接使用相对于当前文件位置的仓库路径。

| 步骤 | 入口 | 怎么做 | 写在哪里 | 完成标准 |
|---|---|---|---|---|
| 1 定位玩家问题 | 01_角色总览 | 选择角色后查看母题总览和子题明细 | 04：身份、母题、子题、玩家问题 | 能用一句话说清这张牌何时值得用 |
| 2 选择设计零件 | 98_工具与Authority索引 | 查工具箱的零件目录和对应规则出处 | 04：目标域、触发/条件、效果正文 | 每个新概念有来源；未定义规则记问题 |
| 3 录入内容 | 04_卡牌配置 | 复制同类行，分配新设计ID；修改卡保留原ID | 04：名称、状态、是否候选、费用与参数 | 验证样例不自动纳入内容；空值不当0 |
| 4 安排首次出现 | 06_阶段与解锁 | 回到卡牌/特性表修改内容阶段；此表读取阶段 | 06：前提、环境要求、设计理由 | 首次出现有可用环境；等级经验保持未定 |
| 5 检查关联 | 03_母题子题 | 按ID核对母题、种子、组合和原型问题 | 03、07、08：受影响关联；09：问题详情 | 无悬空ID；总览数量跟随明细更新 |
| 6 记录验证 | 09_验证与开放问题 | 写现象、验证方法、证据和下一步 | 09：问题状态、判断、验证方式、日期 | 纸面推演与实际原型分开；无证据不关闭 |
| 7 修改后复核 | 04_卡牌配置 | 核对相关特性、阶段、种子与角色说明 | 04备注：日期、原方案、新方案、原因 | 保存设计变更；规则变化需单独归位 |

### 修改已有卡牌

按卡牌ID定位原记录，保留ID。先在备注记录修改日期、原方案、新方案和原因，再修改效果/条件/参数。检查关联 Trait、母题、种子、阶段与问题。具体规则变化只在工作台中保留为候选，并登记需要更新的 GameSpec；本流程不授权自动更改规则。

### 字段由哪里维护

- 卡牌/特性表：维护名称、候选状态、具体效果与首次出现阶段。阶段表对已关联卡牌/特性的名称、状态、冻结层级与阶段使用公式读取，修改源行即可。
- 母题总览：数量由子题明细计算；代表卡牌只是导航例子。编辑子题后，核对角色 MD 的摘要是否仍准确。
- 子题“适用阶段范围”：可跨阶段，表达设计空间；不是卡牌的首次解锁阶段。
- 问题表：维护问题详情与验证证据；卡牌只保存问题ID。
- 设计ID是长期引用键，不是 Runtime ID；新增行复制相邻行格式和下拉，登记关联后检查跳转。自动汇总扫描至第1000行，超过时扩展范围。

### 五角色直达

[农民 Farmer](Characters/Farmer.md) · [猎人 Hunter](Characters/Hunter.md) · [厨师 Cook](Characters/Cook.md) · [劳工 Worker](Characters/Worker.md) · [采药人 Herbalist](Characters/Herbalist.md)

### 查术语与规则

中文字段和英文对应关系见工作台 99_枚举与术语。保留英文身份ID和仓库七级 Freeze 原文便于对照；不要将设计置信度当作冻结等级。

[零件查询目录](卡牌与Trait设计工具箱.md#零件查询目录) · [人物规则](../../GameSpec/Person/Person-Profession-Growth-GameSpec-V0.1.md) · [战斗规则](../../GameSpec/Battle/Battle-GameSpec-V0.1.md)
