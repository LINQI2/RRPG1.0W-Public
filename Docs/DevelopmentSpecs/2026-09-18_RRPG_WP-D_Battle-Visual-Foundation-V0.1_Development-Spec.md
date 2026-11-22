# RRPG｜WP-D Battle Visual Foundation V0.1｜Development Spec

> 日期：2026-09-18  
> 状态：**Development-time Binding / Ready for Repository Landing**  
> Work Package：WP-D Battle Visual Foundation V0.1  
> 目标执行 Agent：Cline + Unity MCP / 团结引擎 Editor（MCP 优先；必要时可用 batchmode + Unity Editor API，禁止直接编辑 YAML）  
> 正式落仓路径：`Docs/DevelopmentSpecs/2026-09-18_RRPG_WP-D_Battle-Visual-Foundation-V0.1_Development-Spec.md`  
> 执行基线：`8c7b8337f59742f4a5b760dd63c99cea556c8aa2`（`refactor(battle): remove legacy item and player action product paths`）
>
> 本 Spec 的目标不是完成正式美术，也不是冻结最终 UI 布局；而是在现有 Battle 产品路径上建立 **可替换视觉宿主（Visual Host）+ 占位素材正式接入路径 + Formation Presentation**，把白模升级为“可看的原型”，并让未来正式素材能够主要通过导入、绑定、替换与 Inspector 调参完成，不要求重写玩法逻辑或重新接线。

---

# -1. Authority 与输入

本 WP 必须服从：

- `.clinerules.md`
- `Docs/AI开发执行规则.md`
- `Docs/AI多Agent开发协作协议.md`
- `Docs/文档地图.md`
- `GameSpec/Battle/Battle-GameSpec-V0.1.md`
- `GameSpec/Battle/Battle-Scene-UI-Binding-V0.1.md`
- `GameSpec/Battle/README.md`
- `GameDesign/03 战斗系统.md`
- `Docs/开发验收交接.md`
- `Docs/项目状态快照.md`

本轮制作人最新确认：

1. Battle 最终主体使用**完整小人身体的场景单位**，不是棋子/头像卡片。
2. 小人**站在场景中**，不是站在可见九宫格里。
3. Player / Enemy 仍为左右对峙；镜头采用成熟的左右卡牌 RPG / Slay-the-Spire 类空间语言，不把镜头本身作为创新点。
4. Front / Middle / Back 三排必须有视觉表达，并与开发逻辑统一；具体美术包装未冻结。
5. Left / Center / Right **只属于内部开发逻辑**，不得作为玩家层可见三格/三列标签。
6. 每排人数与内部 Column 正规化：
   - 1 人：Center；
   - 2 人：Left + Right；
   - 3 人：Left + Center + Right。
7. 玩家理解的是“**排 + 排内相对顺序**”。拖拽/插入可以决定第 3 人位于左 / 中 / 右任意相对位置；内部再自动正规化 Column。
8. 两人排视觉必须动态向排中心聚拢；逻辑仍为 Left + Right。具体聚拢距离属于 Inspector 可调参数。
9. 当前阶段目标是**简易视觉占位 + 与未来正式素材相同的导入/配置/绑定路径**。
10. 当前不设计长期美术生产架构、模块化人物生产系统、正式画风、正式动画体系。
11. Product Experience / Product Validation 继续后置；本 WP 只做 Visual Foundation 的技术接线与原型可视化。

---

# 0. Work Package 目标

本 WP 只解决以下问题：

## 0.1 从白模卡片到可替换场景单位

当前场上单位主要由 `UnitCard.prefab` 的矩形 Image + Text 表现。

目标：

> 保留现有正式 Battle 逻辑与点击/目标宿主，但为 Player / Enemy 单位提供真正的视觉宿主：
>
> `BattleUnit Logic → BattleUnit Visual Host → Placeholder Sprite → Future Formal Sprite/Animator`

占位资源必须走未来正式资源会走的同一视觉入口。

## 0.2 Formation Presentation 与内部 Column 解耦

当前 Visual Position 直接读取固定 Left / Center / Right Offset，导致：

