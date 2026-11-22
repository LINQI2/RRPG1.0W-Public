# Battle Authoring Structure Consolidation V0.1

日期：2026-09-30。执行：Codex。规划 / 复核：开发14.0。
Target Branch：`main`；本轮禁止 commit / push。本报告是实施证据，不是 Gameplay Authority。

> 后续状态说明（2026-10-04）：本报告保留9月30日宿主和参数证据，不直接作为当前Editor操作路径。后续本地工程整理新增 `PrepCharacterHandRoot`、将原 `PrepCardRoot` 原位更名为 `BattleActionHandRoot`，并把UI宿主归入 `SceneUIHosts`；当前配置查[场景指南](../引擎场景层级与配置查找指南.md)，绑定职责查[技术架构总览 §12.2](../Architecture/技术架构总览.md)，发布状态查[项目状态快照](../项目状态快照.md)。历史代码 / 参数表不授权重跑旧Stage写入工具。

## 实施前工作区分类

- Protected User Work：`Assets/Resources/ScriptableObjects/Presentation/PersonVisualBindingCatalog.asset`、`Assets/Scenes/SampleScene.scene`、`Assets/Art/Battle/**`、`Assets/Art/Characters/**`、`Assets/Art/UI/**`（含目录 meta）。额外保护已有修改 `Assets/Prefabs/UI/BattleCard.prefab`、Assets 根下 `ChatGPT 图像 2026年9月30日 22_39_36-3.png` 及 meta。保护所有 Sprite / RectTransform / 色彩 / HUD / Background / Buttons / Unit 视觉。
- Previous Identity Repair：`Assets/Scripts/UI/` 下 BattleUI、BattleUIDisplayNames、BattleUIHandLayout、BattleUIUnitVisualHosts、ExpeditionUI、PersonVisualHostBinder；`Assets/Tests/EditMode/PersonVisual/PersonVisualBindingTests.cs`；`Assets/Tests/PlayMode/` 下 BattleFormationRosterConsistencyPlayTests、BattleMilestoneAPlayTests、BattleStage5IntentPlayTests、BattleVisualFoundationPlayTests、PersonVisualBindingPlayTests。实施前已经检查代码 / 测试 diff；只增量修改本轮必需部分。
- This Task Changes：实施前为空；下文完成后补充。
- Known Unrelated Dirty：`Development/Config/RRPG_Events.xlsx`；仅记录路径，不读取或修改内容。

上述 Assets 路径均相对 `UnityProject/RRPG1.0game/`。禁止 reset / checkout / stash / restore。

## A. Parameter Authority Map（实施前审计）

