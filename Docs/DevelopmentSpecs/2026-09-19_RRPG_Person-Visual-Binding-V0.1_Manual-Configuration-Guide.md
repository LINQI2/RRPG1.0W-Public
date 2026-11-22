# RRPG｜Person Visual Binding V0.1｜Manual Configuration Guide（人物视觉配置手册）

> 状态：**Implementation-only / Not Frozen**（可替换的临时实现，不是最终美术架构）
> 适用范围：Journey（远征行走）/ Battle（战斗）/ Prep（远征准备 + 战前准备）/ Hero Selection（英雄选择）
> 最后更新：2026-09-19

---

## 1. 这套东西是什么

**一句话**：以后你新增一个人物的外观，只需要「导入 1 张 PNG + 在**一个** Inspector 资产里新增 1 行」。

**它做什么**：

- 把「人物身份 → 用哪份外观素材」这件事集中到一处配置；
- Journey / Battle / Prep / Hero Selection 四个表面**读取同一份配置**；
- 你不需要把同一张表复制到多个 Scene 或 Prefab。

**它不是什么（重要）**：

- **不是人物身份来源**：人物是谁仍然由 `PersonId` 决定；这份配置只回答「用哪张图」。
- **不是最终美术方案**：它不叫 PersonVisualCatalog / 换装系统 / Spine / Animator，也不含 Atlas / Addressables。
  以后可以整体换成更正式的美术数据架构，而不需要改四个表面的代码。
- 它不决定任何玩法：不改选人规则、不改上阵人数、不改站位、不改战斗规则、不进存档。

---

## 2. 配置对象在哪里

| 项目 | 内容 |
|---|---|
| 资产路径 | `UnityProject/RRPG1.0game/Assets/Resources/ScriptableObjects/Presentation/PersonVisualBindingCatalog.asset` |
| 资产类型 | `PersonVisualBindingCatalog`（轻量 ScriptableObject） |
| 怎么打开 | 在 Unity/Tuanjie 的 Project 窗口里双击该 `.asset`，右侧 Inspector 出现 `Entries` 列表 |
| 读取方式 | Runtime 通过固定 Resources 路径读取；**不需要**在 Scene / Prefab 里再挂引用 |
| 资产缺失时 | 四个表面全部走各自的 placeholder（不报错、不阻断任何流程） |

**唯一配置面**：如果你在别处（Scene 组件、Prefab、其它 SO）又看到一份「人物 → Sprite」的表，那是遗留物或 Bug，请报告。
（本次迁移已清除 WP-E 时期挂在 `ExpeditionJourneyRoot` 上的旧人物质表。）

---

## 3. 字段含义

`Entries` 每一条 = 一个 **AppearanceKey** 的外观配置。

| 字段 | 必填 | 含义 |
|---|---|---|
| `AppearanceKey` | ✅ | 外观配置键。**Character** 用 `PersonDefinitionId`（例如 `person_lin_en`）；若该人物没有 PersonDefinition 则用 `CharacterId`。**Hero** 用 `HeroId`（例如 `hero_001`）。它不是人物身份，只是「用哪份外观」的键 |
| `DefaultBodySprite` | ✅ | 默认身体图。各表面在没有单独 override 时都用它。**只做一张图时，只填这一项就够了** |
| `JourneySpriteOverride` | ⬜ | Journey（远征行走）专用图，留空 = 用 `DefaultBodySprite` |
| `BattleSpriteOverride` | ⬜ | Battle 单位卡专用图，留空 = 用 `DefaultBodySprite` |
| `PrepSpriteOverride` | ⬜ | 远征准备 / 战前准备人物卡专用图，留空 = 用 `DefaultBodySprite` |
| `HeroSelectSpriteOverride` | ⬜ | 英雄选择肖像专用图，留空 = 用 `DefaultBodySprite` |
| `CardPortraitSpriteOverride` | ⬜ | Battle Character Card 的 Owner 头像；2026-10-01 V0.1 新增。留空 = `DefaultBodySprite`，按 exact OwnerPersonId 解析具体 Person，不按 CharacterId 合并。配置入口与本轮状态见 [TMP / Battle Surface 报告](2026-10-01_RRPG_Current-UI-TMP-Battle-Surface-V0.1_Report.md) |
| `JourneyOffset` / `JourneyScale` | ⬜ | Journey 表面的局部偏移（像素）/ 缩放（默认 0 / 1） |
| `BattleOffset` / `BattleScale` | ⬜ | Battle 表面的局部偏移 / 缩放 |
| `PrepOffset` / `PrepScale` | ⬜ | Prep 表面的局部偏移 / 缩放 |
| `HeroSelectOffset` / `HeroSelectScale` | ⬜ | Hero Selection 表面的局部偏移 / 缩放 |