> 2 人逻辑 Left + Right = 视觉被拉到两端。

目标：

> 保留内部 Column Authority，但增加唯一 Formation Presentation 层。

视觉结果：

```text
1 人：        ●

2 人：      ●   ●

3 人：    ●   ●   ●
```

具体距离全部可调，不在本 Spec 冻结数值。

## 0.3 建立可替换 Battle Visual Hosts

至少覆盖：

- Battle Background；
- Player / Enemy Row Visual；
- Unit Body；
- Name；
- HP；
- Shield；
- Status Anchor；
- Effect Anchor；
- Enemy Intent；
- Battle Card Art / Description / Owner Identity；
- Selection / Target basic highlight 的可替换入口（仅不改变 UX 的部分）。

## 0.4 简易 Placeholder 进入正式路径

允许创建少量简易 PNG / Sprite：

- 1 个 Battle Background placeholder；
- Player body placeholder；
- Enemy body placeholder；
- 必要的 HP / Shield / Intent 极简视觉；
- Card Art placeholder；
- Row visual placeholder（如现有 Row 色块已经足够，可复用而不额外造图）。

这些 Placeholder：

- 只用于验证正式视觉宿主；
- 不代表最终画风；
- 不得作为玩法规则；
- 后续替换时不改 Gameplay。

---

# 1. 本 WP 明确不做

禁止顺带进入：

- 正式角色美术生产；
- 正式 Enemy 美术；
- 正式卡面设计；
- 正式 VFX；
- 正式动画 / Animator 内容生产；
- 模块化人物换装架构；
- Battle / Village 共用美术 Catalog 的长期方案；
- Addressables / AssetBundle；
- Package / Project Settings；
- 新 Input System；
- Camera 系统重构；
- GameSpec 玩家规则变更；
- Battle Position / Occupancy / Range / Adjacency 重构；
- Shared Deck / Shared AP 重构；
- Enemy AP 重设计；
- Common Card 内容设计；
- Return / Expedition；
- Event / Shop / Recruit Legacy Cleanup；
- 正式 Product Acceptance；
- 正式 Target / Move UX 冻结；
- 正式 AllyCell 可视化设计；
- SaveVersion / SaveData / PlayerCollection 结构变化。

---

# 2. 设计与视觉硬边界

## 2.1 场景单位，不是单位卡片

`UnitCard.prefab` 可以继续作为现有点击 / 数据绑定 / RectTransform 宿主，但视觉必须能退化为：

> “透明/弱化的功能容器 + 完整小人 + 独立信息挂点”。

禁止把矩形 Unit Card 的当前白模结构当作最终视觉产品。

## 2.2 Row 可见，Column 不可见

Front / Middle / Back：

> 是玩家可感知的阵型结构，必须有视觉区域。

Left / Center / Right：

> 仅内部逻辑，不得直接显示：
> - `Left / Center / Right` 标签；
> - 3 个永久可见格子；
> - 九宫格常驻棋盘线。

## 2.3 排内相对顺序

视觉层必须服从：

- 1 人居中；
- 2 人聚拢居中；
- 3 人展开；
- 顺序保持稳定；
- 玩家拖拽改变顺序时，画面与逻辑同步；
- 战中 Move / Swap 后同样使用同一 Formation Presentation。

不得让：

> 战前布阵用一套排布，战中 Reposition 再用另一套排布。

## 2.4 参数不冻结

以下只要求 Inspector 可调：

- 1 / 2 / 3 人间距；
- Character / Enemy Scale；
- Body Offset；
- HP / Shield / Intent Anchor；
- Row Rect；
- Background Rect；
- Hand position；
- Card size / spacing；
- 颜色 / 占位图。

本 WP 不以某个数值“看起来漂亮”作为 Technical PASS。

## 2.5 Placeholder 不得走临时旁路

禁止：

- 在 Gameplay 代码里按测试路径 `Resources.Load("test.png")`；
- 为 Placeholder 建立第二套 Battle UI；
- Debug Canvas 代替 Current 产品宿主；
- Placeholder 只在测试 Scene 生效。

必须：