| Surface / 字段 | 分类 | 实际 Consumer / 职责 |
|---|---|---|
| BattleUI.unitCardScale / monsterCardScale | CURRENT | PlaceUnit / PlaceHero / Enemy Prep / Battle 单位根缩放；验证时制作人最新值为 (0.7,0.7,1) / (1.5,1.5,1)，本轮未改写 |
| BattleUI.handStartPosition / handCardSpacing | CURRENT → LEGACY 起点 + CURRENT Prep Step | BattleUIHandLayout；Scene 起点 (-530,-380)、步长 200；只影响 Prep，不影响 Battle Action Hand |
| BattleUI.dragPreviewOffset | CURRENT | 拖拽临时让位量；当前 30；不应决定正常布局 |
| BattleUIFormationPresentation.singleUnitVisualOffset / twoUnitVisualSpacing / threeUnitVisualSpacing / rowLocalVisualOffset | CURRENT | 唯一正常 Formation Resolver；1 / 2 / 3 人；不写 Gameplay Row / Column |
| BattleUI.unitCardPrefab / prepCardPrefab / uiFont / 六排引用 / handContainer / btnCancelOp / btnStartBattle / btnEndTurn / v1Runtime | INTERNAL | 当前宿主与组件绑定；制作人排版编辑对应 Scene / Prefab，避免日常改绑 |
| BattleUI.infoText / _deployedUnitsDebug | DEBUG | 日志 / 实时诊断；infoText.canvas 仍被旧路径用于 Canvas 定位，不能删除绑定 |
| BattleUI.itemCardRoot / btnAttackOp / btnSkillOp / btnGuardOp | LEGACY | 仅旧 UI 退场 Consumer，正式产品已不使用 |
| BattleUIUnitVisualHosts.playerBodyPlaceholder / prepBodyPlaceholder / shieldIconPlaceholder / intentAttackPlaceholder / intentDefendPlaceholder / intentSpecialPlaceholder | CURRENT | 视觉缺省与图标绑定；Person Catalog 配置保持制作人所有权 |
| BattleUIUnitVisualHosts.playerBodyScale / enemyBodyScale / unitBodyVisualOffset | CURRENT | Body 子宿主尺寸与偏移，与 Unit 根缩放不同；不当作第二套 Unit Scale |
| BattleUIEnemyIntent.enemyIntentOffset / enemyIntentFontSize / enemyIntentColor / enemyRangeOffset / enemyRangeFontSize | CURRENT | 动态 Intent / Range 文本表现参数；与 Prefab 文本绑定并存，非本轮 Combat Feedback 范围 |
| BattleUI.battleBackground / battleBackgroundPlaceholder | CURRENT | Background 宿主与缺省 Sprite；保持制作人配置 |
| BattleUI.rowVisualTintEnabled / playerRowVisualColor / enemyRowVisualColor | DEBUG | 原型 Row Tint，放入 Diagnostics；保留序列化兼容 |
| BattleV1Runtime.handBaseStep / handMinStep / handCenterOffset / handScaleByCount / handMinimumScale | CURRENT | RefreshHand 正式 Action Cards；Scene = 200 / 70 / (0,0) / false / 0.8 |
| BattleV1Runtime.handContainer / battleCardPrefab / txtSharedActionPoint | INTERNAL | 复用 PrepCardRoot，Prefab / HUD 引用 |
| BattleV1Runtime.maxActionPoints / baseDrawCount | INTERNAL | 规则配置 / Demo fallback；本轮不调整 |
| BattleV1Runtime.DrawCountProvider / RuleContextProvider / CardBadgeProvider / OnPlayerTurnStarted | INTERNAL | 注入委托，不是 Unity serialized field |
| BattleUIHandLayout | INTERNAL | 无自有 Inspector 字段；消耗 BattleUI 参数；Initial / Returned / Rearrange 应共用一个 Resolver |
| BattleUIBattlefieldLayout | INTERNAL | 无自有 Inspector 字段；Row 映射 / HitTest；固定三列偏移不应成为 Preview 的正常位置 Authority |
| BattlePrepCard | INTERNAL | 无 Inspector / serialized field；运行时卡身份、拖拽状态与 Owner 绑定 |
| BattleCardView.nameText / costText / kindText / ownerText / flagsText / artImage / artPlaceholder / descriptionText | CURRENT（Prefab 绑定） | 卡内文本 / 美术宿主；根 Scale 应由 BattleV1Runtime 提供 |
| DeckViewerUI.cardSpacing / cardStartPosition / heroCardStartPosition / cardScale / heroCardScale / cardsPerRow / rowSpacing | OTHER SURFACE | Town / Deck Viewer Only；不影响 Battle |
| DeckViewerUI.canvasRoot / charCardPrefab / itemCardPrefab / heroPanel / cardPanel / btnCharTab / btnItemTab / infoText / titleText / btnBack | OTHER SURFACE（绑定） | Town 卡册页面宿主 |
| SampleScene | INTERNAL（序列化宿主） | BattleController 挂 BattleUI + BattleV1Runtime；BattleCanvas / PrepCardRoot / 六排 / HUD 是现有正式宿主；不直接编辑 YAML |

