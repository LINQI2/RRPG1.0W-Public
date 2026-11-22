# RRPG｜WP-E Expedition Journey Visual Foundation V0.1｜Final Development Spec V2

> 日期：2026-09-18  
> 项目：RRPG  
> Work Package：WP-E｜Expedition Journey Visual Foundation V0.1  
> 类型：Final Development Spec（FDS）  
> 修订：V2｜继任开发顾问复核版  
> 状态：制作人确认方向后的最终执行输入；E0 文档已落仓，Repository Closed 后直接进入 WP-E-T0  
> 开发远端核对基线：`LINQI2/RRPG1.0W` `main` = `36eff960dc49902fe31469ab3e53315e740a2c89`  
> 前置：WP-D 已 Closure Commit + Push；Authority Sync V2、Current 状态校正与本 FDS 已作为 E0 文档一次性落仓（单一前置，不再拆分后续独立 Landing / E0.5 流程）  
> Git：本 Spec 不授权 Commit / Push  
> V2 核心修订：  
> 1. 不再预先冻结 `PersonVisualCatalog / PersonVisualResolver / PersonVisualProfile` 为长期架构，只冻结 `PersonId → exact Person → Presentation Visual` 解析契约；  
> 2. 新增 `Presentation Cycle → at most one Progression Transition` 契约与连续 Placeholder / N=1 链回归测试；  
> 3. Authority Sync V2、Current 状态校正与本 FDS 已作为 E0 文档一次性落仓；本 FDS 即 WP-E 的正式开发期 Binding，不再生成第三份大型执行文件；  
> 4. WP-E 完成后进入 Milestone A Product Chain Gap Audit，不自动把无限远征作为下一 Work Package。

---

# -1. Authority 与输入

执行前必须读取并服从：

1. `.clinerules.md`
2. `AI_CONTEXT.md`
3. `Docs/AI开发执行规则.md` 相关章节
4. `Docs/AI多Agent开发协作协议.md`
5. `Docs/文档地图.md`
6. `Docs/项目状态快照.md`
7. `Docs/开发验收交接.md`
8. `GameSpec/README.md`
9. `GameSpec/Expedition/Expedition-Progression-GameSpec-V0.1.md`，含 2026-09-18 Journey Presentation Contract
10. `GameDesign/07 冒险系统.md`
11. `GameDesign/当前设计状态.md`
12. `Docs/决策记录.md` D-017
13. `Docs/DevelopmentSpecs/2026-09-07_RRPG_WP-1_Expedition-Progression_Final-Development-Spec.md`（历史开发 Binding，只用于理解 Current seam）
14. `Docs/DevelopmentSpecs/2026-09-18_RRPG_WP-B_Expedition-Return-Integrity_Development-Spec.md`
15. `Docs/DevelopmentSpecs/2026-09-18_RRPG_WP-D_Battle-Visual-Foundation-V0.1_Development-Spec.md`
16. 当前代码、Scene / Prefab / Inspector 与 Unity Editor 实时状态

冲突处理：

> Current GameSpec / 用户当前确认 > 本 FDS > 旧 Development Spec / Legacy Map 实现。

本 FDS 本身就是 WP-E 的正式开发期 Binding（落仓后不再生成第三份大型执行文件），不反向冻结正式美术、正式动画、正式 Encounter 内容、无限生成算法或长期人物美术数据架构。

---

# 0. Work Package 目标

WP-E 不重写 Expedition。

它负责把当前玩家看到的运行时黑色 Debug Progression Panel 升级为：

```text
Expedition Prep
→ 同一批 Person 组成可见 Journey Party
→ 队伍向右旅行
→ Encounter / Branch 自动中断
→ Battle / Placeholder Encounter
→ 回到 Journey
→ Return / Retreat
```

同时建立最小人物视觉解析接缝：

```text
PersonId（具体人物身份）
→ Resolve exact runtime Person
→ Resolve presentation visual by existing / minimal binding
→ Journey Member visual / default placeholder
```

这里冻结的是 **Resolution Contract**，不是某个 Catalog / Profile 架构。

目标不是“画出正式远征美术”，而是：

- Journey 有稳定、可替换、可调的 Scene / Prefab Visual Hosts；
- Current Expedition 全部功能语义保留；
- 自动旅行与 Continue Skip 共用同一推进入口；
- 一次 Presentation Cycle 最多消费一个 Progression Transition；
- 行走、Pause、Skip 不进入规则 Authority；
- Prep 选中的具体 Person 原样成为 Journey 队伍；
- Placeholder 与未来正式素材走同一 Presentation Host 路径；
- 不建立第二套 Party、Progression、Time、Supply、Return 或 Person Identity；
- 不因为本次占位素材提前锁死未来 Sprite / Animator / Spine / 换装生产管线。

完成建议状态：

> **WP-E Expedition Journey Visual Foundation Technical Verified / Visual Preview Ready**

不等于：

> 正式远征美术完成 / 无限远征完成 / Product Visual Accepted / Milestone A 完成。

---

# 1. 本 WP 明确负责

## 1.1 Journey Presentation