> 从真实产品 Battle 入口，使用 Current Scene / Prefab / Visual Host 显示。

---

# 3. 当前已知 Existing Host Map（执行前必须本地复核）

| 新职责 | 当前宿主 / 事实 | 默认处理 |
|---|---|---|
| Player / Enemy Row | `BattleUI` 的 player/enemy Front/Middle/Back `RectTransform` | 复用 |
| Unit runtime display | `UnitCard.prefab` + `BattleUI.RefreshFieldUI` | 原位扩展为 Visual Host |
| Unit click / target | `UnitCard.prefab` Button + BattleUI click routing | 保留 |
| Position | `BattleUnit.Row/Column` + WP-A dual board | 不动 |
| Formation visual | `GetColumnOffset` / `ReassignColumns` / `RepositionUnitCard` | 收敛成单一 Presentation 入口 |
| Enemy Intent | `BattleUIEnemyIntent` 运行时动态创建 Text | 迁移到可替换 Intent Host |
| HP / Shield | `Text_HP` 字符串，其中 Shield 拼接为 `盾X` | 拆为独立绑定宿主 |
| Unit name | `Text_Name` | 可复用 / 迁移到 Anchor_Name |
| Player AP text | WP-C 已从 Current 退出；`UnitCard.prefab` 仍可能残留 `Text_AP` 序列化对象 | 在本 WP 精确清理 Battle 视觉残留 |
| Unit ATK debug | `Text_ATK` | 是否保留只按 Current 可视需求；不得作为正式布局硬需求 |
| Shared Hand | `BattleV1Runtime.handContainer` | 复用 |
| Battle Card | `BattleCard.prefab` + `BattleCardView` | 扩展视觉槽，不重建牌系统 |
| Shared AP | `BattleV1Runtime` / `BattleUI.infoText` 现有绑定 | 原位复用；可以补独立 Visual Host，但不得第二套 Authority |
| Battle Background | 无明确正式 Visual Host | 若本地审计确认缺失，最小新建 |
| Row Visual | 现有 Row Rect / 白模色块 | 原位扩展 |
| Target Cell Marker | `BattleUIRulePipeline` 动态 `RuleCellMarker_Row_Column` | **不正式化**；见 §8 |
| Scene Legacy Battle UI | WP-C 后 YAML 仍可能残留旧 Attack / Skill / Guard / ItemCardRoot 等对象 | 只在精确核对后物理清理 Battle 范围对象 |

若本地事实与表格不同：

> 以本地 Editor / Scene / Prefab 事实为准，更新 Existing Host Map 后继续 Green/Yellow；只有会改变玩家规则 / Scope / Authority 才 Red。

---

# 4. Visual Binding Architecture

目标数据流：

```text
Gameplay / Runtime Fact
        ↓
Presentation Adapter / View Binding
        ↓
Visual Host / Anchor
        ↓
Placeholder Asset
        ↓
Future Formal Asset
```

替换正式素材时，预期动作应主要是：

```text
Import
→ Assign
→ Adjust Inspector Visual Params
```

而不是：

```text
Rewrite Battle Logic
→ Rebuild UI
→ Reconnect Product Path
```

## 4.1 Unit Visual Host 最低结构

实际命名可复用现有对象，不要求机械新建同名对象。

功能身份至少包括：

```text
Unit View Root
├─ Anchor_Visual
│  └─ Body Visual
├─ Anchor_Name
├─ Anchor_HP
├─ Anchor_Shield
├─ Anchor_Status
├─ Anchor_Effect
└─ Anchor_Intent   // Enemy 可用；Player 可为空
```

要求：

- 每个 Anchor 可在 Prefab / Inspector 中移动；
- Body Sprite / visual reference 可替换；
- Body scale 可调；
- UI information 不得写死到 Gameplay 代码坐标；
- 空 Anchor 合法；
- 当前可继续使用 Legacy `Text`，不强制 TMP / UI framework 迁移。

## 4.2 Visual Asset Resolution

执行前必须审计当前 Character / Hero / Monster 内容数据是否已有可复用：