注意：
- `Scale` 填 `0` 或负数会被当成 `1`（不允许把 0 缩放写进画面）。
- 同一个 `AppearanceKey` 出现两次时，只保留第一条并在 Console 给出告警。

---

## 4. Default 与 Override 的取舍规则（fallback）

解析顺序固定，四个表面完全一致：

```
该表面的 Sprite override        ← 有就优先用它
   ↓ 空
DefaultBodySprite               ← 没有 override 就用它
   ↓ 空
该消费者自己的 placeholder      ← 连默认图也没有时用它
   ↓ 也没有
保留卡片 / 成员 Prefab 自带占位图（不隐藏身体、不影响布局与点击）
```

对应地：

- 你**不需要**为了第一版做 4 张不同的人物图；
- 只填 `DefaultBodySprite`，各表面就都会显示同一张图；
- 只想让某个表面特别一点，就只填那个表面的 override。

---

## 5. 表面宿主在哪里（Scale / Offset 调什么）

| 表面 | 实际显示位置（Host） | 谁把图写进去 |
|---|---|---|
| Journey | Scene `ExpeditionJourneyRoot/PartyHost` → `JourneyPartyMemberView.prefab` 的 `Body` | `ExpeditionJourneyView.ApplyParty` |
| Battle | `UnitCard.prefab` → `Anchor_Visual/Body_Visual` | `BattleUI`（`RefreshFieldUI` / 战前布阵 `FillUnitCard`） |
| Prep | `CharCard.prefab` → `Anchor_Visual/Body_Visual` | `ExpeditionUI`（远征准备）与 `BattleUI`（战前准备手牌） |
| Hero Selection | `HeroSelectCard.prefab` → `Anchor_Visual/Body_Visual` | `HeroSelectionUI` |

**Scale / Offset 建议怎么调**：

1. 先只调配置里的 `XxxScale` / `XxxOffset`——它只影响该 AppearanceKey 在该表面的显示；
2. 如果要整体调整所有人在某个表面的位置/大小，去调该表面的宿主 Prefab / Scene 物体：
   - `Anchor_Visual` 的 Anchor / Pivot / Position / Size（这是「挂点」，以后所有人物图共用这个位置）；
   - Battle 另有两个全局参数：`BattleUI.playerBodyScale`、`BattleUI.unitBodyVisualOffset`（默认缩放 / 默认偏移，与配置里的值**相乘 / 相加**）。
3. 图本身的长宽比不用管：四个表面都固定使用 `Image` 的 **Simple + Preserve Aspect**，图不会被拉伸变形。

**没有配置命中时不会动布局**：Prep 与 Hero Selection 的宿主在「没命中配置」时完全保留你在 Prefab 里摆好的位置与自带占位图，不会被运行时归零。

---

## 6. 新增一个 Character 外观（完整步骤）

**目标：1 个人物 = 1 张图 + 1 行配置**（不是三份绑定）。

1. **导入图片**：把 PNG 拖进 `Assets/Art/` 下任意合适位置。
   - 在图片的 Inspector 里确认 `Texture Type = Sprite (2D and UI)`；
   - 其余保持默认即可。
2. **找到该人物的 AppearanceKey**：
   - 打开 `Assets/Resources/ScriptableObjects/Persons/PersonDefinitionCatalog.asset`；
   - 找到该人物那一行的 `PersonDefinitionId`（例如 `person_lin_en`）。**这就是要填的 AppearanceKey**。
   - 如果这个人物没有 PersonDefinition，则用它的 `CharacterId`。
3. **新增配置行**：打开 `PersonVisualBindingCatalog.asset` → 在 `Entries` 上点 `+` → 新增一行：
   - `Appearance Key` = 第 2 步的键；
   - `Default Body Sprite` = 拖入第 1 步的图；
   - 可选：填某一个表面的 Override / Scale / Offset。
4. **Play 验证**（不需要改任何代码、不需要改 Scene / Prefab）：
   - **Journey**：新手/继续 → 领地 → 远征 → 选择该人物出征 → 确认 → 行走画面该成员显示你的图；
   - **Battle**：进入战斗后该人物单位卡的身体显示你的图；
   - **Prep**：远征准备界面 / 战前准备手牌中该人物的卡片显示你的图。

**关于同一模板的两个人**：两个不同的 Person 可以共用同一个 `AppearanceKey`（于是共用同一张图），也可以各自用不同的键（就是两条配置行）。
它们是两个独立人物这一点不受影响——身份永远由 `PersonId` 决定，配置只决定用哪张图。

---

## 7. 新增一个 Hero 外观

完全同理，只是键换成 `HeroId`：

1. 导入 PNG（同 §6 第 1 步）；
2. `AppearanceKey` 填 Hero 的 `HeroId`（例如 `hero_001`，见 `Assets/Resources/ScriptableObjects/Heroes/`）；
3. 填 `Default Body Sprite`（想让英雄选择界面单独用另一张图，就再填 `Hero Select Sprite Override`）；
4. Play 验证：
   - **Journey**：带该英雄出征 → 行走画面英雄成员显示你的图；
   - **Hero Selection**：主菜单 → 新游戏 → 英雄选择界面该英雄卡显示你的图。