- Journey Background / Environment Host；
- Journey Party Host；
- 每位远征成员独立的 Journey Member View；
- 默认向右旅行的表现；
- Pause / Resume Presentation；
- `继续前进`跳过当前旅行表现；
- 可选的 Presentation Speed 倍率入口（非 Technical PASS 必需）；
- Encounter Overlay；
- Branch Overlay；
- Supply / elapsed days / Party / Return / Message HUD；
- 当前进入失败原因的玩家可见反馈；
- Battle 前隐藏 / 冻结 Journey，Battle 后恢复 Journey；
- 0 / 1 / N 后继的表现协调；
- Presentation Cycle 生命周期与旧 callback 失效；
- 连续 Placeholder / N=1 链不得在同一旧 callback 中吞多个 Progression Transition。

## 1.2 Minimal Person Visual Resolution Contract

硬要求：

- 以 `PersonId` 为公开身份输入；
- 精确解析当前 `CharacterInstance` / `HeroInstance` / Person；
- 模板 / Definition 键只能在 exact Person 已解析后用于外观查询；
- 提供 default player placeholder；
- 至少验证两个不同 `PersonId`可显示为两个独立 Member View，并能使用可区分视觉；
- 两个同模板 Person 不合并；
- Journey 当前接入；
- 具体视觉数据存储方式由 T0 后基于现有仓库、最小改动面与未来替换成本决定。

允许的最小实现候选包括但不限于：

- 现有 Person / Character / Hero 上已有可复用 Presentation 字段；
- 一个轻量 SO / lookup table；
- Presentation key → Sprite 的最小映射；
- 小型 resolver / adapter。

本 FDS **不要求** 必须存在：

- `PersonVisualCatalog`
- `PersonVisualResolver`
- `PersonVisualProfile`
- 特定 SO Schema
- 特定资源目录层级

如果 T0 证明 Catalog 方案最小、清晰、无长期锁死风险，可以采用；它是 **WP-E Implementation Decision**，不是产品 / 长期 Architecture Authority。

## 1.3 现有功能迁移

以下产品语义必须保留：

- Supply 显示；
- elapsed days 显示；
- Continue；
- Branch；
- Return / Retreat；
- 无法推进原因；
- Encounter 进入与完成；
- Battle 返回；
- 0 后继不自动判胜；
- V4F Return 恢复入口。

退出的是 Debug 视觉形态与开发术语，不是上述功能。

---

# 2. 本 WP 明确不做

- 不重写 `ExpeditionProgressionResolver`；
- 不修改 Node enter / resolve / branch / supply / elapsed rules；
- 不实现无限远征生成；
- 不实现 Encounter Pool / Biome / World Generation；
- 不修改 `ExpeditionGraph`完整预生成约束；
- 不修改 Boss / Route Terminal；
- 不设计正式 Event / Shop / Recruit / Rest；
- 不实现正式 Event Choice 结果；
- 不修改 Run Save / Resume；
- 不修改 SaveData / SaveVersion；
- 不修改 V4F Return 顺序；
- 不新建 Party gameplay state；
- 不把 Journey movement 变成规则时间；
- 不生产正式人物美术、正式背景、Animator、Spine、换装或模块化身体系统；
- 不冻结长期 Person Visual Catalog / Profile / Sprite Schema；
- 不改 Battle 规则或 WP-D 视觉结构；
- 不改 Prep 的正式视觉；
- 不改 Village World；
- 不引入新 Package；
- 不冻结最终队伍人数、间距、层级、构图、速度、颜色与动画节奏；
- 不用“以后可能复用”作为理由预建跨所有界面的统一美术生产系统。

---

# 3. 当前已知 Existing Host Map（执行前必须本地复核）

远端 `36eff96` 已知事实：

| 当前对象 | 已知职责 | WP-E 处理 |
|---|---|---|
| `ExpeditionSystem` | Prep 选择 Character Cards + optional Hero；确认出征 | 保留规则与选择真相 |
| `AdventureManager._playerChars / _playerHero` | 本次远征参与对象 | 继续作为顶层 Orchestrator 数据来源 |
| `AdventureManager.GetExpeditionPersonIds()` | Card explicit binding + Hero `PersonId`，返回准确成员身份 | Journey Party 唯一成员来源 |
| `AdventureManager.GetAvailableProgressionNodes()` | 只读 0 / 1 / N 合法后继 | Journey Coordinator 只读 |
| `ExpeditionUI.OnProgressionNodeChosen()` | Current node intent 产品入口 | 自动抵达、Skip 与 Branch 最终统一调用 |
| `ExpeditionUI.RequestRetreatFromProgression()` | Current Return 产品入口 | Journey Return 只调用它 |
| `ExpeditionProgressionPanel` | 运行时自建黑色最小 UI；显示 Supply、Continue / Branch、Retreat、拒绝原因 | 功能语义迁移；不作为最终视觉宿主 |
| `MapUI` | Legacy Compatibility view | 不恢复为 Current 玩家表面 |
| `BattleUI.InitFromExpedition()` | Encounter / Battle 接入 | 零规则修改；Journey 只负责隐藏/恢复 |
| `PersonDefinitionId` / `CharacterId` / `HeroId` | 静态定义 / 外观候选键，不是 Person identity | 只在 exact `PersonId`解析后用于视觉查询 |
| WP-D `UnitCard.prefab` | Battle 交互 / HP / Shield / Intent / Position 宿主 | 不复用为 Journey Member View |

T0 必须通过本地与 Unity MCP 补充：