- Portrait；
- Icon；
- Sprite；
- Visual Prefab；
- 或其他稳定视觉字段。

优先级：

1. 复用已有正式视觉字段 / Visual Definition；
2. 如无 per-unit 视觉字段，先允许使用 **default Player / default Enemy placeholder** 完成 Host 验证；
3. 只有正式占位测试确实需要 per-unit mapping 且当前项目无可复用宿主时，才允许新增**最小 Presentation-only Visual Mapping**，并在报告中标 Yellow。

禁止：

- 修改 Save Schema；
- 把 Sprite 放入 BattleUnit 作为新 Gameplay Authority；
- 用 PersonId / CardId 分支写死视觉；
- 为每张牌/每个人写 C# if/else 选素材。

## 4.3 Battle Background

若本地确无宿主：

> 在 Current Battle Scene / Canvas 内新增一个明确 Battle Background Visual Host。

要求：

- 不遮挡 UI / Raycast；
- Sprite 可替换；
- Stretch / PreserveAspect / Rect 参数可调；
- 不与 Gameplay Row / Board Authority 耦合。

## 4.4 Row Visual

现有 6 个 Row Rect 为 Authority 映射宿主。

只允许：

> 给它们增加视觉子对象 / Background / Ground / Highlight。

禁止：

> 重建第二套 6 Row 逻辑。

---

# 5. Formation Presentation

## 5.1 单一入口

把当前所有会改变单位显示位置的产品路径收敛到同一 Presentation 计算：

- 战前 `PlaceUnit / ReassignColumns`；
- 开战后首次渲染；
- Gate 0 Move；
- Gate 0 Swap；
- 0HP / 移除后剩余单位重新排布；
- 其它 Current Position mutation。

必须保持：

> `BattleUnit.Row/Column` 仍是 Gameplay Position Authority。

Visual Presenter：

> 只计算画面位置，不成为第二套逻辑 Position Authority。

## 5.2 视觉规则

按**同一 Row 内当前仍具战斗表现资格的单位**顺序：

- Count 1 → visual center；
- Count 2 → visual pair around center；
- Count 3 → visual triple spread。

排序必须与内部逻辑相对顺序一致。

2 人：

> 虽内部是 Left + Right，但视觉不直接使用最外侧 Column Offset。

3 人：

> 展开后仍保持 Left / Center / Right 顺序。

## 5.3 Inspector 参数

至少提供：

- `singleUnitOffset`
- `twoUnitSpacing` 或等价参数；
- `threeUnitSpacing` 或等价参数；
- Player / Enemy body scale；
- 必要的 row-local offset。

命名可按现有风格调整。

## 5.4 插入 / 排序

不得改变已确认玩法：

> 玩家可通过拖拽相对位置决定排内插入顺序。

本 WP 只保证：

- 现有排序规则不被破坏；
- 视觉排布正确跟随最终逻辑顺序。

本 WP 不重新设计拖拽手势。

---

# 6. Unit Information Hosts

## 6.1 HP

当前 HP 数据源继续使用 Current BattleUnit HP。

表现：

- 可暂时 Text；
- 可以加入简单 Bar；
- 必须独立 Host；
- HP变化即时刷新。

## 6.2 Shield

当前 Shield：

> 不再只允许拼在 HP 文本字符串中。

必须存在独立：

> Shield Visual Host / Text / Icon placeholder。

规则不变：

- Shield 数据仍由 BattleUnit / BattleSystem Current Authority；
- Visual 仅绑定。

## 6.3 Enemy Intent

迁移原则：

> Intent Visual 不再依赖运行时临时 `new Text` 作为唯一正式表现宿主。

目标：

- Enemy View 有稳定 `Anchor_Intent`；
- Intent 内容绑定到该 Host；
- 占位可使用文字/简单 Icon；
- Offset / Scale / Font / Icon 可调；
- 仍只显示 Current 允许的粗粒度 Intent；
- 不泄露隐藏目标 / AI 逻辑。

## 6.4 Status / Effect

本 WP 不制作正式 Status/VFX。

只要求：

- Anchor 存在；
- 可为空；
- 不影响 Gameplay。