## Existing Host Map / Hierarchy 策略

BattleController 上 BattleUI 负责 Prep / Battlefield，BattleV1Runtime 负责正式行动牌。Battle Hand 已使用 BattleCanvas/PrepCardRoot（1520×280，底部锚点）；Prep 卡当前直接挂 BattleCanvas，清理 / 隐藏 / 恢复也按 Canvas direct children 搜索。

最终采用现有 BattleCanvas 安全 Host：Initial、Returned Character、Returned Hero、Rearrange 都调用同一个 RearrangeHandCards，位置统一交给 BattleHandVisualLayout.GetPosition。Canvas RectTransform 提供可用宽度；本轮不新增 Root，不移动静态对象。保留所有按 Canvas direct children 的生命周期 Consumer，避免引入另一套 Host Authority。

静态 Hierarchy 只提出迁移图：BattleCanvas → Background、Battlefield（Player Rows / Enemy Rows）、Top HUD、Unit HUD / Overlay、Bottom HUD（AP / Draw / Battle Hand / Discard / End Turn）、Prep（Prep Hand / Start Battle）、Debug。未能取得 Editor World Corners / Game View 不实施静态迁移。

## B. Root Cause

1. Prep 初次 Character = start + (i+1)×spacing，Hero = start；Return = start + i×spacing。可见数量及 Hero 是否存在不参与居中计算。
2. RefreshHand 主动覆盖 Prefab 根 Scale，关闭 Scale By Count 时固定 1，没有明确 Base Scale。
3. UpdateDragPreview 与 Update 重复通过 GetColumnOffset(pending Column) 重写正常位置，恢复的是三人固定网格，而非当前人数 Resolver；返回手牌结束路径未明确完成源排重排 / 清除全部 Preview。

## C. Changed Files

This Task：

- `Assets/Scripts/UI/BattleUI.cs`：Prep 参数迁移、Inspector 分组 / Legacy 隐藏、Preview 单入口、拖拽结束恢复源排。与 Previous Identity Repair 共用文件；上一轮身份修复保留。
- `Assets/Scripts/UI/BattleUIHandLayout.cs`：统一可见 Prep Card 居中 / 缩放 Resolver。与 Previous Identity Repair 共用文件；视觉身份绑定保留。
- `Assets/Scripts/UI/BattlePrepCard.cs`：Prep 拖拽使用同一 Preview 入口，结束先恢复正常布局。
- `Assets/Scripts/Battle/V1/BattleV1Runtime.cs`：Base Scale 与相对 Minimum Ratio。
- `Assets/Scripts/UI/DeckViewerUI.cs`：Town 作用域字段命名 / Tooltip / 序列化迁移。
- 新增 `Assets/Editor/BattleAuthoringInspectors.cs` 及 Unity 生成 meta：Current 主面板、Advanced、Internal、Diagnostics 折叠。
- `Assets/Tests/EditMode/BattleFormationIntegrityTests.cs`：新增 16 个 Authoring 用例。
- `Assets/Tests/PlayMode/BattleFormationRosterConsistencyPlayTests.cs`：新增 2 个真实产品路径用例，保留上一轮修改。
- `Assets/Tests/PlayMode/BattleVisualFoundationPlayTests.cs`：已有 Formation 测试改为相对 rowLocalVisualOffset 断言，避免把制作人当前 y=80 错当作布局失败；保留上一轮身份修改。
- 本报告。

Previous Identity Repair、Protected User Work、Known Unrelated Dirty 的完整列表见首节。本轮没有对 Scene / Prefab / Catalog / Art / RRPG_Events.xlsx 发起写入，没有直接编辑 Scene YAML。制作人在并行编辑中保存的最新 Scene 值保留；Git 中 Scene 原有 dirty 不代表本轮改动。

## D. Final Authoring Surface

选中 `BattleController`：