1. Local branch / HEAD / origin/main / worktree / staged / untracked；
2. SampleScene 当前 Canvas 层级；
3. ExpeditionUI、Battle Canvas、MapUI 与 EventSystem 的真实绑定；
4. Current `ExpeditionProgressionPanel`运行时生命周期；
5. Battle 返回 Journey 的全部调用点；
6. 当前人物 / Hero / PersonDefinition 是否存在任何 Sprite / presentation key / image / icon 等可复用字段；
7. `PlayerCollection`按 `PersonId`查询 Character / Hero 的真实 API；
8. 当前 Resources / SO / Art / Prefab 目录规范；
9. 当前测试对 ProgressionPanel 对象名 / 文案 /运行时建 Canvas 的依赖；
10. Placeholder non-Battle node 的 enter → resolve → refresh 调用链是否同步；
11. 连续 Placeholder / N=1 情况下是否存在同调用栈继续推进的现有 seam；
12. 当前可用于延迟 / 重调度的 Unity 生命周期或既有队列机制；
13. 本 WP 准备复用、扩展、新建、停用的精确 Host Map；
14. Person Visual Resolution 的最小候选方案比较：现有字段复用 vs 轻量 mapping / SO vs 其它 adapter；
15. planned file whitelist / serialized asset list / tests。

Green Existing Host Map 自主继续；只有需要第二套 Authority、改规则、改 Save、扩大 Scope 或无法满足 Single-transition Contract 才升级为 Red。

---

# 4. Journey Presentation Architecture

## 4.1 单向依赖

```text
AdventureManager / ExpeditionProgressionResolver
  ├─ exact PersonIds
  ├─ available next nodes
  ├─ supply / elapsed days
  └─ existing node / return intent entrypoints
                ↓
JourneyPresentationCoordinator
  ├─ presentation-only phase
  ├─ travel playback / pause / skip
  ├─ 0 / 1 / N projection
  ├─ presentation cycle lifecycle
  └─ exactly-once / single-transition guard
                ↓
JourneyVisualRoot
  ├─ Environment Host
  ├─ Party Host
  ├─ HUD
  ├─ Encounter Overlay
  ├─ Branch Overlay
  └─ Message Host
```

禁止反向：

- Visual Host 直接写 Progression State；
- Coordinator 自己决定合法 Node；
- Journey Party 修改 AdventureManager 成员；
- 动画回调直接扣 Supply / elapsed days；
- HUD 复制规则公式；
- 旧 Presentation callback 在已经消费一次 Progression Transition 后继续 Enter / Resolve 后续 Node。

## 4.2 Presentation-only Phase

允许一个只存在于 Journey UI 的 phase，例如：

```text
Hidden
Traveling
Paused
AwaitingBranch
EncounterVisible
Terminal
Returning
```

名称可按现有风格调整，但必须：

- 不加入 `ExpeditionProgressionPhase`；
- 不持久化；
- 不进入 SaveData；
- 不拥有 Node / Branch / Return 规则；
- 重开 View 时可以从 Current Progression State 重新派生。

## 4.3 Presentation Cycle

定义：

> 一次 Presentation Cycle 从读取一份 Current Progression State 开始，到本轮 Journey / Branch / Encounter Presentation 完成、被中断或失效为止。

必须保证：

- 每个 Cycle 最多消费一个 Progression Transition；
- 消费后当前 Cycle 结束；
- 下一步必须重新读取 Current Progression State；
- 若仍可继续，建立新的 Cycle；
- 新 Cycle 不得复用旧 Cycle 的完成回调；
- 旧 Cycle 的 callback 在 Return / Hide / Rebind / Battle / View dispose / 新 Cycle 创建后必须失效；
- 连续 Placeholder / N=1 链不能在同一旧 callback / 同一旧调度链中自动吞多个节点。

## 4.4 Exactly-once Presentation Completion

自动动画完成与 Continue Skip 必须汇合到单一逻辑入口，例如：

```text
CompleteCurrentTravelPresentation(reason)
```

必须保证：

- 每一段 travel token / cycle 最多完成一次；
- 自动回调与玩家点击同帧发生时只推进一次；
- Return / Encounter / Hide / Rebind 会取消旧 token；
- 旧动画回调不得推进新一段旅程；
- UI 重开不重复 Enter / Resolve；
- Branch Skip 不自动选择 Node；
- 本轮完成消费一个 Transition 后，入口不得同步继续消费第二个 Transition。

实现机制不冻结。可以使用：

- generation / version token；
- cancellation flag；
- deferred dispatch；
- queue；
- coroutine / next-frame scheduling；
- 其它能被测试证明满足契约的方式。

禁止把“必须 next frame”写死成唯一方案。

## 4.5 真实产品接线

Current `ExpeditionUI`继续作为产品 Orchestrator UI seam：

- Confirm 成功后：绑定 `_currentAdventure` → 绑定 Journey Party → 显示 Journey；
- Journey 需要进入 Node：调用现有 `OnProgressionNodeChosen(nodeId)`；
- Enter denied：Journey Message Host 显示 `DescribeEnterResult`结果；
- Battle 开始：Journey 隐藏或冻结，取消当前 Presentation Cycle；
- Battle 普通胜利：结束旧 Cycle，重新显示 Journey，重新读取 0 / 1 / N；
- Placeholder non-Battle encounter：只允许沿用当前立即 Success 的 Compatibility 行为，并通过 Encounter Overlay 短暂显示占位，不发明 Event 规则；resolve 完成后必须结束当前 Cycle，重新调度，不能同步吞下一 Node；
- Return：只调用 `RequestRetreatFromProgression()` / Current V4F 入口。