---

# 7. Battle Card Visual Host

现有 `BattleCard.prefab` 继续作为正式 Current hand card host。

最低视觉槽：

- Frame / root background；
- Name；
- Cost；
- Description；
- Owner / Common identity；
- Art；
- State / Badge。

要求：

- 当前 `BattleCardView` 绑定 Actual Cost / Owner / current modifiers 的行为保持；
- Description 应绑定现有静态说明或当前可用 description source；
- Art 无内容时使用 placeholder；
- 不设计最终角色牌 / 通用牌画风；
- 不新增第二套 Hand prefab / runtime。

手牌位置：

> 当前参数继续 Inspector 可调；不得因本 WP 重新冻结最终 Hand layout。

---

# 8. Target / Position UI：本 WP 只保护，不正式化

当前 `BattleUIRulePipeline` 的 `AllyCell` marker：

- 运行时按 Row + Column 生成；
- 使用“可选位置”矩形；
- 会把内部 Column 直接投影给玩家。

这与制作人最新“Column 仅内部逻辑、玩家操作排内相对位置”的最终方向存在 UX 差异。

因此本 WP：

### 必须
- 保持现有功能可用；
- 不破坏《补位》《换岗》；
- 保持 resolver / legality / AP / execution 正确。

### 禁止
- 把当前 Cell Marker 美化成正式 UI；
- 把 Left / Center / Right 永久做成可见 slot；
- 冻结最终 Position Target UX。

如果 Visual Host 变化导致 Debug Marker 错位：

> 只做最小兼容适配，并在 Handoff 登记“Position Target UX Future Design”。

---

# 9. Placeholder Asset Pack

允许最低创建：

```text
Assets/Art/Prototype/Battle/
```

实际路径可按仓库现有 Art 目录规范调整。

建议最少：

- `battle_bg_placeholder.png`
- `player_body_placeholder.png`
- `enemy_body_placeholder.png`
- `card_art_placeholder.png`
- `intent_attack_placeholder.png`
- `intent_defend_placeholder.png`
- `intent_special_placeholder.png`
- `shield_placeholder.png`

要求：

- PNG / Sprite 使用正常 Unity Import；
- 必要 `.meta` 正常生成；
- 不使用外部版权不明资源；
- 不假装正式画风；
- 文件名与用途清楚；
- Placeholder 必须从 Current Visual Host 被真实产品路径使用。

如果项目已有足够可复用无版权风险占位资源：

> 优先复用，不机械新建。

---

# 10. Scene / Prefab Serialized Cleanup

WP-C 已确认：

> 正式产品代码不再使用 Player Attack / Skill / Guard / Item Hand / TP。

但 Scene / Prefab YAML 可能仍存在序列化旧对象。

由于本 WP 本身第一次正式编辑 Battle Scene / Prefab，本轮允许**精确物理清理 Battle 范围内已被 WP-C 证明 Dead 且无 Current 消费者的序列化对象**，例如：

- Battle Attack button；
- Battle Skill button；
- Battle Guard button；
- Battle Item root；
- `UnitCard.prefab` 中仅用于 Player Personal AP 的 obsolete visual child；
- 其它 WP-C Final Report 已明确退出且本地 consumer map 复核为 0 的 Battle-only UI。

硬约束：

- 先读本地 Scene / Prefab 实际状态；
- 只删已证明 Dead 的对象；
- 不顺手清 Expedition BtnItemTab / Shop / Recruit / Event；
- 不删 Current Cancel；
- 不删 Enemy AP 展示/逻辑所需对象；
- 不直接编辑 YAML；
- MCP 优先；
- 若 MCP 不可用，可使用 Unity Editor batchmode + 专用 Editor API 修改 Scene / Prefab；临时 Editor Builder / Runner 默认 Disposable；
- 任何歧义进入 Yellow/Red，不猜。

---

# 11. Technical Checkpoints

```text
WP-D-T0  Visual Host / Scene / Prefab Audit
→ T1 Unit Visual Host + Formation Presentation
→ T2 Background / Row / HP / Shield / Intent Hosts
→ T3 Battle Card Visual Host
→ T4 Placeholder Asset Integration
→ T5 Product Visual Verification + Regression + Closeout
```