| 要调什么 | 组件 / Inspector 参数 |
|---|---|
| Prep 卡大小 | BattleUI → Prep Character Hand → Prep Card Scale（相对 Prep Prefab 原缩放的乘数，默认 1） |
| Prep 间距 | BattleUI → Prep Hand Base Step / Prep Hand Min Step |
| 整组 Prep 位置 | BattleUI → Prep Hand Center Offset（相对 BattleCanvas 中心） |
| Battle 行动牌大小 | BattleV1Runtime → Battle Card Base Scale（默认 1） |
| Battle Hand 间距 | BattleV1Runtime → Hand Base Step / Hand Min Step |
| 整组 Battle Hand 位置 | BattleV1Runtime → Hand Center Offset（相对 handContainer 中心） |
| 随数量缩小 | BattleV1Runtime → Scale By Count / Minimum Scale (Ratio) |
| 单人偏移 / 两人间距 / 三人展开 | BattleUI → Formation Presentation → Single Unit Offset / Two Unit Spacing / Three Unit Spacing |
| 整排位置 / 临时让位 | BattleUI → Row Local Offset / Drag Preview Offset |
| Player / Monster 大小 | BattleUI → Unit Presentation → Player Unit Scale / Monster Unit Scale |
| Body 子图、Background、Intent 表现 | BattleUI → Advanced Presentation |
| Town 卡册大小 / 间距 | DeckViewerUI → Town / Deck Viewer Only；不影响 Battle |

`Minimum Scale` 始终表示相对 Base Scale 的比例。最终 scale = Base Scale × max(Minimum Ratio, 6/max(6,count))；关闭数量缩放时最终 scale = Base Scale。Base=1.2、count=8、Minimum=0.8 时最终为0.96。

旧字段使用 FormerlySerializedAs：handStartPosition → prepHandCenterOffset；handCardSpacing → prepHandBaseStep；DeckViewer cardScale → deckViewerCardScale、cardSpacing → deckViewerCardSpacing。旧数值保留，没有 Scene 补丁。**旧 Prep 起点数值现在表示整组中心**，这是本轮居中修复的明确语义变化；没有移动静态 HUD。验证时 Prep Center=(-530,-380)、Base Step=200。

## E. Hierarchy Result

实际读取 `BattleController/BattleCanvas`：BattleBackground、Row（六排）、PrepCardRoot（正式 Battle 行动牌）、Start / Cancel / EndTurn 按钮、InfoText、DiscardPileRoot、DrawPileRoot、TurnRoot、ActionPointRoot、Settings / Party 按钮等。

本轮实际整理的是组件 Inspector Authority 和动态布局调用关系。没有重命名、Reparent 或改写现有静态 Hierarchy；不声称完成树结构迁移。推荐后续迁移图：

```text
BattleCanvas
├─ Background              ← BattleBackground
├─ Battlefield             ← Row（保留六排引用）
├─ Top HUD                 ← TurnRoot / Settings / Party
├─ Unit HUD / Overlay      ← 动态单位子宿主
├─ Bottom HUD              ← ActionPointRoot / DrawPileRoot / PrepCardRoot / DiscardPileRoot / EndTurn
├─ Prep                    ← 动态 Prep Hand / Start Battle
└─ Debug                   ← InfoText / 旧 Cancel
```

迁移仍须另行完成 path / name 依赖审计、Editor API 重排、World Corners / Game View 对比与测试。本轮保护制作人当前静态视觉，未实施此迁移。

## F. Drag Fix

Update 只负责把拖拽位置交给 UpdateDragPreview。Preview 首先从当前 Row / 成员集合 / 相对顺序调用 Formation Resolver 恢复正常位置，只对可接收成员的目标排叠加临时 Delta。取消、无效满排及拖拽结束均恢复 Resolver，Preview 不再读取固定 Column 网格作为正常位置。