不得让 `ExpeditionProgressionPanel`与 Journey 同时成为玩家可操作表面。

---

# 5. 0 / 1 / N Journey Flow

## 5.1 N = 0

显示：

- 无普通后继提示；
- 当前 Supply / elapsed days；
- Return / Retreat；
- 必要的无法推进原因。

不得自动判 Victory，不得自行触发 SafeReturn。

## 5.2 N = 1

```text
Cycle A:
Read one legal next node
→ BeginTravelPresentation(nodeId)
→ Auto complete OR Continue Skip
→ CompleteCurrentTravelPresentation once
→ ExpeditionUI.OnProgressionNodeChosen(nodeId)
→ End Cycle A

then:
Re-read Current Progression State
→ decide whether to create Cycle B
```

自动完成和 Skip 进入同一个 Node，不能形成两套推进逻辑。

如果该 Node 是 Placeholder 并同步 resolve，也不得在 Cycle A 的调用链继续 Enter 下一个 Node。

## 5.3 N > 1

```text
Cycle A:
Read legal next nodes
→ BeginTravelToBranchPresentation(snapshot)
→ Auto complete OR Continue Skip
→ Show Branch Overlay
→ Player selects one legal node
→ Revalidate
→ ExpeditionUI.OnProgressionNodeChosen(selectedNodeId)
→ End Cycle A

then:
Re-read Current Progression State
→ create next Cycle when appropriate
```

约束：

- Branch Overlay 的按钮只能来自当次合法后继；
- Skip 不能自动选择第一个后继；
- 选择前应再次验证 Node 仍合法；
- 失效后继显示明确拒绝并刷新，不静默改选。

## 5.4 连续 Placeholder / N=1 链

必须专门处理和验证：

```text
A (Placeholder, N=1)
→ B (Placeholder, N=1)
→ C (Placeholder / Battle / Branch ...)
```

要求：

- A 的 Travel / Skip 完成后，最多进入 / resolve A 对应的一个 Progression Transition；
- 即使 A resolve 是同步返回，也不得在同一个旧 callback 中继续进入 B；
- B 必须来自新的 Current State 读取与新的 Presentation Cycle；
- C 同理；
- 玩家应能看到每个 Cycle 的表现边界，除非未来另有专门 Fast-forward 产品决定；
- “不连续吞 Node”优先于追求同帧执行效率。

---

# 6. Continue / Pause / Speed-up

## 6.1 Continue

玩家文字可继续显示“继续前进”。

旅行中：

> 立即完成当前 Presentation，并抵达下一 Encounter / Branch / 当前 Cycle 的规则入口。

不在旅行中：

- Encounter / Branch 需要玩家输入时，不得越过输入；
- Terminal 状态不得伪造下一步；
- Return 处理中不可用；
- 不得借 Continue 在一个点击调用链中连续跨多个 Placeholder / N=1 Node。

## 6.2 Pause

Pause 只停止：

- Party position interpolation；
- background / environment movement；
- presentation timer；
- visual-only animation。

Pause 不停止也不修改任何规则时钟，因为 Journey 行走本身没有规则结算。

## 6.3 Speed-up

V0.1 可以不提供正式 ×2 / ×4 控件。

如果实现：

- 只影响 Presentation playback speed；
- 不影响 node elapsed days；
- 不影响 Supply；
- 不影响 Encounter 结果；
- Inspector 参数可调；
- 不保存偏好，除非另有授权。

Technical PASS 只要求 Pause / Resume + Continue Skip。

---

# 7. Journey Visual Hosts

实际对象名在 T0 复核后可按现有命名风格调整，但最低结构必须等价：

```text
ExpeditionJourneyRoot
├─ BackgroundHost
├─ EnvironmentHost
├─ PartyHost
│  └─ JourneyPartyMemberView × N
├─ JourneyHUD
│  ├─ SupplyHost
│  ├─ ElapsedDaysHost
│  ├─ PartyStatusEntry
│  ├─ BtnPauseResume
│  ├─ BtnContinue
│  └─ BtnReturn
├─ EncounterOverlay
│  ├─ ImageHost
│  ├─ TitleHost
│  ├─ BodyTextHost
│  └─ ChoiceRoot
├─ BranchOverlay
│  └─ BranchChoiceRoot
└─ MessageHost
```

要求：

- Root 是稳定 Scene / Prefab 宿主，不以每次运行 `new Canvas`作为唯一正式路径；
- 环境、人物、HUD、Overlay 分层明确；
- Placeholder 与正式素材使用同一 View / binding 路径；
- RectTransform / spacing / scale / travel duration / movement distance Inspector 可调；
- Raycast 只开放给需要交互的 HUD / Overlay；
- 不制造第二套 EventSystem；
- Journey 不作为 Battle Canvas 的子对象；
- Prep `ExpeditionUI`隐藏后，Journey 仍可独立显示。

## 7.1 Environment

最低可以使用：

- 单张背景；
- 1–2 层简单相对移动；
- 地面 / 前景简单占位。

不要求：

- Parallax 正式系统；
- Biome；
- 无限拼接；
- 摄像机重构；
- 正式特效。

## 7.2 Party

- 一个 Party Host 控制整体移动 / 暂停 / 隐藏；
- 每个准确 `PersonId`创建一个 Member View；
- 不写死 3 人；
- 当前实际出征成员全部显示；
- 列表顺序第一版可以沿用 `AdventureManager.GetExpeditionPersonIds()`；
- 间距、局部错位、层级可调；
- 不把队形升级为玩法规则；
- 视觉拥挤属于后续 Visual Tuning，不得偷偷少显示人物。