Green / Yellow 自主继续；Red 才等待用户。

---

# 12. WP-D-T0｜Audit

必须输出简短 Evidence：

1. local branch / HEAD / origin/main / worktree；
2. 当前 Unity Editor / MCP 能力；
3. Current Battle Scene / Canvas / BattleUI Host；
4. UnitCard prefab 实际结构；
5. BattleCard prefab 实际结构；
6. 6 Row Rect；
7. Background host 是否存在；
8. Enemy Intent 当前宿主；
9. HP / Shield 当前宿主；
10. WP-C 遗留 Battle Scene objects；
11. 现有 Character / Monster 是否已有 Visual Asset 字段；
12. 本任务最终准备复用 / 扩展 / 新建的 Visual Hosts。

无需为 Green Existing Host Map 请求人工批准。

---

# 13. WP-D-T1｜Unit Host + Formation

Technical PASS：

- Player/Enemy Current units 显示 Placeholder Body；
- 不再依赖大矩形卡片作为唯一可识别单位视觉；
- 1人视觉居中；
- 2人视觉聚拢；
- 3人展开；
- 顺序与 Column Authority 一致；
- 战前、开战、Move、Swap后使用同一 Presentation；
- 点击/目标选择不破坏；
- Logic Row/Column unchanged；
- 所有 spacing/scale 参数可调。

---

# 14. WP-D-T2｜Environment + Unit Info

Technical PASS：

- Battle Background placeholder 可从正式 Host 替换；
- 六 Row visual 仍对应现有 Row Rect；
- HP有独立Host并正确刷新；
- Shield有独立Host并正确刷新；
- Enemy Intent使用稳定Host并正确刷新；
- Status/Effect anchors可为空；
- Enemy Intent 不泄露额外AI信息；
- 无第二套 Battle HUD Authority。

---

# 15. WP-D-T3｜Card Host

Technical PASS：

- Current Shared Hand 仍使用同一 BattleCard prefab；
- Name / Cost / Description / Owner/Common / Art / Badge 有稳定视觉槽；
- Actual Cost / Modifier 显示不回归；
- Card click / target / AP payment 不回归；
- Art placeholder 可以换 Sprite 而无需改 Gameplay。

---

# 16. WP-D-T4｜Placeholder Integration

必须从真实产品 Battle 路径看到：

- Background；
- Player body；
- Enemy body；
- Row visual；
- HP；
- Shield（通过可靠测试/场景触发）；
- Enemy Intent；
- Card visual。

允许：
- 极简色块 / 轮廓 / silhouette；
- 简单文本；
- 简单图标。

不要求：
- 漂亮；
- 最终风格一致；
- 正式动画。

---

# 17. Verification

## 17.1 Module Verification

至少：

- Compile 0 new errors；
- Console baseline vs after；
- Formation unit tests；
- Visual binding / resolver tests（若新增纯逻辑 presenter/resolver）；
- Prefab / Scene Missing Reference check；
- serialized bindings 回读；
- Current legacy dead objects 精确清理检查。

## 17.2 Product Integration Verification

必须从真实入口：

> NewGame / Continue → Town → Expedition → Battle

或当前真实最短产品链进入 Battle。

至少证明：

1. Player / Enemy unit placeholder visible；
2. 1 / 2 / 3 人排布规则；
3. Shared Hand / AP 正常；
4. Enemy Intent 正常；
5. HP / Shield正常；
6. End Turn正常；
7. Card click / target正常；
8. Move / Swap后Visual重排正确；
9. Battle结束/Return未破坏；
10. 无旧 Attack/Skill/Guard/Item UI 回来。

独立 Visual Demo / Test Scene 不替代产品集成。

## 17.3 Screenshot Evidence

本 WP 属视觉基础任务。

最终至少保留/报告：

- 1 张战斗 Game View 截图：展示背景、双阵营、完整小人placeholder、Row、Hand/AP；
- 1 张 2 人排视觉聚拢截图；
- 1 张 Enemy Intent / HP / Shield 可读截图（可合并）。