拖出战场返回 Prep 时，在同一结束操作内 Remove → Reassign remaining Columns → ApplyFormationPresentation；返回人物 / 英雄创建后的 Prep Hand 同步重排。跨排及满排拒绝继续使用现有逻辑 Column 规则，本轮不改变其 Authority。

## G. Tests

新增 EditMode 16 用例覆盖：1/2/3/6 张居中、隐藏 / 回归后重排一致、Hero + Characters 整体居中、Prep 缩放不累乘、Base=1 / 1.2、数量缩放相对 Minimum Ratio、中心不漂移、2/3 人拖拽离排 Preview 不覆盖人数布局、拒绝 / 取消恢复、四个序列化迁移属性。

新增 PlayMode 2 用例走正式产品路径，覆盖初始 Prep 中心、3→2→1 返回即时重排与返回 Prep 中心、开战前后位置一致，以及真实 Action Cards 的 Base Scale / 数量比例。现有完整 Formation 回归覆盖跨排源排与目标排、满排拒绝、逻辑列一致性。

序列化证据：FormerlySerializedAs 回归通过；Unity MCP 读取已有 Scene 时新 Prep 字段正确保留旧数值 (-530,-380) / 200，制作人单位缩放及 rowLocalVisualOffset=(0,80) 保留。没有通过手改 YAML 迁移。

## H. Full Validation（本轮实际执行）

- Compile：AssetDatabase ForceUpdate 后完成编译；项目 C# 编译错误 0；Editor IsCompiling=false / IsUpdating=false。
- 定向 EditMode：BattleStage1.Tests 238 passed，failed=0（含全部新增16用例）。
- 全量 EditMode：**1036 / 1036，failed=0，skipped=0，耗时 1:53.254**。
- 全量 PlayMode：**88 / 88，failed=0，skipped=0，耗时 46.603秒**。
- git diff --check：本轮代码 / 测试 / Inspector 无 whitespace error；存在仓库既有 LF→CRLF 提示。
- 实际 Game View：MCP截图 + Editor C#桥接调用真实拖拽处理入口，Prep 上阵3人→返回1人→剩余2人 y=135/25（中心80±55）→再返回1人→剩余1人 y=80；两次均 battleStarted=false，不需要 StartBattle 恢复。不是物理鼠标手工操作录像。
- 截图：`1PUBLIC_USER/.codex/visualizations/2026/09/30/01a0f301-40f0-7bc0-b6b4-93396d77ff2b/authoring-gui-three.png`、`authoring-gui-two.png`、`authoring-gui-one.png`。GUI fixture只使用内存 NewGame，不 Save；退出 Play Mode 后 SampleScene IsDirty=false，恢复18个静态Root。
- Console：全量故障注入测试会产生其预期 LogError；不把 Console 的预期故障日志混称编译错误。MCP路径查询失败与临时桥接脚本错误也不属于项目编译错误。

## I. Remaining Legacy / Risks

1. Prep 仍复用 Canvas direct children 和名称前缀；已收口布局，专用 Root 迁移延后以保护静态视觉。
2. infoText 仍兼任 Canvas Host 查询来源，归 Debug 但绑定不能删。Legacy 操作按钮字段隐藏保留兼容。
3. Prep Center保留旧起点的数值但采用中心语义；制作人如希望整组移到 Canvas 正中应调整 Center X，而不是寻找旧 StartPosition。Min Step 是可读性下限，极窄空间不保证完全无重叠。
4. 当前美术接入中的文本 / 图标 / HUD 重叠保持原配置，本轮没有排版这些视觉。GUI辅助 fixture通过脚本进入正式 Prep，不替代制作人最终手动拖拽复核。
5. 未生成验收后的 Final Handoff；此文件为实施 / 验证报告，等待开发14.0复核。未 commit、未 push；所有已有 Identity / Art 工作保持在当前 dirty 工作区。

状态：实现和本轮自动化 / Game View验证完成，停止；等待开发14.0复核。