## 7.3 Encounter Overlay

本 WP 只建立可替换宿主：

- Image；
- Title；
- Text；
- ChoiceRoot。

当前非 Battle 内容仍可使用 Placeholder 文案并立即 Success；不得趁机设计正式选项、属性检定、奖励或 Event Authority。

## 7.4 Branch Overlay

- 只显示 Current 合法后继；
- 不显示 NodeId / enum / `NodeKind`开发术语作为正式玩家文案；
- V0.1 可用“道路 1 / 道路 2”等占位玩家文案；
- 正式路线名称 / 预览信息未冻结。

---

# 8. Minimal Person Visual Resolution

## 8.1 冻结的是契约，不是数据架构

外部语义必须满足：

```text
ResolveVisual(PlayerCollection, PersonId)
→ exact runtime Person
→ obtain presentation visual
→ visual result OR default placeholder
```

允许最终实现不是单一 `ResolveVisual` 方法；关键是职责与数据方向一致。

## 8.2 身份与外观键

硬规则：

1. 先用 `PersonId`在 Current 数据中找到准确 Character / Hero / Person；
2. 找到 Character 后，可读取 `PersonDefinitionId` / `CharacterId`等已有字段作为外观配置候选键；
3. 找到 Hero 后，可读取 `HeroId`或已有展示字段作为外观配置候选键；
4. 模板键只回答“使用哪份外观”，不回答“这个人是谁”；
5. 不按 DisplayName 查询身份；
6. 不用 CharacterId / HeroId 反向生成或替代 PersonId；
7. 两个同模板 Person 可以共享同一视觉资源，但仍生成两个独立 Member View；
8. 找不到 Person 或 visual binding 时使用 default player placeholder，并输出去重后的可诊断 warning。

## 8.3 T0 后选择最小实现

T0 必须比较至少：

- 复用现有字段 / asset；
- 轻量 presentation key mapping；
- 轻量 SO / Catalog；
- 其它仓库已有 pattern。

选择标准：

1. 不建立第二套 Person Authority；
2. 不迫使 SaveData / SaveVersion 变化；
3. 不为 Journey 临时需求污染长期 Domain Model；
4. 正式素材替换时不需要改规则逻辑；
5. 未来如果改 Animator / Spine / 模块化，也能替换实现而不改变上层 `PersonId → exact Person → Presentation Visual` 契约；
6. 修改面最小、测试清晰、符合仓库现有风格。

若采用 Catalog / Resolver / Profile：

> 它们只是 WP-E 当前实现，不得在文档中表述为已冻结长期美术架构。

## 8.4 最小 Visual Result

V0.1 只要求能向 Journey Member 提供：

- body Sprite / image asset 或等价 visual handle；
- optional display scale / offset（优先放在 Context Host / Binding 中，除非 T0 证明进入 visual entry 更合理）；
- default fallback；
- 可诊断来源信息（测试 / warning 用）。

不加入：

- 装备外观；
- 换装部件；
- Runtime skin save；
- Animator Controller；
- Spine；
- 表情；
- 多方向动画；
- Context-specific 正式动作库。

## 8.5 资源与序列化规则

具体资源路径由 T0 按仓库规范决定。

所有 `.asset` / `.prefab` / `.scene` / `.meta`只允许经 Unity Editor / MCP 或 Editor API 修改；禁止直接编辑 YAML。

不要求本 WP 一定创建 `PersonVisualCatalog.asset`。

## 8.6 Placeholder

- 至少准备 default player body；
- 至少两份可区分的 Person placeholder，或通过可验证的 presentation binding 让两名不同 Person 获得可区分视觉；
- 可以是简单轮廓 / 色块，不代表正式画风；
- 不使用版权不明资源；
- 不用 AI 生成正式审美替代制作人判断。

---

# 9. `ExpeditionProgressionPanel` 迁移边界

保留的是功能，不是现有黑色运行时自建视觉。

本 WP 必须：

- 将 Supply / Continue / Branch / Retreat / Denied Message 接入 Journey Host；
- Current 产品路径只显示 Journey Surface；
- 不同时显示 Debug Panel 与 Journey；
- 保留必要 Compatibility / Test seam，直到 consumer map 证明可删；
- 如果停用而不删除，明确标注 Legacy / Compatibility / Not Product Surface；
- 如果删除，必须先更新所有相关测试与调用，并证明无 Current consumer。

本 WP 不要求物理删除 `ExpeditionProgressionPanel.cs`。默认优先停用 Current 产品可见路径，避免扩大清理范围。

---

# 10. Scene / Prefab / Asset 修改规则

- 先通过 Unity MCP 读取实时 Scene / Prefab / Inspector；
- 禁止直接编辑 YAML；
- 使用现有 SampleScene，不新建第二套正式 Expedition Scene，除非 T0 证明现有宿主不可安全复用并进入 Red；
- 新建 Journey Root / Prefab 必须接入真实产品路径；
- 临时 Builder / Runner 默认 Disposable；
- 不改变 Battle Scene Host；
- 不改 Camera / Package / Project Settings；
- 不覆盖用户手调的 WP-D 视觉参数；
- 所有 Placeholder Import 设置由 Unity 正常生成 `.meta`；
- 不为了构造 Person Visual Catalog 而强制新增 SO；是否新增由 T0 决定。