截图用于用户快速判断“视觉宿主确实接上”，不等于正式视觉 Acceptance。

---

# 18. Visual Review 边界

本 WP 的 Technical PASS 只判断：

- 资源可替换；
- 绑定正确；
- 画面不再是纯 Debug 白模；
- 数据实时正确；
- 参数可调；
- Current 产品链无回归。

以下不是 Technical PASS 条件：

- “好不好看”；
- 最终比例；
- 最终颜色；
- 最终构图；
- 正式画风；
- 正式动画节奏。

完成后状态建议：

> **WP-D Battle Visual Foundation Technical Verified / Visual Preview Ready**

如果用户只调整 Inspector 视觉参数 / 替换 placeholder，不改变 Gameplay：

> 属后续 Visual Tuning，不自动重开 WP-D Gameplay Verification。

---

# 19. Hard Stop

以下属于 Red / Awaiting Decision：

- 必须修改 Battle 玩家规则；
- 必须改变 Row / Column Authority；
- 必须修改 WP-A dual board；
- 必须改变 Shared Deck / AP；
- 必须改变 WP-C cleanup 结论；
- 必须设计最终 AllyCell / Position UX 才能继续；
- 必须修改 Save Schema / Version；
- 必须新建第二套正式 Battle Scene / Canvas / HUD；
- 必须引入新 Package；
- 必须建立长期美术生产系统才能完成；
- 必须直接编辑 Scene/Prefab YAML；
- 当前 Scene / Prefab 存在用户未提交视觉改动且无法安全隔离。

普通：
- Visual Host component；
- Presenter helper；
- placeholder Sprite；
- anchor；
- Inspector parameter；
- Editor builder；
- 测试修正；

在 Scope 内属于 Green / Yellow。

---

# 20. 文件与序列化资产规则

涉及：

- `.scene`
- `.prefab`
- `.asset`
- `.meta`

必须遵守 `.clinerules.md` 与 `Docs/AI开发执行规则.md`：

> Unity Editor / MCP 修改，禁止直接 YAML 文本编辑。

若本会话无 MCP：

1. 不得静默编辑 YAML；
2. 优先判断 Unity Editor batchmode + Editor API 是否能安全完成；
3. 若不能可靠完成，发出 `Manual Environment Intervention`；
4. 不得把“代码 Host 已写完”冒充 Visual Foundation 已完成。

---

# 21. Final Closeout

完成后同一 Agent：

- Handoff；
- 项目状态快照；
- 项目日志；
- 问题清单（只更新本任务真正影响的条目）；
- `GameSpec/Battle/README.md` 仅在确有实施事实需要同步时更新；
- Disposable Cleanup；
- Final Diff / Workspace Review；
- Closure Commit Staging Manifest。

本 Spec **不授权 commit / push**。

默认停止于：

> **WP-D Battle Visual Foundation Technical Verified / Visual Preview Ready / Ready for Commit / Push**

---

# 22. Technical PASS 总表

只有全部成立才可标 Technical Verified：

- Unit Body 有可替换 Visual Host；
- Placeholder 走真实正式视觉路径；
- Battle Background 有可替换 Host；
- Row Visual复用现有 Row；
- 1人居中 / 2人聚拢 / 3人展开；
- 战前 / Move / Swap 共用 Formation Presentation；
- Logic Column 未暴露为常驻玩家格子；
- HP独立Host；
- Shield独立Host；
- Enemy Intent稳定Host；
- Card Art / Description 等视觉槽补齐；
- Shared Deck / AP / Card rule / Intent / Damage / Move / Swap 无回归；
- Current Cancel保留；
- WP-C 已退出的 Battle Legacy UI 不再在最终 Current Battle 视觉树中占位；
- Scene / Prefab 无 Missing Reference；
- Visual参数 Inspector 可调；
- Compile / Console / tests / Product Integration 通过；
- 至少提供一组 Game View Screenshot evidence；
- 无第二套 Battle UI / Position / Visual Authority；
- 未冻结正式画风 / 最终布局 / Target UX。