Hero 在 Journey 与 Hero Selection 读取的是**同一条配置行**。

---

## 8. 以后替换图时：不要改哪些 Gameplay 对象

视觉配置**只允许**动这些：

- ✅ `PersonVisualBindingCatalog.asset` 里的 Sprite / Override / Scale / Offset；
- ✅ `CharCard.prefab` / `HeroSelectCard.prefab` / `JourneyPartyMemberView.prefab` / `UnitCard.prefab` 里
  **只属于表现的** `Anchor_Visual` 挂点与 `Body_Visual` 的 RectTransform 位置 / 大小；
- ✅ `BattleUI` 上的表现参数（`playerBodyScale` / `enemyBodyScale` / `unitBodyVisualOffset` 等）。

**不要改**（改了会影响玩法，属于 Gameplay Authority）：

- ❌ 任何 Scene / Prefab 上的 `Row` / `Column` / 棋盘占位 / 排 RectTransform（`playerFrontRow` 等）——战斗站位与移动规则的基础；
- ❌ `BattleUI` 的 Formation / Active Roster / `_pendingRows` / `_pendingCols` 相关字段；
- ❌ `BattlePrepCard`、`Button`、`CanvasGroup`、`EventTrigger`、拖拽相关脚本（Prep 的选人 / 上阵靠它们）；
- ❌ `ExpeditionUI` 的选人规则字段（`cardPanel` / `btnConfirm` / 上限 / 占用判定）；
- ❌ `PlayerCollection`、`CharacterInstance` / `HeroInstance` 的任何身份字段（`PersonId` 尤其不能动）；
- ❌ `SaveData` / `SaveVersion` / SO Schema（`PersonDefinitionCatalog` 等正式数据资产）。

另外两条硬规则：

- **视觉节点不允许打开 `Raycast Target`**：`Body_Visual` 必须保持 `raycastTarget = false`，交互只由卡片 Root 的 `Button` 承担。
  如果打开，玩家点不到卡片 / 点不到英雄。
- **不要给视觉节点加 `Button` 组件**；也不要改 Root Button 的 `Target Graphic` 指向（否则高亮/点击反馈会变）。

---

## 9. 故障排查

| 现象 | 原因 / 处理 |
|---|---|
| 该人物显示的是占位图 | Console 会给出 `[PersonVisual] 外观键 xxx 没有可用的视觉绑定` → 检查 `AppearanceKey` 拼写、`DefaultBodySprite` 是否为空 |
| Console 提示「未找到配置资产」 | `PersonVisualBindingCatalog.asset` 被移动/删除 → 放回 `Assets/Resources/ScriptableObjects/Presentation/` |
| 提示「配置存在重复 AppearanceKey：X（生效：行 a；已忽略：行 b）」 | 同一键有两行 → **始终使用 a 行**；删掉多余行即可（不会自动改你的配置） |
| 提示「存在无效配置行：AppearanceKey 为空（行 n）」 | 该行只填了 Sprite 没填键 → 该行**永远不会生效**；补全 `Appearance Key` 或删除该行 |
| 某个表面和别人不一样 | 该行填了那个表面的 Override → 清空即可回退到 `DefaultBodySprite` |
| 图被拉伸 / 变形 | 说明有人改了 `Body_Visual` 的 `Preserve Aspect` 或把 `Image Type` 改成了 Sliced/Tiled → 改回 `Simple` + 勾上 `Preserve Aspect` |
| 卡片点不动 / 选不中人物 | 检查 `Body_Visual.raycastTarget` 是否被打开，或该节点是否被加了 `Button` |
| 换了图但画面没变 | 确认改的是**当前生效**的那一行（键要精确匹配），并重新 Play |

---

## 10. 相关代码位置（供技术同学）

| 文件 | 职责 |
|---|---|
| `Assets/Scripts/UI/PersonVisualBinding.cs` | 表面枚举 `PersonVisualSurface`、配置行 `PersonVisualBindingEntry`、结果 `PersonVisualResult` |
| `Assets/Scripts/UI/PersonVisualBindingCatalog.cs` | 唯一配置面（ScriptableObject，Resources 固定路径） |
| `Assets/Scripts/UI/PersonVisualResolver.cs` | 统一解析契约：`PersonId → exact Person → AppearanceKey → binding → placeholder` |
| `Assets/Scripts/UI/PersonVisualHostBinder.cs` | 把解析结果写进 `Anchor_Visual/Body_Visual`（不动 Button / 不打开 raycast） |
| `Assets/Scripts/UI/ExpeditionJourneyPersonVisual.cs` | WP-E 兼容层（正式路径已转交统一解析器） |