---

# 11. Technical Checkpoints

```text
WP-E-T0  Local / Unity Existing Host + Consumer Map + Visual Resolution Audit
→ T1 Minimal Person Visual Resolution Implementation
→ T2 Journey Root / Party / Environment Hosts
→ T3 0 / 1 / N Coordinator + Presentation Cycle + Continue / Pause
→ T4 Encounter / Branch / HUD + Battle / Return Integration
→ T5 Verification + Visual Preview + Final Closeout
→ Post-WP-E Milestone A Product Chain Gap Audit
```

Green / Yellow 自主继续；Red 才请求制作人决策。

WP-E 完成后不自动开启 Infinite Expedition / Biome / Event / Formal Art 任一方向；先回到 Milestone A 终点做产品链缺口审计。

---

# 12. WP-E-T0｜Audit PASS

必须输出：

- Git local / remote facts；
- Unity / MCP 能力；
- Scene host map；
- Current ExpeditionUI / ProgressionPanel / Battle return consumer map；
- exact PersonId path；
- current visual asset fields / presentation data audit；
- Person Visual Resolution 最小实现方案比较与推荐；
- Placeholder non-Battle 同步 resolve 调用链；
- 连续 N=1 / Placeholder 是否存在同步吞 Node 风险；
- planned file whitelist；
- planned serialized asset list；
- planned tests；
- no second Authority statement。

T0 不创建正式实现资产，不改规则。

前置：Authority Sync、Current 状态校正与本 FDS 的 E0 落仓已完成，且它们是单一前置，不是独立 Hard Gate 或后续 E0 / E0.5 流程。该落仓未 Repository Closed 前 Hard Stop，不开始实现；落仓 Repository Closed 后，下一节点直接是 WP-E-T0。

---

# 13. WP-E-T1｜Person Visual Resolution PASS

- 输入为 exact `PersonId`；
- Character / Hero 都可解析；
- 模板键只用于外观；
- missing binding 使用 default；
- 两个不同 Person 可显示两个独立 Member View，并能使用可区分 placeholder / visual；
- 两个同模板 Person 不合并；
- 无 SaveData / SaveVersion；
- 实现可被替换，不把 Catalog / Profile 形态提升为规则；
- Journey 之外消费者本轮不强制接入；
- 如采用共享 Resolver，其职责只读，不拥有 Person 或 Presentation Gameplay Authority。

---

# 14. WP-E-T2｜Journey Visual Hosts PASS

- Stable Journey Root 已接真实产品路径；
- Environment Host 可换 Sprite / visual；
- Party Host 显示全部准确成员；
- Member View 使用独立 Prefab / stable host；
- spacing / scale / distance / duration Inspector 可调；
- Prep Card / Battle UnitCard 未被直接复用；
- Current Debug Panel 不与 Journey 同屏；
- Placeholder 不走临时旁路；
- 正式素材未来可替换 View binding，而无需修改 Progression 规则。

---

# 15. WP-E-T3｜Coordinator PASS

- N=0 显示 terminal / return，不自动胜利；
- N=1 自动行进后进入同一 Node；
- N=1 Continue Skip 进入同一 Node；
- 自动完成与 Skip 同帧不会重复进入；
- N>1 自动/Skip 后只显示 Branch；
- Branch 不被 Continue 代选；
- Pause 停表现，不改规则；
- Return / Hide / Rebind / Battle / new cycle 取消旧 callback；
- 行走期间 Supply / elapsed days / CurrentNode 不变；
- Node resolve 后仍只结算一次；
- 一次 Presentation Cycle 最多消费一个 Progression Transition；
- 消费后必须重新读取 Current Progression State；
- 连续 Placeholder A → B → C 不得在同一旧 callback / 同一旧 Cycle 中连续进入或 resolve；
- 实现机制可逆，不把调度细节写入规则层。

---

# 16. WP-E-T4｜Product Integration PASS

必须从真实产品路径证明：

```text
NewGame / Continue
→ Town
→ Expedition Prep
→ Confirm
→ Journey Party visible
→ Travel / Continue Skip
→ Encounter / Battle
→ Result / Resolve
→ New Journey Cycle
→ Branch or next Encounter
→ Retreat / Return
→ Town
```

至少验证：

1. Prep 与 Journey 成员 `PersonId`集合精确一致；
2. Hero 若选择，独立显示且身份正确；
3. 两个具体 Person 可区分且不会合并；
4. missing visual fallback；
5. Journey Supply / elapsed HUD 读取 Current state；
6. 自动抵达与 Skip 不重复推进；
7. Pause 不改变 Current state；
8. Branch 必须人工选择；
9. Battle 中 Journey 不接受输入；
10. Battle 后 Journey 恢复并建立新 Cycle；
11. Return 取消旧 travel / cycle callback；
12. V4F Return 顺序与 exactly-once 无回归；
13. Legacy Map 不重新成为玩家主表面；
14. Debug Progression Panel 不与 Journey 同屏；
15. 连续 Placeholder A → B → C 必须分属多个 Presentation Cycle，不得一次 callback 吞完；
16. 连续 N=1 链在每次 Transition 后重新读取合法后继；
17. Enter denied 后不得沿旧 snapshot 自动改选或继续推进。

---

# 17. Verification

## 17.1 编译 / Console

- Asset Refresh；
- 等待编译；
- 0 new `error CS`；
- 读取 Error Console；
- 区分环境噪声与本任务问题；
- Scene / Prefab 无 Missing Reference。

## 17.2 EditMode 长期契约候选

至少覆盖：

- 0 / 1 / N projection；
- presentation token / cycle exactly-once；
- one cycle → at most one progression transition；
- auto / Skip convergence；
- Branch Skip no auto-select；
- Pause no rule mutation；
- old cycle invalidation；
- consecutive placeholder chain requires re-dispatch；
- exact PersonId visual resolution；
- appearance key not identity；
- same-template two-person not merged；
- missing visual fallback；
- Journey prefab / scene host structure；
- no debug panel on Current visible path。

建议新增一个明确的长期契约测试组，语义至少覆盖：

```text
A Placeholder (N=1)
→ B Placeholder (N=1)
→ C
```

断言：

- Cycle A 只能消费 A 对应的一次 Transition；
- A 同步 resolve 后 B 不会在同一旧 callback 中被 Enter；
- 新一轮调度后才允许处理 B；
- Return / Hide / Rebind 后旧 cycle completion no-op。

只保留真正保护长期契约的最小集合；一次性 Builder / inspection tests 在 Closeout 清理。

## 17.3 PlayMode

- 定向 Journey 产品集成；
- Battle round-trip；
- Branch；
- Return；
- 连续 Placeholder / N=1 链；
- Auto vs Continue Skip 等价；
- Pause / Resume 不改规则状态；
- 全量 PlayMode 回归；
- EditMode 与 PlayMode 串行执行。

不得用独立 Demo Scene / Mock Adventure 代替真实产品集成。

## 17.4 Screenshot Evidence

至少：

1. Journey Overview：Background + Party + HUD；
2. 两个以上具体 Person 使用可区分 placeholder / visual；
3. Branch Overlay 或 Encounter Overlay；
4. 可选：Pause / Continue 控件状态。

截图不提交到仓库，除非项目规则另有明确授权。

---

# 18. Visual Review 边界

Technical PASS 判断：

- Host 正确；
- 身份绑定正确；
- Visual Resolution 契约正确；
- 功能接线正确；
- Placeholder 可替换；
- 参数可调；
- 玩家不再面对纯 Debug Node Panel；
- Current 规则无回归；
- Presentation Cycle 不会吞多个 Progression Transition。

不判断：

- 好不好看；
- 最终队伍聚散；
- 最终人物比例；
- 正式背景；
- 正式走路动画；
- 正式字体 / 色彩 / HUD；
- 最终 Encounter / Branch 表现；
- Catalog / Sprite / Animator / Spine 哪个是最终长期美术管线。

制作人可在 Visual Preview 后：

- 直接接受地基并 Closeout；
- 要求只做窄 Visual Tuning；
- 不应让开发 Agent 无限自行迭代审美。

---

# 19. Hard Stop

出现以下任一情况必须停止相关实现并请求决策：

- Authority Sync V2 / FDS V2 / Current 状态校正未落仓；
- 必须修改 Progression Resolver 才能实现 Journey；
- 必须修改 Supply / elapsed / Branch / Return 规则；
- 必须修改 SaveData / SaveVersion；
- 必须实现无限生成；
- 必须设计正式 Event / Encounter Pool；
- 必须新建第二套 Party / Adventure Manager；
- 必须把 Traveling / Pause 写入规则 phase；
- 必须直接编辑 Scene / Prefab YAML；
- 必须修改 WP-D Battle Visual Foundation；
- 现有 Scene 有无法隔离的用户未提交视觉改动；
- 必须引入新 Package；
- Consumer Map 无法证明 Debug Panel 与 Journey 不会并行操作；
- 无法在不改变 Progression 规则的前提下满足“一次 Cycle 最多一个 Transition”；
- Person Visual Resolution 必须修改 Person Identity / Save 才能实现；
- 为实现 Journey 被迫提前冻结正式 Animator / Spine / 换装 / 美术生产系统。

普通 MonoBehaviour、Presenter、View、轻量 resolver / mapping / Catalog、placeholder、Inspector 参数、调度 token / queue / coroutine、测试修复属于 Scope 内 Green / Yellow，只要不改变产品 Authority。

---

# 20. 文件范围预期

最终精确 Manifest 由 T0 后生成。预期类别：

## 可能新增

- Journey presentation code；
- Journey coordinator / presentation cycle support；
- Person visual resolution adapter / mapping（**只有 T0 证明需要时**）；
- Journey member prefab；
- optional Journey root prefab；
- Journey placeholder assets；
- minimal long-term tests。

## 不再预设必须新增

- `PersonVisualCatalog` code；
- `PersonVisualProfile`；
- `PersonVisualCatalog.asset`。

T0 证明最小方案需要时可新增，但必须在任务报告说明为什么比复用现有数据更合适。

## 修改

- `ExpeditionUI.cs`（产品接线）；
- SampleScene 或现有正式宿主 Scene（Unity Editor / MCP）；
- 必要的 asmdef；
- 当前 Handoff / 状态 / 日志；
- Expedition implementation fact entry only where appropriate。

## 默认不修改

- Progression Runtime classes；
- RunState；
- Save；
- Battle rules；
- Village rules；
- Package / Project Settings；
- Person 长期 Identity Authority。

如果实际修改面扩大，按 Hard Stop 判断。

---

# 21. Final Closeout

Technical Verified 后，同一开发 Agent 执行：

1. 生成 / 更新新的 Handoff 版本；不得回写 v31 伪装成提交后快照；
2. 更新 `Docs/项目状态快照.md`；
3. 更新 `Docs/项目日志.md`；
4. 只在真实影响时更新 `Docs/问题清单.md`；
5. 更新 Expedition GameSpec 的 implementation fact 只能是事实摘要，不改本轮已冻结玩家规则；
6. 清理 disposable Builder / Runner / logs / test results；
7. Final Diff / Workspace Review；
8. 输出 Closure Commit Staging Manifest；
9. 未获授权时停在 Ready for Commit / Push；
10. 获得任务级明确授权后才 Commit + Push；
11. 核对 local HEAD == origin/main、remote SHA、staged empty、tracked clean、无本任务 untracked 残留；
12. 输出 WP-E Repository Closed Report；
13. 明确下一节点不是自动进入 Infinite Expedition，而是 **Milestone A Product Chain Gap Audit**。

建议 Commit Message：

```text
feat(expedition): establish journey visual foundation
```

---

# 22. Technical PASS 总表

只有全部成立才可标记 WP-E Technical Verified：

- Authority Sync 与本 FDS 的 E0 落仓已完成；其 Repository Closed 是进入 WP-E-T0 的一次性前置，不是独立执行 Gate；
- `Docs/项目状态快照.md` 已登记 Current 事实：WP-D Repository Closed / WP-E Pre-Execution；
- Journey Root 接入真实产品路径；
- Prep → Journey exact PersonId 集合连续；
- Hero 身份未被降级；
- Person visual resolution 以 PersonId 为入口；
- 没有把 Catalog / Profile 形态升级为长期规则；
- 至少两名人物可使用可区分 placeholder / visual；
- missing visual fallback；
- Party 显示全部实际成员；
- Journey Member 使用独立 Prefab / stable host；
- Environment / Party / HUD / Overlay 均有稳定 Host；
- N=0 / 1 / N 正确；
- 自动旅行与 Continue Skip 共用单一完成入口；
- Continue 不代选 Branch；
- Pause 只暂停 Presentation；
- 行走时间不改变 elapsed days / Supply / HP / VillageTime；
- 一次 Presentation Cycle 最多消费一个 Progression Transition；
- Transition 后重新读取 Current Progression State；
- 连续 Placeholder / N=1 链不会被同一旧 callback 连续吞掉；
- Return / Hide / Rebind / Battle / new cycle 使旧 callback 失效；
- Encounter 正确中断 Journey；
- Battle 后恢复 Journey；
- Return 取消未完成 callback 并走 Current V4F；
- Current Debug Panel 不与 Journey 同屏；
- Legacy Map 不恢复为玩家主表面；
- 不修改 Progression / Save / Battle / Village 规则；
- Compile / Console / EditMode / PlayMode / Product Integration 通过；
- Scene / Prefab 无 Missing Reference；
- Screenshot evidence 已生成；
- Disposable Cleanup 完成；
- 未冻结正式美术、动画、无限生成、正式内容与长期人物美术生产架构。

---

# 23. 本 Work Package 状态说明

**模块 / 主题：** WP-E Expedition Journey Visual Foundation V0.1。  
**原方案：** Current Progression 由运行时自建黑色 `ExpeditionProgressionPanel`直接呈现；原 FDS 同时建议固定 `PersonVisualCatalog / Resolver / Profile` 为最小共享视觉接缝。  
**本轮新增或修改：** 保留 Journey Presentation 目标；人物视觉改为冻结 `PersonId → exact Person → Presentation Visual` Resolution Contract，由 T0 决定最小实现；新增 Presentation Cycle 及“一次 Cycle 最多一个 Progression Transition”契约；增加连续 Placeholder / N=1 链专项测试；Closeout 后进入 Milestone A Product Chain Gap Audit。  
**已否决：** 自动旅行删除 Continue；Journey 重写 Progression；行走产生规则时间；Journey 建第二套 Party；直接复用 Battle UnitCard；WP-E 顺带实现无限远征；提前把 Catalog / Profile / Sprite Schema 定义为长期视觉 Authority；旧 Travel callback 同步连续吞多个 Node；WP-E 结束后机械进入 Infinite Expedition。  
**当前确认方案：** 本 FDS 即 WP-E 的正式开发期 Binding，Authority Sync + Current 状态校正 + 本 FDS 已按 E0 一次性落仓，Repository Closed 后直接从 WP-E-T0 本地 / Unity 审计开始，不再生成第三份大型执行文件；T0 后选择最小 Visual Resolution 实现；Journey Coordinator 以 Presentation Cycle 控制单 Transition 消费。  
**验证结果：** 本 V2 已基于原 FDS、原 Authority Sync、迁移包、当前远端 `36eff96` 与继任顾问审查结论重构；Unity 本地实现尚未开始。  
**尚未解决：** 本机 Git / Worktree / Unity 实时状态；Visual Resolution 最小实现形态；正式 Journey 美术、正式行走动画、无限生成、Biome、Encounter Pool、正式 Event、Speed-up 倍率、最终构图；WP-E 后 Milestone A 的真实剩余产品缺口。  
**下一步：** 完成 E0 落仓的 Repository Closed（Git 事实按本地 / 远端核对），随后由 Cline + Unity MCP 执行 WP-E-T0；本 FDS 已是正式开发期 Binding，不再生成第三份大型执行文件。
